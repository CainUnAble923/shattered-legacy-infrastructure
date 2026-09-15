using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/PolarBearStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class PolarBearStatue : Item
    {
        [Constructible]
        public PolarBearStatue() : base(0x20E1)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1073193;
    }
}
