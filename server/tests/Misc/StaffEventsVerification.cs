// StaffEventsVerification.cs
//
// cc-P810: the staff events, the egg hunt and the mine maze. Notes in shard-migration
// notes/cc-P810-staff-events-egg-hunt-mine-maze.md.
//
// Facts: the egg hunt hides an egg, tells the shard and points the players on its facet; the first living player in range
// wins the tokens and the hunt ends; it runs out in time and a GM can end it; a saved egg removes itself after load. The
// maze's layout always has a joined safe path and comes from its seed; laying it checks its size, the isle and one at a
// time; a mine kills, the dead keep their items and rise at the start; the first three through win, once each; entry only
// over the start row, no Young, no travel; the fair reset moves the mines, clears the winners and sends everyone back; a
// maze survives a save; the Staff Hub's Events tab opens the events; a bad settings file is refused whole.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.RewardStores;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StaffEventsVerification : IDisposable
{
    private static readonly Point3D EggSpot = new(3460, 2604, 18);
    private static readonly Point3D MazeCorner = new(5420, 3820, 0);
    private static bool _achievements;

    private readonly List<Account> _accounts = [];

    public StaffEventsVerification()
    {
        ShardTestHost.EnsureAccounts();
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        StaffEventsSettings.Current = new StaffEventsSettings();
        StaffEggHunt.ForgetForTests();
        StaffMineMaze.ForgetForTests();
    }

    public void Dispose()
    {
        StaffEggHunt.ForgetForTests();
        StaffMineMaze.ForgetForTests();
        StaffMineMaze.NewSeed = () => Utility.Random(int.MaxValue);
        StaffEventsSettings.Current = new StaffEventsSettings();

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    m.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    private PlayerMobile Character(AccessLevel level = AccessLevel.Player, Point3D? at = null, Map map = null)
    {
        var account = new Account($"p810{Guid.NewGuid():N}"[..16], "p810-test-only") { Young = false };
        _accounts.Add(account);

        var pm = new PlayerMobile
        {
            Player = true, Race = Race.Human, AccessLevel = level, Name = $"P810 {Guid.NewGuid():N}"[..12]
        };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(at ?? EggSpot, map ?? Map.Trammel);

        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = account;
        return pm;
    }

    private static int Tokens(PlayerMobile pm) => FestivalTokens.Instance.Balance(pm);

    // ---------------------------------------------------------------- the egg hunt

    [Fact]
    public void TheEggIsHiddenAndThePlayersOnItsFacetGetAnArrow()
    {
        var gm = Character(AccessLevel.GameMaster);
        var near = Character(at: new Point3D(EggSpot.X + 30, EggSpot.Y, EggSpot.Z));
        var elsewhere = Character(map: Map.Felucca);

        Assert.True(StaffEggHunt.Start(gm, Map.Trammel, EggSpot, out var message), message);
        Assert.True(StaffEggHunt.Running);
        var egg = StaffEggHunt.Egg;
        Assert.Equal(0x47E6, egg.ItemID);
        Assert.False(egg.Movable);
        Assert.Equal(EggSpot, egg.Location);

        Assert.IsType<EggHuntArrow>(near.QuestArrow);
        Assert.Null(elsewhere.QuestArrow);
        Assert.Null(gm.QuestArrow);

        // A player who arrives later gets one at the next tick.
        var late = Character(at: new Point3D(EggSpot.X - 20, EggSpot.Y, EggSpot.Z));
        late.QuestArrow = null;
        StaffEggHunt.Tick();
        Assert.IsType<EggHuntArrow>(late.QuestArrow);

        // One at a time; never on the isle.
        Assert.False(StaffEggHunt.Start(gm, Map.Trammel, EggSpot, out _));
        StaffEggHunt.ForgetForTests();
        if (Server.Tutorial.TutorialWorld.Isle is { } isle)
        {
            Assert.False(StaffEggHunt.Start(gm, isle, EggSpot, out _));
        }
    }

    [Fact]
    public void TheFirstLivingPlayerInRangeWinsTheTokensAndTheHuntEnds()
    {
        var gm = Character(AccessLevel.GameMaster);
        var far = Character(at: new Point3D(EggSpot.X + 5, EggSpot.Y, EggSpot.Z));
        var finder = Character(at: new Point3D(EggSpot.X + 1, EggSpot.Y, EggSpot.Z));
        Assert.True(StaffEggHunt.Start(gm, Map.Trammel, EggSpot, out _));
        var egg = StaffEggHunt.Egg;
        var before = Tokens(finder);

        Assert.False(StaffEggHunt.TryClaim(far, egg));     // out of range
        Assert.False(StaffEggHunt.TryClaim(gm, egg));      // staff
        finder.Kill();
        Assert.False(StaffEggHunt.TryClaim(finder, egg));  // a ghost
        finder.Resurrect();
        Assert.True(StaffEggHunt.Running);
        Assert.Equal(before, Tokens(finder));

        Assert.True(StaffEggHunt.TryClaim(finder, egg));
        Assert.Equal(before + 25, Tokens(finder));
        Assert.False(StaffEggHunt.Running);
        Assert.True(egg.Deleted);
        Assert.False(finder.QuestArrow?.Running ?? false);
        Assert.False(StaffEggHunt.TryClaim(far, egg));     // nothing left to win
    }

    [Fact]
    public void TheHuntRunsOutInTimeAndAGmCanEndIt()
    {
        var gm = Character(AccessLevel.GameMaster);
        Assert.True(StaffEggHunt.Start(gm, Map.Trammel, EggSpot, out _));
        var egg = StaffEggHunt.Egg;
        StaffEggHunt.ExpireNowForTests();
        StaffEggHunt.Tick();
        Assert.False(StaffEggHunt.Running);
        Assert.True(egg.Deleted);
        Assert.Contains("Nobody", StaffEggHunt.LastResult);

        Assert.True(StaffEggHunt.Start(gm, Map.Trammel, EggSpot, out _));
        Assert.True(StaffEggHunt.End(gm, out _));
        Assert.False(StaffEggHunt.Running);
        Assert.False(StaffEggHunt.End(gm, out _));
    }

    [Fact]
    public void ASavedEggRemovesItselfAfterLoad()
    {
        var egg = new EventEgg();
        egg.MoveToWorld(EggSpot, Map.Trammel);
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        egg.Serialize(writer);
        writer.Flush();
        egg.Delete();

        ShardTestClock.Arm();
        var copy = (EventEgg)Activator.CreateInstance(typeof(EventEgg), egg.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.False(copy.Deleted);
        ShardTestClock.AdvanceSeconds(0.1);
        Assert.True(copy.Deleted);
    }

    // ---------------------------------------------------------------- the mine maze

    private static bool Crossable(MineMazeRun run)
    {
        var seen = new HashSet<Point2D>();
        var queue = new Queue<Point2D>();
        for (var l = 0; l < run.Width; l++)
        {
            var t = run.Tile(0, l);
            seen.Add(t);
            queue.Enqueue(t);
        }

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (run.IsFinish(p.X, p.Y))
            {
                return true;
            }

            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new Point2D(p.X + dx, p.Y + dy);
                if (run.Contains(n.X, n.Y) && !run.IsMine(n.X, n.Y) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return false;
    }

    [Fact]
    public void EveryLayoutHasAJoinedSafePathAndComesFromItsSeed()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            var run = new MineMazeRun { X1 = 100, Y1 = 100, X2 = 100 + 5 + seed % 20, Y2 = 100 + 4 + seed % 7, Seed = seed };
            run.AlongX = run.X2 - run.X1 >= run.Y2 - run.Y1;
            run.FromLow = seed % 2 == 0;
            run.Generate(60);

            Assert.True(Crossable(run), $"seed {seed} has no way across");
            for (var l = 0; l < run.Width; l++)
            {
                var s = run.Tile(0, l);
                var f = run.Tile(run.Length - 1, l);
                Assert.False(run.IsMine(s.X, s.Y));
                Assert.False(run.IsMine(f.X, f.Y));
            }

            Assert.NotEmpty(run.Mines);

            var again = new MineMazeRun { X1 = run.X1, Y1 = run.Y1, X2 = run.X2, Y2 = run.Y2, Seed = seed, AlongX = run.AlongX, FromLow = run.FromLow };
            again.Generate(60);
            Assert.True(run.Mines.SetEquals(again.Mines));
        }
    }

    private MineMazeRun Lay(int length = 12, int width = 6)
    {
        StaffMineMaze.NewSeed = () => 1234;
        var far = new Point3D(MazeCorner.X + length - 1, MazeCorner.Y + width - 1, MazeCorner.Z);
        Assert.True(StaffMineMaze.Start(Map.Trammel, MazeCorner, far, out var message), message);
        return StaffMineMaze.Active;
    }

    private static Point3D At(MineMazeRun run, Point2D p) => new(p.X, p.Y, run.Map.GetAverageZ(p.X, p.Y));

    private static Point2D SafeTile(MineMazeRun run, int step)
    {
        for (var l = 0; l < run.Width; l++)
        {
            var t = run.Tile(step, l);
            if (!run.IsMine(t.X, t.Y))
            {
                return t;
            }
        }

        throw new InvalidOperationException("no safe tile");
    }

    [Fact]
    public void LayingTheMazeChecksItsSizeTheIsleAndOneAtATime()
    {
        var c = MazeCorner;
        Assert.False(StaffMineMaze.Start(Map.Trammel, c, new Point3D(c.X + 3, c.Y + 10, 0), out _));   // a side under 5
        Assert.False(StaffMineMaze.Start(Map.Trammel, c, new Point3D(c.X + 50, c.Y + 50, 0), out _));  // over 1,600 tiles
        if (Server.Tutorial.TutorialWorld.Isle is { } isle)
        {
            Assert.False(StaffMineMaze.Start(isle, c, new Point3D(c.X + 10, c.Y + 6, 0), out _));
        }

        var run = Lay();
        Assert.True(run.AlongX);
        Assert.True(run.FromLow);              // the start row is the first corner's end
        Assert.True(run.IsStart(c.X, c.Y));
        Assert.True(run.IsFinish(c.X + 11, c.Y + 5));
        Assert.NotEmpty(run.Mines);
        Assert.IsType<MineMazeRegion>(Region.Find(new Point3D(c.X + 5, c.Y + 3, 0), Map.Trammel));
        Assert.False(StaffMineMaze.Start(Map.Trammel, c, new Point3D(c.X + 11, c.Y + 5, 0), out _));

        Assert.True(StaffMineMaze.End(out _));
        Assert.Null(StaffMineMaze.Active);
        Assert.IsNotType<MineMazeRegion>(Region.Find(new Point3D(c.X + 5, c.Y + 3, 0), Map.Trammel));
    }

    [Fact]
    public void AMineKillsTheDeadKeepTheirItemsAndRiseAtTheStart()
    {
        var run = Lay();
        var pm = Character(at: At(run, SafeTile(run, 0)));
        var ring = new GoldRing();
        pm.Backpack.DropItem(ring);

        ShardTestClock.Arm();
        var mine = run.Mines.First();
        pm.Location = At(run, mine);

        Assert.False(pm.Alive);
        Assert.Same(pm.Backpack, ring.Parent);      // kept, not on the corpse

        ShardTestClock.AdvanceSeconds(StaffEventsSettings.Current.MazeReviveSeconds + 1);
        Assert.True(pm.Alive);
        Assert.True(run.IsStart(pm.X, pm.Y));
    }

    [Fact]
    public void TheFirstThreeThroughWinOnceEach()
    {
        var run = Lay();
        var runners = Enumerable.Range(0, 4).Select(_ => Character(at: At(run, SafeTile(run, 0)))).ToList();
        var before = runners.Select(Tokens).ToList();
        var finish = SafeTile(run, run.Length - 1);

        // The first runner crosses twice before anyone else: one place, one prize.
        runners[0].Location = At(run, finish);
        runners[0].Location = At(run, SafeTile(run, 0));
        runners[0].Location = At(run, finish);
        Assert.Single(run.Winners);
        Assert.Equal(before[0] + 20, Tokens(runners[0]));
        runners[0].Location = At(run, SafeTile(run, 0));

        foreach (var pm in runners.Skip(1))
        {
            pm.Location = At(run, finish);
            pm.Location = At(run, SafeTile(run, 0));
        }

        Assert.Equal(3, run.Winners.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(before[i] + 20, Tokens(runners[i]));
        }

        Assert.Equal(before[3], Tokens(runners[3]));
    }

    [Fact]
    public void PlayersEnterOnlyOverTheStartRowAndCannotTravelInOrOut()
    {
        var run = Lay();
        var region = run.Region;
        var pm = Character(at: new Point3D(MazeCorner.X - 2, MazeCorner.Y, MazeCorner.Z));
        var outside = new Point3D(MazeCorner.X - 1, MazeCorner.Y + 2, 0);
        var start = run.Tile(0, 2);
        var side = run.Tile(5, 0);

        Assert.True(region.OnMoveInto(pm, Direction.East, new Point3D(start.X, start.Y, 0), outside));
        Assert.False(region.OnMoveInto(pm, Direction.South, new Point3D(side.X, side.Y, 0), new Point3D(side.X, side.Y - 1, 0)));

        var young = Character(at: outside);
        young.Young = true;
        Assert.False(region.OnMoveInto(young, Direction.East, new Point3D(start.X, start.Y, 0), outside));

        var gm = Character(AccessLevel.GameMaster, outside);
        Assert.True(region.OnMoveInto(gm, Direction.South, new Point3D(side.X, side.Y, 0), new Point3D(side.X, side.Y - 1, 0)));

        Assert.False(region.CheckTravel(pm, new Point3D(start.X, start.Y, 0), TravelCheckType.RecallTo, out _));
        Assert.False(region.CheckTravel(pm, new Point3D(start.X, start.Y, 0), TravelCheckType.TeleportTo, out _));
        Assert.False(region.CheckTravel(pm, new Point3D(start.X, start.Y, 0), TravelCheckType.Mark, out _));
    }

    [Fact]
    public void TheFairResetMovesTheMinesClearsTheWinnersAndSendsEveryoneBack()
    {
        var run = Lay();
        var winner = Character(at: At(run, SafeTile(run, 0)));
        winner.Location = At(run, SafeTile(run, run.Length - 1));
        Assert.Single(run.Winners);

        var walker = Character(at: At(run, SafeTile(run, 0)));
        walker.Location = At(run, SafeTile(run, 4));
        var mines = run.Mines.ToHashSet();

        StaffMineMaze.NewSeed = () => 98765;
        Assert.True(StaffMineMaze.Reset(out _));

        Assert.False(mines.SetEquals(run.Mines));
        Assert.Empty(run.Winners);
        Assert.True(run.IsStart(walker.X, walker.Y));
        Assert.True(run.IsStart(winner.X, winner.Y));
    }

    [Fact]
    public void AMazeSurvivesASaveAndAnUnknownVersionIsRefused()
    {
        var run = Lay();
        var winner = Character(at: At(run, SafeTile(run, 0)));
        winner.Location = At(run, SafeTile(run, run.Length - 1));
        var mines = run.Mines.ToHashSet();

        var store = new StaffMineMazeStore();
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        store.Serialize(writer);
        writer.Flush();

        StaffMineMazeStoreRoundTrip(store, buffer);
        var back = StaffMineMaze.Active;
        Assert.NotNull(back);
        Assert.NotSame(run, back);
        Assert.True(mines.SetEquals(back.Mines));
        Assert.Equal(run.Seed, back.Seed);
        Assert.Same(winner, Assert.Single(back.Winners));
        Assert.IsType<MineMazeRegion>(Region.Find(new Point3D(MazeCorner.X + 5, MazeCorner.Y + 3, 0), Map.Trammel));

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(StaffMineMazeStore.CurrentVersion + 1);
        w2.Flush();
        Assert.Throws<InvalidDataException>(() => store.Deserialize(new BufferReader(bad)));
    }

    private static void StaffMineMazeStoreRoundTrip(StaffMineMazeStore store, byte[] buffer)
    {
        store.Deserialize(new BufferReader(buffer)); // drops the live maze, as a fresh start would have none
        Assert.Null(StaffMineMaze.Active);
        StaffMineMaze.RegisterPending();
        StaffMineMaze.RegisterPending(); // idempotent
    }

    // ---------------------------------------------------------------- the Staff Hub and the settings

    [Fact]
    public void TheEventsTabOpensTheStaffEventsForAGameMasterOnly()
    {
        var gm = Character(AccessLevel.GameMaster);
        var state = new StaffHubState { Tab = StaffHubTab.Events };
        var hub = new StaffHubGump(gm, state);
        Assert.Contains(StaffHubGump.BtnStaffEvents, hub.Entries.OfType<GumpButton>().Select(b => b.ButtonID));

        hub.Respond(gm, StaffHubGump.BtnStaffEvents);
        Assert.True(gm.HasGump<StaffEventsGump>());

        StaffEventsGump.Respond(gm, StaffEventsGump.BtnEggHere);
        Assert.True(StaffEggHunt.Running);
        StaffEventsGump.Respond(gm, StaffEventsGump.BtnEggEnd);
        Assert.False(StaffEggHunt.Running);

        var player = Character();
        StaffEventsGump.Respond(player, StaffEventsGump.BtnEggHere);
        Assert.False(StaffEggHunt.Running);

        // Corners then the maze, from the gump.
        gm.MoveToWorld(MazeCorner, Map.Trammel);
        StaffEventsGump.Respond(gm, StaffEventsGump.BtnCorner1);
        gm.MoveToWorld(new Point3D(MazeCorner.X + 9, MazeCorner.Y + 5, MazeCorner.Z), Map.Trammel);
        StaffEventsGump.Respond(gm, StaffEventsGump.BtnCorner2);
        StaffEventsGump.Respond(gm, StaffEventsGump.BtnMazeStart);
        Assert.NotNull(StaffMineMaze.Active);
        StaffEventsGump.Respond(gm, StaffEventsGump.BtnMazeEnd);
        Assert.Null(StaffMineMaze.Active);
    }

    [Fact]
    public void TheTypedCommandRunsBothEventsAndListsTheSafePath()
    {
        StaffEventsGump.Register();
        StaffEventsGump.Register(); // idempotent
        var gm = Character(AccessLevel.GameMaster);

        Assert.True(CommandSystem.Handle(gm, "[StaffEvents egg here"));
        Assert.True(StaffEggHunt.Running);
        Assert.Equal(gm.Location, StaffEggHunt.Egg.Location);
        CommandSystem.Handle(gm, "[StaffEvents egg end");
        Assert.False(StaffEggHunt.Running);

        gm.MoveToWorld(MazeCorner, Map.Trammel);
        CommandSystem.Handle(gm, "[StaffEvents maze corner1");
        gm.MoveToWorld(new Point3D(MazeCorner.X + 9, MazeCorner.Y + 5, MazeCorner.Z), Map.Trammel);
        CommandSystem.Handle(gm, "[StaffEvents maze corner2");
        CommandSystem.Handle(gm, "[StaffEvents maze lay");
        var run = StaffMineMaze.Active;
        Assert.NotNull(run);

        // The listed path is the safe path: no mine on it, from the start row to the finish row.
        Assert.NotEmpty(run.Path);
        Assert.True(run.IsStart(run.Path[0].X, run.Path[0].Y));
        Assert.True(run.IsFinish(run.Path[^1].X, run.Path[^1].Y));
        Assert.DoesNotContain(run.Path, p => run.IsMine(p.X, p.Y));
        Assert.StartsWith("Safe path: ", StaffMineMaze.PathLine());

        CommandSystem.Handle(gm, "[StaffEvents maze end");
        Assert.Null(StaffMineMaze.Active);
    }

    [Fact]
    public void ABadSettingsFileIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(StaffEventsSettings.Parse("{\"MazePrize\": \"PowerScroll\"}", errors));
        Assert.NotEmpty(errors);

        errors.Clear();
        Assert.Null(StaffEventsSettings.Parse("{\"MazeMinePercent\": 95}", errors));

        errors.Clear();
        Assert.Null(StaffEventsSettings.Parse("{\"MazeBoxTheme\": \"Nonsense\"}", errors));

        errors.Clear();
        var ok = StaffEventsSettings.Parse(StaffEventsSettings.ToJson(new StaffEventsSettings { EggTokens = 40 }), errors);
        Assert.Equal(40, ok.EggTokens);
        Assert.Empty(errors);
    }
}
