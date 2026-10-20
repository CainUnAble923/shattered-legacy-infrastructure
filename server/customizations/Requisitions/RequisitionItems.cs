// cc-P150 (F-47, F-48): the item side of requisitions: the kinds of item a request can name (RequisitionCategories),
// how a property, its intensity and an item's property count are read (RequisitionProperties, through the Artificers'
// catalogue, ImbueCatalogue in ArtificersImbueGump.cs, so the board and the Imbuing table agree on what a property is;
// a spellbook, which the catalogue does not cover, is read through its own AosAttributes), whether an item fills a
// request (RequisitionMatch), the Appraiser's price (AppraiserPricing, shared with the board's gold pay, F-47), and how
// a Lead's drop is made (RequisitionItemFactory: pinned's BaseRunicTool.ApplyAttributesTo, then the asked property
// raised to the request's floor; a normal item, no tag, F-48).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Items;

namespace Server;

public sealed class RequisitionCategory
{
    public string Key { get; }
    public string Label { get; }      // plural: "swords"
    public string Singular { get; }   // "a sword"
    public string PriceKey { get; }   // RequisitionAppraiserRules.BaseByCategory key
    public Func<Item, bool> Matches { get; }
    public Func<Item> Make { get; }

    public RequisitionCategory(string key, string label, string singular, string priceKey, Func<Item, bool> matches, Func<Item> make)
    {
        Key = key;
        Label = label;
        Singular = singular;
        PriceKey = priceKey;
        Matches = matches;
        Make = make;
    }
}

public static class RequisitionCategories
{
    private static readonly Dictionary<string, RequisitionCategory> _all = new(StringComparer.OrdinalIgnoreCase);

    private static Type[] _swords, _maces, _fencing, _axes;

    private static readonly Type[] LeatherTypes =
    [
        typeof(LeatherChest), typeof(LeatherArms), typeof(LeatherGloves), typeof(LeatherLegs), typeof(LeatherGorget), typeof(LeatherCap),
        typeof(StuddedChest), typeof(StuddedArms), typeof(StuddedGloves), typeof(StuddedLegs), typeof(StuddedGorget),
        typeof(BoneChest), typeof(BoneArms), typeof(BoneGloves), typeof(BoneLegs), typeof(BoneHelm),
    ];

    private static readonly Type[] MetalTypes =
    [
        typeof(PlateChest), typeof(PlateArms), typeof(PlateGloves), typeof(PlateLegs), typeof(PlateGorget), typeof(PlateHelm),
        typeof(ChainChest), typeof(ChainLegs), typeof(ChainCoif),
        typeof(RingmailChest), typeof(RingmailArms), typeof(RingmailGloves), typeof(RingmailLegs),
    ];

