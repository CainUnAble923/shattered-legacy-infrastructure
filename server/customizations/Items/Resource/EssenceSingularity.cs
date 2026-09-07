using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceSingularity : Item, ICommodity
    {
        [Constructible]
        public EssenceSingularity(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1109;
        }

        public override int LabelNumber => 1113341; // essence of singularity

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
