using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server;

// ── Imbue property categories ─────────────────────────────────────────────────

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

// ── Property definition ───────────────────────────────────────────────────────

internal sealed class ImbuePropertyDef
{
    public string       Name;         // Display name (also used as discovery key)
    public string       Group;        // "General", "Weapon", "Defense", "Skills", "Slayer"
    public int          MaxVanilla;   // Cap at 100% guild rank
    public int          MaxGuild;     // Cap at 200% guild rank (highest tier)
    public int          ShardBase;    // Shards at vanilla max
    public int          GoldBase;     // Gold at vanilla max
    public bool         IsBool;       // 1/0 on/off property
    public ImbuableItem Applies;

    /// <summary>
    /// How many times a player must successfully imbue this property (using an essence)
    /// before they "master" it and no longer need an essence.
    /// Computed from GoldBase by ImbueCatalogue.ComputeThreshold.
    /// </summary>
    public int DiscoveryThreshold;

    // Accessors
    public Func<Item, int>    Get;
    public Action<Item, int>  Set;

    /// <summary>Returns how many property slots this property currently consumes.</summary>
    public int CountSlots(Item item) => Get(item) != 0 ? 1 : 0;

    /// <summary>Returns the allowed max based on standing (guild intensity cap).</summary>
    public int GetMaxForStanding(int standing)
    {
        if (IsBool) return 1;
        var pct = ArtificersImbueGump.GetIntensityCap(standing);
        var raw = MaxVanilla + (MaxGuild - MaxVanilla) * Math.Max(0, pct - 100) / 100;
        return Math.Clamp(raw, MaxVanilla, MaxGuild);
    }

    /// <summary>Returns Essence Shard cost to set this property to targetValue.</summary>
    public int ShardsFor(int targetValue, int standing)
    {
        if (IsBool) return ShardBase;
        var maxAllowed = GetMaxForStanding(standing);
        var fraction   = maxAllowed > 0 ? (float)targetValue / maxAllowed : 1f;
        var pct  = ArtificersImbueGump.GetIntensityCap(standing);
        var mult = 1.0f + Math.Max(0, pct - 100) / 100.0f;
        return Math.Max(1, (int)(ShardBase * fraction * mult));
    }

    /// <summary>Returns gold cost to set this property to targetValue.</summary>
    public int GoldFor(int targetValue, int standing)
    {
        if (IsBool) return GoldBase;
        var maxAllowed = GetMaxForStanding(standing);
        var fraction   = maxAllowed > 0 ? (float)targetValue / maxAllowed : 1f;
        var pct  = ArtificersImbueGump.GetIntensityCap(standing);
        var mult = 1.0f + Math.Max(0, pct - 100) / 100.0f;
        return Math.Max(100, (int)(GoldBase * fraction * mult));
    }

    /// <summary>Success-check difficulty: 0-120 skill range.</summary>
    public double SkillDifficulty(int targetValue, int standing)
    {
        var maxAllowed = GetMaxForStanding(standing);
        var fraction   = maxAllowed > 0 ? (float)targetValue / maxAllowed : 1f;
        var pct = ArtificersImbueGump.GetIntensityCap(standing);
        var maxDiff = pct <= 100 ? 100.0 : 100.0 + (pct - 100) * 0.2;
        return 40.0 + (maxDiff - 40.0) * fraction;
    }

    /// <summary>Shard cost to craft one PropertyEssence for this property (mastered players only).</summary>
    public int CraftShards => ShardBase * 2;

    /// <summary>Gold cost to craft one PropertyEssence for this property (mastered players only).</summary>
    public int CraftGold => GoldBase / 2;
}

// ── Property catalogue ────────────────────────────────────────────────────────

internal static class ImbueCatalogue
{
    public static readonly List<ImbuePropertyDef> All;

    static ImbueCatalogue()
    {
        All = new List<ImbuePropertyDef>();

        // ── General — apply to most item types ───────────────────────────────

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

        // ── Stat bonuses ──────────────────────────────────────────────────────

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

        // ── Weapon-specific: Hit abilities ────────────────────────────────────

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

        Add("Self Repair",       "Weapon", 5, 7, shards: 5, gold: 2000, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.SelfRepair,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.SelfRepair = v);

        Add("Lower Stat Req (Weapon)", "Weapon", 100, 100, shards: 4, gold: 1500, ImbuableItem.Weapon,
            i => ((BaseWeapon)i).WeaponAttributes.LowerStatReq,
            (i, v) => ((BaseWeapon)i).WeaponAttributes.LowerStatReq = v);

        // ── Armor-specific ────────────────────────────────────────────────────

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

        Add("Self Repair (Armor)",   "Defense",  5,  7, shards: 5, gold: 2000, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.SelfRepair,
            (i, v) => ((BaseArmor)i).ArmorAttributes.SelfRepair = v);

        Add("Lower Stat Req (Armor)", "Defense", 100, 100, shards: 4, gold: 1500, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.LowerStatReq,
            (i, v) => ((BaseArmor)i).ArmorAttributes.LowerStatReq = v);

        Add("Mage Armor",            "Defense",  1,  1, shards: 6, gold: 2000, isBool: true, ImbuableItem.Armor,
            i => ((BaseArmor)i).ArmorAttributes.MageArmor,
            (i, v) => ((BaseArmor)i).ArmorAttributes.MageArmor = v);

        // ── Slayer properties (weapons only) ──────────────────────────────────
        // NOTE: SlayerName enum values — verify against your ModernUO build if
        //       you get CS0117 "SlayerName does not contain a definition for X".
        //       Common alternatives: DaemonDismissal, Exorcism, GargoylesFoe.

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
    }

