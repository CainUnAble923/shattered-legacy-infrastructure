// cc-P199: the Storage batch (ServUO Items/Tools ChestOfSending, GreaterBraceletOfBinding, ForgedMetalOfArtifacts,
// PenOfWisdom and Items/Functional JewelryBox/*, SeedBox/* -> customizations/Items/Tools/ and
// customizations/Items/Functional/). WallSafe.cs is not ported.
//
// Facts:
//   1. Every ported type constructs as ServUO makes it: art, label, hue, blessing, weight, charges, uses, secure level,
//      capacity; the greater bracelet has ten empty slots, never oversaturates and names itself to the powder.
//   2. Every ported type with saved state survives a serialization round trip with each saved field set: the chest's
//      level, charges and next recharge; the forged metal's uses; the bracelet's pending entry, two of its ten slots (one an ordinary bracelet)
//      and its base charges, recharges, inscription and twin; the pen's uses; the jewelry box's level and filter; the
//      seed box's reward flag, level and slots, an empty slot kept and a slot whose seed is gone dropped.
//   3. The chest of sending clamps its charges to 0-50, recharges one charge for each 11-13 hours passed, says "You must
//      be in your house" outside one, and sends a pack item to the bank for a charge (staff, locked down); it refuses a
//      container and does nothing at 0 charges.
//   4. The greater bracelet: a slot fills only when empty and clears the pending entry; accepting a bind from an
//      ordinary bracelet fills the asker's slot and binds the ordinary bracelet back; accepting with a greater bracelet
//      leaves a pending entry that the slot chooser files; releasing clears both sides.
//   5. The pen of wisdom copies every ticked runebook entry into new entries of the destination for one rune, one Mark
//      scroll and one use each; short of runes it copies nothing.
//   6. The jewelry box takes only jewelry while locked down, a filter shows one kind at a time and clicking a piece
//      moves it to the backpack.
//   7. The seed box stacks a kind onto its slot, opens a new slot for a new hue, changes its art, refuses over 5,000
//      seeds and non-seeds, gives seeds back by the slot, and shifts slots right and left as ServUO does.
//   8. SeedInfoGump's label is pinned Seed.GetLabel's.
//   9. Forged metal of artifacts: OKAY spends a use and sets the aura, REMOVE clears it and refunds the use, the last
//      use wears the tool out. (Nothing reads the aura until the Enhance.cs patch in the port's header lands.)
//
// Host notes: timers through ShardTestClock (Arm before anything that schedules); messages read off a test NetState's
// send buffer (0xC1 localized); every fact deletes what it made. No fact places a house: the house checks are pinned's
// and staff (GameMaster) pass them, as ServUO's CheckAccessible lets them.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.Plants;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P199StorageVerification : IDisposable
{
    private static readonly Point3D Spot = new(1400, 1720, 0);

    // The host never runs BaseHouse.Configure, so Item.LockedDownFlag is 0 and IsLockedDown cannot be set. Every fact
    // here runs with BaseHouse.Configure's values (pinned BaseHouse.cs:1304-1305), put back after it.
    private readonly int _lockedFlag = Item.LockedDownFlag;
    private readonly int _secureFlag = Item.SecureFlag;

    public P199StorageVerification()
    {
        Item.LockedDownFlag = 1;
        Item.SecureFlag = 2;
    }

    public void Dispose()
    {
        Item.LockedDownFlag = _lockedFlag;
        Item.SecureFlag = _secureFlag;
    }

    private static RelayInfo Button(int id) => new(id, default, default, default, default);

    // A player at Spot with a backpack and, optionally, a test NetState whose send buffer the fact reads.
    private sealed class Player : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Made = new();

        public Player(bool withNetState = false, int dx = 0)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
            Pm.Hits = Pm.HitsMax;

            if (withNetState)
            {
                Ns = PacketTestUtilities.CreateTestNetState();
                Pm.NetState = Ns;
                Ns.Mobile = Pm;
            }
        }

        public int Mark => Ns.SendBuffer.GetReadSpan().Length;

        public T Make<T>(T e) where T : IEntity
        {
            Made.Add(e);
            return e;
        }

        public T Pack<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            Made.Add(item);
            return item;
        }

        public T Wear<T>(T item) where T : Item
        {
            Pm.AddItem(item);
            Made.Add(item);
            return item;
        }

        public bool Sent(int from, int cliloc)
        {
            var span = Ns.SendBuffer.GetReadSpan()[from..];
            for (var i = 0; i + 18 <= span.Length; i++)
            {
                if (span[i] == 0xC1 && BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            foreach (var e in Made)
            {
                e.Delete();
            }

            foreach (var item in new List<Item>(Pm.Backpack.Items))
            {
                item.Delete();
            }

            if (Ns != null)
            {
                Ns.Mobile = null;
                Pm.NetState = null;
            }

            Pm.Delete();
        }
    }

    [Fact]
    public void EachTypeConstructsAsServUOMakesIt()
    {
        var chest = new ChestOfSending();
        var bracelet = new GreaterBraceletOfBinding();
        var pen = new PenOfWisdom();
        var pen7 = new PenOfWisdom(7);
        var jewelryBox = new JewelryBox();
        var seedBox = new SeedBox();
        var forged = new ForgedMetalOfArtifacts();
        var forged10 = new ForgedMetalOfArtifacts(10);

        try
        {
            Assert.Equal(0x4910, chest.ItemID);
            Assert.Equal(1150418, chest.LabelNumber);
            Assert.Equal(LootType.Blessed, chest.LootType);
            Assert.Equal(50, chest.Charges);
            Assert.Equal(50, ChestOfSending.MaxCharges);
            Assert.Equal(SecureLevel.CoOwners, chest.Level);
            Assert.False(chest.Stackable);

            Assert.Equal(0x1086, bracelet.ItemID);
            Assert.Equal(2575, bracelet.Hue);
            Assert.Equal(LootType.Blessed, bracelet.LootType);
            Assert.Equal(1.0, bracelet.Weight);
            Assert.Equal(Layer.Bracelet, bracelet.Layer);
            Assert.Equal(10, bracelet.Friends.Length);
            Assert.All(bracelet.Friends, e => Assert.Null(e));
            Assert.Null(bracelet.Pending);
            Assert.False(bracelet.IsFull);
            TranslocationItem asTranslocation = bracelet;
            Assert.Equal(int.MaxValue, asTranslocation.MaxRecharges);
            Assert.Equal("greater bracelet of binding", asTranslocation.TranslocationItemName.String);
            Assert.Equal(20, asTranslocation.MaxCharges);

            Assert.Equal(0x0FC0, pen.ItemID);
            Assert.Equal(1115358, pen.LabelNumber);
            Assert.Equal(1260, pen.Hue);
            Assert.Equal(100, pen.UsesRemaining);
            Assert.True(pen.ShowUsesRemaining);
            Assert.Equal(1.0, pen.Weight);
            Assert.Equal(LootType.Blessed, pen.LootType);
            Assert.Equal(7, pen7.UsesRemaining);

            Assert.Equal(0x9F1C, jewelryBox.ItemID);
            Assert.Equal(1157694, jewelryBox.LabelNumber);
            Assert.Equal(10.0, jewelryBox.Weight);
            Assert.Equal(500, jewelryBox.MaxItems);
            Assert.Equal(SecureLevel.CoOwners, jewelryBox.Level);
            Assert.True(jewelryBox.Filter.IsDefault);
            Assert.False(jewelryBox.DisplaysContent);
            Assert.False(jewelryBox.IsFull);

            Assert.Equal(19288, seedBox.ItemID);
            Assert.Equal(LootType.Blessed, seedBox.LootType);
            Assert.Equal(10.0, seedBox.Weight);
            Assert.Equal(300, seedBox.MaxItems);
            Assert.Equal(SecureLevel.Owner, seedBox.Level);
            Assert.False(seedBox.IsRewardItem);
            Assert.Empty(seedBox.Entries);
            Assert.Equal(0, seedBox.TotalCount);
            Assert.Equal(0, seedBox.UniqueCount);
            Assert.False(seedBox.DisplaysContent);

            Assert.Equal(4023, forged.ItemID);
            Assert.Equal(1149868, forged.LabelNumber);
            Assert.Equal(51, forged.Hue);
            Assert.Equal(5.0, forged.Weight);
            Assert.Equal(5, forged.UsesRemaining);
            Assert.Equal(10, forged10.UsesRemaining);
            Assert.Equal(LootType.Blessed, forged.LootType);
        }
        finally
        {
            chest.Delete();
            bracelet.Delete();
            pen.Delete();
            pen7.Delete();
            jewelryBox.Delete();
            seedBox.Delete();
            forged.Delete();
            forged10.Delete();
        }
    }

    [Fact]
    public void EachTypeSurvivesARoundTrip()
    {
        ShardTestClock.Arm();
        var b = new Player();
        var c = new Player(dx: 1);
        var made = new List<Item>();

        try
        {
            // Chest of sending.
            var chest = new ChestOfSending { Charges = 17, Level = SecureLevel.Friends };
            var next = Core.Now + TimeSpan.FromHours(5);
            chest.NextRecharge = next;
            var chestCopy = ConsumablesVerification.RoundTrip(chest, s => new ChestOfSending(s));
            made.Add(chestCopy);
            Assert.Equal(17, chestCopy.Charges);
            Assert.Equal(SecureLevel.Friends, chestCopy.Level);
            Assert.Equal(next, chestCopy.NextRecharge);

            // Greater bracelet: an ordinary bracelet in slot 0, a greater one in slot 3 and pending.
            var ordinary = b.Wear(new BraceletOfBinding());
            var greaterC = c.Wear(new GreaterBraceletOfBinding());
            var bracelet = new GreaterBraceletOfBinding
            {
                Charges = 7, Recharges = 3, Inscription = "for the road", Bound = ordinary
            };
            bracelet.Add(new GreaterBraceletOfBinding.BindEntry(b.Pm, ordinary), 0);
            bracelet.Add(new GreaterBraceletOfBinding.BindEntry(c.Pm, greaterC), 3);
            bracelet.Pending = new GreaterBraceletOfBinding.BindEntry(c.Pm, greaterC);

            var braceletCopy = ConsumablesVerification.RoundTrip(bracelet, s => new GreaterBraceletOfBinding(s));
            made.Add(braceletCopy);
            Assert.Equal(2575, braceletCopy.Hue);
            Assert.Equal(7, braceletCopy.Charges);
            Assert.Equal(3, braceletCopy.Recharges);
            Assert.Equal("for the road", braceletCopy.Inscription);
            Assert.Same(ordinary, braceletCopy.Bound);
            Assert.Same(b.Pm, braceletCopy.Friends[0].Mobile);
            Assert.Same(ordinary, braceletCopy.Friends[0].Bracelet); // ServUO would lose this one
            Assert.Same(c.Pm, braceletCopy.Friends[3].Mobile);
            Assert.Same(greaterC, braceletCopy.Friends[3].Bracelet);
            Assert.Equal(8, braceletCopy.Friends.Count(e => e == null));
            Assert.Same(c.Pm, braceletCopy.Pending.Mobile);
            Assert.Same(greaterC, braceletCopy.Pending.Bracelet);

            // Forged metal.
            var forged = new ForgedMetalOfArtifacts(10) { UsesRemaining = 3 };
            var forgedCopy = ConsumablesVerification.RoundTrip(forged, s => new ForgedMetalOfArtifacts(s));
            made.Add(forgedCopy);
            Assert.Equal(3, forgedCopy.UsesRemaining);
            Assert.Equal(4023, forgedCopy.ItemID);

            // Pen.
            var pen = new PenOfWisdom { UsesRemaining = 42 };
            var penCopy = ConsumablesVerification.RoundTrip(pen, s => new PenOfWisdom(s));
            made.Add(penCopy);
            Assert.Equal(42, penCopy.UsesRemaining);
            Assert.Equal(1260, penCopy.Hue);

            // Jewelry box.
            var jewelryBox = new JewelryBox { Level = SecureLevel.Owner };
            jewelryBox.Filter.Talisman = true;
            var jewelryCopy = ConsumablesVerification.RoundTrip(jewelryBox, s => new JewelryBox(s));
            made.Add(jewelryCopy);
            Assert.Equal(SecureLevel.Owner, jewelryCopy.Level);
            Assert.NotNull(jewelryCopy.Filter);
            Assert.True(jewelryCopy.Filter.Talisman);
            Assert.False(jewelryCopy.Filter.Ring || jewelryCopy.Filter.Bracelet || jewelryCopy.Filter.Earrings ||
                         jewelryCopy.Filter.Necklace);
            Assert.False(jewelryCopy.Filter.IsDefault);

            // Seed box: two kinds, an empty slot between them, and a slot whose seed is gone. The seeds are kept
            // outside the box so the round trip's delete does not take them.
            var seedA = new Seed(PlantType.CampionFlowers, PlantHue.Red, true) { Amount = 4 };
            var seedB = new Seed(PlantType.Poppies, PlantHue.Blue, true) { Amount = 9 };
            var seedC = new Seed(PlantType.Snowdrops, PlantHue.Yellow, true);
            made.Add(seedA);
            made.Add(seedB);
            var seedBox = new SeedBox { IsRewardItem = true, Level = SecureLevel.Friends };
            var entryA = new SeedEntry(seedA);
            var entryB = new SeedEntry(seedB);
            seedBox.Entries.Add(entryA);
            seedBox.Entries.Add(null);
            seedBox.Entries.Add(entryB);
            seedBox.Entries.Add(new SeedEntry(seedC));
            seedBox.SyncSaved(); // the box's own methods do this; the slots above were set by hand
            seedC.Delete();

            var seedCopy = ConsumablesVerification.RoundTrip(seedBox, s => new SeedBox(s));
            made.Add(seedCopy);
            Assert.True(seedCopy.IsRewardItem);
            Assert.Equal(SecureLevel.Friends, seedCopy.Level);
            Assert.Equal(3, seedCopy.Entries.Count);
            Assert.Same(seedA, seedCopy.Entries[0].Seed);
            Assert.Equal(entryA.Image, seedCopy.Entries[0].Image);
            Assert.InRange(seedCopy.Entries[0].Image, 2183, 2185);
            Assert.Null(seedCopy.Entries[1]);
            Assert.Same(seedB, seedCopy.Entries[2].Seed);
            Assert.Equal(entryB.Image, seedCopy.Entries[2].Image);
            Assert.Equal(13, seedCopy.TotalCount);
            Assert.Equal(2, seedCopy.UniqueCount);
        }
        finally
        {
            foreach (var item in made)
            {
                item.Delete();
            }

            b.Dispose();
            c.Dispose();
        }
    }

    [Fact]
    public void TheChestOfSendingRechargesAndSendsToTheBank()
    {
        using var p = new Player(withNetState: true);
        var chest = p.Make(new ChestOfSending());
        chest.MoveToWorld(Spot, Map.Trammel);

        // 0-50.
        chest.Charges = 80;
        Assert.Equal(50, chest.Charges);
        chest.Charges = -3;
        Assert.Equal(0, chest.Charges);

        // One charge for each 11-13 hours passed; a new chest's first check adds one, as ServUO's first save does.
        var now = Core.Now;
        chest.Charges = 10;
        chest.NextRecharge = now - TimeSpan.FromHours(1);
        chest.CheckRecharge();
        Assert.Equal(11, chest.Charges);
        Assert.InRange(chest.NextRecharge, now + TimeSpan.FromHours(10), now + TimeSpan.FromHours(12));

        chest.NextRecharge = now - TimeSpan.FromHours(30);
        chest.CheckRecharge();
        Assert.Equal(14, chest.Charges);
        Assert.True(chest.NextRecharge > now);

        chest.NextRecharge = DateTime.MinValue;
        chest.CheckRecharge();
        Assert.Equal(15, chest.Charges);
        Assert.InRange(chest.NextRecharge, now + TimeSpan.FromHours(11), Core.Now + TimeSpan.FromHours(13));

        // Outside a house.
        var mark = p.Mark;
        chest.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 502092)); // You must be in your house to do this.
        Assert.Null(p.Pm.Target);

        // The send, as staff (CheckAccessible lets staff through without a house) with the chest locked down.
        p.Pm.AccessLevel = AccessLevel.GameMaster;
        chest.IsLockedDown = true;
        var ring = p.Pack(new GoldRing());
        chest.TrySend(p.Pm, ring);
        Assert.True(ring.IsChildOf(p.Pm.BankBox));
        Assert.Equal(14, chest.Charges);

        var bag = p.Pack(new Bag());
        mark = p.Mark;
        chest.TrySend(p.Pm, bag);
        Assert.True(p.Sent(mark, 1150420)); // You cannot send a container through the ~1_NAME~.
        Assert.True(bag.IsChildOf(p.Pm.Backpack));
        Assert.Equal(14, chest.Charges);

        chest.Charges = 0;
        chest.NextRecharge = Core.Now + TimeSpan.FromHours(12);
        var dagger = p.Pack(new Dagger());
        mark = p.Mark;
        chest.TrySend(p.Pm, dagger);
        Assert.True(p.Sent(mark, 1019073)); // This item is out of charges.
        Assert.True(dagger.IsChildOf(p.Pm.Backpack));
    }

    [Fact]
    public void TheGreaterBraceletBindsAndReleases()
    {
        var a = new Player();
        var b = new Player(dx: 1);
        var c = new Player(dx: 2);

        try
        {
            var mine = a.Wear(new GreaterBraceletOfBinding());
            var ordinary = b.Wear(new BraceletOfBinding());
            var greaterC = c.Wear(new GreaterBraceletOfBinding());

            // A slot fills only when empty, and filing the pending entry clears it.
            var entry = new GreaterBraceletOfBinding.BindEntry(b.Pm, ordinary);
            mine.Pending = entry;
            mine.Add(entry, 4);
            Assert.Same(entry, mine.Friends[4]);
            Assert.Null(mine.Pending);
            mine.Add(new GreaterBraceletOfBinding.BindEntry(c.Pm, greaterC), 4);
            Assert.Same(b.Pm, mine.Friends[4].Mobile);
            Assert.True(mine.IsBound(b.Pm));
            mine.Remove(b.Pm);
            Assert.False(mine.IsBound(b.Pm));
            Assert.All(mine.Friends, e => Assert.Null(e));

            // b, wearing an ordinary bracelet, accepts a's request for slot 2.
            new GreaterBraceletOfBinding.ConfirmBindGump(b.Pm, a.Pm, 2, mine, false).OnResponse(null, Button(1));
            Assert.Same(b.Pm, mine.Friends[2].Mobile);
            Assert.Same(ordinary, mine.Friends[2].Bracelet);
            Assert.Same(mine, ordinary.Bound);

            // c, wearing a greater bracelet, accepts a's request for slot 5: c is left to choose a slot for a.
            new GreaterBraceletOfBinding.ConfirmBindGump(c.Pm, a.Pm, 5, mine, false).OnResponse(null, Button(1));
            Assert.Same(c.Pm, mine.Friends[5].Mobile);
            Assert.NotNull(greaterC.Pending);
            Assert.Same(a.Pm, greaterC.Pending.Mobile);
            Assert.Same(mine, greaterC.Pending.Bracelet);

            var chooser = new GreaterBraceletOfBinding.GreaterBraceletOfBindingGump(c.Pm, greaterC, greaterC.Pending);
            chooser.OnResponse(null, Button(10 + 7));
            Assert.Same(a.Pm, greaterC.Friends[7].Mobile);
            Assert.Null(greaterC.Pending);
            Assert.False(chooser.Choose);

            // Declining binds nothing.
            new GreaterBraceletOfBinding.ConfirmBindGump(b.Pm, a.Pm, 8, mine, false).OnResponse(null, Button(2));
            Assert.Null(mine.Friends[8]);

            // a releases c: both sides clear.
            new GreaterBraceletOfBinding.ConfirmBindGump(a.Pm, c.Pm, 5, greaterC, true).OnResponse(null, Button(1));
            Assert.Null(mine.Friends[5]);
            Assert.False(greaterC.IsBound(a.Pm));
            Assert.Same(b.Pm, mine.Friends[2].Mobile);

            // Binding to oneself is refused by the slot target.
            var gump = new GreaterBraceletOfBinding.GreaterBraceletOfBindingGump(a.Pm, mine);
            gump.OnBindTarget(a.Pm, 0);
            Assert.Null(mine.Friends[0]);
        }
        finally
        {
            a.Dispose();
            b.Dispose();
            c.Dispose();
        }
    }

    [Fact]
    public void ThePenOfWisdomCopiesTickedEntries()
    {
        using var p = new Player();
        var pen = p.Pack(new PenOfWisdom());
        var source = p.Pack(new Runebook());
        var copy = p.Pack(new Runebook());

        source.Entries.Add(new RunebookEntry(source, new Point3D(1, 2, 3), Map.Trammel, "first"));
        source.Entries.Add(new RunebookEntry(source, new Point3D(4, 5, 6), Map.Felucca, "second"));
        source.Entries.Add(new RunebookEntry(source, new Point3D(7, 8, 9), Map.Ilshenar, "third"));

        p.Pack(new MarkScroll(3));
        var runes = new List<RecallRune> { p.Pack(new RecallRune()), p.Pack(new RecallRune()) };

        // Two runes for three entries: nothing is copied.
        var gump = new PenOfWisdomGump(p.Pm, pen, source, copy, null);
        gump.OnResponse(null, Button(21)); // Select All
        Assert.Equal(3, gump.Checked.Count);
        gump.OnResponse(null, Button(22)); // Okay
        Assert.Empty(copy.Entries);
        Assert.Equal(100, pen.UsesRemaining);

        // A third rune; untick the second entry, then Okay copies two.
        runes.Add(p.Pack(new RecallRune()));
        gump = new PenOfWisdomGump(p.Pm, pen, source, copy, null);
        Assert.Equal(3, gump.RuneAmount);
        Assert.Equal(3, gump.MarkScrollAmount);
        Assert.Equal(16, gump.Blank);
        gump.OnResponse(null, Button(21));
        gump.OnResponse(null, Button(1)); // the second entry's button
        Assert.Equal(2, gump.Checked.Count);
        gump.OnResponse(null, Button(22));

        Assert.Equal(2, copy.Entries.Count);
        Assert.Equal(new Point3D(1, 2, 3), copy.Entries[0].Location);
        Assert.Equal("first", copy.Entries[0].Description);
        Assert.Equal(Map.Ilshenar, copy.Entries[1].Map);
        Assert.Equal("third", copy.Entries[1].Description);
        Assert.DoesNotContain(copy.Entries[0], source.Entries); // a copy, not the source's entry
        Assert.Equal(3, source.Entries.Count);
        Assert.Equal(99, pen.UsesRemaining);
        Assert.Equal(1, p.Pm.Backpack.GetAmount(typeof(RecallRune), true));
        Assert.Equal(1, p.Pm.Backpack.GetAmount(typeof(MarkScroll), true));
        Assert.False(pen.Deleted);
    }

    [Fact]
    public void TheJewelryBoxTakesJewelryAndFilters()
    {
        using var p = new Player();
        p.Pm.AccessLevel = AccessLevel.GameMaster;
        var box = p.Make(new JewelryBox());
        box.MoveToWorld(Spot, Map.Trammel);

        var ring = p.Pack(new GoldRing());
        var bracelet = p.Pack(new GoldBracelet());
        var talisman = p.Pack(new RandomTalisman());
        var necklace = p.Pack(new GoldNecklace());
        var earrings = p.Pack(new GoldEarrings());
        var dagger = p.Pack(new Dagger());

        Assert.True(box.IsAccept(ring));
        Assert.True(box.IsAccept(bracelet));
        Assert.True(box.IsAccept(necklace));
        Assert.True(box.IsAccept(earrings));
        Assert.True(box.IsAccept(talisman));
        Assert.False(box.IsAccept(dagger));

        // Not locked down: refused.
        Assert.False(box.OnDragDrop(p.Pm, ring));
        Assert.True(ring.IsChildOf(p.Pm.Backpack));

        box.IsLockedDown = true;
        Assert.True(box.OnDragDrop(p.Pm, ring));
        Assert.True(box.OnDragDrop(p.Pm, bracelet));
        Assert.False(box.OnDragDrop(p.Pm, dagger));
        Assert.True(ring.IsChildOf(box));
        Assert.True(dagger.IsChildOf(p.Pm.Backpack));

        // One kind at a time.
        box.Filter.Ring = true;
        box.Filter.Bracelet = true;
        Assert.False(box.Filter.Ring);
        Assert.True(box.Filter.Bracelet);
        Assert.Equal(new Item[] { bracelet }, new JewelryBoxGump(p.Pm, box).Shown);

        var gump = new JewelryBoxGump(p.Pm, box);
        gump.OnResponse(null, Button(101)); // Ring
        Assert.True(box.Filter.Ring);
        Assert.False(box.Filter.Bracelet);
        Assert.Equal(new Item[] { ring }, new JewelryBoxGump(p.Pm, box).Shown);

        gump.OnResponse(null, Button(132)); // All
        Assert.True(box.Filter.IsDefault);
        Assert.Equal(2, new JewelryBoxGump(p.Pm, box).Shown.Count);

        // Clicking a piece takes it to the backpack.
        new JewelryBoxGump(p.Pm, box).OnResponse(null, Button((int)ring.Serial.Value));
        Assert.True(ring.IsChildOf(p.Pm.Backpack));
        Assert.True(bracelet.IsChildOf(box));
    }

    [Fact]
    public void TheSeedBoxStoresAndGivesBackBySlot()
    {
        using var p = new Player();
        var box = p.Pack(new SeedBox());

        var red = p.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Red) { Amount = 5 });
        Assert.True(box.TryAddSeed(p.Pm, red));
        Assert.True(red.IsChildOf(box));
        Assert.False(red.Movable);
        Assert.True(red.ShowType);
        Assert.Equal(19289, box.ItemID);
        Assert.Single(box.Entries);
        Assert.Equal(5, box.TotalCount);

        // The same kind stacks onto its slot.
        var moreRed = p.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Red) { Amount = 3 });
        Assert.True(box.TryAddSeed(p.Pm, moreRed));
        Assert.True(moreRed.Deleted);
        Assert.Single(box.Entries);
        Assert.Equal(8, box.Entries[0].Seed.Amount);

        // Another hue opens a slot.
        var blue = p.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Blue) { Amount = 2 });
        Assert.True(box.OnDragDrop(p.Pm, blue));
        Assert.Equal(2, box.Entries.Count);
        Assert.Equal(2, box.UniqueCount);
        Assert.Equal(10, box.TotalCount);

        // Over 5,000 and non-seeds are refused. Staff for the big stack, so the backpack's weight limit is not what
        // refuses it.
        p.Pm.AccessLevel = AccessLevel.GameMaster;
        var tooMany = p.Pack(new Seed(PlantType.Poppies, PlantHue.Plain) { Amount = 4991 });
        Assert.False(box.TryAddSeed(p.Pm, tooMany));
        Assert.True(tooMany.IsChildOf(p.Pm.Backpack));
        Assert.Equal(10, box.TotalCount);
        tooMany.Delete();
        p.Pm.AccessLevel = AccessLevel.Player;
        Assert.False(box.OnDragDrop(p.Pm, p.Pack(new Dagger())));

        // Shift the blue slot right, then back left: ServUO trims only two or more trailing empty slots.
        var redEntry = box.Entries[0];
        var blueEntry = box.Entries[1];
        new SeedInfoGump(p.Pm, box, blueEntry, null).OnResponse(null, Button(5));
        Assert.Equal(new[] { redEntry, null, blueEntry }, box.Entries);
        new SeedInfoGump(p.Pm, box, blueEntry, null).OnResponse(null, Button(6));
        Assert.Equal(new[] { redEntry, blueEntry, null }, box.Entries);

        // Remove one red, then all of the blue.
        new SeedInfoGump(p.Pm, box, redEntry, null).OnResponse(null, Button(1));
        Assert.Equal(7, redEntry.Seed.Amount);
        var given = p.Pm.Backpack.Items.OfType<Seed>().Single(s => s.PlantHue == PlantHue.Red);
        p.Made.Add(given);
        Assert.Equal(1, given.Amount);
        Assert.True(given.Movable);
        Assert.True(given.ShowType);

        new SeedInfoGump(p.Pm, box, blueEntry, null).OnResponse(null, Button(2));
        Assert.True(blue.IsChildOf(p.Pm.Backpack));
        Assert.True(blue.Movable);
        Assert.DoesNotContain(blueEntry, box.Entries);
        Assert.Equal(7, box.TotalCount);
        Assert.Equal(1, box.UniqueCount);

        // The box gump pages by twenty slots.
        var page = new SeedBoxGump(p.Pm, box);
        Assert.Equal(1, page.Pages);
        page.CheckPage(redEntry);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public void TheForgedMetalSetsAndRefundsTheAura()
    {
        using var p = new Player();
        var tool = p.Pack(new ForgedMetalOfArtifacts(2));

        try
        {
            Assert.False(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));

            new ForgedMetalOfArtifacts.InternalGump(p.Pm, tool).OnResponse(null, Button(0)); // CANCEL
            Assert.False(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));
            Assert.Equal(2, tool.UsesRemaining);

            new ForgedMetalOfArtifacts.InternalGump(p.Pm, tool).OnResponse(null, Button(1)); // OKAY
            Assert.True(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));
            Assert.Equal(1, tool.UsesRemaining);

            new ForgedMetalOfArtifacts.CancelGump(p.Pm, tool).OnResponse(null, Button(1)); // REMOVE
            Assert.False(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));
            Assert.Equal(2, tool.UsesRemaining);

            // Out of the pack: no refund, the aura stays.
            new ForgedMetalOfArtifacts.InternalGump(p.Pm, tool).OnResponse(null, Button(1));
            tool.MoveToWorld(Spot, Map.Trammel);
            new ForgedMetalOfArtifacts.CancelGump(p.Pm, tool).OnResponse(null, Button(1));
            Assert.True(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));
            Assert.Equal(1, tool.UsesRemaining);

            // The last use wears it out.
            p.Pm.Backpack.DropItem(tool);
            tool.Use(p.Pm);
            Assert.True(tool.Deleted);
            Assert.True(ForgedMetalOfArtifacts.GetNextEnhanceSuccess(p.Pm));
        }
        finally
        {
            ForgedMetalOfArtifacts.SetNextEnhanceSuccess(p.Pm, false);
        }
    }

    [Fact]
    public void TheSeedInfoLabelIsPinnedsSeedLabel()
    {
        var getLabel = typeof(Seed).GetMethod("GetLabel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(getLabel);

        var seeds = new[]
        {
            new Seed(PlantType.CampionFlowers, PlantHue.Red, true),
            new Seed(PlantType.Poppies, PlantHue.BrightRed, false) { Amount = 3 },
            new Seed(PlantType.Lilies, PlantHue.Plain, true) { Amount = 12 },
            new Seed(PlantType.Bulrushes, PlantHue.Blue, false)
        };

        try
        {
            foreach (var seed in seeds)
            {
                var args = new object[] { null };
                var expected = (int)getLabel.Invoke(seed, args);
                var actual = SeedInfoGump.SeedLabel(seed, out var actualArgs);

                Assert.Equal(expected, actual);
                Assert.Equal((string)args[0], actualArgs);
            }
        }
        finally
        {
            foreach (var seed in seeds)
            {
                seed.Delete();
            }
        }
    }
}
