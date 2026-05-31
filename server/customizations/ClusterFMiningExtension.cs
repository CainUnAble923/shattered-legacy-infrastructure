using System;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;

namespace Server;

/// <summary>
/// Shattered Legacy — Mining extension.
///
/// Registers extended ore veins (Platinum through Celestial, Tiers 10–17) into the
/// existing Mining harvest definition, and adds the matching ingot types to the
/// Blacksmithy craft sub-resource list.
///
/// Startup timing:
///   Configure() fires before World.Load() — we hook EventSink.WorldLoad here so
///   the vein extension runs during world load (Mining.System is lazily initialized
///   and is always available).
///
///   Blacksmithy sub-resources require DefBlacksmithy.CraftSystem, which is created
///   in DefBlacksmithy.Initialize(). Initialize() calls fire AFTER World.Load(), so
///   we use EventSink.ServerStarted (which fires after all Initialize() calls) to
///   safely extend the craft system.
///
/// Vein weight design:
///   Vanilla ore vein weights total 1000 (Iron 496 through Valorite 14).
///   Extended ores add 36 total weight, making the new pool 1036.
///   Extended ore combined probability ≈ 3.5%; Celestial ≈ 0.1%.
///   All require GM Mining (100.0 skill) to attempt.
///
/// Felucca yield (+100%):
///   Already implemented by vanilla Mining.cs: ConsumedPerFeluccaHarvest = 2.
///   No change required.
/// </summary>
public static class ClusterFMiningExtension
{
    public static void Configure()
    {
        EventSink.WorldLoad    += OnWorldLoad;
        EventSink.ServerStarted += OnServerStarted;
    }

    // ── WorldLoad — extend Mining veins ──────────────────────────────────────

    private static void OnWorldLoad()
    {
        ExtendMiningVeins();
    }

    // ── ServerStarted — extend Blacksmithy (fires after DefBlacksmithy.Initialize) ──

    private static void OnServerStarted()
    {
        ExtendBlacksmithySubResources();
    }

    // ── Mining vein extension ─────────────────────────────────────────────────

    /// <summary>
    /// Appends extended ore HarvestResources and HarvestVeins to Mining.System.OreAndStone.
    ///
    /// Skill ranges follow the vanilla pattern: reqSkill/minSkill/maxSkill.
    /// All extended ores require 100.0 minimum skill (GM Mining).
    /// Message parameter reuses cliloc 1007072 (generic "You mine some ore.").
    /// </summary>
    private static void ExtendMiningVeins()
    {
        var mining = Mining.System;
        if (mining == null)
        {
            Console.WriteLine("[ClusterFMiningExtension] WARNING: Mining.System is null — extended veins not registered.");
            return;
        }

        var def = mining.OreAndStone;
        if (def == null)
        {
            Console.WriteLine("[ClusterFMiningExtension] WARNING: OreAndStone definition is null — extended veins not registered.");
            return;
        }

        // Extended ore HarvestResources
        // reqSkill/minSkill/maxSkill: all require GM Mining (100.0 req) with increasing curves.
        var extResources = new HarvestResource[]
        {
            new(100.0, 65.0, 140.0, 1007072, typeof(PlatinumOre)),   // Platinum
            new(100.0, 68.0, 142.0, 1007072, typeof(ToxicOre)),      // Toxic
            new(100.0, 70.0, 144.0, 1007072, typeof(BlazeOre)),      // Blaze
            new(100.0, 72.0, 146.0, 1007072, typeof(FrostOre)),      // Frost
            new(100.0, 74.0, 148.0, 1007072, typeof(ObsidianOre)),   // Obsidian
            new(100.0, 76.0, 150.0, 1007072, typeof(MythrilOre)),    // Mythril
            new(100.0, 78.0, 152.0, 1007072, typeof(AdamantiumOre)), // Adamantium
            new(100.0, 80.0, 155.0, 1007072, typeof(CelestialOre))  // Celestial
        };

        // Combine existing resources with extended ones
        var existingRes = def.Resources;
        var combined = new HarvestResource[existingRes.Length + extResources.Length];
        Array.Copy(existingRes, combined, existingRes.Length);
        Array.Copy(extResources, 0, combined, existingRes.Length, extResources.Length);
        def.Resources = combined;

        // Iron reference (index 0) used as fallback vein for all extended ores
        var iron = existingRes[0];

        // Extended ore HarvestVeins
        // Weights: 8 for Platinum down to 1 for Celestial, total 36 added to base 1000 = 1036.
        var extVeins = new HarvestVein[]
        {
            new(8, 0.5, combined[existingRes.Length + 0], iron), // Platinum
            new(7, 0.5, combined[existingRes.Length + 1], iron), // Toxic
            new(6, 0.5, combined[existingRes.Length + 2], iron), // Blaze
            new(5, 0.5, combined[existingRes.Length + 3], iron), // Frost
            new(4, 0.5, combined[existingRes.Length + 4], iron), // Obsidian
            new(3, 0.5, combined[existingRes.Length + 5], iron), // Mythril
            new(2, 0.5, combined[existingRes.Length + 6], iron), // Adamantium
            new(1, 0.5, combined[existingRes.Length + 7], iron)  // Celestial
        };

        var existingVeins = def.Veins;
        var combinedVeins = new HarvestVein[existingVeins.Length + extVeins.Length];
        Array.Copy(existingVeins, combinedVeins, existingVeins.Length);
        Array.Copy(extVeins, 0, combinedVeins, existingVeins.Length, extVeins.Length);
        def.Veins = combinedVeins;

        // Permanent veins: disable random re-roll on respawn.
        // Each 8x8 bank cell's ore type is determined by a stable coordinate-based seed
        // (x * 17 + y * 11 + mapID * 3) and never changes — not after respawn, not after
        // a server restart. This ensures Tier 2 logbook travel always leads back to the
        // correct ore type at the recorded location.
        def.RandomizeVeins = false;

        Console.WriteLine($"[ClusterFMiningExtension] Extended mining veins registered: {extVeins.Length} new ore types (Platinum through Celestial). Veins are permanent (RandomizeVeins=false).");
    }

