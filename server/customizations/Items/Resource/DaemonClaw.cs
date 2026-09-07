using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class DaemonClaw : Item, ICommodity
    {
        [Constructible]
        public DaemonClaw(int amount = 1) : base(0x5721)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113330; // daemon claw

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
