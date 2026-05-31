using System;

namespace Server;

public static class ClusterFPetMimicSettings
{
    public static void Configure()
    {
        _ = BaseMaxHP;
        _ = RegenMinutesPerHP;
        _ = CitrineHeal; _ = AmberHeal; _ = TourmalineHeal;
        _ = AmethystHeal; _ = SapphireHeal; _ = StarSapphireHeal;
        _ = RubyHeal; _ = EmeraldHeal; _ = DiamondHeal;
        _ = LesserHealPotionAmount; _ = HealPotionAmount; _ = GreaterHealPotionAmount;
        _ = BandageHealAmount;
        _ = MaxStrBonus; _ = MaxDexBonus; _ = MaxIntBonus;
        _ = MaxBonusHits; _ = MaxBonusStam; _ = MaxBonusMana;
        _ = MaxLRC; _ = MaxFasterCasting; _ = MaxFasterCastRecovery;
        _ = MaxDamageIncrease; _ = MaxResistance; _ = MaxSkillBonus;
        _ = MaxToolUses; _ = ToolRegenMinutesPerUse;
        _ = DecayHPPerHit; _ = MealRegenScale; _ = GemRegenScale; _ = MinRegenMinutesPerHP;
        _ = MaxHCI; _ = MaxDCI; _ = MaxSSI; _ = MaxSDI; _ = MaxLMC;
        _ = MaxRP; _ = MaxEP; _ = MaxLuck;
        _ = MaxRegenHits; _ = MaxRegenStam; _ = MaxRegenMana;
        _ = MaxWeaponHit;
        _ = MaxSelfRepair; _ = MaxMageArmor; _ = SelfRepairRegenScale;
        _ = WeaponBaseDamageMin; _ = WeaponBaseDamageMax;
        _ = WeaponDamagePerFormMin; _ = WeaponDamagePerFormMax; _ = WeaponMaxDamage;
    }

    // --- HP ---
    public static int BaseMaxHP =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.baseMaxHP", 100);

    public static int RegenMinutesPerHP =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.regenMinutesPerHP", 1);

    // --- Gem heals ---
    public static int CitrineHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.citrine", 5);
    public static int AmberHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.amber", 5);
    public static int TourmalineHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.tourmaline", 7);
    public static int AmethystHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.amethyst", 10);
    public static int SapphireHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.sapphire", 15);
    public static int StarSapphireHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.starSapphire", 20);
    public static int RubyHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.ruby", 20);
    public static int EmeraldHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.emerald", 25);
    public static int DiamondHeal =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemHeal.diamond", 50);

    // --- Potion / bandage heals ---
    public static int LesserHealPotionAmount =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.potionHeal.lesser", 5);
    public static int HealPotionAmount =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.potionHeal.normal", 10);
    public static int GreaterHealPotionAmount =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.potionHeal.greater", 20);
    public static int BandageHealAmount =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.bandageHeal", 5);

    // --- Stat accumulation caps (Phase 2+) ---
    public static int MaxStrBonus =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.str", 50);
    public static int MaxDexBonus =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.dex", 50);
    public static int MaxIntBonus =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.int", 50);
    public static int MaxBonusHits =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.bonusHits", 150);
    public static int MaxBonusStam =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.bonusStam", 150);
    public static int MaxBonusMana =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.bonusMana", 150);
    public static int MaxLRC =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.lrc", 100);
    public static int MaxFasterCasting =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.fasterCasting", 4);
    public static int MaxFasterCastRecovery =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.fasterCastRecovery", 6);
    public static int MaxDamageIncrease =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.damageIncrease", 100);
    public static int MaxResistance =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.resistance", 70);
    public static int MaxSkillBonus =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.skillBonus", 20);
    public static int MaxHCI =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.hci", 45);
    public static int MaxDCI =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.dci", 45);
    public static int MaxSSI =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.ssi", 60);
    public static int MaxSDI =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.sdi", 100);
    public static int MaxLMC =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.lmc", 40);
    public static int MaxRP =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.rp", 100);
    public static int MaxEP =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.ep", 50);
    public static int MaxLuck =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.luck", 1200);
    public static int MaxRegenHits =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.regenHits", 18);
    public static int MaxRegenStam =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.regenStam", 24);
    public static int MaxRegenMana =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.regenMana", 18);

    // --- Weapon hit-proc caps (stored on phantom weapon WeaponAttributes) ---
    public static int MaxWeaponHit =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.weaponHit", 50);

    // --- Armor attribute caps ---
    public static int MaxSelfRepair =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.selfRepair", 30);
    public static int MaxMageArmor =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.maxStat.mageArmor", 1);

    // Each point of AccSelfRepair speeds regen (same semantic as armor self-repair = durability recovery).
    public static double SelfRepairRegenScale =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.selfRepairRegenScale", 0.3);

    // --- Weapon damage scaling ---
    // Starting damage when the mimic has eaten 0 weapon forms.
    public static int WeaponBaseDamageMin =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.weapon.baseDamageMin", 5);
    public static int WeaponBaseDamageMax =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.weapon.baseDamageMax", 8);
    // Added to min/max per distinct weapon form eaten.
    public static int WeaponDamagePerFormMin =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.weapon.damagePerFormMin", 1);
    public static int WeaponDamagePerFormMax =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.weapon.damagePerFormMax", 2);
    // Hard cap on min damage (max cap = this + 15).
    // Default: 40 min / 55 max — stronger than most vanilla weapons at full progression.
    public static int WeaponMaxDamage =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.weapon.maxDamage", 40);

    // --- Tool form (Phase 2+) ---
    public static int MaxToolUses =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.toolMaxUses", 50);
    public static int ToolRegenMinutesPerUse =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.toolRegenMinutes", 2);

    // --- Phase 4: Decay and scaled regen ---

    // HP lost per combat hit while equipped. Matches armor durability feel.
    public static int DecayHPPerHit =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.decayHPPerHit", 1);

    // Each eaten item (meal) reduces minutes-per-HP regen by this fraction.
    // e.g. 0.15 means 10 meals cuts regen time by ~60% from base.
    public static double MealRegenScale =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.mealRegenScale", 0.15);

    // Each point of BonusMaxHP (from gems) reduces minutes-per-HP by this fraction.
    // e.g. 0.02 means 50 gem HP bonus doubles the meal contribution.
    public static double GemRegenScale =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.gemRegenScale", 0.02);

    // Fastest possible regen rate (floor). Mimic cannot regen faster than 1 HP per this many minutes.
    // When effective regen <= DecayHPPerHit / (RegenMinutesPerHP / MinRegenMinutesPerHP), mimic is self-sustaining.
    public static double MinRegenMinutesPerHP =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.petMimic.minRegenMinutesPerHP", 1.0);
}
