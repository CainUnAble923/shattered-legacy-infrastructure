// cc-P103: the server half of our TazUO patch's ClientCommand (enhanced packet 0xCE id 4). Notes: shard-migration
// notes/cc-P103-tazuo-client-command.md. The client half is client/patches/0001-enhanced-packet-client-command.patch.
//
// TazUO's enhanced packets (TazUO 3212623f, src/ClassicUO.Client/Network/EnhancedPackets/EnhancedPackets.md):
//   server -> client  [0xCE][u16 length][u16 id][u16 version][payload]
//   client -> server  [0xCE][u16 length][u16 id][payload]
// The handshake: at connect the server sends id 1 (EnableEnhancedPacket) listing the ids it wants; a TazUO client with
// enhanced packets on (its opt-in, ServerSettings.EnableEnhancedPackets) answers id 1. Our patched client also
// answers id 4 when id 4 was listed: it knows ClientCommand. A stock TazUO does not know id 4 and never answers it, and
// other clients answer nothing. So Send goes only to a client that answered id 4; everyone else keeps the gump's words.
//
// cc-P147, shard buttons (client/patches/0003-shard-buttons-achievements.patch): the other direction. Our client's
// Achievements button (paperdoll and top bar) sends id 4 WITH a command, "achievements"; the bare id 4 stays the
// acknowledgement above. The server answers only a client that acknowledged id 4 on this connection, and only a
// command on ShardButtons with a page registered for it (RegisterShardButton). Anything else does nothing, and is
// logged once per command name.
//
// The shard already sends a custom variable-length packet (0xF4, ClusterFExplorationManager) to every client at
// connect; 0xCE id 1 follows the same rule. clusterf.tazuo.enhancedPackets False turns the handshake off.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server.Logging;
using Server.Network;

namespace Server.TazUO;

public enum ClientCommandResult
{
    Sent,
    NotCapable,   // not a client that answered the handshake for id 4: nothing sent
    Refused       // off the whitelist, or not plain ASCII: nothing sent
}

/// <summary>cc-P147: what came of a shard button the client pressed.</summary>
public enum ShardButtonResult
{
    Opened,
    NotCapable,   // the client never acknowledged id 4 on this connection, or has no character: ignored
    Refused       // not on ShardButtons, no page registered, or not plain ASCII: ignored
}

