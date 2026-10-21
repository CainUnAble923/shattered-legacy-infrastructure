// cc-P261: levelable crafted gear, phase 1 (the item level engine). Research: shard-migration
// notes/cc-P72-level-items-research.md (XmlLevelItems, ported as a design, not as code). Build notes:
// notes/cc-P261-levelable-gear-build.md. Chase's rules (design f-levelable-gear.md, 2026-10-05): levels to 200, crafted
// items made levelable by a process (here the Smith's Seal, staff-only until its source is picked), XP from hits and
// kills shared with pets and companions, every equipped levelable item gets XP, crafted gear only.
//
// Storage (P72 B.1): no field on any stock item type. An item's level, XP, unspent points and the bonuses bought live
// here, by item serial, in their own save file (Saves/LevelGear, pinned's GenericPersistence). A deleted item's row is
// dropped at the next save. The item's own Attributes are never written: the bonuses are added where the stock values
// are read (P72 B.2): AosAttributes.GetValue (patch AOS-level-gear-attribute-aggregation.patch) and the wearer's
// GetItemResistance (PlayerMobileSetResistances.cs, LevelGearCreatureResistances.cs). Tooltip lines come from the
// Item.ExtraProperties hook.
//
// XP (Chase: damage share): at a creature's death its pool (OSI's Bard difficulty, BaseInstrument.GetBaseDifficulty,
// which is XmlLevelItems' per-kill formula; P72 A.2) is split by the damage list. Damage done by a player's pets, summons,
// provoked creatures and spells is the player's (pinned's DamageEntry.Responsible, as looting rights read it). Each
// equipped levelable item of a credited player gets that player's share, capped per item after the split (XmlLevelItems:
// the next level's XP / 20). A pet or companion that dealt damage passes its master's share to its own equipped gear.
//
// Curve: total XP to reach level n is ((n + 1) x 10)^2 - 100 (XmlLevelItems' ExpToLevel). Points: 4 a level, 8 on every
// 10th. Every number is a clusterf.levelGear.* setting (LevelGearSettings below).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;
using Server.Collections;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

/// <summary>Every number of levelable gear, as a server setting (Configuration/modernuo.json).</summary>
public static class LevelGearSettings
{
    public static bool Enabled => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.enabled", true);

    // Chase's rule 1: 200 for now, raised later by this setting.
    public static int MaxLevel => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.maxLevel", 200);

    // Curve: total XP to reach level n is ((n + 1) x curveBase)^2 - curveOffset.
    public static int CurveBase => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.curveBase", 10);
    public static int CurveOffset => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.curveOffset", 100);

    // The creature's pool is its Bard difficulty times this.
    public static double XpMultiplier => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.xpMultiplier", 1.0);

    // The per-kill cap, per item, after the split: the next level's total XP / capDivisor.
    public static bool CapEnabled => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.capEnabled", true);
    public static int CapDivisor => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.capDivisor", 20);

    // Points: pointsPerLevel each level, pointsPerMilestone instead on every milestoneEvery-th level.
    public static int PointsPerLevel => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.pointsPerLevel", 4);
    public static int PointsPerMilestone => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.pointsPerMilestone", 8);
    public static int MilestoneEvery => ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.milestoneEvery", 10);

    // An exceptional item's ceiling per property: the regular ceiling (the imbuing Full value) times this, never above
    // the property's Overcharged maximum (P72 C.3, F-29's exceptional gap).
    public static double ExceptionalCeilingFactor =>
        ServerConfiguration.GetOrUpdateSetting("clusterf.levelGear.exceptionalCeilingFactor", 1.5);

    public static int CostOf(LevelGearAttribute a) =>
        ServerConfiguration.GetOrUpdateSetting($"clusterf.levelGear.attr.{a.Key}.cost", a.DefaultCost);

    // The most a level may buy of it; 0 means "the property's ceiling".
    public static int MaxOf(LevelGearAttribute a) =>
        ServerConfiguration.GetOrUpdateSetting($"clusterf.levelGear.attr.{a.Key}.max", 0);

