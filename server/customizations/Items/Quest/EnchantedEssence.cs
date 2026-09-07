using ModernUO.Serialization;

namespace Server.Items
{
    [TypeAlias("Server.Items.EnchantEssence")]
    [SerializationGenerator(0, false)]
    public partial class EnchantedEssence : Item, ICommodity
    {
        [Constructible]
        public EnchantedEssence(int amount = 1) : base(0x2DB2)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1031698; // Enchaned Essence

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