    // ── Blacksmithy sub-resource extension ────────────────────────────────────

    /// <summary>
    /// Appends extended ingot types to DefBlacksmithy.CraftSystem sub-resources.
    ///
    /// Must run after DefBlacksmithy.Initialize() — called from ServerStarted handler.
    ///
    /// Uses string TextDefinition names (no cliloc required) — these display correctly
    /// in the craft menu dropdown since ModernUO's AddSubRes accepts TextDefinition.
    ///
    /// Skill requirements per extended ingot.
    ///
    /// Spread evenly across the shard's normal skill cap (300.0), with Celestial
    /// pinned to the hard cap.  Power scrolls raise the cap to 500.0 — that range
    /// is reserved for future content.
    ///
    /// Platinum    100.0
    /// Toxic       125.0
    /// Blaze       150.0
    /// Frost       175.0
    /// Obsidian    200.0
    /// Mythril     225.0
    /// Adamantium  250.0
    /// Celestial   300.0 — requires max normal skill
    ///
    /// Messages reuse vanilla clilocs:
    ///   1044036 = "You cannot use that material without the proper skill."
    ///   1044268 = "You don't have enough materials to make anything."
    /// </summary>
    private static void ExtendBlacksmithySubResources()
    {
        var smithy = DefBlacksmithy.CraftSystem;
        if (smithy == null)
        {
            Console.WriteLine("[ClusterFMiningExtension] WARNING: DefBlacksmithy.CraftSystem is null — extended sub-resources not registered.");
            return;
        }

        smithy.AddSubRes(typeof(PlatinumIngot),   "Platinum",   100.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(ToxicIngot),      "Toxic",      125.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(BlazeIngot),      "Blaze",      150.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(FrostIngot),      "Frost",      175.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(ObsidianIngot),   "Obsidian",   200.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(MythrilIngot),    "Mythril",    225.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(AdamantiumIngot), "Adamantium", 250.0, 1044036, 1044268);
        smithy.AddSubRes(typeof(CelestialIngot),  "Celestial",  300.0, 1044036, 1044268);

        Console.WriteLine("[ClusterFMiningExtension] Blacksmithy sub-resources registered: Platinum through Celestial.");
    }
}
