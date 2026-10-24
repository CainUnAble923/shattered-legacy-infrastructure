// =============================================================================
// ShardMoongates.cs
// cc-P363: Carl Hamilton's spinning moongates in four colours (registry 46598-46617) as new gate items. OSI's moongates
// (0xF6C, 0xDDA, 0x1FD4), pinned's Moongate and PublicMoongate, and every gate already in the world keep their IDs and
// look: these are new IDs beside them.
//
// ShardMoongate     pinned's Moongate (its delay, confirmation, young/murderer/sigil rules and pets) with a Colour that
//                   picks the art, two options of ours, and a staff [add: [add ShardMoongate Red
//                     LockTag   only a carrier of the matching ShardKey passes (ShardKeys.cs): the League's trial gates
//                     Network   "guildhalls": the gate is a hub; using it opens the list of guild halls to travel to
// The guild hall network ([ShardGates guildhalls [dryrun] | clear | status], Administrator; nothing is placed until
// it is run):
//   at the League's headquarters (its hall's Travel point, P281): a White hub gate, to every other hall;
//   at every other hall's Travel point: a Blue gate back to the headquarters.
// Travellers arrive on the destination's gate, as on OSI's public moongates; arriving does not trigger it.
// The Valley of Eodon's public moongate (P198, EodonGeneration) takes the Red look when [GenEodon places it.
// A client without the package update draws OSI's "UNUSED" placeholder for these IDs.
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShardMoongate : Moongate, IShardLock
{
    public const string GuildHallNetwork = "guildhalls";

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _lockTag;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _lockName;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _network;

    [Constructible]
    public ShardMoongate() : this(ShardGateColor.Blue)
    {
    }

    [Constructible]
    public ShardMoongate(ShardGateColor color) : this(color, Point3D.Zero, null)
    {
    }

    public ShardMoongate(ShardGateColor color, Point3D target, Map? targetMap) : base(target, targetMap, false)
    {
        _color = color;
        _lockTag = "";
        _lockName = "";
        _network = "";
        ItemID = ShardItemArt.GateItemId(color);
        Hue = 0; // the art carries its colour; a hue would turn the whole vortex one shade
    }

    public override string DefaultName => $"a {_color.ToString().ToLowerInvariant()} moongate";

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public ShardGateColor Color
    {
        get => _color;
        set
        {
            ItemID = ShardItemArt.GateItemId(value); // throws for anything but the four: [props then says it was not set
            _color = value;
            this.MarkDirty();
            InvalidateProperties();
        }
    }

    public bool IsHub => _network == GuildHallNetwork;

    /// <summary>Whether this mobile may pass: no lock, staff, or carrying the gate's key.</summary>
    public bool MayPass(Mobile m) =>
        string.IsNullOrEmpty(_lockTag) || m.AccessLevel >= AccessLevel.GameMaster || ShardKeys.FindKey(m, this) != null;

    public override void CheckGate(Mobile m, int range)
    {
        if (!MayPass(m))
        {
            m.SendMessage(0x22, $"The gate will not open for you. It answers to {(_lockName.Length > 0 ? _lockName : "a key")}.");
            return;
        }

        base.CheckGate(m, range);
    }

    public override void DelayCallback(Mobile from, int range)
    {
        if (IsHub)
        {
            if (ValidateUse(from, false) && from.InRange(this, range) && from is PlayerMobile pm)
            {
                pm.SendGump(new ShardGateNetworkGump(pm, this));
            }

            return;
        }

        base.DelayCallback(from, range);
    }

    /// <summary>A hub's trip to one hall (ShardGateNetworkGump): pinned's own UseGate, to that hall.</summary>
    public void TravelTo(Mobile m, Point3D to, Map map)
    {
        if (!ValidateUse(m, true) || !MayPass(m))
        {
            return;
        }

        var (oldTarget, oldMap) = (Target, TargetMap);

        try
        {
            Target = to;
            TargetMap = map;
            UseGate(m);
        }
        finally
        {
            Target = oldTarget;
            TargetMap = oldMap;
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization() => ItemID = ShardItemArt.GateItemId(_color);
}

/// <summary>The hub's list: every guild hall but the one this gate stands at, by its guild's name and town.</summary>
public class ShardGateNetworkGump : Gump
{
    private const int BtnHallBase = 100;

    private readonly ShardMoongate _gate;
    private readonly List<ClusterFGuildHallPlacementData.Hall> _halls;

    public ShardGateNetworkGump(PlayerMobile pm, ShardMoongate gate) : base(120, 100)
    {
        pm.CloseGump<ShardGateNetworkGump>();
        _gate = gate;
        _halls = ShardGates.Destinations(gate);

        var h = 70 + _halls.Count * 24;
        AddBackground(0, 0, 340, h, 9270);
        RequirementStyle.AddSolidPanel(this, 340, h);
        AddLabel(20, 16, RequirementStyle.HeadingHue, "Where will the gate take you?");

        for (var i = 0; i < _halls.Count; i++)
        {
            AddButton(20, 44 + i * 24, 4005, 4007, BtnHallBase + i);
            AddLabel(56, 46 + i * 24, RequirementStyle.InfoHue, $"{_halls[i].Guild} ({_halls[i].Town})");
        }
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        var i = info.ButtonID - BtnHallBase;

        if (state.Mobile is PlayerMobile pm && !_gate.Deleted && i >= 0 && i < _halls.Count)
        {
            _gate.TravelTo(pm, _halls[i].Travel, Map.Maps[_halls[i].MapIndex]);
        }
    }
}

public static class ShardGates
{
    public const string CollectionKey = "shardgates";
    public const string HeadquartersKey = "league";

    public static ClusterFGuildHallPlacementData.Hall? Hall(string key) =>
        Array.Find(ClusterFGuildHallPlacementData.Halls, h => h.Key == key);

    /// <summary>Where a hub sends people: every hall but the one the gate stands at (its Travel point, its facet).</summary>
    public static List<ClusterFGuildHallPlacementData.Hall> Destinations(Item gate)
    {
        var list = new List<ClusterFGuildHallPlacementData.Hall>();

        foreach (var h in ClusterFGuildHallPlacementData.Halls)
        {
            if (!(Map.Maps[h.MapIndex] == gate.Map && h.Travel == gate.Location))
            {
                list.Add(h);
            }
        }

        return list;
    }

    public static void Initialize() =>
        CommandSystem.Register("ShardGates", AccessLevel.Administrator, OnCommand);

    /// <summary>A League trial gate (Black): only a carrier of that chapter's trial key passes (ShardKeys.LeagueTrialGateKey).</summary>
    public static ShardMoongate LeagueTrialGate(int chapter, Point3D to, Map map) =>
        new(ShardGateColor.Black, to, map)
        {
            LockTag = ShardKeys.TrialGateTag(chapter),
            LockName = $"a League trial key (chapter {chapter})"
        };

    /// <summary>The guild hall network: a White hub at the League's headquarters, a Blue gate home at every other hall.</summary>
    public static List<string> PlaceGuildHallNetwork(bool dryRun)
    {
        var lines = new List<string>();
        var hq = Hall(HeadquartersKey);

        if (hq == null)
        {
            lines.Add("No League headquarters in ClusterFGuildHallPlacementData.Halls: nothing placed.");
            return lines;
        }

        var hqMap = Map.Maps[hq.MapIndex];

        if (!dryRun)
        {
            var removed = WeakEntityCollection.Delete(CollectionKey);
            if (removed > 0)
            {
                lines.Add($"{removed} gates of an earlier run removed.");
            }
        }

        foreach (var h in ClusterFGuildHallPlacementData.Halls)
        {
            var map = Map.Maps[h.MapIndex];
            var hub = h.Key == HeadquartersKey;
            var what = hub ? "White hub gate (to every hall)" : $"Blue gate to the League's headquarters";

            if (map == null || h.Travel == Point3D.Zero)
            {
                lines.Add($"{h.Key}: no facet or Travel point, skipped.");
                continue;
            }

            if (dryRun)
            {
                lines.Add($"{h.Key}: would place a {what} at {h.Travel} ({map}).");
                continue;
            }

            var gate = hub
                ? new ShardMoongate(ShardGateColor.White) { Network = ShardMoongate.GuildHallNetwork }
                : new ShardMoongate(ShardGateColor.Blue, hq.Travel, hqMap);
            WeakEntityCollection.Add(CollectionKey, gate);
            gate.MoveToWorld(h.Travel, map);
            lines.Add($"{h.Key}: {what} at {h.Travel} ({map}).");
        }

        return lines;
    }

    [Usage("ShardGates <guildhalls [dryrun] | clear | status>")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Replaces, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "The guild hall gate network: a White hub gate at the League's headquarters to every hall, and a Blue gate home at every other hall's Travel point. Removes its own earlier gates and places them again; 'clear' removes them; with no argument it reports how many stand.")]
    [Description("cc-P363: the guild hall gate network (a White hub at the League's headquarters, a Blue gate home at every other hall).")]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        List<string> lines = verb switch
        {
            "guildhalls" => PlaceGuildHallNetwork(e.Length > 1 && e.GetString(1).Equals("dryrun", StringComparison.OrdinalIgnoreCase)),
            "clear"      => [$"{WeakEntityCollection.Delete(CollectionKey)} gates removed."],
            "status"     => [$"{WeakEntityCollection.GetCollection(CollectionKey).Count} gates placed by [ShardGates."],
            _            => new List<string> { "Usage: [ShardGates <guildhalls [dryrun] | clear | status>" }
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }
}
