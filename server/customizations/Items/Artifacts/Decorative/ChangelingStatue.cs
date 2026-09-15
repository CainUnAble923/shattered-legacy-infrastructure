using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/ChangelingStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ChangelingStatue : Item
    {
        [Constructible]
        public ChangelingStatue() : base(0x2D8A)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1073191;
    }
}
