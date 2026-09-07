using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SilverSnakeSkin : Item, ICommodity
    {
        [Constructible]
        public SilverSnakeSkin(int amount = 1) : base(0x5744)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113357; // silver snake skin

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
