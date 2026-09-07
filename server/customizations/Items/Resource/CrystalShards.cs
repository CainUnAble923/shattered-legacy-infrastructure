using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class CrystalShards : Item, ICommodity
    {
        [Constructible]
        public CrystalShards(int amount = 1) : base(0x5738)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113347; // crystal shards

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
