using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceFeeling : Item, ICommodity
    {
        [Constructible]
        public EssenceFeeling(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 455;
        }

        public override int LabelNumber => 1113339; // essence of feeling

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
