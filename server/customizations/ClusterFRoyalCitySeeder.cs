using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Seeds Ter Mur's Royal City with gargoyle-appropriate NPCs and a public moongate.
///
/// Problems solved:
///   • Human vendors appeared in Royal City instead of gargoyle vendors because the
///     vanilla Vendors.json spawn file uses generic class names (Baker, Blacksmith, …)
///     which resolve to human-bodied NPCs.
///   • The Royal City public moongate may not have been placed by [GenMoonGates if
///     the expansion flag was not active at world creation time.
///
/// Commands:
///   [SeedRoyalCity          — Seeds gargoyle vendors + moongate (safe to re-run).
///   [ClearRoyalCityVendors  — Removes gargoyle vendors + moongates in Royal City
///                             bounds. Use before re-seeding during testing.
///
/// Seeding is idempotent: existing gargoyle vendors of each type are counted and
/// missing ones are added. The moongate is placed only if none exists within 15 tiles.
///
/// Royal City vendor bounds (TerMur map):
///   X: 680 – 870,  Y: 3370 – 3540
/// The area at (940–1025, 3870–3940) is the Underworld entrance — intentionally
/// left as human-vendor spawners (for non-gargoyle adventurers or future redesign).
/// </summary>
public static class ClusterFRoyalCitySeeder
{
    // ── Royal City geographic bounds ──────────────────────────────────────────

    private const int RcX1 = 680,  RcX2 = 870;
    private const int RcY1 = 3370, RcY2 = 3540;

    // ── Moongate location (matches PublicMoongate.PMList.TerMur) ──────────────

    private static readonly Point3D MoongateLocation = new(850, 3525, -38);
    private const int MoongateHue = 0;   // standard blue/purple gate

    // ── Vendor placement table ────────────────────────────────────────────────
    //
    // Each entry mirrors one spawner from Vendors.json (Royal City section only).
    // Coordinates are taken verbatim from the JSON so vendors appear at the same
    // positions the spawner system would have used.
    //
    // Format: (x, y, z, facing, gargoyle type)

