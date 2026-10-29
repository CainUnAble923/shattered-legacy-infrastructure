// cc-P728: fixes from Chase's walk of the Wakening Isle (round 4). Notes: shard-migration
// notes/cc-P728-isle-walkthrough-fixes-4.md.
//
// Facts, each seen failing first:
//   1. A new character's first-login windows (the first achievement with its window, fanfare and box; the patch notes; the
//      welcome lines) wait until the trainee has stood up, and the guild welcome waits for a login off the isle.
//   2. The trainee lies on the sand until their first move (a move request gets them up and is spent on it), with the
//      light coming up, "Move to get up." and a safety stand after a minute; a disconnect ends the lie.
//   3. The cloak comes in a chest that appears for its trainee: only they can open or lift from it, nothing goes in, it
//      vanishes once the cloak is on them, and it comes back with a new cloak when the cloak was lost.
//   4. The farm's combat part starts with the pitchfork, then war mode, then the pet's commands, then the pests; a trainee
//      with the commands from the old order goes on to the pests once armed and in war mode.
//   6. Wren never says "0 more": with the count reached she names what is missing. The cleared rows are said with the next
//      step over the trainee's head, in the system line, at the top of the (reopened) lesson window, and the arrow goes to
//      Wren; [lesson opens the window again. Every finished lesson names the next and points at its signpost.
//   7. The isle seed at world load places what is missing (P721's Pip on a copy seeded before him), records it for the
//      copy's reset, places nothing more the second time, and does nothing while the tutorial is off.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIsleWalkFixes4Verification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleWalkFixes4Verification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFTutorial.Enabled = true;
        ClusterFTutorial.SkipCountsAsGraduation = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWake.EnsureHooks();
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
        TutorialWorld.IsleReady = () => true;
        TutorialTestStart.ReleaseIdleCopies();
    }

    public void Dispose()
    {
        ClusterFLootBoxes.LoopProvider = _loopProvider;
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
        ClusterFTutorial.Enabled = true;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p728{Guid.NewGuid():N}"[..16], "p728-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Ashore", RawStr = 60, RawDex = 60, RawInt = 60 };
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

        public T Add<T>(T e) where T : IEntity
        {
            Litter.Add(e);
            return e;
        }

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            TutorialWake.Stand(Pm);
            ClusterFFanfare.FlushAll();
            foreach (var e in Litter)
            {
                e.Delete();
            }

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

    private static TutorialAction[] Evidence(string key) => key switch
    {
        "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
        "core.paperdoll" => [TutorialAction.Equipped],
        "core.backpack" => [TutorialAction.OpenedPaperdoll, TutorialAction.OpenedBag],
        "core.chat" => [TutorialAction.SaidPhrase],
        _ => throw new InvalidOperationException(key)
    };

    private static void PlayCoreUntil(PlayerMobile pm, string stop)
    {
        foreach (var station in TutorialStations.Core.ToList())
        {
            if (station.Key == stop)
            {
                return;
            }

            ClusterFTutorial.RunStation(pm, station);
            foreach (var action in Evidence(station.Key))
            {
                TutorialCoreTrials.Observe(pm, action);
            }
        }
    }

    /// <summary>Into the tutorial on the isle, and up off the sand.</summary>
    private static void Awake(Player p)
    {
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWake.OnMoveRequest(p.Pm));
    }

    private static void Hold(PlayerMobile pm)
    {
        var fork = pm.Backpack!.FindItemByType<TutorialPitchfork>()!;
        fork.Layer = Layer.TwoHanded; // the host has no tiledata
        Assert.True(pm.EquipItem(fork));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheFirstLoginWindowsWaitUntilTheTraineeStandsUp()
    {
        using var p = new Player();

        // A login handler that runs before the tutorial's (the generated event's order is not ours): the choice is pending.
        TutorialStart.Record(p.Pm, TutorialStart.Choice.Isle);
        Assert.True(TutorialStart.WillWake(p.Pm));
        ClusterFAchievementSystem.OnLogin(p.Pm);
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWake.IsLying(p.Pm));

        // One that runs after it: the trainee is lying.
        GuildWelcome.OnLogin(p.Pm);

        Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, "exploration.citizen"));
        Assert.Null(p.Pm.FindGump<AchievementEarnedGump>());
        Assert.Null(p.Pm.FindGump<GuildWelcomeGump>()); // not on the isle at all
        Assert.True(TutorialWake.HeldCount(p.Pm) >= 1);
        TutorialWake.Advance(p.Pm, 20);
        Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, "exploration.citizen")); // still down

        // Up: the first lines, then the held windows a moment later.
        Assert.False(p.Pm.Move(Direction.North));
        Assert.False(TutorialWake.IsLying(p.Pm));
        Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, "exploration.citizen"));
        ShardTestClock.Advance(TutorialWake.HeldDelay + TimeSpan.FromSeconds(1));
        Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, "exploration.citizen"));
        Assert.NotNull(p.Pm.FindGump<AchievementEarnedGump>());
        Assert.Equal(0, TutorialWake.HeldCount(p.Pm));
        Assert.Null(p.Pm.FindGump<GuildWelcomeGump>());

        // A later login holds nothing.
        Assert.False(TutorialStart.WillWake(p.Pm));
        Assert.False(TutorialWake.IsHolding(p.Pm));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheTraineeLiesOnTheSandUntilTheirFirstMove()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWake.IsLying(p.Pm));
        Assert.False(p.Pm.Frozen); // cc-P732: not frozen, so the real client sends the move that gets them up
        Assert.True(TutorialWake.LightOf(p.Pm) > 10); // dark
        Assert.Equal(1, TutorialWake.PosesSent(p.Pm)); // cc-P732: the lying pose

        TutorialWake.Advance(p.Pm, TutorialWake.HintSeconds);
        Assert.True(TutorialWake.IsLying(p.Pm));
        Assert.True(TutorialWake.LightOf(p.Pm) > 10); // cc-P732: still dark while they lie
        Assert.Equal(2, TutorialWake.PosesSent(p.Pm)); // and the pose again a second in
        Assert.Equal("Move to get up.", TutorialWake.MoveHintLine);
        TutorialWake.Advance(p.Pm, 30);
        Assert.True(TutorialWake.IsLying(p.Pm)); // still down, half a minute on
        Assert.False(p.Pm.Frozen);
        Assert.Equal(3, TutorialWake.PosesSent(p.Pm)); // every HoldEverySeconds

        // The first move request (PlayerMobile.Move) gets them up and is spent on it.
        var at = p.Pm.Location;
        Assert.False(p.Pm.Move(Direction.North));
        Assert.Equal(at, p.Pm.Location);
        Assert.False(TutorialWake.IsLying(p.Pm));
        Assert.False(p.Pm.Frozen);
        Assert.Equal(TutorialWake.DawnLight[0], TutorialWake.LightOf(p.Pm)); // cc-P732: the dawn after standing
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialWake.DawnLight.Length + 1));
        Assert.Null(TutorialWake.LightOf(p.Pm)); // then the isle's own light
        Assert.False(TutorialWake.OnMoveRequest(p.Pm)); // the next one is an ordinary move

        // The safety: up after a minute anyway.
        using var q = new Player();
        TutorialTestStart.Begin(q.Pm);
        TutorialWake.Advance(q.Pm, TutorialWake.SafetySeconds - 1);
        Assert.True(TutorialWake.IsLying(q.Pm));
        TutorialWake.Advance(q.Pm, 1);
        Assert.False(TutorialWake.IsLying(q.Pm));
        Assert.False(q.Pm.Frozen);

        // A disconnect while down ends it at once.
        using var r = new Player();
        TutorialTestStart.Begin(r.Pm);
        Assert.True(TutorialWake.IsLying(r.Pm));
        EventSink.InvokeDisconnected(r.Pm);
        Assert.False(TutorialWake.IsLying(r.Pm));
        Assert.False(r.Pm.Frozen);
    }

    // cc-P732 (P731 in TazUO): nothing freezes the trainee, so the real client sends their first step or turn; a turn
    // alone gets them up and is refused (they stay on their tile, facing as they were). The lying pose is the death
    // animation backward from the ground at the longest delay (TazUO holds frames 4 and 3, both lying), sent again every
    // HoldEverySeconds, and the dark lasts until they stand. Notes: shard-migration notes/cc-P732-isle-wake-pose.md.
    [Fact]
    public void ATurnGetsThemUpAndThePoseAndTheDarkHoldUntilThen()
    {
        Assert.Equal(5, TutorialWake.HoldFrames);       // TazUO draws frame 5 first: on the ground, no standing frame
        Assert.Equal(255, TutorialWake.HoldDelay);      // 80 ms x 256 = 20.5 s a frame
        Assert.True(TutorialWake.HoldEverySeconds * 1000 < 80 * (TutorialWake.HoldDelay + 1) * 2); // again before frame 2

        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWake.IsLying(p.Pm));
        Assert.False(p.Pm.Frozen);
        TutorialWake.Advance(p.Pm, TutorialWake.SafetySeconds - 1);
        Assert.True(TutorialWake.IsLying(p.Pm));
        Assert.True(TutorialWake.LightOf(p.Pm) >= TutorialWake.DarkLight);
        Assert.Equal(1 + (TutorialWake.SafetySeconds - 1 + TutorialWake.HoldEverySeconds - 1) / TutorialWake.HoldEverySeconds, TutorialWake.PosesSent(p.Pm));

        var at = p.Pm.Location;
        var facing = p.Pm.Direction & Direction.Mask;
        var turn = (Direction)(((int)facing + 2) & 7);
        Assert.False(p.Pm.Move(turn));
        Assert.False(TutorialWake.IsLying(p.Pm));
        Assert.Equal(at, p.Pm.Location);
        Assert.Equal(facing, p.Pm.Direction & Direction.Mask);
        Assert.Equal(-1, TutorialWake.PosesSent(p.Pm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheCloakComesInAPersonalChestThatAppears()
    {
        using var p = new Player();
        using var other = new Player();
        Awake(p);
        PlayCoreUntil(p.Pm, "core.paperdoll");
        var station = TutorialStations.Find("core.paperdoll")!;
        ClusterFTutorial.RunStation(p.Pm, station);

        var map = TutorialWorld.MapOf(p.Pm)!;
        var chest = TutorialCloakChest.Find(p.Pm, map);
        Assert.NotNull(chest);
        Assert.Equal((TutorialCorePlacement.ClothesTable.X, TutorialCorePlacement.ClothesTable.Y), (chest!.X, chest.Y));
        Assert.Same(p.Pm, chest.Owner);
        var stays = LRReason.Inspecific;
        Assert.False(chest.CheckLift(p.Pm, chest, ref stays)); // nobody carries the chest off
        var cloak = chest.FindItemByType<WakeningIsleCloak>();
        Assert.NotNull(cloak);
        Assert.Same(p.Pm, cloak!.Owner);
        Assert.Null(WakeningIsleCloak.OnTable(p.Pm, map)); // nothing lies loose on the table
        Assert.Contains("chest", TutorialCoreTrials.Hint("core.paperdoll"));
        Assert.Contains("Double-click the chest", TutorialCoreTrials.Hint("core.paperdoll"));

        // Personal: another trainee cannot open it or take from it, and nothing goes in.
        Assert.False(chest.IsFor(other.Pm));
        var reject = LRReason.Inspecific;
        Assert.False(chest.CheckLift(other.Pm, cloak, ref reject));
        var gold = p.Add(new Gold(5));
        Assert.False(chest.OnDragDrop(p.Pm, gold));
        Assert.False(chest.OnDragDropInto(p.Pm, gold, Point3D.Zero));

        // A restart while it waits puts out no second chest.
        TutorialCoreTrials.Reset(p.Pm, station);
        Assert.Same(chest, TutorialCloakChest.Find(p.Pm, map));

        // Taken into the backpack (pinned's own lift, as a client's drag): the chest vanishes.
        Assert.False(chest.CheckTaken());
        p.Pm.MoveToWorld(new Point3D(chest.X - 1, chest.Y, chest.Z - TutorialCorePlacement.TableHeight), map); // beside the table
        p.Pm.NextActionTime = 0;
        p.Pm.Lift(cloak, 1, out var refused, out var why);
        Assert.False(refused, $"the lift was refused: {why}");
        p.Pm.Holding = null;
        p.Pm.Backpack!.DropItem(cloak);
        Assert.True(chest.CheckTaken());
        Assert.True(chest.Deleted);
        Assert.Null(TutorialCloakChest.EnsureFor(p.Pm)); // they have theirs: no chest

        // Lost: Hint and arrow brings the chest back with a new cloak.
        cloak.Delete();
        new TutorialTrialGump(station, false).OnResponse(p.Ns, new RelayInfo(1, default, default, default, default));
        chest = TutorialCloakChest.Find(p.Pm, map);
        Assert.NotNull(chest);
        cloak = chest!.FindItemByType<WakeningIsleCloak>();
        Assert.NotNull(cloak);

        // Worn (straight from the chest to the paperdoll): the lesson is done, and the chest goes.
        cloak!.Layer = Layer.Cloak; // the host has no tiledata
        Assert.True(p.Pm.EquipItem(cloak));
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.paperdoll", p.Record.StationsDone);
        Assert.True(chest.CheckTaken());
        Assert.True(chest.Deleted);
    }

    // ---------------------------------------------------------------- 4 and 5

    [Fact]
    public void TheFarmFightStartsWithThePitchforkThenWarModeThenThePests()
    {
        using var p = new Player();
        Awake(p);
        PlayCoreUntil(p.Pm, "core.farm");
        Assert.Equal(1, TutorialFarm.StepOf(p.Pm));
        Assert.Null(TutorialFarm.ChoosePet(p.Pm, "dog")); // cc-P726: no pet before the pests

        // The pitchfork at once, with Wren's line on holding it.
        Assert.Equal(TutorialFarm.PitchforkLine, TutorialFarm.BeginCombat(p.Pm));
        Assert.NotNull(p.Pm.Backpack!.FindItemByType<TutorialPitchfork>());
        Assert.Contains("double-click it", TutorialFarm.PitchforkLine);
        Assert.False(TutorialFarm.PestPartOn(p.Pm));

        Hold(p.Pm);
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        Assert.Contains(TutorialFarm.WeaponKey, p.Record.StationsDone);
        Assert.Equal(TutorialFarm.WeaponDoneLine, TutorialFarm.BeginCombat(p.Pm)); // war mode next
        Assert.False(TutorialFarm.PestPartOn(p.Pm));

        // War mode: Wren opens the gate on the pests (cc-P726: the commands come with the pet, after them).
        p.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.Contains(TutorialFarm.WarKey, p.Record.StationsDone);
        Assert.True(TutorialFarm.PestPartOn(p.Pm));
        Assert.DoesNotContain("all follow", TutorialFarm.WarDoneLine);

        // A record from the old orders (the pet and its commands first): armed and in war mode, straight on to the pests,
        // and done once they are cleared.
        using var q = new Player();
        Awake(q);
        PlayCoreUntil(q.Pm, "core.farm");
        q.Record.PetClaimed = true;
        q.Record.StationsDone.UnionWith([TutorialFarm.FollowKey, TutorialFarm.GuardKey]);
        TutorialFarm.BeginCombat(q.Pm);
        Hold(q.Pm);
        Assert.False(TutorialFarm.CheckCommands(q.Pm));
        q.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(q.Pm));
        Assert.True(TutorialFarm.PestPartOn(q.Pm));
        TutorialFarm.NoteStruck(q.Pm);
        for (var i = 0; i < TutorialFarm.PestsToClear; i++) { TutorialFarm.CountPest(q.Pm, false); }
        Assert.Equal(4, TutorialFarm.StepOf(q.Pm));
        Assert.Contains("core.farm", q.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheClearedRowsAndEveryFinishedLessonSayTheNextStep()
    {
        using var p = new Player();
        Awake(p);
        PlayCoreUntil(p.Pm, "core.farm");
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.farm")!);
        var map = TutorialWorld.MapOf(p.Pm)!;
        var wren = p.Add(new TutorialFarmhand());
        wren.MoveToWorld(TutorialWorld.Ground(map, TutorialFarm.FarmhandSpot), map);
        p.Record.StationsDone.UnionWith([TutorialFarm.WeaponKey, TutorialFarm.WarKey]); // cc-P726: armed, no pet yet
        Assert.True(TutorialFarm.PestPartOn(p.Pm));

        // The count reached without a strike of their own: Wren says what is missing, never "0 more".
        for (var i = 0; i < TutorialFarm.PestsToClear; i++) { TutorialFarm.CountPest(p.Pm, true); }
        Assert.DoesNotContain(TutorialFarm.PestsKey, p.Record.StationsDone);
        Assert.Equal(TutorialFarm.StrikeLine, TutorialFarm.PestsNow(p.Pm));
        Assert.DoesNotContain("0 more", TutorialFarm.PestsNow(p.Pm));

        // The lesson window closed, the last pest falls: the next step everywhere, the window back, the arrow to Wren.
        p.Pm.CloseGump<TutorialTrialGump>();
        p.Pm.QuestArrow?.Stop();
        p.Pm.QuestArrow = null;
        TutorialFarm.NoteStruck(p.Pm);
        TutorialFarm.CountPest(p.Pm, true);
        Assert.Contains(TutorialFarm.PestsKey, p.Record.StationsDone);
        Assert.StartsWith("The cabbage rows are clear!", TutorialFarm.EchoPestsLine);
        Assert.Contains("Wren", TutorialFarm.EchoPestsLine);
        var gump = p.Pm.FindGump<TutorialTrialGump>();
        Assert.NotNull(gump);
        Assert.Equal(TutorialFarm.EchoPestsLine, gump!.Now);
        Assert.NotNull(p.Pm.QuestArrow);

        // [lesson opens it again, with the step; Wren's next answer spends it.
        p.Pm.CloseGump<TutorialTrialGump>();
        Assert.True(TutorialCoreTrials.Reopen(p.Pm));
        Assert.Equal(TutorialFarm.EchoPestsLine, p.Pm.FindGump<TutorialTrialGump>()!.Now);
        Assert.Equal(2, TutorialFarm.Talk(p.Pm, wren)); // cc-P726: the pet, as thanks
        Assert.True(TutorialCoreTrials.Reopen(p.Pm));
        Assert.Null(p.Pm.FindGump<TutorialTrialGump>()!.Now);

        // Any lesson's end: the next named, the arrow on its signpost, and how to get the window back.
        using var q = new Player();
        Awake(q);
        ClusterFTutorial.RunStation(q.Pm, TutorialStations.Find("core.movement")!);
        q.Pm.QuestArrow?.Stop();
        q.Pm.QuestArrow = null;
        TutorialCoreTrials.Observe(q.Pm, TutorialAction.FirstMark);
        TutorialCoreTrials.Observe(q.Pm, TutorialAction.SecondMark);
        Assert.Contains("core.movement", q.Record.StationsDone);
        Assert.NotNull(q.Pm.QuestArrow);
        Assert.Contains("[lesson", TutorialTrialGump.FinishedText);
        Assert.Contains("[lesson", TutorialCoreTrials.ReopenLine);
        Assert.True(TutorialCoreTrials.Reopen(q.Pm)); // the next lesson's window
        Assert.NotNull(q.Pm.FindGump<TutorialTrialGump>());
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheIsleSeedAtWorldLoadPlacesOnlyWhatIsMissing()
    {
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialCorePlacement.IsTestShard = () => true; // copies are seeded as the test shard seeds them
        TutorialFarm.IsTestShard = () => true;
        TutorialTestStart.FreeAllCopies();
        using var p = new Player();
        Awake(p);
        var map = TutorialWorld.MapOf(p.Pm)!;
        var slot = TutorialCopies.SlotOf(map);
        Assert.True(slot.Seeded);

        // A copy seeded before P721: no Pip.
        var pip = TutorialFarm.FindPip(map);
        Assert.NotNull(pip);
        slot.Placed.Remove(pip!.Serial);
        pip.Delete();
        Assert.Null(TutorialFarm.FindPip(map));

        // The seed also places the Fold's isle and New Haven's Rook; other facts expect those unseeded, so they go after.
        var before = new HashSet<Serial>(World.Items.Values.Select(i => i.Serial).Concat(World.Mobiles.Values.Select(m => m.Serial)));
        try
        {
            // The tutorial off: nothing.
            ClusterFTutorial.Enabled = false;
            Assert.Contains("skipped", TutorialSeeder.SeedMissing()[0]);
            Assert.Null(TutorialFarm.FindPip(map));
            ClusterFTutorial.Enabled = true;

            // On: Pip is back, and the copy's reset will take him.
            var lines = TutorialSeeder.SeedMissing();
            pip = TutorialFarm.FindPip(map);
            Assert.NotNull(pip);
            Assert.Contains(pip!.Serial, slot.Placed);
            Assert.Contains(lines, l => l.Contains($"copy {slot.N}: placed"));

            // The next load: nothing more.
            var count = slot.Placed.Count;
            lines = TutorialSeeder.SeedMissing();
            Assert.Equal(count, slot.Placed.Count);
            Assert.Contains(lines, l => l.Contains("nothing missing"));
        }
        finally
        {
            foreach (var item in World.Items.Values.Where(i => !before.Contains(i.Serial) && i.Parent == null && i.Map != map).ToList())
            {
                item.Delete();
            }

            foreach (var m in World.Mobiles.Values.Where(m => !before.Contains(m.Serial) && !m.Player && m.Map != map).ToList())
            {
                m.Delete();
            }
        }
    }
}