    public static void Configure()
    {
        _ = Enabled; _ = MaxLevel; _ = CurveBase; _ = CurveOffset; _ = XpMultiplier; _ = CapEnabled; _ = CapDivisor;
        _ = PointsPerLevel; _ = PointsPerMilestone; _ = MilestoneEvery; _ = ExceptionalCeilingFactor;
        foreach (var a in LevelGearAttribute.All)
        {
            _ = CostOf(a);
            _ = MaxOf(a);
        }
    }
}

/// <summary>
/// What a level point may buy (P72 C.3 and decision 8): AosAttribute properties and the five resists, never a skill
/// bonus, Self Repair or a weapon hit effect. Each is one imbuing catalogue row, whose Full value is its regular ceiling,
/// whose item kinds are where it may go, and whose value on the item counts against the ceiling. Left out: Str, Dex and
/// Int (worn as stat mods added at equip, not read through GetValue) and the on/off properties (Spell Channeling, Night
/// Sight: read from the item's own Attributes).
/// </summary>
public sealed class LevelGearAttribute
{
    public string Key { get; }
    public string Row { get; }
    public AosAttribute? Attribute { get; }
    public ResistanceType? Resist { get; }
    public int DefaultCost { get; }

    private LevelGearAttribute(string key, string row, AosAttribute? attribute, ResistanceType? resist, int cost)
    {
        Key = key;
        Row = row;
        Attribute = attribute;
        Resist = resist;
        DefaultCost = cost;
    }

    private static LevelGearAttribute A(string key, string row, AosAttribute attr, int cost) => new(key, row, attr, null, cost);
    private static LevelGearAttribute R(string key, string row, ResistanceType type, int cost) => new(key, row, null, type, cost);

    // Costs per +1, on XmlLevelItems' scale (P72 A.3: Damage Increase 5, Hit Chance 10, Luck 2, resists 2), the rest set
    // so a property's full ceiling costs roughly what Damage Increase's does. Order is the gump's.
    public static readonly LevelGearAttribute[] All =
    [
        A("damageIncrease", "Damage Increase", AosAttribute.WeaponDamage, 5),
        A("swingSpeed", "Swing Speed Increase", AosAttribute.WeaponSpeed, 5),
        A("hitChance", "Hit Chance Increase", AosAttribute.AttackChance, 10),
        A("defenseChance", "Defense Chance Increase", AosAttribute.DefendChance, 10),
        A("spellDamage", "Spell Damage Increase", AosAttribute.SpellDamage, 15),
        A("castRecovery", "Faster Cast Recovery", AosAttribute.CastRecovery, 60),
        A("castSpeed", "Faster Casting", AosAttribute.CastSpeed, 120),
        A("lowerManaCost", "Lower Mana Cost", AosAttribute.LowerManaCost, 6),
        A("lowerRegCost", "Lower Reagent Cost", AosAttribute.LowerRegCost, 2),
        A("luck", "Luck", AosAttribute.Luck, 2),
        A("enhancePotions", "Enhance Potions", AosAttribute.EnhancePotions, 5),
        A("reflectPhysical", "Reflect Physical Damage", AosAttribute.ReflectPhysical, 10),
        A("hitPointRegen", "Hit Points Regen", AosAttribute.RegenHits, 30),
        A("staminaRegen", "Stamina Regen", AosAttribute.RegenStam, 30),
        A("manaRegen", "Mana Regen", AosAttribute.RegenMana, 30),
        A("hitPointIncrease", "Hit Point Increase", AosAttribute.BonusHits, 30),
        A("staminaIncrease", "Stamina Increase", AosAttribute.BonusStam, 20),
        A("manaIncrease", "Mana Increase", AosAttribute.BonusMana, 20),
        R("physicalResist", "Physical Resist Bonus", ResistanceType.Physical, 2),
        R("fireResist", "Fire Resist Bonus", ResistanceType.Fire, 2),
        R("coldResist", "Cold Resist Bonus", ResistanceType.Cold, 2),
        R("poisonResist", "Poison Resist Bonus", ResistanceType.Poison, 2),
        R("energyResist", "Energy Resist Bonus", ResistanceType.Energy, 2),
    ];

