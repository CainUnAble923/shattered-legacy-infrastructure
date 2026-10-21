// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Generate.cs (cc-P194).
//
// [SetupNewCovetous (Administrator), once per world: a VoidPoolController on each facet at 5605,1998,10, the 7x7
// "Void Pool" (stair tiles 1873-1888 round the edge hued 1954, star tiles 8511-8516 for the floor) at 5497-5503 x
// 1995-2001 z 5, a spawner for Cora the sorceress at 5457,1808 (home range 5, one to one and a half hours) and
// one for Vela in Cove at 2254,1207, the two waypoint paths the waves follow (AddWaypoints), and ConvertSpawners:
// the stock spawners of levels 1 and 2 (the region "Covetous", inside 5383,1845 125x115) have their gazers and
// gazer larvae made strange gazers, headless ones headless miners, harpies dazzled harpies and stone harpies
// vampire mongbats, then respawn. Refuses to run twice ("This has already been setup!"). [DeleteCovetous removes
// every tagged object and forgets the controllers.
//
// Conversion notes: XmlSpawner is pinned's Spawner (HomeRange for SpawnRange; DoRespawn is Respawn()); the stock
// spawners are walked through the region's items; entry names are SpawnerEntry.SpawnedName. ServUO forgets to tag
// two of the twenty waypoints into the collection (the first of path A on Trammel and the fifth): all twenty are
// tagged here, so [DeleteCovetous removes them (a one-line ServUO bug of the kind the D-4 rule fixes).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.VoidPool;

public static class Generate
{
    public const string CollectionKey = "newcovetous";

    public static readonly Point3D ControllerLocation = new(5605, 1998, 10);
    public static readonly Point3D CoraSpawnerLocation = new(5457, 1808, 0);
    public static readonly Point3D VelaSpawnerLocation = new(2254, 1207, 0);

    public static readonly Rectangle2D SpawnerBounds = new(5383, 1845, 125, 115);

    // ServUO Generate.AddWaypoints: path A, then path B, in order; each path ends at the pool.
    public static readonly Point3D[] PathA =
    {
        new(5590, 2024, 0), new(5578, 2029, 0), new(5566, 2027, 0), new(5555, 2021, 0), new(5545, 2015, 0),
        new(5537, 2020, 0), new(5527, 2015, 0), new(5509, 2005, 0), new(5500, 1998, 0)
    };

    public static readonly Point3D[] PathB =
    {
        new(5469, 2016, 0), new(5478, 2025, 0), new(5484, 2029, 0), new(5490, 2027, 0), new(5504, 2027, 0),
        new(5516, 2020, 0), new(5524, 2012, 0), new(5513, 2005, 0), new(5502, 2004, 0), new(5500, 1998, 0)
    };

