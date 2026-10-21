// cc-P199: ServUO pub57 Scripts/Items/Tools/ChestOfSending.cs. A blessed gothic chest (art 0x4910/0x4911, "a chest of
// sending") that, locked down or secured in a house and used from inside the house, sends a targeted item from the
// backpack to the user's bank box for one charge. 50 charges at most, one back about every 11-13 hours. Who may use it
// follows its secure level (co-owners by default); staff always may.
//
// Differences from ServUO:
// - The recharge. ServUO adds the charge while saving the world (ChestOfSending.cs:212-216): at each save past
//   NextRecharge it adds one and sets the next 11-13 hours on. Pinned's generated serializer has no hook in a save, so
//   the same rule runs when the chest is used instead (CheckRecharge, from a double-click and from the send target),
//   catching up one charge for every interval that has passed, as the saves would have. NextRecharge is saved here;
//   ServUO never saves it (Serialize writes level and charges only), so every ServUO restart grants a charge at the
//   first save. The property list shows the stored count, which can lag until the chest is next used.
// - The disabled context entry: ServUO's CMEFlags.Disabled (ChestOfSending.cs:183-184) is pinned's Enabled = false.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Quests;
using Server.Gumps;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Targeting;

namespace Server.Items
{
    [Flippable(0x4910, 0x4911)]
    [SerializationGenerator(0, false)]
    public partial class ChestOfSending : Item, ISecurable
    {
        public static readonly int MaxCharges = 50;

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        [SerializableField(2)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private DateTime _nextRecharge;

        [Constructible]
        public ChestOfSending() : base(0x4910)
        {
            _level = SecureLevel.CoOwners;
            _charges = 50;
            LootType = LootType.Blessed;
        }

        [SerializableProperty(1)]
        [CommandProperty(AccessLevel.GameMaster)]
        public int Charges
        {
            get => _charges;
            set
            {
                if (value > MaxCharges)
                {
                    _charges = MaxCharges;
                }
                else if (value < 0)
                {
                    _charges = 0;
                }
                else
                {
                    _charges = value;
                }

                InvalidateProperties();
                this.MarkDirty();
            }
        }

        public override int LabelNumber => 1150418; // a chest of sending

        // ServUO's save-time rule (ChestOfSending.cs:212-216), run on use: one charge for each 11-13 hour interval passed.
        public void CheckRecharge()
        {
            while (NextRecharge < Core.Now)
            {
                Charges++;

                var from = NextRecharge == DateTime.MinValue || Charges >= MaxCharges ? Core.Now : NextRecharge;
                NextRecharge = from + TimeSpan.FromHours(Utility.RandomMinMax(11, 13));
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            CheckRecharge();

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
            else if (!from.Region.IsPartOf<HouseRegion>())
            {
                from.SendLocalizedMessage(502092); // You must be in your house to do this.
            }
            else if (!IsLockedDown && !IsSecure)
            {
                from.SendLocalizedMessage(1112573); // This must be locked down or secured in order to use it.
            }
            else if (_charges == 0)
            {
                from.SendLocalizedMessage(1019073); // This item is out of charges.
            }
            else if (CheckAccessible(from, this))
            {
                from.Target = new SendTarget(this);
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060741, _charges.ToString()); // charges: ~1_val~
            list.Add(1150598);                      // auto recharge
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

        // The send itself, after ServUO's region and chest checks (ChestOfSending.cs:126-161).
        public void TrySend(Mobile from, Item item)
        {
            CheckRecharge();

            if (!item.IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1054107); // This item must be in your backpack.
            }
            else if (item is Container or BagOfSending or ChestOfSending)
            {
                from.SendLocalizedMessage(1150420, "#1150424"); // You cannot send a container through the ~1_NAME~.
            }
            else if (!IsLockedDown && !IsSecure)
            {
                from.SendLocalizedMessage(1112573); // This must be locked down or secured in order to use it.
            }
            else if (Charges == 0)
            {
                from.SendLocalizedMessage(1019073); // This item is out of charges.
            }
            else if (!CheckAccessible(from, this))
            {
            }
            else if (InSecureTrade)
            {
                from.SendLocalizedMessage(1150422, "#1150424"); // The ~1_NAME~ will not function while being traded.
            }
            else if (!item.VerifyMove(from) || item is QuestItem)
            {
                from.SendLocalizedMessage(1150421, "#1150424"); // The ~1_NAME~ rejects that item.
            }
            else if (!from.BankBox.TryDropItem(from, item, false))
            {
                from.SendLocalizedMessage(1054110); // Your bank box is full.
            }
            else
            {
                Charges--;
                from.SendLocalizedMessage(1054150); // The item was placed in your bank box.
            }
        }

        private class SendTarget : Target
        {
            private readonly ChestOfSending _chest;

            public SendTarget(ChestOfSending chest) : base(-1, false, TargetFlags.None) => _chest = chest;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_chest.Deleted)
                {
                    return;
                }

                if (targeted is not Item item || from.Backpack == null)
                {
                    return;
                }

                if (!from.Region.IsPartOf<HouseRegion>())
                {
                    from.SendLocalizedMessage(502092); // You must be in your house to do this.
                }
                else
                {
                    _chest.TrySend(from, item);
                }
            }
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            if (from.CheckAlive())
            {
                list.Add(new UseChestEntry(CheckAccessible(from, this)));
            }

            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        private class UseChestEntry : ContextMenuEntry
        {
            public UseChestEntry(bool enabled) : base(1150419, 2) => Enabled = enabled;

            public override void OnClick(Mobile from, IEntity target)
            {
                if (target is not ChestOfSending chest || chest.Deleted)
                {
                    return;
                }

                if (from.CheckAlive())
                {
                    chest.OnDoubleClick(from);
                }
            }
        }
    }
}
