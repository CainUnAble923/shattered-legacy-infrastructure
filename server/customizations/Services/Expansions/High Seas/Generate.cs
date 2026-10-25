// cc-P269: the fish monger part of ServUO pub57 Scripts/Services/Expansions/High Seas/Generate.cs ([DecorateHS) -> ours,
// High Seas, second half. [GenFishMongers places a fish monger (the profession fish quests) at each of ServUO's eight
// docks, in Trammel and in Felucca: Britain, Moonglow, Trinsic, Vesper, Jhelom, Skara Brae, Papua and the Sea Market
// (the floating emporium, whose statics are in the client's map files). Each is given its port's name. A dock that
// already has its monger within ten tiles is skipped, so the command can be run again; "dryrun" lists what it would do.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO places an XmlSpawner of one fish monger at each dock (spawn range 1, home range 5; the Sea Market's 0). Here
//   the monger himself is placed, with the same home and range, and he is blessed, so he needs no respawn.
// - The rest of [DecorateHS (Corgul's altar, the bounty boards, the Sea Market's signs, the charybdis spawner) is not
//   here: the altar and the boards wait for the boat port, and the spawner makes itself.

using System.Collections.Generic;
using Server.Commands;
using Server.Mobiles;

namespace Server.Items;

public static class GenerateHighSeas
{
    // ServUO Generate.cs: the eight docks, each in Trammel and Felucca, and each spawner's home range.
    public static readonly (string Port, Point3D Location, int HomeRange)[] FishMongerSpots =
    {
        ("Britain", new Point3D(1482, 1754, -2), 5),
        ("Moonglow", new Point3D(4406, 1049, -2), 5),
        ("Trinsic", new Point3D(2061, 2855, -2), 5),
        ("Vesper", new Point3D(3009, 826, -2), 5),
        ("Jhelom", new Point3D(1373, 3885, -2), 5),
        ("Skara Brae", new Point3D(641, 2234, -2), 5),
        ("Papua", new Point3D(5827, 3258, -2), 5),
        ("Sea Market", new Point3D(4552, 2299, -1), 0)
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenFishMongers", AccessLevel.Administrator, GenFishMongers_OnCommand);
    }

    [Usage("GenFishMongers [dryrun]")]
    [Description("Places a fish monger (the profession fish quests) at each of ServUO's eight docks, in Trammel and Felucca.")]
    [ShardCommand(
        CommandCategory.WorldSetup,
        Rerun = CommandRerun.Skips,
        Shard = CommandShard.TestFirst,
        DryRun = "dryrun",
        Summary = "Places the fish mongers for the profession fish quests at the eight docks, Trammel and Felucca."
    )]
    public static void GenFishMongers_OnCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 && e.GetString(0).InsensitiveEquals("dryrun");

        foreach (var line in PlaceFishMongers(dryRun))
        {
            e.Mobile.SendMessage(line);
        }
    }

    /// <summary>Places the missing mongers (or, dry, says which it would place). One line per dock and facet.</summary>
    public static List<string> PlaceFishMongers(bool dryRun)
    {
        var report = new List<string>();

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            foreach (var (port, location, homeRange) in FishMongerSpots)
            {
                if (HasMonger(map, location, port))
                {
                    report.Add($"{port} ({map}): already has its fish monger, skipped.");
                    continue;
                }

                if (dryRun)
                {
                    report.Add($"{port} ({map}): would place a fish monger at {location}.");
                    continue;
                }

                var monger = new FishMonger { Port = port };
                monger.MoveToWorld(location, map);
                monger.Home = location;
                monger.RangeHome = homeRange;

                if (homeRange == 0)
                {
                    monger.CantWalk = true;
                }

                report.Add($"{port} ({map}): placed a fish monger at {location}.");
            }
        }

        return report;
    }

    private static bool HasMonger(Map map, Point3D location, string port)
    {
        foreach (var m in map.GetMobilesInRange(location, 10))
        {
            if (m is FishMonger monger && monger.PortName == port)
            {
                return true;
            }
        }

        return false;
    }
}
