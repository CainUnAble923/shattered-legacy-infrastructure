using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceOrder : Item, ICommodity
    {
        [Constructible]
        public EssenceOrder(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1153;
        }

        public override int LabelNumber => 1113342; // essence of order

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
