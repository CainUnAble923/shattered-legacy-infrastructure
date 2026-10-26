// LeagueEchoesVerification.cs
//
// cc-P630 (P567 design, stage 1): calling, tracking and ending League Echoes, and the two saved fields. Notes in
// shard-migration notes/cc-P630-build-echo-summon.md. The creature's own profile and harm rules are in
// Mobiles/LeagueDragonEchoVerification.cs.
//
// Facts:
//   1. Settings: off, Mythril, 20 s, 30 min by default; an unknown rank, a duration outside 1-60 and a cooldown outside
//      300-86400 are refused.
//   2. Rank: below Mythril refused, Mythril and Celestial called; a low-rank character on a Mythril's account refused.
//   3. A call charges two follower slots once, follows the caller, lasts 20 s, starts the account's 30-minute cooldown
//      and reveals the caller; the account cannot call a second (same or other character); another account can.
//   4. The cooldown: refused one tick before the deadline, ready at it.
//   5. Too few follower slots, the switch off, a blocked neighbourhood: refused, no creature, no slots, no cooldown.
//   6. Places: Felucca, a guarded region, the jail, a safe zone with its guards off, the tutorial yard and the Apprentice
//      grounds are refused; a dungeon is not.
//   7. Ending: Dismiss (only by its caller), the owner's death and logout, distance, another map, a lost rank and the
//      switch turned off each dissolve it and give the slots back; the cooldown is never refunded; a stale callback
//      cannot remove a newer Echo.
//   8. Expiry: the Echo is gone 20 seconds after the call and the slots are back.
//   9. The save: the account's next use (v23) and the character's Tome serial (League data v3) round-trip; a v22 account
//      reads as ready and a v2 character as no Tome; a newer version is still refused.

