using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/BronzedArmoroftheValkyrie.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class BronzedArmorValkyrie : FemaleLeatherChest
    {
        [Constructible]
        public BronzedArmorValkyrie()
        {
            Attributes.BonusHits = 5;
            Attributes.BonusStr = 5;
            Attributes.BonusDex = 5;
            Attributes.BonusStam = 5;
            Attributes.RegenStam = 3;
            Attributes.LowerManaCost = 10;
            Hue = 1863;
        }

        public override int LabelNumber => 1149957;
        public override int BasePhysicalResistance => 11;
        public override int BaseFireResistance => 14;
        public override int BaseColdResistance => 8;
        public override int BasePoisonResistance => 11;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