    static RequisitionCategories()
    {
        Add("any", "magic items", "a magic item", "other", _ => true, () => Loot.RandomWeapon());
        Add("weapon", "weapons", "a weapon", "weapon", i => i is BaseWeapon, () => Loot.RandomWeapon());
        Add("sword", "swords", "a sword", "weapon", i => i is BaseSword or BasePoleArm, () => Pick(Swords) ?? Loot.RandomWeapon());
        Add("mace", "maces", "a mace", "weapon", i => i is BaseBashing or BaseStaff, () => Pick(Maces) ?? Loot.RandomWeapon());
        Add("fencing", "fencing weapons", "a fencing weapon", "weapon", i => i is BaseSpear or BaseKnife, () => Pick(Fencing) ?? Loot.RandomWeapon());
        Add("axe", "axes", "an axe", "weapon", i => i is BaseAxe, () => Pick(Axes) ?? Loot.RandomWeapon());
        Add("bow", "bows", "a bow", "ranged", i => i is BaseRanged, () => Loot.RandomRangedWeapon());
        Add("armor", "armor pieces", "a piece of armor", "armor", i => i is BaseArmor and not BaseShield, () => Loot.RandomArmor());
        Add("leatherArmor", "leather armor pieces", "a piece of leather armor", "armor",
            i => i is BaseArmor a and not BaseShield && a.MaterialType is ArmorMaterialType.Leather or ArmorMaterialType.Studded or ArmorMaterialType.Bone or ArmorMaterialType.Spined or ArmorMaterialType.Horned or ArmorMaterialType.Barbed,
            () => Pick(LeatherTypes) ?? Loot.RandomArmor());
        Add("metalArmor", "metal armor pieces", "a piece of metal armor", "armor",
            i => i is BaseArmor a and not BaseShield && a.MaterialType is ArmorMaterialType.Ringmail or ArmorMaterialType.Chainmail or ArmorMaterialType.Plate or ArmorMaterialType.Dragon,
            () => Pick(MetalTypes) ?? Loot.RandomArmor());
        Add("shield", "shields", "a shield", "shield", i => i is BaseShield, () => Pick(Loot.ShieldTypes) ?? Loot.RandomArmor());
        Add("jewelry", "jewelry", "a piece of jewelry", "jewelry", i => i is BaseJewel, () => Loot.RandomJewelry());
        Add("spellbook", "spellbooks", "a spellbook", "spellbook", i => i is Spellbook, () => new Spellbook());
        Add("necrobook", "necromancer spellbooks", "a necromancer spellbook", "spellbook", i => i is NecromancerSpellbook, () => new NecromancerSpellbook());
        Add("hat", "hats", "a hat", "clothing", i => i is BaseHat, () => Loot.RandomHat());
        Add("instrument", "instruments", "an instrument", "instrument", i => i is BaseInstrument, () => Pick(Loot.InstrumentTypes) ?? new Lute());
    }

    private static void Add(string key, string label, string singular, string priceKey, Func<Item, bool> matches, Func<Item> make) =>
        _all[key] = new RequisitionCategory(key, label, singular, priceKey, matches, make);

    public static IEnumerable<string> Keys => _all.Keys;

    public static IEnumerable<RequisitionCategory> All => _all.Values;

    public static RequisitionCategory Get(string key) => key != null && _all.TryGetValue(key, out var c) ? c : null;

    public static bool Known(string key) => Get(key) != null;

    private static Type[] Weapons() => Loot.WeaponTypes.Concat(Loot.AosWeaponTypes).Concat(Loot.SEWeaponTypes).Distinct().ToArray();

    private static Type[] Swords => _swords ??= Weapons().Where(t => typeof(BaseSword).IsAssignableFrom(t) || typeof(BasePoleArm).IsAssignableFrom(t)).ToArray();
    private static Type[] Maces => _maces ??= Weapons().Where(t => typeof(BaseBashing).IsAssignableFrom(t) || typeof(BaseStaff).IsAssignableFrom(t)).ToArray();
    private static Type[] Fencing => _fencing ??= Weapons().Where(t => typeof(BaseSpear).IsAssignableFrom(t) || typeof(BaseKnife).IsAssignableFrom(t)).ToArray();
    private static Type[] Axes => _axes ??= Weapons().Where(t => typeof(BaseAxe).IsAssignableFrom(t)).ToArray();

