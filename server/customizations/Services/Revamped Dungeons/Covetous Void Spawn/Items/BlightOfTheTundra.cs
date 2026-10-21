// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/BlightOfTheTundra.cs (cc-P194).
//
// A composite bow (and its gargish twin, a soul glaive), hue 1165, 100% fire: strength 5, stamina regeneration
// 10, hit chance 15, swing speed 45, damage 50, cold resist 15, a random slayer. Vela sells the antique ones (2515
// points) and the void spawn drops the non-antique bow.
//
// Deviation (notes): NegativeAttributes.Antique has no pinned or shard member, so the antique copy has 250 maximum
// hit points and nothing else marks it. The [Alterable] attribute has no pinned counterpart.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlightOfTheTundra : CompositeBow
{
    [Constructible]
    public BlightOfTheTundra() : this(true)
    {
    }

    [Constructible]
    public BlightOfTheTundra(bool antique)
    {
        Attributes.BonusStr = 5;
        Attributes.RegenStam = 10;
        Attributes.AttackChance = 15;
        Attributes.WeaponSpeed = 45;
        Attributes.WeaponDamage = 50;

        Slayer = BaseRunicTool.GetRandomSlayer();

        WeaponAttributes.ResistColdBonus = 15;
        AosElementDamages.Fire = 100;
        Hue = 1165;

        MaxHitPoints = antique ? 250 : 255;
        HitPoints = MaxHitPoints;
    }

    public override int LabelNumber => 1152727; // Blight of the Tundra
}

[SerializationGenerator(0, false)]
public partial class GargishBlightOfTheTundra : SoulGlaive
{
    [Constructible]
    public GargishBlightOfTheTundra() : this(true)
    {
    }

    [Constructible]
    public GargishBlightOfTheTundra(bool antique)
    {
        Attributes.BonusStr = 5;
        Attributes.RegenStam = 10;
        Attributes.AttackChance = 15;
        Attributes.WeaponSpeed = 45;
        Attributes.WeaponDamage = 50;

        Slayer = BaseRunicTool.GetRandomSlayer();

        WeaponAttributes.ResistColdBonus = 15;
        AosElementDamages.Fire = 100;
        Hue = 1165;

        MaxHitPoints = antique ? 250 : 255;
        HitPoints = MaxHitPoints;
    }

    public override int LabelNumber => 1152727; // Blight of the Tundra
}
