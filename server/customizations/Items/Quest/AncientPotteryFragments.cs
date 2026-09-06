using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class AncientPotteryFragments : Item
    {
        [Constructible]
        public AncientPotteryFragments() : base(0x2243)
        {
            Hue = 2108;
        }

        public override int LabelNumber => 1112990; // Ancient Pottery fragments
    }
}
