// cc-P199: ServUO pub57 Scripts/Items/Tools/BegFishingPole.cs. A begging reward (ServUO Skills/Begging.cs:216 hands one
// out): pinned's stock FishingPole with "Acquired by begging" (1075129) on its properties, like the cc-P128 begging foods
// (BegCheeseWedge and the rest under Items/Consumables). Pinned's Begging has no reward table, so until that skill is
// ported only staff can make one ([add BegFishingPole).
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class BegFishingPole : FishingPole
    {
        [Constructible]
        public BegFishingPole()
        {
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add(1075129); // Acquired by begging
        }
    }
}
