using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class PerfectBanana : Item
    {
        [Constructible]
        public PerfectBanana(int amount = 1) : base(5922)
        {
            Hue = 1119;
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1156730; // Perfect Bananas
    }
}
