using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/WolfStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class WolfStatue : Item
    {
        [Constructible]
        public WolfStatue() : base(0x25D3)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1073190;
    }
}
