// cc-P199: ServUO pub57 Scripts/Items/Tools/GreaterBraceletOfBinding.cs. The greater bracelet of binding (ServUO's
// first-year veteran reward, RewardSystem.cs:400): a blessed bracelet of binding (hue 2575) with ten friend slots instead
// of one twin. Worn and double-clicked it opens its gump: an empty slot targets a player wearing a bracelet of binding
// and asks them to accept; a filled slot whose player is online teleports to them through pinned's own bracelet
// (Bound is set to that friend's bracelet, then BraceletOfBinding.Activate runs pinned's charge, layer, facet, sigil,
// SpellHelper.CheckTravel, Young, murderer, criminal, combat, weight and jail checks and its two-second timer); the
// red button releases the slot. Accepting with a greater bracelet of your own asks you to pick a slot for them too.
// ServUO's BaseGump becomes pinned's DynamicGump; layout, buttons and messages are ServUO's.
//
// Differences from ServUO:
// - MaxRecharges. ServUO overrides it to -1, which its powder of translocation reads as "no limit"
//   (PowderOfTranslocation.cs:93 "MaxRecharges != -1 &&"). Pinned's MaxRecharges is not virtual and pinned's powder
//   has no -1 case (it would refuse every recharge), so this class re-implements TranslocationItem.MaxRecharges as
//   int.MaxValue: never oversaturated, as in ServUO. The [props] line still shows the base's 255.
// - TranslocationItemName is also re-implemented through the interface (pinned's is not virtual).
// - Saving. ServUO writes Pending and the ten slots by hand (GreaterBraceletOfBinding.cs:395-458). Here they are two
//   generated lists of BindEntry (pinned's AccountComment shape: a reader constructor and Serialize), kept in step
//   with the slots on every change and read back into them after load; an empty slot is saved as an entry with no
//   player. ServUO reads every bracelet back as a GreaterBraceletOfBinding
//   (:437, :451) although it writes plain bracelets of binding too, so a friend wearing an ordinary bracelet silently
//   vanishes at every restart; here the entry is read back as a BraceletOfBinding and kept.
// - Siege.SiegeShard (:60) does not exist in pinned; this shard is not Siege, so the check is gone.
// - IsFull is ServUO's expression (:16), which is always false (FirstOrDefault of a null slot is null); kept as is, so
//   "the target's greater bracelet is full" (1151781) only shows for an ordinary bracelet that is already bound.
// - BindEntry gains the reader constructor and Serialize that pinned's generator needs.
// - OnRemoved calls the base (pinned BaseJewel.OnRemoved removes the jewel's mods); ServUO's override (:120-129) skips it.
// - The bind target is a named method (OnBindTarget) instead of ServUO's lambda (:213-256); same checks, same order.