    private static readonly (int X, int Y, int Z, Direction Dir, Func<Mobile> Factory)[] _vendors =
    {
        (740, 3466, -19, Direction.South,  () => new GargoyleInnKeeper()),
        (772, 3467, -20, Direction.South,  () => new GargoyleScribe()),
        (773, 3478, -20, Direction.East,   () => new GargoyleHerbalist()),
        (778, 3478, -20, Direction.East,   () => new GargoyleAlchemist()),
        (783, 3478, -20, Direction.East,   () => new GargoyleCustomHairstylist()),
        (772, 3490, -20, Direction.South,  () => new GargoyleMage()),
        (777, 3490, -20, Direction.South,  () => new GargoyleAlchemist()),
        (782, 3490, -20, Direction.South,  () => new GargoyleMageGuildmaster()),
        (783, 3491, -20, Direction.West,   () => new GargoyleHealer()),
        (788, 3491, -20, Direction.West,   () => new GargoyleHealerGuildmaster()),
        (797, 3494,   0, Direction.North,  () => new GargoyleProvisioner()),
        (802, 3494,   0, Direction.North,  () => new GargoyleCobbler()),
        (805, 3491, -20, Direction.East,   () => new GargoyleBaker()),
        (817, 3456, -10, Direction.South,  () => new GargoyleButcher()),
        (817, 3438,   0, Direction.West,   () => new GargoyleCarpenter()),
        (822, 3438,   0, Direction.West,   () => new GargoyleArchitect()),
        (827, 3438,   0, Direction.West,   () => new GargoyleRealEstateBroker()),
        (817, 3426,   0, Direction.South,  () => new GargoyleTinker()),
        (822, 3426,   0, Direction.South,  () => new GargoyleTinkerGuildmaster()),
        (816, 3419,   0, Direction.South,  () => new GargoyleBlacksmithGuildmaster()),
        (811, 3419,   0, Direction.South,  () => new GargoyleBlacksmithGuildmaster()),
        (805, 3397,   0, Direction.East,   () => new GargoyleTailor()),
        (810, 3397,   0, Direction.East,   () => new GargoyleWeaver()),
        (815, 3397,   0, Direction.East,   () => new GargoyleTailorGuildmaster()),
        (804, 3387,   0, Direction.North,  () => new GargoyleTanner()),
        (809, 3387,   0, Direction.North,  () => new GargoyleFurtrader()),
        (851, 3404, -20, Direction.West,   () => new GargoyleAnimalTrainer()),
        (833, 3439, -20, Direction.South,  () => new GargoyleBanker()),
        (838, 3439, -20, Direction.South,  () => new GargoyleMinter()),
        (843, 3439, -20, Direction.South,  () => new GargoyleBanker()),
        (848, 3439, -20, Direction.South,  () => new GargoyleMinter()),
        (702, 3435, -20, Direction.East,   () => new GargoyleTailor()),
        (707, 3435, -20, Direction.East,   () => new GargoyleWeaver()),
        (727, 3425, -20, Direction.South,  () => new GargoyleArmorer()),
        (732, 3425, -20, Direction.South,  () => new GargoyleWeaponsmith()),
        (715, 3446, -20, Direction.North,  () => new GargoyleFarmer()),
        (720, 3446, -20, Direction.North,  () => new GargoyleFarmer()),
        (713, 3438, -20, Direction.East,   () => new GargoyleTavernKeeper()),
        (718, 3438, -20, Direction.East,   () => new GargoyleWaiter()),
        (723, 3438, -20, Direction.East,   () => new GargoyleBarkeeper()),
        (794, 3446, -10, Direction.West,   () => new GargoyleFarmer()),
        (799, 3446, -10, Direction.West,   () => new GargoyleFarmer()),
        (779, 3431, -10, Direction.South,  () => new GargoyleJeweler()),
        (842, 3453, -18, Direction.North,  () => new GargoyleTownCrier()),
        (811, 3427,   0, Direction.South,  () => new GargoyleStoneCrafter()),
    };

    // ── Registration ──────────────────────────────────────────────────────────

    public static void Configure()
    {
        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("SeedRoyalCity",         AccessLevel.Administrator, OnSeedCommand);
        CommandSystem.Register("ClearRoyalCityVendors", AccessLevel.Administrator, OnClearCommand);
    }

    private static void OnWorldLoad()
    {
        // Auto-seed on world load if no gargoyle vendors are present yet.
        if (!HasAnyGargoyleVendors())
            Seed(verbose: false);
    }

    private static void OnSeedCommand(CommandEventArgs e)
    {
        Seed(verbose: true);
        e.Mobile.SendMessage(0x44, "[SeedRoyalCity complete.");
    }

    private static void OnClearCommand(CommandEventArgs e)
    {
        ClearRoyalCity(e.Mobile);
        e.Mobile.SendMessage(0x22, "[ClearRoyalCityVendors complete.");
    }

    // ── Seeding ───────────────────────────────────────────────────────────────

    private static void Seed(bool verbose)
    {
        // 1. Moongate
        int gates = EnsureMoongate(verbose);

        // 2. Vendors — delete old human vendor mobiles in bounds, then place gargoyles
        int removed = RemoveHumanVendors();
        int placed  = PlaceVendors(verbose);

        if (verbose || removed > 0 || placed > 0 || gates > 0)
        {
            Console.WriteLine(
                $"[ClusterFRoyalCitySeeder] Seeding complete: " +
                $"{gates} moongate(s) placed, {removed} human vendor(s) removed, {placed} gargoyle vendor(s) placed.");
        }
    }

