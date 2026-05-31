using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Server.Engines.Harvest;
using Server.Mobiles;
using Server.Network;

namespace Server;

// =============================================================================
// ClusterFExplorationManager
//
// Tracks which 8x8-tile chunks each character has walked through and sends
// that state to the ClusterF custom client (ClassicUO fork) via packet 0xF4.
//
// Packet 0xF4 subcommands:
//   0x01  full explored bitfield (gzip-compressed)
//   0x02  delta explored  (list of newly-revealed cx/cy pairs)
//   0x03  full depleted bitfield (gzip-compressed)
//   0x04  delta depletion (single chunk + bool state)
//
// Data is stored in ClusterFAccountData under the character serial.
// Chunk size = 8x8 tiles (matches HarvestDefinition.BankWidth/BankHeight).
// =============================================================================

public static class ClusterFExplorationManager
{
    public const int ChunkSize = 8;

    private static readonly (int W, int H)[] MapSizes =
    {
        (6144, 4096), // 0  Felucca
        (6144, 4096), // 1  Trammel
        (2304, 1600), // 2  Ilshenar
        (2560, 2048), // 3  Malas
        (1448, 1448), // 4  Tokuno
        (1280, 4096), // 5  TerMur
    };

    private const int ExploreRadius = 2;

    // -------------------------------------------------------------------------

    public static void OnPlayerMoved(PlayerMobile pm)
    {
        if (pm?.NetState == null || pm.Map == null || pm.Map == Map.Internal) return;

        int facet = pm.Map.MapID;
        if (facet < 0 || facet >= MapSizes.Length) return;

        var data   = ClusterFAccountPersistence.GetOrCreate(pm.Account);
        var chunks = data.GetOrCreateExploration(pm.Serial, facet, ChunkWidth(facet), ChunkHeight(facet));

        int centerCX = pm.X / ChunkSize;
        int centerCY = pm.Y / ChunkSize;

        var delta = new List<(ushort cx, ushort cy)>();

        for (int dy = -ExploreRadius; dy <= ExploreRadius; dy++)
        {
            for (int dx = -ExploreRadius; dx <= ExploreRadius; dx++)
            {
                int cx = centerCX + dx;
                int cy = centerCY + dy;

                if (cx < 0 || cx >= ChunkWidth(facet)) continue;
                if (cy < 0 || cy >= ChunkHeight(facet)) continue;

                int idx = cy * ChunkWidth(facet) + cx;
                if (!chunks[idx])
                {
                    chunks[idx] = true;
                    delta.Add(((ushort)cx, (ushort)cy));
                }
            }
        }

        if (delta.Count > 0)
            ClusterFExplorationPackets.SendDeltaExplored(pm.NetState, (byte)facet, delta);
    }

    public static void OnPlayerLogin(PlayerMobile pm)
    {
        if (pm?.NetState == null) return;

        var data = ClusterFAccountPersistence.GetOrCreate(pm.Account);

        for (int facet = 0; facet < MapSizes.Length; facet++)
        {
            var chunks = data.GetOrCreateExploration(pm.Serial, facet, ChunkWidth(facet), ChunkHeight(facet));
            ClusterFExplorationPackets.SendFullExplored(pm.NetState, (byte)facet, chunks);
        }

        ClusterFDepletionSync.SendAllFacets(pm);
    }

    public static int ChunkWidth(int facet)  => (MapSizes[facet].W + ChunkSize - 1) / ChunkSize;
    public static int ChunkHeight(int facet) => (MapSizes[facet].H + ChunkSize - 1) / ChunkSize;
}

// =============================================================================
// ClusterFExplorationPackets
//
// Packet layout (all 0xF4):
//   [0xF4]  1 byte   packet ID
//   [len]   2 bytes  total length (big-endian, includes header)
//   [sub]   1 byte   subcommand (0x01-0x04)
//   [facet] 1 byte   facet index
//   [dlen]  4 bytes  payload length (big-endian)
//   [data]  N bytes  payload
// =============================================================================

