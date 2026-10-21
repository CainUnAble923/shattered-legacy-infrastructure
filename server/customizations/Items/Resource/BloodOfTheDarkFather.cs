// cc-P199: ServUO pub57 Scripts/Items/Resource/BloodOfTheDarkFather.cs. Blood of the Dark Father (1157343), hue 2741, a
// stackable commodity. ServUO uses it as an ingredient in three recipe-gated crafts (BlacksmithyCraftRegistrations.cs,
// TailoringCraftRegistrations.cs and TinkeringCraftRegistrations.cs here name them); this file adds only the item.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class BloodOfTheDarkFather : Item, ICommodity
    {
        [Constructible]
        public BloodOfTheDarkFather(int amount = 1) : base(0x9D7F)
        {
            Hue = 2741;
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1157343; // Blood of the Dark Father

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
