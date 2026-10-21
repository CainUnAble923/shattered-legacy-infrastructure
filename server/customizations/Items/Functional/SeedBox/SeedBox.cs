// cc-P199: ServUO pub57 Scripts/Items/Functional/SeedBox/SeedBox.cs. The seed box (ServUO's fourth-year veteran reward,
// RewardSystem.cs:490; art 19288/19290, 19289/19291 when it holds seeds): a blessed 10-stone container for up to 5,000
// seeds in up to 300 kinds. Dropping a seed in stacks it onto its kind's slot (same plant type and hue) or opens a new
// slot; the seeds stay inside the box, unmovable, and the box weighs only itself. Double-clicking it in the backpack, or
// within 3 tiles with access by its secure level (owner by default; anywhere outside a house), opens its gump
// (SeedBoxGump.cs). Staff also open it as a container.
//
// Differences from ServUO:
// - Saving the slots. ServUO writes a 0 for an empty slot and 1 plus the entry for a used one (SeedBox.cs:340-354).
//   Pinned's generated list writer has no null slots, so the slots are saved through _savedEntries, a generated list
//   kept in step with Entries on every change, which writes an empty slot as a SeedEntry with no seed and picture 0
//   and turns it back into an empty slot on load. A used slot whose seed is gone is dropped on load, as ServUO drops it (:379-380).
// - IsRewardItem and Level are generated serializable fields, saved in ServUO's order.
// - ColUtility.Free (:311-312) is a ServUO pool helper; the temporary lists are left to the collector.
// - ServUO's BaseHouse also leaves the box out of the house item count and decay (BaseHouse.cs:372-384), and its
//   reward system offers it (RewardSystem.cs:490). Those are lists in ServUO files; nothing here adds them.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.VeteranRewards;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;

namespace Server.Engines.Plants
{
    [Flippable(19288, 19290)]
    [SerializationGenerator(0, false)]
    public partial class SeedBox : Container, IRewardItem, ISecurable
    {
        public static readonly int MaxSeeds = 5000;
        public static readonly int MaxUnique = 300;

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _isRewardItem;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        [Constructible]
        public SeedBox() : base(19288)
        {
            Entries = new List<SeedEntry>();

            LootType = LootType.Blessed;

            _level = SecureLevel.Owner;
        }

        public override int DefaultMaxItems => MaxUnique;
        public override bool DisplaysContent => false;

        public List<SeedEntry> Entries { get; set; } = new();

        // The generator writes this list as it is (it never reads a property at save), so every change to Entries
        // refreshes it through SyncSaved, and AfterSeedBoxDeserialization rebuilds Entries from it.
        [SerializableField(2, getter: "private", setter: "private")]
        private List<SeedEntry> _savedEntries = [];

