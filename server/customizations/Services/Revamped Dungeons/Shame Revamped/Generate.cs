// ServUO: Services/Revamped Dungeons/Shame Revamped/Generate.cs (CC4 Shame).
//
// [GenerateNewShame (Administrator) places, on Trammel AND Felucca: the three Guardian's Altars (levels 1-3),
// the three cave-troll walls with their wall teleporters, and the 194 spawners. [DeleteShame removes everything
// it placed and re-enables the stock spawners it had stopped.
//
// ServUO's Read Me has the GM run [XmlLoad spawns/shamerevamped.xml first and [GenerateNewShame second (the
// command also re-issues the XmlLoad itself). ModernUO has neither XmlSpawner nor XmlLoad, so when no Shame
// spawners exist yet the command generates the same 194 from ShameSpawns.Definitions. One command; the
// objects are the same.
//
// What the command does to the world it finds (ServUO's RemoveItems/ResetOldSpawners, reproduced):
//   - deletes a stock Teleporter (exact type) standing on each of the three altar-teleporter tiles, so the
//     altar's own ShameTeleporter can take the tile;
//   - stops and empties (Reset) every other spawner inside the "Shame" region of each facet, so the pre-revamp
//     population goes away; [DeleteShame starts them again (Running = true; Respawn). Ours are told apart by
//     name, "Shame_Revamped" / "Shame_Chest", exactly as ServUO does. If a facet has no dungeon region
//     registered (the test host loads no regions.json) there is nothing to reset and the step is skipped.
//
// Paper-only differences (notes/cc4-shame.md): the spawners are tagged into the "newshame" collection so
// [DeleteShame removes them, where ServUO leaves its XmlSpawners in place; an altar deletes its teleporter
// and a live guardian, and a wall its troll, when [DeleteShame removes them.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.ShameRevamped;

public static class ShameGenerator
{
    public const string CollectionKey = "newshame";

    private static readonly Point3D SpawnerProbe = new(5538, 170, 5);

    private static readonly Point3D[] AltarTeleporterTiles =
    {
        new(5490, 19, -25),
        new(5604, 102, 5),
        new(5538, 170, 5)
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenerateNewShame", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteShame", AccessLevel.Administrator, Delete_OnCommand);
    }

    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteShame();
        e.Mobile.SendMessage($"Shame Revamped removed ({deleted} objects).");
    }

    public static int DeleteShame()
    {
        var deleted = WeakEntityCollection.Delete(CollectionKey);
        ResetOldSpawners(false);
        return deleted;
    }

    public static void Generate_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(Generate());
    }

    /// <summary>
    ///     ServUO's Generate body. Returns the message the command sends.
    /// </summary>
    public static string Generate()
    {
        if (!Core.ML)
        {
            return "Shame Revamped needs Mondain's Legacy enabled.";
        }

        RemoveItems();

        if (!ShameSpawns.AnyPresent())
        {
            ShameSpawns.Generate();
        }

        // Level 1
        PlaceAltar(
            new Point3D(5403, 43, 30), typeof(QuartzElemental),
            new Point3D(5490, 19, -25), new Point3D(5514, 10, 5), new Point3D(5387, 11, 30), 10
        );

        // Level 2
        PlaceAltar(
            new Point3D(5577, 54, 2), typeof(FlameElemental),
            new Point3D(5604, 102, 5), new Point3D(5514, 147, 25), new Point3D(5571, 115, 3), 20
        );

        // Level 3
        PlaceAltar(
            new Point3D(5390, 145, 20), typeof(WindElemental),
            new Point3D(5538, 170, 5), new Point3D(5513, 176, 5), new Point3D(5618, 223, 0), 30
        );

        // Wall 1
        PlaceWall(
            new Point3D(5403, 82, 10), new Point3D(5405, 90, 10),
            new Dictionary<Point3D, int>
            {
                [new Point3D(-1, 0, 0)] = 2272,
                [new Point3D(0, 0, 0)] = 2272,
                [new Point3D(2, 0, 0)] = 2272,
                [new Point3D(1, 0, 0)] = 2272
            }
        );

        // Wall 2
        PlaceWall(
            new Point3D(5465, 26, -10), new Point3D(5472, 26, -30),
            new Dictionary<Point3D, int>
            {
                [new Point3D(0, -1, 0)] = 2272,
                [new Point3D(0, 0, 0)] = 2272,
                [new Point3D(0, 1, 0)] = 2272,
                [new Point3D(0, 2, 0)] = 2272
            }
        );

        // Wall 3
        PlaceWall(
            new Point3D(5619, 57, 0), new Point3D(5621, 43, 0),
            new Dictionary<Point3D, int>
            {
                [new Point3D(-1, 0, 0)] = 1059,
                [new Point3D(0, 0, 0)] = 1059,
                [new Point3D(1, 0, 0)] = 1059
            }
        );

        return "Shame Revamped setup!";
    }

    private static readonly Map[] Facets = { Map.Trammel, Map.Felucca };

    private static void PlaceAltar(Point3D loc, Type guardian, Point3D teleLoc, Point3D teleDest, Point3D spawnLoc, int cost)
    {
        foreach (var map in Facets)
        {
            if (CheckForAltar(loc, map))
            {
                continue;
            }

            var altar = new ShameAltar(guardian, teleLoc, teleDest, spawnLoc, cost);
            WeakEntityCollection.Add(CollectionKey, altar);
            altar.MoveToWorld(loc, map);
        }
    }

    private static void PlaceWall(Point3D loc, Point3D trollSpawnLoc, Dictionary<Point3D, int> pieces)
    {
        foreach (var map in Facets)
        {
            if (CheckForAltar(loc, map))
            {
                continue;
            }

            var wall = new ShameWall(pieces, loc, trollSpawnLoc, map);
            WeakEntityCollection.Add(CollectionKey, wall);
            wall.MoveToWorld(loc, map);
            ShameWall.AddTeleporters(wall);
        }
    }

    public static void RemoveItems()
    {
        foreach (var tile in AltarTeleporterTiles)
        {
            foreach (var map in Facets)
            {
                RemoveItem(tile, map, typeof(Teleporter));
            }
        }

        ResetOldSpawners();
    }

    /// <summary>
    ///     Stops (reset = true) or restarts (reset = false) every spawner in each facet's "Shame" region that is
    ///     not one of ours. Returns how many it touched.
    /// </summary>
    public static int ResetOldSpawners(bool reset = true)
    {
        var touched = 0;

        foreach (var map in Facets)
        {
            var r = Region.Find(SpawnerProbe, map);

            if (r == null || r.IsDefault)
            {
                continue; // no dungeon region on this facet: nothing of the old population to reset
            }

            foreach (var spawner in r.GetItems().OfType<Spawner>())
            {
                if (ShameSpawns.IsShameSpawner(spawner))
                {
                    continue;
                }

                if (reset)
                {
                    spawner.Reset();
                }
                else
                {
                    spawner.Running = true;
                    spawner.Respawn();
                }

                touched++;
            }
        }

        return touched;
    }

    public static bool RemoveItem(Point3D p, Map map, Type t)
    {
        Item found = null;

        foreach (var item in map.GetItemsInRange(p, 0))
        {
            if (item.GetType() == t)
            {
                found = item;
                break;
            }
        }

        if (found == null)
        {
            return false;
        }

        found.Delete();
        return true;
    }

    public static bool CheckForAltar(Point3D p, Map map)
    {
        foreach (var item in map.GetItemsInRange(p, 0))
        {
            if (item is ShameAltar or ShameWall)
            {
                return true;
            }
        }

        return false;
    }
}
