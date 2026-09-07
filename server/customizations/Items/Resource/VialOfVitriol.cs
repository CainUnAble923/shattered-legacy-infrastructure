using ModernUO.Serialization;

namespace Server.Items
{
    [TypeAlias("Server.Items.VialVitirol")]
    [SerializationGenerator(0, false)]
    public partial class VialOfVitriol : Item, ICommodity
    {
        [Constructible]
        public VialOfVitriol(int amount = 1) : base(0x5722)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113331; // vial of vitriol

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
