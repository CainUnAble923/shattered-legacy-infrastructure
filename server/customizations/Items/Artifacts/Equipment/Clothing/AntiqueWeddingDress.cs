using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/AntiqueWeddingDress.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class AntiqueWeddingDress : PlainDress
    {
        [Constructible]
        public AntiqueWeddingDress()
        {
            Hue = 2953;
        }

        public override int LabelNumber => 1149958;
    }
}