using System;
using Server;
using Server.Accounting;
using Server.Engines.ConPVP;
using Server.Engines.LeagueEchoes;
using Server.Engines.LeagueRegister;
using Server.Mobiles;
using Server.Regions;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueEchoesVerification
{
    public LeagueEchoesVerification()
    {
        ShardTestHost.EnsureAccounts();
        LeagueEchoSystem.Configure(); // the test host does not run Configure
    }

    internal static readonly Point3D Wild = new(1700, 700, 0);

    // ---------------------------------------------------------------- helpers

    internal static Account NewAccount() => new($"p630{Guid.NewGuid():N}"[..16], "p630-test-only");

    internal static PlayerMobile NewCharacter(Account account, string rank = "Mythril", int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Server.Items.Backpack());
        account[slot] = pm;
        pm.MoveToWorld(Wild, Map.Trammel);
        if (rank != null)
        {
            ClusterFLeagueRanks.SetRank(pm, LeagueRegisterSettings.Rank(rank));
        }

        return pm;
    }

    internal static void Cleanup(params Account[] accounts)
    {
        LeagueEchoSettings.EnabledOverride = null;
        foreach (var account in accounts)
        {
            LeagueEchoSystem.ActiveFor(account)?.Delete();
            for (var i = 0; i < account.Length; i++)
            {
                account[i]?.Delete();
            }

            Accounts.Remove(account);
        }
    }

    private static DateTime NextUse(Account a) => LeagueEchoSystem.NextUse(a);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheSettingsDefaultOffAndRefuseBadValues()
    {
        Assert.False(LeagueEchoSettings.Enabled);
        Assert.Equal("Mythril", LeagueEchoSettings.RequiredRankKey);
        Assert.Equal(20, LeagueEchoSettings.DurationSeconds);
        Assert.Equal(1800, LeagueEchoSettings.CooldownSeconds);
        Assert.Equal(2, LeagueEchoSettings.ControlSlots);
        Assert.Null(LeagueEchoSettings.Problem());

        Assert.NotNull(LeagueEchoSettings.Problem("NotAMetal", 20, 1800));
        Assert.NotNull(LeagueEchoSettings.Problem("Mythril", 0, 1800));
        Assert.NotNull(LeagueEchoSettings.Problem("Mythril", 61, 1800));
        Assert.NotNull(LeagueEchoSettings.Problem("Mythril", 20, 299));
        Assert.NotNull(LeagueEchoSettings.Problem("Mythril", 20, 86401));
        Assert.Null(LeagueEchoSettings.Problem("Celestial", 60, 86400));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void OnlyAMythrilCharacterOrHigherMayCall()
    {
        var a = NewAccount();
        var b = NewAccount();
        var mythril = NewCharacter(a);
        var lowAlt = NewCharacter(a, "Blaze", 1);
        var celestial = NewCharacter(b, "Celestial");
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            Assert.Equal(EchoCall.BelowRank, LeagueEchoSystem.TryCall(lowAlt));
            Assert.Contains("Mythril", LeagueEchoSystem.Message(EchoCall.BelowRank, lowAlt));
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(mythril));
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(celestial));
        }
        finally
        {
            Cleanup(a, b);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ACallChargesTwoSlotsOnceAndStartsTheAccountCooldown()
    {
        var a = NewAccount();
        var b = NewAccount();
        var pm = NewCharacter(a);
        var alt = NewCharacter(a, "Mythril", 1);
        var other = NewCharacter(b);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            pm.Hidden = true;
            var max = pm.FollowersMax;

            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));
            Assert.NotNull(echo);
            Assert.Equal(2, pm.Followers);
            Assert.Equal(max, pm.FollowersMax);
            Assert.True(echo!.Summoned);
            Assert.Equal(pm, echo.ControlMaster);
            Assert.Equal(OrderType.Follow, echo.ControlOrder);
            Assert.Equal(pm, echo.ControlTarget);
            Assert.NotEqual(pm.Location, echo.Location);
            Assert.True(pm.InRange(echo.Location, 1));
            Assert.Equal(Core.Now + TimeSpan.FromSeconds(1800), NextUse(a));
            Assert.False(pm.Hidden);
            Assert.Same(echo, LeagueEchoSystem.ActiveFor(a));
            Assert.Contains("20 seconds", LeagueEchoSystem.Message(EchoCall.Called, pm));

            Assert.Equal(EchoCall.Active, LeagueEchoSystem.TryCall(pm));
            Assert.Equal(EchoCall.Active, LeagueEchoSystem.TryCall(alt));
            Assert.Equal(2, pm.Followers);
            Assert.Equal(0, alt.Followers);

            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(other));
            Assert.NotNull(LeagueEchoSystem.ActiveFor(b));
            Assert.Same(echo, LeagueEchoSystem.ActiveFor(a));
        }
        finally
        {
            Cleanup(a, b);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheCooldownEndsExactlyAtItsDeadline()
    {
        var a = NewAccount();
        var pm = NewCharacter(a);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            var data = ClusterFAccountPersistence.GetOrCreate(a);
            data.LeagueEchoNextUseUtc = Core.Now + TimeSpan.FromTicks(1);
            Assert.Equal(EchoCall.Cooldown, LeagueEchoSystem.TryCall(pm));
            Assert.Null(LeagueEchoSystem.ActiveFor(a));

            data.LeagueEchoNextUseUtc = Core.Now + TimeSpan.FromSeconds(728);
            Assert.Contains("12m 08s", LeagueEchoSystem.Message(EchoCall.Cooldown, pm));

            data.LeagueEchoNextUseUtc = Core.Now;
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm));
        }
        finally
        {
            Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ARefusedCallLeavesNoCreatureNoSlotsAndNoCooldown()
    {
        var a = NewAccount();
        var pm = NewCharacter(a);
        try
        {
            Assert.Equal(EchoCall.Disabled, LeagueEchoSystem.TryCall(pm));
            LeagueEchoSettings.EnabledOverride = true;

            pm.Followers = pm.FollowersMax - 1;
            Assert.Equal(EchoCall.Slots, LeagueEchoSystem.TryCall(pm));
            Assert.Equal(pm.FollowersMax - 1, pm.Followers);
            pm.Followers = 0;

            // Every neighbour inside the jail, the caller's own tile outside it.
            var c = pm.Location;
            var jail = new JailRegion("P630 ring", Map.Trammel, 60,
                new Rectangle3D(new Point3D(c.X - 1, c.Y - 1, -128), new Point3D(c.X + 2, c.Y, 127)),
                new Rectangle3D(new Point3D(c.X - 1, c.Y + 1, -128), new Point3D(c.X + 2, c.Y + 2, 127)),
                new Rectangle3D(new Point3D(c.X - 1, c.Y, -128), new Point3D(c.X, c.Y + 1, 127)),
                new Rectangle3D(new Point3D(c.X + 1, c.Y, -128), new Point3D(c.X + 2, c.Y + 1, 127)));
            jail.Register();
            try
            {
                Assert.False(LeagueEchoPolicy.IsForbiddenRegion(Region.Find(c, Map.Trammel)));
                Assert.Equal(EchoCall.NoRoom, LeagueEchoSystem.TryCall(pm));
            }
            finally
            {
                jail.Unregister();
            }

            Assert.Equal(0, pm.Followers);
            Assert.Null(LeagueEchoSystem.ActiveFor(a));
            Assert.Equal(DateTime.MinValue, NextUse(a));
            var near = 0;
            foreach (var _ in pm.GetMobilesInRange<LeagueDragonEcho>(3))
            {
                near++;
            }

            Assert.Equal(0, near);
        }
        finally
        {
            Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TownsJailSafeZonesTutorialAndFeluccaAreRefusedAndADungeonIsNot()
    {
        var a = NewAccount();
        var pm = NewCharacter(a);
        LeagueEchoSettings.EnabledOverride = true;
        var area = new Rectangle3D(new Point3D(Wild.X - 10, Wild.Y - 10, -128), new Point3D(Wild.X + 10, Wild.Y + 10, 127));
        try
        {
            pm.MoveToWorld(Wild, Map.Felucca);
            Assert.Equal(EchoCall.Facet, LeagueEchoSystem.TryCall(pm));
            pm.MoveToWorld(Wild, Map.Trammel);

            var town = new GuardedRegion("P630 town", Map.Trammel, 60, area);
            town.Register();
            try
            {
                Assert.Equal(EchoCall.Region, LeagueEchoSystem.TryCall(pm));
            }
            finally
            {
                town.Unregister();
            }

            var safe = new SafeZone(new Rectangle2D(Wild.X - 10, Wild.Y - 10, 20, 20), Wild, Map.Trammel, false);
            try
            {
                Assert.True(safe.IsDisabled());
                Assert.True(LeagueEchoPolicy.IsForbiddenRegion(safe));
                Assert.Equal(EchoCall.Region, LeagueEchoSystem.TryCall(pm));
            }
            finally
            {
                safe.Unregister(); // its constructor registers it
            }

            Assert.True(LeagueEchoPolicy.IsForbiddenRegion(new JailRegion("P630 jail", Map.Trammel, 60, area)));
            Assert.True(LeagueEchoPolicy.IsForbiddenRegion(new TutorialYardRegion(null)));
            Assert.True(LeagueEchoPolicy.IsForbiddenRegion(new ApprenticeTrainingRegion("P630 grounds", Map.Trammel, null, area)));
            var openTown = new GuardedRegion("P630 lawless", Map.Trammel, 60, area) { GuardsDisabled = true };
            Assert.False(LeagueEchoPolicy.IsForbiddenRegion(openTown));

            var dungeon = new DungeonRegion("P630 dungeon", Map.Trammel, 60, area);
            dungeon.Register();
            try
            {
                Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm));
            }
            finally
            {
                dungeon.Unregister();
            }

            Assert.False(LeagueEchoPolicy.AllowedFacet(Map.Felucca));
            Assert.False(LeagueEchoPolicy.AllowedFacet(Map.Internal));
            Assert.True(LeagueEchoPolicy.AllowedFacet(Map.Malas));
        }
        finally
        {
            Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void EveryEndingDissolvesItGivesTheSlotsBackAndKeepsTheCooldown()
    {
        var a = NewAccount();
        var pm = NewCharacter(a);
        var alt = NewCharacter(a, "Mythril", 1);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            void Ends(string why, Action end, Action? undo = null)
            {
                var data = ClusterFAccountPersistence.GetOrCreate(a);
                data.LeagueEchoNextUseUtc = DateTime.MinValue;
                Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));
                var deadline = NextUse(a);

                end();

                Assert.True(echo!.Deleted, why);
                Assert.Equal(0, pm.Followers);
                Assert.Null(LeagueEchoSystem.ActiveFor(a));
                Assert.Equal(deadline, NextUse(a));
                undo?.Invoke();
            }

            Ends("dismiss", () =>
            {
                Assert.False(LeagueEchoSystem.Dismiss(alt));
                Assert.True(LeagueEchoSystem.Dismiss(pm));
            });
            Ends("death event", () => PlayerMobile.PlayerDeathEvent(pm));
            Ends("logout event", () => EventSink.InvokeLogout(pm));
            Ends("distance", () =>
            {
                pm.MoveToWorld(new Point3D(Wild.X + 13, Wild.Y, Wild.Z), Map.Trammel);
                LeagueEchoSystem.Watch();
            }, () => pm.MoveToWorld(Wild, Map.Trammel));
            Ends("another map", () =>
            {
                pm.MoveToWorld(Wild, Map.Malas);
                LeagueEchoSystem.Watch();
            }, () => pm.MoveToWorld(Wild, Map.Trammel));
            Ends("rank lost", () =>
            {
                ClusterFLeagueRanks.SetRank(pm, LeagueRegisterSettings.Rank("Blaze"));
                LeagueEchoSystem.Watch();
            }, () => ClusterFLeagueRanks.SetRank(pm, LeagueRegisterSettings.Rank("Mythril")));
            Ends("switched off", () =>
            {
                LeagueEchoSettings.EnabledOverride = false;
                LeagueEchoSystem.Watch();
            }, () => LeagueEchoSettings.EnabledOverride = true);

            // Within range and allowed, the watchdog leaves it alone.
            ClusterFAccountPersistence.GetOrCreate(a).LeagueEchoNextUseUtc = DateTime.MinValue;
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var stays));
            LeagueEchoSystem.Watch();
            Assert.False(stays!.Deleted);

            // A stale Echo of the same account deleted now cannot remove the one held.
            var stale = new LeagueDragonEcho { OwnerAccount = a };
            stale.Delete();
            Assert.Same(stays, LeagueEchoSystem.ActiveFor(a));
            Assert.Equal(2, pm.Followers);
        }
        finally
        {
            Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheEchoIsGoneTwentySecondsAfterTheCall()
    {
        var a = NewAccount();
        var pm = NewCharacter(a);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            ShardTestClock.Arm();
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));

            ShardTestClock.AdvanceSeconds(19);
            Assert.False(echo!.Deleted);

            ShardTestClock.AdvanceSeconds(1.5);
            Assert.True(echo.Deleted);
            Assert.Equal(0, pm.Followers);
            Assert.Null(LeagueEchoSystem.ActiveFor(a));
            Assert.Equal(EchoCall.Cooldown, LeagueEchoSystem.TryCall(pm));
        }
        finally
        {
            Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheTwoNewFieldsSaveAndOlderSavesReadAsReady()
    {
        var when = new DateTime(2026, 10, 26, 3, 4, 5, DateTimeKind.Utc);
        var data = new ClusterFAccountData { Renown = 7, LeagueEchoNextUseUtc = when };

        var w = new BufferWriter(true);
        data.Serialize(w);
        var length = (int)w.Position;
        var bytes = w.Buffer[..length];

        Assert.Equal(23, BitConverter.ToInt32(bytes, 0));
        var r = new BufferReader(bytes);
        Assert.Equal(when, new ClusterFAccountData(r).LeagueEchoNextUseUtc);
        Assert.Equal(length, (int)r.Position);

        // The same record as v22 wrote it: version 22 and no trailing field.
        var v22 = bytes[..(length - 8)];
        BitConverter.GetBytes(22).CopyTo(v22, 0);
        var r22 = new BufferReader(v22);
        var old = new ClusterFAccountData(r22);
        Assert.Equal(DateTime.MinValue, old.LeagueEchoNextUseUtc);
        Assert.Equal(7, old.Renown);
        Assert.Equal(v22.Length, (int)r22.Position);

        var league = new CharacterLeagueData { Rank = 15, RegisterSerial = 0x4000_0001, EchoTomeSerial = 0x4000_0123 };
        var lw = new BufferWriter(true);
        league.Serialize(lw);
        var lLength = (int)lw.Position;
        var lBytes = lw.Buffer[..lLength];
        Assert.Equal(3, BitConverter.ToInt32(lBytes, 0));
        var lr = new BufferReader(lBytes);
        var copy = new CharacterLeagueData(lr);
        Assert.Equal(0x4000_0123u, copy.EchoTomeSerial);
        Assert.Equal(0x4000_0001u, copy.RegisterSerial);
        Assert.Equal(lLength, (int)lr.Position);

        var v2 = lBytes[..(lLength - 4)];
        BitConverter.GetBytes(2).CopyTo(v2, 0);
        var lr2 = new BufferReader(v2);
        var oldLeague = new CharacterLeagueData(lr2);
        Assert.Equal(0u, oldLeague.EchoTomeSerial);
        Assert.Equal(15, oldLeague.Rank);
        Assert.Equal(v2.Length, (int)lr2.Position);

        BitConverter.GetBytes(4).CopyTo(lBytes, 0);
        Assert.Throws<System.IO.InvalidDataException>(() => new CharacterLeagueData(new BufferReader(lBytes)));
    }
}
