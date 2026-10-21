// cc-P228 (F-29, Chase 2026-10-03): exceptional items get their material's bonuses x1.5, for every material.
//
// Ours, and a deviation for OSI's materials too (shard-migration docs/fidelity-principle.md; the register line is in
// notes/cc-P228-exceptional-material-gap.md). Chase: "normal stays as it is, exceptional gets x1.5 (rounded)", every
// material, OSI's included.
//
// Pinned reads a material's bonuses live, with no quality check, from CraftResources.GetInfo(resource).AttributeInfo:
// armor resists through BaseArmor.GetResourceAttrs (pinned BaseArmor.cs:859-860, used :484-497), durability
// GetDurabilityBonus :894, lower requirements GetLowerStatReq :958, luck GetLuckBonus :1263; weapon durability
// BaseWeapon.cs:940, lower requirements GetLowerStatReq :955, element damage GetDamageTypes :2366-2377, luck
// GetLuckBonus :2908.
// Each of those reads now asks Attrs(resource, exceptional) instead (patches BaseArmor-exceptional-material.patch and
// BaseWeapon-exceptional-material.patch), so this is the one place the rule lives:
// - Normal and low quality items read the material's own CraftAttributeInfo, exactly as pinned.
// - Exceptional items read a copy with every positive value x1.5, rounded half up (Scale). Penalties (the scales'
//   -3 resists, Frost's -2 fire) stay as they are: the rule scales bonuses.
// - The runic ranges are copied unchanged (runic properties scale by runic tier, not by this rule).
//
// Element damage: the material's share is taken from physical by pinned's ApplyCraftAttributeElementDamage, which
// stops at what physical has left, so the split still totals 100 with a x1.5 share (Celestial's 15 x4 becomes 23 x4,
// physical 8).
//
// The OSI woods have no CraftAttributeInfo values; their bonuses are BaseRanged's craft-time switch (bows only, pinned
// BaseRanged.cs:207-258). BeginWoodBonus / EndWoodBonus wrap that call (patch BaseRanged-exceptional-wood.patch) and
// scale what it added on an exceptional bow. The extended woods' F-28 table (ClusterFWoodBonuses) is not multiplied:
// it has its own exceptional line.
//
// Not multiplied either: the exceptional bonuses that are not the material's (weapon damage increase 35 plus Arms
// Lore, armor resist points, the +20 durability) and anything a runic tool rolled.

using System.Collections.Generic;
using Server.Items;

namespace Server;

public static class ClusterFExceptionalMaterial
{
    private static readonly Dictionary<CraftAttributeInfo, CraftAttributeInfo> _exceptional = new();

    /// <summary>A material bonus as an exceptional item reads it: positive values x1.5, half up; penalties unchanged.</summary>
    public static int Scale(int value) =>
        value > 0 ? (value * 3 + 1) / 2 : value;

    /// <summary>The material's attribute info as an item of this quality reads it (null when the resource has none).</summary>
    public static CraftAttributeInfo Attrs(CraftResource resource, bool exceptional)
    {
        var info = CraftResources.GetInfo(resource)?.AttributeInfo;

        if (info == null || !exceptional)
        {
            return info;
        }

        if (!_exceptional.TryGetValue(info, out var scaled))
        {
            _exceptional[info] = scaled = Scaled(info);
        }

        return scaled;
    }

    public static CraftAttributeInfo Attrs(BaseArmor armor) =>
        Attrs(armor.Resource, armor.Quality == ArmorQuality.Exceptional);

    public static CraftAttributeInfo Attrs(BaseWeapon weapon) =>
        Attrs(weapon.Resource, weapon.Quality == WeaponQuality.Exceptional);

