using ModernUO.Serialization;

namespace Server.Items
{
    [TypeAlias("Server.Items.ElvenFletchings")]
    [SerializationGenerator(0, false)]
    public partial class ElvenFletching : Item, ICommodity
    {
        [Constructible]
        public ElvenFletching(int amount = 1) : base(0x5737)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113346; // elven fletching

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