        public void SyncSaved()
        {
            _savedEntries = new List<SeedEntry>(Entries.Count);

            foreach (var e in Entries)
            {
                _savedEntries.Add(e ?? new SeedEntry());
            }

            this.MarkDirty();
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalCount
        {
            get
            {
                var count = 0;

                Entries?.ForEach(e => count += e?.Seed == null ? 0 : e.Seed.Amount);

                return count;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int UniqueCount => Entries?.Count(e => e?.Seed != null && e.Seed.Amount > 0) ?? 0;

        public override double DefaultWeight => 10.0;

        public override int GetTotal(TotalType type) => 0;

        public override void OnDoubleClick(Mobile m)
        {
            if (IsChildOf(m.Backpack) || (CheckAccessible(m) && m.InRange(GetWorldLocation(), 3)))
            {
                if (m is PlayerMobile pm)
                {
                    pm.SendGump(new SeedBoxGump(pm, this));
                }
            }

            if (m.AccessLevel > AccessLevel.Player)
            {
                base.OnDoubleClick(m);
            }
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);
            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        public bool CheckAccessible(Mobile from)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                return true;
            }

            var house = BaseHouse.FindHouseAt(this);

            if (house == null)
            {
                return true;
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

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (dropped is Seed seed)
            {
                return TryAddSeed(from, seed);
            }

            from.SendLocalizedMessage(1151838); // This item cannot be stored in the seed box.
            return false;
        }

        public bool TryAddSeed(Mobile from, Seed seed, int index = -1)
        {
            if (!from.Backpack.CheckHold(from, seed, true, true) || seed.Amount <= 0)
            {
                return false;
            }

            if (!from.InRange(GetWorldLocation(), 3) || from.Map != Map)
            {
                return false;
            }

            if (TotalCount + seed.Amount <= MaxSeeds)
            {
                var entry = GetExisting(seed);

                if (entry != null)
                {
                    entry.Seed.Amount += seed.Amount;
                    seed.Delete();

                    entry.Seed.InvalidateProperties();
                }
                else if (UniqueCount < MaxUnique)
                {
                    entry = new SeedEntry(seed);
                    DropItem(seed);

                    seed.Movable = false;
                    seed.InvalidateProperties();
                }
                else
                {
                    from.SendLocalizedMessage(1151839); // There is not enough room in the box.
                }

                if (entry != null)
                {
                    if (Entries.Contains(entry))
                    {
                        if (index > -1 && index < Entries.Count - 1)
                        {
                            Entries.Remove(entry);
                            AddEntry(entry, index);
                        }
                    }
                    else
                    {
                        if (index > -1 && index < Entries.Count)
                        {
                            AddEntry(entry, index);
                        }
                        else
                        {
                            AddEntry(entry);
                        }
                    }

                    from.SendLocalizedMessage(1151846); // You put the seed in the seedbox.

                    if (from is PlayerMobile pm)
                    {
                        var gump = new SeedBoxGump(pm, this);
                        gump.CheckPage(entry);

                        pm.SendGump(gump);
                    }

                    InvalidateProperties();
                    SyncSaved();
                    return true;
                }
            }
            else
            {
                from.SendLocalizedMessage(1151839); // There is not enough room in the box.
            }

            return false;
        }

        private void AddEntry(SeedEntry entry, int index = -1)
        {
            if (index == -1)
            {
                TrimEntries();
                Entries.Add(entry);
            }
            else if (index >= 0 && index < Entries.Count)
            {
                Entries.Insert(index, entry);
            }

            if (Entries.Count > 0)
            {
                if (ItemID == 19288)
                {
                    ItemID = 19289;
                }
                else if (ItemID == 19290)
                {
                    ItemID = 19291;
                }
            }

            SyncSaved();
        }

        private void RemoveEntry(SeedEntry entry)
        {
            Entries.Remove(entry);
            TrimEntries();

            if (Entries.Count == 0)
            {
                if (ItemID == 19289)
                {
                    ItemID = 19288;
                }
                else if (ItemID == 19291)
                {
                    ItemID = 19290;
                }
            }

            SyncSaved();
        }

        public void DropSeed(Mobile from, SeedEntry entry, int amount)
        {
            if (amount > entry.Seed.Amount)
            {
                amount = entry.Seed.Amount;
            }

            Seed seed;

            if (amount == entry.Seed.Amount)
            {
                seed = entry.Seed;
                entry.Seed = null;
            }
            else
            {
                seed = new Seed(entry.Seed.PlantType, entry.Seed.PlantHue, true)
                {
                    Amount = amount
                };

                entry.Seed.Amount -= amount;
            }

            seed.Movable = true;

            if (from.Backpack?.TryDropItem(from, seed, false) != true)
            {
                seed.MoveToWorld(from.Location, from.Map);
                from.SendLocalizedMessage(1151844); // There is not enough room in your backpack!
            }

            if (entry.Seed != null && entry.Seed.Amount <= 0)
            {
                entry.Seed.Delete();
                entry.Seed = null;
            }

            if (entry.Seed == null || entry.Seed.Amount <= 0)
            {
                RemoveEntry(entry);
            }

            this.MarkDirty();
        }

        public SeedEntry GetExisting(Seed seed)
        {
            return Entries.FirstOrDefault(
                e => e?.Seed != null && e.Seed.PlantType == seed.PlantType && e.Seed.PlantHue == seed.PlantHue
            );
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            if (_isRewardItem)
            {
                list.Add(1076220); // 4th Year Veteran Reward
            }

            list.Add(1151847, $"{TotalCount}\t{MaxSeeds}");    // Seeds in Box: ~1_val~ / ~2_val~
            list.Add(1151848, $"{UniqueCount}\t{MaxUnique}"); // Unique Seeds In Box: ~1_val~ / ~2_val~
        }

        public void CheckEntries()
        {
            var toDelete = new List<Item>(Items);

            foreach (var item in toDelete.Where(i => i != null && i.Amount == 0))
            {
                item.Delete();
            }

            var entries = new List<SeedEntry>(Entries);

            foreach (var entry in entries.Where(e => e != null && (e.Seed == null || e.Seed.Amount == 0 || e.Seed.Deleted)))
            {
                Entries.Remove(entry);
            }

            SyncSaved();
        }

        public void TrimEntries()
        {
            var lastIndex = Entries.FindLastIndex(e => e != null);

            if (lastIndex + 1 < Entries.Count - 1)
            {
                Entries.RemoveRange(lastIndex + 1, Entries.Count - 1 - lastIndex);
            }

            Entries.TrimExcess();
            SyncSaved();
        }

        [AfterDeserialization]
        private void AfterSeedBoxDeserialization()
        {
            Entries = new List<SeedEntry>();

            foreach (var e in _savedEntries ?? [])
            {
                if (e.Seed != null)
                {
                    Entries.Add(e);
                }
                else if (e.Image == 0)
                {
                    Entries.Add(null); // an empty slot
                }
            }

            Timer.DelayCall(
                () =>
                {
                    foreach (var item in Items.Where(i => i.Movable))
                    {
                        item.Movable = false;
                    }
                }
            );

            Timer.DelayCall(TimeSpan.FromSeconds(10), CheckEntries);
        }
    }
}
