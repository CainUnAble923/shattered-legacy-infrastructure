using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class UndyingFlesh : Item, ICommodity
    {
        [Constructible]
        public UndyingFlesh(int amount = 1) : base(0x5731)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113337; // undying flesh

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
