// ServUO: Services/Revamped Dungeons/WrongDungeon/Generate.cs (cc-P194).
//
// [GenWrongRevamp (Administrator) builds the revamped Wrong on Trammel AND Felucca, as ServUO's does:
//   1. DeleteOldWrong: inside the dungeon's rectangle (5633,511 to 5886,1021) every spawner, Teleporter, sparkle
//      (item 0x375A), BarredMetalDoor and SecretDungeonDoor is deleted on both facets (ServUO deletes XmlSpawners;
//      here the stock Spawners the importer placed, and ours from an earlier run).
//   2. The 38 spawners (WrongSpawns, ServUO's "[XmlLoad RevampedSpawns/WrongRevamped.xml").
//   3. The maze decoration (WrongDecoration, ServUO's Decorate.Generate on Data/Decoration/Wrong).
//   4. A BedrollSpawner at 5823,601,0 per facet (the bedrolls and mysterious tunnels).
//   5. The seven hot-item chests per facet (EnchantedHotItem.SpawnChests).
//   6. Sixteen Teleporters, eight per facet, at ServUO's exact spots.
// Everything placed is tagged into the "newwrong" collection so [DeleteWrongRevamp can take it down again
// (ServUO has no delete command for Wrong; DeleteOldWrong is what its Generate runs first). Running the
// generator again replaces: it deletes first, exactly as ServUO's does.
//
// The two regions (WrongLevel3, WrongJail) are registered at startup by WrongRegions, not by this command.
//
// Conversion notes: Map.GetItemsInBounds is a foreach over an enumerable here; the counts the command reports are
// ServUO's "{0} items deleted." plus what each step placed.

using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;

namespace Server.Engines.WrongRevamp;

public static class WrongGenerator
{
    public const string CollectionKey = "newwrong";

    public static readonly Rectangle2D DungeonBounds = new(5633, 511, 253, 510);

    public static readonly Point3D BedrollSpawnerLocation = new(5823, 601, 0);

    // ServUO Generate.cs: (destination, where it stands), the same eight on each facet.
    public static readonly (Point3D Dest, Point3D At)[] Teleporters =
    {
        (new Point3D(5690, 569, 25), new Point3D(5827, 590, 1)),
        (new Point3D(5829, 595, 0), new Point3D(5690, 573, 25)),
        (new Point3D(5690, 569, 25), new Point3D(5872, 532, 24)),
        (new Point3D(5827, 593, 0), new Point3D(5732, 554, 24)),
        (new Point3D(5703, 639, 0), new Point3D(5708, 625, 0)),
        (new Point3D(5792, 526, 10), new Point3D(5698, 662, 0)),
        (new Point3D(2041, 215, 14), new Point3D(5824, 631, 5)),
        (new Point3D(2043, 215, 14), new Point3D(5825, 631, 5))
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenWrongRevamp", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteWrongRevamp", AccessLevel.Administrator, Delete_OnCommand);
    }

    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true, Summary = "Wrong Revamped on Trammel and Felucca: clears the old dungeon spawners, doors and teleporters, then places the revamp's spawners, maze, bedrolls, hot-item chests and teleporters. Deletes first, so it can run again.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteOldWrong();
        e.Mobile.SendMessage($"{deleted} items deleted.");

        var report = Generate();
        e.Mobile.SendMessage(report);
        e.Mobile.SendMessage("Wrong Revamp generation complete.");
    }

    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES everything GenWrongRevamp placed (spawners, maze, bedrolls, chests, teleporters). The stock Wrong spawners it removed do not come back; re-import them.")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteWrongRevamp();
        e.Mobile.SendMessage($"Wrong Revamped removed ({deleted} objects).");
    }

    public static int DeleteWrongRevamp() => WeakEntityCollection.Delete(CollectionKey);

    /// <summary>
    ///     ServUO's DeleteOldWrong: both facets, everything in the dungeon rectangle that is a spawner, a Teleporter,
    ///     a sparkle, a BarredMetalDoor or a SecretDungeonDoor. Returns the count.
    /// </summary>
    public static int DeleteOldWrong()
    {
        var count = 0;

        foreach (var map in new[] { Map.Felucca, Map.Trammel })
        {
            var doomed = new List<Item>();

            foreach (var item in map.GetItemsInBounds(DungeonBounds))
            {
                // BedrollSpawner is ours to add: ServUO's list misses it, so its own re-run lays a second set of
                // bedrolls over the first (a one-line ServUO bug of the kind the D-4 rule fixes; notes).
                if (item is BaseSpawner || item is Teleporter || item.ItemID == 0x375A ||
                    item is BarredMetalDoor || item is SecretDungeonDoor || item is BedrollSpawner)
                {
                    doomed.Add(item);
                }
            }

            foreach (var item in doomed)
            {
                if (!item.Deleted)
                {
                    count++;
                    item.Delete();
                }
            }
        }

        return count;
    }

    /// <summary>ServUO's Generate_NewWrong after DeleteOldWrong. Returns a one-line report.</summary>
    public static string Generate()
    {
        var spawners = WrongSpawns.Generate();

        var pieces = 0;
        var chests = 0;
        var teleporters = 0;

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            pieces += WrongDecoration.Generate(map).Count;

            var spawner = new BedrollSpawner();
            WeakEntityCollection.Add(CollectionKey, spawner);
            spawner.MoveToWorld(BedrollSpawnerLocation, map);

            chests += EnchantedHotItem.SpawnChests(map).Count;

            foreach (var (dest, at) in Teleporters)
            {
                var teleporter = new Teleporter(dest, map);
                WeakEntityCollection.Add(CollectionKey, teleporter);
                teleporter.MoveToWorld(at, map);
                teleporters++;
            }
        }

        return $"Wrong Revamped: {spawners.Count} spawners, {pieces} maze pieces, 2 bedroll spawners, {chests} hot-item chests, {teleporters} teleporters.";
    }
}
