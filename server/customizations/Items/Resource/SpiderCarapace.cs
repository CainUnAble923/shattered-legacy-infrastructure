using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SpiderCarapace : Item, ICommodity
    {
        [Constructible]
        public SpiderCarapace(int amount = 1) : base(0x5720)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113329; // spider carapace

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
