using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssencePassion : Item, ICommodity
    {
        [Constructible]
        public EssencePassion(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1161;
        }

        public override int LabelNumber => 1113326; // essence of passion

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
