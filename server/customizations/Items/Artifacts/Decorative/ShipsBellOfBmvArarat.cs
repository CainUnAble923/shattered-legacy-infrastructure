using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/ShipsBellOfBmvArarat.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ShipsBellOfBmvArarat : BaseDecorationArtifact
    {
        [Constructible]
        public ShipsBellOfBmvArarat() : base(0x4C5E)
        {
            Hue = 2968;
        }

        public override string DefaultName => "Ship's Bell Of The Bmv Ararat";
        public override double DefaultWeight => 10.0;
        public override int ArtifactRarity => 8;
    }
}
