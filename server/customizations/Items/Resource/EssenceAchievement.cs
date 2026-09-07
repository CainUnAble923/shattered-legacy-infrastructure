using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssenceAchievement : Item, ICommodity
    {
        [Constructible]
        public EssenceAchievement(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1724;
        }

        public override int LabelNumber => 1113325; // essence of achievement

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
