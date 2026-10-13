// cc-P69, F-33: the Compact Mule's data. Notes: shard-migration notes/cc-P69-mule-core.md.
//
// One table for the tiers, one for the bloodline traits, one for the breeding odds, one for the upgrade steps
// (MuleUpgrades.cs). Every number Chase or the module prompt will tune is here, not in the mule's code. The tier table
// (HP, pack, resists, module slots, metal) is Chase's, 2026-10-05; the rest are proposals, printed in the notes.

using System;
using Server.Items;

namespace Server.Mobiles
{
    public sealed record MuleTier(
        int Tier,
        string Name,            // "Compact" -> "a Compact mule"
        int HitsMin, int HitsMax,
        int PackWeight, int PackItems,
        int ResistMin, int ResistMax,
        int ModuleSlots,
        CraftResource Metal,    // its hue: the metal's own (CraftResources.GetHue), the ore/metal ladder
        int StrMin, int StrMax,
        int DexMin, int DexMax,
        int StamMin, int StamMax,
        double Skill,           // MagicResist, Tactics, Wrestling
        int DamageMin, int DamageMax,
        int VirtualArmor
    )
    {
        public int Hue => CraftResources.GetHue(Metal);
    }

    /// <summary>The bloodline traits. The ids are saved and read by the module code (traits boost matching modules).</summary>
    public enum MuleTrait
    {
        None = 0,
        Burdened = 1,           // Burdened Bloodline: pack capacity
        Stoneback = 2,          // physical resist
        Emberblood = 3,         // fire resist (and, later, the Field Smelter Rig)
        Surefooted = 4,         // stamina
        Deepdelver = 5,         // takes less damage in dungeons
        NightmareKin = 6,       // magic resist
        CelestialTempered = 7   // rare: a little of everything
    }

    public sealed record MuleTraitInfo(MuleTrait Id, string Name, string Effect, int[] PerGrade);

    public static class MuleData
    {
        public const int MinTier = 1;
        public const int MaxTier = 5;
        public const int MaxTraits = 3;
        public const int MaxGrade = 3;

        // Chase's table (2026-10-05) for HP, pack, resists, slots and metal. Str, Dex, Stam, skills, damage and armor are
        // mine, scaled so tier 3 is about today's mule (PackMule before cc-P69: Str 400-450, Dex 86-105, Stam 120-150,
        // skills 80-95, damage 10-14, armor 55).
        public static readonly MuleTier[] Tiers =
        {
            new(1, "Compact",         150, 190, 1_600, 150, 25, 45, 1, CraftResource.DullCopper, 250, 300,  70,  85,  80, 100,  70.0,  6,  9, 35),
            new(2, "Bronzeback",      200, 250, 2_400, 225, 35, 55, 2, CraftResource.Bronze,     325, 375,  78,  95, 100, 125,  80.0,  8, 12, 45),
            new(3, "Shadowhide",      250, 310, 3_200, 300, 45, 65, 3, CraftResource.ShadowIron, 400, 450,  86, 105, 120, 150,  87.5, 10, 14, 55),
            new(4, "Valorite-Barded", 320, 400, 4_000, 400, 55, 70, 4, CraftResource.Valorite,   475, 525,  95, 115, 145, 175,  95.0, 12, 16, 62),
            new(5, "Celestial",       420, 500, 5_000, 500, 60, 75, 5, CraftResource.Celestial,  550, 600, 105, 125, 170, 200, 100.0, 14, 18, 70)
        };

        public static MuleTier Tier(int tier) => Tiers[Math.Clamp(tier, MinTier, MaxTier) - 1];

        // Where each resist sits in the tier's range: today's shape, physical highest, fire and energy lowest. A resist is
        // min + span * (Shape/2 + quality/2): quality 100 puts physical at the tier's top, fire and energy at its middle.
        public const double PhysicalShape = 1.0;
        public const double ColdShape = 0.5;
        public const double PoisonShape = 0.5;
        public const double FireShape = 0.0;
        public const double EnergyShape = 0.0;

