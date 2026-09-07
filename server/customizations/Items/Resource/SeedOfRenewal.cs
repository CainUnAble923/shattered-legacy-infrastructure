using ModernUO.Serialization;

namespace Server.Items
{
    [TypeAlias("Server.Items.SeedRenewal")]
    [SerializationGenerator(0, false)]
    public partial class SeedOfRenewal : Item, ICommodity
    {
        [Constructible]
        public SeedOfRenewal(int amount = 1) : base(0x5736)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113345; // seed of renewal

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
