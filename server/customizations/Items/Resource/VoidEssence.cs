// cc-P199: ServUO pub57 Scripts/Items/Resource/VoidEssence.cs. Void essence (1112327), a stackable commodity.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class VoidEssence : Item, ICommodity
    {
        [Constructible]
        public VoidEssence(int amount = 1) : base(0x4007)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1112327; // void essence

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