public static class TazUOClientCommand
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TazUOClientCommand));

    public const byte PacketId = 0xCE;
    public const ushort EnableEnhancedPacket = 1;
    public const ushort TazUOIdentifier = 2;
    public const ushort ClientCommand = 4;
    public const int MaxCommandLength = 100;

    /// <summary>The patch's whitelist, the same seven names in the same order (EnhancedClientCommand.cs).</summary>
    public static readonly string[] Whitelist = ["optlink", "paperdoll", "marktile", "radius", "worldmap", "journal", "options"];

    /// <summary>cc-P147: the shard buttons the client may press, the same list as the patch's ShardButtons.All.</summary>
    public static readonly string[] ShardButtons = ["achievements"];

    private static readonly Dictionary<string, Action<Mobile>> _shardPages = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _loggedRefusals = new(StringComparer.OrdinalIgnoreCase);

    public static bool HandshakeEnabled { get; set; } = true;

    private sealed class Seen
    {
        public bool TazUO;
        public bool ClientCommand;
        public string? Version;
    }

    // Per connection; collected with the NetState.
    private static readonly ConditionalWeakTable<NetState, Seen> _seen = new();

    public static unsafe void Configure()
    {
        HandshakeEnabled = ServerConfiguration.GetOrUpdateSetting("clusterf.tazuo.enhancedPackets", true);
        IncomingPackets.Register(PacketId, 0, false, &OnEnhancedPacket);
        EventSink.Connected += OnConnected;
    }

    private static void OnConnected(Mobile m)
    {
        if (HandshakeEnabled && m.NetState is { } ns)
        {
            SendHandshake(ns);
        }
    }

    /// <summary>0xCE id 1, version 0, asking for id 4 (ClientCommand).</summary>
    public static void SendHandshake(NetState ns)
    {
        var writer = new SpanWriter(stackalloc byte[11]);
        writer.Write(PacketId);
        writer.Write((ushort)11);
        writer.Write(EnableEnhancedPacket);
        writer.Write((ushort)0);     // version
        writer.Write((ushort)1);     // count
        writer.Write(ClientCommand);
        ns.Send(writer.Span);
    }

    public static void OnEnhancedPacket(NetState ns, SpanReader reader)
    {
        if (reader.Remaining < 2)
        {
            return;
        }

        var id = reader.ReadUInt16();
        var seen = _seen.GetOrCreateValue(ns);
        switch (id)
        {
            case EnableEnhancedPacket:
                seen.TazUO = true;
                break;
            case ClientCommand when reader.Remaining > 0:
                HandleShardButton(ns, reader.ReadAscii());   // cc-P147: a shard button, not the acknowledgement
                break;
            case ClientCommand:
                seen.TazUO = true;
                seen.ClientCommand = true;
                break;
            case TazUOIdentifier:
                seen.TazUO = true;
                seen.Version = reader.ReadAscii();
                break;
        }
    }

    /// <summary>cc-P147: the page a shard button opens. Only a name on ShardButtons can be registered.</summary>
    public static void RegisterShardButton(string command, Action<Mobile> open)
    {
        if (Array.IndexOf(ShardButtons, command) < 0)
        {
            throw new ArgumentException($"{command} is not on TazUOClientCommand.ShardButtons", nameof(command));
        }

        _shardPages[command] = open;
    }

    /// <summary>cc-P147: a shard button pressed in our client. Opens its page for a client that acknowledged id 4.</summary>
    public static ShardButtonResult HandleShardButton(NetState ns, string? command)
    {
        if (!CanRunCommands(ns) || ns.Mobile is not { Deleted: false } mobile)
        {
            LogRefusalOnce("from a client that never acknowledged id 4", command);
            return ShardButtonResult.NotCapable;
        }

        if (command == null || command.Length > MaxCommandLength || Array.IndexOf(ShardButtons, command) < 0 ||
            !_shardPages.TryGetValue(command, out var open))
        {
            LogRefusalOnce("not a shard button", command);
            return ShardButtonResult.Refused;
        }

        open(mobile);
        return ShardButtonResult.Opened;
    }

    private static void LogRefusalOnce(string why, string? command)
    {
        var head = (command ?? "").Split(' ', 2)[0];
        if (_loggedRefusals.Add(why + ":" + (head.Length > 32 ? head[..32] : head)))
        {
            logger.Information("TazUO shard button ignored ({Why}): {Command}", why, head);
        }
    }

    public static bool IsTazUO(NetState? ns) => ns != null && _seen.TryGetValue(ns, out var s) && s.TazUO;

    public static bool CanRunCommands(NetState? ns) => ns != null && _seen.TryGetValue(ns, out var s) && s.ClientCommand;

    /// <summary>The command's first word is on the whitelist, and the line is plain printable ASCII.</summary>
    public static bool IsAllowed(string? command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > MaxCommandLength)
        {
            return false;
        }

        foreach (var c in command)
        {
            if (c < 0x20 || c > 0x7E)
            {
                return false;
            }
        }

        var head = command.Trim().Split(' ', 2)[0];
        return Array.IndexOf(Whitelist, head.ToLowerInvariant()) >= 0;
    }

    /// <summary>Runs a whitelisted client command (no leading "-") in the player's TazUO, if it can.</summary>
    public static ClientCommandResult Send(Mobile? mobile, string command)
    {
        if (!IsAllowed(command))
        {
            logger.Warning("TazUO ClientCommand refused before sending: {Command}", command);
            return ClientCommandResult.Refused;
        }

        var ns = mobile?.NetState;
        if (!CanRunCommands(ns))
        {
            return ClientCommandResult.NotCapable;
        }

        var text = command.Trim();
        var length = 7 + text.Length + 1;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write(PacketId);
        writer.Write((ushort)length);
        writer.Write(ClientCommand);
        writer.Write((ushort)0);     // version
        writer.WriteAsciiNull(text);
        ns!.Send(writer.Span);
        return ClientCommandResult.Sent;
    }
}
