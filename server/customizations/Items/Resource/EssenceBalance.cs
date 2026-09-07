using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceBalance : Item, ICommodity
    {
        [Constructible]
        public EssenceBalance(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1268;
        }

        public override int LabelNumber => 1113324; // essence of balance

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
