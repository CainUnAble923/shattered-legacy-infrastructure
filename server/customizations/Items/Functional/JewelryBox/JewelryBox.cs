// cc-P199: ServUO pub57 Scripts/Items/Functional/JewelryBox/JewelryBox.cs. The jewelry box (art 0x9F1C/0x9F1D, 10
// stones, dyeable): a house container for up to 500 rings, bracelets, necklaces, earrings and talismans that must be
// locked down or secured to use. It never shows its contents; double-clicking opens its gump instead (JewelryBoxGump.cs),
// and dropping a piece in reopens the gump a second later. Who may add or take follows its secure level (co-owners by
// default), set from the house's context menu entry. Its contents weigh 30% of their total, at least 1.
//
// Differences from ServUO:
// - The "This container is secure." line is pinned's Item.SendLocalizedMessageTo with hue 946, the overhead message
//   from the item that ServUO's PrivateOverheadMessage sends (JewelryBox.cs:115); pinned's Item has no
//   PrivateOverheadMessage.
// - Weight = 10.0 in ServUO's constructor is DefaultWeight here.
// - ServUO's BaseHouse also leaves the box out of the house item count and decay (BaseHouse.cs:372-384), and its
//   HarvestSystem refuses to chop it (HarvestSystem.cs:755). Those are lists in ServUO files; nothing here adds them.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Multis;
using Server.Network;

namespace Server.Items
{
    [Furniture]
    [Flippable(0x9F1C, 0x9F1D)]
    [SerializationGenerator(0, false)]
    public partial class JewelryBox : Container, IDyable
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        [SerializableField(1)]
        private JewelryBoxFilter _filter;

        private static readonly Type[] _acceptList =
        {
            typeof(BaseRing), typeof(BaseBracelet), typeof(BaseNecklace), typeof(BaseEarrings), typeof(BaseTalisman)
        };

        [Constructible]
        public JewelryBox() : base(0x9F1C)
        {
            _filter = new JewelryBoxFilter(this);
            _level = SecureLevel.CoOwners;
        }

        public override int LabelNumber => 1157694; // Jewelry Box

        public override double DefaultWeight => 10.0;

        public override int DefaultMaxItems => 500;

        public bool IsFull => DefaultMaxItems <= Items.Count;

        public override bool DisplaysContent => false;

        public bool Dye(Mobile from, DyeTub sender)
        {
            if (Deleted)
            {
                return false;
            }

            Hue = sender.DyedHue;

            return true;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                base.OnDoubleClick(from);
            }

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
            else if (!IsLockedDown && !IsSecure)
            {
                from.SendLocalizedMessage(1157727); // The jewelry box must be secured before you can use it.
            }
            else
            {
                from.SendGump(new JewelryBoxGump(from, this));
            }
        }

        public bool CheckAccessible(Mobile from, Item item)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                return true; // Staff can access anything
            }

            var house = BaseHouse.FindHouseAt(item);

            if (house == null)
            {
                return false;
            }

            return _level switch
            {
                SecureLevel.Owner    => house.IsOwner(from),
                SecureLevel.CoOwners => house.IsCoOwner(from),
                SecureLevel.Friends  => house.IsFriend(from),
                SecureLevel.Anyone   => true,
                SecureLevel.Guild    => house.IsGuildMember(from),
                _                    => false
            };
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        public bool IsAccept(Item item)
        {
            foreach (var type in _acceptList)
            {
                if (item.GetType().IsSubclassOf(type))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (!IsLockedDown && !IsSecure)
            {
                from.SendLocalizedMessage(1157727); // The jewelry box must be secured before you can use it.
                return false;
            }

            if (!CheckAccessible(from, this))
            {
                SendLocalizedMessageTo(from, 1010563, 946); // This container is secure.
                return false;
            }

            if (!IsAccept(dropped))
            {
                from.SendLocalizedMessage(1157724); // This is not a ring, bracelet, necklace, earring, or talisman.
                return false;
            }

            if (IsFull)
            {
                from.SendLocalizedMessage(1157723); // The jewelry box is full.
                return false;
            }

            Timer.DelayCall(TimeSpan.FromSeconds(1), () => from.SendGump(new JewelryBoxGump(from, this)));
            return base.OnDragDrop(from, dropped);
        }

        public override int GetTotal(TotalType type)
        {
            if (type == TotalType.Weight)
            {
                var weight = base.GetTotal(type);

                if (weight > 0)
                {
                    return (int)Math.Max(1, base.GetTotal(type) * 0.3);
                }
            }

            return base.GetTotal(type);
        }
    }
}
