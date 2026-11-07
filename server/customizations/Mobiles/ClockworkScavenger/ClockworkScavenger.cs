// cc-P808: the Clockwork Scavenger, the auto-loot reward (F-33 Option A, Chase 2026-10-05). Notes: shard-migration
// notes/cc-P808-clockwork-scavenger.md.
//
// EARNED. Chase, 2026-11-07: from the Tinkers' Union, not the League shop. A character at Journeyman in the Union says
// "scavenger" to a Tinker guildmaster (or picks Quest Conversation) and gets a frame bound to them. They gather the parts
// (Configuration/clockwork-scavenger.json) and use the frame with the parts in the pack: the parts are used up and the
// Scavenger is theirs. One per character: no frame while they hold a frame or a Scavenger. A released Scavenger powers
// down (it is deleted), and the guildmaster gives a new frame for the same parts.
//
// WHAT IT DOES. Every few seconds, near its owner, it lifts items from monster corpses within reach into the owner's
// backpack, by a filter the owner sets (MuleModules.PickupCategories: gold, gems, reagents and the rest; never gear, so
// never an item property). It takes only what the owner could loot without a crime: no player or pet corpse, no
// criminal loot, and only the owner's share of instanced loot (Corpse.IsChildVisibleTo). With Clockwork Load Harness III
// on the owner's mule nearby, the loot goes to the mule instead, then to the backpack when the mule is full.
//
// It is blessed (nothing can harm or target it, pinned Mobile.CanBeHarmful), does not fight and does not need food. Only
// the character who built it gets loot from it.
//
// The idea of the pick-up loop is from the Picky Beetle by Xanthor (from Charon); none of its code is used.
// Saved types: ClockworkScavenger and ClockworkScavengerFrame, both source generated.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Multis;
using Server.Network;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class ClockworkScavenger : BaseCreature
    {
        private static readonly HashSet<ClockworkScavenger> _all = new();

        public static IEnumerable<ClockworkScavenger> All => _all.Where(s => !s.Deleted && World.FindMobile(s.Serial) == s); // not a save-test copy

        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private Mobile _builder;

        [SerializableField(1, setter: "private")]
        private List<string> _filterList;

        [Constructible]
        public ClockworkScavenger() : this(null)
        {
        }

        public ClockworkScavenger(Mobile builder) : base(AIType.AI_Animal, FightMode.None)
        {
            var c = ClockworkScavengerConfig.Current;
            _builder = builder;
            _filterList = new List<string>(c.DefaultFilter ?? []);
            Body = c.Body;
            Hue = c.Hue;
            BaseSoundID = 397;
            Blessed = true;
            Tamable = false;
            ControlSlots = c.ControlSlots;
            SetStr(50);
            SetDex(60);
            SetInt(10);
            SetHits(50);
            SetDamage(0);
            VirtualArmor = 20;
            Fame = 0;
            Karma = 0;
            _all.Add(this);
        }

        public override string DefaultName => "a Clockwork Scavenger";
        public override string CorpseName => "a broken Clockwork Scavenger";
        public override bool DeleteOnRelease => true; // a construct powers down; the guildmaster gives a new frame
        public override bool CanBeRenamedBy(Mobile from) => from == _builder || base.CanBeRenamedBy(from);
        public override FoodType FavoriteFood => FoodType.None;
        public override bool BardImmune => true;

        public IReadOnlyList<string> Filter => _filterList ??= new List<string>();

        public void ToggleFilter(string category)
        {
            _filterList ??= new List<string>();

            if (!_filterList.Remove(category))
            {
                _filterList.Add(category);
            }

            this.MarkDirty();
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            _filterList ??= new List<string>();
            _all.Add(this);
        }

        public override void OnAfterDelete()
        {
            _all.Remove(this);
            base.OnAfterDelete();
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Built by {_builder?.Name ?? "nobody"}");
            list.Add(Filter.Count == 0 ? "Collects: nothing (set it in Options)" : $"Collects: {string.Join(", ", Filter)}");
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            if (from == ControlMaster && from == _builder)
            {
                list.Add(new FilterEntry(this));
            }
        }

        // --- the loop ----------------------------------------------------------------------------------------------

        public static void Initialize()
        {
            var every = TimeSpan.FromSeconds(ClockworkScavengerConfig.Current.TickSeconds);
            Timer.StartTimer(every, every, TickAll);
        }

        private static void TickAll()
        {
            foreach (var s in All.ToList())
            {
                s.Scavenge();
            }
        }

        /// <summary>One pass: lifts what the filter keeps from lootable corpses near it. Returns the items moved.</summary>
        public int Scavenge()
        {
            var c = ClockworkScavengerConfig.Current;

            if (Deleted || !Alive || IsDeadPet || IsStabled || !Controlled || Map == null || Map == Map.Internal ||
                ControlMaster is not PlayerMobile owner || owner != _builder || !owner.Alive || owner.NetState == null ||
                owner.Map != Map || !InRange(owner, c.OwnerRange) || owner.Backpack == null || Filter.Count == 0)
            {
                return 0;
            }

            var mule = MuleModules.MuleOf(owner, c.MuleRange) is { Backpack: { } } m &&
                       MuleModules.TierOf(m, MuleModuleId.ClockworkLoadHarness) >= 3 ? m : null;

            var corpses = new List<Corpse>();

            foreach (var corpse in Map.GetItemsInRange<Corpse>(Location, c.CorpseRange))
            {
                corpses.Add(corpse);
            }

            var moved = 0;

            foreach (var corpse in corpses)
            {
                if (!MayLoot(owner, corpse))
                {
                    continue;
                }

                foreach (var item in corpse.Items.ToList())
                {
                    if (moved >= c.MaxPerTick)
                    {
                        break;
                    }

                    if (!item.Movable || item.Nontransferable || item.LootType == LootType.Cursed || item is Container ||
                        !corpse.IsChildVisibleTo(owner, item) || !MuleModules.Matches(item, Filter))
                    {
                        continue;
                    }

                    if (mule?.Backpack.TryDropItem(owner, item, false) == true || owner.Backpack.TryDropItem(owner, item, false))
                    {
                        moved++;
                    }
                }
            }

            if (moved > 0)
            {
                PublicOverheadMessage(MessageType.Emote, 0x3B2, false, "*click-whirr*");
                owner.SendMessage(0x59, mule != null
                    ? $"Your Clockwork Scavenger carries {moved} {(moved == 1 ? "item" : "items")} to your mule."
                    : $"Your Clockwork Scavenger collects {moved} {(moved == 1 ? "item" : "items")}.");
            }

            return moved;
        }

        /// <summary>A monster's corpse the owner may loot now without a crime, not inside a house.</summary>
        public static bool MayLoot(PlayerMobile owner, Corpse corpse) =>
            !corpse.Deleted && corpse.Owner is not PlayerMobile && (corpse.Owner is BaseCreature || corpse.OwnerWasBaseCreature) &&
            !corpse.IsCriminalAction(owner) && corpse.CanLoot(owner, null) && BaseHouse.FindHouseAt(corpse) == null;

        private sealed class FilterEntry : ContextMenuEntry
        {
            private readonly ClockworkScavenger _s;

            public FilterEntry(ClockworkScavenger s) : base(3000127, 3) => _s = s; // "Options"

            public override void OnClick(Mobile from, IEntity target)
            {
                if (from is PlayerMobile pm && pm == _s.ControlMaster && !_s.Deleted)
                {
                    pm.SendGump(new ScavengerFilterGump(_s));
                }
            }
        }
    }

    /// <summary>The owner's filter: one toggle per pickup category (the Salvage Bin's list).</summary>
    public sealed class ScavengerFilterGump : Gump
    {
        private readonly ClockworkScavenger _s;

        public ScavengerFilterGump(ClockworkScavenger s) : base(80, 80)
        {
            _s = s;
            var cats = MuleModules.PickupCategories;
            AddPage(0);
            AddBackground(0, 0, 300, 70 + cats.Length * 26, 9270);
            AddLabel(20, 15, 0x481, "Clockwork Scavenger: what it collects");

            for (var i = 0; i < cats.Length; i++)
            {
                var on = s.Filter.Contains(cats[i]);
                AddButton(20, 45 + i * 26, on ? 4018 : 4005, 4007, 1 + i);
                AddLabel(55, 47 + i * 26, on ? 0x44 : 999, cats[i]);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;

            if (i < 0 || i >= MuleModules.PickupCategories.Length || _s.Deleted || sender.Mobile != _s.ControlMaster)
            {
                return;
            }

            _s.ToggleFilter(MuleModules.PickupCategories[i]);
            sender.Mobile.SendGump(new ScavengerFilterGump(_s));
        }
    }

    /// <summary>The build: bound to the character the guildmaster gave it to. Used with every part in the pack, it becomes the Scavenger.</summary>
    [SerializationGenerator(0, false)]
    public partial class ClockworkScavengerFrame : Item
    {
        private static readonly HashSet<ClockworkScavengerFrame> _all = new();

        public static IEnumerable<ClockworkScavengerFrame> All => _all.Where(f => !f.Deleted && World.FindItem(f.Serial) == f);

        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private Mobile _owner;

        [Constructible]
        public ClockworkScavengerFrame() : this(null)
        {
        }

        public ClockworkScavengerFrame(Mobile owner) : base(0x104F)
        {
            _owner = owner;
            Hue = ClockworkScavengerConfig.Current.Hue;
            LootType = LootType.Blessed;
            Weight = 5.0;
            _all.Add(this);
        }

        public override string DefaultName => "a Clockwork Scavenger frame";

        [AfterDeserialization]
        private void AfterDeserialization() => _all.Add(this);

        public override void OnAfterDelete()
        {
            _all.Remove(this);
            base.OnAfterDelete();
        }

        public override bool OnDragLift(Mobile from) =>
            (_owner == null || from == _owner || from.AccessLevel > AccessLevel.Player) && base.OnDragLift(from);

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Bound to {_owner?.Name ?? "nobody"}");
            list.Add($"Needs: {ClockworkScavengerQuest.PartsText()}");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile pm)
            {
                ClockworkScavengerQuest.Assemble(pm, this);
            }
        }
    }
}
