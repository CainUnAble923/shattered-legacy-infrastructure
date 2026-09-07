using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class MagicalResidue : Item, ICommodity
    {
        [Constructible]
        public MagicalResidue(int amount = 1) : base(0x2DB1)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1031697; // Magical Residue

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
