using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class BottleIchor : Item, ICommodity
    {
        [Constructible]
        public BottleIchor(int amount = 1) : base(0x5748)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113361; // bottle of ichor

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
