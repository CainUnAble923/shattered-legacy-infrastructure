using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceDiligence : Item, ICommodity
    {
        [Constructible]
        public EssenceDiligence(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1166;
        }

        public override int LabelNumber => 1113338; // essence of diligence

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
