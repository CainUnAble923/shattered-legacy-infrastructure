// cc-P227 (F-28, Chase 2026-10-02): what the eight extended woods give on bows, staves, clubs and wooden shields.
//
// Ours: OSI has six woods, so every number here is ours (a deviation for shard-migration docs/fidelity-principle.md;
// the register line is in notes/cc-P227-extended-wood-bonuses.md). Pinned gives wood bonuses to bows only, from
// BaseRanged.OnCraft when Core.ML, regardless of quality (pinned BaseRanged.cs:206-210, the switch :215-258,
// Heartwood's random pick :260-295). This table is the
// fall-through for the woods that switch does not know: Ironwood to Starwood. The OSI woods keep pinned's rows (bows
// only, nothing on staves, clubs or shields) and Rootwood keeps its cc-P36 row (none: a cosmetic starter wood).
//
// Each wood has a weapon row and a shield row, as F-28's two tables give them: a normal item gets "Normal", an
// exceptional one gets "Normal" plus "Exceptional adds" (the exceptional gap, F-28 rule 1). Applied once, at craft
// time, like pinned's bow bonuses, from the end of BaseWeapon.OnCraft and BaseArmor.OnCraft
// (patches BaseWeapon-wood-bonuses.patch and BaseArmor-wood-bonuses.patch).
//
// Rules carried from the entry:
// - No Self Repair from wood, ever: no row sets it, and the Heartwood pick has none.
// - "Lower level req" is stored (LowerLevelReq) and applies nothing: there is no level system yet (F-12).
// - Starwood's "one random Heartwood-style extra" is pinned's Heartwood pick, the same six outcomes from the same single
//   Utility.Random(6) (HeartwoodExtra; a test holds it to pinned's private BaseRanged.ApplyHeartwoodBonus).
//
// Which items count as wood: a bow (BaseRanged), staff (BaseStaff) or club (BaseBashing) weapon, or a shield
// (BaseShield), whose crafted Resource is one of the eight woods. Metal maces and shields never carry a wood Resource,
// so the resource check is what makes them wood items; the class check keeps out the other wooden weapons
// (the Bokuto is a sword) that F-28 did not name.

using Server.Items;

namespace Server;

public sealed class WoodBonus
{
    // Both rows
    public int Durability { get; init; }
    public int LowerLevelReq { get; init; } // stored only: no level system yet (F-12)
    public int LowerStatReq { get; init; }
    public int Luck { get; init; }
    public int DefendChance { get; init; }
    public int RegenMana { get; init; }

    // Weapon rows
    public int Damage { get; init; }
    public int HitChance { get; init; }
    public int SwingSpeed { get; init; }
    public int HitFireArea { get; init; }
    public int HitLowerAttack { get; init; }
    public int HitLeechHits { get; init; }
    public int HitLeechMana { get; init; }
    public int HitLeechStam { get; init; }
    public bool HeartwoodExtra { get; init; }

    // Damage types (percent), set as pinned sets Frostwood's (60 physical / 40 cold); 0 for all leaves the weapon's own
    public int PhysicalDamage { get; init; }
    public int FireDamage { get; init; }
    public int ColdDamage { get; init; }
    public int EnergyDamage { get; init; }

    // Shield rows
    public int PhysicalResist { get; init; }
    public int FireResist { get; init; }
    public int ColdResist { get; init; }
    public int PoisonResist { get; init; }
    public int EnergyResist { get; init; }
    public int AllResists { get; init; }
    public int ReflectPhysical { get; init; }
    public int RegenHits { get; init; }
    public bool SpellChanneling { get; init; } // without the -1 faster casting a runic's spell channeling carries

    public static readonly WoodBonus None = new();
}

public sealed record WoodRow(WoodBonus Normal, WoodBonus ExceptionalAdds);

