using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/FigureheadOfBmvArarat.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class FigureheadOfBmvArarat : BaseDecorationArtifact
    {
        [Constructible]
        public FigureheadOfBmvArarat() : base(0x2D0E)
        {
            Hue = 2968;
        }

        public override string DefaultName => "Figurehead Of The Bmv Ararat";
        public override double DefaultWeight => 10.0;
        public override int ArtifactRarity => 8;
    }
}
