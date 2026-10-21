// cc-P199: ServUO pub57 Scripts/Items/Resource/FeyWings.cs. Fey wings (1113332), a stackable commodity of weight 0.1.
// ServUO uses it as a tailoring ingredient (TailoringCraftRegistrations.cs here names the recipes) and as a Clean Up
// Britannia turn-in; this file adds only the item.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class FeyWings : Item, ICommodity
    {
        [Constructible]
        public FeyWings(int amount = 1) : base(0x5726)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113332; // fey wings
        public override double DefaultWeight => 0.1;

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
