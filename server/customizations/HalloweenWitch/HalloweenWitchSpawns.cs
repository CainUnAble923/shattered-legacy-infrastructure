// cc-P786: where the Halloween witch appears.
//
// The wild spots (HalloweenWitchTable.Spots): while the Halloween season is on (FestivalSeasons, the Festival stall's
// switch, which staff can force with [FestivalMarket season), one witch stands at each spot; a spot whose witch died
// gets a new one respawnMinutes later. Out of the season every witch the spots placed is taken away (her corpse, if
// any, is never touched). Sync runs on every world load (so an existing world gets them by itself), every
// checkMinutes, and on [HalloweenWitch sync; it is idempotent: one witch per spot however often it runs. The placed
// witches are tracked by serial in ClusterFSeasonalState ("halloween-witch") and each knows its spot (saved).
//
// The encounter: the witch's entry joins the random encounter table (cc-P776) whenever that table comes into force
// (EncounterTable.Supplement), so an encounters.json written before this change still gets it. It is a night
// encounter in the "halloween" season, an EncounterKind.Boss: she comes for the player.
//
// [HalloweenWitch status|reload|sync|clear] for staff.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Encounters;
using Server.Engines.RewardStores;

namespace Server.Mobiles;

public static class HalloweenWitchSpawns
{
    public const string StateName = "halloween-witch";

    public static Func<DateTime> Clock { get; set; } = () => Core.Now;

    /// <summary>Where a witch can stand near a spot, or null. Tests replace it (the test host has no map files).</summary>
    public static Func<Map, Point3D, Point3D?> FitSpot { get; set; } = (map, p) =>
        ClusterFRequisitionSeeder.Fit(map, p.X, p.Y, p.Z, out var loc) ? loc : null;

    // When each spot's witch died or was taken away (not saved: after a restart a spot is filled at once).
    private static readonly Dictionary<int, DateTime> _gone = new();

    public static HalloweenWitchTable Table => HalloweenWitchTable.Current;

    public static bool InSeason => Table.Enabled && FestivalSeasons.Current() == FestivalSeasons.Halloween;

