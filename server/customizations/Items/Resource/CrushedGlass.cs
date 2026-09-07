using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class CrushedGlass : Item, ICommodity
    {
        [Constructible]
        public CrushedGlass(int amount = 1) : base(0x573B)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113351; // crushed glass

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
