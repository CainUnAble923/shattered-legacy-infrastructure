// cc-P664 (P481 F4): private copies of the Wakening Isle. Notes: shard-migration notes/cc-P664-private-isle-copies.md.
//
// Facts, each seen failing first:
//   1. Two trainees in two copies kill pests without crediting each other: each has their own copy, their own pests.
//   2. A party shares one copy: a member joins the copy their party's leader is on.
//   3. Logout and login inside a held copy: the copy is held for 30 minutes after logging out; logging back in before
//      then finds the trainee on the same copy, same generation.
//   4. Login after the copy was freed: past 30 minutes the copy is freed (its generation moves on) and the trainee who
//      logs in goes to a fresh copy at their first undone lesson.
//   5. A reset copy holds only fresh seeds: what the seeding placed is gone, a stray item is gone, and everything on the
//      copy afterwards is the new seeding.
//   6. A save and restart with copies in use: the copies' saved state reads back whole.
//   And: when all 16 copies are in use the next trainee waits on the Fold's beach, and goes to a copy when one frees.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.PartySystem;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialCopiesVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;
    private readonly List<PlayerMobile> _players = new();
    private readonly List<NetState> _states = new();
    private readonly List<IEntity> _litter = new();

    public TutorialCopiesVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        FoldMaps.Configure();
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialCorePlacement.IsTestShard = () => true; // copies are seeded as the test shard seeds them
        TutorialFarm.IsTestShard = () => true;
        TutorialTestStart.FreeAllCopies();
    }

    public void Dispose()
    {
        foreach (var pm in _players)
        {
            TutorialCoreTrials.Cancel(pm);
            foreach (var m in pm.AllFollowers?.ToList() ?? new List<Mobile>())
            {
                m.Delete();
            }

            pm.Party = null;
        }

        foreach (var e in _litter)
        {
            e.Delete();
        }

        foreach (var ns in _states)
        {
            if (ns.Mobile != null)
            {
                ns.Mobile.NetState = null;
            }
        }

        foreach (var pm in _players)
        {
            pm.Delete();
        }

        TutorialTestStart.FreeAllCopies();
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
    }

    /// <summary>A trainee on a fresh account, connected, at the New Haven start, entered on the isle.</summary>
    private PlayerMobile Trainee(string name, bool enter = true)
    {
        var account = new Account($"p664{Guid.NewGuid():N}"[..16], "p664-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = name, RawStr = 60, RawDex = 60, RawInt = 60 };
        pm.AddItem(new Backpack());
        pm.Hits = pm.HitsMax;
        account[0] = pm;
        pm.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = account;
        pm.NetState = ns;
        ns.Mobile = pm;
        _states.Add(ns);
        _players.Add(pm);
        if (enter)
        {
            TutorialStart.Record(pm, TutorialStart.Choice.Isle);
            ClusterFTutorial.OnLogin(pm);
        }

        return pm;
    }

    private static TutorialRecord RecordOf(PlayerMobile pm) => ClusterFTutorial.RecordOf(pm)!;

    /// <summary>Logs a character out as the server does: off the map, with where it was kept as its logout map.</summary>
    private static void LogOut(PlayerMobile pm, DateTime at)
    {
        pm.LastOnline = at;
        pm.NetState!.Mobile = null;
        pm.NetState = null;
        pm.LogoutLocation = pm.Location;
        pm.LogoutMap = pm.Map;
        pm.Internalize();
    }

    private void LogIn(PlayerMobile pm)
    {
        pm.MoveToWorld(pm.LogoutLocation, pm.LogoutMap);
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = pm.Account;
        pm.NetState = ns;
        ns.Mobile = pm;
        _states.Add(ns);
        ClusterFTutorial.OnLogin(pm);
    }

    private static List<BaseCreature> Pests(Map map) =>
        TutorialFarm.FindSpawner(map, TutorialFarm.PestSpot, TutorialFarm.PestTypes[0])!.Spawned.Keys.OfType<BaseCreature>().ToList();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TwoTraineesInTwoCopiesKillPestsWithoutCreditingEachOther()
    {
        var a = Trainee("CopyA");
        var b = Trainee("CopyB");
        Assert.True(TutorialCopies.IsCopy(a.Map));
        Assert.True(TutorialCopies.IsCopy(b.Map));
        Assert.NotSame(a.Map, b.Map);
        RecordOf(a).PetClaimed = true;
        RecordOf(b).PetClaimed = true;
        Assert.Equal(2, TutorialFarm.StepOf(a));
        Assert.Equal(2, TutorialFarm.StepOf(b));

        var mine = Pests(a.Map);
        var theirs = Pests(b.Map);
        Assert.NotEmpty(mine);
        Assert.Empty(mine.Intersect(theirs));
        Assert.All(mine, p => Assert.Same(a.Map, p.Map));

        var pest = mine[0];
        pest.Damage(1, a);
        pest.Kill();
        Assert.Equal(1, TutorialFarm.PestsKilled(a));
        Assert.Equal(0, TutorialFarm.PestsKilled(b));

        var other = theirs[0];
        other.Damage(1, b);
        other.Kill();
        Assert.Equal(1, TutorialFarm.PestsKilled(a));
        Assert.Equal(1, TutorialFarm.PestsKilled(b));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void APartySharesOneCopy()
    {
        var leader = Trainee("CopyLeader");
        var member = Trainee("CopyMember", enter: false);
        var party = new Party(leader);
        leader.Party = party;
        party.Add(member);

        TutorialStart.Record(member, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(member);

        Assert.True(TutorialCopies.IsCopy(leader.Map));
        Assert.Same(leader.Map, member.Map);
        var slot = TutorialCopies.SlotOf(leader.Map);
        Assert.Equal(2, TutorialCopies.Occupants(slot).Count);
        Assert.Equal(1, TutorialCopies.Slots.Count(s => s.InUse));

        // Someone outside the party gets a copy of their own.
        var stranger = Trainee("CopyStranger");
        Assert.NotSame(leader.Map, stranger.Map);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void LogoutAndLoginInsideAHeldCopy()
    {
        var pm = Trainee("CopyHeld");
        var copy = pm.Map;
        var slot = TutorialCopies.SlotOf(copy);
        var generation = slot.Generation;
        var now = Core.Now;

        LogOut(pm, now);
        TutorialCopies.Tick(now + TimeSpan.FromMinutes(29));
        Assert.True(slot.InUse); // held: the occupant logged out on it under 30 minutes ago
        Assert.Equal(generation, slot.Generation);

        LogIn(pm);
        Assert.Same(copy, pm.Map);
        Assert.Equal(generation, slot.Generation);
        Assert.Same(copy, TutorialCopies.CurrentCopyOf(pm));
    }

    // ---------------------------------------------------------------- 3b (cc-P707)

    [Fact]
    public void TheHoldIsTheSettingSoATestWorldCanShortenIt()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), TutorialCopies.HoldAfterLeaving);
        var pm = Trainee("CopyShortHold");
        var slot = TutorialCopies.SlotOf(pm.Map);
        var generation = slot.Generation;
        var now = Core.Now;
        try
        {
            ServerConfiguration.SetSetting("clusterf.tutorial.copyHoldMinutes", 2);
            Assert.Equal(TimeSpan.FromMinutes(2), TutorialCopies.HoldAfterLeaving);
            LogOut(pm, now);
            TutorialCopies.Tick(now + TimeSpan.FromMinutes(1));
            Assert.True(slot.InUse); // held for the two minutes
            TutorialCopies.Tick(now + TimeSpan.FromMinutes(3));
            Assert.False(slot.InUse); // freed after them
            Assert.Equal(generation + 1, slot.Generation);
            ServerConfiguration.SetSetting("clusterf.tutorial.copyHoldMinutes", 0);
            Assert.Equal(TimeSpan.FromMinutes(1), TutorialCopies.HoldAfterLeaving); // never less than a minute
        }
        finally
        {
            ServerConfiguration.SetSetting("clusterf.tutorial.copyHoldMinutes", 30);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void LoginAfterTheCopyWasFreedGoesToAFreshCopyAtTheNextLesson()
    {
        var pm = Trainee("CopyFreed");
        RecordOf(pm).StationsDone.UnionWith(["core.movement", "core.backpack", "core.paperdoll"]);
        var copy = pm.Map;
        var slot = TutorialCopies.SlotOf(copy);
        var generation = slot.Generation;
        var now = Core.Now;

        LogOut(pm, now);
        TutorialCopies.Tick(now + TimeSpan.FromMinutes(31));
        Assert.False(slot.InUse);
        Assert.Equal(generation + 1, slot.Generation);

        LogIn(pm);
        Assert.True(TutorialCopies.IsCopy(pm.Map));
        Assert.NotNull(TutorialCopies.CurrentCopyOf(pm)); // a current copy: the generation it was given matches
        var chat = TutorialCorePlacement.MarkerSpot("core.chat");
        Assert.Equal(new Point2D(chat.X, chat.Y), new Point2D(pm.X, pm.Y)); // the first undone lesson's stop
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AResetCopyHoldsOnlyFreshSeeds()
    {
        var pm = Trainee("CopyReset");
        var copy = pm.Map;
        var slot = TutorialCopies.SlotOf(copy);
        var first = slot.Placed.ToList();
        Assert.NotEmpty(first);
        var stray = new Gold(50);
        stray.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), copy);

        TutorialStart.SendHome(pm); // leaves the copy for New Haven
        Assert.Same(HavenSpots.Map, pm.Map);
        TutorialCopies.Tick(Core.Now + TimeSpan.FromMinutes(31));

        Assert.False(slot.InUse);
        Assert.True(slot.Seeded);
        Assert.True(stray.Deleted);
        Assert.All(first, serial => Assert.Null(World.FindEntity(serial) is { Deleted: false } e && e.Map == copy ? e : null));
        var now = World.Items.Values.Where(i => !i.Deleted && i.Map == copy && i.Parent == null).Select(i => i.Serial)
            .Concat(World.Mobiles.Values.Where(m => !m.Deleted && m.Map == copy && !m.Player).Select(m => m.Serial)).ToHashSet();
        Assert.Equal(now, slot.Placed.ToHashSet()); // everything on the copy is the new seeding
        Assert.Equal(first.Count, slot.Placed.Count);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ASaveAndRestartWithCopiesInUseReadsBackWhole()
    {
        var a = Trainee("CopySaveA");
        var b = Trainee("CopySaveB");
        var sa = TutorialCopies.SlotOf(a.Map);
        var sb = TutorialCopies.SlotOf(b.Map);
        var before = TutorialCopies.Slots.Select(s => (s.Generation, s.Seeded, s.InUse, s.LastSeen, Placed: string.Join(",", s.Placed))).ToList();
        var assigned = TutorialCopies.Assigned.ToDictionary(p => p.Key, p => p.Value);

        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        TutorialCopiesStore.Instance.Serialize(writer);
        writer.Flush();

        // The restart: the in-memory state is gone, then the save is read.
        foreach (var s in TutorialCopies.Slots)
        {
            s.Generation = 99;
            s.Seeded = false;
            s.InUse = false;
            s.Placed.Clear();
        }

        TutorialCopies.Assigned.Clear();
        TutorialCopiesStore.Instance.Deserialize(new BufferReader(buffer));

        Assert.Equal(before, TutorialCopies.Slots.Select(s => (s.Generation, s.Seeded, s.InUse, s.LastSeen, Placed: string.Join(",", s.Placed))).ToList());
        Assert.Equal(assigned, TutorialCopies.Assigned.ToDictionary(p => p.Key, p => p.Value));
        Assert.True(sa.InUse && sb.InUse);
        Assert.Same(a.Map, TutorialCopies.CurrentCopyOf(a));
        Assert.Same(b.Map, TutorialCopies.CurrentCopyOf(b));
    }

    // ---------------------------------------------------------------- the line

    [Fact]
    public void WhenAllCopiesAreInUseTheNextTraineeWaitsOnTheBeachUntilOneFrees()
    {
        var now = Core.Now;
        foreach (var s in TutorialCopies.Slots)
        {
            s.InUse = true; // held by nobody online: Tick frees them 30 minutes after LastSeen
            s.LastSeen = now;
        }

        var pm = Trainee("CopyWaiting");
        Assert.Same(TutorialWorld.Isle, pm.Map); // the Fold, map 32
        Assert.Equal(TutorialWorld.Arrival, pm.Location);
        Assert.Contains(pm.Serial, TutorialCopies.Waiting);

        TutorialCopies.Tick(now + TimeSpan.FromMinutes(10));
        Assert.Same(TutorialWorld.Isle, pm.Map); // still held

        TutorialCopies.Tick(now + TimeSpan.FromMinutes(31));
        Assert.True(TutorialCopies.IsCopy(pm.Map));
        Assert.DoesNotContain(pm.Serial, TutorialCopies.Waiting);
        Assert.Same(pm.Map, TutorialCopies.CurrentCopyOf(pm));
    }
}
