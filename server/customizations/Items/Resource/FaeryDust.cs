using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class FaeryDust : Item, ICommodity
    {
        [Constructible]
        public FaeryDust(int amount = 1) : base(0x5745)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113358; // faery dust

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
