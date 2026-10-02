using System.Collections.Generic;
using Server.Items;

namespace Server;

/// <summary>
/// Places the Fountain of Fortune where OSI's stands (cc-P27, bug-list D14, Q-021): Ter Mur 1121, 957, -42, in the
/// Underworld. ServUO places it at exactly that point and nowhere else (Services/Underworld/Generate.cs:183-185);
/// only those lines are taken, not the rest of the Underworld generator. Without it a LuckyCoin can only answer
/// "That is not sacred waters."
///
/// It changes a world only when told to. It never runs at world load, so the live world gains the fountain only
/// when staff run the command there:
///
///   [ClusterFSeedFountainOfFortune dryrun   what would happen, nothing changed
///   [ClusterFSeedFountainOfFortune          place it if it is missing
///
/// Re-running never duplicates: a FountainOfFortune within SearchRange of the point counts as present, wherever
/// staff may have nudged it.
/// </summary>
public static class ClusterFFountainOfFortuneSeeder
{
    public static readonly Point3D Location = new(1121, 957, -42);

    // The addon spans 4 x 4 tiles around its centre; a few tiles more covers a hand-placed one nearby.
    public const int SearchRange = 5;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFSeedFountainOfFortune", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFSeedFountainOfFortune [dryrun]")]
    [Description("Places the Fountain of Fortune at its OSI point in the Ter Mur Underworld (cc-P27) if none is there.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Fountain of Fortune in the Ter Mur Underworld if it is missing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        if (mode is not ("" or "dryrun"))
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedFountainOfFortune [dryrun]");
            return;
        }

        foreach (var line in Seed(dryRun: mode == "dryrun"))
            e.Mobile.SendMessage(line);
    }

    public static FountainOfFortune FindNear(Map map)
    {
        foreach (var item in map.GetItemsInRange(Location, SearchRange))
        {
            if (item is FountainOfFortune { Deleted: false } fountain)
                return fountain;
        }

        return null;
    }

    public static List<string> Seed(bool dryRun)
    {
        var report = new List<string>();
        var map = Map.TerMur;

        if (map == null || map == Map.Internal)
        {
            report.Add("Fountain of Fortune: Ter Mur is not loaded, nothing placed.");
            return report;
        }

        var existing = FindNear(map);
        if (existing != null)
        {
            report.Add($"Fountain of Fortune: already there at {existing.Location}, nothing placed.");
            return report;
        }

        if (!dryRun)
            new FountainOfFortune().MoveToWorld(Location, map);

        report.Add($"Fountain of Fortune: {(dryRun ? "would place" : "placed")} at {Location} in Ter Mur.");
        return report;
    }
}
