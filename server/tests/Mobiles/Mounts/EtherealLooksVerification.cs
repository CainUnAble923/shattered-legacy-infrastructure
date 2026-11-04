// EtherealLooksVerification.cs
//
// cc-P794: the earned ethereal mounts (server/customizations/Mobiles/Mounts/EtherealLooks.cs). Notes in shard-migration
// notes/cc-P794-ethereal-looks.md.
//
// Facts:
//   1. Each has its statuette and mount item, its own name, is blessed, solid, and no veteran or donation item.
//   2. Each mounts by its real double-click and cast, wears its mount item solid, takes its follower slot, and dismounts
//      back into the pack as a statuette, the slot given back.
//   3. Each saves and loads in the pack and ridden.
//   4. A player with no slot free is refused; a single click does not say "Veteran Reward".
//   5. The two settings: the hue goes on while ridden and comes off after, and the slots follow.
//   6. A companion can ride one: it wears it, keeps its slots with its owner apart, saves and loads ridden, and dismounts
//      into its own pack. A companion that dies does not lose it.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Companions;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class EtherealLooksVerification
{
    // Its own spot: shared spots can hold another fact's leftovers (cc-P793).
    private static readonly Point3D Here = new(1794, 1183, 0);

    private readonly ITestOutputHelper _out;

    public EtherealLooksVerification(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Looks => EtherealLooks.All.Select(l => new object[] { l.Type, l.Name, l.Statuette, l.MountItem });

    private static EtherealMount New(Type type) => (EtherealMount)Activator.CreateInstance(type)!;

    private static PlayerMobile Player(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name, Body = 400, FollowersMax = 5, Hits = 50 };
        pm.MoveToWorld(Here, Map.TerMur);
        pm.AddItem(new Backpack());
        return pm;
    }

    private static EtherealMount InPack(PlayerMobile pm, Type type)
    {
        var mount = New(type);
        pm.Backpack!.DropItem(mount);
        return mount;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EachHasItsArtAndNameAndIsSolidAndBlessed(Type type, string name, int statuette, int mountItem)
    {
        var mount = New(type);

        Assert.IsAssignableFrom<EtherealMount>(mount);
        Assert.Equal(statuette, mount.ItemID);
        Assert.Equal(statuette, mount.RegularID);
        Assert.Equal(mountItem, mount.MountedID);
        Assert.Equal(name, mount.DefaultName);
        Assert.Equal(0, mount.EtherealHue);
        Assert.Equal(0, mount.Hue);
        Assert.Equal(LootType.Blessed, mount.LootType);
        Assert.False(mount.IsRewardItem);
        Assert.False(mount.IsDonationItem);
        Assert.Equal(1, mount.FollowerSlots);

        mount.Delete();
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EachMountsByItsRealCastAndDismountsBackToThePack(Type type, string name, int statuette, int mountItem)
    {
        ShardTestClock.Arm();

        var pm = Player("Rider");
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        var mount = InPack(pm, type);

        try
        {
            mount.OnDoubleClick(pm);
            ShardTestClock.AdvanceSeconds(5);

            _out.WriteLine($"{name}: rider={mount.Rider} item=0x{mount.ItemID:X} layer={mount.Layer} hue={mount.Hue} followers={pm.Followers}");
            Assert.Same(pm, mount.Rider);
            Assert.Same(mount, pm.Mount);
            Assert.True(pm.Mounted);
            Assert.Same(mount, pm.FindItemOnLayer(Layer.Mount));
            Assert.Equal(mountItem, mount.ItemID);
            Assert.Equal(0, mount.Hue);
            Assert.Equal(1, pm.Followers);

            mount.Rider = null;

            Assert.Null(pm.Mount);
            Assert.False(pm.Mounted);
            Assert.True(mount.IsChildOf(pm.Backpack));
            Assert.Equal(statuette, mount.ItemID);
            Assert.Equal(0, pm.Followers);
        }
        finally
        {
            pm.NetState = null;
            mount.Delete();
            pm.Delete();
        }
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EachSavesAndLoadsInThePackAndRidden(Type type, string name, int statuette, int mountItem)
    {
        var pm = Player("Saver");
        var mount = InPack(pm, type);

        var packed = RoundTrip(mount);
        Assert.IsType(type, packed);
        Assert.Equal(statuette, packed.ItemID);
        Assert.Equal(statuette, packed.RegularID);
        Assert.Equal(mountItem, packed.MountedID);
        Assert.Null(packed.Rider);
        Assert.Equal(name, packed.DefaultName);

        mount.Rider = pm;

        var ridden = RoundTrip(mount);
        Assert.IsType(type, ridden);
        Assert.Same(pm, ridden.Rider);
        Assert.Equal(mountItem, ridden.ItemID);

        // packed and ridden share mount's serial; only the real one is deleted.
        mount.Rider = null;
        mount.Delete();
        pm.Delete();
    }

    [Fact]
    public void WithNoSlotFreeARiderIsRefusedAndASingleClickIsNotAVeteranReward()
    {
        var pm = Player("Full");
        var mount = InPack(pm, typeof(EtherealPeacock));

        pm.Followers = pm.FollowersMax;
        Assert.False(mount.Validate(pm));

        pm.Followers = pm.FollowersMax - 1;
        Assert.True(mount.Validate(pm));

        var method = mount.GetType().GetMethod(nameof(Item.OnSingleClick))!;
        Assert.NotEqual(typeof(EtherealMount), method.DeclaringType);

        mount.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheSettingsReachTheRide()
    {
        var pm = Player("Tuner");
        var mount = InPack(pm, typeof(EtherealBoura));

        try
        {
            EtherealLooks.Hue = 0x4001;
            EtherealLooks.FollowerSlots = 2;

            mount.Rider = pm;
            Assert.Equal(0x4001, mount.Hue);
            Assert.Equal(2, pm.Followers);

            mount.Rider = null;
            Assert.Equal(0, mount.Hue);
            Assert.Equal(0, pm.Followers);
        }
        finally
        {
            EtherealLooks.Hue = EtherealLooks.SolidHue;
            EtherealLooks.FollowerSlots = EtherealLooks.DefaultFollowerSlots;
            mount.Delete();
            pm.Delete();
        }
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void ACompanionCanRideEach(Type type, string name, int statuette, int mountItem)
    {
        var owner = Player("Owner");
        var squire = new BaseCompanion();
        squire.MoveToWorld(Here, Map.TerMur);
        squire.SetControlMaster(owner);
        var followers = owner.Followers;

        var mount = New(type);
        squire.Backpack!.DropItem(mount);

        mount.Rider = squire;

        _out.WriteLine($"{name}: rider={mount.Rider} item=0x{mount.ItemID:X} parent={mount.Parent} squire followers={squire.Followers} owner={owner.Followers}");
        Assert.Same(squire, mount.Rider);
        Assert.Same(mount, squire.Mount);
        Assert.True(squire.Mounted);
        Assert.Same(mount, squire.FindItemOnLayer(Layer.Mount));
        Assert.Equal(mountItem, mount.ItemID);
        Assert.Equal(followers, owner.Followers); // the owner's party budget does not pay for it

        var ridden = RoundTrip(mount);
        Assert.Same(squire, ridden.Rider);

        mount.Rider = null;

        Assert.Null(squire.Mount);
        Assert.True(mount.IsChildOf(squire.Backpack));
        Assert.Equal(statuette, mount.ItemID);

        mount.Delete();
        squire.Delete();
        owner.Delete();
    }

    [Fact]
    public void ACompanionThatDiesDoesNotLoseItsEthereal()
    {
        var owner = Player("Mourner");
        var squire = new BaseCompanion();
        squire.MoveToWorld(Here, Map.TerMur);
        squire.SetControlMaster(owner);
        squire.IsBonded = true; // as CompanionParty makes every companion: it dies to a ghost, not deleted

        var mount = New(typeof(EtherealPeacock));
        squire.Backpack!.DropItem(mount);
        mount.Rider = squire;

        squire.Kill();

        _out.WriteLine($"after death: deleted={mount.Deleted} rider={mount.Rider} parent={mount.Parent} item=0x{mount.ItemID:X}");
        Assert.True(squire.IsDeadBondedPet);
        Assert.False(mount.Deleted);
        Assert.Null(mount.Rider);
        Assert.Equal(0xA0C0, mount.ItemID);

        (squire.Corpse as Item)?.Delete();
        mount.Delete();
        squire.Delete();
        owner.Delete();
    }
}
