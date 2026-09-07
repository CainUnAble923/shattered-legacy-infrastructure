using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ArcanicRuneStone : Item, ICommodity
    {
        [Constructible]
        public ArcanicRuneStone(int amount = 1) : base(0x573C)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113352; // arcanic rune stone

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
