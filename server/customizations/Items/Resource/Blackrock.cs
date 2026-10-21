// cc-P199: ServUO pub57 Scripts/Items/Resource/Blackrock.cs. A piece of blackrock, hue 1954: a large piece (0x136C,
// 1153837) or a small one (0x1EA7, 1150016), picked at random when made. Not stackable, no behavior. The label follows
// the current ItemID, so a GM who changes the art gets 1153836 "a piece of blackrock" (ServUO Blackrock.cs:28).
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class Blackrock : Item
    {
        [Constructible]
        public Blackrock() : base(Utility.RandomList(0x136C, 0x1EA7)) => Hue = 1954;

        public override int LabelNumber => ItemID switch
        {
            0x136C => 1153837, // a large piece of blackrock
            0x1EA7 => 1150016, // a small piece of blackrock
            _      => 1153836  // a piece of blackrock
        };
    }
}
