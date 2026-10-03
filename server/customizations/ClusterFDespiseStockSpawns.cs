using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P42 Part J, bug-list D53; Chase, 2026-10-03). Trammel Despise runs the revamp only: the stock
/// pre-revamp creature spawners of upstream's Data/Spawns/shared/trammel/Despise.json (43 spawners: 30 creature-only,
/// 11 treasure-chest-only, 2 mixed) are not placed, and the ones a world already has are removed. Kept: every treasure
/// chest entry (F-32 makes them worth searching; a mixed spawner keeps its chests and loses its creatures), Felucca
/// Despise (stays classic: its own file, shared/felucca/Despise.json, is never touched), and everything [SetupDespise
/// placed (DespiseSpawns, which the JSON importer never sees and whose spawners are not in the file).
///
/// Which spawners are stock is read from the shipped file itself (ClusterFStockSpawnData), never a hand-kept list: an
/// upstream change to the file changes the set, and a fact imports the real file.
///
///   Fresh world: ImportSpawnersCommand calls Admit for every spawner it builds (server/patches/
///     ImportSpawners-despise-exclusion.patch), so [GenerateSpawners of shared/trammel/Despise.json, of shared/**, or
///     the admin gump's world build places the chests and no creatures.
///   Existing world: [ClusterFDespiseStockCleanup dryrun, then [ClusterFDespiseStockCleanup.
/// </summary>
public static class ClusterFDespiseStockSpawns
{
    public const string TrammelFile = "shared/trammel/Despise.json";

    private static HashSet<Guid> _trammelGuids;

    /// <summary>The stock Trammel Despise spawners, from the shipped file.</summary>
    public static List<ClusterFStockSpawnData.StockSpawner> TrammelStock() =>
        ClusterFStockSpawnData.LoadRelative(TrammelFile, Map.Trammel);

    private static HashSet<Guid> TrammelGuids =>
        _trammelGuids ??= TrammelStock().Select(s => s.Guid).ToHashSet();

    public static bool IsChest(string spawnedName) =>
        spawnedName?.StartsWith("TreasureChestLevel", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Import hook. A stock Trammel Despise spawner keeps only its treasure chest entries; one with none left is not
    /// placed (false). Every other spawner, on every map, is admitted untouched.
    /// </summary>
    public static bool Admit(BaseSpawner spawner, Map map)
    {
        if (map != Map.Trammel || !TrammelGuids.Contains(spawner.Guid))
        {
            return true;
        }

        StripCreatures(spawner);
        return spawner.Entries.Count > 0;
    }

    private static List<string> StripCreatures(BaseSpawner spawner)
    {
        var removed = new List<string>();
        foreach (var entry in spawner.Entries.ToList())
        {
            if (!IsChest(entry.SpawnedName))
            {
                removed.Add(entry.SpawnedName);
                spawner.RemoveEntry(entry); // deletes what the entry spawned (BaseSpawner.Entries.cs:93-110)
            }
        }

        return removed;
    }

    public static void Configure()
    {
        CommandSystem.Register("ClusterFDespiseStockCleanup", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFDespiseStockCleanup [dryrun]")]
    [Description("Removes the stock pre-revamp creature spawners from Trammel Despise (keeps their treasure chests, Felucca and the revamp).")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "DELETES Trammel Despise's stock creature spawners and their creatures; keeps chest spawners, Felucca Despise and [SetupDespise's spawners.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var arg = e.Length > 0 ? e.GetString(0) : "";
        var dryRun = arg.Equals("dryrun", StringComparison.OrdinalIgnoreCase);

        // Only the dry-run word or nothing: a typo must not delete (P21 section 7 defect 2 on the Old Haven cleanup).
        if (arg.Length > 0 && !dryRun)
        {
            e.Mobile.SendMessage(0x22, "Usage: [ClusterFDespiseStockCleanup [dryrun]");
            return;
        }

        foreach (var line in Run(dryRun))
        {
            e.Mobile.SendMessage(line);
        }
    }

    /// <summary>The cleanup; returns the lines the command shows.</summary>
    public static List<string> Run(bool dryRun)
    {
        var lines = new List<string>();
        var stock = TrammelStock();

        if (stock.Count == 0)
        {
            lines.Add($"ClusterF Despise stock cleanup: no stock spawners read from {TrammelFile} in " +
                      $"{ClusterFStockSpawnData.SpawnsDirectory}. Nothing done.");
            return lines;
        }

        var found = new List<(BaseSpawner Spawner, bool Mixed)>();
        var chestsKept = 0;

        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel || item is not BaseSpawner spawner || !stock.Any(s => s.Matches(spawner)))
            {
                continue;
            }

            var chests = spawner.Entries.Count(entry => IsChest(entry.SpawnedName));
            if (chests == spawner.Entries.Count)
            {
                chestsKept++;
                continue;
            }

            found.Add((spawner, chests > 0));
        }

        var deleted = 0;
        var stripped = 0;

        foreach (var (spawner, mixed) in found)
        {
            var names = string.Join(", ", spawner.Entries.Select(entry => entry.SpawnedName));
            if (mixed)
            {
                var creatures = string.Join(", ", spawner.Entries.Where(entry => !IsChest(entry.SpawnedName)).Select(entry => entry.SpawnedName));
                lines.Add($"{(dryRun ? "[DRY RUN] would strip" : "stripped")} {creatures} from the spawner at {spawner.Location} ({names}); its chests stay.");
                if (!dryRun)
                {
                    StripCreatures(spawner);
                }

                stripped++;
            }
            else
            {
                lines.Add($"{(dryRun ? "[DRY RUN] would delete" : "deleted")} the spawner at {spawner.Location} ({names}) and what it spawned.");
                if (!dryRun)
                {
                    spawner.Delete();
                }

                deleted++;
            }
        }

        lines.Add(
            $"ClusterF Despise stock cleanup {(dryRun ? "dry run" : "complete")} (Trammel only): " +
            $"{(dryRun ? "would delete" : "deleted")} {deleted} creature spawners, {(dryRun ? "would strip" : "stripped")} " +
            $"the creatures from {stripped} mixed ones, kept {chestsKept} chest spawners."
        );
        return lines;
    }
}
