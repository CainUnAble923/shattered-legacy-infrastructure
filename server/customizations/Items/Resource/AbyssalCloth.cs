// cc-P199: ServUO pub57 Scripts/Items/Resource/AbyssalCloth.cs. Abyssal cloth (1113350), a stackable hued bolt
// commodity; scissors turn each piece into one bandage of the same hue. Same shape as pinned's Cloth
// (Items/Resources/Tailor/Cloth.cs), whose single-click line and Scissor body this mirrors.
//
// No deliberate differences from ServUO. The single-click message goes out through pinned's
// NetState.SendMessageLocalized (ServUO AbyssalCloth.cs:70 sends a MessageLocalized packet); same cliloc, hue and args.

using ModernUO.Serialization;
using Server.Network;

namespace Server.Items
{
    [Flippable(0x1765, 0x1767)]
    [SerializationGenerator(0, false)]
    public partial class AbyssalCloth : Item, ICommodity, IScissorable
    {
        [Constructible]
        public AbyssalCloth(int amount = 1) : base(0x1767)
        {
            Stackable = true;
            Amount = amount;
            Hue = 2075;
        }

        public override int LabelNumber => 1113350; // abyssal cloth

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;

        public override void OnSingleClick(Mobile from)
        {
            var number = Amount == 1 ? 1049124 : 1049123;

            from.NetState.SendMessageLocalized(Serial, ItemID, MessageType.Regular, 0x3B2, 3, number, "", Amount.ToString());
        }

        public bool Scissor(Mobile from, Scissors scissors)
        {
            if (Deleted || !from.CanSee(this))
            {
                return false;
            }

            ScissorHelper(from, new Bandage(), 1);

            return true;
        }
    }
}