public static class ClusterFExplorationPackets
{
    private const byte PacketID  = 0xF4;
    private const int  HeaderLen = 1 + 2 + 1 + 1 + 4; // id + totallen + sub + facet + dlen

    public static void SendFullExplored(NetState ns, byte facet, BitArray explored)
    {
        byte[] raw     = ToByteArray(explored);
        byte[] payload = Compress(raw);
        Send(ns, 0x01, facet, payload);
    }

    public static void SendDeltaExplored(NetState ns, byte facet, List<(ushort cx, ushort cy)> chunks)
    {
        byte[] payload = new byte[chunks.Count * 4];
        for (int i = 0; i < chunks.Count; i++)
        {
            int off = i * 4;
            payload[off + 0] = (byte)(chunks[i].cx >> 8);
            payload[off + 1] = (byte)(chunks[i].cx & 0xFF);
            payload[off + 2] = (byte)(chunks[i].cy >> 8);
            payload[off + 3] = (byte)(chunks[i].cy & 0xFF);
        }
        Send(ns, 0x02, facet, payload);
    }

    public static void SendFullDepleted(NetState ns, byte facet, BitArray depleted)
    {
        byte[] raw     = ToByteArray(depleted);
        byte[] payload = Compress(raw);
        Send(ns, 0x03, facet, payload);
    }

    public static void SendDeltaDepletion(NetState ns, byte facet, ushort cx, ushort cy, bool depleted)
    {
        byte[] payload =
        {
            (byte)(cx >> 8), (byte)(cx & 0xFF),
            (byte)(cy >> 8), (byte)(cy & 0xFF),
            (byte)(depleted ? 1 : 0)
        };
        Send(ns, 0x04, facet, payload);
    }

    private static void Send(NetState ns, byte subcommand, byte facet, byte[] payload)
    {
        if (ns == null || ns.CannotSendPackets()) return;

        int totalLen = HeaderLen + payload.Length;
        var buffer   = new byte[totalLen];
        var writer   = new SpanWriter(buffer);

        writer.Write(PacketID);
        writer.Write((ushort)totalLen);
        writer.Write(subcommand);
        writer.Write(facet);
        writer.Write((uint)payload.Length);
        writer.Write((ReadOnlySpan<byte>)payload);

        ns.Send(writer.Span);
    }

    private static byte[] ToByteArray(BitArray bits)
    {
        byte[] bytes = new byte[(bits.Length + 7) / 8];
        bits.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using var gz     = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        gz.Write(data, 0, data.Length);
        gz.Flush();
        return output.ToArray();
    }
}

// =============================================================================
// ClusterFDepletionSync
//
// Sends current mining depletion state to a player on login, and broadcasts
// delta updates to nearby players when a chunk's depletion state changes.
// =============================================================================

public static class ClusterFDepletionSync
{
    private const int BroadcastRange = 64; // tiles

    public static void SendAllFacets(PlayerMobile pm)
    {
        if (pm?.NetState == null) return;

        for (int facet = 0; facet < 6; facet++)
        {
            var bits = ClusterFOreDepletionData.GetDepletedBits(facet);
            if (bits != null)
                ClusterFExplorationPackets.SendFullDepleted(pm.NetState, (byte)facet, bits);
        }
    }

    public static void BroadcastDelta(Map map, int tileX, int tileY, bool depleted)
    {
        if (map == null || map == Map.Internal) return;

        byte   facet = (byte)map.MapID;
        ushort cx    = (ushort)(tileX / ClusterFExplorationManager.ChunkSize);
        ushort cy    = (ushort)(tileY / ClusterFExplorationManager.ChunkSize);

        foreach (var ns in NetState.Instances)
        {
            if (ns?.Mobile is not PlayerMobile pm) continue;
            if (pm.Map != map) continue;
            if (Math.Abs(pm.X - tileX) > BroadcastRange) continue;
            if (Math.Abs(pm.Y - tileY) > BroadcastRange) continue;

            ClusterFExplorationPackets.SendDeltaDepletion(ns, facet, cx, cy, depleted);
        }
    }
}