public static class ClusterFWoodBonuses
{
    // F-28, first table (bows, and by Chase's answer 2 staves and clubs)
    public static WoodRow WeaponRow(CraftResource wood) => wood switch
    {
        CraftResource.Ironwood => new WoodRow(
            new WoodBonus { Durability = 50, LowerLevelReq = 2 },
            new WoodBonus { Damage = 14, LowerStatReq = 20 }
        ),
        CraftResource.Ghostwood => new WoodRow(
            new WoodBonus { HitChance = 6 },
            new WoodBonus { Damage = 15, DefendChance = 5 }
        ),
        CraftResource.Emberbark => new WoodRow(
            new WoodBonus { PhysicalDamage = 50, FireDamage = 50 },
            new WoodBonus { Damage = 16, HitFireArea = 10 }
        ),
        CraftResource.Frostbark => new WoodRow(
            new WoodBonus { PhysicalDamage = 40, ColdDamage = 60 },
            new WoodBonus { Damage = 17, HitLowerAttack = 10 }
        ),
        CraftResource.Shadowbark => new WoodRow(
            new WoodBonus { SwingSpeed = 10 },
            new WoodBonus { Damage = 18, HitChance = 6 }
        ),
        CraftResource.Runewood => new WoodRow(
            new WoodBonus { PhysicalDamage = 50, EnergyDamage = 50, RegenMana = 2 },
            new WoodBonus { Damage = 20, HitLeechMana = 15 }
        ),
        CraftResource.Voidwood => new WoodRow(
            new WoodBonus { HitLeechHits = 15 },
            new WoodBonus { Damage = 22, HitLeechStam = 15 }
        ),
        CraftResource.Starwood => new WoodRow(
            new WoodBonus { Luck = 100, LowerLevelReq = 5 },
            new WoodBonus { Damage = 25, HitChance = 8, HeartwoodExtra = true }
        ),
        _ => null
    };

    // F-28, second table (wooden shields)
    public static WoodRow ShieldRow(CraftResource wood) => wood switch
    {
        CraftResource.Ironwood => new WoodRow(
            new WoodBonus { Durability = 50, LowerLevelReq = 2 },
            new WoodBonus { PhysicalResist = 3, LowerStatReq = 20 }
        ),
        CraftResource.Ghostwood => new WoodRow(
            new WoodBonus { DefendChance = 4 },
            new WoodBonus { EnergyResist = 3, DefendChance = 3 }
        ),
        CraftResource.Emberbark => new WoodRow(
            new WoodBonus { FireResist = 4 },
            new WoodBonus { FireResist = 3, ReflectPhysical = 5 }
        ),
        CraftResource.Frostbark => new WoodRow(
            new WoodBonus { ColdResist = 4 },
            new WoodBonus { ColdResist = 3, ReflectPhysical = 5 }
        ),
        CraftResource.Shadowbark => new WoodRow(
            new WoodBonus { DefendChance = 5 },
            new WoodBonus { PoisonResist = 3, DefendChance = 3 }
        ),
        CraftResource.Runewood => new WoodRow(
            new WoodBonus { EnergyResist = 4, RegenMana = 1 },
            new WoodBonus { SpellChanneling = true }
        ),
        CraftResource.Voidwood => new WoodRow(
            new WoodBonus { AllResists = 1 },
            new WoodBonus { AllResists = 1, RegenHits = 2 }
        ),
        CraftResource.Starwood => new WoodRow(
            new WoodBonus { Luck = 100, LowerLevelReq = 5 },
            new WoodBonus { AllResists = 2, DefendChance = 5 }
        ),
        _ => null
    };

    public static bool IsWoodWeapon(BaseWeapon weapon) =>
        weapon is BaseRanged or BaseStaff or BaseBashing &&
        CraftResources.GetType(weapon.Resource) == CraftResourceType.Wood;

    public static bool IsWoodShield(BaseArmor armor) =>
        armor is BaseShield && CraftResources.GetType(armor.Resource) == CraftResourceType.Wood;

    /// <summary>The end of BaseWeapon.OnCraft (patch): the wood's weapon row, after quality and Resource are set.</summary>
    public static void OnCraft(BaseWeapon weapon)
    {
        if (!Core.ML || !IsWoodWeapon(weapon))
        {
            return;
        }

        var row = WeaponRow(weapon.Resource);

        if (row == null)
        {
            return;
        }

        Apply(weapon, row.Normal);

        if (weapon.Quality == WeaponQuality.Exceptional)
        {
            Apply(weapon, row.ExceptionalAdds);
        }
    }

