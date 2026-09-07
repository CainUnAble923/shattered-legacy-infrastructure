using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ChagaMushroom : Item, ICommodity
    {
        [Constructible]
        public ChagaMushroom(int amount = 1) : base(0x5743)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113356; // chaga mushroom

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
