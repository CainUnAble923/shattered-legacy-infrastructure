// cc-P860: where the ridable polar bear lives, and how an existing world gets it (PeacockSystem's shape, cc-P793).
//
//   - two dens on Dagger Isle's snow, Trammel (the Siege of Sosaria land has the isle's snow where Britannia's is; both
//     spots are flat snow with no static within the home range, and outside every SoS fence: the north-east snow isles'
//     fence starts at x 4224), each a spawner of two bears, home range 10, every 20 to 40 minutes;
//   - after every world load (on the next tick, so the world's items are in place) each den gets its spawner if it has
//     none, so an existing world has the bears by itself and a second load adds nothing.
//
//   [PolarBearDens status | seed [dryrun]

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Spawners;

namespace Server.Mobiles;

public static class RidablePolarBearDens
{
    public record Den(string Name, Point3D Location, int Count, int HomeRange, int MinDelayMinutes, int MaxDelayMinutes);

    public static readonly Den[] Dens =
    [
        new("Dagger Isle, north snowfield", new Point3D(4090, 325, 0), 2, 10, 20, 40),
        new("Dagger Isle, west snowfield", new Point3D(3963, 508, 0), 2, 10, 20, 40)
    ];

    public const int SearchRange = 1;

    public static void Configure()
    {
        CommandSystem.Register("PolarBearDens", AccessLevel.GameMaster, OnCommand);
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () => Print(Seed()));
    }

    private static void Print(List<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine($"[PolarBearDens] {line}");
        }
    }

    private static bool Spawns(Spawner s) =>
        s.Entries.Any(e => e.SpawnedName.Equals(nameof(RidablePolarBear), StringComparison.OrdinalIgnoreCase));

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

    /// <summary>Places a spawner at every den that has none. Idempotent.</summary>
    public static List<string> Seed(bool dryRun = false)
    {
        var report = new List<string>();
        var map = Map.Trammel;

        if (map == null || map == Map.Internal)
        {
            report.Add("Trammel is not loaded; nothing seeded.");
            return report;
        }

        int placed = 0, present = 0;
        foreach (var den in Dens)
        {
            if (FindAt(map, den.Location) != null)
            {
                present++;
                continue;
            }

            placed++;
            if (!dryRun)
            {
                var spawner = new Spawner(
                    den.Count,
                    TimeSpan.FromMinutes(den.MinDelayMinutes),
                    TimeSpan.FromMinutes(den.MaxDelayMinutes),
                    0,
                    default,
                    nameof(RidablePolarBear)
                ) { Name = $"Ridable polar bear den: {den.Name}" };

                // As pinned's importer does; the range after the move, so it centres on the den.
                spawner.MoveToWorld(den.Location, map);
                spawner.HomeRange = den.HomeRange;
                spawner.WalkingRange = den.HomeRange;
                spawner.Respawn();
            }

            report.Add($"{(dryRun ? "would place" : "placed")} a ridable polar bear spawner at {den.Name} {den.Location}");
        }

        report.Add($"Ridable polar bear dens: {placed} {(dryRun ? "to place" : "placed")}, {present} already there, of {Dens.Length}.");
        return report;
    }

    public static List<string> Status()
    {
        var lines = new List<string>
        {
            $"Ridable polar bear: tame {RidablePolarBear.TameSkill}, 1 slot, {Dens.Length} dens on Trammel."
        };

        foreach (var d in Dens)
        {
            lines.Add($"  {d.Name} {d.Location}: {(FindAt(Map.Trammel, d.Location) != null ? "spawner there" : "no spawner")}");
        }

        return lines;
    }

    [Usage("PolarBearDens [status|seed [dryrun]]")]
    [Description("Ridable polar bear (cc-P860): status says the dens on Dagger Isle and which have a spawner; seed places any missing den spawner (dryrun says what it would place). The dens are seeded by themselves after every world load.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        var lines = verb switch
        {
            "status" => Status(),
            "seed"   => Seed(e.Length > 1 && e.GetString(1).Equals("dryrun", StringComparison.OrdinalIgnoreCase)),
            _        => ["Usage: [PolarBearDens status|seed [dryrun]"]
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }
}
