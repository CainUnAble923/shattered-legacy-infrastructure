// AccessorySlotsVerification.cs
//
// cc-P262: accessory slots (customizations Accessories/; spec D:\UO\cc-prompts\design\accessory-slots-spec.md; notes
// shard-migration notes/cc-P262-accessory-slots.md).
//
// Facts:
//   1. Totals: a jewel in the accessory container counts like a paperdoll one: its attributes in AosAttributes.GetValue,
//      its resist in the wearer's resistance, its skill bonus on the skill; taken out, it counts for nothing.
//   2. Caps still apply: a resist from the container stops at the wearer's resist cap.
//   3. Toe rings: they count barefoot, not under shoes (with the suffocated message and a Smothered count), and again
//      in Plate Socks; skill bonuses come off and go back on with them.
//   4. The barefoot counter: a running step counts with 4 armour pieces, legs and chest among them, and the shoes layer
//      empty; walking, mounted, shod, no legs or chest, or 3 pieces do not. 1,000 steps earn Plate Bottoms Only (toe ring
//      slot 1), 25,000 Iron Soles (slot 2 and the title "the Unshod"), through the real movement event.
//   5. The container: invisible, unliftable, inaccessible, on layer 0x0F; at death a plain jewel leaves with the corpse
//      and a blessed one stays in its slot.
//   6. Slots: 1 ring by default (loops not open), +1 at loops 1, 5 and 10 once loop slots are on; a second bracelet at
//      League rank 9; a full kind refuses another; a toe ring never goes on the paperdoll.
//   7. Ring Bearer (four rings) and Smothered (ten times) are earned.

