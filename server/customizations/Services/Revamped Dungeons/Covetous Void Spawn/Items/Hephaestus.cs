// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/Hephaestus.cs (cc-P194).
//
// A heater shield, hue 1910: spell channeling, reflect physical 15, defence chance 15, faster casting 1, lower
// mana cost 8, physical resist 15, self repair 5, Parrying 10. Vela sells the antique one (2000 points) and the
// void spawn drops the non-antique one.
//
// Where the port met the shard's rule: F-27 (cc-P32) keeps Self Repair out of the random loot table; an artifact's
// fixed Self Repair 5 is OSI's and stays. Deviations (notes): NegativeAttributes.Antique has no pinned or shard
// member (the antique copy has 250 maximum hit points and nothing else marks it); GargishHephaestus needs a
// LargePlateShield base pinned does not have; [Alterable] has no pinned counterpart. Not the shard's
// HammerOfHephaestus, which is a different item.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Hephaestus : HeaterShield
{
    [Constructible]
    public Hephaestus() : this(true)
    {
    }

    [Constructible]
    public Hephaestus(bool antique)
    {
        Hue = 1910;
        Attributes.SpellChanneling = 1;
        Attributes.ReflectPhysical = 15;
        Attributes.DefendChance = 15;
        Attributes.CastSpeed = 1;
        Attributes.LowerManaCost = 8;

        PhysicalBonus = 15;
        ArmorAttributes.SelfRepair = 5;

        SkillBonuses.SetValues(0, SkillName.Parry, 10.0);

        MaxHitPoints = antique ? 250 : 255;
        HitPoints = MaxHitPoints;
    }

    public override int LabelNumber => 1152909; // Hephaestus
}