    private static int EnsureMoongate(bool verbose)
    {
        // Check if a PublicMoongate already exists near the expected location.
        foreach (var item in Map.TerMur.GetItemsInRange(MoongateLocation, 15))
        {
            if (item is PublicMoongate)
            {
                if (verbose)
                    Console.WriteLine("[ClusterFRoyalCitySeeder] Royal City moongate already present — skipping.");
                return 0;
            }
        }

        var gate = new PublicMoongate();
        gate.MoveToWorld(MoongateLocation, Map.TerMur);

        if (verbose)
            Console.WriteLine($"[ClusterFRoyalCitySeeder] Placed Royal City moongate at {MoongateLocation}.");
        return 1;
    }

    private static int RemoveHumanVendors()
    {
        // Find and delete human-bodied vendor mobiles in Royal City bounds.
        // We target Body 0x190 (human male) and 0x191 (human female) to avoid
        // accidentally removing gargoyle vendors we've already placed.
        var toDelete = new List<Mobile>();

        foreach (var m in Map.TerMur.GetMobilesInRange(
                     new Point3D((RcX1 + RcX2) / 2, (RcY1 + RcY2) / 2, 0),
                     (RcX2 - RcX1) / 2 + 10))
        {
            if (m == null || m.Deleted) continue;
            if (m.X < RcX1 || m.X > RcX2 || m.Y < RcY1 || m.Y > RcY2) continue;
            if (m.Map != Map.TerMur) continue;

            // Only remove human-bodied NPCs that are vendors or guildmasters.
            if ((m.Body == 0x190 || m.Body == 0x191) &&
                (m is BaseVendor or BaseGuildmaster or TownCrier))
            {
                toDelete.Add(m);
            }
        }

        foreach (var m in toDelete)
            m.Delete();

        return toDelete.Count;
    }

    private static int PlaceVendors(bool verbose)
    {
        var placed = 0;

        foreach (var (x, y, z, dir, factory) in _vendors)
        {
            var pt = new Point3D(x, y, z);

            // Skip if a gargoyle (body 666/667) mobile already exists within 2 tiles.
            bool exists = false;
            foreach (var m in Map.TerMur.GetMobilesInRange(pt, 2))
            {
                if ((m.Body == 666 || m.Body == 667) && m.Map == Map.TerMur)
                { exists = true; break; }
            }
            if (exists) continue;

            var npc = factory();
            npc.MoveToWorld(pt, Map.TerMur);
            npc.Direction = dir;
            placed++;
        }

        return placed;
    }

    // ── Clear helper (testing) ────────────────────────────────────────────────

    private static void ClearRoyalCity(Mobile admin)
    {
        int removedVendors = 0, removedGates = 0;

        var mobsToDelete  = new List<Mobile>();
        var itemsToDelete = new List<Item>();

        foreach (var m in Map.TerMur.GetMobilesInRange(
                     new Point3D((RcX1 + RcX2) / 2, (RcY1 + RcY2) / 2, 0),
                     (RcX2 - RcX1) / 2 + 10))
        {
            if (m == null || m.Deleted) continue;
            if (m.X < RcX1 || m.X > RcX2 || m.Y < RcY1 || m.Y > RcY2) continue;
            if ((m.Body == 666 || m.Body == 667) &&
                (m is BaseVendor or BaseGuildmaster or TownCrier))
                mobsToDelete.Add(m);
        }

        foreach (var item in Map.TerMur.GetItemsInRange(MoongateLocation, 20))
            if (item is PublicMoongate) itemsToDelete.Add(item);

        foreach (var m in mobsToDelete) { m.Delete(); removedVendors++; }
        foreach (var i in itemsToDelete) { i.Delete(); removedGates++; }

        admin.SendMessage($"Cleared {removedVendors} gargoyle vendor(s) and {removedGates} moongate(s) from Royal City.");
    }

    // ── Check ─────────────────────────────────────────────────────────────────

    private static bool HasAnyGargoyleVendors()
    {
        foreach (var m in World.Mobiles.Values)
        {
            if (m is GargoyleAlchemist g && g.Map == Map.TerMur && !g.Deleted)
                return true;
        }
        return false;
    }
}