    public static LevelGearAttribute? ByKey(string key) =>
        Array.Find(All, a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private ImbuePropertyDef? _def;

    // The catalogue is built once (ImbueCatalogue.All) and our rows are not config-driven custom rows, so cache the row.
    internal ImbuePropertyDef? Def
    {
        get
        {
            if (_def == null)
            {
                foreach (var d in ImbueCatalogue.All)
                {
                    if (d.Name == Row)
                    {
                        _def = d;
                        break;
                    }
                }
            }

            return _def;
        }
    }

    public string Name => Row;

    /// <summary>Whether this property may go on this item kind (the imbuing row's kinds).</summary>
    public bool AppliesTo(Item item) =>
        Def is { } d && (d.AppliesNow & ImbueCatalogue.GetItemFlag(item)) != 0 && d.Allows?.Invoke(item) != false;

    /// <summary>The item's own value of this property (imbued, crafted or runic), which counts against the ceiling.</summary>
    public int OwnValue(Item item) => AppliesTo(item) ? Def!.Get(item) : 0;

    /// <summary>
    /// The highest the item's own value plus the level bonus may reach: the imbuing Full value, x1.5 on an exceptional
    /// weapon or armour piece but never past the Overcharged maximum (jewellery has no quality, so the regular ceiling).
    /// </summary>
    public int Ceiling(Item item)
    {
        if (Def is not { } d)
        {
            return 0;
        }

        var full = d.FullMax;
        if (!ClusterFLevelGear.IsExceptional(item))
        {
            return full;
        }

        return Math.Max(full, Math.Min((int)(full * LevelGearSettings.ExceptionalCeilingFactor), d.TopValue));
    }

    /// <summary>The most a level may buy on this item now: the setting's max, and what the ceiling leaves.</summary>
    public int MaxBuy(Item item)
    {
        var room = Math.Max(0, Ceiling(item) - OwnValue(item));
        var max = LevelGearSettings.MaxOf(this);
        return max > 0 ? Math.Min(max, room) : room;
    }
}

/// <summary>One levelable item's state.</summary>
public sealed class LevelGearState
{
    public int Level { get; set; }
    public long Xp { get; set; }
    public int Points { get; set; }
    public int MaxLevel { get; set; }
    public DateTime MadeAt { get; set; }
    public Serial MadeBy { get; set; }

    // What the points bought, by attribute key: the bonus value (each +1 cost the attribute's price when bought).
    public Dictionary<string, int> Bought { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int BoughtOf(LevelGearAttribute a) => Bought.TryGetValue(a.Key, out var v) ? v : 0;
}

public class LevelGearStore : GenericPersistence
{
    // 0: cc-P261.
    public const int CurrentVersion = 0;

    private static LevelGearStore? _instance;

    internal static readonly Dictionary<Serial, LevelGearState> Rows = new();

    public LevelGearStore() : base("LevelGear", 10)
    {
    }

    public static LevelGearStore Instance => _instance ??= new LevelGearStore();

    public static void Configure()
    {
        _ = Instance;
        Item.ExtraProperties += ClusterFLevelGear.AddTooltip;
        LevelGearSettings.Configure();
        CommandSystem.Register("ItemLevel", AccessLevel.Player, ClusterFLevelGear.OnItemLevelCommand);
        CommandSystem.Register("LevelGearSet", AccessLevel.GameMaster, ClusterFLevelGear.OnLevelGearSetCommand);
    }

