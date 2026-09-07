using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class BouraPelt : Item, ICommodity
    {
        [Constructible]
        public BouraPelt(int amount = 1) : base(0x5742)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113355; // boura pelt

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
