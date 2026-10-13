// cc-P70, F-33: the Compact Mule's modules, the data. Notes: shard-migration notes/cc-P70-mule-modules.md.
//
// One row per module: its guild, name, art, the number it scales by tier, and its effect text per tier. One row per
// trait boost. Every number Chase may tune is here; the effects read them (MuleModules.cs).

using System;
using System.Linq;

namespace Server.Mobiles
{
    /// <summary>The 17 modules. Saved by value (on the module item): append only, never renumber.</summary>
    public enum MuleModuleId
    {
        None = 0,
        OreHopper = 1,
        TimberRack = 2,
        ReinforcedSaddlebags = 3,
        ClockworkLoadHarness = 4,
        TrailwiseTack = 5,
        FieldMedicPack = 6,
        ExpeditionBarding = 7,
        ArcaneWardHarness = 8,
        SoulTether = 9,
        FieldSmelterRig = 10,
        SurveySaddleRoll = 11,
        ShadowCloak = 12,
        SalvageBin = 13,
        MinstrelsBells = 14,
        TradersLedger = 15,
        SteadyHoofWraps = 16,
        ImbuedBardingSocket = 17
    }

    /// <summary>Built: the effect works. Partial: some tiers work, the rest are stubbed (said in the tooltip). Stubbed:
    /// the item and its crafting exist, the effect waits on a pinned change (Part E).</summary>
    public enum MuleModuleState { Built, Partial, Stubbed }

    public sealed record MuleModuleInfo(
        MuleModuleId Id,
        string GuildKey,
        string GuildName,
        string Name,
        int ItemId,
        int[] Value,            // the module's main number at tier I, II, III (what it means: see Effects)
        string[] Effects,       // what each tier does, for the tooltip and the notes
        MuleModuleState State,
        string StubNote = null  // what is stubbed and why, shown on the tooltip
    );

    public sealed record MuleTraitBoost(MuleTrait Trait, MuleModuleId Module, int[] PerGrade, string Effect);

    public static class MuleModuleData
    {
        /// <summary>Module tiers that exist (Chase: 3 now, maybe 5). Nothing else may assume 3.</summary>
        public static int MaxTier => ServerConfiguration.GetOrUpdateSetting("mule.modules.maxTier", 3);

