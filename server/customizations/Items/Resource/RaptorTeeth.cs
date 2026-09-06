using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class RaptorTeeth : Item, ICommodity
    {
        [Constructible]
        public RaptorTeeth(int amount = 1) : base(0x5747)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113360; // raptor teeth

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
