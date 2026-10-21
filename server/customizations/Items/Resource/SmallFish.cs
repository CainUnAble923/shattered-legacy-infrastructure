// cc-P199: ServUO pub57 Scripts/Items/Resource/SmallFish.cs. A small fish on 0x0DD6 or 0x0DD7 at random; its name is
// the tiledata name ("small fish"). Not stackable and not food in ServUO either; no behavior.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SmallFish : Item
    {
        [Constructible]
        public SmallFish() : base(Utility.Random(0x0DD6, 2))
        {
        }
    }
}
