using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class PowderedIron : Item
    {
        [Constructible]
        public PowderedIron(int amount = 1) : base(0x573D)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113353; // powdered iron
    }
}
