using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class VoidOrb : Item, ICommodity
    {
        [Constructible]
        public VoidOrb(int amount = 1) : base(0x573E)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113354; // void orb

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
