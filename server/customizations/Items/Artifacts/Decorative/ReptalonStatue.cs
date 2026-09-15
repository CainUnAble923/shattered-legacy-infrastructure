using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/ReptalonStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ReptalonStatue : Item
    {
        [Constructible]
        public ReptalonStatue() : base(0x2D95)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1073192;
    }
}