    public static void Initialize()
    {
        CommandSystem.Register("SetupNewCovetous", AccessLevel.Administrator, Setup_OnCommand);
        CommandSystem.Register("DeleteCovetous", AccessLevel.Administrator, Delete_OnCommand);
    }

    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES everything SetupNewCovetous placed (the two Void Pool controllers, the pool, the Cora and Vela spawners, the waypoints) and forgets the controllers.")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();
        e.Mobile.SendMessage($"Covetous Void Spawn removed ({deleted} objects).");
    }

    public static int Delete()
    {
        var deleted = WeakEntityCollection.Delete(CollectionKey);
        VoidPoolController.InstanceTram = null;
        VoidPoolController.InstanceFel = null;
        return deleted;
    }

    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Refuses, Shard = CommandShard.Any, NoDryRun = true, Summary = "Covetous Void Spawn on Trammel and Felucca: the Void Pool controllers and pool, the Cora and Vela spawners, the wave paths; retypes the stock level 1-2 spawners. Refuses to run twice.")]
    public static void Setup_OnCommand(CommandEventArgs e)
    {
        if (VoidPoolController.InstanceTram != null || VoidPoolController.InstanceFel != null)
        {
            e.Mobile.SendMessage("This has already been setup!");
        }
        else
        {
            Setup();
            e.Mobile.SendMessage("Covetous Void Spawn setup complete.");
        }
    }

    /// <summary>ServUO's Setup body. Returns the two controllers (Trammel, Felucca).</summary>
    public static VoidPoolController[] Setup()
    {
        var one = new VoidPoolController(Map.Trammel);
        WeakEntityCollection.Add(CollectionKey, one);
        one.MoveToWorld(ControllerLocation, Map.Trammel);

        var two = new VoidPoolController(Map.Felucca);
        WeakEntityCollection.Add(CollectionKey, two);
        two.MoveToWorld(ControllerLocation, Map.Felucca);

        for (var x = 5497; x <= 5503; x++)
        {
            for (var y = 1995; y <= 2001; y++)
            {
                int id;

                if (x == 5497 && y == 1995)
                {
                    id = 1886;
                }
                else if (x == 5497 && y == 2001)
                {
                    id = 1887;
                }
                else if (x == 5503 && y == 1995)
                {
                    id = 1888;
                }
                else if (x == 5503 && y == 2001)
                {
                    id = 1885;
                }
                else if (x == 5497)
                {
                    id = 1874;
                }
                else if (x == 5503)
                {
                    id = 1876;
                }
                else if (y == 1995)
                {
                    id = 1873;
                }
                else if (y == 2001)
                {
                    id = 1875;
                }
                else
                {
                    id = Utility.Random(8511, 6);
                }

                var hue = id >= 8511 ? 0 : 1954;

                foreach (var map in new[] { Map.Trammel, Map.Felucca })
                {
                    var item = new Static(id) { Name = "Void Pool", Hue = hue };
                    WeakEntityCollection.Add(CollectionKey, item);
                    item.MoveToWorld(new Point3D(x, y, 5), map);
                }
            }
        }

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            var cora = new Spawner(1, TimeSpan.FromHours(1), TimeSpan.FromHours(1.5), 0, default, "CoraTheSorceress");
            WeakEntityCollection.Add(CollectionKey, cora);
            cora.MoveToWorld(CoraSpawnerLocation, map);
            cora.HomeRange = 5;
            cora.Respawn();

            var vela = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "VelaTheSorceress");
            WeakEntityCollection.Add(CollectionKey, vela);
            vela.MoveToWorld(VelaSpawnerLocation, map);
            vela.HomeRange = 0;
            vela.Respawn();
        }

        AddWaypoints();
        ConvertSpawners();

        return new[] { one, two };
    }

    public static void AddWaypoints()
    {
        var one = VoidPoolController.InstanceTram;
        var two = VoidPoolController.InstanceFel;

        if (one == null || two == null)
        {
            return;
        }

        AddWaypoints(one, Map.Trammel);
        AddWaypoints(two, Map.Felucca);
    }

    public static void AddWaypoints(VoidPoolController controller, Map map)
    {
        foreach (var w in controller.WaypointsA.ToList())
        {
            if (w?.Deleted == false)
            {
                w.Delete();
            }
        }

        foreach (var w in controller.WaypointsB.ToList())
        {
            if (w?.Deleted == false)
            {
                w.Delete();
            }
        }

        controller.WaypointsA.Clear();
        controller.WaypointsB.Clear();

        foreach (var p in PathA)
        {
            var wp = new WayPoint();
            WeakEntityCollection.Add(CollectionKey, wp);
            wp.MoveToWorld(p, map);
            controller.WaypointsA.Add(wp);
        }

        foreach (var p in PathB)
        {
            var wp = new WayPoint();
            WeakEntityCollection.Add(CollectionKey, wp);
            wp.MoveToWorld(p, map);
            controller.WaypointsB.Add(wp);
        }

        Link(controller.WaypointsA);
        Link(controller.WaypointsB);

        controller.WaypointACount = controller.WaypointsA.Count;
        controller.WaypointBCount = controller.WaypointsB.Count;
    }

    private static void Link(List<WayPoint> path)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            path[i].NextPoint = path[i + 1];
        }
    }

    public static void ConvertSpawners()
    {
        ConvertRegionSpawners(Region.Regions.FirstOrDefault(r => r.Map == Map.Trammel && r.Name == "Covetous"));
        ConvertRegionSpawners(Region.Regions.FirstOrDefault(r => r.Map == Map.Felucca && r.Name == "Covetous"));
    }

    public static int ConvertRegionSpawners(Region r)
    {
        if (r == null)
        {
            return 0;
        }

        var list = new List<Spawner>();

        foreach (var item in r.GetItems())
        {
            if (item is Spawner spawner && SpawnerBounds.Contains(item.Location))
            {
                foreach (var entry in spawner.Entries)
                {
                    entry.SpawnedName = ConvertName(entry.SpawnedName);
                }

                list.Add(spawner);
            }
        }

        foreach (var spawner in list)
        {
            spawner.Respawn();
        }

        return list.Count;
    }

    public static string ConvertName(string typeName)
    {
        if (typeName == null)
        {
            return null;
        }

        return typeName.ToLowerInvariant() switch
        {
            "gazer" or "gazerlarva" => "StrangeGazer",
            "headlessone"           => "HeadlessMiner",
            "harpy"                 => "DazzledHarpy",
            "stoneharpy"            => "VampireMongbat",
            _                       => typeName
        };
    }
}
