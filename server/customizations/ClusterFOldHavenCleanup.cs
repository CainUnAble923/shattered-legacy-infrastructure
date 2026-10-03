using System;
using System.Linq;
using System.Collections.Generic;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// One-shot cleanup: removes misplaced vendors, Newbie Manor guards,
/// Uzeraan's old quest hub, and any spawners that were generating those
/// NPCs from the Old Haven ruins on Trammel.
///
/// NPC targets are matched by class name + coordinates (±8 tile tolerance).
/// Spawner targets are matched by scanning spawner entries for any of the
/// vendor type names and confirming the spawner is in the Old Haven area —
/// no coordinate pin needed, so relocated spawners are still caught.
///
/// Stock spawners are never touched (cc-P42 Part B): any spawner pinned's post-uoml/** or shared/**
/// spawn files place on Trammel (ClusterFStockSpawnData), and any NPC one of them spawned, is left
/// alone, whatever it spawns. Before this the box reached into New Haven (X 3416-3570) and took
/// pinned's Blacksmith Guildmaster spawner at 3526,2536,20 (P21 section 7 defect 2), and also
/// pinned's Healer spawner in the ruins at 3618,2614,0 with its Healer.
///
/// Usage:
///   [ClusterFOldHavenCleanup          — deletes all targets
///   [ClusterFOldHavenCleanup dryrun   — reports what would be deleted
/// </summary>
public static class ClusterFOldHavenCleanup
{
    // (typeName, x, y) — sourced from [ClusterFAreaScan haven output 2026-05-12
    private static readonly (string Type, int X, int Y)[] NpcTargets =
    [
        // ── Newbie Manor guards ──────────────────────────────────────────
        ("MansionGuard",           3585, 2586),
        ("MansionGuard",           3585, 2588),
        ("MansionGuard",           3603, 2583),
        ("MansionGuard",           3613, 2586),
        ("MansionGuard",           3613, 2589),

        // ── Uzeraan quest hub (pre-ML Haven system, now unreachable) ────
        ("Uzeraan",                3595, 2587),
        ("Blacksmith",             3594, 2593),   // Justine — part of quest hub
        ("BlacksmithGuildmaster",  3596, 2595),   // Gabriella — part of quest hub

        // ── Normal town vendors stranded in the ruins ────────────────────
        ("Baker",                  3629, 2541),
        ("Minter",                 3621, 2618),
        ("Banker",                 3622, 2615),
        ("Mage",                   3631, 2571),
        ("Alchemist",              3632, 2572),
        ("MageGuildmaster",        3632, 2575),
        ("Mapmaker",               3634, 2636),
        ("Shipwright",             3635, 2635),
        ("Blacksmith",             3643, 2614),
        ("BlacksmithGuildmaster",  3645, 2615),
        ("RealEstateBroker",       3640, 2502),
        ("Carpenter",              3642, 2505),
        ("Architect",              3643, 2502),
        ("BardGuildmaster",        3662, 2529),
        ("Tinker",                 3666, 2508),
        ("TinkerGuildmaster",      3668, 2505),
        ("HealerGuildmaster",      3669, 2582),
        ("Healer",                 3617, 2614),
        ("Healer",                 3665, 2579),
        ("InnKeeper",              3671, 2615),
        ("Provisioner",            3673, 2595),
        ("Cobbler",                3674, 2595),
        ("Scribe",                 3614, 2476),
        ("TailorGuildmaster",      3687, 2478),
        ("Tailor",                 3688, 2476),
        ("Weaver",                 3688, 2478),
        ("ElwoodMcCarrin",         3666, 2652),
        ("TavernKeeper",           3667, 2653),
        ("Barkeeper",              3667, 2652),
        ("Waiter",                 3669, 2654),
        ("Cook",                   3670, 2653),
        ("Butcher",                3717, 2649),
        ("Bowyer",                 3745, 2584),
        ("WarriorGuildmaster",     3738, 2690),
        ("MinerGuildmaster",       3752, 2704),
        ("ThiefGuildmaster",       3696, 2510),

        // ── Misplaced static NPCs in ruins ───────────────────────────────
        ("SeekerOfAdventure",      3695, 2701),
        ("SeekerOfAdventure",      3698, 2702),
        ("Noble",                  3748, 2709),
    ];

