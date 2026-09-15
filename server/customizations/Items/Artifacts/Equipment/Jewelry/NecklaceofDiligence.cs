using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/NecklaceofDiligence.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class NecklaceofDiligence : SilverNecklace
    {
        [Constructible]
        public NecklaceofDiligence()
        {
            Hue = 221;
            Attributes.RegenMana = 1;
            Attributes.BonusInt = 5;
        }

        public override int LabelNumber => 1113137;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
