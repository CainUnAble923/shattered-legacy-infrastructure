// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Generate.cs (cc-P196; moved to the Fold by cc-P691).
//
// [GenBlackthorn (Administrator) builds the revamped Blackthorn Dungeon. ServUO builds it on Trammel and Felucca, each
// facet's own copy of EA's map. cc-P691: on the Siege of Sosaria map that area holds the author's dungeons, so the map
// patch copies EA's dungeon onto the Fold (map 32) in two pieces (BlackthornFold.cs), and there is one dungeon, there:
//   1. On the Fold: the DungeonHitchingPost at the landing (pets cannot enter the castle), eight MetalDoors at the
//      landing hall's thresholds, the sixteen Blockers that close off the New Magincia room's north side, and the
//      thirty teleporters of the join between the two pieces (BlackthornFold.Join), every ServUO spot through
//      BlackthornFold.Move.
//   2. The way in and out: a cellar hatch (a server item) in the author's Blackthorn Castle on Trammel with one
//      teleporter down to the landing, and five teleporters on ServUO's way out (the top of the east stairs) back to
//      beside the hatch. These replace ServUO's nine Felucca teleporters (castle 1517,1417-1420,9 <-> 6440,2677-2681,20)
//      and stock teleporters.json's Trammel pair, which S10 already dropped from the SoS teleporters.
//   3. The spawners (BlackthornSpawns): ServUO's 22 Trammel dungeon rows on the Fold, and the Trammel Agent of the
//      Crown in the castle's storeroom by the hatch. ServUO's Felucca rows (its second dungeon and nine city Agents) are
//      not placed: one dungeon, and no Felucca city spot has been checked on the SoS land.
// Everything placed is tagged into the "blackthorn" collection and [DeleteBlackthorn (ours) takes it down; running
// the generator again deletes first, so it replaces rather than doubles (ServUO's Generate places a second set on a
// re-run; the D-4 rule's kind of fix, notes). The dungeon region is registered at startup by BlackthornRegions.
// Nothing is placed at boot: run it once on a shard whose map has the copy (it refuses when the Fold is void there,
// BlackthornFold.MapReady, as on a server started without the map overlay).

using Server.Commands;
using Server.Items;

namespace Server.Engines.Blackthorn;

public static class GenerateBlackthornDungeon
{
    public const string CollectionKey = "blackthorn";

    public static readonly Point3D HitchingPostLocation = BlackthornFold.Move(6428, 2677, 0);

    // ServUO Generate(Map): (facing, where).
    public static readonly (DoorFacing Facing, Point3D At)[] Doors =
    {
        (DoorFacing.WestCCW, BlackthornFold.Move(6409, 2695, 0)),
        (DoorFacing.EastCW, BlackthornFold.Move(6410, 2695, 0)),
        (DoorFacing.WestCW, BlackthornFold.Move(6409, 2664, 0)),
        (DoorFacing.EastCCW, BlackthornFold.Move(6410, 2664, 0)),
        (DoorFacing.SouthCW, BlackthornFold.Move(6394, 2680, 0)),
        (DoorFacing.NorthCCW, BlackthornFold.Move(6394, 2679, 0)),
        (DoorFacing.NorthCW, BlackthornFold.Move(6425, 2679, 0)),
        (DoorFacing.SouthCCW, BlackthornFold.Move(6425, 2680, 0))
    };

