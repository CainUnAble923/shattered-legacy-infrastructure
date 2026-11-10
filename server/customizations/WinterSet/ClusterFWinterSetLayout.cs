// ClusterFWinterSetLayout.cs
//
// cc-P856: where New Haven's winter set stands, as rows of the winter decor (ClusterFWinterDecor places them with the
// rest of the decor on December 1 and takes them down after January 6, by serial). Layer "addon": the row's piece is a
// WinterSeasonAddon of the design DesignOf(key) names, standing with its own tile at X, Y, Z; ItemId is 0 (an addon has
// no art of its own) and Name is the piece's name.
//
// MAIN's New Haven (the Siege of Sosaria map). Each spot was found by shard-migration notes/cc-P856-tools/find856.py,
// which applies P678's own map rules to every tile a piece covers (reached on foot from the square, flat within 2, not
// a door or its swing, an NPC post, a spawner, a protected tile or another winter row, nothing impassable, outdoors) and
// checks the town's walk with the pieces in place. After the first renders it also refuses any standing static under a
// piece (a garden bench) and anything impassable inside its outline (a tree between the train's cars); placed.md in
// notes/cc-P856-review lists every part.
//
//   the park west of the hall      the train in the clearing west of the hall, the snowman bench between it and the
//                                  hall, the Santa bench on the open grass south of the train
//   the grass north of the square  the large snow globe
//
// The holiday fireplace has no row on main: no room in main's New Haven fits it (notes, "Deviations").

using System.Collections.Generic;

namespace Server;

public static class ClusterFWinterSetLayout
{
    public const string AddonLayer = "addon";

    private static readonly Dictionary<string, string> _designs = new()
    {
        ["set-train"] = "christmas-train",
        ["set-snowman-bench"] = "snowman-bench",
        ["set-santa-bench"] = "santa-bench",
        ["set-snow-globe"] = "large-snow-globe"
    };

    public static readonly ClusterFWinterDecorLayout.Row[] Rows =
    {
        new("set-train", "park", 3612, 2539, 0, 0, "a Christmas train", 0, "-", AddonLayer),
        new("set-snowman-bench", "park", 3618, 2545, 0, 0, "a snowman bench", 0, "-", AddonLayer),
        new("set-santa-bench", "park", 3613, 2554, 0, 0, "a Santa bench", 0, "-", AddonLayer),
        new("set-snow-globe", "square", 3639, 2557, 0, 0, "a large snow globe", 0, "-", AddonLayer),
    };

    /// <summary>The design an addon row stands, or null for a key that is not one of these rows.</summary>
    public static string DesignOf(string key) => key != null && _designs.TryGetValue(key, out var d) ? d : null;
}
