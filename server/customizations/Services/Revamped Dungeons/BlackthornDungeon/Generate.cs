// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Generate.cs (cc-P196).
//
// [GenBlackthorn (Administrator) builds the revamped Blackthorn Dungeon on Trammel AND Felucca, as ServUO's does:
//   1. On each facet: the DungeonHitchingPost at 6428,2677,0 (pets cannot enter the castle), eight MetalDoors at the
//      invasion rooms' thresholds, and the sixteen Blockers that close off the New Magincia room's north side.
//   2. On Felucca only: nine Teleporters between Lord Blackthorn's castle in Britain (1517,1417-1420,9) and the
//      dungeon's landing (6440,2677-2681,20). The Trammel ones are stock: pinned Data/teleporters.json carries
//      both directions (the castle's 1477,1471-1475,-8 to 6432,2677-2681,0 and back from 6440,2677-2681,20), the
//      "create world teleporter section" ServUO's comment relies on.
//   3. The 54 spawners (BlackthornSpawns): ServUO's "[XmlLoad RevampedSpawns/BlackthornDungeonCreature.xml" and
//      "...BlackthornDungeonAgent.xml".
// Everything placed is tagged into the "blackthorn" collection and [DeleteBlackthorn (ours) takes it down; running
// the generator again deletes first, so it replaces rather than doubles (ServUO's Generate places a second set on a
// re-run; the D-4 rule's kind of fix, notes). The two regions are registered at startup by BlackthornRegions.
//
// One ServUO value is corrected: its fourth return teleporter lands at 6440,2680 z 12 where its own three
// neighbours and the landing tiles are z 20 (Generate.cs:86); z 20 here, argued in the notes.

using System.Collections.Generic;
using Server.Commands;
using Server.Items;

namespace Server.Engines.Blackthorn;

public static class GenerateBlackthornDungeon
{
    public const string CollectionKey = "blackthorn";

    public static readonly Point3D HitchingPostLocation = new(6428, 2677, 0);

    // ServUO Generate(Map): (facing, where), the same eight on each facet.
    public static readonly (DoorFacing Facing, Point3D At)[] Doors =
    {
        (DoorFacing.WestCCW, new Point3D(6409, 2695, 0)),
        (DoorFacing.EastCW, new Point3D(6410, 2695, 0)),
        (DoorFacing.WestCW, new Point3D(6409, 2664, 0)),
        (DoorFacing.EastCCW, new Point3D(6410, 2664, 0)),
        (DoorFacing.SouthCW, new Point3D(6394, 2680, 0)),
        (DoorFacing.NorthCCW, new Point3D(6394, 2679, 0)),
        (DoorFacing.NorthCW, new Point3D(6425, 2679, 0)),
        (DoorFacing.SouthCCW, new Point3D(6425, 2680, 0))
    };

    // ServUO Generate(Map), Felucca only: (destination, where it stands). Five in, four back.
    public static readonly (Point3D Dest, Point3D At)[] FeluccaTeleporters =
    {
        (new Point3D(1517, 1417, 9), new Point3D(6440, 2677, 20)),
        (new Point3D(1517, 1418, 9), new Point3D(6440, 2678, 20)),
        (new Point3D(1517, 1419, 9), new Point3D(6440, 2679, 20)),
        (new Point3D(1517, 1420, 9), new Point3D(6440, 2680, 20)),
        (new Point3D(1517, 1420, 9), new Point3D(6440, 2681, 20)),
        (new Point3D(6440, 2677, 20), new Point3D(1517, 1417, 9)),
        (new Point3D(6440, 2678, 20), new Point3D(1517, 1418, 9)),
        (new Point3D(6440, 2679, 20), new Point3D(1517, 1419, 9)),
        (new Point3D(6440, 2680, 20), new Point3D(1517, 1420, 9)) // ServUO: z 12 (see the header)
    };

    public static readonly Point3D[] Blockers =
    {
        new(6435, 2425, -5),
        new(6435, 2424, -5),
        new(6435, 2423, -5),
        new(6435, 2422, -5),
        new(6435, 2421, -5),
        new(6434, 2421, -5),
        new(6433, 2421, -5),
        new(6432, 2421, -5),
        new(6442, 2425, -5),
        new(6442, 2424, -5),
        new(6442, 2423, -5),
        new(6442, 2422, -5),
        new(6442, 2421, -5),
        new(6443, 2421, -5),
        new(6444, 2421, -5),
        new(6445, 2421, -5)
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenBlackthorn", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteBlackthorn", AccessLevel.Administrator, Delete_OnCommand);
    }

    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true, Summary = "Blackthorn Dungeon (revamped) on Trammel and Felucca: the hitching post, the invasion-room doors, the New Magincia blockers, the Felucca castle teleporters, the 44 dungeon spawners and the 10 Agent of the Crown spawners. Deletes its earlier placement first, so it can run again.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();

        if (deleted > 0)
        {
            e.Mobile.SendMessage($"{deleted} objects of an earlier run deleted.");
        }

        e.Mobile.SendMessage(Generate());
        e.Mobile.SendMessage("Blackthorn Dungeon generation complete.");
    }

    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES everything GenBlackthorn placed (the post, doors, blockers, Felucca teleporters and all 54 spawners with their spawn).")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();
        e.Mobile.SendMessage($"Blackthorn Dungeon removed ({deleted} objects).");
    }

    public static int Delete() => WeakEntityCollection.Delete(CollectionKey);

    /// <summary>ServUO's Generate(Trammel), Generate(Felucca) and the two XmlLoads. Returns a one-line report.</summary>
    public static string Generate()
    {
        var doors = 0;
        var blockers = 0;
        var teleporters = 0;

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            var post = new DungeonHitchingPost();
            WeakEntityCollection.Add(CollectionKey, post);
            post.MoveToWorld(HitchingPostLocation, map);

            foreach (var (facing, at) in Doors)
            {
                var door = new MetalDoor(facing);
                WeakEntityCollection.Add(CollectionKey, door);
                door.MoveToWorld(at, map);
                doors++;
            }

            if (map == Map.Felucca)
            {
                foreach (var (dest, at) in FeluccaTeleporters)
                {
                    var teleporter = new Teleporter(dest, map);
                    WeakEntityCollection.Add(CollectionKey, teleporter);
                    teleporter.MoveToWorld(at, map);
                    teleporters++;
                }
            }

            foreach (var p in Blockers)
            {
                var blocker = new Blocker();
                WeakEntityCollection.Add(CollectionKey, blocker);
                blocker.MoveToWorld(p, map);
                blockers++;
            }
        }

        var spawners = BlackthornSpawns.Generate();

        return $"Blackthorn Dungeon: 2 hitching posts, {doors} doors, {blockers} blockers, {teleporters} Felucca teleporters, {spawners.Count} spawners.";
    }
}
