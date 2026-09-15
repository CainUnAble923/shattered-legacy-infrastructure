using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/HumanFeyLeggings.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class HumanFeyLeggings : ChainLegs
    {
        [Constructible]
        public HumanFeyLeggings()
        {
            Attributes.BonusHits = 6;
            Attributes.DefendChance = 20;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1075041;
        public override int BasePhysicalResistance => 12;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 19;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
