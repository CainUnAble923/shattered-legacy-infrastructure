// cc-P199: ServUO pub57 Scripts/Items/Resource/EssencePersistence.cs. Essence of persistence (1113343), hue 37, a
// stackable imbuing commodity on the shared essence art 0x571C, like the other Essence* files here.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EssencePersistence : Item, ICommodity
    {
        [Constructible]
        public EssencePersistence(int amount = 1) : base(0x571C)
        {
            Stackable = true;
            Amount = amount;
            Hue = 37;
        }

        public override int LabelNumber => 1113343; // essence of persistence

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
