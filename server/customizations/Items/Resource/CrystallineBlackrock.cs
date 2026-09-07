using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class CrystallineBlackrock : Item, ICommodity
    {
        [Constructible]
        public CrystallineBlackrock(int amount = 1) : base(0x5732)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113344; // crystalline blackrock

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
