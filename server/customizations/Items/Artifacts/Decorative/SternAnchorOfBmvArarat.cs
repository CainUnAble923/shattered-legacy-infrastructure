using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/SternAnchorOfBmvArarat.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class SternAnchorOfBmvArarat : BaseDecorationArtifact
    {
        [Constructible]
        public SternAnchorOfBmvArarat() : base(0x14F7)
        {
            Hue = 2959;
        }

        public override string DefaultName => "Stern Anchor of the BMV Ararat";
        public override double DefaultWeight => 10.0;
        public override int ArtifactRarity => 8;
    }
}
