using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/CloakOfLife.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2FB9, 0x3173)]
    [SerializationGenerator(0, false)]
    public partial class CloakOfLife : BaseOuterTorso
    {
        [Constructible]
        public CloakOfLife() : base(0x2FB9)
        {
            Hue = 0x21;
            Attributes.RegenHits = 1;
            Attributes.BonusHits = 3;
        }

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1112880;
    }
}