using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AccessorySlotsVerification : IDisposable
{
    private static readonly Point3D Spot = new(1480, 1750, 0);
    private const string LoopKey = "clusterf.accessories.loopSlotsEnabled";

    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _things = [];
    private readonly List<Account> _accounts = [];
    private readonly bool _loopKept;

    public AccessorySlotsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFAccessories.Register();
        _loopKept = ServerConfiguration.GetSetting(LoopKey, false);
    }

    public void Dispose()
    {
        ClusterFAccessories.LoopOverride = null;
        ServerConfiguration.SetSetting(LoopKey, _loopKept);
        foreach (var e in _things)
        {
            e.Delete();
        }

        foreach (var a in _accounts)
        {
            ClusterFAchievementSystem.ResetForAccount(a.Username);
        }
    }

    private T Track<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private PlayerMobile Player()
    {
        var account = new Account($"p262{Guid.NewGuid():N}"[..16], "p262-test-only");
        _accounts.Add(account);
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P262 tester" });
        pm.RawStr = 100;
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static T InPack<T>(PlayerMobile pm, T item) where T : Item
    {
        pm.Backpack.DropItem(item);
        return item;
    }

    private static void ExtraRings(int loop)
    {
        ServerConfiguration.SetSetting(LoopKey, true);
        ClusterFAccessories.LoopOverride = _ => loop;
    }

    private static void UnlockToeRings(PlayerMobile pm, int slots)
    {
        Assert.True(ClusterFAchievementSystem.TryGrant(pm.Account, ClusterFAchievementSystem.PlateBottomsOnlyKey));
        if (slots > 1)
        {
            Assert.True(ClusterFAchievementSystem.TryGrant(pm.Account, ClusterFAchievementSystem.IronSolesKey));
        }

        Assert.Equal(slots, ClusterFAccessories.ToeRingSlots(pm));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AJewelInTheContainerCountsLikeAPaperdollOne()
    {
        var pm = Player();
        ExtraRings(loop: 1);

        var first = InPack(pm, new GoldRing());
        first.Attributes.DefendChance = 5;
        var second = InPack(pm, new GoldRing());
        second.Attributes.DefendChance = 7;
        second.Resistances.Physical = 6;
        second.SkillBonuses.SetValues(0, SkillName.Magery, 10.0);

        pm.Skills.Magery.Base = 50.0;
        pm.UpdateResistances();
        var resist = pm.PhysicalResistance;

        Assert.Null(ClusterFAccessories.Equip(pm, first));
        Assert.Equal(pm, first.Parent); // the paperdoll ring first
        Assert.Null(ClusterFAccessories.Equip(pm, second));
        Assert.IsType<AccessoryContainer>(second.Parent);

        Assert.Equal(12, AosAttributes.GetValue(pm, AosAttribute.DefendChance));
        Assert.Equal(resist + 6, pm.PhysicalResistance);
        Assert.Equal(60.0, pm.Skills.Magery.Value, 1);
        Assert.True(ClusterFAccessories.IsApplied(second));

        Assert.Null(ClusterFAccessories.Unequip(pm, second));
        Assert.True(second.IsChildOf(pm.Backpack));
        Assert.Equal(5, AosAttributes.GetValue(pm, AosAttribute.DefendChance));
        Assert.Equal(resist, pm.PhysicalResistance);
        Assert.Equal(50.0, pm.Skills.Magery.Value, 1);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheWearersCapsStillApply()
    {
        var pm = Player();
        ExtraRings(loop: 1);

        var ring = InPack(pm, new GoldRing());
        ring.Resistances.Fire = 200;
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));
        Assert.Null(ClusterFAccessories.Equip(pm, ring));

        var max = pm.GetMaxResistance(ResistanceType.Fire);
        _out.WriteLine($"fire {pm.FireResistance}, cap {max}");
        Assert.Equal(max, pm.FireResistance);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ToeRingsCountBarefootAndInPlateSocksAndNotUnderShoes()
    {
        var pm = Player();
        UnlockToeRings(pm, 1);

        var toe = InPack(pm, new ToeRing());
        toe.Attributes.LowerManaCost = 4;
        toe.SkillBonuses.SetValues(0, SkillName.Meditation, 5.0);
        pm.Skills.Meditation.Base = 40.0;

        Assert.Null(ClusterFAccessories.Equip(pm, toe));
        Assert.Equal(4, AosAttributes.GetValue(pm, AosAttribute.LowerManaCost));
        Assert.Equal(45.0, pm.Skills.Meditation.Value, 1);

        var before = ClusterFAchievementSystem.GetCounter(pm.Account.Username, ClusterFAchievementSystem.SmotheredCounter);
        var shoes = Track(new Shoes());
        pm.AddItem(shoes);
        Assert.False(ClusterFAccessories.IsActive(pm, toe));
        Assert.Equal(0, AosAttributes.GetValue(pm, AosAttribute.LowerManaCost));
        Assert.Equal(40.0, pm.Skills.Meditation.Value, 1);
        Assert.Equal(before + 1, ClusterFAchievementSystem.GetCounter(pm.Account.Username, ClusterFAchievementSystem.SmotheredCounter));

        shoes.Delete();
        Assert.True(ClusterFAccessories.IsActive(pm, toe));
        Assert.Equal(4, AosAttributes.GetValue(pm, AosAttribute.LowerManaCost));

        var socks = Track(new PlateSocks());
        pm.AddItem(socks);
        Assert.Equal(socks, pm.FindItemOnLayer(Layer.Shoes));
        Assert.True(ClusterFAccessories.IsActive(pm, toe));
        Assert.Equal(4, AosAttributes.GetValue(pm, AosAttribute.LowerManaCost));
        Assert.Equal(45.0, pm.Skills.Meditation.Value, 1);
        Assert.Equal(before + 1, ClusterFAchievementSystem.GetCounter(pm.Account.Username, ClusterFAchievementSystem.SmotheredCounter));
    }

    // ---------------------------------------------------------------- 4

    // The test host has no tile data to give armour its layer (D27), so each piece's layer is set.
    private static void Armour(PlayerMobile pm, bool legs = true, bool chest = true, bool helm = true, bool gloves = true)
    {
        if (legs) pm.AddItem(new PlateLegs { Layer = Layer.Pants });
        if (chest) pm.AddItem(new PlateChest { Layer = Layer.InnerTorso });
        if (helm) pm.AddItem(new PlateHelm { Layer = Layer.Helm });
        if (gloves) pm.AddItem(new PlateGloves { Layer = Layer.Gloves });
    }

    [Fact]
    public void OnlyARunningBarefootStepInFourArmourPiecesWithLegsAndChestCounts()
    {
        var run = Direction.North | Direction.Running;

        var pm = Player();
        Armour(pm);
        Assert.True(ClusterFAccessories.CountsAsBarefootStep(pm, run));
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(pm, Direction.North)); // walking

        var shoes = Track(new Shoes());
        pm.AddItem(shoes);
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(pm, run));
        shoes.Delete();

        var horse = Track(new Horse());
        horse.MoveToWorld(Spot, Map.Trammel);
        horse.Rider = pm;
        Assert.True(pm.Mounted);
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(pm, run));
        horse.Rider = null;
        Assert.True(ClusterFAccessories.CountsAsBarefootStep(pm, run));

        var noLegs = Player();
        Armour(noLegs, legs: false);
        noLegs.AddItem(new PlateArms { Layer = Layer.Arms });
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(noLegs, run));

        var noChest = Player();
        Armour(noChest, chest: false);
        noChest.AddItem(new PlateArms { Layer = Layer.Arms });
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(noChest, run));

        var three = Player();
        Armour(three, gloves: false);
        Assert.False(ClusterFAccessories.CountsAsBarefootStep(three, run));

        // The tiles, through the real movement event.
        var user = pm.Account.Username;
        for (var i = 0; i < 999; i++)
        {
            EventSink.InvokeMovement(MovementEventArgs.Create(pm, run));
        }

        EventSink.InvokeMovement(MovementEventArgs.Create(pm, Direction.North)); // a walk: not counted
        Assert.Equal(999, ClusterFAchievementSystem.GetCounter(user, ClusterFAchievementSystem.BarefootTilesCounter));
        Assert.Equal(0, ClusterFAccessories.ToeRingSlots(pm));

        EventSink.InvokeMovement(MovementEventArgs.Create(pm, run));
        Assert.True(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.PlateBottomsOnlyKey));
        Assert.Equal(1, ClusterFAccessories.ToeRingSlots(pm));

        for (var i = 1000; i < ClusterFAchievementSystem.IronSolesTiles; i++)
        {
            ClusterFAchievementSystem.NotifyBarefootStep(pm);
        }

        Assert.True(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.IronSolesKey));
        Assert.Equal(2, ClusterFAccessories.ToeRingSlots(pm));
        Assert.Contains(LeagueTitles.Unshod, ClusterFAccountPersistence.GetOrCreate(pm.Account).LeagueTitles.Owned);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheContainerCannotBeTouchedAndFollowsTheDeathRules()
    {
        var pm = Player();
        ExtraRings(loop: 5);

        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));
        var plain = InPack(pm, new SilverRing());
        var blessed = InPack(pm, new GoldRing { LootType = LootType.Blessed });
        Assert.Null(ClusterFAccessories.Equip(pm, plain));
        Assert.Null(ClusterFAccessories.Equip(pm, blessed));

        var c = ClusterFAccessories.ContainerOf(pm)!;
        Assert.Equal((Layer)0x0F, c.Layer);
        Assert.False(c.Visible);
        Assert.False(c.Movable);
        Assert.False(c.OnDragLift(pm));
        Assert.False(c.IsAccessibleTo(pm));
        var reject = LRReason.Inspecific;
        Assert.False(c.CheckLift(pm, plain, ref reject));
        Assert.False(c.CheckHold(pm, new GoldRing(), false, true, 0, 0));

        pm.Kill();
        _out.WriteLine($"after death: plain in {plain.Parent?.GetType().Name}, blessed in {blessed.Parent?.GetType().Name}");
        Assert.Equal(c, blessed.Parent);
        Assert.NotEqual(pm, plain.RootParent);
        Assert.Equal(c, ClusterFAccessories.ContainerOf(pm));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void SlotsFollowTheirThresholdsAndAFullKindRefusesMore()
    {
        var pm = Player();
        Assert.Equal(1, ClusterFAccessories.RingSlots(pm)); // loops not open: the paperdoll ring only
        Assert.Equal(1, ClusterFAccessories.BraceletSlots(pm));
        Assert.Equal(0, ClusterFAccessories.ToeRingSlots(pm));

        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));
        Assert.Contains("full", ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));

        ClusterFAccessories.LoopOverride = _ => 10;
        Assert.Equal(1, ClusterFAccessories.RingSlots(pm)); // the setting still off
        ServerConfiguration.SetSetting(LoopKey, true);
        foreach (var (loop, slots) in new[] { (0, 1), (1, 2), (4, 2), (5, 3), (9, 3), (10, 4), (50, 4) })
        {
            ClusterFAccessories.LoopOverride = _ => loop;
            Assert.Equal(slots, ClusterFAccessories.RingSlots(pm));
        }

        pm.LeagueRank = 8;
        Assert.Equal(1, ClusterFAccessories.BraceletSlots(pm));
        pm.LeagueRank = 9;
        Assert.Equal(2, ClusterFAccessories.BraceletSlots(pm));

        var toe = InPack(pm, new ToeRing());
        Assert.Contains("no toe ring slots", ClusterFAccessories.Equip(pm, toe));
        Assert.False(pm.EquipItem(toe)); // never the paperdoll ring layer
        Assert.True(toe.IsChildOf(pm.Backpack));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void RingBearerAndSmotheredAreEarned()
    {
        var pm = Player();
        ExtraRings(loop: 10);
        for (var i = 0; i < 4; i++)
        {
            Assert.False(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.RingBearerKey));
            Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));
        }

        Assert.Equal(4, ClusterFAccessories.RingsWorn(pm));
        Assert.True(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.RingBearerKey));

        UnlockToeRings(pm, 1);
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new ToeRing())));
        for (var i = 0; i < 10; i++)
        {
            Assert.False(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.SmotheredKey));
            var shoes = Track(new Sandals());
            pm.AddItem(shoes);
            shoes.Delete();
        }

        Assert.True(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAchievementSystem.SmotheredKey));
    }
}