    public static LevelGearState? Of(Item? item) =>
        item != null && Rows.TryGetValue(item.Serial, out var s) ? s : null;

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<Serial, LevelGearState>>();
        foreach (var pair in Rows)
        {
            if (World.FindItem(pair.Key) is { Deleted: false })
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(live.Count);
        foreach (var (serial, s) in live)
        {
            writer.Write(serial);
            writer.WriteEncodedInt(s.Level);
            writer.Write(s.Xp);
            writer.WriteEncodedInt(s.Points);
            writer.WriteEncodedInt(s.MaxLevel);
            writer.Write(s.MadeAt);
            writer.Write(s.MadeBy);
            writer.WriteEncodedInt(s.Bought.Count);
            foreach (var (key, value) in s.Bought)
            {
                writer.Write(key);
                writer.WriteEncodedInt(value);
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"LevelGearStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Rows.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var s = new LevelGearState
            {
                Level = reader.ReadEncodedInt(),
                Xp = reader.ReadLong(),
                Points = reader.ReadEncodedInt(),
                MaxLevel = reader.ReadEncodedInt(),
                MadeAt = reader.ReadDateTime(),
                MadeBy = reader.ReadSerial()
            };

            var bought = reader.ReadEncodedInt();
            for (var j = 0; j < bought; j++)
            {
                var key = reader.ReadString();
                s.Bought[key] = reader.ReadEncodedInt();
            }

            Rows[serial] = s;
        }
    }
}

public static class ClusterFLevelGear
{
    // -- Eligibility (Chase's rules 2 and 5) --------------------------------------------------------------------------

    public static bool IsLevelable(Item? item) => LevelGearStore.Of(item) != null;

    public static bool IsExceptional(Item item) => item switch
    {
        BaseWeapon w => w.Quality == WeaponQuality.Exceptional,
        BaseArmor a  => a.Quality == ArmorQuality.Exceptional,
        _            => false
    };

    private static int ArtifactRarityOf(Item item) => item switch
    {
        BaseWeapon w => w.ArtifactRarity,
        BaseArmor a  => a.ArtifactRarity,
        BaseJewel j  => j.ArtifactRarity,
        _            => 0
    };

    /// <summary>
    /// Null when this item may be made levelable, else why not. Crafted means pinned's PlayerConstructed, which the craft
    /// engine sets (CraftItem) and staff-made, found, bought and reward items never have; an artifact (ArtifactRarity
    /// above 0) is refused even when crafted. Weapons, armour (shields included) and jewellery only.
    /// </summary>
    public static string? WhyNot(Item item)
    {
        if (item is not (BaseWeapon or BaseArmor or BaseJewel))
        {
            return "Only weapons, armor, shields and jewelry can take the seal.";
        }

        if (ArtifactRarityOf(item) > 0)
        {
            return "Artifacts can never be made levelable.";
        }

        if (!item.PlayerConstructed)
        {
            return "Only crafted gear can be made levelable. Found gear cannot.";
        }

        if (IsLevelable(item))
        {
            return "That is already levelable.";
        }

        return null;
    }

    /// <summary>Makes the item levelable (no checks beyond the rules in WhyNot). Returns why not, or null when done.</summary>
    public static string? MakeLevelable(Item item, Mobile? by)
    {
        if (WhyNot(item) is { } why)
        {
            return why;
        }

        LevelGearStore.Rows[item.Serial] = new LevelGearState
        {
            MaxLevel = LevelGearSettings.MaxLevel,
            MadeAt = Core.Now,
            MadeBy = by?.Serial ?? Serial.MinusOne
        };
        item.InvalidateProperties();
        return null;
    }

    // -- The curve and points -----------------------------------------------------------------------------------------

    /// <summary>Total XP to reach level n: ((n + 1) x 10)^2 - 100, so level 0 is 0 and level 1 is 300.</summary>
    public static long XpForLevel(int level)
    {
        if (level <= 0)
        {
            return 0;
        }

        var root = (long)(level + 1) * LevelGearSettings.CurveBase;
        return root * root - LevelGearSettings.CurveOffset;
    }

    public static int PointsForReaching(int level)
    {
        var every = LevelGearSettings.MilestoneEvery;
        return every > 0 && level % every == 0 ? LevelGearSettings.PointsPerMilestone : LevelGearSettings.PointsPerLevel;
    }

