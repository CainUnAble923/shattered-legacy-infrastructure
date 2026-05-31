using System;
using System.Buffers;
using System.Collections;
using System.IO;
using System.IO.Compression;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFExplorationManager
//
// Tracks which 8×8-tile chunks each character has walked through and sends
// that state to the ClusterF custom client (ClassicUO fork) via packet 0xF4.
//
// Called from:
//   • ClusterFExplorationHook.OnMove  — marks chunks explored as player walks
//   • ClusterFExplorationHook.OnLogin — full sync on login
//
// Data is stored in ClusterFAccountPersistence under the character's serial.
// Chunk size = 8×8 tiles (matches ore depletion grid).
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFExplorationManager
{
    public const int ChunkSize = 8;

    // Map dimensions in tiles per facet index (mirrors client FogOfWarManager).
    private static readonly (int W, int H)[] MapSizes =
    {
        (6144, 4096), // 0  Felucca
        (6144, 4096), // 1  Trammel
        (2304, 1600), // 2  Ilshenar
        (2560, 2048), // 3  Malas
        (1448, 1448), // 4  Tokuno
        (1280, 4096), // 5  TerMur
    };

    // Exploration radius in chunks — how many chunks around the player are
    // revealed per step. 2 chunks = 16 tiles, roughly the visible screen radius.
    private const int ExploreRadius = 2;

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Called from the movement hook. Marks chunks within ExploreRadius of the
    /// player as explored and sends delta packets for any newly revealed chunks.
    /// </summary>
    public static void OnPlayerMoved(PlayerMobile pm)
    {
        if (pm?.NetState == null || pm.Map == null || pm.Map == Map.Internal) return;

        int facet = pm.Map.MapID;
        if (facet < 0 || facet >= MapSizes.Length) return;

        var data   = ClusterFAccountPersistence.GetOrCreate(pm.Account);
        var chunks = data.GetOrCreateExploration(pm.Serial, facet, ChunkWidth(facet), ChunkHeight(facet));

        int centerCX = pm.X / ChunkSize;
        int centerCY = pm.Y / ChunkSize;

        var delta = new System.Collections.Generic.List<(ushort cx, ushort cy)>();

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

    /// <summary>
    /// Called on login. Sends the full explored BitArray for each facet.
    /// </summary>
    public static void OnPlayerLogin(PlayerMobile pm)
    {
        if (pm?.NetState == null) return;

        var data = ClusterFAccountPersistence.GetOrCreate(pm.Account);

        for (int facet = 0; facet < MapSizes.Length; facet++)
        {
            var chunks = data.GetOrCreateExploration(pm.Serial, facet, ChunkWidth(facet), ChunkHeight(facet));
            ClusterFExplorationPackets.SendFullExplored(pm.NetState, (byte)facet, chunks);
        }

        // Also send current mining depletion state for each facet.
        ClusterFDepletionSync.SendAllFacets(pm);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static int ChunkWidth(int facet)  => (MapSizes[facet].W + ChunkSize - 1) / ChunkSize;
    public static int ChunkHeight(int facet) => (MapSizes[facet].H + ChunkSize - 1) / ChunkSize;
}

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFExplorationPackets
//
// Builds and sends custom packet 0xF4 to the ClusterF client.
//
// Packet layout:
//   [0xF4]  1 byte  packet ID
//   [len]   2 bytes total packet length (big-endian, includes all header bytes)
//   [sub]   1 byte  subcommand (0x01 full explored / 0x02 delta / 0x03 full depleted / 0x04 delta depletion)
//   [facet] 1 byte  facet index
//   [dlen]  4 bytes payload length (big-endian)
//   [data]  N bytes payload
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFExplorationPackets
{
    private const byte PacketID  = 0xF4;
    private const int  HeaderLen = 1 + 2 + 1 + 1 + 4; // id + len + sub + facet + dlen

    // ── Exploration ───────────────────────────────────────────────────────────

    public static void SendFullExplored(NetState ns, byte facet, BitArray explored)
    {
        byte[] raw      = ToByteArray(explored);
        byte[] payload  = Compress(raw);
        Send(ns, 0x01, facet, payload);
    }

    public static void SendDeltaExplored(
        NetState ns,
        byte facet,
        System.Collections.Generic.List<(ushort cx, ushort cy)> chunks)
    {
        // 4 bytes per chunk: uint16 cx + uint16 cy
        byte[] payload = new byte[chunks.Count * 4];
        for (int i = 0; i < chunks.Count; i++)
        {
            int offset = i * 4;
            payload[offset + 0] = (byte)(chunks[i].cx >> 8);
            payload[offset + 1] = (byte)(chunks[i].cx & 0xFF);
            payload[offset + 2] = (byte)(chunks[i].cy >> 8);
            payload[offset + 3] = (byte)(chunks[i].cy & 0xFF);
        }
        Send(ns, 0x02, facet, payload);
    }

    // ── Mining depletion ──────────────────────────────────────────────────────

    public static void SendFullDepleted(NetState ns, byte facet, BitArray depleted)
    {
        byte[] raw     = ToByteArray(depleted);
        byte[] payload = Compress(raw);
        Send(ns, 0x03, facet, payload);
    }

    public static void SendDeltaDepletion(NetState ns, byte facet, ushort cx, ushort cy, bool depleted)
    {
        // 5 bytes: uint16 cx + uint16 cy + byte state
        byte[] payload =
        {
            (byte)(cx >> 8), (byte)(cx & 0xFF),
            (byte)(cy >> 8), (byte)(cy & 0xFF),
            (byte)(depleted ? 1 : 0)
        };
        Send(ns, 0x04, facet, payload);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void Send(NetState ns, byte subcommand, byte facet, byte[] payload)
    {
        if (ns == null) return;

        int totalLen = HeaderLen + payload.Length;

        var writer = new SpanWriter(stackalloc byte[totalLen]);
        writer.Write(PacketID);
        writer.Write((ushort)totalLen);
        writer.Write(subcommand);
        writer.Write(facet);
        writer.Write((uint)payload.Length);
        writer.Write(payload);

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

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFDepletionSync
//
// Sends current mining depletion state to a player on login, and broadcasts
// delta updates to nearby players when a chunk's depletion state changes.
//
// Hooks into the existing ore regeneration/depletion system.
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFDepletionSync
{
    private const int BroadcastRange = 64; // tiles — players within this range get delta updates

    /// <summary>Send full depletion state for all facets to a player on login.</summary>
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

    /// <summary>
    /// Broadcast a depletion state change to all players near the chunk.
    /// Call this when ore is mined out or regenerates.
    /// </summary>
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

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFExplorationHook
//
// Wires exploration tracking into the player lifecycle.
// Register in ClusterFInitializer or an existing Initialize() method.
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFExplorationHook
{
    public static void Initialize()
    {
        EventSink.Movement += OnMovement;
        EventSink.Connected += OnConnected;
    }

    private static void OnMovement(MovementEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
            ClusterFExplorationManager.OnPlayerMoved(pm);
    }

    private static void OnConnected(Mobile m)
    {
        if (m is not PlayerMobile pm) return;
        // Small delay so client is fully initialized before receiving bulk data.
        Timer.DelayCall(TimeSpan.FromSeconds(2), () => ClusterFExplorationManager.OnPlayerLogin(pm));
    }
}
