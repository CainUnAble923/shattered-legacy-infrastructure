using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/GiantSteps.cs (CC9). ServUO derives from its
    // GargishStoneLegs (0x28A); ModernUO ships that item as GargishStoneLegsType1.
    [SerializationGenerator(0, false)]
    public partial class GiantSteps : GargishStoneLegsType1
    {
        [Constructible]
        public GiantSteps()
        {
            Hue = 656;
            Attributes.BonusStr = 5;
            Attributes.BonusDex = 5;
            Attributes.BonusHits = 5;
            Attributes.RegenHits = 2;
            Attributes.WeaponDamage = 10;
        }

        public override int LabelNumber => 1113537; // Giant Steps

        public override int BasePhysicalResistance => 18;
        public override int BaseFireResistance => 16;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 12;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
