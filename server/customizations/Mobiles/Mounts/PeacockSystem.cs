// cc-P793: the Peacock's start-up, its meadow spawners and its staff command.
//
//   - the table (Configuration/peacock.json) is read at Configure, written with the defaults if missing;
//   - after every world load (on the next tick, so the world's items are in place) each meadow gets its spawner if it has
//     none, so an existing world has peacocks by itself and a second load adds nothing;
//
//   [Peacock status | reload | seed [dryrun]

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Spawners;

namespace Server.Mobiles;

public static class PeacockSystem
{
    public const int SearchRange = 1;

    public static void Configure()
    {
        Print(PeacockTable.LoadOrWriteDefaults(PeacockTable.FilePath));

        CommandSystem.Register("Peacock", AccessLevel.GameMaster, OnCommand);
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () => Print(Seed()));
    }

    private static void Print(List<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine($"[Peacock] {line}");
        }
    }

    private static bool Spawns(Spawner s) =>
        s.Entries.Any(e => e.SpawnedName.Equals(nameof(Peacock), StringComparison.OrdinalIgnoreCase));

    public static Spawner? FindAt(Map map, Point3D at)
    {
        foreach (var item in map.GetItemsInRange(at, SearchRange))
        {
            if (item is Spawner { Deleted: false } s && Spawns(s))
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>Places a spawner at every meadow that has none. Idempotent.</summary>
    public static List<string> Seed(bool dryRun = false)
    {
        var report = new List<string>();
        var t = PeacockTable.Current;
        var map = Map.Trammel;

        if (!t.Enabled)
        {
            report.Add("Peacocks are off; nothing seeded.");
            return report;
        }

        if (map == null || map == Map.Internal)
        {
            report.Add("Trammel is not loaded; nothing seeded.");
            return report;
        }

        int placed = 0, present = 0;
        foreach (var spot in t.Spawns)
        {
            if (FindAt(map, spot.Location) != null)
            {
                present++;
                continue;
            }

            placed++;
            if (!dryRun)
            {
                var spawner = new Spawner(
                    spot.Count,
                    TimeSpan.FromMinutes(spot.MinDelayMinutes),
                    TimeSpan.FromMinutes(spot.MaxDelayMinutes),
                    0,
                    default,
                    nameof(Peacock)
                ) { Name = $"Peacock meadow: {spot.Name}" };

                // As pinned's importer does (ImportSpawnersCommand.cs:248-249); the range after the move, so it centres on the spot.
                spawner.MoveToWorld(spot.Location, map);
                spawner.HomeRange = spot.HomeRange;
                spawner.WalkingRange = spot.HomeRange;
                spawner.Respawn();
            }

            report.Add($"{(dryRun ? "would place" : "placed")} a peacock spawner at {spot.Name} {spot.Location}");
        }

        report.Add($"Peacock meadows: {placed} {(dryRun ? "to place" : "placed")}, {present} already there, of {t.Spawns.Count}.");
        return report;
    }

    [Usage("Peacock [status|reload|seed [dryrun]]")]
    [Description("Peacock mount (cc-P793): status says the table in force (Configuration/peacock.json) and which meadows have a spawner; reload reads the file now (new peacocks take the numbers); seed places any missing meadow spawner (dryrun says what it would place).")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        var lines = verb switch
        {
            "status" => Status(),
            "reload" => PeacockTable.Reload(PeacockTable.FilePath),
            "seed"   => Seed(e.Length > 1 && e.GetString(1).Equals("dryrun", StringComparison.OrdinalIgnoreCase)),
            _        => ["Usage: [Peacock status|reload|seed [dryrun]"]
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Status()
    {
        var t = PeacockTable.Current;
        var lines = new List<string>
        {
            $"Peacocks {(t.Enabled ? "on" : "off")} ({t.Source}): tame {t.MinTameSkill}, {t.ControlSlots} slot(s), {t.Spawns.Count} meadows."
        };

        foreach (var s in t.Spawns)
        {
            lines.Add($"  {s.Name} {s.Location}: {(FindAt(Map.Trammel, s.Location) != null ? "spawner there" : "no spawner")}");
        }

        return lines;
    }
}
