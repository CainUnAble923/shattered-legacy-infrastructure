using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Places the guildmasters GuildLocations marks Seeded (cc-P15, F-9 Decision 1): one per guild hall
/// in New Haven that no other seeder or spawn fills, and the New Haven Artificers' Guildmaster in the
/// Magery School.
///
/// It changes a world only when told to. The other two seeders run on every world load
/// (ClusterFInstitutionSeeder always; ClusterFNewHavenSeeder with seedOnWorldLoad, which both shards
/// set true), so an entry added to either would place NPCs on the live world at its next start. This
/// one runs at world load only if clusterf.guildHallSeeder.seedOnWorldLoad is true (default false),
/// and otherwise by command:
///
///   [ClusterFSeedGuildHalls dryrun   what would happen, nothing changed
///   [ClusterFSeedGuildHalls          place the missing ones (default "missing")
///   [ClusterFSeedGuildHalls repair   also move one standing elsewhere (or on an old tile) onto its tile
///
/// The Thieves' Den lookout by the fighting pit (GuildLocations.Lookouts, cc-P22) is placed the same way.
///
/// Re-running never duplicates: a guildmaster of the right type within GuildLocation.SearchRange of
/// its tile, or of one of its OldTiles, counts as present.
/// </summary>
public static class ClusterFGuildHallSeeder
{
    private static bool _seedOnWorldLoad;

    /// <summary>
    /// cc-P51: the halls this seeder places a guildmaster in (the Seeded GuildLocations Seed walks, without the
    /// lookout), for the Staff Hub's Travel tab. Each location carries its own facet and tile.
    /// </summary>
    public static IEnumerable<GuildLocation> Anchors => GuildLocations.All.Where(l => l.Seeded);

    public static void Configure()
    {
        _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.guildHallSeeder.seedOnWorldLoad", false);
        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedGuildHalls", AccessLevel.Administrator, OnCommand);
    }

    private static void OnWorldLoad()
    {
        if (_seedOnWorldLoad)
        {
            foreach (var line in Seed(dryRun: false, repair: false))
                Console.WriteLine($"[ClusterFGuildHallSeeder] {line}");
        }
    }

    [Usage("ClusterFSeedGuildHalls [dryrun|missing|repair]")]
    [Description("Places the guild hall guildmasters (cc-P15). Default places only missing ones; repair also moves misplaced ones.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places missing guild hall guildmasters. repair also moves misplaced ones; dryrun does not show moves.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "missing";
        if (mode is not ("dryrun" or "missing" or "repair"))
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedGuildHalls [dryrun|missing|repair]");
            return;
        }

        foreach (var line in Seed(dryRun: mode == "dryrun", repair: mode == "repair"))
            e.Mobile.SendMessage(line);
    }

    public static List<string> Seed(bool dryRun, bool repair)
    {
        var report = new List<string>();
        int created = 0, moved = 0, present = 0;

        foreach (var loc in GuildLocations.All.Concat(GuildLocations.Lookouts))
        {
            if (!loc.Seeded)
                continue;

            var map = loc.Map;
            var label = $"{loc.GuildKey} ({loc.NpcType.Name}) at {loc.Hall}";

            if (map == null || map == Map.Internal)
            {
                report.Add($"{label}: facet {loc.MapIndex} is not loaded, skipped.");
                continue;
            }

            var existing = loc.Find();
            var onOldTile = false;

            if (existing == null)
            {
                foreach (var old in loc.OldTiles)
                {
                    existing = loc.FindNear(old);
                    if (existing != null)
                    {
                        onOldTile = true;
                        break;
                    }
                }
            }

            if (existing != null)
            {
                var placed = existing.Map == map && existing.Location == loc.Point;
                if (placed || !repair && !onOldTile)
                {
                    present++;
                    continue;
                }

                if (!repair)
                {
                    report.Add($"{label}: one stands on its old tile {existing.Location}; run repair to move it.");
                    present++;
                    continue;
                }

                if (!dryRun)
                    Place(existing, loc);

                report.Add($"{label}: {(dryRun ? "would move" : "moved")} from {existing.Location} to {loc.Point}.");
                moved++;
                continue;
            }

            if (!dryRun)
                Place(loc.NpcType.CreateInstance<Mobile>(), loc);

            report.Add($"{label}: {(dryRun ? "would place" : "placed")} at {loc.Point}.");
            created++;
        }

        report.Add($"Guild hall seed {(dryRun ? "dry run" : "done")}: placed {created}, moved {moved}, already there {present}.");
        return report;
    }

    private static void Place(Mobile npc, GuildLocation loc)
    {
        npc.Direction = loc.Facing;
        npc.CantWalk  = true;

        if (npc is BaseCreature bc)
        {
            bc.Home      = loc.Point;
            bc.RangeHome = 0;
        }

        npc.MoveToWorld(loc.Point, loc.Map);
    }
}
