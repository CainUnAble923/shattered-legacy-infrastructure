using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceControl : Item, ICommodity
    {
        [Constructible]
        public EssenceControl(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1165;
        }

        public override int LabelNumber => 1113340; // essence of control

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
