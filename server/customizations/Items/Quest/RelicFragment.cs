using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class RelicFragment : Item, ICommodity
    {
        [Constructible]
        public RelicFragment(int amount = 1) : base(0x2DB3)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1031699; // Relic Fragment

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