    // Any spawner in Old Haven whose entry list contains one of these type
    // names is deleted.  This catches spawners that were generating the
    // vendor NPCs above — including the Carpenter/RealEstateBroker spawner
    // that escaped the first manual cleanup pass.
    internal static readonly HashSet<string> SpawnerVendorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MansionGuard",
        "Uzeraan",
        "Baker", "Minter", "Banker",
        "Mage", "Alchemist", "MageGuildmaster",
        "Mapmaker", "Shipwright",
        "Blacksmith", "BlacksmithGuildmaster",
        "RealEstateBroker", "Carpenter", "Architect",
        "BardGuildmaster", "Tinker", "TinkerGuildmaster",
        "HealerGuildmaster", "Healer", "InnKeeper",
        "Provisioner", "Cobbler", "Scribe",
        "TailorGuildmaster", "Tailor", "Weaver",
        "ElwoodMcCarrin", "TavernKeeper", "Barkeeper",
        "Waiter", "Cook", "Butcher", "Bowyer",
        "WarriorGuildmaster", "MinerGuildmaster", "ThiefGuildmaster",
    };

    // Old Haven ruins bounding box used for spawner scan. It overlaps New Haven (whose west half ends at
    // X 3545, ClusterFNewHavenServicesSeeder; its Necromancers Guild Hall reaches X 3559), so the box alone
    // never decides: stock spawners are excluded by IsStock below.
    internal static bool IsOldHavenArea(Point3D loc) =>
        loc.X >= 3520 && loc.X <= 3760 &&
        loc.Y >= 2420 && loc.Y <= 2760;

    // cc-P42 Part B: every stock Trammel spawner inside the box, from the shipped spawn files.
    internal static List<ClusterFStockSpawnData.StockSpawner> StockInBox() =>
        ClusterFStockSpawnData.Load(Map.Trammel, ClusterFStockSpawnData.ShardFolders)
            .Where(s => IsOldHavenArea(s.Location))
            .ToList();

    private static bool IsStock(BaseSpawner spawner, List<ClusterFStockSpawnData.StockSpawner> stock) =>
        stock.Any(s => s.Matches(spawner));

    // How many tiles from the recorded position we still consider an NPC match.
    private const int Tolerance = 8;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFOldHavenCleanup", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFOldHavenCleanup [dryrun]")]
    [Description("Removes misplaced Old Haven vendors, guards, and their spawners.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, DryRun = "dryrun", Summary = "DELETES stray Old Haven vendors, guards and their spawners. Any argument but dryrun deletes.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 &&
                     e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);

        foreach (var line in Run(dryRun))
        {
            e.Mobile.SendMessage(line);
        }
    }

    /// <summary>The cleanup; returns the lines the command shows (cc-P42 Part B, so a test reads them).</summary>
    internal static List<string> Run(bool dryRun)
    {
        var lines = new List<string>();
        var npcDeleted      = 0;
        var spawnerDeleted  = 0;
        var notFound        = 0;
        var stockKept       = 0;

        // cc-P42 Part B. Without the stock spawn files nothing can be told apart from New Haven's own
        // spawners, so refuse rather than guess.
        var stock = StockInBox();
        if (stock.Count == 0)
        {
            lines.Add(
                $"ClusterF Old Haven cleanup: no stock Trammel spawners read from {ClusterFStockSpawnData.SpawnsDirectory}. " +
                "Nothing done: without them New Haven's spawners cannot be told apart.");
            return lines;
        }

        // ── 1. Delete NPC targets ────────────────────────────────────────
        var lookup = new Dictionary<string, List<(int X, int Y)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (type, x, y) in NpcTargets)
        {
            if (!lookup.TryGetValue(type, out var list))
                lookup[type] = list = new List<(int, int)>();
            list.Add((x, y));
        }

        var foundNpcs = new HashSet<(string, int, int)>();

        // Deleted after each scan, not inside it: deleting removes from the collection being enumerated.
        var toDelete = new List<IEntity>();

        foreach (var m in World.Mobiles.Values)
        {
            if (m.Deleted || m.Map != Map.Trammel) continue;

            var typeName = m.GetType().Name;
            if (!lookup.TryGetValue(typeName, out var coords)) continue;

            // Spawned by a stock spawner: stock content, never a stray (cc-P42 Part B).
            if (m is ISpawnable { Spawner: BaseSpawner owner } && IsStock(owner, stock)) continue;

            foreach (var (tx, ty) in coords)
            {
                if (Math.Abs(m.X - tx) > Tolerance || Math.Abs(m.Y - ty) > Tolerance)
                    continue;

                foundNpcs.Add((typeName, tx, ty));

                if (dryRun)
                    lines.Add($"[DRY RUN] NPC: {typeName} \"{m.Name}\" at ({m.X},{m.Y})");
                else
                {
                    Console.WriteLine($"[ClusterFOldHavenCleanup] Deleting NPC {typeName} \"{m.Name}\" at ({m.X},{m.Y})");
                    toDelete.Add(m);
                }

                npcDeleted++;
                break;
            }
        }

        foreach (var (type, x, y) in NpcTargets)
        {
            if (!foundNpcs.Contains((type, x, y)))
            {
                lines.Add($"[NOT FOUND] {type} near ({x},{y}): already gone or moved.");
                notFound++;
            }
        }

        // ── 2. Delete vendor spawners in Old Haven ───────────────────────
        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel) continue;
            if (!IsOldHavenArea(item.Location)) continue;
            if (item is not Spawner spawner) continue;

            // Check whether any entry in this spawner spawns a vendor type
            // we want gone.
            var matched = false;
            foreach (var entry in spawner.Entries)
            {
                if (SpawnerVendorTypes.Contains(entry.SpawnedName))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched) continue;

            var entryNames = string.Join(", ", spawner.Entries.Select(se => se.SpawnedName));

            if (IsStock(spawner, stock))
            {
                lines.Add($"[STOCK, KEPT] Spawner at ({spawner.X},{spawner.Y},{spawner.Z}) entries: {entryNames}");
                stockKept++;
                continue;
            }

            if (dryRun)
                lines.Add($"[DRY RUN] Spawner at ({spawner.X},{spawner.Y}) entries: {entryNames}");
            else
            {
                Console.WriteLine($"[ClusterFOldHavenCleanup] Deleting spawner at ({spawner.X},{spawner.Y}) entries: {entryNames}");
                toDelete.Add(spawner);
            }

            spawnerDeleted++;
        }

        foreach (var entity in toDelete)
        {
            entity.Delete();
        }

        var verb = dryRun ? "Would delete" : "Deleted";
        lines.Add(
            $"ClusterF Old Haven cleanup {(dryRun ? "dry run" : "complete")}: " +
            $"{verb} {npcDeleted} NPCs, {spawnerDeleted} spawners. Not found: {notFound}. " +
            $"Stock spawners left alone: {stockKept}."
        );
        return lines;
    }
}
