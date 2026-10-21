// cc-P199: ServUO pub57 Scripts/Items/Resource/OceanSapphire.cs. Ocean sapphire (1159162), hue 1917, a stackable
// commodity. The art 0xA414 is named "fireball" in the client's tiledata; the label is what players see.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class OceanSapphire : Item, ICommodity
    {
        [Constructible]
        public OceanSapphire(int amount = 1) : base(0xA414)
        {
            Hue = 1917;
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1159162; // ocean sapphire

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
