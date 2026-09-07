using ModernUO.Serialization;

namespace Server.Items
{
    [TypeAlias("Server.Items.LavaSerpenCrust")]
    [SerializationGenerator(0, false)]
    public partial class LavaSerpentCrust : Item, ICommodity
    {
        [Constructible]
        public LavaSerpentCrust(int amount = 1) : base(0x572D)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113336; // lava serpent crust

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
