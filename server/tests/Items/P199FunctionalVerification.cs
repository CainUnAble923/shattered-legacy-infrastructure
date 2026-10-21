// cc-P199: the Functional batch (ServUO Items/Functional and Equipment/Instruments/SnakeCharmerFlute.cs ->
// customizations/Items/Functional/ and customizations/Items/Equipment/Instruments/).
//
// Facts:
//   1. Every ported type constructs as ServUO makes it: art, label, hue, blessing, movability, visibility, uses.
//   2. Every ported type with saved state survives a serialization round trip with each saved field set; a switch
//      comes back off and unused, as ServUO's Deserialize leaves it.
//   3. The grinder refuses until locked down; its target turns a pack stack of coffee pods into as many grounds and
//      refuses pods outside the pack and anything else.
//   4. The magic vines answer by distance: one tile, four tiles, further.
//   5. A decaying item counts down ten seconds a slice, shows its lifespan in seconds and expires from the pack with
//      "The ~1_name~ expired..."; with UseSeconds false it shows the largest of weeks, days, hours, minutes, seconds.
//   6. The three sliding doors take ServUO's art for a facing, sound 0x539, and open onto the next art in place.
//   7. A dead lever: the first flip turns it on, later flips toggle it, every message is one of 500357-500361, and two
//      minutes later it is off and unused; out of reach it says "I can't reach that."
//   8. The Underworld switch says 1042901 and lifts a hue-1000 Globe of Sosaria 8 Z and sets it back; an unhued globe
//      does not move. A second flip says 1042900.
//   9. An artifact light shows "artifact rarity ~1_val~" unless ShowArtifactRarity is false.
//  10. An unowned goblin trap hurts a player who steps on it for 50-75, shows itself with blood on the floor, and hides
//      again 10 seconds later; it spares staff, the dead and its owner.
//  11. The trap kit needs 80 Tinkering; at 80 its target sets a hidden trap owned by the setter and uses the kit up;
//      a spot more than two tiles away is too far.
//  12. The scale collar freezes an untamed battle chicken lizard for 30 seconds (refusing reuse meanwhile) and then
//      frees it with "The chicken frees itself of the collar!!"; taming it frees it at once.
//  13. Coral the Owl, locked down, answers "news" with "Some of the latest news!" and the entry's lines at 1 and 4
//      seconds, or "I have no news at this time."; not locked down it says nothing; a reward copy shows 1076217.
//  14. The snake charmer flute charms a snake to a spot (pinned's herding field), costs a use, is spent for 5 seconds,
//      refuses a second charmer, and the charm wears off after 20 seconds with 1112181.
//  15. The flute refuses a non-snake and breaks on its last use.
//
// Host notes: timers through ShardTestClock (Arm before anything that schedules, Advance to fire); hit points read on
// the 8 ms tick the damage lands (port recipe); messages read off a test NetState's send buffer (0xC1 localized, 0xAE
// unicode); every fact deletes what it made.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Targeting;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P199FunctionalVerification
{
    private static readonly Point3D Spot = new(1400, 1720, 0);

    public P199FunctionalVerification()
    {
        ShardTestHost.EnsureSkillChecks();
    }

    private static Point3D At(int dx, int dy = 0) => new(Spot.X + dx, Spot.Y + dy, Spot.Z);

    // A player at Spot with a backpack and, optionally, a test NetState whose send buffer the fact reads.
    private sealed class Player : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Made = new();

        public Player(bool withNetState = true)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(Spot, Map.Trammel);
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

        public T Place<T>(T item, Point3D p) where T : Item
        {
            item.MoveToWorld(p, Map.Trammel);
            Made.Add(item);
            return item;
        }

        public bool Sent(int from, int cliloc) => SentLocalized(Ns, from, cliloc);

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

    private static bool SentLocalized(NetState ns, int from, int cliloc)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 18 <= span.Length; i++)
        {
            if (span[i] == 0xC1 && BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf((ReadOnlySpan<byte>)Encoding.BigEndianUnicode.GetBytes(text)) >= 0;
    }

    private static List<T> ItemsAt<T>(Point3D p) where T : Item
    {
        var list = new List<T>();
        foreach (var item in Map.Trammel.GetItemsInRange<T>(p, 0))
        {
            if (item.Location.X == p.X && item.Location.Y == p.Y)
            {
                list.Add(item);
            }
        }

        return list;
    }

    private static int SentAnyOf(NetState ns, int from, int low, int high)
    {
        for (var c = low; c <= high; c++)
        {
            if (SentLocalized(ns, from, c))
            {
                return c;
            }
        }

        return 0;
    }

    private sealed class TestArtifactLight : BaseArtifactLight
    {
        public bool Show = true;

        public TestArtifactLight() : base(0xA22)
        {
        }

        public override int ArtifactRarity => 7;
        public override int LitItemID => 0xA22;
        public override bool ShowArtifactRarity => Show;
    }

    private sealed class TestDecaying : BaseDecayingItem
    {
        public TestDecaying() : base(0x1F14)
        {
        }

        public override int Lifespan => 30;
    }

    private sealed class TestDecayingLong : BaseDecayingItem
    {
        public TestDecayingLong() : base(0x1F14)
        {
        }

        public override int Lifespan => 4 * 7 * 86400;
        public override bool UseSeconds => false;
    }

    [Fact]
    public void EveryPortedTypeConstructsAsServUOMakesIt()
    {
        ShardTestClock.Arm();
        var made = new List<Item>();
        T Keep<T>(T i) where T : Item
        {
            made.Add(i);
            return i;
        }

        try
        {
            var grinder = Keep(new Grinder());
            Assert.Equal(0x9A97, grinder.ItemID);
            Assert.Equal(1123599, grinder.LabelNumber);
            Assert.Equal(LootType.Blessed, grinder.LootType);
            Assert.Equal(SecureLevel.Owner, grinder.Level);

            var vine = Keep(new AcidVine());
            Assert.Equal(3313, vine.ItemID);
            Assert.Equal(1111655, vine.LabelNumber);
            Assert.False(vine.Movable);
            Assert.Equal(1.0, vine.Weight);

            var decaying = Keep(new BaseDecayingItem(0x1F14));
            Assert.Equal(LootType.Blessed, decaying.LootType);
            Assert.Equal(0, decaying.Lifespan);
            Assert.Equal(0, decaying.TimeLeft);
            Assert.True(decaying.UseSeconds);

            var paper = Keep(new PaperSlidingDoor(DoorFacing.WestCW));
            Assert.Equal(0x2A05, paper.ItemID);
            Assert.Equal(0x2A06, paper.OpenedId);
            var cloth = Keep(new ClothSlidingDoor(DoorFacing.EastCCW));
            Assert.Equal(0x2A0F, cloth.ItemID);
            Assert.Equal(0x2A10, cloth.OpenedId);
            var wooden = Keep(new WoodenSlidingDoor(DoorFacing.EastCW));
            Assert.Equal(0x2A1B, wooden.ItemID);
            Assert.Equal(0x2A1C, wooden.OpenedId);
            foreach (var door in new BaseDoor[] { paper, cloth, wooden })
            {
                Assert.Equal(0x539, door.OpenedSound);
                Assert.Equal(0x539, door.ClosedSound);
                Assert.Equal(new Point3D(0, 0, 0), door.Offset);
                Assert.False(door.Movable);
            }

            var lever = Keep(new BaseSwitch(0x1091, 0x1092));
            Assert.Equal(0x1091, lever.ItemID);
            Assert.Equal(0x1092, lever.TurnOn);
            Assert.Equal(0, lever.LocMessageA);
            Assert.False(lever.Working);
            Assert.False(lever.Movable);

            var underworld = Keep(new UnderworldSwitchWE());
            Assert.Equal(0x1091, underworld.ItemID);
            Assert.Equal(0x1092, underworld.TurnOn);
            Assert.Equal(1042901, underworld.LocMessageA);
            Assert.Equal(1042900, underworld.LocMessageB);
            Assert.True(underworld.Working);

            var light = Keep(new TestArtifactLight());
            Assert.IsAssignableFrom<BaseLight>(light);
            Assert.True(typeof(BaseArtifactLight).IsAbstract);

            var trap = Keep(new GoblinFloorTrap());
            Assert.Equal(0x4004, trap.ItemID);
            Assert.Equal(1113296, trap.LabelNumber);
            Assert.False(trap.Visible);
            Assert.False(trap.Movable);
            Assert.Null(trap.Owner);
            Assert.True(trap.PassivelyTriggered);
            Assert.Equal(1, trap.PassiveTriggerRange);
            Assert.Equal(TimeSpan.FromSeconds(1.0), trap.PassiveTriggerDelay);
            Assert.Equal(TimeSpan.FromSeconds(1.0), trap.ResetDelay);
            Assert.True(trap.CheckWhenHidden);

            var kit = Keep(new GoblinFloorTrapKit());
            Assert.Equal(16704, kit.ItemID);
            Assert.True(kit.Movable);

            var collar = Keep(new ScaleCollar());
            Assert.Equal(4235, collar.ItemID);
            Assert.Equal(2125, collar.Hue);
            Assert.Equal(1112480, collar.LabelNumber);

            var owl = Keep(new CoralTheOwl());
            Assert.Equal(0x9A9B, owl.ItemID);
            Assert.Equal(1123603, owl.LabelNumber);
            Assert.Equal(LootType.Blessed, owl.LootType);
            Assert.False(owl.IsRewardItem);
            Assert.True(owl.HandlesOnSpeech);

            var flute = Keep(new SnakeCharmerFlute());
            Assert.Equal(0x2805, flute.ItemID);
            Assert.Equal(1112174, flute.LabelNumber);
            Assert.Equal(0x187, flute.Hue);
            Assert.InRange(flute.UsesRemaining, 50, 80);
            Assert.IsAssignableFrom<BambooFlute>(flute);
        }
        finally
        {
            foreach (var i in made)
            {
                i.Delete();
            }
        }
    }

    [Fact]
    public void EveryPortedTypeSurvivesARoundTrip()
    {
        ShardTestClock.Arm();
        var owner = new PlayerMobile { Player = true };
        var copies = new List<Item>();

        try
        {
            var grinder = new Grinder { Level = SecureLevel.Friends };
            var g = ConsumablesVerification.RoundTrip(grinder, s => new Grinder(s));
            copies.Add(g);
            Assert.Equal(SecureLevel.Friends, g.Level);

            var decaying = new BaseDecayingItem(0x1F14) { TimeLeft = 1234 };
            var d = ConsumablesVerification.RoundTrip(decaying, s => new BaseDecayingItem(s));
            copies.Add(d);
            Assert.Equal(1234, d.TimeLeft);
            Assert.Equal(LootType.Blessed, d.LootType);

            var door = new ClothSlidingDoor(DoorFacing.WestCCW);
            var dc = ConsumablesVerification.RoundTrip(door, s => new ClothSlidingDoor(s));
            copies.Add(dc);
            Assert.Equal(0x2A11, dc.ItemID);
            Assert.Equal(0x2A12, dc.OpenedId);
            Assert.Equal(0x539, dc.OpenedSound);

            var lever = new BaseSwitch(0x1091, 0x1092, 1042901, 1042900, true) { Used = true };
            lever.ItemID = 0x1092;
            var l = ConsumablesVerification.RoundTrip(lever, s => new BaseSwitch(s));
            copies.Add(l);
            Assert.Equal(0x1092, l.TurnOn);
            Assert.Equal(0x1091, l.TurnOff);
            Assert.Equal(1042901, l.LocMessageA);
            Assert.Equal(1042900, l.LocMessageB);
            Assert.True(l.Working);
            Assert.False(l.Used);          // ServUO BaseSwitch.cs:137: reset on load
            Assert.Equal(0x1091, l.ItemID); // ServUO BaseSwitch.cs:138

            var underworld = new UnderworldSwitchWE();
            var u = ConsumablesVerification.RoundTrip(underworld, s => new UnderworldSwitchWE(s));
            copies.Add(u);
            Assert.Equal(1042901, u.LocMessageA);
            Assert.True(u.Working);

            var trap = new GoblinFloorTrap(owner);
            var t = ConsumablesVerification.RoundTrip(trap, s => new GoblinFloorTrap(s));
            copies.Add(t);
            Assert.Same(owner, t.Owner);
            Assert.False(t.Visible);

            var kit = new GoblinFloorTrapKit();
            var k = ConsumablesVerification.RoundTrip(kit, s => new GoblinFloorTrapKit(s));
            copies.Add(k);
            Assert.Equal(16704, k.ItemID);

            var vine = new AcidVine();
            var v = ConsumablesVerification.RoundTrip(vine, s => new AcidVine(s));
            copies.Add(v);
            Assert.False(v.Movable);

            var collar = new ScaleCollar();
            var c = ConsumablesVerification.RoundTrip(collar, s => new ScaleCollar(s));
            copies.Add(c);
            Assert.Equal(2125, c.Hue);

            var owl = new CoralTheOwl { IsRewardItem = true };
            var o = ConsumablesVerification.RoundTrip(owl, s => new CoralTheOwl(s));
            copies.Add(o);
            Assert.True(o.IsRewardItem);
            Assert.True(PropertyListReader.Read(o).Has(1076217));

            var flute = new SnakeCharmerFlute { UsesRemaining = 61 };
            var f = ConsumablesVerification.RoundTrip(flute, s => new SnakeCharmerFlute(s));
            copies.Add(f);
            Assert.Equal(61, f.UsesRemaining);
            Assert.Equal(0x187, f.Hue);
        }
        finally
        {
            foreach (var i in copies)
            {
                i.Delete();
            }

            owner.Delete();
        }
    }

    [Fact]
    public void TheGrinderNeedsLockingDownAndGrindsAPackStackOfPods()
    {
        using var p = new Player();
        var grinder = p.Place(new Grinder(), At(1));

        var mark = p.Mark;
        grinder.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1114298)); // This must be locked down in order to use it.
        Assert.Null(p.Pm.Target);

        // The grinder's own target (a locked-down grinder in a house hands out exactly this one).
        var targetType = typeof(Grinder).GetNestedType("InternalTarget", BindingFlags.NonPublic);
        Assert.NotNull(targetType);

        var pod = p.Pack(new CoffeePod(7));
        p.Pm.Target = (Target)Activator.CreateInstance(targetType!);
        p.Pm.Target.Invoke(p.Pm, pod);
        Assert.True(pod.Deleted);
        var grounds = p.Pm.Backpack.FindItemByType<CoffeeGrounds>();
        Assert.NotNull(grounds);
        Assert.Equal(7, grounds.Amount);

        var loose = p.Place(new CoffeePod(3), At(1, 1));
        mark = p.Mark;
        p.Pm.Target = (Target)Activator.CreateInstance(targetType!);
        p.Pm.Target.Invoke(p.Pm, loose);
        Assert.True(p.Sent(mark, 1042001)); // That must be in your pack for you to use it.
        Assert.False(loose.Deleted);

        var other = p.Pack(new Backpack());
        mark = p.Mark;
        p.Pm.Target = (Target)Activator.CreateInstance(targetType!);
        p.Pm.Target.Invoke(p.Pm, other);
        Assert.True(p.Sent(mark, 1155729)); // That is not something that can be ground.
    }

    [Fact]
    public void TheVinesAnswerByDistance()
    {
        using var p = new Player();
        var vine = p.Place(new AcidVine(), Spot);

        void Check(int dx, int cliloc)
        {
            p.Pm.MoveToWorld(At(dx), Map.Trammel);
            var mark = p.Mark;
            vine.OnDoubleClick(p.Pm);
            Assert.True(p.Sent(mark, cliloc), $"at {dx} tiles expected {cliloc}");
        }

        Check(1, 1111658);  // The vines tighten their grip, stopping you from opening the door.
        Check(3, 1111665);  // You notice something odd about the vines covering the wall.
        Check(6, 1019045);  // I can't reach that.
    }

    [Fact]
    public void ADecayingItemCountsDownAndExpiresFromThePack()
    {
        using var p = new Player();
        var item = p.Pack(new TestDecaying());

        Assert.Equal(30, item.TimeLeft);
        Assert.True(PropertyListReader.Read(item).Has(1072517, "30")); // Lifespan: ~1_val~ seconds

        ShardTestClock.AdvanceSeconds(10.5);
        Assert.Equal(20, item.TimeLeft);
        Assert.False(item.Deleted);

        var mark = p.Mark;
        ShardTestClock.AdvanceSeconds(20.5);
        Assert.True(item.Deleted);
        Assert.True(p.Sent(mark, 1072515)); // The ~1_name~ expired...
    }

    [Fact]
    public void ADecayingItemNotInSecondsShowsItsLargestUnit()
    {
        ShardTestClock.Arm();
        var item = new TestDecayingLong();

        try
        {
            Assert.Equal(4 * 7 * 86400, item.TimeLeft);
            Assert.True(PropertyListReader.Read(item).Has(1153092, "4")); // weeks

            item.TimeLeft = 5 * 86400;
            Assert.True(PropertyListReader.Read(item).Has(1153091, "5")); // days

            item.TimeLeft = 5 * 3600;
            Assert.True(PropertyListReader.Read(item).Has(1153090, "5")); // hours

            item.TimeLeft = 5 * 60;
            Assert.True(PropertyListReader.Read(item).Has(1153089, "5")); // minutes

            item.TimeLeft = 45;
            Assert.True(PropertyListReader.Read(item).Has(1072517, "45")); // seconds

            var never = new BaseDecayingItem(0x1F14) { TimeLeft = 45 };
            try
            {
                Assert.False(PropertyListReader.Read(never).Has(1072517)); // Lifespan 0 shows nothing
            }
            finally
            {
                never.Delete();
            }
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void TheSlidingDoorsOpenOntoTheirNextArtInPlace()
    {
        using var p = new Player(false);
        var door = p.Place(new PaperSlidingDoor(DoorFacing.EastCW), At(1));
        Assert.Equal(0x2A0B, door.ItemID); // ServUO Citadel.cfg:138 places this one as 0x2A0B

        door.Open = true;
        Assert.Equal(0x2A0C, door.ItemID);
        Assert.Equal(At(1), door.Location);

        door.Open = false;
        Assert.Equal(0x2A0B, door.ItemID);
        Assert.Equal(At(1), door.Location);
    }

    [Fact]
    public void ADeadLeverTogglesAndResetsAfterTwoMinutes()
    {
        using var p = new Player();
        var lever = p.Place(new BaseSwitch(0x1091, 0x1092), At(1));

        var mark = p.Mark;
        lever.OnDoubleClick(p.Pm);
        Assert.Equal(0x1092, lever.ItemID);
        Assert.True(lever.Used);
        Assert.NotEqual(0, SentAnyOf(p.Ns, mark, 500357, 500361));
        Assert.False(p.Sent(mark, 1116629)); // a dead lever does nothing special

        mark = p.Mark;
        lever.OnDoubleClick(p.Pm);
        Assert.Equal(0x1091, lever.ItemID);
        Assert.NotEqual(0, SentAnyOf(p.Ns, mark, 500357, 500361));

        lever.OnDoubleClick(p.Pm);
        Assert.Equal(0x1092, lever.ItemID);
        Assert.True(lever.Used);

        ShardTestClock.Advance(TimeSpan.FromMinutes(2.0) + TimeSpan.FromSeconds(1));
        Assert.Equal(0x1091, lever.ItemID);
        Assert.False(lever.Used);

        p.Pm.MoveToWorld(At(5), Map.Trammel);
        mark = p.Mark;
        lever.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1019045)); // I can't reach that.
        Assert.Equal(0x1091, lever.ItemID);
    }

    [Fact]
    public void TheUnderworldSwitchLiftsTheGlobeAndSetsItBack()
    {
        using var p = new Player();
        var sw = p.Place(new UnderworldSwitchWE(), At(1));
        var globe = p.Place(new Static(0x3660) { Hue = 1000 }, At(4));
        var plain = p.Place(new Static(0x3660), At(4, 1));

        var mark = p.Mark;
        sw.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1042901)); // You hear a deep rumbling as something seems to happen.

        var maxZ = globe.Z;
        for (var i = 0; i < 80; i++)
        {
            ShardTestClock.AdvanceSeconds(0.5);
            maxZ = Math.Max(maxZ, globe.Z);
        }

        Assert.Equal(Spot.Z + 8, maxZ);
        Assert.Equal(Spot.Z, globe.Z);
        Assert.Equal(Spot.Z, plain.Z);

        mark = p.Mark;
        sw.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1042900)); // There seems to be no further effect right now.
    }

    [Fact]
    public void TheArtifactLightShowsItsRarityUnlessTold()
    {
        ShardTestClock.Arm();
        var light = new TestArtifactLight();

        try
        {
            Assert.True(PropertyListReader.Read(light).Has(1061078, "7")); // artifact rarity ~1_val~
            light.Show = false;
            Assert.False(PropertyListReader.Read(light).Has(1061078));
        }
        finally
        {
            light.Delete();
        }
    }

    [Fact]
    public void AnUnownedGoblinTrapHurtsShowsItselfAndHidesAgain()
    {
        using var p = new Player();
        var trap = p.Place(new GoblinFloorTrap(), At(1));
        var before = p.Pm.HitsMax;
        p.Pm.Hits = before;

        var mark = p.Mark;
        trap.OnTrigger(p.Pm);
        Assert.True(p.Sent(mark, 1095157)); // You stepped onto a goblin trap!
        Assert.True(trap.Visible);
        var blood = ItemsAt<Blood>(Spot);
        Assert.NotEmpty(blood);

        var lowest = p.Pm.Hits;
        for (var i = 0; i < 75; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));
            lowest = Math.Min(lowest, p.Pm.Hits);
        }

        Assert.InRange(before - lowest, 49, 75);

        ShardTestClock.AdvanceSeconds(10.5);
        Assert.False(trap.Visible);

        foreach (var b in ItemsAt<Blood>(Spot))
        {
            b.Delete();
        }
    }

    [Fact]
    public void TheGoblinTrapSparesItsOwnerStaffAndTheDead()
    {
        using var p = new Player();
        var trap = p.Place(new GoblinFloorTrap(p.Pm), At(1));

        var mark = p.Mark;
        trap.OnTrigger(p.Pm);
        Assert.False(p.Sent(mark, 1095157));
        Assert.False(trap.Visible);

        trap.Owner = null;
        p.Pm.AccessLevel = AccessLevel.GameMaster;
        mark = p.Mark;
        trap.OnTrigger(p.Pm);
        Assert.False(p.Sent(mark, 1095157));
        Assert.False(trap.Visible);
        p.Pm.AccessLevel = AccessLevel.Player;

        p.Pm.Body = 0x192; // a ghost: Alive false (port recipe, test-NetState trap 3)
        Assert.False(p.Pm.Alive);
        mark = p.Mark;
        trap.OnTrigger(p.Pm);
        Assert.False(p.Sent(mark, 1095157));
        Assert.False(trap.Visible);
    }

    [Fact]
    public void TheTrapKitNeedsTinkeringAndSetsAnOwnedHiddenTrap()
    {
        using var p = new Player();
        var guarded = Region.Find(Spot, Map.Trammel) as GuardedRegion;
        var wasDisabled = guarded?.GuardsDisabled ?? false;
        if (guarded != null)
        {
            guarded.GuardsDisabled = true;
        }

        try
        {
            var kit = p.Pack(new GoblinFloorTrapKit());

            p.Pm.Skills[SkillName.Tinkering].Base = 79.9;
            var mark = p.Mark;
            kit.OnDoubleClick(p.Pm);
            Assert.True(p.Sent(mark, 1113318)); // You do not have enough skill to set the trap.
            Assert.Null(p.Pm.Target);

            p.Pm.Skills[SkillName.Tinkering].Base = 80.0;
            kit.OnDoubleClick(p.Pm);
            Assert.NotNull(p.Pm.Target);

            mark = p.Mark;
            p.Pm.Target.Invoke(p.Pm, new StaticTarget(At(3), 0x1));
            Assert.True(p.Sent(mark, 500446)); // That is too far away.
            Assert.False(kit.Deleted);

            kit.OnDoubleClick(p.Pm);
            mark = p.Mark;
            p.Pm.Target.Invoke(p.Pm, new StaticTarget(At(2), 0x1));
            Assert.True(p.Sent(mark, 1113294)); // You carefully arm the goblin trap.
            Assert.True(p.Sent(mark, 1113297)); // You hide the trap to the best of your ability.
            Assert.True(kit.Deleted);

            var traps = ItemsAt<GoblinFloorTrap>(At(2));
            foreach (var t in traps)
            {
                p.Made.Add(t);
            }

            var trap = Assert.Single(traps);
            Assert.Same(p.Pm, trap.Owner);
            Assert.False(trap.Visible);
        }
        finally
        {
            if (guarded != null)
            {
                guarded.GuardsDisabled = wasDisabled;
            }
        }
    }

    [Fact]
    public void TheScaleCollarFreezesAChickenForThirtySeconds()
    {
        using var p = new Player();
        var collar = p.Pack(new ScaleCollar());
        var lizard = p.Make(new BattleChickenLizard());
        lizard.MoveToWorld(Spot, Map.Trammel); // distance 0 counts as 1: a 50% chance a throw
        lizard.FightMode = FightMode.None;

        collar.OnDoubleClick(p.Pm);
        Assert.NotNull(p.Pm.Target);
        p.Pm.Target = null;

        for (var i = 0; i < 200 && !lizard.Frozen; i++)
        {
            collar.OnTarget(p.Pm, lizard);
        }

        Assert.True(lizard.Frozen);

        var mark = p.Mark;
        collar.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 501789)); // You must wait before trying again.

        ShardTestClock.AdvanceSeconds(29);
        Assert.True(lizard.Frozen);

        mark = p.Mark;
        ShardTestClock.AdvanceSeconds(3);
        Assert.False(lizard.Frozen);
        Assert.True(p.Sent(mark, 1112482)); // The chicken frees itself of the collar!!

        collar.OnDoubleClick(p.Pm);
        Assert.NotNull(p.Pm.Target);
        p.Pm.Target = null;

        // Caught again and tamed: freed on the next tick, with no "frees itself".
        for (var i = 0; i < 200 && !lizard.Frozen; i++)
        {
            collar.OnTarget(p.Pm, lizard);
        }

        Assert.True(lizard.Frozen);
        lizard.Controlled = true;
        lizard.ControlMaster = p.Pm;
        mark = p.Mark;
        ShardTestClock.AdvanceSeconds(1.5);
        Assert.False(lizard.Frozen);
        Assert.False(p.Sent(mark, 1112482));

        // A tamed lizard cannot be collared.
        collar.OnTarget(p.Pm, lizard);
        Assert.False(lizard.Frozen);
    }

    [Fact]
    public void CoralTellsTheNewsWhenLockedDown()
    {
        using var p = new Player();
        var owl = p.Place(new CoralTheOwl(), At(1));
        var list = GlobalTownCrierEntryList.Instance;
        var saved = (list.Entries ?? new List<TownCrierEntry>()).ToList();
        foreach (var e in saved)
        {
            list.RemoveEntry(e);
        }

        TownCrierEntry mine = null;

        // The host never runs BaseHouse.Configure, so Item.LockedDownFlag is 0 and IsLockedDown cannot be set; this fact
        // uses BaseHouse.Configure's values (pinned BaseHouse.cs:1304-1305) and puts them back.
        var lockedFlag = Item.LockedDownFlag;
        var secureFlag = Item.SecureFlag;
        Item.LockedDownFlag = 1;
        Item.SecureFlag = 2;

        try
        {
            void Say() => owl.OnSpeech(new SpeechEventArgs(p.Pm, "news", MessageType.Regular, 0x3B2, new[] { 0x30 }));

            var mark = p.Mark;
            Say();
            Assert.False(p.Sent(mark, 1005643)); // not locked down: silent
            Assert.False(p.Sent(mark, 502978));

            owl.IsLockedDown = true;
            mark = p.Mark;
            Say();
            Assert.True(p.Sent(mark, 1005643)); // I have no news at this time.

            mine = list.AddEntry(new[] { "Coral line one", "Coral line two" }, TimeSpan.FromHours(1));
            mark = p.Mark;
            Say();
            Assert.True(p.Sent(mark, 502978)); // Some of the latest news!
            Assert.False(SentText(p.Ns, mark, "Coral line one"));

            ShardTestClock.AdvanceSeconds(1.2);
            Assert.True(SentText(p.Ns, mark, "Coral line one"));
            Assert.False(SentText(p.Ns, mark, "Coral line two"));

            ShardTestClock.AdvanceSeconds(3.0);
            Assert.True(SentText(p.Ns, mark, "Coral line two"));
        }
        finally
        {
            owl.IsLockedDown = false;
            Item.LockedDownFlag = lockedFlag;
            Item.SecureFlag = secureFlag;

            if (mine != null)
            {
                list.RemoveEntry(mine);
            }

            foreach (var e in saved)
            {
                if (!e.Expired)
                {
                    list.AddEntry(e.Lines, e.ExpireTime - Core.Now);
                }
            }
        }
    }

    [Fact]
    public void TheFluteCharmsASnakeToASpotAndTheCharmWearsOff()
    {
        using var p = new Player();
        var flute = p.Pack(new SnakeCharmerFlute { UsesRemaining = 60 });
        var snake = p.Make(new Snake());
        snake.MoveToWorld(At(2), Map.Trammel);
        snake.FightMode = FightMode.None;

        var mark = p.Mark;
        flute.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1112175)); // Target the serpent you wish to entice.
        p.Pm.Target.Invoke(p.Pm, snake);
        Assert.True(p.Sent(mark, 502475)); // Click where you wish the animal to go.
        Assert.NotNull(p.Pm.Target);

        var spot = At(2, 3);
        p.Pm.Target.Invoke(p.Pm, new LandTarget(spot, Map.Trammel));
        Assert.True(p.Sent(mark, 502479)); // The animal walks where it was instructed to.
        Assert.Same(p.Pm, SnakeCharmerFlute.GetCharmMaster(snake));
        Assert.NotNull(snake.TargetLocation);
        Assert.Equal(spot.X, snake.TargetLocation.X);
        Assert.Equal(spot.Y, snake.TargetLocation.Y);
        Assert.Equal(59, flute.UsesRemaining);

        mark = p.Mark;
        flute.OnDoubleClick(p.Pm);
        Assert.True(p.Sent(mark, 1072306)); // You must wait a moment for it to recharge.

        // A second charmer is refused (502802 goes over the snake to them) and gets no ground target.
        var other = new PlayerMobile { Player = true };
        try
        {
            other.MoveToWorld(At(1, 1), Map.Trammel);
            other.Target = new SnakeCharmerFluteProbe(flute).Target;
            other.Target.Invoke(other, snake);
            Assert.Null(other.Target);
            Assert.Same(p.Pm, SnakeCharmerFlute.GetCharmMaster(snake));
        }
        finally
        {
            other.Delete();
        }

        ShardTestClock.AdvanceSeconds(5.5);
        Assert.True(p.Pm.CanBeginAction<SnakeCharmerFlute>());

        mark = p.Mark;
        ShardTestClock.AdvanceSeconds(15.5);
        Assert.Null(SnakeCharmerFlute.GetCharmMaster(snake));
        Assert.True(p.Sent(mark, 1112181)); // The charm seems to wear off.
    }

    // The flute's first target, taken the way a player gets it: double-click with the action free.
    private sealed class SnakeCharmerFluteProbe
    {
        public readonly Target Target;

        public SnakeCharmerFluteProbe(SnakeCharmerFlute flute)
        {
            var probe = new PlayerMobile { Player = true };
            try
            {
                flute.OnDoubleClick(probe);
                Target = probe.Target;
                probe.Target = null;
            }
            finally
            {
                probe.Delete();
            }
        }
    }

    [Fact]
    public void TheFluteRefusesANonSnakeAndBreaksOnItsLastUse()
    {
        using var p = new Player();
        var flute = p.Pack(new SnakeCharmerFlute { UsesRemaining = 1 });
        var skeleton = p.Make(new Skeleton());
        skeleton.MoveToWorld(At(2), Map.Trammel);
        skeleton.FightMode = FightMode.None;
        var snake = p.Make(new GiantSerpent());
        snake.MoveToWorld(At(-2), Map.Trammel);
        snake.FightMode = FightMode.None;

        var mark = p.Mark;
        flute.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, skeleton);
        Assert.True(p.Sent(mark, 1112176)); // That is not a snake or serpent.
        Assert.Null(p.Pm.Target);

        flute.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, snake);
        mark = p.Mark;
        p.Pm.Target.Invoke(p.Pm, new LandTarget(At(-2, 2), Map.Trammel));
        Assert.True(p.Sent(mark, 1112177)); // You broke your snake charmer flute.
        Assert.True(flute.Deleted);

        SnakeCharmerFlute.EndCharm(snake);
        Assert.Null(SnakeCharmerFlute.GetCharmMaster(snake));
    }
}