        public static readonly MuleModuleInfo[] Modules =
        {
            // --- 1. Hauling -----------------------------------------------------------------------------------------
            new(MuleModuleId.OreHopper, "mining", "Miners' Compact", "Ore Hopper", 0x19B9, new[] { 25, 50, 50 },
                new[] { "ore, ingots and gems weigh 25% less in the pack", "ore, ingots and gems weigh 50% less",
                        "50% less, and a 5% chance of a gem when the owner mines within 12 tiles" }, MuleModuleState.Partial,
                "III: the gem chance when the owner mines near the mule is not wired yet"),
            new(MuleModuleId.TimberRack, "foresters", "Foresters' Union", "Timber Rack", 0x1BD7, new[] { 25, 50, 50 },
                new[] { "logs and boards weigh 25% less in the pack", "logs and boards weigh 50% less",
                        "50% less, and carried logs turn into boards (10 every 5 minutes)" }, MuleModuleState.Built),
            new(MuleModuleId.ReinforcedSaddlebags, "tailoring", "Tailors' Circle", "Reinforced Saddlebags", 0x0E76, new[] { 50, 100, 150 },
                new[] { "+50 items in the pack", "+100 items", "+150 items" }, MuleModuleState.Built),
            new(MuleModuleId.ClockworkLoadHarness, "tinkers", "Tinkers' Union", "Clockwork Load Harness", 0x1053, new[] { 10, 20, 30 },
                new[] { "+10% pack weight", "+20% pack weight, and sorts the pack into bags",
                        "+30%, and the Clockwork Scavenger carries its loot to the mule" }, MuleModuleState.Partial,
                "III: the Clockwork Scavenger (the League's auto-loot reward) is not built; the redirect hook is"),

            // --- 2. Survival --------------------------------------------------------------------------------------------
            new(MuleModuleId.TrailwiseTack, "rangers", "Rangers' League", "Trailwise Tack", 0x1079, new[] { 20, 20, 20 },
                new[] { "+20% stamina; warns the owner of hostiles within 12 tiles",
                        "Homing: when left behind on the same map it finds the owner; \"all come\" from any distance",
                        "the mule costs no follower slot" }, MuleModuleState.Built),
            new(MuleModuleId.FieldMedicPack, "healers", "Healers' Covenant", "Field Medic Pack", 0x0E21, new[] { 2, 2, 2 },
                new[] { "heals 2% of its hits every 10 seconds out of combat",
                        "also cures its own poison; the owner can bandage it from 6 tiles",
                        "below 25% hits, an emergency heal (5-minute cooldown); and a slow healing aura on the owner and companions within 3 tiles" },
                MuleModuleState.Partial, "II: bandaging the mule from 6 tiles waits on a pinned change to bandage range (planned)"),
            new(MuleModuleId.ExpeditionBarding, "warriors", "Warriors' Brotherhood", "Expedition Barding", 0x1415, new[] { 10, 20, 30 },
                new[] { "10% less damage taken", "20% less, and +10% hits", "30% less, and +20% hits" }, MuleModuleState.Built),
            new(MuleModuleId.ArcaneWardHarness, "arcane", "Arcane Society", "Arcane Ward Harness", 0x0EFA, new[] { 15, 15, 15 },
                new[] { "+15 Magic Resist", "a ward that absorbs one spell, then recharges in 60 seconds",
                        "below 15% hits, the mule and its pack are sent to the owner's stable" }, MuleModuleState.Built),
            new(MuleModuleId.SoulTether, "keepers", "Keepers of the Last Door", "Soul Tether", 0x1F14, new[] { 0, 0, 0 },
                new[] { "rises again faster and cheaper", "the pack is kept on death", "no stat or skill loss on death" },
                MuleModuleState.Built),

            // --- 3. Utility ---------------------------------------------------------------------------------------------
            new(MuleModuleId.FieldSmelterRig, "smithing", "Society of Smiths", "Field Smelter Rig", 0x1BF2, new[] { 10, 10, 10 },
                new[] { "smelts the ore in its pack, 10 a minute", "the mule counts as a forge", "the mule also counts as an anvil" },
                MuleModuleState.Built),
            new(MuleModuleId.SurveySaddleRoll, "maritime", "Maritime Brotherhood", "Survey Saddle Roll", 0x14EB, new[] { 1, 1, 1 },
                new[] { "treasure maps and SOS bottles weigh nothing; the owner reveals one chunk farther while the mule is near",
                        "a quest arrow to a decoded map or SOS in the pack (\"Track map\" picks which)",
                        "the mule digs the tracked map's chest at the spot" }, MuleModuleState.Built),
            new(MuleModuleId.ShadowCloak, "thieves", "Thieves' Den", "Shadow Cloak", 0x1F03, new[] { 0, 0, 0 },
                new[] { "hides when the owner hides", "stays hidden while the owner stealths beside it (walking only)",
                        "hides on its own when attacked" }, MuleModuleState.Built),
            new(MuleModuleId.SalvageBin, "custodians", "Custodians", "Salvage Bin", 0x0E77, new[] { 3, 3, 3 },
                new[] { "a Clean Up Britannia trash bag on the mule", "picks up loose items near the owner by a filter",
                        "salvages items as a salvage bag does, and gives Clean Up points" }, MuleModuleState.Built),
            new(MuleModuleId.MinstrelsBells, "bards", "Bards' Consortium", "Minstrel's Bells", 0x0E9C, new[] { 0, 10, 10 },
                new[] { "monsters are slower to target the mule", "a 10% chance to calm whatever attacks it",
                        "the owner's bard abilities are stronger near the mule" }, MuleModuleState.Built),

            // --- 4. Merchants, Dojo, Artificers --------------------------------------------------------------------------
            new(MuleModuleId.TradersLedger, "merchants", "Merchants' Exchange", "Trader's Ledger", 0x0FEF, new[] { 0, 10, 0 },
                new[] { "sell from the mule's pack to a vendor", "a higher sell cap and 10% better prices",
                        "a portable bank: open the bank box from the mule (not in dungeons, combat, or while criminal)" },
                MuleModuleState.Built),
            new(MuleModuleId.SteadyHoofWraps, "dojo", "Twin Paths Dojo", "Steady Hoof Wraps", 0x13C6, new[] { 25, 50, 100 },
                new[] { "25% chance to resist being dismounted", "50% chance", "cannot be dismounted; shorter remount delay" },
                MuleModuleState.Stubbed, "waits on a pinned change to the dismount paths (planned)"),
            new(MuleModuleId.ImbuedBardingSocket, "artificers", "Artificers' Order", "Imbued Barding Socket", 0x0F16, new[] { 1, 2, 3 },
                new[] { "Imbuing can add 1 property to the mule", "2 properties, and unravel anywhere near the mule",
                        "3 properties, and imbue anywhere near the mule" }, MuleModuleState.Partial,
                "the property sockets work; unravel and imbue near the mule follow the imbue research (notes)")
        };

        public static MuleModuleInfo Info(MuleModuleId id) => Modules.FirstOrDefault(m => m.Id == id);

        public static int ValueAt(MuleModuleId id, int tier) =>
            Info(id) is { } info && tier >= 1 ? info.Value[Math.Min(tier, info.Value.Length) - 1] : 0;

        /// <summary>A module's hue by tier: the same metal as a mule of that tier (the ore/metal ladder).</summary>
        public static int HueFor(int tier) => MuleData.Tier(Math.Clamp(tier, 1, MuleData.MaxTier)).Hue;

        // Traits boost matching modules (Chase: yes). Per grade I, II, III.
        public static readonly MuleTraitBoost[] TraitBoosts =
        {
            new(MuleTrait.Emberblood, MuleModuleId.FieldSmelterRig, new[] { 20, 40, 60 }, "smelts {0}% faster"),
            new(MuleTrait.Burdened, MuleModuleId.OreHopper, new[] { 5, 10, 15 }, "{0} more points of weight saved"),
            new(MuleTrait.Burdened, MuleModuleId.TimberRack, new[] { 5, 10, 15 }, "{0} more points of weight saved"),
            new(MuleTrait.Burdened, MuleModuleId.ClockworkLoadHarness, new[] { 5, 10, 15 }, "{0} more points of pack weight"),
            new(MuleTrait.Surefooted, MuleModuleId.TrailwiseTack, new[] { 10, 20, 30 }, "{0}% more stamina, and a warning range {0}% wider"),
            new(MuleTrait.Stoneback, MuleModuleId.ExpeditionBarding, new[] { 3, 6, 9 }, "{0} more points of damage reduction"),
            new(MuleTrait.NightmareKin, MuleModuleId.ArcaneWardHarness, new[] { 5, 10, 15 }, "{0} more Magic Resist, and the ward recharges {0}% faster"),
            new(MuleTrait.Deepdelver, MuleModuleId.FieldMedicPack, new[] { 25, 50, 75 }, "heals {0}% faster in dungeons"),
            new(MuleTrait.CelestialTempered, MuleModuleId.None, new[] { 2, 4, 6 }, "every installed module's number +{0}%")
        };
    }
}
