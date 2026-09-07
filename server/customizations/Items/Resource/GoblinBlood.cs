using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class GoblinBlood : Item, ICommodity
    {
        [Constructible]
        public GoblinBlood(int amount = 1) : base(0x572C)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113335; // goblin blood

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