    /// <summary>The most one kill may give this item, after the split: the next level's total XP / 20.</summary>
    public static long CapFor(LevelGearState s) =>
        LevelGearSettings.CapEnabled ? XpForLevel(s.Level + 1) / Math.Max(1, LevelGearSettings.CapDivisor) : long.MaxValue;

    /// <summary>Adds XP to one item (capped), levels it and pays its points. Returns the XP actually added.</summary>
    public static long AddXp(Item item, long amount, Mobile? wearer)
    {
        if (LevelGearStore.Of(item) is not { } s || amount <= 0)
        {
            return 0;
        }

        var maxLevel = Math.Min(s.MaxLevel, LevelGearSettings.MaxLevel);
        if (s.Level >= maxLevel)
        {
            return 0;
        }

        var gain = Math.Min(amount, CapFor(s));
        gain = Math.Min(gain, XpForLevel(maxLevel) - s.Xp);
        if (gain <= 0)
        {
            return 0;
        }

        s.Xp += gain;

        // Every level crossed pays its own points (XmlLevelItems paid one level's points for a multi-level jump, P72 A.6).
        var before = s.Level;
        while (s.Level < maxLevel && s.Xp >= XpForLevel(s.Level + 1))
        {
            s.Level++;
            s.Points += PointsForReaching(s.Level);
        }

        if (s.Level > before)
        {
            wearer?.SendMessage(0x59, $"Your {Describe(item)} has reached level {s.Level}. ({s.Points} points to spend: [ItemLevel)");
        }

        item.InvalidateProperties();
        return gain;
    }

    private static string Describe(Item item) => item.Name ?? item.GetType().Name;

    // -- XP from deaths (Chase: damage share; rule 4, every equipped levelable item) --------------------------------------

    /// <summary>The creature's XP pool: its Bard difficulty (XmlLevelItems' per-kill formula), times the setting.</summary>
    public static double PoolFor(BaseCreature bc) => BaseInstrument.GetBaseDifficulty(bc) * LevelGearSettings.XpMultiplier;

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (LevelGearSettings.Enabled)
        {
            AwardDeath(bc, PoolFor(bc));
        }
    }

    /// <summary>
    /// Splits the pool by the damage list and pays every credited player's equipped levelable items, and the equipped
    /// levelable items of their pets and companions that dealt damage. Returns each credited player's share (before the
    /// per-item cap). No XP from a pet, summon or anything with a player master, or a creature flagged NoKillAwards.
    /// </summary>
    public static Dictionary<Mobile, long> AwardDeath(BaseCreature bc, double pool)
    {
        var shares = new Dictionary<Mobile, long>();
        if (bc.Controlled || bc.Summoned || bc.NoKillAwards || bc.GetMaster() is PlayerMobile || pool <= 0)
        {
            return shares;
        }

        var damage = new Dictionary<Mobile, int>();
        var helpers = new Dictionary<Mobile, List<Mobile>>();
        var total = 0;

        foreach (var de in bc.DamageEntries)
        {
            var dealt = de.DamageGiven;
            total += dealt;

            var byMasters = 0;
            for (var j = 0; j < de.Responsible?.Count; j++)
            {
                var sub = de.Responsible[j];
                if (sub.Damager is not { Deleted: false, Player: true } master)
                {
                    continue;
                }

                var part = Math.Min(sub.DamageGiven, dealt - byMasters);
                byMasters += part;
                damage[master] = (damage.TryGetValue(master, out var d) ? d : 0) + part;

                if (de.Damager is BaseCreature helper && helper != master)
                {
                    if (!helpers.TryGetValue(master, out var list))
                    {
                        helpers[master] = list = [];
                    }

                    if (!list.Contains(helper))
                    {
                        list.Add(helper);
                    }
                }
            }

            if (dealt > byMasters && de.Damager is { Deleted: false, Player: true } player)
            {
                damage[player] = (damage.TryGetValue(player, out var d) ? d : 0) + dealt - byMasters;
            }
        }

        if (total <= 0)
        {
            return shares;
        }

        foreach (var (player, dealt) in damage)
        {
            var share = (long)Math.Round(pool * dealt / total);
            if (share <= 0)
            {
                continue;
            }

            shares[player] = share;
            AwardWorn(player, share);

            if (helpers.TryGetValue(player, out var list))
            {
                foreach (var helper in list)
                {
                    AwardWorn(helper, share);
                }
            }
        }

        return shares;
    }