    // ServUO Generate(Map): in the invasion block.
    public static readonly Point3D[] Blockers =
    {
        BlackthornFold.Move(6435, 2425, -5),
        BlackthornFold.Move(6435, 2424, -5),
        BlackthornFold.Move(6435, 2423, -5),
        BlackthornFold.Move(6435, 2422, -5),
        BlackthornFold.Move(6435, 2421, -5),
        BlackthornFold.Move(6434, 2421, -5),
        BlackthornFold.Move(6433, 2421, -5),
        BlackthornFold.Move(6432, 2421, -5),
        BlackthornFold.Move(6442, 2425, -5),
        BlackthornFold.Move(6442, 2424, -5),
        BlackthornFold.Move(6442, 2423, -5),
        BlackthornFold.Move(6442, 2422, -5),
        BlackthornFold.Move(6442, 2421, -5),
        BlackthornFold.Move(6443, 2421, -5),
        BlackthornFold.Move(6444, 2421, -5),
        BlackthornFold.Move(6445, 2421, -5)
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenBlackthorn", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteBlackthorn", AccessLevel.Administrator, Delete_OnCommand);
    }

    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true, Summary = "Blackthorn Dungeon (revamped) on the Fold: the hitching post, the landing hall's doors, the New Magincia blockers, the teleporters between its two pieces, the cellar hatch in Blackthorn Castle (Trammel) with the way down and back, the 22 dungeon spawners and the castle's Agent of the Crown. Deletes its earlier placement first, so it can run again. Refuses until the map patch has the dungeon.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        if (!BlackthornFold.MapReady())
        {
            // cc-P691: the dungeon is the map patch's copy on the Fold; without the map overlay the Fold is void there.
            e.Mobile.SendMessage("Blackthorn Dungeon is not on this server's map (the Fold is void at its landing; is the map overlay missing?). Nothing placed.");
            return;
        }

        var deleted = Delete();

        if (deleted > 0)
        {
            e.Mobile.SendMessage($"{deleted} objects of an earlier run deleted.");
        }

        e.Mobile.SendMessage(Generate());
        e.Mobile.SendMessage("Blackthorn Dungeon generation complete.");
    }

    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES everything GenBlackthorn placed (the post, doors, blockers, teleporters, the castle's cellar hatch and all the spawners with their spawn).")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();
        e.Mobile.SendMessage($"Blackthorn Dungeon removed ({deleted} objects).");
    }

    public static int Delete() => WeakEntityCollection.Delete(CollectionKey);

    private static T Place<T>(T item, Point3D at, Map map) where T : Item
    {
        WeakEntityCollection.Add(CollectionKey, item);
        item.MoveToWorld(at, map);
        return item;
    }

    private static Teleporter PlaceTeleporter(string label, Point3D at, Map map, Point3D to, Map toMap) =>
        Place(new Teleporter(to, toMap) { Name = $"Blackthorn: {label}" }, at, map);

    /// <summary>ServUO's Generate and the two XmlLoads, on the Fold (cc-P691). Returns a one-line report.</summary>
    public static string Generate()
    {
        var fold = BlackthornFold.Map;

        if (fold == null)
        {
            return "Blackthorn Dungeon: the Fold is not registered on this server. Nothing placed.";
        }

        Place(new DungeonHitchingPost(), HitchingPostLocation, fold);

        foreach (var (facing, at) in Doors)
        {
            Place(new MetalDoor(facing), at, fold);
        }

        foreach (var p in Blockers)
        {
            Place(new Blocker(), p, fold);
        }

        var teleporters = 0;

        foreach (var (at, to) in BlackthornFold.Join)
        {
            PlaceTeleporter(BlackthornFold.Dungeon.Contains(at) ? "the cave, north" : "the corridor, south", at, fold, to, fold);
            teleporters++;
        }

        Place(new Static(BlackthornFold.HatchItemId) { Name = BlackthornFold.HatchName }, BlackthornFold.Hatch, Map.Trammel);
        PlaceTeleporter("the cellar hatch, down", BlackthornFold.HatchTeleporter, Map.Trammel, BlackthornFold.Landing, fold);
        teleporters++;

        foreach (var p in BlackthornFold.WayOut)
        {
            PlaceTeleporter("the stairs, up to the castle", p, fold, BlackthornFold.BesideHatch, Map.Trammel);
            teleporters++;
        }

        var spawners = BlackthornSpawns.Generate();

        return $"Blackthorn Dungeon: 1 hitching post, {Doors.Length} doors, {Blockers.Length} blockers, {teleporters} teleporters, 1 cellar hatch, {spawners.Count} spawners.";
    }
}