    // ── Slayer helper ─────────────────────────────────────────────────────────

    private static void AddSlayer(string name, SlayerName slayer)
    {
        // Slayers are expensive to master (threshold 10) and costly to imbue.
        // They are boolean: either applied or not.
        var sn = slayer;
        All.Add(new ImbuePropertyDef
        {
            Name               = name,
            Group              = "Slayer",
            MaxVanilla         = 1,
            MaxGuild           = 1,
            ShardBase          = 15,
            GoldBase           = 6000,
            IsBool             = true,
            DiscoveryThreshold = 10,
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
                    // else both slots full — no-op (caller should check)
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

    // ── General Add helpers ───────────────────────────────────────────────────

    private static void Add(
        string name, string group, int maxVanilla, int maxGuild,
        int shards, int gold, ImbuableItem applies,
        Func<Item, int> get, Action<Item, int> set,
        bool isBool = false)
    {
        All.Add(new ImbuePropertyDef
        {
            Name               = name,
            Group              = group,
            MaxVanilla         = maxVanilla,
            MaxGuild           = maxGuild,
            ShardBase          = shards,
            GoldBase           = gold,
            IsBool             = isBool,
            DiscoveryThreshold = ComputeThreshold(gold),
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

    // ── Discovery threshold ───────────────────────────────────────────────────

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

    // ── Helper — get AosAttributes regardless of item type ───────────────────

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
            if ((def.Applies & flag) != 0)
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

// ── Main imbuing / disenchanting gump ────────────────────────────────────────

/// <summary>
/// Shattered Legacy — Artificers' Order Imbuing &amp; Disenchanting Gump.
///
/// Stages:
///   SelectItem        — entry point; choose to imbue, disenchant, or craft essence
///   ViewItem          — show item enchantments; click property to imbue/upgrade
///   SelectProp        — choose vanilla max or guild-enhanced max; confirm costs
///   Confirm           — final cost confirmation before applying imbue
///   DisenchantView    — show item enchantments + choose full disenchant or extract property
///   DisenchantConfirm — confirm destroying item (full disenchant)
///   ExtractConfirm    — confirm extracting a specific property
///   CraftSelect       — list mastered properties available to craft essences from
///   CraftConfirm      — confirm crafting a specific PropertyEssence
///
/// Discovery system:
///   All properties require a PropertyEssence until mastered.
///   Mastery threshold scales with property power (GoldBase):
///     &lt;500g → 3 uses, &lt;1500g → 5 uses, &lt;3000g → 8 uses, else 10 uses.
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
    }

    private const int W    = 530;
    private const int H    = 570;
    private const int BgId = 9270;

    // ── Per-instance state ────────────────────────────────────────────────────
    private readonly PlayerMobile _pm;
    private readonly Item?        _item;
    private readonly int          _selectedPropIdx;  // index into ForItem(_item) for imbue/extract; index into All for craft
    private readonly int          _targetValue;
    private readonly Stage        _stage;
    private readonly int          _page;

    // Public constructor — no item selected yet
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

        AddLabel(W / 2 - 95, 12, 1153, "Artificers' Order — Imbuing Table");
        AddImageTiled(10, 32, W - 20, 2, 9304);

        var acct     = pm.Account as IAccount;
        var data     = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();
        var standing = data.GetReputation("artificers");
        var shards   = data.GetCurrency("artificers");
        var isMember = data.JoinedGuilds.Contains("artificers");

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

    // ── Stage: Select Item (entry point) ─────────────────────────────────────

    private void DrawSelectItem(ClusterFAccountData data, int standing)
    {
        var y = 62;

        // ── Imbue section ─────────────────────────────────────────────────────
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

        // ── Disenchant section ────────────────────────────────────────────────
        AddLabel(18, y, 1153, "Disenchanting");
        y += 18;
        AddLabel(18, y, 999,
            "Break down a magic item into Essence Shards, or extract a specific property.");
        y += 20;
        AddButton(18, y, 4011, 4012, (int)BtnId.DisenchantTarget);
        AddLabel(44, y + 2, 0x21, "Target an item to disenchant...");
        y += 32;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // ── Craft Essence section ─────────────────────────────────────────────
        AddLabel(18, y, 1153, "Craft Essence");
        y += 18;

        // Count mastered properties
        var masteredCount = 0;
        foreach (var def in ImbueCatalogue.All)
            if (data.IsMastered(def.Name, def.DiscoveryThreshold))
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

    // ── Stage: View Item (imbue mode) ─────────────────────────────────────────

    private void DrawViewItem(ClusterFAccountData data, int standing, bool isMember)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var maxSlots  = GetMaxPropertySlots(standing);
        var itemProps = ImbueCatalogue.ForItem(_item);
        var usedSlots = ImbueCatalogue.CountActiveProperties(_item);
        var intensPct = GetIntensityCap(standing);

        AddLabel(18, 60, 1154, $"Item: {_item.Name ?? _item.GetType().Name}");
        AddLabel(18, 78, 999,  $"Properties: {usedSlots}/{maxSlots}  |  Intensity cap: {intensPct}%");

        AddLabel(18, 98, 1153, "Current Enchantments:");
        AddImageTiled(10, 114, W - 20, 1, 9304);

        var y   = 118;
        var any = false;
        foreach (var def in itemProps)
        {
            var cur = def.Get(_item);
            if (cur == 0) continue;
            var maxV   = def.GetMaxForStanding(standing);
            var pctStr = def.IsBool ? "(on)" : $"{cur}/{maxV}";
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
        var pageIdx  = 0;
        var rowIdx   = 0;

        AddLabel(18, listY - 18, 1153, "Available Properties (click to imbue/upgrade):");

        foreach (var def in itemProps)
        {
            if (pageIdx < startIdx) { pageIdx++; continue; }
            if (rowIdx  >= rowsMax) break;

            var propIdx  = itemProps.IndexOf(def);
            var cur      = def.Get(_item);
            var maxAllow = def.GetMaxForStanding(standing);
            var atMax    = def.IsBool ? (cur == 1) : (cur >= maxAllow);
            var wouldAddSlot = cur == 0;
            var noRoom   = wouldAddSlot && usedSlots >= maxSlots;
            var blocked  = cur != 0 && !isMember && standing < 1000;

            // Rank gate: property tier vs player standing
            var minStanding   = ArtificersGuildmasterGump.GetMinStanding(def.DiscoveryThreshold);
            var rankLocked    = standing < minStanding;
            var reqRankName   = ArtificersGuildmasterGump.GetRequiredRankName(def.DiscoveryThreshold);

            // Discovery status
            var mastered = data.IsMastered(def.Name, def.DiscoveryThreshold);
            var discCount = data.GetDiscoveryCount(def.Name);
            var essCount  = CountEssencesFor(def.Name);
            var needsEss  = !mastered && essCount == 0 && !atMax && !rankLocked;

            var hue = rankLocked ? 0x3B2 :
                      atMax      ? 0x3B2 :
                      noRoom     ? 0x22  :
                      blocked    ? 0x22  :
                      needsEss   ? 0x3B2 : 999;

            var btnActive = !rankLocked && !atMax && !noRoom && !blocked && !needsEss;

            if (btnActive)
            {
                var bid = (int)BtnId.PropBase + propIdx;
                AddButton(18, listY, 4011, 4012, bid);
            }

            var curStr  = def.IsBool ? (cur == 1 ? "ON" : "off") : $"{cur}";
            var maxStr  = def.IsBool ? "" : $"/ {maxAllow}";
            var discStr = mastered ? " [mastered]" :
                          needsEss ? $" [need essence {discCount}/{def.DiscoveryThreshold}]" :
                          essCount > 0 ? $" [essence ready {discCount}/{def.DiscoveryThreshold}]" : "";
            var note    = rankLocked ? $" [req: {reqRankName}]" :
                          atMax      ? " [MAX]"        :
                          noRoom     ? " [slots full]" :
                          blocked    ? " [rank req'd]" : discStr;

            AddLabel(44,  listY, hue, def.Name);
            AddLabel(300, listY, hue, $"{curStr} {maxStr}{note}");

            listY += rowH;
            rowIdx++;
            pageIdx++;
        }

        var footY = H - 56;
        if (_page > 0)
        {
            AddButton(18, footY, 4014, 4015, (int)BtnId.PrevPage);
            AddLabel(40, footY + 2, 999, "Prev");
        }
        if (startIdx + rowsMax < itemProps.Count)
        {
            AddButton(120, footY, 4005, 4006, (int)BtnId.NextPage);
            AddLabel(142, footY + 2, 999, "Next");
        }

        AddButton(W - 180, footY, 4011, 4012, (int)BtnId.ImbueTarget);
        AddLabel(W - 154,  footY + 2, 999, "Change Item");
    }

    // ── Stage: Select Target Value ────────────────────────────────────────────

    private void DrawSelectProp(ClusterFAccountData data, int standing, bool isMember)
    {
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def = itemProps[_selectedPropIdx];
        var cur = def.Get(_item);

        var mastered    = data.IsMastered(def.Name, def.DiscoveryThreshold);
        var discCount   = data.GetDiscoveryCount(def.Name);
        var essCount    = CountEssencesFor(def.Name);
        var minStanding = ArtificersGuildmasterGump.GetMinStanding(def.DiscoveryThreshold);
        var rankLocked  = standing < minStanding;
        var reqRankName = ArtificersGuildmasterGump.GetRequiredRankName(def.DiscoveryThreshold);

        AddLabel(18, 60, 1154, $"Imbue: {def.Name}");
        AddLabel(18, 80, 999,  $"Current value: {(def.IsBool ? (cur == 1 ? "ON" : "OFF") : cur.ToString())}");

        // Rank gate — show locked message and bail out of controls
        if (rankLocked)
        {
            AddLabel(18, 98,  0x22, $"Requires {reqRankName} rank ({minStanding:N0} standing).");
            AddLabel(18, 116, 0x3B2, "You have not yet earned the standing to attempt this property.");
            AddButton(18, H - 56, 4014, 4015, (int)BtnId.ImbueTarget);
            AddLabel(44, H - 54, 999, "Back");
            return;
        }

        // Discovery status bar
        if (mastered)
            AddLabel(18, 98, 0x44, $"Mastered — no essence required. (Uses: {discCount}/{def.DiscoveryThreshold})");
        else
        {
            var essHue = essCount > 0 ? 1154 : 0x22;
            AddLabel(18, 98, essHue,
                $"Discovery: {discCount}/{def.DiscoveryThreshold} uses — " +
                (essCount > 0 ? $"{essCount} essence(s) in pack." : "No essence in pack!"));
        }

        AddImageTiled(10, 114, W - 20, 1, 9304);

        if (def.IsBool)
            DrawBoolChoice(def, standing, cur);
        else
            DrawValueChoice(def, standing, cur, isMember);
    }

    private void DrawBoolChoice(ImbuePropertyDef def, int standing, int cur)
    {
        var shards = def.ShardBase;
        var gold   = def.GoldBase;
        var y      = 120;

        if (cur == 0)
        {
            AddLabel(18, y, 999, $"Enable {def.Name}");
            y += 20;
            AddLabel(18, y, 1154, $"Cost: {shards} Essence Shard(s) + {gold:N0} gold");
            y += 30;
            AddButton(18, y, 4005, 4006, (int)BtnId.ConfirmBase + 1);
            AddLabel(44, y + 2, 0x44, "Apply");
        }
        else
        {
            AddLabel(18, y, 0x3B2, $"{def.Name} is already enabled on this item.");
        }
    }

    private void DrawValueChoice(ImbuePropertyDef def, int standing, int cur, bool isMember)
    {
        var y          = 120;
        var vanillaMax = def.MaxVanilla;
        var guildMax   = def.GetMaxForStanding(standing);

        AddLabel(18, y, 1153, "Available imbue tiers:");
        y += 22;

        TryDrawTier(def, standing, cur, y, (int)(vanillaMax * 0.25f), 1); y += 50;
        TryDrawTier(def, standing, cur, y, (int)(vanillaMax * 0.50f), 2); y += 50;
        TryDrawTier(def, standing, cur, y, vanillaMax, 3); y += 50;

        if (standing >= 1000 && guildMax > vanillaMax)
        {
            var mid4 = vanillaMax + (guildMax - vanillaMax) / 2;
            if (mid4 > vanillaMax) { TryDrawTier(def, standing, cur, y, mid4, 4); y += 50; }
            TryDrawTier(def, standing, cur, y, guildMax, 5);
        }
    }

    private void TryDrawTier(ImbuePropertyDef def, int standing, int cur, int y, int target, int tier)
    {
        if (target <= 0) return;
        var isUpgrade = target > cur;
        var isAtValue = target == cur;
        var shards    = def.ShardsFor(target, standing);
        var gold      = def.GoldFor(target, standing);
        var diff      = def.SkillDifficulty(target, standing);
        var chance    = ComputeSuccessChance(_pm.Skills[SkillName.Imbuing].Value, diff);
        var chanceHue = chance >= 75 ? 0x44 : chance >= 40 ? 999 : 0x22;
        var hue       = isAtValue ? 0x3B2 : (isUpgrade ? 999 : 0x3B2);
        var note      = isAtValue ? " [current]" : (!isUpgrade ? " [downgrade]" : "");
        var label     = $"Set to {target}{note} — {shards} Shard(s) + {gold:N0} gold";

        AddLabel(28, y, hue, label);
        if (isUpgrade)
        {
            AddLabel(28, y + 16, chanceHue, $"  Success: {chance}%  (skill {_pm.Skills[SkillName.Imbuing].Value:F1} vs difficulty {diff:F0})");
            AddButton(18, y, 4005, 4006, (int)BtnId.ConfirmBase + tier);
        }
    }

    // ── Stage: Confirm Imbue ──────────────────────────────────────────────────

    private void DrawConfirm(ClusterFAccountData data, int standing, bool isMember)
    {
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def    = itemProps[_selectedPropIdx];
        var cur    = def.Get(_item);
        var shards = def.ShardsFor(_targetValue, standing);
        var gold   = def.GoldFor(_targetValue, standing);
        var diff   = def.SkillDifficulty(_targetValue, standing);

        var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
        var discCount = data.GetDiscoveryCount(def.Name);
        var essCount  = CountEssencesFor(def.Name);
        var pmShards  = data.GetCurrency("artificers");
        var pmGold    = CompactGoldHelper.GetTotalGold(_pm);
        var canAfford = pmShards >= shards && pmGold >= gold;
        var hasEss    = mastered || essCount > 0;

        AddLabel(18, 60, 1154, "Confirm Imbue");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var essLine = mastered
            ? "<BASEFONT COLOR=#44FF44>Mastered — no essence required.</BASEFONT><BR>"
            : $"<BASEFONT COLOR={(essCount > 0 ? "#AACCFF" : "#FF4444")}>Requires: 1 Essence of {def.Name} " +
              $"(you have: {essCount})  Discovery: {discCount}/{def.DiscoveryThreshold}</BASEFONT><BR>";

        var chance     = ComputeSuccessChance(_pm.Skills[SkillName.Imbuing].Value, diff);
        var chanceColor = chance >= 75 ? "#44FF44" : chance >= 40 ? "#FFD700" : "#FF4444";

        var html =
            $"<BASEFONT COLOR=#CCCCCC>Property: {def.Name}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Current value: {cur}  →  Target: {_targetValue}</BASEFONT><BR>" +
            $"{essLine}" +
            $"<BASEFONT COLOR=#FFD700>Essence Shards: {shards}  (you have: {pmShards})</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#FFD700>Gold: {gold:N0}  (you have: {pmGold:N0})</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Difficulty: {diff:F0}  |  Your Imbuing: {_pm.Skills[SkillName.Imbuing].Value:F1}</BASEFONT><BR>" +
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
            AddLabel(18, 322, 0x22, $"You need an Essence of {def.Name} to imbue this property.");
        else
            AddLabel(18, 322, 0x22, "Insufficient Essence Shards or Gold.");
    }

    // ── Stage: Disenchant View ────────────────────────────────────────────────

    private void DrawDisenchantView(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var activeProps = ImbueCatalogue.GetActiveProperties(_item);

        AddLabel(18, 60, 0x21, $"Disenchant: {_item.Name ?? _item.GetType().Name}");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var y = 82;

        if (activeProps.Count == 0)
        {
            AddLabel(18, y, 0x3B2, "This item has no imbued properties to extract or disenchant.");
            y += 20;
            AddLabel(18, y, 0x3B2, "Full disenchanting an unenchanted item yields minimal shards.");
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

        // ── Full disenchant option ────────────────────────────────────────────
        var totalShards = 0;
        foreach (var (def, val) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);

        AddLabel(18, y, 0x21, "Option 1 — Full Disenchant (destroys item)");
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

        // ── Extract specific property option ──────────────────────────────────
        AddLabel(18, y, 1154, "Option 2 — Extract a Property (item survives)");
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
                AddLabel(44, y + 2, 999, $"Extract: {def.Name}  → 1 Essence + {def.ShardBase / 4} shards");
                y += 22;
            }
        }
    }

    // ── Stage: Disenchant Confirm (full) ──────────────────────────────────────

    private void DrawDisenchantConfirm(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted) { DrawSelectItem(data, standing); return; }

        var activeProps = ImbueCatalogue.GetActiveProperties(_item);
        var totalShards = 0;
        foreach (var (def, _) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);
        if (activeProps.Count == 0) totalShards = 1;

        AddLabel(18, 60, 0x21, "Full Disenchant — FINAL CONFIRMATION");
        AddImageTiled(10, 78, W - 20, 1, 9304);

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

    // ── Stage: Extract Property Confirm ──────────────────────────────────────

    private void DrawExtractConfirm(ClusterFAccountData data, int standing)
    {
        if (_item == null || _item.Deleted || _selectedPropIdx < 0) { DrawSelectItem(data, standing); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx >= itemProps.Count) { DrawSelectItem(data, standing); return; }
        var def = itemProps[_selectedPropIdx];

        var shardGain = Math.Max(1, def.ShardBase / 4);

        AddLabel(18, 60, 1154, "Extract Property — Confirmation");
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

    // ── Stage: Craft Essence Select ───────────────────────────────────────────

    private void DrawCraftSelect(ClusterFAccountData data, int standing)
    {
        AddLabel(18, 60, 0x44, "Craft PropertyEssence — Mastered Properties");
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
            if (!data.IsMastered(def.Name, def.DiscoveryThreshold)) continue;

            if (pageIdx < startIdx) { pageIdx++; continue; }
            if (rowIdx  >= rowsMax) break;

            var allIdx = ImbueCatalogue.All.IndexOf(def);
            AddButton(18, y, 4011, 4012, (int)BtnId.CraftPropBase + allIdx);
            AddLabel(44, y + 2, 999,
                $"{def.Name}  — {def.CraftShards} Shard(s) + {def.CraftGold:N0} gold");

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

    // ── Stage: Craft Essence Confirm ──────────────────────────────────────────

    private void DrawCraftConfirm(ClusterFAccountData data, int standing)
    {
        if (_selectedPropIdx < 0 || _selectedPropIdx >= ImbueCatalogue.All.Count)
        {
            DrawSelectItem(data, standing); return;
        }

        var def      = ImbueCatalogue.All[_selectedPropIdx];
        var pmShards = data.GetCurrency("artificers");
        var pmGold   = CompactGoldHelper.GetTotalGold(_pm);
        var canAfford = pmShards >= def.CraftShards && pmGold >= def.CraftGold;

        AddLabel(18, 60, 0x44, "Craft PropertyEssence — Confirmation");
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

    // ── Button IDs ────────────────────────────────────────────────────────────

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
        PropBase           = 100,    // + propIdx in ForItem list (imbue)
        ExtractPropBase    = 200,    // + propIdx in ForItem list (extract)
        CraftPropBase      = 300,    // + propIdx in All list (craft)
        ConfirmBase        = 1000,   // + tier 1-5 (imbue tier confirm)
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        var acct     = _pm.Account as IAccount;
        var data     = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();
        var standing = data.GetReputation("artificers");
        var isMember = data.JoinedGuilds.Contains("artificers");

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
                if (_item != null && !_item.Deleted)
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

        // Imbue property row click (ViewItem → SelectProp)
        if (info.ButtonID >= (int)BtnId.PropBase && info.ButtonID < (int)BtnId.ExtractPropBase)
        {
            var propIdx = info.ButtonID - (int)BtnId.PropBase;
            if (_item != null && !_item.Deleted)
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.SelectProp, propIdx, 0, _page));
            return;
        }

        // Extract property row click (DisenchantView → ExtractConfirm)
        if (info.ButtonID >= (int)BtnId.ExtractPropBase && info.ButtonID < (int)BtnId.CraftPropBase)
        {
            var propIdx = info.ButtonID - (int)BtnId.ExtractPropBase;
            if (_item != null && !_item.Deleted)
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ExtractConfirm, propIdx, 0, 0));
            return;
        }

        // Craft essence row click (CraftSelect → CraftConfirm)
        if (info.ButtonID >= (int)BtnId.CraftPropBase && info.ButtonID < (int)BtnId.ConfirmBase)
        {
            var allIdx = info.ButtonID - (int)BtnId.CraftPropBase;
            if (allIdx >= 0 && allIdx < ImbueCatalogue.All.Count)
                _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftConfirm, allIdx, 0, 0));
            return;
        }

        // Tier selected (SelectProp → Confirm)
        if (info.ButtonID >= (int)BtnId.ConfirmBase && info.ButtonID < (int)BtnId.ConfirmBase + 10)
        {
            var tier = info.ButtonID - (int)BtnId.ConfirmBase;
            if (_item == null || _item.Deleted || _selectedPropIdx < 0) return;

            var itemProps = ImbueCatalogue.ForItem(_item);
            if (_selectedPropIdx >= itemProps.Count) return;

            var def        = itemProps[_selectedPropIdx];
            var vanillaMax = def.MaxVanilla;
            var guildMax   = def.GetMaxForStanding(standing);
            int target     = tier switch
            {
                1 => (int)(vanillaMax * 0.25f),
                2 => (int)(vanillaMax * 0.50f),
                3 => vanillaMax,
                4 => vanillaMax + (guildMax - vanillaMax) / 2,
                5 => guildMax,
                _ => 0
            };

            if (def.IsBool) target = 1;

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
            _                       => new ArtificersImbueGump(_pm)
        });
    }

