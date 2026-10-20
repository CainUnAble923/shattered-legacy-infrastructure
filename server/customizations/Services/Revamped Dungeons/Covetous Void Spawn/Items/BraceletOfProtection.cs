// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/BraceletOfProtection.cs (cc-P194).
//
// A gold bracelet, hue 1157: hit points 5, hit point regeneration 10, defence chance 5, and one damage eater
// (kinetic, fire, cold, poison, energy or all) at 15. Vela sells it (1840 points, the antique one) and the void
// spawn drops the non-antique one.
//
// ServUO puts it on GoldBracelet; here it is on BaseSetJewel, the shard's carrier for AbsorptionAttributes (the
// final-parent rule of AGENTS.md: a GoldBracelet cannot carry an eater), with GoldBracelet's one member inlined
// (DefaultWeight 0.1; Items/Jewels/Bracelet.cs). Deviation (notes): NegativeAttributes.Antique has no pinned or
// shard member, so the antique copy has 250 maximum hit points and nothing else marks it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BraceletOfProtection : BaseSetJewel
{
    [Constructible]
    public BraceletOfProtection() : this(true)
    {
    }

    [Constructible]
    public BraceletOfProtection(bool antique) : base(0x1086, Layer.Bracelet)
    {
        Hue = 1157;
        Attributes.BonusHits = 5;
        Attributes.RegenHits = 10;
        Attributes.DefendChance = 5;

        switch (Utility.Random(6))
        {
            case 0:
                AbsorptionAttributes.EaterKinetic = 15;
                break;
            case 1:
                AbsorptionAttributes.EaterFire = 15;
                break;
            case 2:
                AbsorptionAttributes.EaterCold = 15;
                break;
            case 3:
                AbsorptionAttributes.EaterPoison = 15;
                break;
            case 4:
                AbsorptionAttributes.EaterEnergy = 15;
                break;
            case 5:
                AbsorptionAttributes.EaterDamage = 15;
                break;
        }

        MaxHitPoints = antique ? 250 : 255;
        HitPoints = MaxHitPoints;
    }

    public override double DefaultWeight => 0.1;

    public override int LabelNumber => 1152730; // Bracelet of Protection
}
