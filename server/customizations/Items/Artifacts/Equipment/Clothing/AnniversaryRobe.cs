using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/AnniversaryRobe.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x4B9D, 0x4B9E)]
    [SerializationGenerator(0, false)]
    public partial class AnniversaryRobe : BaseOuterTorso
    {
        [Constructible]
        public AnniversaryRobe(int hue = 0x455) : base(0x4B9D, hue)
        {
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1153496;
    }
}