using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class GreaterBraceletOfBinding : BraceletOfBinding, TranslocationItem
    {
        public BindEntry[] Friends = new BindEntry[10];

        private BindEntry _pending;

        public BindEntry Pending
        {
            get => _pending;
            set
            {
                _pending = value;
                SyncSaved();
            }
        }

        // What is saved: the generator writes these two lists as they are (it never reads a property getter at save), so
        // every change to Friends or Pending refreshes them through SyncSaved, and AfterDeserialization rebuilds the slots.
        [SerializableField(0, getter: "private", setter: "private")]
        private List<BindEntry> _savedPending = [];

        [SerializableField(1, getter: "private", setter: "private")]
        private List<BindEntry> _savedFriends = [];

        public bool IsFull => Friends.FirstOrDefault(entry => entry == null) != null;

        private static readonly TextDefinition _greaterTranslocationItemName = "greater bracelet of binding";

        int TranslocationItem.MaxRecharges => int.MaxValue;

        TextDefinition TranslocationItem.TranslocationItemName => _greaterTranslocationItemName;

        [Constructible]
        public GreaterBraceletOfBinding()
        {
            Hue = 2575;
            LootType = LootType.Blessed;
        }

        // ServUO's Weight = 1.0 is the base's DefaultWeight already.

        private void SyncSaved()
        {
            _savedPending = _pending == null ? [] : [_pending];
            _savedFriends = new List<BindEntry>(Friends.Length);

            foreach (var e in Friends)
            {
                _savedFriends.Add(e ?? new BindEntry(null, null));
            }

            this.MarkDirty();
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            Friends = new BindEntry[10];

            for (var i = 0; _savedFriends != null && i < _savedFriends.Count && i < Friends.Length; i++)
            {
                var e = _savedFriends[i];

                if (e.Mobile != null && e.Bracelet != null)
                {
                    Friends[i] = e;
                }
            }

            _pending = null;

            foreach (var e in _savedPending ?? [])
            {
                if (e.Mobile != null && e.Bracelet != null)
                {
                    _pending = e;
                }
            }

            SyncSaved();
        }

        public override void AddNameProperty(IPropertyList list)
        {
            list.Add(1151769); // Greater Bracelet of Binding
        }

        public override void OnSingleClick(Mobile from)
        {
            LabelTo(from, 1151769); // Greater Bracelet of Binding
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile pm && from.Items.Contains(this))
            {
                if (Pending != null)
                {
                    new GreaterBraceletOfBindingGump(pm, this, Pending).Refresh();
                }
                else
                {
                    new GreaterBraceletOfBindingGump(pm, this).Refresh();
                }
            }
            else
            {
                from.SendLocalizedMessage(1151772); // You must be wearing this item to bind to another character.
            }
        }

        public bool IsBound(Mobile m)
        {
            foreach (var e in Friends)
            {
                if (e != null && m == e.Mobile)
                {
                    return true;
                }
            }

            return false;
        }

        public void Remove(PlayerMobile pm)
        {
            for (var i = 0; i < Friends.Length; i++)
            {
                if (Friends[i] != null && Friends[i].Mobile == pm)
                {
                    Friends[i] = null;
                    SyncSaved();
                    break;
                }
            }
        }

        public void Add(BindEntry entry, int index)
        {
            if (index >= 0 && index < Friends.Length && Friends[index] == null)
            {
                Friends[index] = entry;

                if (Pending == entry)
                {
                    _pending = null;
                }

                SyncSaved();
            }
        }

        // ServUO tests RootParent, which is already null once the bracelet is off, so this never closes anything there
        // either; kept as written. ServUO also skips the base call, leaving a jewel's stat and skill mods on the wearer;
        // here base.OnRemoved runs first.
        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (RootParent is Mobile m)
            {
                m.CloseGump<GreaterBraceletOfBindingGump>();
                m.CloseGump<ConfirmBindGump>();
            }
        }

        public class GreaterBraceletOfBindingGump : DynamicGump
        {
            public PlayerMobile User { get; }
            public GreaterBraceletOfBinding Bracelet { get; set; }
            public bool Choose { get; set; }
            public BindEntry Entry { get; set; }

            public override bool Singleton => true;

            public GreaterBraceletOfBindingGump(PlayerMobile pm, GreaterBraceletOfBinding bracelet, BindEntry entry)
                : base(100, 100)
            {
                User = pm;
                Bracelet = bracelet;
                Entry = entry;
                Choose = true;
            }

            public GreaterBraceletOfBindingGump(PlayerMobile pm, GreaterBraceletOfBinding bracelet) : base(100, 100)
            {
                User = pm;
                Bracelet = bracelet;
                Choose = false;
            }

            // ServUO closes the confirmation and its own gump at every layout (GreaterBraceletOfBinding.cs:154-155);
            // Singleton replaces its own.
            public void Refresh()
            {
                User.CloseGump<ConfirmBindGump>();
                User.SendGump(this);
            }

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                var color = Utility.C32216(0x0000CD);

                builder.AddBackground(0, 0, 220, 320, 5170);

                if (Choose)
                {
                    builder.AddHtmlLocalized(0, 3, 220, 20, 1154645, "#1151796", color); // **Choose slot to bind**
                }
                else
                {
                    builder.AddHtmlLocalized(0, 3, 220, 20, 1154645, "#1151769", color); // Greater Bracelet of Binding
                    // Teleport Charges: ~1_val~
                    builder.AddHtmlLocalized(40, 275, 220, 20, 1017337, Bracelet.Charges.ToString(), color);
                }

                for (var i = 0; i < 10; i++)
                {
                    if (Bracelet.Friends[i] != null)
                    {
                        builder.AddLabel(60, 25 + i * 25, 0x21, Bracelet.Friends[i].Mobile.Name);

                        if (!Choose)
                        {
                            if (Bracelet.Friends[i].Mobile.NetState != null)
                            {
                                builder.AddButton(32, 28 + i * 25, 0x2C89, 0x2C8A, i + 10);
                            }

                            builder.AddButton(165, 25 + i * 25, 4017, 4019, i + 100);
                        }
                    }
                    else
                    {
                        builder.AddButton(25, 25 + i * 25, 4006, 4005, i + 10);
                        builder.AddHtmlLocalized(60, 25 + i * 25, 150, 20, 3006110);
                    }
                }
            }

            public override void OnResponse(NetState sender, in RelayInfo info)
            {
                if (info.ButtonID == 0)
                {
                    return;
                }

                var id = info.ButtonID - 10;

                if (id >= 0 && id < Bracelet.Friends.Length)
                {
                    if (Bracelet.Friends[id] == null)
                    {
                        if (Choose)
                        {
                            Bracelet.Add(Entry, id);
                            Choose = false;

                            Refresh();
                        }
                        else if (!Bracelet.IsFull)
                        {
                            User.BeginTarget(10, false, TargetFlags.None, (_, targeted) => OnBindTarget(targeted, id));
                        }
                    }
                    else
                    {
                        Bracelet.Bound = Bracelet.Friends[id].Bracelet;
                        Bracelet.Activate(User);
                    }
                }
                else
                {
                    id = info.ButtonID - 100;

                    if (id >= 0 && id < Bracelet.Friends.Length && Bracelet.Friends[id] != null)
                    {
                        User.SendGump(
                            new ConfirmBindGump(
                                User,
                                Bracelet.Friends[id].Mobile,
                                id,
                                Bracelet.Friends[id].Bracelet as GreaterBraceletOfBinding,
                                true
                            )
                        );
                    }
                }
            }

            public void OnBindTarget(object targeted, int id)
            {
                if (targeted is PlayerMobile pm)
                {
                    var pmBrac = pm.FindItemOnLayer(Layer.Bracelet) as BraceletOfBinding;

                    if (pm == User)
                    {
                        User.SendLocalizedMessage(1151779); // The bracelet cannot bind with itself.
                    }
                    else if (Bracelet.IsBound(pm))
                    {
                        User.SendLocalizedMessage(1151770); // This bracelet is already bound to this character.
                    }
                    else if (!User.Items.Contains(Bracelet))
                    {
                        User.SendLocalizedMessage(1151772); // You must be wearing this item to bind to another character.
                    }
                    else if (pmBrac == null)
                    {
                        // The target player must be wearing a Bracelet of Binding or Greater Bracelet of Binding for the device to work.
                        User.SendLocalizedMessage(1151771);
                    }
                    else if (pm.HasGump<ConfirmBindGump>() || pmBrac is GreaterBraceletOfBinding { Pending: not null })
                    {
                        User.SendLocalizedMessage(1151833); // You may not get confirmation from this player at this time.
                    }
                    else if (pmBrac is GreaterBraceletOfBinding { IsFull: true } ||
                             pmBrac is not GreaterBraceletOfBinding && pmBrac.Bound != null)
                    {
                        User.SendLocalizedMessage(1151781); // The target player's greater bracelet of binding is full.
                    }
                    else
                    {
                        User.SendLocalizedMessage(1151777, pm.Name); // Waiting for ~1_val~ to respond.
                        Refresh();

                        pm.SendGump(new ConfirmBindGump(pm, User, id, Bracelet, false));
                    }
                }
                else
                {
                    User.SendLocalizedMessage(1151775); // You may not bind your bracelet to that.
                }
            }
        }

        public class ConfirmBindGump : DynamicGump
        {
            public PlayerMobile User { get; }
            public GreaterBraceletOfBinding Bracelet { get; set; }
            public PlayerMobile From { get; set; }
            public int Index { get; set; }

            public bool RemoveFromBracelet { get; set; }

            public ConfirmBindGump(PlayerMobile user, PlayerMobile from, int index, GreaterBraceletOfBinding bracelet, bool remove)
                : base(200, 200)
            {
                User = user;
                Bracelet = bracelet;
                From = from;

                RemoveFromBracelet = remove;
                Index = index;
            }

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                builder.AddBackground(0, 0, 405, 110, 5054);
                builder.AddImageTiled(10, 10, 385, 90, 2702);
                builder.AddAlphaRegion(10, 10, 385, 90);

                if (RemoveFromBracelet)
                {
                    // Releasing ~1_val~'s bracelet from yours.  Are you sure you want to do this?
                    builder.AddHtmlLocalized(55, 20, 285, 60, 1151774, From.Name, 0xFFFF);
                }
                else
                {
                    // ~1_val~ is attempting to bind bracelets with you.  Do you accept?
                    builder.AddHtmlLocalized(55, 20, 285, 60, 1151773, From.Name, 0xFFFF);
                }

                builder.AddHtml(110, 60, 100, 20, "<BASEFONT COLOR=#FFFFFF>Yes");
                builder.AddButton(75, 60, 4005, 4007, 1);

                builder.AddHtml(250, 60, 100, 20, "<BASEFONT COLOR=#FFFFFF>No");
                builder.AddButton(285, 60, 4005, 4007, 2);
            }

            public override void OnResponse(NetState sender, in RelayInfo info)
            {
                switch (info.ButtonID)
                {
                    case 1:
                        {
                            if (RemoveFromBracelet)
                            {
                                var bracelet = User.FindItemOnLayer(Layer.Bracelet) as GreaterBraceletOfBinding;

                                if (bracelet?.Friends[Index] != null)
                                {
                                    var entry = bracelet.Friends[Index];

                                    if (entry.Bracelet is GreaterBraceletOfBinding gbr)
                                    {
                                        var pm = entry.Mobile;

                                        gbr.Remove(User);

                                        if (pm?.NetState != null)
                                        {
                                            pm.FindGump<GreaterBraceletOfBindingGump>()?.Refresh();
                                        }
                                    }

                                    bracelet.Remove(entry.Mobile);
                                    new GreaterBraceletOfBindingGump(User, bracelet).Refresh();
                                }
                            }
                            else
                            {
                                var brac = User.FindItemOnLayer(Layer.Bracelet) as BraceletOfBinding;

                                if (brac != null)
                                {
                                    var entry = new BindEntry(User, brac);
                                    Bracelet.Add(entry, Index);

                                    From.FindGump<GreaterBraceletOfBindingGump>()?.Refresh();

                                    if (brac is GreaterBraceletOfBinding greater && !greater.IsBound(From))
                                    {
                                        entry = new BindEntry(From, Bracelet);
                                        greater.Pending = entry;

                                        new GreaterBraceletOfBindingGump(User, greater, entry).Refresh();
                                    }
                                    else
                                    {
                                        brac.Bound = Bracelet;
                                    }
                                }
                                else
                                {
                                    // You must be wearing this item to bind to another character.
                                    User.SendLocalizedMessage(1151772);
                                    // The target player must be wearing a Bracelet of Binding or Greater Bracelet of Binding for the device to work.
                                    From.SendLocalizedMessage(1151771);
                                }
                            }

                            break;
                        }
                    case 2:
                        {
                            if (!RemoveFromBracelet)
                            {
                                From.SendLocalizedMessage(1151778, User.Name); // ~1_val~ has declined your request to bind bracelets.
                            }

                            break;
                        }
                }
            }
        }

        public class BindEntry
        {
            public PlayerMobile Mobile { get; set; }
            public BraceletOfBinding Bracelet { get; set; }

            public BindEntry(PlayerMobile m, BraceletOfBinding bracelet)
            {
                Mobile = m;
                Bracelet = bracelet;
            }

            public BindEntry(IGenericReader reader)
            {
                reader.ReadEncodedInt(); // version

                Mobile = reader.ReadEntity<PlayerMobile>();
                Bracelet = reader.ReadEntity<BraceletOfBinding>();
            }

            public void Serialize(IGenericWriter writer)
            {
                writer.WriteEncodedInt(0); // version

                writer.Write(Mobile);
                writer.Write(Bracelet);
            }
        }
    }
}
