using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssencePrecision : Item, ICommodity
    {
        [Constructible]
        public EssencePrecision(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1158;
        }

        public override int LabelNumber => 1113327; // essence of precision

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
