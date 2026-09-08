using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/BakeKitsuneStatue.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class BakeKitsuneStatue : Item
    {
        [Constructible]
        public BakeKitsuneStatue() : base(0x2763) => LootType = LootType.Blessed;

        public override double DefaultWeight => 1.0;

        // A Bake Kitsune Contribution Statue from the Britannia Royal Zoo.
        public override int LabelNumber => 1073189;
    }
}
