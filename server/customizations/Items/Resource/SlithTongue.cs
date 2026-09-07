using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SlithTongue : Item, ICommodity
    {
        [Constructible]
        public SlithTongue(int amount = 1) : base(0x5746)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113359; // slith tongue

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
