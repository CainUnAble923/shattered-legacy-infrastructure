// cc-P610 (P533 U1, P487 #1-#7): a companion is never lost to a reload, a transfer, the loyalty sweep, a disabled
// setting or a restart. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. Cold load: an equipped elemental weapon's damage split is back once the world is up, with the companion and the
//      weapon loaded as the world loads them (shells first, the mobile before its item), not a same-serial copy (#1, #7).
//   2. A companion refuses the transfer order and an already open transfer trade, keeping its owner and its bond; an
//      ordinary pet still transfers (#2, #3).
//   3. The hourly loyalty sweep never drains or releases a controlled companion; an ordinary pet's loyalty still drains;
//      the owner's own release still frees the slots, once (#4).
//   4. A companion away with an owner who logged out while companions were enabled comes back at a login after they
//      were disabled (#5).
//   5. A restart from a save taken while the owner was online: the companion, alive or a ghost, goes away with its
//      offline owner (stabled against the abandonment sweep) and comes back at the next login (#6).

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Companions;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CompanionLifecycleVerification
{
    private static readonly Point3D Spot = new(1510, 1610, 0);

    public CompanionLifecycleVerification() => ClusterFSkillCaps.ReloadForTests();

    private static PlayerMobile NewOwner(int dx = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Felucca);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static BaseCompanion Joined(PlayerMobile owner)
    {
        var c = new BaseCompanion(CompanionPath.Blade, Personality.Noble, false);
        Assert.True(CompanionParty.Join(owner, c, out var reason), reason);
        return c;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AnElementalWeaponKeepsItsSplitThroughAColdLoad()
    {
        ShardTestClock.Arm();
        var owner = NewOwner();
        var c = Joined(owner);
        var sword = new Longsword { Layer = Layer.OneHanded }; // the host has no tile data to read the layer from
        sword.AosElementDamages.Fire = 100;
        c.AddItem(sword);
        Assert.Equal(100, c.FireDamage);

        var cBuf = new byte[65536];
        var cw = new BufferWriter(cBuf, true);
        c.Serialize(cw);
        cw.Flush();
        var wBuf = new byte[65536];
        var ww = new BufferWriter(wBuf, true);
        sword.Serialize(ww);
        ww.Flush();
        var (cSerial, wSerial) = (c.Serial, sword.Serial);
        c.Delete(); // and its sword

        // The world load: every entity is made empty first, then each is read, the mobiles before the items.
        var wShell = new Longsword(wSerial);
        World.AddEntity(wShell);
        var cShell = new BaseCompanion(cSerial);
        World.AddEntity(cShell);
        BaseCompanion loaded = null;

        try
        {
            cShell.Deserialize(new BufferReader(cBuf));
            wShell.Deserialize(new BufferReader(wBuf));
            loaded = cShell;
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100)); // the world is up

            Assert.Same(wShell, loaded.Weapon);
            Assert.Equal(100, loaded.FireDamage);
            Assert.Equal(0, loaded.PhysicalDamage);
        }
        finally
        {
            loaded?.Delete();
            wShell.Delete();
            owner.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ACompanionRefusesTheTransferOrderAndAnOpenTradeAndAPetStillTransfers()
    {
        var a = NewOwner();
        var b = NewOwner(1);
        var c = Joined(a);
        var horse = new Horse();
        horse.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Felucca);
        Assert.True(horse.SetControlMaster(a));

        try
        {
            // The order's gate (PetOrders.IssueTransfer) and the trade's (TransferItem.ValidateControlStatus) both ask this.
            Assert.True(c.CanBeControlledBy(a));
            Assert.False(c.CanBeControlledBy(b), "a companion obeys no one but its owner");

            // A trade already open: accepting it does nothing.
            var trade = new TransferItem(c);
            Assert.False(trade.AllowSecureTrade(a, b, b, true));
            trade.Delete();
            Assert.Same(a, c.ControlMaster);
            Assert.True(c.IsBonded);
            Assert.Single(CompanionParty.CompanionsOf(a));
            Assert.Empty(CompanionParty.CompanionsOf(b));

            // An ordinary pet still goes.
            var petTrade = new TransferItem(horse);
            Assert.True(petTrade.AllowSecureTrade(a, b, b, true));
            petTrade.OnSecureTrade(a, b, b, true);
            Assert.Same(b, horse.ControlMaster);
        }
        finally
        {
            horse.Delete();
            c.Delete();
            a.Delete();
            b.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheLoyaltySweepNeverReleasesACompanionAndStillDrainsAPetAndARequestedReleaseFreesTheSlotsOnce()
    {
        ShardTestClock.Arm();
        var owner = NewOwner();
        var c = Joined(owner);
        var horse = new Horse();
        horse.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Felucca);
        Assert.True(horse.SetControlMaster(owner));
        var sweep = new LoyaltyTimer();
        sweep.Start();

        try
        {
            ShardTestClock.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(6));
            Assert.True(horse.Loyalty < BaseCreature.MaxLoyalty, "an ordinary pet's loyalty still drains");

            ShardTestClock.Advance(TimeSpan.FromHours(11));
            Assert.Equal(BaseCreature.MaxLoyalty, c.Loyalty);
            Assert.Same(owner, c.ControlMaster);
            Assert.True(c.Controlled);

            // The owner's own release frees the companion's slots, once.
            var before = owner.Followers;
            c.IssueOrder(OrderType.Release, owner);
            ShardTestClock.Advance(TimeSpan.FromSeconds(2));
            Assert.Null(c.ControlMaster);
            Assert.Equal(before - c.ControlSlots, owner.Followers);
            CompanionParty.OnOwnerLogout(owner);
            CompanionParty.OnOwnerLogin(owner);
            Assert.Equal(before - c.ControlSlots, owner.Followers);
            Assert.Empty(CompanionParty.CompanionsOf(owner));
        }
        finally
        {
            sweep.Stop();
            horse.Delete();
            c.Delete();
            owner.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ACompanionAwayWhenCompanionsWereDisabledComesBackAtLogin()
    {
        var key = "clusterf.companions.enabled";
        var owner = NewOwner();
        var c = Joined(owner);

        try
        {
            CompanionParty.OnOwnerLogout(owner);
            Assert.True(c.AwayWithOwner);
            Assert.Equal(Map.Internal, c.Map);

            ServerConfiguration.SetSetting(key, false);
            CompanionParty.OnOwnerLogin(owner);
            Assert.False(c.AwayWithOwner, "brought back though companions are now disabled");
            Assert.Equal(owner.Map, c.Map);
            Assert.False(c.IsStabled);
            Assert.Equal(1, owner.Followers);
        }
        finally
        {
            ServerConfiguration.SetSetting(key, true);
            c.Delete();
            owner.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    /// <summary>The world-up step after a load. Reflection, so the fact also runs (and fails) against the code before it.</summary>
    private static void AfterLoad() =>
        typeof(CompanionParty).GetMethod("ReconcileAfterLoad", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARestartFromAnOnlineSaveTakesTheCompanionAwayWithItsOfflineOwner(bool ghost)
    {
        ShardTestClock.Arm();
        var owner = NewOwner();
        var c = Joined(owner);
        var sweep = new LoyaltyTimer();

        try
        {
            if (ghost)
            {
                c.Kill();
                c.Corpse?.Delete();
                Assert.True(c.IsDeadBondedPet);
            }

            // The save was taken with the owner online; at the restart the owner is offline and internal, and no
            // Logout fired. The companion still stands where it was.
            owner.Internalize();
            Assert.NotEqual(Map.Internal, c.Map);

            AfterLoad();
            Assert.True(c.AwayWithOwner, "away with its offline owner");
            Assert.True(c.IsStabled, "stabled while away, so the abandonment sweep leaves a ghost alone");
            Assert.Equal(Map.Internal, c.Map);

            AfterLoad(); // once
            Assert.True(c.AwayWithOwner);

            sweep.Start();
            ShardTestClock.Advance(TimeSpan.FromDays(3));
            Assert.False(c.Deleted, "not swept while its owner is away");

            owner.MoveToWorld(Spot, Map.Felucca);
            CompanionParty.OnOwnerLogin(owner);
            Assert.Equal(Map.Felucca, c.Map);
            Assert.False(c.AwayWithOwner);
            Assert.Equal(ghost, c.IsDeadBondedPet); // no resurrection on the way back
        }
        finally
        {
            sweep.Stop();
            c.Delete();
            owner.Delete();
        }
    }
}
