using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/LightsRampart.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class LightsRampart : MetalShield
    {
        [Constructible]
        public LightsRampart()
        {
            Hue = 1272;
            Attributes.SpellChanneling = 1;
            Attributes.DefendChance = 20;
        }

        public override int LabelNumber => 1112407;
        public override int ArtifactRarity => 11;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 13;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 3;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
    }
}