        // The traits, and what each grade (I, II, III) gives. Proposals for Chase.
        public static readonly MuleTraitInfo[] Traits =
        {
            new(MuleTrait.Burdened, "Burdened Bloodline", "pack weight and items +{0}%", new[] { 5, 10, 15 }),
            new(MuleTrait.Stoneback, "Stoneback", "physical resist +{0}", new[] { 3, 6, 9 }),
            new(MuleTrait.Emberblood, "Emberblood", "fire resist +{0}", new[] { 3, 6, 9 }),
            new(MuleTrait.Surefooted, "Surefooted", "stamina +{0}%", new[] { 10, 20, 30 }),
            new(MuleTrait.Deepdelver, "Deepdelver", "takes {0}% less damage in dungeons", new[] { 5, 10, 15 }),
            new(MuleTrait.NightmareKin, "Nightmare-Kin", "Magic Resist skill +{0}", new[] { 5, 10, 15 }),
            new(MuleTrait.CelestialTempered, "Celestial Tempered", "every resist +{0} and hit points +{0}%", new[] { 2, 4, 6 })
        };

        public static MuleTraitInfo Trait(MuleTrait id) => Array.Find(Traits, t => t.Id == id);

        public static int TraitValue(MuleTrait id, int grade) =>
            grade is >= 1 and <= MaxGrade && Trait(id) is { } t ? t.PerGrade[grade - 1] : 0;

        public static string GradeName(int grade) => grade switch { 1 => "I", 2 => "II", 3 => "III", _ => "?" };

        // Resists never go above this, traits and modules included (players' own cap is 70; the mule may go a little past).
        public const int ResistCap = 80;

        // Family names for a new line: the first bred foal whose parents have none draws one.
        public static readonly string[] FamilyNames =
        {
            "Ironhoof", "Stonecart", "Copperback", "Deepvein", "Oreheart", "Cragstep", "Lodestar", "Tallowmane",
            "Gritwhistle", "Anvilbrook", "Quarrytail", "Emberhock", "Slatefoot", "Bramblecart", "Silverlode", "Dunmarrow",
            "Hearthpack", "Rockwillow", "Ashgallop", "Moorhaul"
        };

        // A line is Legendary once three generations in a row have reached tier 5.
        public const int LegendaryGenerations = 3;
    }

    /// <summary>
    /// Breeding odds (overseer draft, agreed by Chase 2026-10-05). The breeder's Animal Lore from 120 to 200 adds the
    /// Lore bonus to each chance, linearly: nothing at 120 or below, all of it at 200.
    /// </summary>
    public static class MuleBreeding
    {
        public const double LoreFloor = 120.0;
        public const double LoreCeiling = 200.0;

        // Stat quality: the parents' average plus a roll in [-QualityRoll, +QualityRoll], or the better parent's.
        public const int QualityRoll = 5;
        public const double BetterParentChance = 0.50;
        public const double BetterParentLoreBonus = 0.30;

        public const double TraitFromOneParent = 0.35;
        public const double TraitFromBothParents = 0.70;
        public const double TraitLoreBonus = 0.15;
        public const double GradeUpChance = 0.20;     // a trait both parents carry
        public const double GradeUpLoreBonus = 0.10;
        public const double NewTraitChance = 0.05;    // a new grade I trait
        public const double NewTraitLoreBonus = 0.05;
        public const double CelestialTemperedChance = 0.02; // new, only from two tier 5 parents
        public const int GradeIIIMinParentTier = 4;   // grade III only when both parents are tier 4 or higher
        public const int CelestialTemperedParentTier = 5;

        public static readonly TimeSpan Cooldown = TimeSpan.FromDays(7);
        public static readonly TimeSpan Gestation = TimeSpan.FromHours(24);

        /// <summary>0 at Lore 120 or below, 1 at 200, linear between.</summary>
        public static double LoreFactor(double lore) =>
            Math.Clamp((lore - LoreFloor) / (LoreCeiling - LoreFloor), 0.0, 1.0);
    }
}