    // ── Apply imbue logic ─────────────────────────────────────────────────────

    private void HandleApplyImbue(ClusterFAccountData data, int standing, bool isMember)
    {
        if (!_pm.CheckAlive()) return;
        if (_item == null || _item.Deleted) { _pm.SendMessage(0x22, "The item is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        var itemProps = ImbueCatalogue.ForItem(_item);
        if (_selectedPropIdx < 0 || _selectedPropIdx >= itemProps.Count)
        { _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page)); return; }

        var def    = itemProps[_selectedPropIdx];
        var cur    = def.Get(_item);
        var shards = def.ShardsFor(_targetValue, standing);
        var gold   = def.GoldFor(_targetValue, standing);
        var diff   = def.SkillDifficulty(_targetValue, standing);

        // Rank gate: property tier vs player standing
        var minStanding = ArtificersGuildmasterGump.GetMinStanding(def.DiscoveryThreshold);
        if (standing < minStanding)
        {
            var reqRank = ArtificersGuildmasterGump.GetRequiredRankName(def.DiscoveryThreshold);
            _pm.SendMessage(0x22, $"You need {reqRank} rank ({minStanding:N0} standing) to imbue {def.Name}.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Rank gate for existing-magic-item imbuing
        if (cur != 0 && !isMember && standing < 1000)
        {
            _pm.SendMessage(0x22, "Journeyman (1,000 Standing) or higher required to imbue already-enchanted items.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Slot limit
        var maxSlots  = GetMaxPropertySlots(standing);
        var usedSlots = ImbueCatalogue.CountActiveProperties(_item);
        if (cur == 0 && usedSlots >= maxSlots)
        {
            _pm.SendMessage(0x22, $"This item has reached its maximum of {maxSlots} imbued properties for your rank.");
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Discovery / essence check
        var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
        PropertyEssence? essence = null;
        if (!mastered)
        {
            essence = FindEssenceInPack(def.Name);
            if (essence == null)
            {
                _pm.SendMessage(0x22, $"You need an Essence of {def.Name} to imbue this property. Obtain one by extracting it from a magic item.");
                _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
                return;
            }
        }

        // Resource check
        var pmShards = data.GetCurrency("artificers");
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
            _pm.SendMessage(0x22, "Your hands falter — the imbue fails. No resources were consumed.");
            if (_item is BaseWeapon bwF && bwF.MaxHitPoints > 10) bwF.HitPoints = Math.Max(1, bwF.HitPoints - 10);
            else if (_item is BaseArmor baF && baF.MaxHitPoints > 10) baF.HitPoints = Math.Max(1, baF.HitPoints - 10);
            _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
            return;
        }

        // Success — consume resources
        data.SpendCurrency("artificers", shards);
        CompactGoldHelper.ConsumeGold(_pm, gold);

        // Consume essence if not mastered
        if (!mastered && essence != null)
        {
            essence.Delete();
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

        var repGain = Math.Max(1, shards / 2);
        data.AddReputation("artificers", repGain);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, $"You successfully imbue {_item.Name ?? _item.GetType().Name} with {def.Name} = {_targetValue}! (+{repGain} Artificers' Standing)");
        _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.ViewItem, -1, 0, _page));
    }

    // ── Full disenchant logic ─────────────────────────────────────────────────

    private void HandleFullDisenchant(ClusterFAccountData data, int standing)
    {
        if (!_pm.CheckAlive()) return;
        if (_item == null || _item.Deleted) { _pm.SendMessage(0x22, "The item is gone."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }
        if (_item.RootParent != _pm) { _pm.SendMessage(0x22, "You must have the item in your possession."); _pm.SendGump(new ArtificersImbueGump(_pm)); return; }

        var activeProps = ImbueCatalogue.GetActiveProperties(_item);
        var totalShards = 0;

        foreach (var (def, _) in activeProps)
            totalShards += Math.Max(1, def.ShardBase / 2);

        if (activeProps.Count == 0) totalShards = 1; // bare minimum for unenchanted items

        // Shards to player
        data.AddCurrency("artificers", totalShards);

        // Chance at PropertyEssence per property
        var essencesDropped = 0;
        foreach (var (def, _) in activeProps)
        {
            if (Utility.Random(4) == 0) // 25% chance
            {
                var ess = new PropertyEssence(def.Name);
                _pm.AddToBackpack(ess);
                essencesDropped++;
            }
        }

        // Skill gain from disenchanting
        _pm.CheckSkill(SkillName.Imbuing, 20.0, 80.0);

        var itemName = _item.Name ?? _item.GetType().Name;
        _item.Delete();

        var msg = $"You disenchant the {itemName}, recovering {totalShards} Essence Shard(s)";
        if (essencesDropped > 0) msg += $" and {essencesDropped} PropertyEssence(s)";
        msg += "!";

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, msg);
        _pm.SendGump(new ArtificersImbueGump(_pm));
    }

    // ── Extract property logic ────────────────────────────────────────────────

    private void HandleExtractProperty(ClusterFAccountData data, int standing)
    {
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

        // Durability hit (20% of max HP)
        if (_item is BaseWeapon bw)
            bw.MaxHitPoints = Math.Max(1, bw.MaxHitPoints - bw.MaxHitPoints / 5);
        else if (_item is BaseArmor ba)
            ba.MaxHitPoints = Math.Max(1, ba.MaxHitPoints - ba.MaxHitPoints / 5);

        // Yield: essence + bonus shards
        var ess = new PropertyEssence(def.Name);
        _pm.AddToBackpack(ess);

        var shardGain = Math.Max(1, def.ShardBase / 4);
        data.AddCurrency("artificers", shardGain);

        // Skill gain
        _pm.CheckSkill(SkillName.Imbuing, 20.0, 80.0);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44,
            $"You extract {def.Name} from {_item.Name ?? _item.GetType().Name}, " +
            $"receiving an Essence of {def.Name} and {shardGain} Essence Shard(s).");

        _pm.SendGump(new ArtificersImbueGump(_pm, _item, Stage.DisenchantView, -1, 0, 0));
    }

    // ── Craft essence logic ───────────────────────────────────────────────────

    private void HandleCraftEssence(ClusterFAccountData data, int standing)
    {
        if (!_pm.CheckAlive()) return;
        if (_selectedPropIdx < 0 || _selectedPropIdx >= ImbueCatalogue.All.Count)
        { _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page)); return; }

