using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class AloronsLegs : TigerPeltLegs
    {
        [Constructible]
        public AloronsLegs()
        {
            // AbsorptionAttributes.EaterCold = 2 is omitted: ModernUO has no SAAbsorptionAttributes
            // and porting it pulls in three further missing subsystems. See notes/s1-armour-sets.md.
            Attributes.BonusDex = 4;
            Attributes.BonusStam = 4;
            Attributes.RegenStam = 3;

            SetAttributes.BonusMana = 15;
            SetAttributes.LowerManaCost = 20;
            SetSelfRepair = 3;

            SetPhysicalBonus = 8;
            SetFireBonus = 8;
            SetColdBonus = 9;
            SetPoisonBonus = 8;
            SetEnergyBonus = 8;
        }

        public override int LabelNumber => 1156243; // Aloron's Armor

        public override SetItem SetID => SetItem.Aloron;
        public override int Pieces => 4;

        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 6;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
    }
}