    /// <summary>Pays this share to every levelable item the mobile wears (a creature's equipped items too).</summary>
    public static void AwardWorn(Mobile m, long share)
    {
        if (m.Items == null)
        {
            return;
        }

        // Copied: a level-up message or tooltip refresh never changes what is worn, but be safe against re-entry.
        foreach (var item in m.Items.ToArray())
        {
            if (item is not Container && IsLevelable(item))
            {
                AddXp(item, share, m is PlayerMobile ? m : (m as BaseCreature)?.GetMaster());
            }
        }
    }

    // -- Spending points ------------------------------------------------------------------------------------------------

    /// <summary>How many properties the item has now, its own and those only a level bought.</summary>
    public static int SlotsUsed(Item item)
    {
        var used = ImbueCatalogue.CountActiveProperties(item);
        if (LevelGearStore.Of(item) is { } s)
        {
            foreach (var a in LevelGearAttribute.All)
            {
                if (s.BoughtOf(a) > 0 && a.OwnValue(item) == 0)
                {
                    used++;
                }
            }
        }

        return used;
    }

    /// <summary>The item's property slots: the imbuing table's base slots (the same limit imbuing has).</summary>
    public static int SlotLimit => ImbuingTable.Current.BaseSlots;

    /// <summary>Why +1 of this attribute cannot be bought on this item now, or null.</summary>
    public static string? WhyNotBuy(Item item, LevelGearAttribute a)
    {
        if (LevelGearStore.Of(item) is not { } s)
        {
            return "That item is not levelable.";
        }

        if (!a.AppliesTo(item))
        {
            return $"{a.Name} cannot go on this kind of item.";
        }

        var cost = LevelGearSettings.CostOf(a);
        if (s.Points < cost)
        {
            return $"{a.Name} costs {cost} points; the item has {s.Points}.";
        }

        if (s.BoughtOf(a) + 1 > a.MaxBuy(item))
        {
            return $"{a.Name} is at its most for this item.";
        }

        if (s.BoughtOf(a) == 0 && a.OwnValue(item) == 0 && SlotsUsed(item) >= SlotLimit)
        {
            return $"The item already has {SlotLimit} properties; it can only raise ones it has.";
        }

        return null;
    }

    /// <summary>Buys +1 of the attribute. Returns why not, or null when bought.</summary>
    public static string? Buy(Item item, LevelGearAttribute a)
    {
        if (WhyNotBuy(item, a) is { } why)
        {
            return why;
        }

        var s = LevelGearStore.Of(item)!;
        s.Points -= LevelGearSettings.CostOf(a);
        s.Bought[a.Key] = s.BoughtOf(a) + 1;

        item.InvalidateProperties();
        if (item.Parent is Mobile wearer)
        {
            wearer.UpdateResistances();
            wearer.Delta(MobileDelta.Hits | MobileDelta.Stam | MobileDelta.Mana);
        }

        return null;
    }

    // -- Applying the bonuses (read time; P72 B.2) ------------------------------------------------------------------------

    /// <summary>
    /// The bonus a level bought, as it counts now: never more than the ceiling leaves above the item's own value, so an
    /// item imbued higher later never passes the shard's property cap.
    /// </summary>
    public static int EffectiveBonus(Item item, LevelGearAttribute a)
    {
        if (LevelGearStore.Of(item) is not { } s)
        {
            return 0;
        }

        var bought = s.BoughtOf(a);
        return bought <= 0 ? 0 : Math.Min(bought, a.MaxBuy(item));
    }

