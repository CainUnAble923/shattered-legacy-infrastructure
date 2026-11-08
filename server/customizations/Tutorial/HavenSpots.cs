// cc-P663 (P481 F3, P673 1.4): every New Haven spot the tutorial reads, in one place. P678 (P673 S7) moves New Haven into
// the Siege of Sosaria map's Haven town and changes only this file. Notes: shard-migration
// notes/cc-P663-tutorial-moves-to-isle.md.
//
// The core lessons left the plaza for the Wakening Isle (TutorialWorld). What stays here: Rook (graduation, the wings,
// skip and remedial training), the isle's dock moongate's way out beside him, the New Haven start (character creation's
// "New Haven" entry, the skip, and anyone the isle sends home), the island gate's bounds, the town vet and the healer.

#nullable enable

namespace Server.Tutorial;

public static class HavenSpots
{
    /// <summary>The map New Haven is on.</summary>
    public static Map Map => Map.Trammel;

    /// <summary>
    /// The New Haven start: character creation's city (with the tutorial on or off, cc-P705), the skip, the Fold's
    /// send-home (3503,2574,14). Settable only as a seam for tests; S7 changes the value here.
    /// </summary>
    public static Point3D Start { get; set; } = new(3503, 2574, 14);

    /// <summary>
    /// Rook Hallowell, seated on a bench in the Bountiful Harvest Inn's south yard (cc-P743, Chase 10-30: "a better spot
    /// instead of in the middle of the square"; cc-P799 on the stock town, which has no tavern patio near the square).
    /// </summary>
    public static readonly Point3D Rook = new(3502, 2530, 22);

    /// <summary>Rook's old spot on the plaza, where an older world's Rook is moved from (cc-P743).</summary>
    public static readonly Point3D OldRook = new(3500, 2572, 14);

    /// <summary>The way Rook sits on his bench (cc-P806): the yard, the moongate exit and the square are south of him, the Inn's low wall one tile north.</summary>
    public const Direction RookFacing = Direction.South;

    /// <summary>His bench: two wooden bench pieces (0x0B2D, which runs east-west; cc-P806, read from the art), Rook on the west one (cc-P743).</summary>
    public static readonly Point3D[] RookBench = [new(3502, 2530, 22), new(3503, 2530, 22)];

    /// <summary>Where the isle's dock moongate sets a trainee down: in front of Rook's bench (cc-P743; was 3501,2572,14).</summary>
    public static readonly Point3D MoongateExit = new(3502, 2532, 22);

    /// <summary>cc-P743: Rook's place in words, for every line that sends a trainee to him.</summary>
    public const string RookPlace = "on his bench by the Bountiful Harvest Inn, north of the square";

    /// <summary>cc-P799: New Haven's warrior training area on the stock town, the Warrior Guild's open court (Z 20).</summary>
    public static readonly Point3D ProvingGrounds = new(3529, 2544, 20);

    /// <summary>cc-P799: the Magery School's south room on the stock town (Z 52).</summary>
    public static readonly Point3D MageryHall = new(3486, 2501, 52);

    /// <summary>cc-P799: the theater, the audience floor before the stage (Z 55, the plank stage is Z 60), Chase 11-05.</summary>
    public static readonly Point3D Theater = new(3414, 2602, 55);

    /// <summary>cc-P799: the mine in the mountains south of town, by Jacob Waltz's camp (the ravine floor).</summary>
    public static readonly Point3D MiningCave = new(3514, 2747, 0);

    /// <summary>cc-P799: the stables' aisle, in front of the sick-bay stall.</summary>
    public static readonly Point3D Stables = new(3534, 2577, 7);

    /// <summary>cc-P799: the sick-bay stall, where the injured horse waits.</summary>
    public static readonly Point3D SickStall = new(3534, 2575, 7);

    /// <summary>The town's centre, for "a vet in New Haven" (60 tiles round it).</summary>
    public static readonly Point3D Centre = new(3500, 2590, 0);

    /// <summary>The vet by the stables, placed when the town has none (cc-P480).</summary>
    public static readonly Point3D VetSpot = new(3526, 2578, 7);

    /// <summary>New Haven's public moongate (pinned PublicMoongate.cs:194), the travel tour's first stop.</summary>
    public static readonly Point3D PublicMoongate = new(3450, 2677, 25);

    /// <summary>cc-P801: the jail's north-west wall post (the open yard inside the palisade; the land runs Z 8 to 10).</summary>
    public static readonly Point3D JailPost = new(3469, 2597, 11);

    /// <summary>cc-P801: the jail's floor, above the land's highest tile.</summary>
    public const int JailFloorZ = 11;

    /// <summary>cc-P801: the jail's place in words.</summary>
    public const string JailWhere = "the New Haven jail, the stone building inside the palisade, south-west of the plaza";

    /// <summary>The town healer's spawner (pinned Vendors.json; ClusterFNewHavenServicesSeeder).</summary>
    public static readonly Point3D Healer = new(3462, 2559, 35);

    /// <summary>
    /// The island gate's Haven bounds: pinned Distribution/Data/regions.json, Haven Island NoHousingRegion
    /// (3314,2345,500,750), docks and the water round them included. Start inclusive, end exclusive.
    /// </summary>
    public static readonly Rectangle2D IslandBounds = new(3314, 2345, 500, 750);

    public static bool InIslandBounds(Point3D p) =>
        p.X >= IslandBounds.Start.X && p.X < IslandBounds.End.X && p.Y >= IslandBounds.Start.Y && p.Y < IslandBounds.End.Y;
}