    public static void Configure()
    {
        foreach (var line in HalloweenWitchTable.LoadOrWriteDefaults(HalloweenWitchTable.FilePath))
        {
            Console.WriteLine($"[HalloweenWitch] {line}");
        }

        HookEncounters();
        CommandSystem.Register("HalloweenWitch", AccessLevel.GameMaster, OnCommand);

        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            Report(Sync());
            Timer.DelayCall(TimeSpan.FromMinutes(Table.CheckMinutes), TimeSpan.FromMinutes(Table.CheckMinutes), () => Report(Sync()));
        });
    }

    private static void Report(List<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }

    // -- The wild spots -----------------------------------------------------------------------------------------------

    public static HashSet<Serial> Placed() => ClusterFSeasonalState.Placed(StateName);

    public static List<HalloweenWitch> Witches() =>
        Placed().Select(s => World.FindMobile(s)).OfType<HalloweenWitch>().Where(w => !w.Deleted && w.Alive).ToList();

    public static HalloweenWitch? WitchAt(int spot) => Witches().FirstOrDefault(w => w.Spot == spot);

    /// <summary>A witch left the world (killed, or taken away): her spot waits respawnMinutes from now.</summary>
    public static void NoteGone(HalloweenWitch witch)
    {
        if (witch.Spot >= 0)
        {
            _gone[witch.Spot] = Clock();
        }
    }

    public static void ForgetDeaths() => _gone.Clear();

    /// <summary>In the season, a witch at every spot that has none and is past its wait; out of it, every placed witch away.</summary>
    public static List<string> Sync()
    {
        var placed = Placed();
        placed.RemoveWhere(s => World.FindMobile(s) is not HalloweenWitch { Deleted: false, Alive: true });

        if (!InSeason)
        {
            return Clear();
        }

        var lines = new List<string>();
        var now = Clock();
        var spots = Table.Spots;
        for (var i = 0; i < spots.Count; i++)
        {
            if (WitchAt(i) != null)
            {
                continue;
            }

            if (_gone.TryGetValue(i, out var at) && now < at + TimeSpan.FromMinutes(Table.RespawnMinutes))
            {
                continue;
            }

            var spot = spots[i];
            if (spot.Facet is not { } map || FitSpot(map, spot.Location) is not { } loc)
            {
                lines.Add($"[HalloweenWitch] Nothing fits at {spot.Name} ({spot.Location} on {spot.Map}); skipped. Tell a Game Master.");
                continue;
            }

            var witch = new HalloweenWitch { Spot = i, Home = loc, RangeHome = Table.WanderRange };
            witch.MoveToWorld(loc, map);
            placed.Add(witch.Serial);
            _gone.Remove(i);
            lines.Add($"[HalloweenWitch] A witch is out at {spot.Name}, {loc} on {map.Name}.");
        }

        return lines;
    }

    /// <summary>Takes away every witch the spots placed (by serial). Never a corpse.</summary>
    public static List<string> Clear()
    {
        var placed = Placed();
        var n = 0;
        foreach (var serial in placed.ToList())
        {
            if (World.FindMobile(serial) is HalloweenWitch { Deleted: false } witch)
            {
                witch.Delete();
                n++;
            }
        }

        placed.Clear();
        _gone.Clear();
        return n == 0 ? [] : [$"[HalloweenWitch] The witches are gone for the year ({n} taken away)."];
    }

    // -- The encounter ------------------------------------------------------------------------------------------------

    private static bool _hooked;

    /// <summary>Joins the encounter table's Supplement, once (Configure does it; the test host, which runs no Configure, too).</summary>
    public static void HookEncounters()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        EncounterTable.Supplement += AddEncounter;
    }

    /// <summary>The witch's encounter, from the witch's table.</summary>
    public static EncounterDef EncounterDef()
    {
        var e = Table.Encounter;
        return new EncounterDef
        {
            Id = HalloweenWitchTable.EncounterId,
            Name = "The Witch's Pot",
            Kind = EncounterKind.Boss,
            Weight = e.Weight,
            Facets = e.Facets.ToList(),
            Time = EncounterTime.Night,
            MinLevel = e.MinLevel,
            Season = "halloween",
            Creatures = [new EncounterGroup { Types = [nameof(HalloweenWitch)], Min = 1, Max = 1 }],
            SystemLine = e.SystemLine,
            OpeningLine = e.OpeningLine
        };
    }

    /// <summary>Adds the witch's encounter to a table coming into force, unless it is off or the table already has one.</summary>
    public static void AddEncounter(EncounterTable table)
    {
        if (!Table.Enabled || !Table.Encounter.Enabled || table.Find(HalloweenWitchTable.EncounterId) != null)
        {
            return;
        }

        table.Encounters.Add(EncounterDef());
    }

    // -- Staff --------------------------------------------------------------------------------------------------------

    [Usage("HalloweenWitch [status|reload|sync|clear]")]
    [Description("The Halloween witch (cc-P786). status says the season, the witches out at the wild spots and the table in force; reload reads Configuration/halloween-witch.json again (a bad file is refused whole); sync puts a witch at each empty spot in the Halloween season (or takes them away out of it); clear takes every spot's witch away now.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, NoDryRun = true, Summary = "Puts the Halloween witches at their wild spots (only in the Halloween season) or takes them away; reload reads their table again. They come and go by themselves with the season.")]
    private static void OnCommand(CommandEventArgs e)
    {
        foreach (var line in Run(e.Arguments))
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Run(string[] args)
    {
        var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        switch (verb)
        {
            case "reload":
                return HalloweenWitchTable.Reload(HalloweenWitchTable.FilePath);
            case "sync":
                var synced = Sync();
                return synced.Count > 0 ? synced : ["[HalloweenWitch] Nothing to change."];
            case "clear":
                var cleared = Clear();
                return cleared.Count > 0 ? cleared : ["[HalloweenWitch] No witch was out."];
            default:
                var t = Table;
                var out_ = Witches();
                return
                [
                    $"Halloween witch: {(t.Enabled ? "on" : "off")}; season {FestivalSeasons.Label(FestivalSeasons.Current())}; " +
                    $"{out_.Count} of {t.Spots.Count} spots have a witch ({string.Join(", ", out_.Select(w => $"{w.Location}"))}).",
                    $"Table ({t.Source}): hits {t.HitsMin}-{t.HitsMax}, Magery {t.Magery}, {t.Tokens} tokens, drops " +
                    string.Join(", ", t.Drops.Select(d => $"{d.Type} w{d.Weight}")) + $" at {t.DropChancePercent}%; " +
                    $"encounter {(t.Encounter.Enabled ? "on" : "off")}.",
                    "[HalloweenWitch status|reload|sync|clear]"
                ];
        }
    }
}
