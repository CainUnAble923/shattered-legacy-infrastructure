using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server;

// -- Imbue property categories -------------------------------------------------

[Flags]
internal enum ImbuableItem
{
    None     = 0,
    Weapon   = 1 << 0,
    Armor    = 1 << 1,   // includes shields
    Jewel    = 1 << 2,
    Clothing = 1 << 3,
    All      = Weapon | Armor | Jewel | Clothing,
    NonWeapon = Armor | Jewel | Clothing,
}

// -- Property definition -------------------------------------------------------

internal sealed class ImbuePropertyDef
{
    public string       Name;         // Display name (also used as discovery key)
    public string       Group;        // "General", "Stats", "Weapon", "Defense", "Slayer", "Skills", "Custom"
    public int          MaxVanilla;   // OSI max: the Full tier (cc-P82)
    public int          MaxGuild;     // The built-in Overcharged maximum (Configuration/imbuing.json overchargedMax wins)
    public int          ShardBase;    // Shards at the Full value
    public int          GoldBase;     // Gold at the Full value
    public bool         IsBool;       // 1/0 on/off property
    public ImbuableItem Applies;

    /// <summary>
    /// How many times a player must successfully imbue this property (using an essence)
    /// before they "master" it and no longer need an essence.
    /// Computed from GoldBase by ImbueCatalogue.ComputeThreshold. 0 for a custom property (it takes its ingredient).
    /// </summary>
    public int DiscoveryThreshold;

    /// <summary>
    /// cc-P82: what the account must know to imbue this row (ClusterFImbueKnowledge): the row's own name, or a family
    /// several rows share (ImbueFamilies: slayers, elemental damages, +skill).
    /// </summary>
    public string KnowledgeKey;

    /// <summary>cc-P82: one of the design's custom properties (ImbueCustomStore holds the value; one per item).</summary>
    public bool IsCustom;

    /// <summary>cc-P82: a further test on the item beyond Applies (a ranged weapon, crafter gear), or null.</summary>
    public Func<Item, bool> Allows;

    /// <summary>cc-P82: why this row cannot go on this item now (the +skill group's slot, a second custom), or null.</summary>
    public Func<Item, string> BlockedBy;

    /// <summary>
    /// cc-P46 Part C: every value of this property costs at least one shard and one gold more than the most expensive
    /// value of any other property (ImbueCatalogue.MostShardsOfOthers / MostGoldOfOthers); above that floor the usual
    /// scaling applies. Self Repair.
    /// </summary>
    public bool PricedAboveAll;

    // Accessors
    public Func<Item, int>    Get;
    public Action<Item, int>  Set;

    /// <summary>Returns how many property slots this property currently consumes.</summary>
    public int CountSlots(Item item) => Get(item) != 0 ? 1 : 0;

    /// <summary>cc-P82: the Full value (the OSI max; a custom property's from the config).</summary>
    public int FullMax => IsCustom ? ImbuingTable.Current.CustomOf(Name)?.Full ?? MaxVanilla : MaxVanilla;

    /// <summary>cc-P82: the item kinds this row goes on (a custom property's from the config).</summary>
    public ImbuableItem AppliesNow =>
        IsCustom && ImbuingTable.Current.CustomOf(Name) is { } c ? ImbueFamilies.ParseApplies(c.Applies) : Applies;

    private int BaseShards => IsCustom ? ImbuingTable.Current.CustomOf(Name)?.Shards ?? ShardBase : ShardBase;
    private int BaseGold => IsCustom ? ImbuingTable.Current.CustomOf(Name)?.Gold ?? GoldBase : GoldBase;

    /// <summary>The highest value this row can ever take (its top tier's).</summary>
    public int TopValue => ImbueTiers.MaxFor(this, ImbueTiers.TopTier(this));

    /// <summary>Returns Essence Shard cost to set this property to targetValue.</summary>
    public int ShardsFor(int targetValue)
    {
        var scaled = ScaledShardsFor(targetValue);
        return PricedAboveAll ? Math.Max(scaled, ImbueCatalogue.MostShardsOfOthers() + 1) : scaled;
    }

    /// <summary>The shard cost from the scaling alone, before any floor (cc-P46 Part C): by value over Full (cc-P82).</summary>
    public int ScaledShardsFor(int targetValue)
    {
        if (IsBool) return BaseShards;
        return Math.Max(1, (int)(BaseShards * (double)targetValue / Math.Max(1, FullMax)));
    }

    /// <summary>Returns gold cost to set this property to targetValue.</summary>
    public int GoldFor(int targetValue)
    {
        var scaled = ScaledGoldFor(targetValue);
        return PricedAboveAll ? Math.Max(scaled, ImbueCatalogue.MostGoldOfOthers() + 1) : scaled;
    }

    /// <summary>The gold cost from the scaling alone, before any floor (cc-P46 Part C).</summary>
    public int ScaledGoldFor(int targetValue)
    {
        if (IsBool) return BaseGold;
        return Math.Max(100, (int)(BaseGold * (double)targetValue / Math.Max(1, FullMax)));
    }

    /// <summary>
    /// Success-check difficulty (cc-P82): 40 at nothing to 100 at Full, then up to 120 at the top of Overcharged (the
    /// 200 skill cap's tier).
    /// </summary>
    public double SkillDifficulty(int targetValue)
    {
        var full = Math.Max(1, FullMax);
        if (IsBool || targetValue <= full)
        {
            return 40.0 + 60.0 * Math.Min(1.0, (double)targetValue / full);
        }

        var top = Math.Max(full + 1, TopValue);
        return 100.0 + 20.0 * Math.Min(1.0, (double)(targetValue - full) / (top - full));
    }

    /// <summary>The intensity this value takes of the item budget, in percent of Full (budget mode only).</summary>
    public int IntensityPercent(int value) => IsBool ? (value != 0 ? 100 : 0) : value * 100 / Math.Max(1, FullMax);

    /// <summary>Shard cost to craft one PropertyEssence for this property (mastered players only).</summary>
    public int CraftShards => ShardBase * 2;

    /// <summary>Gold cost to craft one PropertyEssence for this property (mastered players only).</summary>
    public int CraftGold => GoldBase / 2;
}

// -- Property catalogue --------------------------------------------------------

internal static class ImbueCatalogue
{
    // cc-P95 (D89): built on first use, not in a static initializer. The skill rows read SkillInfo.Table, which pinned
    // SkillsInfo.Configure fills; a static initializer that ran first threw a TypeInitializationException and the shard
    // did not start. ClusterFImbueKnowledge.Configure, the first user at start, now runs after it (its CallPriority).
    private static List<ImbuePropertyDef>? _all;
    private static List<ImbuePropertyDef>? _building;

    public static List<ImbuePropertyDef> All => _all ??= Build();

    // The list Build is filling; the Add helpers below append to it.
    private static List<ImbuePropertyDef> Rows => _building!;

    // cc-P42 Part A. Self Repair (weapon and armor): shards and gold at the vanilla maximum, scaled like every other
    // property by target value. cc-P82: no longer gated at Master Artificer; the design makes it free (known at Basic).
    public const int SelfRepairShards      = 40;
    public const int SelfRepairGold        = 40000;

    // cc-P46 Part C (Chase, 2026-10-03): a floor, so Self Repair 1 never costs fewer shards than another property. The
    // most any property without the floor costs at its highest value (cc-P82: its top tier, Overcharged where it has one);
    // Self Repair costs at least one more.
    public static int MostShardsOfOthers()
    {
        var most = 0;
        foreach (var d in All)
        {
            if (!d.PricedAboveAll)
            {
                most = Math.Max(most, d.ScaledShardsFor(d.TopValue));
            }
        }

        return most;
    }

    public static int MostGoldOfOthers()
    {
        var most = 0;
        foreach (var d in All)
        {
            if (!d.PricedAboveAll)
            {
                most = Math.Max(most, d.ScaledGoldFor(d.TopValue));
            }
        }

        return most;
    }

    internal static List<ImbuePropertyDef> Build()
    {
        if (SkillInfo.Table.Length == 0)
        {
            throw new InvalidOperationException(
                "The imbuing catalogue was used before SkillsInfo.Configure filled SkillInfo.Table (D89)."
            );
        }

        _building = new List<ImbuePropertyDef>();

        // -- General - apply to most item types -------------------------------

        Add("Swing Speed Increase",    "General",  60,  85, shards: 8,  gold: 3500,  ImbuableItem.Weapon,
            i => ((BaseWeapon)i).Attributes.WeaponSpeed,
            (i, v) => ((BaseWeapon)i).Attributes.WeaponSpeed = v);

        Add("Damage Increase",         "General",  50,  70, shards: 8,  gold: 3500,  ImbuableItem.Weapon,
            i => ((BaseWeapon)i).Attributes.WeaponDamage,
            (i, v) => ((BaseWeapon)i).Attributes.WeaponDamage = v);

        Add("Hit Chance Increase",     "General",  45,  65, shards: 8,  gold: 3500,  ImbuableItem.Weapon,
            i => ((BaseWeapon)i).Attributes.AttackChance,
            (i, v) => ((BaseWeapon)i).Attributes.AttackChance = v);

        Add("Defense Chance Increase", "General",  45,  65, shards: 8,  gold: 3500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).DefendChance,
            (i, v) => GetAttr(i).DefendChance = v);

