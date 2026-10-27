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

    /// <summary>Rook Hallowell, on the plaza two tiles from the start (cc-P80).</summary>
    public static readonly Point3D Rook = new(3500, 2572, 14);

    /// <summary>Where the isle's dock moongate sets a trainee down: beside Rook (the old travel gate's exit, cc-P289).</summary>
    public static readonly Point3D MoongateExit = new(3501, 2572, 14);

    /// <summary>The town's centre, for "a vet in New Haven" (60 tiles round it).</summary>
    public static readonly Point3D Centre = new(3500, 2590, 0);

    /// <summary>The vet by the stables, placed when the town has none (cc-P480).</summary>
    public static readonly Point3D VetSpot = new(3526, 2578, 7);

    /// <summary>New Haven's public moongate (pinned PublicMoongate.cs:194), the travel tour's first stop.</summary>
    public static readonly Point3D PublicMoongate = new(3450, 2677, 25);

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