    /// <summary>The end of BaseArmor.OnCraft (patch): the wood's shield row, after quality and Resource are set.</summary>
    public static void OnCraft(BaseArmor armor)
    {
        if (!Core.ML || !IsWoodShield(armor))
        {
            return;
        }

        var row = ShieldRow(armor.Resource);

        if (row == null)
        {
            return;
        }

        Apply(armor, row.Normal);

        if (armor.Quality == ArmorQuality.Exceptional)
        {
            Apply(armor, row.ExceptionalAdds);
        }
    }

    public static void Apply(BaseWeapon weapon, WoodBonus b)
    {
        weapon.WeaponAttributes.DurabilityBonus += b.Durability;
        weapon.WeaponAttributes.LowerStatReq += b.LowerStatReq;
        weapon.Attributes.Luck += b.Luck;
        weapon.Attributes.DefendChance += b.DefendChance;
        weapon.Attributes.RegenMana += b.RegenMana;
        weapon.Attributes.WeaponDamage += b.Damage;
        weapon.Attributes.AttackChance += b.HitChance;
        weapon.Attributes.WeaponSpeed += b.SwingSpeed;
        weapon.WeaponAttributes.HitFireArea += b.HitFireArea;
        weapon.WeaponAttributes.HitLowerAttack += b.HitLowerAttack;
        weapon.WeaponAttributes.HitLeechHits += b.HitLeechHits;
        weapon.WeaponAttributes.HitLeechMana += b.HitLeechMana;
        weapon.WeaponAttributes.HitLeechStam += b.HitLeechStam;

        if (b.PhysicalDamage + b.FireDamage + b.ColdDamage + b.EnergyDamage > 0)
        {
            weapon.AosElementDamages.Physical = b.PhysicalDamage;
            weapon.AosElementDamages.Fire = b.FireDamage;
            weapon.AosElementDamages.Cold = b.ColdDamage;
            weapon.AosElementDamages.Energy = b.EnergyDamage;
        }

        if (b.HeartwoodExtra)
        {
            HeartwoodExtra(weapon);
        }
    }

    public static void Apply(BaseArmor armor, WoodBonus b)
    {
        armor.ArmorAttributes.DurabilityBonus += b.Durability;
        armor.ArmorAttributes.LowerStatReq += b.LowerStatReq;
        armor.Attributes.Luck += b.Luck;
        armor.Attributes.DefendChance += b.DefendChance;
        armor.Attributes.RegenMana += b.RegenMana;
        armor.Attributes.RegenHits += b.RegenHits;
        armor.Attributes.ReflectPhysical += b.ReflectPhysical;
        armor.PhysicalBonus += b.PhysicalResist + b.AllResists;
        armor.FireBonus += b.FireResist + b.AllResists;
        armor.ColdBonus += b.ColdResist + b.AllResists;
        armor.PoisonBonus += b.PoisonResist + b.AllResists;
        armor.EnergyBonus += b.EnergyResist + b.AllResists;

        if (b.SpellChanneling && armor.Attributes.SpellChanneling == 0)
        {
            armor.Attributes.SpellChanneling = 1;
        }
    }

    /// <summary>Pinned's Heartwood pick (BaseRanged.ApplyHeartwoodBonus, private there): one of six, one roll.</summary>
    public static void HeartwoodExtra(BaseWeapon weapon)
    {
        switch (Utility.Random(6))
        {
            case 0:
                weapon.Attributes.Luck += 40;
                break;
            case 1:
                weapon.WeaponAttributes.DurabilityBonus += 50;
                break;
            case 2:
                weapon.WeaponAttributes.LowerStatReq += 20;
                break;
            case 3:
                weapon.Attributes.WeaponSpeed += 10;
                break;
            case 4:
                weapon.Attributes.AttackChance += 5;
                break;
            case 5:
                weapon.WeaponAttributes.HitLeechHits += 10;
                break;
        }
    }
}
