using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class DelicateScales : Item, ICommodity
    {
        [Constructible]
        public DelicateScales(int amount = 1) : base(0x573A)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113349; // delicate scales

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