    /// <summary>AosAttributes.GetValue's hook (patched): what this worn item's levels add to this attribute.</summary>
    public static int AttributeBonus(Mobile m, Item item, AosAttribute attribute)
    {
        if (LevelGearStore.Rows.Count == 0 || item is Container || LevelGearStore.Of(item) is not { Bought.Count: > 0 })
        {
            return 0;
        }

        var total = 0;
        foreach (var a in LevelGearAttribute.All)
        {
            if (a.Attribute == attribute)
            {
                total += EffectiveBonus(item, a);
            }
        }

        return total;
    }

    /// <summary>GetItemResistance's addition (PlayerMobile and BaseCreature overrides): the item's bought resist.</summary>
    public static int ResistBonus(Item item, ResistanceType type)
    {
        if (LevelGearStore.Rows.Count == 0 || LevelGearStore.Of(item) is not { Bought.Count: > 0 })
        {
            return 0;
        }

        var total = 0;
        foreach (var a in LevelGearAttribute.All)
        {
            if (a.Resist == type)
            {
                total += EffectiveBonus(item, a);
            }
        }

        return total;
    }

    // -- Tooltip --------------------------------------------------------------------------------------------------------

    internal static void AddTooltip(Item item, IPropertyList list)
    {
        if (LevelGearStore.Of(item) is not { } s)
        {
            return;
        }

        var next = s.Level >= Math.Min(s.MaxLevel, LevelGearSettings.MaxLevel) ? "max" : $"{XpForLevel(s.Level + 1):N0}";
        list.Add(1060847, $"Level {s.Level}\t{s.Xp:N0} / {next} XP"); // ~1_val~ ~2_val~

        foreach (var a in LevelGearAttribute.All)
        {
            var bonus = EffectiveBonus(item, a);
            if (bonus > 0)
            {
                list.Add(1060847, $"{a.Name} +{bonus}\t(level)");
            }
        }
    }

    // -- Commands -------------------------------------------------------------------------------------------------------

    [Usage("ItemLevel")]
    [Description("Target a levelable item you have to see its level and spend its points.")]
    [ShardCommand(CommandCategory.Player)]
    public static void OnItemLevelCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a levelable item you carry or wear.");
        e.Mobile.Target = new ItemLevelTarget();
    }

    [Usage("LevelGearSet <level> [points]")]
    [Description("Staff: set a levelable item's level (and XP to match), and optionally its unspent points.")]
    [ShardCommand(CommandCategory.DevTool)]
    public static void OnLevelGearSetCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: LevelGearSet <level> [points]");
            return;
        }

        var level = e.GetInt32(0);
        var points = e.Length > 1 ? e.GetInt32(1) : -1;
        e.Mobile.SendMessage("Target a levelable item.");
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not Item item || LevelGearStore.Of(item) is not { } s)
            {
                from.SendMessage("That is not levelable.");
                return;
            }

            s.Level = Math.Clamp(level, 0, Math.Min(s.MaxLevel, LevelGearSettings.MaxLevel));
            s.Xp = XpForLevel(s.Level);
            if (points >= 0)
            {
                s.Points = points;
            }

            item.InvalidateProperties();
            from.SendMessage($"Level {s.Level}, {s.Xp:N0} XP, {s.Points} points.");
        });
    }

    /// <summary>Whether this player may view and spend this item: they carry or wear it.</summary>
    public static bool Holds(Mobile m, Item item) => !item.Deleted && (item.Parent == m || item.IsChildOf(m.Backpack));

    private sealed class ItemLevelTarget : Target
    {
        public ItemLevelTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Item item || !IsLevelable(item))
            {
                from.SendMessage("That is not levelable.");
            }
            else if (!Holds(from, item))
            {
                from.SendMessage("You must carry or wear the item.");
            }
            else
            {
                from.SendGump(new LevelGearGump(from, item));
            }
        }
    }
}
