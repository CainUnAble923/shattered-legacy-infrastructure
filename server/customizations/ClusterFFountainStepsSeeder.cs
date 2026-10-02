using System.Collections.Generic;
using Server.Items;

namespace Server;

/// <summary>
/// Stepping stones to the Fountain of Fortune (cc-P29 Part D, bug-list D14). The fountain stands at OSI's point,
/// Ter Mur 1121, 957, -42, in the middle of a pool (ClusterFFountainOfFortuneSeeder). A line of rocks runs from the
/// south shore to it, but every one of them is tiledata "Impassable" with no "Surface" flag, in the server's client
/// data (7.0.114.40) and in EA's 7.0.117.0 alike, so nobody can walk them and no tile a player can reach is within
/// a lucky coin's range of 3 (LuckyCoin.cs:35). Walked under pinned's movement rules, the nearest reachable tile is 8
/// away (shard-migration notes/cc-P29-tools/fountain_walk.py).
///
/// This lays one walkable paver (existing art, "PaverStones Natural", height 1, Surface) over each of those twelve
/// rocks, at z -41 so that it clears the tallest rock, ending one tile from the fountain. A deliberate deviation:
/// OSI's stones are decoration. The fountain is not moved.
///
/// It changes a world only when told to. It never runs at world load:
///
///   [ClusterFSeedFountainSteps dryrun   what would happen, nothing changed
///   [ClusterFSeedFountainSteps          place each missing stone
///
/// Re-running never duplicates: a paver of this art already on a stone's tile counts as that stone.
/// </summary>
public static class ClusterFFountainStepsSeeder
{
    // From the south shore (1121..1124, 966, z -42, reachable from the Trammel entrance) to the fountain's
    // south-east corner. Each is a rock in the client statics (notes/cc-P29-tools/stones.py).
    public static readonly Point2D[] Steps =
    {
        new(1122, 965), new(1121, 965), new(1123, 965), new(1122, 964), new(1123, 964), new(1123, 963),
        new(1123, 962), new(1123, 961), new(1122, 961), new(1122, 960), new(1121, 960), new(1121, 959)
    };

    // The tallest rock under a step tops out at z -40 (0x1363 at -43, height 3; 0x136A and 0x1367 at -42, height 2).
    // A height-1 paver at -41 stands a player at -40: clear of every rock, and a step of 2 up from the shore's -42.
    public const int Z = -41;

    // "PaverStones Natural": tiledata Surface only, height 1, in both client data versions.
    public static readonly int[] Art = { 0x47DC, 0x47DD, 0x47DE, 0x47DF, 0x47E0, 0x47E5 };

    public static void Configure()
    {
        CommandSystem.Register("ClusterFSeedFountainSteps", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFSeedFountainSteps [dryrun]")]
    [Description("Lays walkable stepping stones over the rocks in the Fountain of Fortune's pool (cc-P29) where missing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Lays the stepping stones to the Fountain of Fortune in the Ter Mur Underworld where missing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        if (mode is not ("" or "dryrun"))
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedFountainSteps [dryrun]");
            return;
        }

        foreach (var line in Seed(dryRun: mode == "dryrun"))
            e.Mobile.SendMessage(line);
    }

    public static bool IsStep(Item item) => item is Static { Deleted: false } && System.Array.IndexOf(Art, item.ItemID) >= 0;

    public static Item FindAt(Map map, Point2D p)
    {
        foreach (var item in map.GetItemsInRange(new Point3D(p.X, p.Y, Z), 0))
        {
            if (item.X == p.X && item.Y == p.Y && IsStep(item))
                return item;
        }

        return null;
    }

    public static List<string> Seed(bool dryRun)
    {
        var report = new List<string>();
        var map = Map.TerMur;

        if (map == null || map == Map.Internal)
        {
            report.Add("Fountain steps: Ter Mur is not loaded, nothing placed.");
            return report;
        }

        int placed = 0, present = 0;

        for (var i = 0; i < Steps.Length; i++)
        {
            var p = Steps[i];

            if (FindAt(map, p) != null)
            {
                present++;
                continue;
            }

            if (!dryRun)
            {
                var stone = new Static(Art[i % Art.Length]) { Name = "stepping stone" };
                stone.MoveToWorld(new Point3D(p.X, p.Y, Z), map);
            }

            placed++;
        }

        report.Add(
            $"Fountain steps: {(dryRun ? "would place" : "placed")} {placed}, already there {present}, of {Steps.Length} " +
            $"{Steps[0]} to {Steps[^1]} at z {Z} in Ter Mur."
        );

        if (ClusterFFountainOfFortuneSeeder.FindNear(map) == null)
            report.Add("Fountain steps: no Fountain of Fortune near 1121, 957 yet; run [ClusterFSeedFountainOfFortune too.");

        return report;
    }
}
