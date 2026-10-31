// cc-P740: the healing lesson's bandages again, from the farmhouse table beside Pip or from Wren, with Wren's hint when the
// trainee has none. (The wake's part of P740 is in TutorialIsleWalkFixes4Verification.) Notes: shard-migration
// notes/cc-P740-isle-wake-visible.md.
//
// Facts, each seen failing first:
//   1. The table beside Pip holds bandages that stay there: a trainee on the healing lesson with none gets a stack of
//      their own by double-clicking or lifting them, a few times at most; nobody else gets any.
//   2. While a trainee on the healing lesson has no bandages, Wren says where to get them, a few times; double-clicked,
//      she hands more or sends them to the table; a bandage from either finishes the lesson.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIsleWakeVisibleVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleWakeVisibleVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWake.EnsureHooks();
        TutorialWorld.IsleReady = () => true;
        TutorialTestStart.ReleaseIdleCopies();
    }

    public void Dispose()
    {
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p740{Guid.NewGuid():N}"[..16], "p740-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Bandager", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            TutorialWake.Stand(Pm, true);
            foreach (var m in Pm.AllFollowers?.ToList() ?? new List<Mobile>())
            {
                m.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    /// <summary>On the isle, up, the farm done, the healing lesson started (its own bandages in the pack).</summary>
    private static Map AtTheHealingLesson(Player p)
    {
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialCorePlacement.IsTestShard = () => true;
        TutorialFarm.IsTestShard = () => true;
        TutorialTestStart.FreeAllCopies();
        TutorialTestStart.Begin(p.Pm);
        TutorialWake.Stand(p.Pm, true);
        TutorialTestStart.ToTheFarm(p.Pm);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialFarm.CoreStation)!);
        p.Record.PetClaimed = true;
        p.Record.StationsDone.UnionWith([TutorialFarm.PestsKey, TutorialFarm.FollowKey, TutorialFarm.GuardKey]);
        TutorialFarm.CheckDone(p.Pm);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialFarm.HealingStationKey)!);
        Assert.True(TutorialCoreTrials.IsDoing(p.Pm, TutorialFarm.HealingStationKey));
        Assert.True(TutorialFarm.HealingOn(p.Pm));
        return TutorialWorld.MapOf(p.Pm)!;
    }

    private static List<Bandage> InPack(PlayerMobile pm)
    {
        var list = new List<Bandage>();
        foreach (var item in pm.Backpack!.Items)
        {
            if (item is Bandage b)
            {
                list.Add(b);
            }
        }

        return list;
    }

    private static int Bandages(PlayerMobile pm) => InPack(pm).Sum(b => b.Amount);

    private static void UseThemUp(PlayerMobile pm)
    {
        foreach (var b in InPack(pm))
        {
            b.Delete();
        }
    }

    private static int TablesNear(Map map)
    {
        var n = 0;
        foreach (var _ in map.GetItemsInRange<TutorialTableBandages>(TutorialFarm.TableBandagesSpot, 3))
        {
            n++;
        }

        return n;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTableBesidePipHandsBandagesThatStayThere()
    {
        using var p = new Player();
        using var other = new Player();
        var map = AtTheHealingLesson(p);

        // On the farmhouse table, beside Pip's bench; it never leaves the table.
        var table = TutorialFarm.FindTableBandages(map);
        Assert.NotNull(table);
        Assert.Equal(TutorialFarm.TableBandagesSpot, table!.Location);
        Assert.False(table.Movable);
        Assert.True(TutorialFarm.PipBench.X - table.X <= 2 && Math.Abs(TutorialFarm.PipBench.Y - table.Y) <= 1);

        // With the lesson's own bandages in the pack: none more.
        var had = Bandages(p.Pm);
        Assert.True(had > 0);
        Assert.False(TutorialFarm.TakeTableBandages(p.Pm));
        Assert.Equal(had, Bandages(p.Pm));

        // Used up: a stack of their own, and the table's stays.
        UseThemUp(p.Pm);
        Assert.True(TutorialFarm.TakeTableBandages(p.Pm));
        Assert.Equal(TutorialFarm.TableBandagesGiven, Bandages(p.Pm));
        Assert.False(table.Deleted);
        Assert.Equal(TutorialFarm.TableBandagesSpot, table.Location);

        // A drag gives the same and leaves the stack on the table.
        UseThemUp(p.Pm);
        var reject = LRReason.CannotLift;
        Assert.True(table.VerifyMove(p.Pm));
        Assert.False(table.CheckLift(p.Pm, table, ref reject));
        Assert.Equal(TutorialFarm.TableBandagesGiven, Bandages(p.Pm));
        Assert.Null(table.Parent);

        // Not to be hoarded: a few times a character.
        for (var i = 2; i < TutorialFarm.TableTakes; i++)
        {
            UseThemUp(p.Pm);
            Assert.True(TutorialFarm.TakeTableBandages(p.Pm));
        }

        UseThemUp(p.Pm);
        Assert.False(TutorialFarm.TakeTableBandages(p.Pm));
        Assert.Equal(0, Bandages(p.Pm));

        // Nobody off the healing lesson gets any.
        Assert.False(TutorialFarm.TakeTableBandages(other.Pm));
        Assert.Null(other.Pm.Backpack!.FindItemByType<Bandage>());

        // The isle's seed places it once.
        Assert.Equal(1, TablesNear(map));
        TutorialFarm.Seed(map, false);
        Assert.Equal(1, TablesNear(map));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void WrenSaysWhereTheBandagesAreAndEitherKindFinishesTheLesson()
    {
        using var p = new Player();
        var map = AtTheHealingLesson(p);
        var wren = TutorialFarmhand.Find(map);
        Assert.NotNull(wren);

        // With bandages, no hint.
        Assert.False(TutorialFarm.HintBandages(p.Pm));

        // Without: Wren's hint, a few times in a row, then quiet.
        UseThemUp(p.Pm);
        for (var i = 0; i < TutorialFarm.HintTimes; i++)
        {
            Assert.True(TutorialFarm.HintBandages(p.Pm));
        }

        Assert.False(TutorialFarm.HintBandages(p.Pm));
        Assert.Contains("table beside Pip", TutorialFarm.WrenBandageHintLine);

        // Double-clicked, she hands more (the lesson's first stack was one of BandageIssues).
        Assert.Equal(4, TutorialFarm.Talk(p.Pm, wren!));
        Assert.True(Bandages(p.Pm) > 0);
        Assert.Equal(TutorialFarm.WrenHasBandagesLine, TutorialFarm.WrenBandages(p.Pm));

        // Out of her stacks: she sends them to the table, and the hints start again.
        UseThemUp(p.Pm);
        p.Record.BandagesIssued = TutorialFarm.BandageIssues;
        Assert.Equal(TutorialFarm.WrenTableLine, TutorialFarm.WrenBandages(p.Pm));
        Assert.Equal(0, Bandages(p.Pm));

        // From the table, then a heal on Pip finishes the lesson.
        Assert.True(TutorialFarm.TakeTableBandages(p.Pm));
        Assert.False(TutorialFarm.HintBandages(p.Pm));
        Assert.True(TutorialFarm.OnHealed(p.Pm));
        Assert.Contains(TutorialFarm.HealingStationKey, p.Record.StationsDone);

        // Then Wren is the farm's again, and the table is not theirs.
        Assert.False(TutorialFarm.HealingOn(p.Pm));
        UseThemUp(p.Pm);
        Assert.False(TutorialFarm.TakeTableBandages(p.Pm));
        Assert.False(TutorialFarm.HintBandages(p.Pm));
    }
}
