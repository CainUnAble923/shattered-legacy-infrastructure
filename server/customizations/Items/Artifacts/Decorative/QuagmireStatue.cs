using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/QuagmireStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class QuagmireStatue : Item
    {
        [Constructible]
        public QuagmireStatue() : base(0x2614)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1073195;
    }
}