        Add("Lower Mana Cost",         "General",  40,  55, shards: 6,  gold: 2500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).LowerManaCost,
            (i, v) => GetAttr(i).LowerManaCost = v);

        Add("Lower Reagent Cost",      "General", 100, 100, shards: 10, gold: 4000,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).LowerRegCost,
            (i, v) => GetAttr(i).LowerRegCost = v);

        Add("Spell Damage Increase",   "General",  12,  30, shards: 6,  gold: 2500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).SpellDamage,
            (i, v) => GetAttr(i).SpellDamage = v);

        Add("Faster Cast Recovery",    "General",   3,   5, shards: 5,  gold: 2000,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).CastRecovery,
            (i, v) => GetAttr(i).CastRecovery = v);

        Add("Faster Casting",          "General",   1,   2, shards: 5,  gold: 2000,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).CastSpeed,
            (i, v) => GetAttr(i).CastSpeed = v);

        Add("Spell Channeling",        "General",   1,   1, shards: 4,  gold: 1500, isBool: true,
            ImbuableItem.Weapon | ImbuableItem.Armor,
            i => GetAttr(i).SpellChanneling,
            (i, v) => GetAttr(i).SpellChanneling = v);

        Add("Night Sight",             "General",   1,   1, shards: 2,  gold:  800, isBool: true,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).NightSight,
            (i, v) => GetAttr(i).NightSight = v);

        Add("Luck",                    "General", 100, 150, shards: 5,  gold: 2000,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).Luck,
            (i, v) => GetAttr(i).Luck = v);

        Add("Enhance Potions",         "General",  25,  40, shards: 5,  gold: 2000,
            ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).EnhancePotions,
            (i, v) => GetAttr(i).EnhancePotions = v);

        Add("Reflect Physical Damage", "General",  15,  25, shards: 6,  gold: 2500,
            ImbuableItem.Armor,
            i => GetAttr(i).ReflectPhysical,
            (i, v) => GetAttr(i).ReflectPhysical = v);

        // -- Stat bonuses ------------------------------------------------------

        Add("Bonus Strength",          "Stats",   8, 15, shards: 4,  gold: 1500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).BonusStr,
            (i, v) => GetAttr(i).BonusStr = v);

        Add("Bonus Dexterity",         "Stats",   8, 15, shards: 4,  gold: 1500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).BonusDex,
            (i, v) => GetAttr(i).BonusDex = v);

        Add("Bonus Intelligence",      "Stats",   8, 15, shards: 4,  gold: 1500,
            ImbuableItem.Weapon | ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).BonusInt,
            (i, v) => GetAttr(i).BonusInt = v);

        Add("Hit Points Regen",        "Stats",   5,  8, shards: 5,  gold: 2000,
            ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).RegenHits,
            (i, v) => GetAttr(i).RegenHits = v);

        Add("Stamina Regen",           "Stats",   5,  8, shards: 5,  gold: 2000,
            ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).RegenStam,
            (i, v) => GetAttr(i).RegenStam = v);

        Add("Mana Regen",              "Stats",   5,  8, shards: 5,  gold: 2000,
            ImbuableItem.Armor | ImbuableItem.Jewel | ImbuableItem.Clothing,
            i => GetAttr(i).RegenMana,
            (i, v) => GetAttr(i).RegenMana = v);

        // -- Weapon-specific: Hit abilities ------------------------------------

        Add("Hit Life Leech",    "Weapon", 50, 70, shards: 7, gold: 3000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLeechHits,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLeechHits = v);

        Add("Hit Stamina Leech", "Weapon", 50, 70, shards: 7, gold: 3000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLeechStam,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLeechStam = v);

        Add("Hit Mana Leech",    "Weapon", 50, 70, shards: 7, gold: 3000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLeechMana,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLeechMana = v);

        Add("Hit Lower Attack",  "Weapon", 50, 70, shards: 7, gold: 3000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLowerAttack,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLowerAttack = v);

        Add("Hit Lower Defense", "Weapon", 50, 70, shards: 7, gold: 3000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLowerDefend,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLowerDefend = v);

        Add("Hit Magic Arrow",   "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitMagicArrow,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitMagicArrow = v);

        Add("Hit Harm",          "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitHarm,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitHarm = v);

        Add("Hit Fireball",      "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitFireball,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitFireball = v);

        Add("Hit Lightning",     "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitLightning,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitLightning = v);

        Add("Hit Cold Area",     "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitColdArea,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitColdArea = v);

        Add("Hit Fire Area",     "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitFireArea,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitFireArea = v);

        Add("Hit Energy Area",   "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitEnergyArea,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitEnergyArea = v);

        Add("Hit Poison Area",   "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitPoisonArea,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitPoisonArea = v);

        Add("Hit Physical Area", "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitPhysicalArea,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitPhysicalArea = v);

        Add("Hit Dispel",        "Weapon", 50, 70, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.HitDispel,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.HitDispel = v);

        // cc-P42 Part A (Chase, 2026-10-02): Self Repair is kept but priced far above every other property. With F-27
        // (1 in 500 from high-end loot) this keeps it rare. Numbers: shard-migration notes/cc-P42-defect-batch-3.md,
        // Part A. cc-P82: the Master Artificer gate is gone (the design's ladder makes Self Repair free).
        Add("Self Repair",       "Weapon", 5, 7, shards: SelfRepairShards, gold: SelfRepairGold, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.SelfRepair,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.SelfRepair = v);
        Rows[^1].PricedAboveAll = true; // cc-P46 Part C: the floor above every other property

        Add("Lower Stat Req (Weapon)", "Weapon", 100, 100, shards: 4, gold: 1500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.LowerStatReq,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.LowerStatReq = v);

        // -- Armor-specific ----------------------------------------------------

        Add("Physical Resist Bonus", "Defense", 20, 30, shards: 6, gold: 2500, ImbuableItem.Armor,
            i => ((BaseArmor)i).PhysicalBonus,
            (i, v) => ((BaseArmor)i).PhysicalBonus = v);

        Add("Fire Resist Bonus",     "Defense", 20, 30, shards: 6, gold: 2500, ImbuableItem.Armor,
            i => ((BaseArmor)i).FireBonus,
            (i, v) => ((BaseArmor)i).FireBonus = v);

        Add("Cold Resist Bonus",     "Defense", 20, 30, shards: 6, gold: 2500, ImbuableItem.Armor,
            i => ((BaseArmor)i).ColdBonus,
            (i, v) => ((BaseArmor)i).ColdBonus = v);

        Add("Poison Resist Bonus",   "Defense", 20, 30, shards: 6, gold: 2500, ImbuableItem.Armor,
            i => ((BaseArmor)i).PoisonBonus,
            (i, v) => ((BaseArmor)i).PoisonBonus = v);

        Add("Energy Resist Bonus",   "Defense", 20, 30, shards: 6, gold: 2500, ImbuableItem.Armor,
            i => ((BaseArmor)i).EnergyBonus,
            (i, v) => ((BaseArmor)i).EnergyBonus = v);

        Add("Self Repair (Armor)",   "Defense",  5,  7, shards: SelfRepairShards, gold: SelfRepairGold, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.SelfRepair,
            (i, v) => ((BaseArmor)i).ArmorAttributes.SelfRepair = v);
        Rows[^1].PricedAboveAll = true; // cc-P46 Part C

        Add("Lower Stat Req (Armor)", "Defense", 100, 100, shards: 4, gold: 1500, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.LowerStatReq,
            (i, v) => ((BaseArmor)i).ArmorAttributes.LowerStatReq = v);

        Add("Mage Armor",            "Defense",  1,  1, shards: 6, gold: 2000, isBool: true, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.MageArmor,
            (i, v) => ((BaseArmor)i).ArmorAttributes.MageArmor = v);

        // -- Slayer properties (weapons only) ----------------------------------
        // Every SlayerName but None has a row (cc-P173; ImbueAllSlayersVerification fails the build otherwise).

        AddSlayer("Slayer: Silver (Undead)",   SlayerName.Silver);
        AddSlayer("Slayer: Repond (Humanoid)", SlayerName.Repond);
        AddSlayer("Slayer: Dragon Slaying",    SlayerName.DragonSlaying);
        AddSlayer("Slayer: Daemon Dismissal",  SlayerName.DaemonDismissal);
        AddSlayer("Slayer: Fey",               SlayerName.Fey);
        AddSlayer("Slayer: Elemental Health",  SlayerName.ElementalHealth);
        AddSlayer("Slayer: Arachnid Doom",     SlayerName.ArachnidDoom);
        AddSlayer("Slayer: Reptilian Death",   SlayerName.ReptilianDeath);
        AddSlayer("Slayer: Troll Slaughter",   SlayerName.TrollSlaughter);
        AddSlayer("Slayer: Ogre Thrashing",    SlayerName.OgreTrashing);
        AddSlayer("Slayer: Snake's Bane",      SlayerName.SnakesBane);
        AddSlayer("Slayer: Spider's Death",    SlayerName.SpidersDeath);
        AddSlayer("Slayer: Scorpion's Bane",   SlayerName.ScorpionsBane);
        AddSlayer("Slayer: Ophidian",          SlayerName.Ophidian);

        // cc-P173 (Chase 2026-10-18, a War Axe with Snow Elemental Slayer showed nothing to extract): the other 13 of pinned's
        // SlayerName values (SlayerName.cs), so every slayer a weapon can carry can be extracted, disenchanted and imbued.
        // The pinned name first, as the 14 above, then OSI's property name (SlayerEntry.cs m_AosTitles) where it differs.
        // The 14 names above stay as they are: a saved essence's PropertyKey is the row's name. Super or lesser by
        // SuperSlayers below. Balron Damnation is not imbuable on OSI (ServUO ItemPropertyInfo.cs skips IDs 109-110 with
        // Daemon Dismissal, which this table already had): kept as a lesser slayer, as Daemon Dismissal is.
        AddSlayer("Slayer: Orc Slaying",                      SlayerName.OrcSlaying);
        AddSlayer("Slayer: Terathan",                         SlayerName.Terathan);
        AddSlayer("Slayer: Lizardman Slaughter",              SlayerName.LizardmanSlaughter);
        AddSlayer("Slayer: Gargoyle's Foe",                   SlayerName.GargoylesFoe);
        AddSlayer("Slayer: Balron Damnation",                 SlayerName.BalronDamnation);
        AddSlayer("Slayer: Exorcism (Demon)",                 SlayerName.Exorcism);
        AddSlayer("Slayer: Flame Dousing (Fire Elemental)",   SlayerName.FlameDousing);
        AddSlayer("Slayer: Water Dissipation (Water Elemental)", SlayerName.WaterDissipation);
        AddSlayer("Slayer: Vacuum (Air Elemental)",           SlayerName.Vacuum);
        AddSlayer("Slayer: Earth Shatter (Earth Elemental)",  SlayerName.EarthShatter);
        AddSlayer("Slayer: Blood Drinking (Blood Elemental)", SlayerName.BloodDrinking);
        AddSlayer("Slayer: Summer Wind (Snow Elemental)",     SlayerName.SummerWind);
        AddSlayer("Slayer: Elemental Ban (Elemental)",        SlayerName.ElementalBan);

        // -- cc-P82: the design ladder's rows the catalogue lacked (appended, so earlier rows keep their indexes) --------

        Add("Hit Point Increase", "Stats", 5, 7, shards: 5, gold: 2000, ImbuableItem.All,
            i => GetAttr(i).BonusHits, (i, v) => GetAttr(i).BonusHits = v);
        Add("Stamina Increase", "Stats", 8, 12, shards: 5, gold: 2000, ImbuableItem.All,
            i => GetAttr(i).BonusStam, (i, v) => GetAttr(i).BonusStam = v);
        Add("Mana Increase", "Stats", 8, 12, shards: 5, gold: 2000, ImbuableItem.All,
            i => GetAttr(i).BonusMana, (i, v) => GetAttr(i).BonusMana = v);

        Add("Durability (Weapon)", "Weapon", 100, 150, shards: 3, gold: 1000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.DurabilityBonus,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.DurabilityBonus = v);
        Add("Durability (Armor)", "Defense", 100, 150, shards: 3, gold: 1000, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.DurabilityBonus,
            (i, v) => ((BaseArmor)i).ArmorAttributes.DurabilityBonus = v);

        // Elemental damage conversion: one knowledge family; the rest of the hit stays physical (pinned BaseWeapon
        // GetDamageTypes: physical is 100 less the others), so the elements together never pass 100.
        AddElement("Fire Damage",   e => e.Fire,   (e, v) => e.Fire = v);
        AddElement("Cold Damage",   e => e.Cold,   (e, v) => e.Cold = v);
        AddElement("Poison Damage", e => e.Poison, (e, v) => e.Poison = v);
        AddElement("Energy Damage", e => e.Energy, (e, v) => e.Energy = v);

        Add("Balanced", "Weapon", 1, 1, shards: 6, gold: 2500, isBool: true, ImbuableItem.Weapon,
            i => ((BaseRanged)i).Balanced ? 1 : 0, (i, v) => ((BaseRanged)i).Balanced = v != 0);
        Rows[^1].Allows = i => i is BaseRanged;
        Add("Velocity", "Weapon", 50, 60, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseRanged)i).Velocity, (i, v) => ((BaseRanged)i).Velocity = v);
        Rows[^1].Allows = i => i is BaseRanged;

        // Mage Weapon: the stored value v reads "Mage Weapon -(30 - v) skill" (pinned AOS.cs), so 15 is OSI's -15.
        Add("Mage Weapon", "Weapon", 15, 20, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.MageWeapon,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.MageWeapon = v);

        // +Skill (design "+Skill properties"): jewelry takes OSI's combat and magic skills, one per OSI group, in the
        // group's own AosSkillBonuses slot (ServUO Imbuing.cs m_SkillGroups; slot j is group j). Crafter gear (aprons, and
        // the types in imbuing.json crafterGearTypes) takes crafting skills in slot 0 and gathering skills in slot 1.
        for (var g = 0; g < JewelrySkillGroups.Length; g++)
        {
            foreach (var skill in JewelrySkillGroups[g])
            {
                AddSkill(skill, g, ImbuableItem.Jewel, ImbueFamilies.JewelrySkill, null);
            }
        }

        for (var g = 0; g < CrafterSkillGroups.Length; g++)
        {
            foreach (var skill in CrafterSkillGroups[g])
            {
                AddSkill(skill, g, ImbuableItem.All, ImbueFamilies.CrafterSkill, IsCrafterGear);
            }
        }

        // The design's custom properties: values, kinds and costs from imbuing.json (custom); one per item.
        foreach (var (name, c) in ImbuingTable.DefaultCustom())
        {
            AddCustom(name, c);
        }

        var rows = Rows;
        _building = null;
        return rows;
    }

    // ServUO Imbuing.cs m_SkillGroups (OSI's five +skill groups).
    public static readonly SkillName[][] JewelrySkillGroups =
    [
        [SkillName.Fencing, SkillName.Macing, SkillName.Swords, SkillName.Musicianship, SkillName.Magery],
        [SkillName.Wrestling, SkillName.AnimalTaming, SkillName.SpiritSpeak, SkillName.Tactics, SkillName.Provocation],
        [SkillName.Focus, SkillName.Parry, SkillName.Stealth, SkillName.Meditation, SkillName.AnimalLore, SkillName.Discordance],
        [SkillName.Mysticism, SkillName.Bushido, SkillName.Necromancy, SkillName.Veterinary, SkillName.Stealing, SkillName.EvalInt,
         SkillName.Anatomy],
        [SkillName.Peacemaking, SkillName.Ninjitsu, SkillName.Chivalry, SkillName.Archery, SkillName.MagicResist, SkillName.Healing,
         SkillName.Throwing],
    ];

    // cc-P82: crafter gear's two groups, crafting then gathering.
    public static readonly SkillName[][] CrafterSkillGroups =
    [
        [SkillName.Blacksmith, SkillName.Tailoring, SkillName.Carpentry, SkillName.Fletching, SkillName.Tinkering,
         SkillName.Alchemy, SkillName.Inscribe, SkillName.Cooking, SkillName.Cartography],
        [SkillName.Mining, SkillName.Lumberjacking, SkillName.Fishing],
    ];

    public static bool IsCrafterGear(Item item) =>
        Array.Exists(ImbuingTable.Current.CrafterGearTypes, n =>
        {
            for (var t = item.GetType(); t != null; t = t.BaseType)
            {
                if (t.Name == n) return true;
            }

            return false;
        });

    public static string SkillRowName(SkillName skill) => $"Skill: {SkillInfo.Table[(int)skill].Name}";

    private static AosSkillBonuses SkillBonusesOf(Item item) => item switch
    {
        BaseJewel j    => j.SkillBonuses,
        BaseClothing c => c.SkillBonuses,
        BaseWeapon w   => w.SkillBonuses,
        BaseArmor a    => a.SkillBonuses,
        _              => null
    };

    private static void AddSkill(SkillName skill, int slot, ImbuableItem applies, string family, Func<Item, bool> allows)
    {
        var sk = skill;
        Rows.Add(new ImbuePropertyDef
        {
            Name               = SkillRowName(sk),
            Group              = "Skills",
            MaxVanilla         = 15,
            MaxGuild           = 25,
            ShardBase          = 6,
            GoldBase           = 2500,
            DiscoveryThreshold = ComputeThreshold(2500),
            KnowledgeKey       = family,
            Applies            = applies,
            Allows             = allows,
            Get = item => SkillBonusesOf(item) is { } b && b.GetBonus(slot) > 0 && b.GetSkill(slot) == sk ? (int)b.GetBonus(slot) : 0,
            Set = (item, v) =>
            {
                if (SkillBonusesOf(item) is not { } b) return;
                if (v > 0) b.SetValues(slot, sk, v);
                else if (b.GetSkill(slot) == sk) b.SetValues(slot, SkillName.Alchemy, 0);
            },
            // One skill per group: the group's slot holds another skill.
            BlockedBy = item => SkillBonusesOf(item) is { } b && b.GetBonus(slot) > 0 && b.GetSkill(slot) != sk
                ? $"one skill per group: this item already has {SkillInfo.Table[(int)b.GetSkill(slot)].Name}"
                : null
        });
    }

    private static void AddElement(string name, Func<AosElementAttributes, int> get, Action<AosElementAttributes, int> set)
    {
        Add(name, "Weapon", 100, 100, shards: 6, gold: 2500, ImbuableItem.Weapon,
            i => get(((BaseWeapon)i).AosElementDamages),
            (i, v) =>
            {
                var e = ((BaseWeapon)i).AosElementDamages;
                var others = e.Fire + e.Cold + e.Poison + e.Energy + e.Chaos + e.Direct - get(e);
                set(e, Math.Clamp(v, 0, Math.Max(0, 100 - others)));
            });
        Rows[^1].KnowledgeKey = ImbueFamilies.ElementalDamage;
    }

    private static void AddCustom(string name, ImbueCustomConfig c)
    {
        var n = name;
        Rows.Add(new ImbuePropertyDef
        {
            Name               = n,
            Group              = "Custom",
            MaxVanilla         = c.Full,
            MaxGuild           = c.Overcharged,
            ShardBase          = c.Shards,
            GoldBase           = c.Gold,
            DiscoveryThreshold = 0, // no essence: a custom property takes its ingredient instead
            KnowledgeKey       = n,
            IsCustom           = true,
            Applies            = ImbueFamilies.ParseApplies(c.Applies),
            Get                = item => ImbueCustomStore.Get(item, n),
            Set                = (item, v) => ImbueCustomStore.Set(item, n, v),
            BlockedBy          = item => ImbueCustomStore.Of(item) is { } p && !p.Name.Equals(n, StringComparison.OrdinalIgnoreCase)
                ? $"one custom property per item: this item already has {p.Name}"
                : null
        });
    }

    /// <summary>The ingredient a custom property takes on each successful imbue (imbuing.json custom), or null.</summary>
    public static GuildCost CustomIngredient(ImbuePropertyDef def) =>
        def.IsCustom && ImbuingTable.Current.CustomOf(def.Name) is { IngredientAmount: > 0 } c &&
        AssemblyHandler.FindTypeByName(c.Ingredient) is { } type
            ? GuildCost.Of(type, c.IngredientAmount)
            : null;

    // -- Slayer helper ---------------------------------------------------------

    // cc-P82: OSI's super slayers (the design's rank 4); the rest are single slayers (rank 3).
    private static readonly HashSet<SlayerName> SuperSlayers =
    [
        SlayerName.Silver, SlayerName.Repond, SlayerName.ReptilianDeath, SlayerName.ArachnidDoom, SlayerName.Fey,
        SlayerName.ElementalBan, SlayerName.Exorcism
    ];

    private static void AddSlayer(string name, SlayerName slayer)
    {
        // Slayers are expensive to master (threshold 10) and costly to imbue.
        // They are boolean: either applied or not.
        var sn = slayer;
        Rows.Add(new ImbuePropertyDef
        {
            Name               = name,
            Group              = "Slayer",
            MaxVanilla         = 1,
            MaxGuild           = 1,
            ShardBase          = 15,
            GoldBase           = 6000,
            IsBool             = true,
            DiscoveryThreshold = 10,
            KnowledgeKey       = SuperSlayers.Contains(sn) ? ImbueFamilies.SuperSlayer : ImbueFamilies.Slayer,
            Applies            = ImbuableItem.Weapon,
            // Get: returns 1 if item already has this slayer, else 0
            Get = item =>
            {
                if (item is not BaseWeapon w) return 0;
                return (w.Slayer == sn || w.Slayer2 == sn) ? 1 : 0;
            },
            // Set: value=1 applies slayer (in Slayer or Slayer2 slot); value=0 removes it
            Set = (item, value) =>
            {
                if (item is not BaseWeapon w) return;
                if (value > 0)
                {
                    // Apply to first available slot
                    if (w.Slayer == SlayerName.None)
                        w.Slayer = sn;
                    else if (w.Slayer2 == SlayerName.None)
                        w.Slayer2 = sn;
                    // else both slots full - no-op (caller should check)
                }
                else
                {
                    // Remove from whichever slot it's in
                    if (w.Slayer  == sn) w.Slayer  = SlayerName.None;
                    if (w.Slayer2 == sn) w.Slayer2 = SlayerName.None;
                }
            }
        });
    }

    // -- General Add helpers ---------------------------------------------------

    private static void Add(
        string name, string group, int maxVanilla, int maxGuild,
        int shards, int gold, ImbuableItem applies,
        Func<Item, int> get, Action<Item, int> set,
        bool isBool = false)
    {
        Rows.Add(new ImbuePropertyDef
        {
            Name               = name,
            Group              = group,
            MaxVanilla         = maxVanilla,
            MaxGuild           = maxGuild,
            ShardBase          = shards,
            GoldBase           = gold,
            IsBool             = isBool,
            DiscoveryThreshold = ComputeThreshold(gold),
            KnowledgeKey       = name,
            Applies            = applies,
            Get                = get,
            Set                = set
        });
    }

    private static void Add(
        string name, string group, int maxVanilla, int maxGuild,
        int shards, int gold, bool isBool, ImbuableItem applies,
        Func<Item, int> get, Action<Item, int> set)
        => Add(name, group, maxVanilla, maxGuild, shards, gold, applies, get, set, isBool);

    // -- Discovery threshold ---------------------------------------------------

    /// <summary>
    /// Computes how many successful imbues using an essence are required before
    /// the property is "mastered" and no longer needs an essence.
    /// More expensive properties require more uses.
    /// </summary>
    public static int ComputeThreshold(int goldBase) => goldBase switch
    {
        < 500  => 3,
        < 1500 => 5,
        < 3000 => 8,
        _      => 10
    };

    // -- Helper - get AosAttributes regardless of item type -------------------

    internal static AosAttributes GetAttr(Item item) => item switch
    {
        BaseWeapon  w => w.Attributes,
        BaseArmor   a => a.Attributes,
        BaseJewel   j => j.Attributes,
        BaseClothing c => c.Attributes,
        _             => throw new InvalidOperationException($"Unsupported item type: {item.GetType()}")
    };

    /// <summary>Returns properties applicable to the given item.</summary>
    public static List<ImbuePropertyDef> ForItem(Item item)
    {
        var flag   = GetItemFlag(item);
        var result = new List<ImbuePropertyDef>();
        foreach (var def in All)
            if ((def.AppliesNow & flag) != 0 && def.Allows?.Invoke(item) != false)
                result.Add(def);
        return result;
    }

    public static ImbuableItem GetItemFlag(Item item) => item switch
    {
        BaseWeapon   => ImbuableItem.Weapon,
        BaseArmor    => ImbuableItem.Armor,
        BaseJewel    => ImbuableItem.Jewel,
        BaseClothing => ImbuableItem.Clothing,
        _            => ImbuableItem.None
    };

    public static bool IsImbuable(Item item) => GetItemFlag(item) != ImbuableItem.None;

    /// <summary>cc-P82: the item's total intensity, each property's value over its Full value, in percent (budget mode).</summary>
    public static int TotalIntensity(Item item)
    {
        var total = 0;
        foreach (var def in ForItem(item))
            total += def.IntensityPercent(def.Get(item));
        return total;
    }

    /// <summary>
    /// cc-P175 Part A: ServUO's Imbuing.GetTotalWeight (Services/LootGeneration/Imbuing/Core/Imbuing.cs:1330-1452, each
    /// property by GetIntensityForID :1986-2016, weight / max intensity x value) read from this catalogue, so Clean Up
    /// Britannia sees exactly the properties the Imbuing Table sees. Our rows carry no per-property weight (ServUO's run 100
    /// to 150), so each counts 100 at its Full value: the row's IntensityPercent. <paramref name="skipDamageIncrease"/> is
    /// ServUO's id 12 for a weapon that is not DImodded (CleanUpBritanniaData.cs:811); pinned has no DImodded flag.
    /// </summary>
    public static int GetTotalWeight(Item item, bool skipDamageIncrease)
    {
        var total = 0;
        foreach (var (def, value) in GetActiveProperties(item))
        {
            if (skipDamageIncrease && def.Name == "Damage Increase")
            {
                continue;
            }

            total += def.IntensityPercent(value);
        }

        return total;
    }

    /// <summary>Counts how many imbue-tracked properties the item currently has.</summary>
    public static int CountActiveProperties(Item item)
    {
        var props  = ForItem(item);
        var active = new HashSet<string>();
        foreach (var def in props)
            if (def.Get(item) != 0)
                active.Add(def.Name);
        return active.Count;
    }

    /// <summary>
    /// Returns all properties currently active on the item with their values.
    /// Includes slayer properties read dynamically.
    /// </summary>
    public static List<(ImbuePropertyDef Def, int Value)> GetActiveProperties(Item item)
    {
        var result = new List<(ImbuePropertyDef, int)>();
        foreach (var def in ForItem(item))
        {
            var val = def.Get(item);
            if (val != 0)
                result.Add((def, val));
        }
        return result;
    }
}

// -- Main imbuing / disenchanting gump ----------------------------------------

/// <summary>
/// Shattered Legacy - Artificers' Order Imbuing &amp; Disenchanting Gump.
///
/// Stages:
///   SelectItem        - entry point; choose to imbue, disenchant, or craft essence
///   ViewItem          - show item enchantments; click property to imbue/upgrade
///   SelectProp        - choose vanilla max or guild-enhanced max; confirm costs
///   Confirm           - final cost confirmation before applying imbue
///   DisenchantView    - show item enchantments + choose full disenchant or extract property
///   DisenchantConfirm - confirm destroying item (full disenchant)
///   ExtractConfirm    - confirm extracting a specific property
///   UnravelContainerConfirm - confirm unravelling every magic item at the top level of a container (cc-P176)
///   CraftSelect       - list mastered properties available to craft essences from
///   CraftConfirm      - confirm crafting a specific PropertyEssence
///
/// Discovery system:
///   All properties require a PropertyEssence until mastered.
///   Mastery threshold scales with property power (GoldBase):
///     &lt;500g -> 3 uses, &lt;1500g -> 5 uses, &lt;3000g -> 8 uses, else 10 uses.
///   Once mastered: no essence needed; player can craft &amp; sell essences.
/// </summary>
public sealed class ArtificersImbueGump : Gump
{
    private enum Stage
    {
        SelectItem,
        ViewItem,
        SelectProp,
        Confirm,
        DisenchantView,
        DisenchantConfirm,
        ExtractConfirm,
        CraftSelect,
        CraftConfirm,
        UnravelContainerConfirm,
    }

    private const int W    = 530;
    private const int H    = 570;
    private const int BgId = 9270;

    // -- Per-instance state ----------------------------------------------------
    private readonly PlayerMobile _pm;
    private readonly Item?        _item;
    private readonly int          _selectedPropIdx;  // index into ForItem(_item) for imbue/extract; index into All for craft
    private readonly int          _targetValue;
    private readonly Stage        _stage;
    private readonly int          _page;

    // Public constructor - no item selected yet
    public ArtificersImbueGump(PlayerMobile pm)
        : this(pm, null, Stage.SelectItem, -1, 0, 0) { }

    // Internal stage constructors
    private ArtificersImbueGump(PlayerMobile pm, Item? item, Stage stage,
        int selectedPropIdx, int targetValue, int page)
        : base(80, 60)
    {
        _pm              = pm;
        _item            = item;
        _stage           = stage;
        _selectedPropIdx = selectedPropIdx;
        _targetValue     = targetValue;
        _page            = page;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 95, 12, 1153, "Artificers' Order - Imbuing Table");
        AddImageTiled(10, 32, W - 20, 2, 9304);

        var acct     = pm.Account as IAccount;
        var data     = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        var standing = guild.GetReputation("artificers");
        var shards   = guild.GetCurrency("artificers");
        var isMember = guild.JoinedGuilds.Contains("artificers");

        AddLabel(18,  38, 999,  $"Rank: {ArtificersGuildmasterGump.GetRankName(standing)}");
        AddLabel(200, 38, 1153, $"Essence Shards: {shards}");

        AddImageTiled(10, 54, W - 20, 2, 9304);

        switch (_stage)
        {
            case Stage.SelectItem:        DrawSelectItem(data, standing);                    break;
            case Stage.ViewItem:          DrawViewItem(data, standing, isMember);            break;
            case Stage.SelectProp:        DrawSelectProp(data, standing, isMember);          break;
            case Stage.Confirm:           DrawConfirm(data, standing, isMember);             break;
            case Stage.DisenchantView:    DrawDisenchantView(data, standing);                break;
            case Stage.DisenchantConfirm: DrawDisenchantConfirm(data, standing);             break;
            case Stage.ExtractConfirm:    DrawExtractConfirm(data, standing);                break;
            case Stage.CraftSelect:       DrawCraftSelect(data, standing);                   break;
            case Stage.CraftConfirm:      DrawCraftConfirm(data, standing);                  break;
            case Stage.UnravelContainerConfirm: DrawUnravelContainerConfirm(data, standing);   break;
        }

        // Footer
        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        if (_stage != Stage.SelectItem)
        {
            AddButton(18, H - 28, 4014, 4015, (int)BtnId.Back);
            AddLabel(40, H - 26, 999, "Back");
        }
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28,  H - 26, 1153, "X");
    }

    // -- Stage: Select Item (entry point) -------------------------------------

    private void DrawSelectItem(ClusterFAccountData data, int standing)
    {
        var y = 62;

        // -- Imbue section -----------------------------------------------------
        AddLabel(18, y, 1153, "Imbuing");
        y += 18;
        AddLabel(18, y, 999,
            "Target a weapon, armour, jewellery, or clothing item to begin imbuing.");
        y += 20;
        AddButton(18, y, 4011, 4012, (int)BtnId.ImbueTarget);
        AddLabel(44, y + 2, 1154, "Target an item to imbue...");
        y += 32;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // -- Disenchant section ------------------------------------------------
        AddLabel(18, y, 1153, "Disenchanting");
        y += 18;
        AddLabel(18, y, 999,
            "Break down a magic item into Essence Shards, or extract a specific property.");
        y += 20;
        AddButton(18, y, 4011, 4012, (int)BtnId.DisenchantTarget);
        AddLabel(44, y + 2, 0x21, "Target an item to disenchant...");
        y += 24;
        // cc-P176: OSI's Unravel Container (ServUO pub57 ImbuingGump.cs:55, 153-158).
        AddButton(18, y, 4011, 4012, (int)BtnId.UnravelContainerTarget);
        AddLabel(44, y + 2, 0x21, "Unravel Container - every magic item in a bag...");
        y += 32;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // -- Craft Essence section ---------------------------------------------
        AddLabel(18, y, 1153, "Craft Essence");
        y += 18;

        // Count mastered properties
        var masteredCount = 0;
        foreach (var def in ImbueCatalogue.All)
            if (!def.IsCustom && data.IsMastered(def.Name, def.DiscoveryThreshold))
                masteredCount++;

        if (masteredCount == 0)
        {
            AddLabel(18, y, 0x3B2,
                "You have not mastered any properties yet. Master a property by imbuing it " +
                "enough times using a PropertyEssence.");
        }
        else
        {
            AddLabel(18, y, 999,
                $"You have mastered {masteredCount} propert{(masteredCount == 1 ? "y" : "ies")}. " +
                "Craft essences to sell or trade.");
            y += 20;
            AddButton(18, y, 4005, 4006, (int)BtnId.CraftBtn);
            AddLabel(44, y + 2, 0x44, "Open Essence Crafting...");
        }
    }

    // -- Stage: View Item (imbue mode) -----------------------------------------
    //
    // cc-P82: only what the account knows is listed (ClusterFImbueKnowledge), each up to the tier it knows and the place
    // allows (ImbueSites); then one hint line per category with anything locked. Rows keep their index in
    // ImbueCatalogue.ForItem(item) as their button (PropBase + index).

    private void DrawViewItem(ClusterFAccountData data, int standing, bool isMember)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var site      = ImbueSites.Resolve(_pm);
        var maxSlots  = ClusterFImbueKnowledge.MaxSlots(data);
        var budget    = ClusterFImbueKnowledge.IntensityBudget(data);
        var itemProps = ImbueCatalogue.ForItem(_item);
        var usedSlots = ImbueCatalogue.CountActiveProperties(_item);

        AddLabel(18, 60, 1154, $"Item: {_item.Name ?? _item.GetType().Name}");
        var budgetStr = budget > 0 ? $"  |  Intensity: {ImbueCatalogue.TotalIntensity(_item)}/{budget}%" : "";
        AddLabel(18, 78, 999,  $"Properties: {usedSlots}/{maxSlots}{budgetStr}  |  Here: up to {site.Cap}" +
                               (site.BonusPercent > 0 ? $", +{site.BonusPercent}% success" : ""));

        AddLabel(18, 98, 1153, "Current Enchantments:");
        AddImageTiled(10, 114, W - 20, 1, 9304);

        var y   = 118;
        var any = false;
        foreach (var def in itemProps)
        {
            var cur = def.Get(_item);
            if (cur == 0) continue;
            var pctStr = def.IsBool ? "(on)" : $"{cur} ({ImbueTiers.TierOf(def, cur)})";
            AddLabel(18,  y, 999,  def.Name);
            AddLabel(340, y, 1154, pctStr);
            y  += 18;
            any = true;
        }
        if (!any) AddLabel(18, y, 0x3B2, "(none)");

        AddImageTiled(10, y + 20, W - 20, 1, 9304);

        var listY    = y + 28;
        var listH    = H - listY - 60;
        var rowH     = 22;
        var rowsMax  = Math.Max(1, listH / rowH);
        var startIdx = _page * rowsMax;

        AddLabel(18, listY - 18, 1153, "Known Properties (click to imbue/upgrade):");

        // The list: known rows, then the hint lines (a category with nothing known still gets its hint).
        var entries = new List<(ImbuePropertyDef Def, string Hint)>();
        foreach (var def in itemProps)
            if (ClusterFImbueKnowledge.KnownTier(data, def.KnowledgeKey) >= ImbueTier.Basic)
                entries.Add((def, null));
        foreach (var group in itemProps.Select(d => d.Group).Distinct())
            if (ClusterFImbueKnowledge.HintFor(data, group, itemProps) is { } hint)
                entries.Add((null, hint));

        for (var e = startIdx; e < entries.Count && e < startIdx + rowsMax; e++)
        {
            var (def, hint) = entries[e];
            if (def == null)
            {
                AddLabel(18, listY, 0x3B2, hint);
                listY += rowH;
                continue;
            }

            var propIdx  = itemProps.IndexOf(def);
            var cur      = def.Get(_item);
            var top      = AllowedTier(data, def, site);
            var maxAllow = top >= ImbueTier.Basic ? ImbueTiers.MaxFor(def, top) : 0;
            var atMax    = cur >= maxAllow;
            var noRoom   = cur == 0 && usedSlots >= maxSlots;
            var blockedBy = def.BlockedBy?.Invoke(_item);

            // Discovery status
            var mastered = data.IsMastered(def.Name, def.DiscoveryThreshold);
            var discCount = data.GetDiscoveryCount(def.Name);
            var essCount  = mastered ? 0 : CountEssencesFor(def.Name);
            var needsEss  = !mastered && essCount == 0 && !atMax;

            var hue = atMax           ? 0x3B2 :
                      noRoom          ? 0x22  :
                      blockedBy != null ? 0x22 :
                      needsEss        ? 0x3B2 : 999;

            var btnActive = !atMax && !noRoom && blockedBy == null && !needsEss;
            if (btnActive)
                AddButton(18, listY, 4011, 4012, (int)BtnId.PropBase + propIdx);

            var curStr  = def.IsBool ? (cur == 1 ? "ON" : "off") : $"{cur}";
            var maxStr  = def.IsBool ? "" : $"/ {maxAllow}";
            var discStr = mastered ? (def.IsCustom ? "" : " [mastered]") :
                          needsEss ? $" [need essence {discCount}/{def.DiscoveryThreshold}]" :
                          essCount > 0 ? $" [essence ready {discCount}/{def.DiscoveryThreshold}]" : "";
            var note    = atMax           ? " [MAX]"        :
                          noRoom          ? " [slots full]" :
                          blockedBy != null ? " [one per group/item]" : discStr;

            AddLabel(44,  listY, hue, def.Name);
            AddLabel(300, listY, hue, $"{curStr} {maxStr}{note}");

            listY += rowH;
        }

        var footY = H - 56;
        if (_page > 0)
        {
            AddButton(18, footY, 4014, 4015, (int)BtnId.PrevPage);
            AddLabel(40, footY + 2, 999, "Prev");
        }
        if (startIdx + rowsMax < entries.Count)
        {
            AddButton(120, footY, 4005, 4006, (int)BtnId.NextPage);
            AddLabel(142, footY + 2, 999, "Next");
        }

        AddButton(W - 180, footY, 4011, 4012, (int)BtnId.ImbueTarget);
        AddLabel(W - 154,  footY + 2, 999, "Change Item");
    }

    /// <summary>cc-P82: the highest tier this account may imbue this row at here: what it knows, what the place allows.</summary>
    internal static ImbueTier AllowedTier(ClusterFAccountData data, ImbuePropertyDef def, ImbueSite site)
    {
        var known = ClusterFImbueKnowledge.KnownTier(data, def.KnowledgeKey);
        var top   = (ImbueTier)Math.Min((int)known, Math.Min((int)site.Cap, (int)ImbueTiers.TopTier(def)));
        return top;
    }

    // -- Stage: Select Target Value --------------------------------------------

    private void DrawSelectProp(ClusterFAccountData data, int standing, bool isMember)
    {
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def = itemProps[_selectedPropIdx];
        var cur = def.Get(_item);

        var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
        var discCount = data.GetDiscoveryCount(def.Name);
        var essCount  = mastered ? 0 : CountEssencesFor(def.Name);
        var known     = ClusterFImbueKnowledge.KnownTier(data, def.KnowledgeKey);

        AddLabel(18, 60, 1154, $"Imbue: {def.Name}");
        AddLabel(18, 80, 999,  $"Current value: {(def.IsBool ? (cur == 1 ? "ON" : "OFF") : cur.ToString())}");

        // cc-P82: an unknown property is not offered (a client could still press its button).
        if (known < ImbueTier.Basic)
        {
            AddLabel(18, 98, 0x22, "Your account does not know this property.");
            return;
        }

        if (def.IsCustom)
            AddLabel(18, 98, 1154, $"Ingredient: {DescribeIngredient(def)}");
        else if (mastered)
            AddLabel(18, 98, 0x44, $"Mastered - no essence required. (Uses: {discCount}/{def.DiscoveryThreshold})");
        else
        {
            var essHue = essCount > 0 ? 1154 : 0x22;
            AddLabel(18, 98, essHue,
                $"Discovery: {discCount}/{def.DiscoveryThreshold} uses - " +
                (essCount > 0 ? DescribeEssences(_pm, def.Name, withName: false) : "No essence in pack or bank!"));
        }

        AddImageTiled(10, 114, W - 20, 1, 9304);

        if (def.IsBool)
            DrawBoolChoice(def, cur);
        else
            DrawValueChoice(data, def, cur);
    }

    private void DrawBoolChoice(ImbuePropertyDef def, int cur)
    {
        var shards = def.ShardsFor(1);
        var gold   = def.GoldFor(1);
        var y      = 120;

        if (cur == 0)
        {
            AddLabel(18, y, 999, $"Enable {def.Name}");
            y += 20;
            AddLabel(18, y, 1154, $"Cost: {shards} Essence Shard(s) + {gold:N0} gold");
            y += 30;
            AddButton(18, y, 4005, 4006, (int)BtnId.ConfirmBase + (int)ImbueTier.Basic);
            AddLabel(44, y + 2, 0x44, "Apply");
        }
        else
        {
            AddLabel(18, y, 0x3B2, $"{def.Name} is already enabled on this item.");
        }
    }

    // cc-P82: one choice per tier the account knows (locked tiers are not shown). A known tier the place does not allow
    // is shown without a button, with where it can be reached.
    private void DrawValueChoice(ClusterFAccountData data, ImbuePropertyDef def, int cur)
    {
        var y     = 120;
        var site  = ImbueSites.Resolve(_pm);
        var known = ClusterFImbueKnowledge.KnownTier(data, def.KnowledgeKey);

        AddLabel(18, y, 1153, "Available imbue tiers:");
        y += 22;

        for (var tier = ImbueTier.Basic; tier <= ImbueTiers.TopTier(def) && tier <= known; tier++)
        {
            var target = ImbueTiers.MaxFor(def, tier);
            if (tier > site.Cap)
            {
                AddLabel(28, y, 0x3B2, $"{tier} ({target}): {(tier == ImbueTier.Overcharged ? "needs a soul forge" : "not here")}");
                y += 30;
                continue;
            }

            TryDrawTier(def, cur, y, target, tier, site.BonusPercent);
            y += 50;
        }
    }

    private void TryDrawTier(ImbuePropertyDef def, int cur, int y, int target, ImbueTier tier, int bonus)
    {
        if (target <= 0) return;
        var isUpgrade = target > cur;
        var isAtValue = target == cur;
        var shards    = def.ShardsFor(target);
        var gold      = def.GoldFor(target);
        var diff      = EffectiveDifficulty(def, target, bonus);
        var chance    = ComputeSuccessChance(_pm.Skills[SkillName.Imbuing].Value, diff);
        var chanceHue = chance >= 75 ? 0x44 : chance >= 40 ? 999 : 0x22;
        var hue       = isAtValue ? 0x3B2 : (isUpgrade ? 999 : 0x3B2);
        var note      = isAtValue ? " [current]" : (!isUpgrade ? " [downgrade]" : "");
        var label     = $"{tier}: set to {target}{note} - {shards} Shard(s) + {gold:N0} gold";

        AddLabel(28, y, hue, label);
        if (isUpgrade)
        {
            AddLabel(28, y + 16, chanceHue, $"  Success: {chance}%  (skill {_pm.Skills[SkillName.Imbuing].Value:F1} vs difficulty {diff:F0})");
            AddButton(18, y, 4005, 4006, (int)BtnId.ConfirmBase + (int)tier);
        }
    }

    /// <summary>cc-P82: the difficulty with the place's success bonus taken off (1 point of chance is 0.4 skill on the 40-point window).</summary>
    internal static double EffectiveDifficulty(ImbuePropertyDef def, int target, int bonusPercent) =>
        def.SkillDifficulty(target) - bonusPercent * 0.4;

    private static string DescribeIngredient(ImbuePropertyDef def) =>
        ImbuingTable.Current.CustomOf(def.Name) is { IngredientAmount: > 0 } c ? $"{c.IngredientAmount} {c.Ingredient}" : "none";

    // -- Stage: Confirm Imbue --------------------------------------------------

    private void DrawConfirm(ClusterFAccountData data, int standing, bool isMember)
    {
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def    = itemProps[_selectedPropIdx];
        var cur    = def.Get(_item);
        var site   = ImbueSites.Resolve(_pm);
        var shards = def.ShardsFor(_targetValue);
        var gold   = def.GoldFor(_targetValue);
        var diff   = EffectiveDifficulty(def, _targetValue, site.BonusPercent);

        var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
        var discCount = data.GetDiscoveryCount(def.Name);
        var essCount  = mastered ? 0 : CountEssencesFor(def.Name);
        var pmShards  = guild.GetCurrency("artificers");
        var pmGold    = CompactGoldHelper.GetTotalGold(_pm);
        var canAfford = pmShards >= shards && pmGold >= gold;
        var hasEss    = mastered || essCount > 0;

        AddLabel(18, 60, 1154, "Confirm Imbue");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var essLine = def.IsCustom
            ? $"<BASEFONT COLOR=#AACCFF>Ingredient: {DescribeIngredient(def)}</BASEFONT><BR>"
            : mastered
            ? "<BASEFONT COLOR=#44FF44>Mastered - no essence required.</BASEFONT><BR>"
            : $"<BASEFONT COLOR={(essCount > 0 ? "#AACCFF" : "#FF4444")}>Requires: 1 Essence of {def.Name} " +
              $"(you have {DescribeEssences(_pm, def.Name)})  Discovery: {discCount}/{def.DiscoveryThreshold}</BASEFONT><BR>";
        var chance     = ComputeSuccessChance(_pm.Skills[SkillName.Imbuing].Value, diff);
        var chanceColor = chance >= 75 ? "#44FF44" : chance >= 40 ? "#FFD700" : "#FF4444";
        var siteLine   = site.BonusPercent > 0 ? $"  |  {site.Name}: +{site.BonusPercent}%" : "";

        var html =
            $"<BASEFONT COLOR=#CCCCCC>Property: {def.Name}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Current value: {cur}  ->  Target: {_targetValue} ({ImbueTiers.TierOf(def, _targetValue)})</BASEFONT><BR>" +
            $"{essLine}" +
            $"<BASEFONT COLOR=#FFD700>Essence Shards: {shards}  (you have: {pmShards})</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#FFD700>Gold: {gold:N0}  (you have: {pmGold:N0})</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Difficulty: {diff:F0}  |  Your Imbuing: {_pm.Skills[SkillName.Imbuing].Value:F1}{siteLine}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={chanceColor}><B>Success Chance: {chance}%</B></BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Failure does NOT consume resources but the item may lose " +
            "10 durability. The essence is consumed on success.</BASEFONT>";

        AddHtml(16, 84, W - 32, 220, html, false, false);
        AddImageTiled(10, 316, W - 20, 1, 9304);

        if (canAfford && hasEss)
        {
            AddButton(18, 322, 4005, 4006, (int)BtnId.ApplyImbue);
            AddLabel(44, 324, 0x44, "Apply Imbue");
        }
        else if (!hasEss)
            AddLabel(18, 322, 0x22, $"You need an Essence of {def.Name} in your pack or bank.");
        else
            AddLabel(18, 322, 0x22, "Insufficient Essence Shards or Gold.");
    }

    // -- Stage: Disenchant View ------------------------------------------------

    private void DrawDisenchantView(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var activeProps = ImbueCatalogue.GetActiveProperties(_item);

        AddLabel(18, 60, 0x21, $"Disenchant: {_item.Name ?? _item.GetType().Name}");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var y = 82;

        if (activeProps.Count == 0)
        {
            // cc-P137: OSI refuses to unravel an item with no magic, and so does every page here (it used to pay a shard).
            AddLabel(18, y, 0x22, NoMagicToUnravel);
            return;
        }
        else
        {
            AddLabel(18, y, 1153, "Enchantments on this item:");
            y += 18;
            foreach (var (def, val) in activeProps)
            {
                var valStr = def.IsBool ? "ON" : val.ToString();
                AddLabel(24, y, 999, $"{def.Name}  [{valStr}]");
                y += 16;
            }
            y += 4;
        }

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // -- Full disenchant option --------------------------------------------
        var totalShards = 0;
        foreach (var (def, val) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);

        AddLabel(18, y, 0x21, "Option 1 - Full Disenchant (destroys item)");
        y += 18;
        AddLabel(18, y, 999,
            $"Yield: ~{totalShards} Essence Shard(s) + chance at PropertyEssence(s) for each property.");
        y += 18;
        AddLabel(18, y, 0x3B2, "WARNING: The item is permanently destroyed.");
        y += 22;
        AddButton(18, y, 4005, 4006, (int)BtnId.FullDisenchantBtn);
        AddLabel(44, y + 2, 0x21, "Proceed to Full Disenchant Confirmation");
        y += 32;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // -- Extract specific property option ----------------------------------
        AddLabel(18, y, 1154, "Option 2 - Extract a Property (item survives)");
        y += 18;

        if (activeProps.Count == 0)
        {
            AddLabel(18, y, 0x3B2, "No properties available to extract.");
        }
        else
        {
            AddLabel(18, y, 999,
                "Remove one enchantment from the item and receive a PropertyEssence for it.");
            y += 18;

            foreach (var (def, val) in activeProps)
            {
                var propIdx = ImbueCatalogue.ForItem(_item).IndexOf(def);
                if (propIdx < 0) continue;

                AddButton(18, y, 4011, 4012, (int)BtnId.ExtractPropBase + propIdx);
                AddLabel(44, y + 2, 999, $"Extract: {def.Name}  -> 1 Essence + {def.ShardBase / 4} shards");
                y += 22;
            }
        }
    }

    // -- Stage: Disenchant Confirm (full) --------------------------------------

    private void DrawDisenchantConfirm(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var activeProps = ImbueCatalogue.GetActiveProperties(_item);
        var totalShards = 0;
        foreach (var (def, _) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);

        AddLabel(18, 60, 0x21, "Full Disenchant - FINAL CONFIRMATION");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        if (activeProps.Count == 0)
        {
            AddLabel(18, 84, 0x22, NoMagicToUnravel);
            return;
        }

        var html =
            $"<BASEFONT COLOR=#FF6666>You are about to permanently destroy:<BR>" +
            $"<B>{_item.Name ?? _item.GetType().Name}</B></BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFCC44>Guaranteed yield: {totalShards} Essence Shard(s)</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AACCFF>Each enchantment has a 25% chance to also drop a PropertyEssence.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FF4444>This action CANNOT be undone. The item will be deleted.</BASEFONT>";

        AddHtml(16, 84, W - 32, 180, html, false, false);
        AddImageTiled(10, 276, W - 20, 1, 9304);

        AddButton(18, 282, 4005, 4006, (int)BtnId.ApplyDisenchant);
        AddLabel(44, 284, 0x21, "Destroy Item and Collect Yield");
    }

    // -- Stage: Unravel Container Confirm (cc-P176) ----------------------------
    //
    // OSI asks once before a container is unravelled (ServUO pub57 ImbuingGump.cs:302-336, cliloc 1112404); this page
    // also says how many items go and how many stay.

    // cc-P610 (P494 #1): exactly the items this page showed. The approval unravels these, each checked again, and nothing
    // else: an item put in the bag after the page opened, or swapped in for one, stays; one moved out stays where it went.
    private List<Item>? _unravelPlan;

    private void DrawUnravelContainerConfirm(ClusterFAccountData data, int standing)
    {
        if (_item is not Container cont || cont.Deleted) { DrawSelectItem(data, standing); return; }

        _unravelPlan = UnravelableIn(_pm, cont);
        var unravel = _unravelPlan.Count;
        var skip    = cont.Items.Count - unravel;

        AddLabel(18, 60, 0x21, "Unravel Container - FINAL CONFIRMATION");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        if (unravel == 0)
        {
            AddLabel(18, 84, 0x22, "There is no magic item in this container to unravel.");
            return;
        }

        var html =
            $"<BASEFONT COLOR=#FF6666>You are about to unravel every magic item in:<BR>" +
            $"<B>{cont.Name ?? cont.GetType().Name}</B></BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFCC44>{unravel} item(s) will be DESTROYED for their ingredients.</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AACCFF>{skip} item(s) will be skipped and stay in the container: plain, blessed, " +
            "insured and quest items, and containers inside it (only the top level is unravelled).</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FF4444>This action CANNOT be undone.</BASEFONT>";

        AddHtml(16, 84, W - 32, 180, html, false, false);
        AddImageTiled(10, 276, W - 20, 1, 9304);

        AddButton(18, 282, 4005, 4006, (int)BtnId.ApplyUnravelContainer);
        AddLabel(44, 284, 0x21, $"Unravel {unravel} Item(s)");
    }

    // -- Stage: Extract Property Confirm --------------------------------------

    private void DrawExtractConfirm(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def = itemProps[_selectedPropIdx];

        var shardGain = Math.Max(1, def.ShardBase / 4);

        AddLabel(18, 60, 1154, "Extract Property - Confirmation");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var html =
            $"<BASEFONT COLOR=#CCCCCC>Extract: <B>{def.Name}</B></BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>From: {_item.Name ?? _item.GetType().Name}</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#44CCFF>You will receive: 1 Essence of {def.Name}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#FFD700>Bonus: {shardGain} Essence Shard(s)</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FFAA44>The property is permanently removed from the item.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>The item loses 20% of its max durability from the stress of extraction.</BASEFONT>";

        AddHtml(16, 84, W - 32, 180, html, false, false);
        AddImageTiled(10, 276, W - 20, 1, 9304);

        AddButton(18, 282, 4005, 4006, (int)BtnId.ApplyExtract);
        AddLabel(44, 284, 1154, "Confirm Extraction");
    }

    // -- Stage: Craft Essence Select -------------------------------------------

    private void DrawCraftSelect(ClusterFAccountData data, int standing)
    {
        AddLabel(18, 60, 0x44, "Craft PropertyEssence - Mastered Properties");
        AddImageTiled(10, 78, W - 20, 1, 9304);
        AddLabel(18, 82, 999,
            "Select a mastered property to craft one PropertyEssence (tradeable to other Artificers).");
        AddImageTiled(10, 100, W - 20, 1, 9304);

        var y        = 104;
        var rowH     = 22;
        var rowsMax  = (H - y - 60) / rowH;
        var startIdx = _page * rowsMax;
        var pageIdx  = 0;
        var rowIdx   = 0;

        foreach (var def in ImbueCatalogue.All)
        {
            if (def.IsCustom || !data.IsMastered(def.Name, def.DiscoveryThreshold)) continue;

            if (pageIdx < startIdx) { pageIdx++; continue; }
            if (rowIdx  >= rowsMax) break;

            var allIdx = ImbueCatalogue.All.IndexOf(def);
            AddButton(18, y, 4011, 4012, (int)BtnId.CraftPropBase + allIdx);
            AddLabel(44, y + 2, 999,
                $"{def.Name}  - {def.CraftShards} Shard(s) + {def.CraftGold:N0} gold");

            y += rowH;
            rowIdx++;
            pageIdx++;
        }

        if (rowIdx == 0)
            AddLabel(18, y, 0x3B2, "(No mastered properties on this page.)");

        var footY = H - 56;
        if (_page > 0)
        {
            AddButton(18, footY, 4014, 4015, (int)BtnId.PrevPage);
            AddLabel(40, footY + 2, 999, "Prev");
        }
        // A rough check whether there are more mastered props beyond current page
        if (pageIdx >= startIdx + rowsMax)
        {
            AddButton(120, footY, 4005, 4006, (int)BtnId.NextPage);
            AddLabel(142, footY + 2, 999, "Next");
        }
    }

    // -- Stage: Craft Essence Confirm ------------------------------------------

    private void DrawCraftConfirm(ClusterFAccountData data, int standing)
    {
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        if (_selectedPropIdx < 0 || _selectedPropIdx >= ImbueCatalogue.All.Count)
        {
            DrawSelectItem(data, standing); return;
        }

        var def      = ImbueCatalogue.All[_selectedPropIdx];
        var pmShards = guild.GetCurrency("artificers");
        var pmGold   = CompactGoldHelper.GetTotalGold(_pm);
        var canAfford = pmShards >= def.CraftShards && pmGold >= def.CraftGold;

        AddLabel(18, 60, 0x44, "Craft PropertyEssence - Confirmation");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var html =
            $"<BASEFONT COLOR=#CCCCCC>Crafting: <B>Essence of {def.Name}</B></BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFD700>Essence Shards: {def.CraftShards}  (you have: {pmShards})</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#FFD700>Gold: {def.CraftGold:N0}  (you have: {pmGold:N0})</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The resulting PropertyEssence can be sold or traded to other " +
            "Artificers' members who have not yet mastered this property.</BASEFONT>";

        AddHtml(16, 84, W - 32, 160, html, false, false);
        AddImageTiled(10, 256, W - 20, 1, 9304);

        if (canAfford)
        {
            AddButton(18, 262, 4005, 4006, (int)BtnId.ApplyCraft);
            AddLabel(44, 264, 0x44, "Craft Essence");
        }
        else
            AddLabel(18, 262, 0x22, "Insufficient Essence Shards or Gold.");
    }

    // -- Button IDs ------------------------------------------------------------

    internal const string NoMagicToUnravel = "There is no magic in this to unravel.";

    private enum BtnId
    {
        ImbueTarget        = 1,
        Back               = 2,
        PrevPage           = 3,
        NextPage           = 4,
        ApplyImbue         = 5,
        DisenchantTarget   = 6,
        FullDisenchantBtn  = 7,
        ApplyDisenchant    = 8,
        ApplyExtract       = 9,
        CraftBtn           = 10,
        ApplyCraft         = 11,
        UnravelContainerTarget = 12, // cc-P176
        ApplyUnravelContainer  = 13, // cc-P176
        PropBase           = 100,    // + propIdx in ForItem list (imbue)
        ExtractPropBase    = 200,    // + propIdx in ForItem list (extract)
        CraftPropBase      = 300,    // + propIdx in All list (craft)
        ConfirmBase        = 1000,   // + tier 1-5 (imbue tier confirm)
    }

    // -- Response --------------------------------------------------------------

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        var acct     = _pm.Account as IAccount;
        var data     = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        var standing = guild.GetReputation("artificers");
        var isMember = guild.JoinedGuilds.Contains("artificers");

        switch (info.ButtonID)
        {
            case (int)BtnId.Back:
                HandleBack();
                return;

            case (int)BtnId.ImbueTarget:
                _pm.Target = new ImbueItemTarget(_pm, targetForDisenchant: false);
                return;

            case (int)BtnId.DisenchantTarget:
                _pm.Target = new ImbueItemTarget(_pm, targetForDisenchant: true);
                return;

            case (int)BtnId.UnravelContainerTarget:
                _pm.SendMessage(0x3B2, "Target a container in your backpack to unravel the magic items in it.");
                _pm.Target = new UnravelContainerTarget(_pm);
                return;

            case (int)BtnId.ApplyUnravelContainer:
                HandleUnravelContainer();
                return;

            case (int)BtnId.CraftBtn:
                _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, 0));
                return;

            case (int)BtnId.PrevPage:
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, _stage, _selectedPropIdx, 0, Math.Max(0, _page - 1)));
                return;

            case (int)BtnId.NextPage:
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, _stage, _selectedPropIdx, 0, _page + 1));
                return;

            case (int)BtnId.ApplyImbue:
                HandleApplyImbue(data, standing, isMember);
                return;

            case (int)BtnId.FullDisenchantBtn:
                if (_item != null && !_item.Deleted && ImbueCatalogue.GetActiveProperties(_item).Count == 0)
                {
                    _pm.SendMessage(0x22, NoMagicToUnravel);
                    _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0));
                }
                else if (_item != null && !_item.Deleted)
                    _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantConfirm, -1, 0, 0));
                return;

            case (int)BtnId.ApplyDisenchant:
                HandleFullDisenchant(data, standing);
                return;

            case (int)BtnId.ApplyExtract:
                HandleExtractProperty(data, standing);
                return;

            case (int)BtnId.ApplyCraft:
                HandleCraftEssence(data, standing);
                return;
        }

        // Imbue property row click (ViewItem -> SelectProp)
        if (info.ButtonID >= (int)BtnId.PropBase && info.ButtonID < (int)BtnId.ExtractPropBase)
        {
            var propIdx = info.ButtonID - (int)BtnId.PropBase;
            if (_item != null && !_item.Deleted)
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.SelectProp, propIdx, 0, _page));
            return;
        }

        // Extract property row click (DisenchantView -> ExtractConfirm)
        if (info.ButtonID >= (int)BtnId.ExtractPropBase && info.ButtonID < (int)BtnId.CraftPropBase)
        {
            var propIdx = info.ButtonID - (int)BtnId.ExtractPropBase;
            if (_item != null && !_item.Deleted)
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ExtractConfirm, propIdx, 0, 0));
            return;
        }

        // Craft essence row click (CraftSelect -> CraftConfirm)
        if (info.ButtonID >= (int)BtnId.CraftPropBase && info.ButtonID < (int)BtnId.ConfirmBase)
        {
            var allIdx = info.ButtonID - (int)BtnId.CraftPropBase;
            if (allIdx >= 0 && allIdx < ImbueCatalogue.All.Count)
                _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftConfirm, allIdx, 0, 0));
            return;
        }

        // Tier selected (SelectProp -> Confirm)
        if (info.ButtonID >= (int)BtnId.ConfirmBase && info.ButtonID < (int)BtnId.ConfirmBase + 10)
        {
            var tier = info.ButtonID - (int)BtnId.ConfirmBase;
            if (_item == null || _item.Deleted || _selectedPropIdx < 0) return;

            var itemProps = ImbueCatalogue.ForItem(_item);
            if (_selectedPropIdx >= itemProps.Count) return;

            // cc-P82: tier 1 to 4 is Basic, Refined, Full, Overcharged; the value is that tier's. Whether the account
            // knows it and the place allows it is checked again at Apply.
            var def    = itemProps[_selectedPropIdx];
            var target = tier is >= 1 and <= 4 ? ImbueTiers.MaxFor(def, (ImbueTier)tier) : 0;

            if (def.IsBool && tier >= 1) target = 1;

            if (target > 0)
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.Confirm, _selectedPropIdx, target, _page));
            return;
        }
    }

    private void HandleBack()
    {
        _pm.SendGump(_stage switch
        {
            Stage.ViewItem          => new ArtificersImbueGump(_pm),
            Stage.SelectProp        => new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page),
            Stage.Confirm           => new ArtificersImbueGump(_pm, _item, Stage.SelectProp, _selectedPropIdx, 0, _page),
            Stage.DisenchantView    => new ArtificersImbueGump(_pm),
            Stage.DisenchantConfirm => new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0),
            Stage.ExtractConfirm    => new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0),
            Stage.CraftSelect       => new ArtificersImbueGump(_pm),
            Stage.CraftConfirm      => new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page),
            Stage.UnravelContainerConfirm => new ArtificersImbueGump(_pm),
            _                       => new ArtificersImbueGump(_pm)
        });
    }

    // -- Apply imbue logic -----------------------------------------------------

    private void HandleApplyImbue(ClusterFAccountData data, int standing, bool isMember)
    {
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        if (!_pm.CheckAlive()) return;
        if (_item == null || _item.Deleted) { _pm.SendMessage(0x22, "The item is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx < 0 || _selectedPropIdx >= itemProps.Count)
        { _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page)); return; }

        var def    = itemProps[_selectedPropIdx];
        var cur    = def.Get(_item);
        var site   = ImbueSites.Resolve(_pm);
        var shards = def.ShardsFor(_targetValue);
        var gold   = def.GoldFor(_targetValue);
        var diff   = EffectiveDifficulty(def, _targetValue, site.BonusPercent);

        // cc-P82: the account must know the property at the target's tier, and the place must allow that tier. (The
        // cc-P42 Master Artificer gate on Self Repair and the Journeyman gate on enchanted items are gone: imbuing is
        // gated by property and intensity, not by item.)
        var need  = ImbueTiers.TierOf(def, _targetValue);
        var known = ClusterFImbueKnowledge.KnownTier(data, def.KnowledgeKey);
        if (_targetValue <= 0 || _targetValue > def.TopValue || known < need)
        {
            _pm.SendMessage(0x22, known < ImbueTier.Basic
                ? $"Your account does not know {def.Name}."
                : $"Your account knows {def.Name} only up to {known}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        if (need > site.Cap)
        {
            _pm.SendMessage(0x22, need == ImbueTier.Overcharged
                ? "Overcharged imbuing needs a soul forge: the Queen's Soulforge or an Artificers' hall forge."
                : $"Here you can imbue only up to {site.Cap}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        if (def.BlockedBy?.Invoke(_item) is { } why)
        {
            _pm.SendMessage(0x22, $"You cannot: {why}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Slot limit (rank 5's Master Formulary raises it), or in budget mode the item's intensity budget.
        var maxSlots  = ClusterFImbueKnowledge.MaxSlots(data);
        var usedSlots = ImbueCatalogue.CountActiveProperties(_item);
        if (cur == 0 && usedSlots >= maxSlots)
        {
            _pm.SendMessage(0x22, $"This item has reached your maximum of {maxSlots} imbued properties.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        var budget = ClusterFImbueKnowledge.IntensityBudget(data);
        if (budget > 0 && ImbueCatalogue.TotalIntensity(_item) - def.IntensityPercent(cur) + def.IntensityPercent(_targetValue) > budget)
        {
            _pm.SendMessage(0x22, $"That would take the item past your intensity budget of {budget}%.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // A custom property's ingredient (pack first, then bank: GuildResources).
        var ingredient = ImbueCatalogue.CustomIngredient(def);
        if (ingredient != null && !GuildResources.Has(_pm, ingredient))
        {
            _pm.SendMessage(0x22, $"You need {DescribeIngredient(def)} in your backpack or bank to imbue {def.Name}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Discovery / essence check (pack first, then bank: GuildResources)
        var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
        var essence   = mastered ? null : EssenceCost(def.Name);
        if (essence != null)
        {
            if (!GuildResources.Has(_pm, essence))
            {
                _pm.SendMessage(0x22, $"You need an Essence of {def.Name} in your backpack or bank to imbue this property. Obtain one by extracting it from a magic item.");
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
                return;
            }
        }

        // Resource check
        var pmShards = guild.GetCurrency("artificers");
        var pmGold   = CompactGoldHelper.GetTotalGold(_pm);
        if (pmShards < shards)
        {
            _pm.SendMessage(0x22, $"You need {shards} Essence Shard(s) but only have {pmShards}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.Confirm, _selectedPropIdx, _targetValue, _page));
            return;
        }
        if (pmGold < gold)
        {
            _pm.SendMessage(0x22, $"You need {gold:N0} gold but only have {pmGold:N0}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.Confirm, _selectedPropIdx, _targetValue, _page));
            return;
        }

        // Skill check
        var success = _pm.CheckSkill(SkillName.Imbuing, diff - 20.0, diff + 20.0);
        if (!success)
        {
            _pm.PlaySound(0x5C3);
            _pm.SendMessage(0x22, "Your hands falter - the imbue fails. No resources were consumed.");
            if (_item is BaseWeapon bwF && bwF.MaxHitPoints > 10) bwF.HitPoints = Math.Max(1, bwF.HitPoints - 10);
            else if (_item is BaseArmor baF && baF.MaxHitPoints > 10) baF.HitPoints = Math.Max(1, baF.HitPoints - 10);
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Success - consume resources. The essence first, so that if it cannot be taken nothing else is.
        if (essence != null && !GuildResources.TryConsume(_pm, essence))
        {
            _pm.SendMessage(0x22, $"You need an Essence of {def.Name} in your backpack or bank to imbue this property.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        if (ingredient != null && !GuildResources.TryConsume(_pm, ingredient))
        {
            _pm.SendMessage(0x22, $"You need {DescribeIngredient(def)} in your backpack or bank to imbue {def.Name}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        guild.SpendCurrency("artificers", shards);
        CompactGoldHelper.ConsumeGold(_pm, gold);

        // Essence taken (not mastered): count the use toward mastery
        if (essence != null)
        {
            data.IncrementDiscovery(def.Name);
            var newCount = data.GetDiscoveryCount(def.Name);
            if (newCount >= def.DiscoveryThreshold)
                _pm.SendMessage(0x44, $"You have mastered {def.Name}! You no longer need an essence to imbue it, and can now craft essences to sell.");
            else
                _pm.SendMessage(1154, $"Discovery progress: {newCount}/{def.DiscoveryThreshold} uses toward mastering {def.Name}.");
        }

        // Apply the property
        def.Set(_item, _targetValue);
        _item.InvalidateProperties();
        ClusterFAccessories.OnPropertiesChanged(_item); // cc-P610 (P486 F07): a jewel in an extra slot, at once

        var repGain = Math.Max(1, shards / 2);
        guild.AddReputation("artificers", repGain);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, $"You successfully imbue {_item.Name ?? _item.GetType().Name} with {def.Name} = {_targetValue}! (+{repGain} Artificers' Standing)");
        _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
    }

    // -- Full disenchant logic -------------------------------------------------

    private void HandleFullDisenchant(ClusterFAccountData data, int standing)
    {
        if (!_pm.CheckAlive()) return;
        if (_item == null || _item.Deleted) { _pm.SendMessage(0x22, "The item is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }
        if (_item.RootParent != _pm) { _pm.SendMessage(0x22, "You must have the item in your possession."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        // cc-P137: an item with no magic is refused, never paid (it paid 1 shard, so vendor stock farmed shards).
        // cc-P176: and OSI's other refusals (CanUnravel), the same ones Unravel Container skips on.
        var refusal = CanUnravel(_pm, _item, fromContainer: false);
        if (refusal != null)
        {
            _pm.SendMessage(0x22, refusal);
            _pm.SendGump(new ArtificersImbueGump(_pm));
            return;
        }

        var itemName = _item.Name ?? _item.GetType().Name;
        var yield = UnravelOne(_pm, _item);

        var msg = $"You disenchant the {itemName}, recovering {yield.Shards} Essence Shard(s)";
        if (yield.Essences.Count > 0) msg += $" and {yield.Essences.Count} PropertyEssence(s)";
        msg += "!";

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, msg);
        _pm.SendGump(new ArtificersImbueGump(_pm));
    }

    // -- One unravel (cc-P176) ---------------------------------------------------
    //
    // The one code path that unravels an item: Full Disenchant uses it for one item and Unravel Container for each item
    // it takes, so each gets the same yield, the same skill check and the same gain.

    internal const string BlessedRefusal = "You cannot unravel the magic of a blessed item.";          // OSI 1080421
    internal const string NotInPackRefusal = "The item must be in your backpack to magically unravel it."; // OSI 1080424
    internal const string CannotUnravel = "You cannot magically unravel this item.";                  // OSI 1080425
    internal const string FactionRefusal = "You cannot magically unravel a faction reward item.";     // OSI 1112408
    internal const string QuestRefusal = "You cannot magically unravel a quest item.";
    internal const string InsuredSkip = "An insured item is never unravelled from a container.";
    internal const string LockedRefusal = "That container is locked.";

    /// <summary>
    /// cc-P610 (P494 #4): a locked container (ILockable, as GuildResources.IsClosedToGuild reads it) anywhere between the
    /// item and the backpack. Checked at the approval, so a chest locked after the page opened still protects its bags.
    /// </summary>
    internal static bool LockedOnTheWay(Item item, Container pack)
    {
        for (var p = item.Parent as Item; p != null && p != pack; p = p.Parent as Item)
        {
            if (p is ILockable { Locked: true })
            {
                return true;
            }
        }

        return false;
    }

    internal readonly record struct UnravelYield(int Shards, List<string> Essences);

    /// <summary>
    /// Why this item cannot be unravelled, or null when it can. OSI's rules (ServUO pub57 Imbuing.cs:134-176) where
    /// pinned has the system: in the backpack (so never equipped), not blessed or newbied, an imbuable kind, not a
    /// faction item; and it must hold magic (Imbuing.cs:749-758, cc-P137). Ours: never a quest item, and from a
    /// container never an insured one (a bulk unravel should not take what the player chose to keep). Pinned has no
    /// Enchant spell or focus attack, so ServUO's two refusals for those do not apply.
    /// </summary>
    internal static string? CanUnravel(PlayerMobile pm, Item item, bool fromContainer)
    {
        if (item.Deleted || !ImbueCatalogue.IsImbuable(item)) return CannotUnravel;
        if (pm.Backpack == null || !item.IsChildOf(pm.Backpack)) return NotInPackRefusal;
        if (LockedOnTheWay(item, pm.Backpack)) return LockedRefusal;  // cc-P610 (P494 #4)
        if (item.LootType is LootType.Blessed or LootType.Newbied) return BlessedRefusal;
        if (item.BlessedFor != null) return BlessedRefusal;           // cc-P610 (P494 #2): a personal blessing too
        if (item is Server.Factions.IFactionItem { FactionItemState: not null }) return FactionRefusal;
        if (item.QuestItem) return QuestRefusal;
        if (fromContainer && item.Insured) return InsuredSkip;
        if (ImbueCatalogue.GetActiveProperties(item).Count == 0) return NoMagicToUnravel;
        return null;
    }

    /// <summary>Unravels an item CanUnravel passed: shards, a 25% essence chance per property, one skill check.</summary>
    internal static UnravelYield UnravelOne(PlayerMobile pm, Item item)
    {
        var activeProps = ImbueCatalogue.GetActiveProperties(item);
        var guild = (pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData())
            .GetOrCreateGuildData(pm.Serial);

        var totalShards = 0;

        foreach (var (def, _) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);

        // Shards to player
        guild.AddCurrency("artificers", totalShards);

        // Chance at PropertyEssence per property
        var essences = new List<string>();
        foreach (var (def, _) in activeProps)
        {
            if (Utility.Random(4) == 0) // 25% chance
            {
                var ess = new PropertyEssence(def.Name);
                ArtificersSatchel.Deliver(pm, ess); // cc-P137 (F-50): the satchel first, else the backpack
                essences.Add(def.Name);
            }
        }

        // Skill gain from disenchanting
        pm.CheckSkill(SkillName.Imbuing, 20.0, 80.0);

        item.Delete();
        return new UnravelYield(totalShards, essences);
    }

    /// <summary>The top-level items of a container that would be unravelled (ServUO pub57 ImbuingGump.cs:272, 348).</summary>
    internal static List<Item> UnravelableIn(PlayerMobile pm, Container cont) =>
        cont.Items.Where(i => CanUnravel(pm, i, fromContainer: true) == null).ToList();

    // -- Unravel Container logic (cc-P176) ----------------------------------------

    private void HandleUnravelContainer()
    {
        if (!_pm.CheckAlive()) return;
        if (_item is not Container cont || cont.Deleted) { _pm.SendMessage(0x22, "The container is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        var refusal = ContainerRefusal(_pm, cont);
        if (refusal != null)
        {
            _pm.SendMessage(0x22, refusal);
            _pm.SendGump(new ArtificersImbueGump(_pm));
            return;
        }

        UnravelContainer(_pm, cont, _unravelPlan ?? []);
        _pm.SendGump(new ArtificersImbueGump(_pm));
    }

    /// <summary>Why this container cannot be unravelled, or null (ServUO pub57 ImbuingGump.cs:260-268).</summary>
    internal static string? ContainerRefusal(PlayerMobile pm, Container cont)
    {
        if (pm.Backpack == null || !cont.IsChildOf(pm.Backpack))
            return "This item must be in your backpack to be used."; // OSI 1062334
        if (cont is LockableContainer { Locked: true } || LockedOnTheWay(cont, pm.Backpack))  // cc-P610: and its ancestors
            return "That container is locked. Unraveled: 0/0 items";  // OSI sends 1111814 "0\t0"
        return null;
    }

    /// <summary>
    /// Unravels every top-level item CanUnravel passes, each through UnravelOne, and sends the summary. Everything
    /// else stays in the container with no skill check. Returns the yields.
    /// </summary>
    internal static List<UnravelYield> UnravelContainer(PlayerMobile pm, Container cont) =>
        UnravelContainer(pm, cont, UnravelableIn(pm, cont));

    /// <summary>cc-P610 (P494 #1): unravels the approved items only, each still in this container and still passing
    /// CanUnravel at the approval. The summary counts the container's top level as it is now.</summary>
    internal static List<UnravelYield> UnravelContainer(PlayerMobile pm, Container cont, IReadOnlyList<Item> approved)
    {
        var all = cont.Items.ToList(); // a snapshot: unravelling deletes from cont.Items
        var yields = new List<UnravelYield>();

        foreach (var item in approved)
            if (!item.Deleted && item.Parent == cont && CanUnravel(pm, item, fromContainer: true) == null)
                yields.Add(UnravelOne(pm, item));

        var shards = yields.Sum(y => y.Shards);
        var essences = yields.SelectMany(y => y.Essences)
            .GroupBy(n => n)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Count()} Essence of {g.Key}")
            .ToList();

        if (yields.Count > 0)
        {
            pm.PlaySound(0x1F5);
            pm.SendMessage(0x44, "You magically unravel the items!"); // OSI 1080429, plural
        }

        pm.SendMessage(0x44, $"Unraveled: {yields.Count}/{all.Count} items"); // OSI 1111814
        pm.SendMessage(0x44, $"Unraveled {yields.Count} item(s), skipped {all.Count - yields.Count}.");
        if (yields.Count > 0)
        {
            var got = $"Ingredients: {shards} Essence Shard(s)";
            if (essences.Count > 0) got += ", " + string.Join(", ", essences);
            pm.SendMessage(0x44, got + ".");
        }

        return yields;
    }

    // -- Extract property logic ------------------------------------------------

    private void HandleExtractProperty(ClusterFAccountData data, int standing)
    {
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        if (!_pm.CheckAlive()) return;
        if (_item == null || _item.Deleted) { _pm.SendMessage(0x22, "The item is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }
        if (_item.RootParent != _pm) { _pm.SendMessage(0x22, "You must have the item in your possession."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx < 0 || _selectedPropIdx >= itemProps.Count)
        { _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0)); return; }

        var def = itemProps[_selectedPropIdx];
        var cur = def.Get(_item);

        if (cur == 0)
        {
            _pm.SendMessage(0x22, $"{def.Name} is not present on this item.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0));
            return;
        }

        // Remove property
        def.Set(_item, 0);
        _item.InvalidateProperties();
        ClusterFAccessories.OnPropertiesChanged(_item); // cc-P610 (P486 F07)

        // Durability hit (20% of max HP). The current value comes down with the max (cc-P29, D48): the HitPoints
        // setters clamp only what is assigned to them, so a lowered max alone left e.g. 89 / 58.
        if (_item is BaseWeapon bw)
        {
            bw.MaxHitPoints = Math.Max(1, bw.MaxHitPoints - bw.MaxHitPoints / 5);
            bw.HitPoints = Math.Min(bw.HitPoints, bw.MaxHitPoints);
        }
        else if (_item is BaseArmor ba)
        {
            ba.MaxHitPoints = Math.Max(1, ba.MaxHitPoints - ba.MaxHitPoints / 5);
            ba.HitPoints = Math.Min(ba.HitPoints, ba.MaxHitPoints);
        }

        // Yield: essence + bonus shards
        var ess = new PropertyEssence(def.Name);
        ArtificersSatchel.Deliver(_pm, ess); // cc-P137 (F-50): the satchel first, else the backpack

        var shardGain = Math.Max(1, def.ShardBase / 4);
        guild.AddCurrency("artificers", shardGain);

        // Skill gain
        _pm.CheckSkill(SkillName.Imbuing, 20.0, 80.0);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44,
            $"You extract {def.Name} from {_item.Name ?? _item.GetType().Name}, " +
            $"receiving an Essence of {def.Name} and {shardGain} Essence Shard(s).");

        _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0));
    }

    // -- Craft essence logic ---------------------------------------------------

    private void HandleCraftEssence(ClusterFAccountData data, int standing)
    {
        var guild = data.GetOrCreateGuildData(_pm.Serial);
        if (!_pm.CheckAlive()) return;
        if (_selectedPropIdx < 0 || _selectedPropIdx >= ImbueCatalogue.All.Count)
        { _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page)); return; }

        var def = ImbueCatalogue.All[_selectedPropIdx];

        if (def.IsCustom || !data.IsMastered(def.Name, def.DiscoveryThreshold))
        {
            _pm.SendMessage(0x22, $"You have not mastered {def.Name} yet.");
            _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page));
            return;
        }

        var pmShards = guild.GetCurrency("artificers");
        var pmGold   = CompactGoldHelper.GetTotalGold(_pm);

        if (pmShards < def.CraftShards)
        {
            _pm.SendMessage(0x22, $"You need {def.CraftShards} Essence Shard(s) to craft this essence.");
            _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftConfirm, _selectedPropIdx, 0, 0));
            return;
        }
        if (pmGold < def.CraftGold)
        {
            _pm.SendMessage(0x22, $"You need {def.CraftGold:N0} gold to craft this essence.");
            _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftConfirm, _selectedPropIdx, 0, 0));
            return;
        }

        guild.SpendCurrency("artificers", def.CraftShards);
        CompactGoldHelper.ConsumeGold(_pm, def.CraftGold);

        var ess = new PropertyEssence(def.Name);
        _pm.AddToBackpack(ess);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, $"You craft an Essence of {def.Name}!");
        _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page));
    }

    // -- Target inner class ----------------------------------------------------

    private sealed class ImbueItemTarget : Target
    {
        private readonly PlayerMobile _pm;
        private readonly bool         _disenchant;

        public ImbueItemTarget(PlayerMobile pm, bool targetForDisenchant) : base(2, false, TargetFlags.None)
        {
            _pm         = pm;
            _disenchant = targetForDisenchant;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Item item)
            {
                from.SendMessage(0x22, "That is not an item.");
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            if (item.Deleted || item.RootParent != from)
            {
                from.SendMessage(0x22, "You must have the item in your possession.");
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            if (!ImbueCatalogue.IsImbuable(item))
            {
                from.SendMessage(0x22, "Only weapons, armour, jewellery, and clothing can be imbued or disenchanted.");
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            if (_disenchant && ImbueCatalogue.GetActiveProperties(item).Count == 0)
            {
                from.SendMessage(0x22, NoMagicToUnravel);
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            var nextStage = _disenchant ? Stage.DisenchantView : Stage.ViewItem;
            from.SendGump(new ArtificersImbueGump(_pm, item, nextStage, -1, 0, 0));
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendGump(new ArtificersImbueGump(_pm));
        }
    }

    // cc-P176: ServUO pub57 ImbuingGump.cs:246-290. Not a container: nothing; outside the backpack or locked: refused;
    // nothing to unravel: the summary at once (OSI's TryUnravelContainer); else the confirm page.
    private sealed class UnravelContainerTarget : Target
    {
        private readonly PlayerMobile _pm;

        public UnravelContainerTarget(PlayerMobile pm) : base(-1, false, TargetFlags.None) => _pm = pm;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Container cont || cont.Deleted)
            {
                from.SendMessage(0x22, "That is not a container.");
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            var refusal = ContainerRefusal(_pm, cont);
            if (refusal != null)
            {
                from.SendMessage(0x22, refusal);
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            if (UnravelableIn(_pm, cont).Count == 0)
            {
                UnravelContainer(_pm, cont); // unravels nothing: the 0/N summary, as OSI sends
                from.SendGump(new ArtificersImbueGump(_pm));
                return;
            }

            from.SendGump(new ArtificersImbueGump(_pm, cont, Stage.UnravelContainerConfirm, -1, 0, 0));
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendGump(new ArtificersImbueGump(_pm));
        }
    }

    // -- Essence helpers -------------------------------------------------------
    //
    // cc-P27 (Chase, 2026-10-01): essences come from the pack first, then the bank, through GuildResources (F-11),
    // so the order and the exclusions are those of every other guild cost: pack loose, pack satchels (the
    // Artificers' Essence Satchel among them), bank loose, bank satchels; never equipped, blessed or locked away.
    // Before this the imbue path read only the top level of the backpack, so an essence kept in the Essence
    // Satchel, or in any bag, could not be used to imbue.

    /// <summary>The one essence an unmastered imbue of this property takes.</summary>
    public static GuildCost EssenceCost(string propertyKey) =>
        GuildCost.Where(
            item => item is PropertyEssence ess && ess.PropertyKey.Equals(propertyKey, StringComparison.OrdinalIgnoreCase),
            1, $"Essence of {propertyKey}");

    public static GuildStock CountEssences(PlayerMobile pm, string propertyKey) =>
        GuildResources.Count(pm, EssenceCost(propertyKey));

    /// <summary>"3 essences of Luck: 1 in pack, 2 in bank". Without the name: "3 essences: 1 in pack, 2 in bank".</summary>
    public static string DescribeEssences(PlayerMobile pm, string propertyKey, bool withName = true)
    {
        var stock = CountEssences(pm, propertyKey);
        var noun  = stock.Total == 1 ? "essence" : "essences";
        var what  = withName ? $"{noun} of {propertyKey}" : noun;
        return $"{stock.Total} {what}: {stock.Pack} in pack, {stock.Bank} in bank";
    }

    private int CountEssencesFor(string propertyKey) => CountEssences(_pm, propertyKey).Total;

    // -- Public static helpers (used by ArtificersGuildmasterGump) ------------

    /// <summary>
    /// Returns the success chance (0-100%) for an imbue attempt.
    /// Mirrors CheckSkill(diff-20, diff+20): 0% at skill=diff-20, 100% at skill=diff+20.
    /// </summary>
    public static int ComputeSuccessChance(double skill, double diff)
    {
        var chance = (skill - (diff - 20.0)) / 40.0 * 100.0;
        return (int)Math.Clamp(chance, 0.0, 100.0);
    }
}
