// cc-P663 (P481 F3, P673 1.4): every New Haven spot the tutorial reads, in one place. Notes: shard-migration
// notes/cc-P663-tutorial-moves-to-isle.md.
//
// cc-P678 (P673 S7): New Haven moved into the Siege of Sosaria map's Haven town (the author's restored pre-UOML Haven,
// x 3600-3760, y 2500-2665: Uzeraan's Mansion, the Second Bank of Haven, the smithy, the docks). The old plaza (3460-
// 3530, 2540-2600) is forest and shore on SoS. Every spot below was checked on SoS 3.0's own files (open ground, not
// under an impassable static, its floor's Z; shard-migration notes/cc-P678-tools/townmap.py and spots.py), and keeps
// its place relative to the start: the start on the square's east-west street north of the mansion's garden, Rook,
// the moongate's way out and the quest board beside it. Notes: notes/cc-P678-new-haven-moves.md.
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
    /// send-home. Settable only as a seam for tests. cc-P678: on the square, north of the mansion's garden (was the
    /// plaza's 3503,2574,14).
    /// </summary>
    public static Point3D Start { get; set; } = new(3634, 2586, 0);

    /// <summary>Rook Hallowell, two tiles from the start, as on the old plaza (cc-P80; was 3500,2572,14).</summary>
    public static readonly Point3D Rook = new(3631, 2584, 0);

    /// <summary>Where the isle's dock moongate sets a trainee down: beside Rook (the old travel gate's exit, cc-P289).</summary>
    public static readonly Point3D MoongateExit = new(3632, 2584, 0);

    /// <summary>The quest board, one tile from the start as on the old plaza (NewHavenQuestBoard; was 3502,2573,14).</summary>
    public static readonly Point3D QuestBoard = new(3633, 2585, 0);

    /// <summary>The reset stone (ClusterFResetStone; was 3501,2575,14).</summary>
    public static readonly Point3D ResetStone = new(3632, 2587, 0);

    /// <summary>
    /// The Fountain of Youth, on the square east of the mansion's garden (was 3497,2583,14 on the plaza). Its addon covers
    /// x -2 to +1, y -2 to +1 round this tile: all 16 are open, flat and hold no static on SoS (the garden held 11).
    /// </summary>
    public static readonly Point3D FountainOfYouth = new(3646, 2596, 0);

    /// <summary>
    /// The town's centre, the square's east end. The travel tour starts here; "a vet in New Haven" is 60 tiles round it, so
    /// the vet (52 tiles east), the healer, the bank and the dock all count (the tutorial seeder places a vet only when none
    /// is in reach, so a vet out of reach would be placed again at every run).
    /// </summary>
    public static readonly Point3D Centre = new(3660, 2590, 0);

    /// <summary>The vet, by the farmhouse at the town's east edge, placed when the town has none (cc-P480).</summary>
    public static readonly Point3D VetSpot = new(3712, 2580, 20);

    /// <summary>
    /// New Haven's public moongate: the open field east of the square. S4 (P673) puts PMList's New Haven gate here; the
    /// travel tour points at it. (Was pinned PublicMoongate.cs:194's 3450,2677,25, sea on SoS.)
    /// </summary>
    public static readonly Point3D PublicMoongate = new(3696, 2570, 14);

    /// <summary>The town healer: pinned's pre-UOML Haven healer spawner (Spawns/uoml/trammel/Vendors.json).</summary>
    public static readonly Point3D Healer = new(3665, 2581, 0);

    /// <summary>The bank: the Second Bank of Haven, pinned's pre-UOML Haven banker spawner (uoml Vendors.json).</summary>
    public static readonly Point3D Bank = new(3620, 2617, 0);

    /// <summary>The docks (the author's "Haven / Docks" spot).</summary>
    public static readonly Point3D Dock = new(3650, 2653, 0);

    /// <summary>
    /// The island gate's Haven bounds: pinned Distribution/Data/regions.json, Haven Island NoHousingRegion
    /// (3314,2345,500,750), docks and the water round them included. Start inclusive, end exclusive.
    /// </summary>
    public static readonly Rectangle2D IslandBounds = new(3314, 2345, 500, 750);

    /// <summary>
    /// cc-P678: the town itself on SoS, the bounding box of pinned regions.json's pre-UOML "Haven" town region (3568-3761,
    /// 2460-2712), which is the author's Haven. For "is it in New Haven" checks of our own pieces (the reset stone).
    /// </summary>
    public static readonly Rectangle2D Town = new(3568, 2460, 194, 253);

    public static bool InTown(Point3D p) =>
        p.X >= Town.Start.X && p.X < Town.End.X && p.Y >= Town.Start.Y && p.Y < Town.End.Y;

    public static bool InIslandBounds(Point3D p) =>
        p.X >= IslandBounds.Start.X && p.X < IslandBounds.End.X && p.Y >= IslandBounds.Start.Y && p.Y < IslandBounds.End.Y;
}