        var def = ImbueCatalogue.All[_selectedPropIdx];

        if (!data.IsMastered(def.Name, def.DiscoveryThreshold))
        {
            _pm.SendMessage(0x22, $"You have not mastered {def.Name} yet.");
            _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page));
            return;
        }

        var pmShards = data.GetCurrency("artificers");
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

        data.SpendCurrency("artificers", def.CraftShards);
        CompactGoldHelper.ConsumeGold(_pm, def.CraftGold);

        var ess = new PropertyEssence(def.Name);
        _pm.AddToBackpack(ess);

        _pm.PlaySound(0x1F5);
        _pm.SendMessage(0x44, $"You craft an Essence of {def.Name}!");
        _pm.SendGump(new ArtificersImbueGump(_pm, null, Stage.CraftSelect, -1, 0, _page));
    }

    // ── Target inner class ────────────────────────────────────────────────────

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

            var nextStage = _disenchant ? Stage.DisenchantView : Stage.ViewItem;
            from.SendGump(new ArtificersImbueGump(_pm, item, nextStage, -1, 0, 0));
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendGump(new ArtificersImbueGump(_pm));
        }
    }

    // ── Essence helpers ───────────────────────────────────────────────────────

    private int CountEssencesFor(string propertyKey)
    {
        if (_pm.Backpack == null) return 0;
        var count = 0;
        foreach (var item in _pm.Backpack.Items)
            if (item is PropertyEssence ess && ess.PropertyKey.Equals(propertyKey, StringComparison.OrdinalIgnoreCase))
                count++;
        return count;
    }

    private PropertyEssence? FindEssenceInPack(string propertyKey)
    {
        if (_pm.Backpack == null) return null;
        foreach (var item in _pm.Backpack.Items)
            if (item is PropertyEssence ess && ess.PropertyKey.Equals(propertyKey, StringComparison.OrdinalIgnoreCase))
                return ess;
        return null;
    }

    // ── Public static helpers (used by ArtificersGuildmasterGump) ────────────

    /// <summary>
    /// Returns the success chance (0-100%) for an imbue attempt.
    /// Mirrors CheckSkill(diff-20, diff+20): 0% at skill=diff-20, 100% at skill=diff+20.
    /// </summary>
    public static int ComputeSuccessChance(double skill, double diff)
    {
        var chance = (skill - (diff - 20.0)) / 40.0 * 100.0;
        return (int)Math.Clamp(chance, 0.0, 100.0);
    }

    public static int GetIntensityCap(int standing) => standing switch
    {
        >= 40000 => 200,
        >= 15000 => 175,
        >= 5000  => 150,
        >= 1000  => 120,
        _        => 100
    };

    public static int GetMaxPropertySlots(int standing) => standing switch
    {
        >= 15000 => 8,
        >= 5000  => 7,
        >= 1000  => 6,
        _        => 5
    };
}