    private static Item Pick(Type[] types)
    {
        if (types == null || types.Length == 0) return null;
        try
        {
            return types[Utility.Random(types.Length)].CreateInstance<Item>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The Appraiser's base-price key for an item: its kind, whatever request it might fill.</summary>
    public static string PriceKeyOf(Item item) => item switch
    {
        BaseRanged     => "ranged",
        BaseWeapon     => "weapon",
        BaseShield     => "shield",
        BaseArmor      => "armor",
        BaseJewel      => "jewelry",
        Spellbook      => "spellbook",
        BaseInstrument => "instrument",
        BaseClothing   => "clothing",
        _              => "other",
    };
}

public static class RequisitionProperties
{
    // A spellbook is not in the Artificers' catalogue (ImbueCatalogue.GetItemFlag), so its AosAttributes are read by name.
    private static readonly Dictionary<string, AosAttribute> AosByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lower Mana Cost"] = AosAttribute.LowerManaCost,
        ["Lower Reagent Cost"] = AosAttribute.LowerRegCost,
        ["Spell Damage Increase"] = AosAttribute.SpellDamage,
        ["Faster Cast Recovery"] = AosAttribute.CastRecovery,
        ["Faster Casting"] = AosAttribute.CastSpeed,
        ["Mana Regen"] = AosAttribute.RegenMana,
        ["Hit Points Regen"] = AosAttribute.RegenHits,
        ["Stamina Regen"] = AosAttribute.RegenStam,
        ["Bonus Strength"] = AosAttribute.BonusStr,
        ["Bonus Dexterity"] = AosAttribute.BonusDex,
        ["Bonus Intelligence"] = AosAttribute.BonusInt,
        ["Hit Point Increase"] = AosAttribute.BonusHits,
        ["Stamina Increase"] = AosAttribute.BonusStam,
        ["Mana Increase"] = AosAttribute.BonusMana,
        ["Luck"] = AosAttribute.Luck,
        ["Night Sight"] = AosAttribute.NightSight,
        ["Defense Chance Increase"] = AosAttribute.DefendChance,
        ["Hit Chance Increase"] = AosAttribute.AttackChance,
        ["Enhance Potions"] = AosAttribute.EnhancePotions,
        ["Reflect Physical Damage"] = AosAttribute.ReflectPhysical,
        ["Spell Channeling"] = AosAttribute.SpellChanneling,
    };

    internal static ImbuePropertyDef Def(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var d in ImbueCatalogue.All)
            if (d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return d;
        return null;
    }

    public static bool IsKnownProperty(string name) => Def(name) != null;

    public static int FullMaxOf(string name) => Math.Max(1, Def(name)?.FullMax ?? 1);

    private static bool CatalogueApplies(ImbuePropertyDef def, Item item) =>
        ImbueCatalogue.IsImbuable(item) && (def.AppliesNow & ImbueCatalogue.GetItemFlag(item)) != 0 && def.Allows?.Invoke(item) != false;

    /// <summary>The item's value of a catalogue property, 0 when it has none; false when the property cannot sit on it.</summary>
    public static bool TryRead(Item item, string property, out int value)
    {
        value = 0;
        var def = Def(property);
        if (def == null || item == null) return false;

        if (ImbueCatalogue.IsImbuable(item))
        {
            if (!CatalogueApplies(def, item)) return false;
            value = def.Get(item);
            return true;
        }

        if (item is IAosItem aos && AosByName.TryGetValue(def.Name, out var attr))
        {
            value = aos.Attributes[attr];
            return true;
        }

        return false;
    }

    /// <summary>The item's intensity of a property, in percent of the row's Full value (100 for a boolean that is on).</summary>
    public static int IntensityOf(Item item, string property)
    {
        var def = Def(property);
        if (def == null || !TryRead(item, property, out var value)) return 0;
        return def.IntensityPercent(value);
    }

    /// <summary>How many magic properties the item has (the catalogue's count; a spellbook's attributes, skills and slayers).</summary>
    public static int PropertyCount(Item item)
    {
        if (item == null) return 0;
        if (ImbueCatalogue.IsImbuable(item)) return ImbueCatalogue.CountActiveProperties(item);

        var n = 0;
        if (item is IAosItem aos)
            foreach (var a in Enum.GetValues<AosAttribute>())
                if (aos.Attributes[a] != 0)
                    n++;

        if (item is Spellbook sb)
            for (var i = 0; i < 5; i++)
                if (sb.SkillBonuses.GetBonus(i) > 0)
                    n++;

        if (item is ISlayer s)
        {
            if (s.Slayer != SlayerName.None) n++;
            if (s.Slayer2 != SlayerName.None) n++;
        }

        return n;
    }

    /// <summary>The item's total intensity: each property's value over its Full value, in percent, summed.</summary>
    public static int TotalIntensity(Item item)
    {
        if (item == null) return 0;
        if (ImbueCatalogue.IsImbuable(item)) return ImbueCatalogue.GetTotalWeight(item, false);

        var total = 0;
        if (item is IAosItem aos)
            foreach (var (name, attr) in AosByName)
            {
                var v = aos.Attributes[attr];
                if (v != 0) total += Def(name)?.IntensityPercent(v) ?? 100;
            }

        if (item is Spellbook sb)
            for (var i = 0; i < 5; i++)
            {
                var bonus = sb.SkillBonuses.GetBonus(i);
                if (bonus > 0) total += (int)(bonus * 100 / 15.0);
            }

        if (item is ISlayer s)
        {
            if (s.Slayer != SlayerName.None) total += 100;
            if (s.Slayer2 != SlayerName.None) total += 100;
        }

        return total;
    }

    public static bool IsMagic(Item item) => PropertyCount(item) > 0;

    /// <summary>True when either slayer on the item is the family asked for, or that family's group super slayer.</summary>
    public static bool HasSlayer(Item item, SlayerName want)
    {
        if (want == SlayerName.None || item is not ISlayer s) return false;
        return Satisfies(s.Slayer, want) || Satisfies(s.Slayer2, want);
    }

    private static bool Satisfies(SlayerName have, SlayerName want)
    {
        if (have == SlayerName.None) return false;
        if (have == want) return true;
        var entry = SlayerGroup.GetEntryByName(want);
        return entry?.Group?.Super != null && entry.Group.Super.Name == have;
    }

    /// <summary>
    /// Raises a property to at least <paramref name="minIntensity"/> percent of Full (a random value from there to Full;
    /// a boolean is switched on). False when the property cannot sit on the item.
    /// </summary>
    public static bool SetAtLeast(Item item, string property, int minIntensity)
    {
        var def = Def(property);
        if (def == null || item == null) return false;

        var full = Math.Max(1, def.FullMax);
        var floor = def.IsBool ? 1 : Math.Clamp((int)Math.Ceiling(minIntensity * full / 100.0), 1, full);
        var target = def.IsBool || full <= floor ? floor : Utility.RandomMinMax(floor, full);

        if (ImbueCatalogue.IsImbuable(item))
        {
            if (!CatalogueApplies(def, item)) return false;
            if (def.Get(item) < floor) def.Set(item, target);
            return true;
        }

        if (item is Spellbook sb && AosByName.TryGetValue(def.Name, out var attr))
        {
            if (sb.Attributes[attr] < floor) sb.Attributes[attr] = target;
            return true;
        }

        return false;
    }
}

public static class RequisitionMatch
{
    /// <summary>Whether this item fills this request; <paramref name="reason"/> says why not in the player's words.</summary>
    public static bool Check(RequisitionEntry e, Item item, out string reason)
    {
        reason = null;
        if (item == null || item.Deleted)
        {
            reason = "That is gone.";
            return false;
        }

        if (item.LootType == LootType.Blessed)
        {
            reason = "A blessed item cannot be turned in.";
            return false;
        }

        var cat = RequisitionCategories.Get(e.Category);
        if (cat == null || !cat.Matches(item))
        {
            reason = $"The request asks for {cat?.Singular ?? "another kind of item"}.";
            return false;
        }

        var count = RequisitionProperties.PropertyCount(item);

        switch (e.Kind)
        {
            case RequisitionKind.Property:
                if (!RequisitionProperties.TryRead(item, e.Property, out var value) || value == 0)
                {
                    reason = $"That has no {e.Property}.";
                    return false;
                }

                var pct = RequisitionProperties.IntensityOf(item, e.Property);
                if (pct < e.MinIntensity)
                {
                    reason = $"Its {e.Property} is {pct}% of full; the request asks {e.MinIntensity}% or better.";
                    return false;
                }

                if (count < e.MinProperties)
                {
                    reason = $"It has {count} {(count == 1 ? "property" : "properties")}; the request asks {e.MinProperties} or more.";
                    return false;
                }

                return true;

            case RequisitionKind.Slayer:
                if (!RequisitionProperties.HasSlayer(item, e.Slayer))
                {
                    reason = $"That does not slay {RequisitionLeads.FamilyNoun(e.Slayer)}.";
                    return false;
                }

                return true;

            default:
                var need = Math.Max(1, e.MinProperties);
                if (count < need)
                {
                    reason = count == 0
                        ? "That is not magic."
                        : $"It has {count} {(count == 1 ? "property" : "properties")}; the request asks {need} or more.";
                    return false;
                }

                return true;
        }
    }
}

/// <summary>
/// The Appraiser's price (F-47), shared with the board's gold pay: a base near the stock shopkeeper's price for the kind
/// of item (pinned's SBInfo tables sell a longsword for 27, a bow 17, a gold ring 13, a spellbook 9, leather and plate
/// chests 52 and 121), plus gold per property and per intensity point, capped. A floor for magic loot, not a market.
/// </summary>
public static class AppraiserPricing
{
    public static int Price(Item item)
    {
        if (item == null) return 0;
        var count = RequisitionProperties.PropertyCount(item);
        if (count <= 0) return 0;

        var rules = RequisitionTable.Current.Appraiser;
        var basePrice = rules.BaseFor(RequisitionCategories.PriceKeyOf(item));
        var total = basePrice + rules.PerProperty * count +
                    (int)Math.Round(rules.PerIntensityPoint * RequisitionProperties.TotalIntensity(item));
        return Math.Clamp(total, 1, Math.Max(1, rules.Max));
    }