// =============================================================================
// ClusterFOreDepletionData
//
// Reads current depletion state from the live HarvestBank dictionary
// maintained by Mining.System.OreAndStone.
// =============================================================================

public static class ClusterFOreDepletionData
{
    private static readonly Map?[] FacetMaps =
    {
        Map.Felucca,
        Map.Trammel,
        Map.Ilshenar,
        Map.Malas,
        Map.Tokuno,
        Map.TerMur,
    };

    private static readonly (int W, int H)[] MapSizes =
    {
        (6144, 4096),
        (6144, 4096),
        (2304, 1600),
        (2560, 2048),
        (1448, 1448),
        (1280, 4096),
    };

    private const int ChunkSize = 8;

    public static BitArray? GetDepletedBits(int facet)
    {
        if (facet < 0 || facet >= FacetMaps.Length) return null;
        var map = FacetMaps[facet];
        if (map == null || map == Map.Internal) return null;

        var def = Mining.System?.OreAndStone;
        if (def == null) return null;
        if (!def.Banks.TryGetValue(map, out var banks) || banks.Count == 0) return null;

        var (w, h) = MapSizes[facet];
        int cw   = (w + ChunkSize - 1) / ChunkSize;
        int ch   = (h + ChunkSize - 1) / ChunkSize;
        var bits = new BitArray(cw * ch, false);

        foreach (var (point, bank) in banks)
        {
            if (bank.Current == 0)
            {
                int idx = point.Y * cw + point.X;
                if (idx >= 0 && idx < bits.Length)
                    bits[idx] = true;
            }
        }

        return bits;
    }
}

// =============================================================================
// ClusterFExplorationHook
//
// Wires exploration tracking into the player lifecycle.
// ModernUO auto-discovers the Initialize() static method and calls it after
// world load.
//
// Also starts a periodic depletion resync timer so that when HarvestBanks
// regenerate (ModernUO resets Current lazily on next access), all online
// players eventually see the haze clear from the World Map without needing
// to relog.  The resync period is intentionally longer than the ore regen
// window (typically ~10 min) so the push is cheap and infrequent.
// =============================================================================

public static class ClusterFExplorationHook
{
    // How often to push a full depletion resync to all logged-in players.
    // Must be longer than the ore bank regen timer so cleared banks are caught.
    // Default ore regen ≈ 10 min.  We resync every 5 min so a newly emptied
    // chunk shows up within one cycle for players who were offline at the time.
    private static readonly TimeSpan DepletionResyncInterval = TimeSpan.FromMinutes(5);

    public static void Initialize()
    {
        EventSink.Movement  += OnMovement;
        EventSink.Connected += OnConnected;

        // Periodic full depletion resync — clears stale haze after ore regenerates.
        Timer.DelayCall(DepletionResyncInterval, OnDepletionResyncTick);
    }

    private static void OnMovement(MovementEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
            ClusterFExplorationManager.OnPlayerMoved(pm);
    }

    private static void OnConnected(Mobile m)
    {
        if (m is PlayerMobile pm)
            // Small delay so client is fully initialized before receiving bulk data.
            Timer.DelayCall(TimeSpan.FromSeconds(2), () => ClusterFExplorationManager.OnPlayerLogin(pm));
    }

    /// <summary>
    /// Pushes the current full depletion state to every online player, then
    /// reschedules itself.  This keeps the World Map haze accurate even after
    /// ore banks regenerate (which happens lazily in ModernUO — banks reset
    /// Current to Maximum on next access, not on a dedicated timer event).
    /// </summary>
    private static void OnDepletionResyncTick()
    {
        foreach (var ns in NetState.Instances)
        {
            if (ns?.Mobile is PlayerMobile pm)
                ClusterFDepletionSync.SendAllFacets(pm);
        }

        // Reschedule the next tick.
        Timer.DelayCall(DepletionResyncInterval, OnDepletionResyncTick);
    }
}