    private static CraftAttributeInfo Scaled(CraftAttributeInfo i) => new()
    {
        WeaponFireDamage = Scale(i.WeaponFireDamage),
        WeaponColdDamage = Scale(i.WeaponColdDamage),
        WeaponPoisonDamage = Scale(i.WeaponPoisonDamage),
        WeaponEnergyDamage = Scale(i.WeaponEnergyDamage),
        WeaponChaosDamage = Scale(i.WeaponChaosDamage),
        WeaponDirectDamage = Scale(i.WeaponDirectDamage),
        WeaponDurability = Scale(i.WeaponDurability),
        WeaponLuck = Scale(i.WeaponLuck),
        WeaponGoldIncrease = Scale(i.WeaponGoldIncrease),
        WeaponLowerRequirements = Scale(i.WeaponLowerRequirements),
        ArmorPhysicalResist = Scale(i.ArmorPhysicalResist),
        ArmorFireResist = Scale(i.ArmorFireResist),
        ArmorColdResist = Scale(i.ArmorColdResist),
        ArmorPoisonResist = Scale(i.ArmorPoisonResist),
        ArmorEnergyResist = Scale(i.ArmorEnergyResist),
        ArmorDurability = Scale(i.ArmorDurability),
        ArmorLuck = Scale(i.ArmorLuck),
        ArmorGoldIncrease = Scale(i.ArmorGoldIncrease),
        ArmorLowerRequirements = Scale(i.ArmorLowerRequirements),

        // runic ranges: unchanged
        RunicMinAttributes = i.RunicMinAttributes,
        RunicMaxAttributes = i.RunicMaxAttributes,
        RunicMinIntensity = i.RunicMinIntensity,
        RunicMaxIntensity = i.RunicMaxIntensity
    };

    // ---------------------------------------------------------------- OSI woods on bows

    /// <summary>What BaseRanged's wood switch can touch, read before it runs (patch, BaseRanged.OnCraft).</summary>
    public readonly record struct WoodSnapshot(
        int Luck, int Damage, int Speed, int HitChance, int RegenHits, int Durability, int LowerStatReq, int LeechHits,
        int Fire, int Cold, int Poison, int Energy, int Chaos, int Direct
    );

    public static WoodSnapshot BeginWoodBonus(BaseWeapon w) => new(
        w.Attributes.Luck, w.Attributes.WeaponDamage, w.Attributes.WeaponSpeed, w.Attributes.AttackChance,
        w.Attributes.RegenHits, w.WeaponAttributes.DurabilityBonus, w.WeaponAttributes.LowerStatReq,
        w.WeaponAttributes.HitLeechHits, w.AosElementDamages.Fire, w.AosElementDamages.Cold, w.AosElementDamages.Poison,
        w.AosElementDamages.Energy, w.AosElementDamages.Chaos, w.AosElementDamages.Direct
    );

    /// <summary>After BaseRanged's wood switch (patch): on an exceptional bow, what it added becomes x1.5.</summary>
    public static void EndWoodBonus(BaseWeapon w, WoodSnapshot before)
    {
        if (w.Quality != WeaponQuality.Exceptional)
        {
            return;
        }

        var a = w.Attributes;
        var wa = w.WeaponAttributes;
        var e = w.AosElementDamages;

        a.Luck = Rescale(before.Luck, a.Luck);
        a.WeaponDamage = Rescale(before.Damage, a.WeaponDamage);
        a.WeaponSpeed = Rescale(before.Speed, a.WeaponSpeed);
        a.AttackChance = Rescale(before.HitChance, a.AttackChance);
        a.RegenHits = Rescale(before.RegenHits, a.RegenHits);
        wa.LowerStatReq = Rescale(before.LowerStatReq, wa.LowerStatReq);
        wa.HitLeechHits = Rescale(before.LeechHits, wa.HitLeechHits);

        if (wa.DurabilityBonus != before.Durability)
        {
            wa.DurabilityBonus = Rescale(before.Durability, wa.DurabilityBonus);
        }

        // Frostwood's 40 cold becomes 60; physical keeps what is left, so the split still totals 100
        var fire = Rescale(before.Fire, e.Fire);
        var cold = Rescale(before.Cold, e.Cold);
        var poison = Rescale(before.Poison, e.Poison);
        var energy = Rescale(before.Energy, e.Energy);
        var chaos = Rescale(before.Chaos, e.Chaos);
        var direct = Rescale(before.Direct, e.Direct);

        if (fire != e.Fire || cold != e.Cold || poison != e.Poison || energy != e.Energy || chaos != e.Chaos ||
            direct != e.Direct)
        {
            e.Fire = fire;
            e.Cold = cold;
            e.Poison = poison;
            e.Energy = energy;
            e.Chaos = chaos;
            e.Direct = direct;
            e.Physical = System.Math.Max(0, 100 - fire - cold - poison - energy - chaos - direct);
        }
    }

    private static int Rescale(int before, int after) => before + Scale(after - before);
}