    public static string Explain(Item item)
    {
        var count = RequisitionProperties.PropertyCount(item);
        if (count <= 0) return "no magic properties: a shopkeeper will buy it, the Appraiser will not";
        var rules = RequisitionTable.Current.Appraiser;
        var basePrice = rules.BaseFor(RequisitionCategories.PriceKeyOf(item));
        var intensity = RequisitionProperties.TotalIntensity(item);
        var raw = basePrice + rules.PerProperty * count + (int)Math.Round(rules.PerIntensityPoint * intensity);
        var price = Price(item);
        return $"{count} {(count == 1 ? "property" : "properties")}, {intensity} intensity: {basePrice} + {rules.PerProperty * count} + " +
               $"{(int)Math.Round(rules.PerIntensityPoint * intensity)} = {raw}{(raw > price ? $", capped at {price}" : "")} gold";
    }
}

/// <summary>A Lead's drop (F-48): a normal item of the request's kind that meets it; no tag.</summary>
public static class RequisitionItemFactory
{
    private static readonly string[] Fillers =
        ["Luck", "Bonus Strength", "Bonus Dexterity", "Bonus Intelligence", "Hit Point Increase", "Night Sight", "Lower Mana Cost"];

    public static Item Create(RequisitionEntry e)
    {
        var cat = RequisitionCategories.Get(e.Category) ?? RequisitionCategories.Get("weapon");
        var item = cat.Make();
        if (item == null) return null;

        var count = Math.Clamp(Math.Max(e.MinProperties, 1), 1, 5);
        var min = Math.Clamp(Math.Max(e.MinIntensity, 10), 1, 100);
        const int max = 100;

        switch (item)
        {
            case BaseWeapon w: BaseRunicTool.ApplyAttributesTo(w, count, min, max); break;
            case BaseArmor a: BaseRunicTool.ApplyAttributesTo(a, count, min, max); break;
            case BaseJewel j: BaseRunicTool.ApplyAttributesTo(j, count, min, max); break;
            case BaseHat h: BaseRunicTool.ApplyAttributesTo(h, count, min, max); break;
            case Spellbook sb: BaseRunicTool.ApplyAttributesTo(sb, count, min, max); break;
        }

        if (e.Kind == RequisitionKind.Property)
            RequisitionProperties.SetAtLeast(item, e.Property, e.MinIntensity);

        if (e.Kind == RequisitionKind.Slayer && item is ISlayer s && !RequisitionProperties.HasSlayer(item, e.Slayer))
            s.Slayer = e.Slayer;

        // Make sure the count is met too: fill with plain properties until it is.
        var need = e.Kind == RequisitionKind.Slayer ? 1 : Math.Max(1, e.MinProperties);
        foreach (var filler in Fillers)
        {
            if (RequisitionProperties.PropertyCount(item) >= need) break;
            RequisitionProperties.SetAtLeast(item, filler, 20);
        }

        return item;
    }
}
