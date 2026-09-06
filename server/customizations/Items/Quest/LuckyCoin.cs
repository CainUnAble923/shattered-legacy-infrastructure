using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class LuckyCoin : Item, ICommodity
    {
        [Constructible]
        public LuckyCoin(int amount = 1) : base(0xF87)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1174;
        }

        public override int LabelNumber => 1113366; // lucky coin

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
