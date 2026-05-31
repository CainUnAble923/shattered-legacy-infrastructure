using System;
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
    private static readonly HashSet<string> SpawnerVendorTypes = new(StringComparer.OrdinalIgnoreCase)
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

    // Old Haven ruins bounding box used for spawner scan.
    private static bool IsOldHavenArea(Point3D loc) =>
        loc.X >= 3520 && loc.X <= 3760 &&
        loc.Y >= 2420 && loc.Y <= 2760;

    // How many tiles from the recorded position we still consider an NPC match.
    private const int Tolerance = 8;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFOldHavenCleanup", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFOldHavenCleanup [dryrun]")]
    [Description("Removes misplaced Old Haven vendors, guards, and their spawners.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 &&
                     e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);

        var npcDeleted      = 0;
        var spawnerDeleted  = 0;
        var notFound        = 0;

        // ── 1. Delete NPC targets ────────────────────────────────────────
        var lookup = new Dictionary<string, List<(int X, int Y)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (type, x, y) in NpcTargets)
        {
            if (!lookup.TryGetValue(type, out var list))
                lookup[type] = list = new List<(int, int)>();
            list.Add((x, y));
        }

        var foundNpcs = new HashSet<(string, int, int)>();

        foreach (var m in World.Mobiles.Values)
        {
            if (m.Deleted || m.Map != Map.Trammel) continue;

            var typeName = m.GetType().Name;
            if (!lookup.TryGetValue(typeName, out var coords)) continue;

            foreach (var (tx, ty) in coords)
            {
                if (Math.Abs(m.X - tx) > Tolerance || Math.Abs(m.Y - ty) > Tolerance)
                    continue;

                foundNpcs.Add((typeName, tx, ty));

                if (dryRun)
                    e.Mobile.SendMessage($"[DRY RUN] NPC: {typeName} \"{m.Name}\" at ({m.X},{m.Y})");
                else
                {
                    Console.WriteLine($"[ClusterFOldHavenCleanup] Deleting NPC {typeName} \"{m.Name}\" at ({m.X},{m.Y})");
                    m.Delete();
                }

                npcDeleted++;
                break;
            }
        }

        foreach (var (type, x, y) in NpcTargets)
        {
            if (!foundNpcs.Contains((type, x, y)))
            {
                e.Mobile.SendMessage($"[NOT FOUND] {type} near ({x},{y}) — already gone or moved.");
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

            var entryNames = string.Join(", ", spawner.Entries.ConvertAll(se => se.SpawnedName));

            if (dryRun)
                e.Mobile.SendMessage($"[DRY RUN] Spawner at ({spawner.X},{spawner.Y}) entries: {entryNames}");
            else
            {
                Console.WriteLine($"[ClusterFOldHavenCleanup] Deleting spawner at ({spawner.X},{spawner.Y}) entries: {entryNames}");
                spawner.Delete();
            }

            spawnerDeleted++;
        }

        var verb = dryRun ? "Would delete" : "Deleted";
        e.Mobile.SendMessage(
            $"ClusterF Old Haven cleanup {(dryRun ? "dry run" : "complete")}: " +
            $"{verb} {npcDeleted} NPCs, {spawnerDeleted} spawners. Not found: {notFound}."
        );
    }
}
