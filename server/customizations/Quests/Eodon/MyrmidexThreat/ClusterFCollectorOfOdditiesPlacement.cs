// cc-P199: places ServUO's Collector of Oddities, who sells the three trap training kits. Never at world load.
//
// Where OSI has him: ServUO pub57 Scripts/Quests/Eodon/MyrmidexThreat/Generate.cs:207-211 spawns him at Trammel
// 4305,1003,0 (Moonglow) with a home range of 5. This puts him on that tile with the same home range. ServUO uses an
// XmlSpawner for him; here he is placed once, the way the shard places other vendors, and the command re-places him if
// he is ever gone.
//
// [ClusterFPlaceCollectorOfOddities [dryrun]
//   Skipped if a Collector of Oddities already stands within 15 tiles, or if the tile cannot hold a mobile. Nothing is
//   moved or deleted.

using System;
using Server.Commands;
using Server.Mobiles;

namespace Server;

public static class ClusterFCollectorOfOdditiesPlacement
{
    public const int Range = 15;
    public static readonly Point3D Spot = new(4305, 1003, 0);

    public static void Configure()
    {
        CommandSystem.Register("ClusterFPlaceCollectorOfOddities", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFPlaceCollectorOfOddities [dryrun]")]
    [Description("Places the Collector of Oddities (the trap training kit vendor) at ServUO's spot in Moonglow, Trammel 4305,1003, if none stands near and the tile can hold him. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Collector of Oddities, who sells the trap training kits, in Moonglow (Trammel 4305,1003).")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        if (mode != "" && mode != "dryrun")
        {
            e.Mobile.SendMessage("Usage: [ClusterFPlaceCollectorOfOddities [dryrun]");
            return;
        }

        e.Mobile.SendMessage(Place(Map.Trammel, mode == "dryrun"));
    }

    // fits: the map's own CanFit (height 16, mobiles counted) unless a test supplies one; the test host has no map files.
    public static string Place(Map map, bool dryRun, Func<Point3D, bool> fits = null)
    {
        fits ??= p => map.CanFit(p, 16, false, true);

        if (map == null || map == Map.Internal)
        {
            return "Collector of Oddities placement: no map.";
        }

        foreach (var m in map.GetMobilesInRange(Spot, Range))
        {
            if (m is CollectorOfOddities)
            {
                return $"Collector of Oddities placement{(dryRun ? " (dry run)" : "")}: already at {m.Location} on {map}.";
            }
        }

        if (!fits(Spot))
        {
            return $"Collector of Oddities placement{(dryRun ? " (dry run)" : "")}: {Spot} on {map} cannot hold a mobile.";
        }

        if (!dryRun)
        {
            var collector = new CollectorOfOddities { Home = Spot, RangeHome = 5 };
            collector.MoveToWorld(Spot, map);
        }

        return $"Collector of Oddities placement{(dryRun ? " (dry run)" : "")}: {(dryRun ? "would place" : "placed")} at {Spot} on {map}.";
    }
}
