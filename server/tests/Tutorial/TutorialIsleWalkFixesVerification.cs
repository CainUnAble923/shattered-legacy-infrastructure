// cc-P718: fixes from Chase's walk of the Wakening Isle (round 1). Notes: shard-migration
// notes/cc-P718-isle-walkthrough-fixes-1.md.
//
// Facts, each seen failing first:
//   1. Tamsin greets a new trainee who comes near, no faster than the interval, and stops once they used her; Wren greets
//      a trainee whose next lesson is the farm until the pet is chosen.
//   2 and 5. The cloak waits on the clothes table for its trainee alone, one per character (a lost one comes back on the
//      table, a restart makes no second one); wearing it finishes the lesson; it is blessed, cannot be dropped, banked or
//      sold, and nothing takes it away at the skip or after leaving the isle.
//   3. The client tips are offered in Tamsin's window before the core is done, finish with Done, and are not core.
//   4. The backpack lesson waits for the character window (a real PaperdollRequest) before the bag counts.
//   6. The chat signpost is a standing post with its board, named, and the lesson's arrow points at it; the seeder swaps
//      the old one; the farm's gate sign is the standing post, and mending an old farm keeps the farm.
//   7. A pest never fights a trainee before the pest part (nor their pet), never steps outside the field, and the field
//      gate opens only for a trainee on that part.
//   8. On the isle each skill gains at most once per interval; off the isle the shard's rate is untouched.
//   9. Wren's heal step hands bandages and the lines say how to use them and how to make more.
//  10. The pet commands: Wren sets the pet to stay; follow, then guard (by any means that sets the order) open the pests;
//      one pest must fall to the pet on command. cc-P728: after the pitchfork in hand and war mode, which come first.
// cc-P728: the cloak of fact 2 and 5 now comes in a chest (TutorialIsleWalkFixes4Verification); this fact takes it from
// there and keeps the rest.

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
using Server.Random;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIsleWalkFixesVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleWalkFixesVerification()
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
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
    }

    public void Dispose()
    {
        ClusterFLootBoxes.LoopProvider = _loopProvider;
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p718{Guid.NewGuid():N}"[..16], "p718-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Walker", RawStr = 60, RawDex = 60, RawInt = 60 };
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
        "core.farm" => [TutorialAction.FarmDone],
        "core.healing" => [TutorialAction.Healed],
        "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
        "core.banking" => [TutorialAction.Deposited],
        "core.travel" => [TutorialAction.UsedGate],
        "core.league" => [TutorialAction.TalkedToLeague],
        "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
        _ => throw new InvalidOperationException(key)
    };

    private static void PlayCoreUntil(PlayerMobile pm, string? stop)
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

    private static Point3D Beside(IEntity m, int dx) => new(m.X + dx, m.Y, m.Z);

    /// <summary>cc-P721: the trainee takes Wren's pitchfork in hand (the host has no tiledata: its layer is set).</summary>
    private static void Hold(PlayerMobile pm)
    {
        var fork = pm.Backpack!.FindItemByType<TutorialPitchfork>()!;
        fork.Layer = Layer.TwoHanded;
        Assert.True(pm.EquipItem(fork));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TamsinGreetsANewTraineeNoFasterThanTheIntervalAndStopsOnceUsed()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        var tamsin = p.Add(new TutorialGuide());

        // Every greeting by her to this trainee is counted, by whichever path spoke: her approach hook, her AI think
        // (which also runs while the clock advances) or the call here.
        var said = 0;
        void Count(Mobile npc, PlayerMobile pm)
        {
            if (npc == tamsin && pm == p.Pm)
            {
                said++;
            }
        }

        TutorialGreeting.Greeted += Count;
        try
        {
            Assert.True(TutorialGreeting.NeedsGuide(p.Pm));
            tamsin.MoveToWorld(Beside(p.Pm, 2), p.Pm.Map);
            TutorialGreeting.GreetAround(tamsin, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
            Assert.Equal(1, said); // on approach, once
            Assert.Contains("Double-click me", TutorialGreeting.TamsinLine);

            // Not again within the interval, however often she is asked.
            ShardTestClock.Advance(TutorialGreeting.Interval - TimeSpan.FromSeconds(1));
            TutorialGreeting.GreetAround(tamsin, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
            Assert.Equal(1, said);

            // Again once it has passed, while the trainee stays near and has not used her.
            ShardTestClock.Advance(TimeSpan.FromSeconds(2));
            TutorialGreeting.GreetAround(tamsin, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
            Assert.Equal(2, said);

            // Out of range: nothing.
            tamsin.MoveToWorld(Beside(p.Pm, TutorialGreeting.Range + 3), p.Pm.Map);
            ShardTestClock.Advance(TutorialGreeting.Interval + TimeSpan.FromSeconds(1));
            TutorialGreeting.GreetAround(tamsin, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
            Assert.Equal(2, said);

            // Used: the prompt stops.
            tamsin.MoveToWorld(Beside(p.Pm, 1), p.Pm.Map);
            tamsin.OnDoubleClick(p.Pm);
            Assert.False(TutorialGreeting.NeedsGuide(p.Pm));
            var before = said;
            ShardTestClock.Advance(TutorialGreeting.Interval + TimeSpan.FromSeconds(1));
            TutorialGreeting.GreetAround(tamsin, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
            Assert.Equal(before, said);
        }
        finally
        {
            TutorialGreeting.Greeted -= Count;
        }
    }

    [Fact]
    public void WrenGreetsATraineeWhoseNextLessonIsTheFarmUntilThePetIsChosen()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        Assert.False(TutorialGreeting.NeedsWren(p.Pm)); // the farm is not next yet
        PlayCoreUntil(p.Pm, "core.farm");
        var wren = p.Add(new TutorialFarmhand());
        wren.MoveToWorld(Beside(p.Pm, 2), p.Pm.Map);
        TutorialGreeting.Forget(wren, p.Pm);

        Assert.True(TutorialGreeting.NeedsWren(p.Pm));
        Assert.Equal(1, TutorialGreeting.GreetAround(wren, TutorialGreeting.NeedsWren, TutorialGreeting.WrenLine));
        Assert.Equal(0, TutorialGreeting.GreetAround(wren, TutorialGreeting.NeedsWren, TutorialGreeting.WrenLine));

        Assert.NotNull(TutorialFarm.GivePitchfork(p.Pm)); // cc-P726: Wren has started the trainee on the fight
        ShardTestClock.Advance(TutorialGreeting.Interval + TimeSpan.FromSeconds(1));
        Assert.False(TutorialGreeting.NeedsWren(p.Pm));
        Assert.Equal(0, TutorialGreeting.GreetAround(wren, TutorialGreeting.NeedsWren, TutorialGreeting.WrenLine));
    }

    // ---------------------------------------------------------------- 2 and 5

    [Fact]
    public void TheCloakWaitsOnTheTableIsWornToFinishAndIsKept()
    {
        using var p = new Player();
        using var other = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.paperdoll");
        Assert.Null(p.Pm.Backpack!.FindItemByType<Cloak>()); // nothing in the bag
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.paperdoll")!);

        var map = TutorialWorld.MapOf(p.Pm);
        var cloak = TutorialCloakChest.Find(p.Pm, map)?.FindItemByType<WakeningIsleCloak>(); // cc-P728: in its chest
        Assert.NotNull(cloak);
        Assert.Same(p.Pm, cloak!.Owner);
        Assert.Equal(WakeningIsleCloak.CloakName, cloak.Name);
        Assert.Equal(LootType.Blessed, cloak.LootType);
        Assert.True(cloak.Nontransferable); // no sale (GenericSellInfo.IsSellable), trade, bank or drop
        Assert.Equal(TutorialCorePlacement.Spot("core.paperdoll"), TutorialWorld.Ground(TutorialCorePlacement.ClothesTable));
        Assert.Contains("drag", TutorialCoreTrials.Hint("core.paperdoll"));

        // Another trainee cannot take it.
        var reject = LRReason.Inspecific;
        Assert.False(TutorialCloakChest.Find(p.Pm, map)!.CheckLift(other.Pm, cloak, ref reject));

        // A restart puts out no second one; a lost one comes back with Hint and arrow.
        TutorialCoreTrials.Reset(p.Pm, TutorialStations.Find("core.paperdoll")!);
        Assert.Same(cloak, TutorialCloakChest.Find(p.Pm, map)?.FindItemByType<WakeningIsleCloak>());
        cloak.Delete();
        new TutorialTrialGump(TutorialStations.Find("core.paperdoll")!, false).OnResponse(p.Ns, new RelayInfo(1, default, default, default, default));
        cloak = TutorialCloakChest.Find(p.Pm, map)?.FindItemByType<WakeningIsleCloak>();
        Assert.NotNull(cloak);

        // Into the backpack: it cannot be dropped back on the ground.
        p.Pm.Backpack.DropItem(cloak!);
        Assert.False(cloak!.OnDroppedToWorld(p.Pm, p.Pm.Location));
        Assert.Null(TutorialCloakChest.EnsureFor(p.Pm)); // they have theirs: no other appears

        // Worn: the lesson is done and the cloak stays on them.
        cloak.Layer = Layer.Cloak; // the host has no tiledata
        Assert.True(p.Pm.EquipItem(cloak));
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.paperdoll", p.Record.StationsDone);
        Assert.False(cloak.Deleted);
        Assert.Same(p.Pm, cloak.Parent);

        // Skipping (which also leaves the isle) takes nothing.
        Assert.True(ClusterFTutorial.Skip(p.Pm));
        Assert.False(TutorialWorld.IsOnIsle(p.Pm));
        Assert.False(cloak.Deleted);
        Assert.Same(cloak, WakeningIsleCloak.On(p.Pm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheClientTipsAreOfferedAtAnyTimeAndAreNotCore()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        var tips = TutorialStations.Find(TutorialClientTips.Key)!;
        Assert.False(tips.IsCore);
        Assert.Contains(TutorialClientTips.Tips, t => t.Text.Contains("Always run") && t.Text.Contains("Movement"));

        var page = new TutorialRecruiterGump(p.Pm, guide: true);
        var button = TutorialRecruiterGump.BtnStationBase + Array.IndexOf(TutorialStations.All, tips);
        Assert.Contains(button, page.Entries.OfType<GumpButton>().Select(b => b.ButtonID));

        page.OnResponse(p.Ns, new RelayInfo(button, default, default, default, default));
        Assert.True(p.Pm.HasGump<TutorialClientTipsGump>());
        new TutorialClientTipsGump(false).OnResponse(p.Ns, new RelayInfo(TutorialClientTipsGump.BtnDone, default, default, default, default));
        Assert.Contains(TutorialClientTips.Key, p.Record.StationsDone);
        Assert.False(ClusterFTutorial.CoreDone(p.Record));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheBackpackLessonWaitsForTheCharacterWindow()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.backpack");
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.backpack")!);
        Assert.Contains("double-click your own character", TutorialCoreTrials.Hint("core.backpack"));
        var bag = (TutorialPracticeBag)TutorialCoreTrials.Current(p.Pm)!.Supply!;

        bag.OnDoubleClick(p.Pm);
        Assert.DoesNotContain("core.backpack", p.Record.StationsDone);
        p.Pm.DisplayPaperdollTo(p.Pm); // a double-click on themselves, or the paperdoll button
        Assert.DoesNotContain("core.backpack", p.Record.StationsDone);
        bag.OnDoubleClick(p.Pm);
        Assert.Contains("core.backpack", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheSignpostsStandAndAreNamedAndTheArrowPointsAtTheChatPost()
    {
        Assert.Contains(TutorialCorePlacement.Props,
            r => r.Id == TutorialCorePlacement.SignpostId && r.At == TutorialCorePlacement.ChatSignpost && r.Name.Contains("I am ready to learn"));
        Assert.DoesNotContain(TutorialCorePlacement.Props, r => r.Id == TutorialCorePlacement.OldSignpostId);
        Assert.Equal(TutorialWorld.Ground(TutorialCorePlacement.ChatSignpost), TutorialCorePlacement.Spot("core.chat"));

        // The seeder removes the old wall-arm post (then places the standing one from Props).
        var isle = TutorialWorld.Isle;
        var old = new Static(TutorialCorePlacement.OldSignpostId) { Name = TutorialCorePlacement.OldChatSignpostName };
        var other = new Static(TutorialCorePlacement.OldSignpostId) { Name = "someone else's post" };
        old.MoveToWorld(TutorialWorld.Ground(TutorialCorePlacement.ChatSignpost), isle);
        other.MoveToWorld(TutorialWorld.Ground(TutorialCorePlacement.ChatSignpost), isle);
        try
        {
            Assert.Equal(1, TutorialCorePlacement.RemoveOldSignpost(isle));
            Assert.True(old.Deleted);
            Assert.False(other.Deleted);
        }
        finally
        {
            old.Delete();
            other.Delete();
        }

        // The farm gate's sign: a standing post, named; no wall arm, no hung board.
        var farm = new TutorialIsleFarmAddon();
        try
        {
            Assert.Contains(farm.Components, c => c.ItemID == TutorialFarm.GateSignpost && c.Name == TutorialFarm.GateSignpostName);
            Assert.DoesNotContain(farm.Components, c => c.ItemID is TutorialFarm.OldSignArm or TutorialFarm.OldSignBoard);

            // An old farm is mended in place, and stays.
            var sign = farm.Components.First(c => c.ItemID == TutorialFarm.GateSignpost);
            sign.ItemID = TutorialFarm.OldSignArm;
            var board = new AddonComponent(TutorialFarm.OldSignBoard);
            farm.AddComponent(board, sign.Offset.X, sign.Offset.Y, sign.Offset.Z);
            Assert.Equal(2, TutorialFarm.FixGateSign(farm));
            Assert.False(farm.Deleted);
            Assert.True(board.Deleted);
            Assert.Equal(TutorialFarm.GateSignpost, sign.ItemID);
        }
        finally
        {
            farm.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void APestFightsOnlyATraineeOnThePestPartAndStaysInTheField()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.farm");
        var rat = p.Add(new FarmRat());
        rat.MoveToWorld(new Point3D(TutorialFarm.PestSpot, 0), p.Pm.Map);

        // Step 1 before the pitchfork and war mode (cc-P726: no pet yet): never an enemy, and a blow does nothing.
        Assert.Equal(1, TutorialFarm.StepOf(p.Pm));
        Assert.False(rat.IsEnemy(p.Pm));
        Assert.False(rat.CanBeHarmful(p.Pm, false, false));
        rat.Damage(5, p.Pm);
        Assert.Equal(rat.HitsMax, rat.Hits);
        TutorialFarm.BeginCombat(p.Pm); // Wren hands the pitchfork

        // The gate opens only for a trainee on the pest part.
        var gates = TutorialFarm.MakeFieldGates();
        for (var i = 0; i < gates.Length; i++)
        {
            p.Add(gates[i]).MoveToWorld(TutorialWorld.Ground(p.Pm.Map, TutorialFarm.FieldGateSpots[i]), p.Pm.Map);
        }

        p.Pm.MoveToWorld(Beside(gates[0], 1), p.Pm.Map);
        gates[0].Use(p.Pm);
        Assert.False(gates[0].Open);

        // The pitchfork in hand and war mode: the pests fight, and Wren opened the gate.
        Hold(p.Pm);
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        p.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.True(TutorialFarm.PestPartOn(p.Pm));
        Assert.True(rat.IsEnemy(p.Pm));
        Assert.All(gates, g => Assert.True(g.Open));

        // A pest never steps outside the field fence.
        Assert.Equal(new Rectangle2D(6678, 3968, 17, 15), TutorialFarm.PestField); // cc-P741: 18 west of P720's
        rat.MoveToWorld(new Point3D(TutorialFarm.PestField.End.X - 1, TutorialFarm.PestSpot.Y, 0), p.Pm.Map);
        Assert.False(rat.CheckMovement(Direction.East, out _));
    }

    // ---------------------------------------------------------------- 8

    private sealed class AlwaysGain : System.Random
    {
        protected override double Sample() => 0.0;
        public override double NextDouble() => 0.0;
    }

    [Fact]
    public void OnTheIsleEachSkillGainsAtMostOncePerInterval()
    {
        var location = Mobile.SkillCheckLocationHandler;
        var directLocation = Mobile.SkillCheckDirectLocationHandler;
        var target = Mobile.SkillCheckTargetHandler;
        var directTarget = Mobile.SkillCheckDirectTargetHandler;
        using var p = new Player();
        try
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            BuiltInRng.Generator = new AlwaysGain();
            var tactics = p.Pm.Skills.Tactics;
            tactics.Cap = 200.0;

            // Off the isle: the shard's rate, a gain on every swing here.
            tactics.Base = 11.6;
            for (var i = 0; i < 5; i++) { p.Pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap); }
            Assert.True(tactics.Base >= 12.0, $"off the isle: {tactics.Base}");

            // On the isle: one gain, then none until the interval has passed.
            TutorialTestStart.Begin(p.Pm);
            Assert.True(TutorialWorld.IsOnIsle(p.Pm));
            TutorialSkillPace.Forget(p.Pm);
            tactics.Base = 11.6;
            for (var i = 0; i < 20; i++) { p.Pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap); }
            Assert.Equal(11.7, tactics.Base, 1);
            ShardTestClock.Advance(TutorialSkillPace.Interval - TimeSpan.FromSeconds(1));
            p.Pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap);
            Assert.Equal(11.7, tactics.Base, 1);
            ShardTestClock.Advance(TimeSpan.FromSeconds(2));
            for (var i = 0; i < 20; i++) { p.Pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap); }
            Assert.Equal(11.8, tactics.Base, 1);

            // Each skill has its own pace.
            p.Pm.Skills.Anatomy.Cap = 200.0;
            p.Pm.Skills.Anatomy.Base = 11.6;
            p.Pm.CheckSkill(SkillName.Anatomy, 0.0, 200.0);
            Assert.Equal(11.7, p.Pm.Skills.Anatomy.Base, 1);
        }
        finally
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = location;
            Mobile.SkillCheckDirectLocationHandler = directLocation;
            Mobile.SkillCheckTargetHandler = target;
            Mobile.SkillCheckDirectTargetHandler = directTarget;
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void WrensHealStepHandsBandagesAndSaysHowToUseAndMakeThem()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.farm");
        var wren = p.Add(new TutorialFarmhand());
        wren.MoveToWorld(Beside(p.Pm, 1), p.Pm.Map);
        p.Record.PetClaimed = true;
        p.Record.StationsDone.UnionWith([TutorialFarm.FollowKey, TutorialFarm.GuardKey, TutorialFarm.WeaponKey, TutorialFarm.WarKey, TutorialFarm.PestsKey]);
        Assert.Equal(4, TutorialFarm.Talk(p.Pm, wren)); // cc-P726: the farm is done; bandages are the next lesson's
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialFarm.HealingStationKey)!);
        Assert.NotNull(p.Pm.Backpack!.FindItemByType<Bandage>());
        Assert.Contains("double-click a bandage", TutorialFarm.HealLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scissors", TutorialFarm.MakeBandagesLine);
        Assert.Contains("cloth", TutorialFarm.MakeBandagesLine);
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void ThePetCommandsFollowThePetGivenAfterThePests()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.farm");
        // cc-P726: the pests first (the pitchfork, war mode, one struck by the trainee; no pet's kill needed), then the pet.
        TutorialFarm.BeginCombat(p.Pm);
        Hold(p.Pm);
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        p.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.True(TutorialFarm.PestPartOn(p.Pm));
        for (var i = 0; i < TutorialFarm.PestsToClear; i++) { TutorialFarm.CountPest(p.Pm, false); }
        Assert.DoesNotContain(TutorialFarm.PestsKey, p.Record.StationsDone);
        TutorialFarm.NoteStruck(p.Pm); // cc-P721: one struck by the trainee
        TutorialFarm.CountPest(p.Pm, false);
        Assert.Contains(TutorialFarm.PestsKey, p.Record.StationsDone);
        Assert.Equal(2, TutorialFarm.StepOf(p.Pm));

        var pet = TutorialFarm.ChoosePet(p.Pm, "cat")!;
        Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
        Assert.False(TutorialFarm.CommandsDone(p.Pm));
        Assert.Equal(OrderType.Stay, pet.ControlOrder); // Wren told it to stay

        // Guard before follow does not count; follow (speech or the pet's menu: both set the order), then guard.
        pet.ControlOrder = OrderType.Guard;
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        Assert.DoesNotContain(TutorialFarm.GuardKey, p.Record.StationsDone);
        pet.ControlTarget = p.Pm;
        pet.ControlOrder = OrderType.Follow;
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        Assert.Contains(TutorialFarm.FollowKey, p.Record.StationsDone);
        pet.ControlOrder = OrderType.Guard;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.Equal(4, TutorialFarm.StepOf(p.Pm)); // the farm is done
        Assert.Contains("core.farm", p.Record.StationsDone);
        Assert.Contains("all follow me", TutorialFarm.CommandsLine);
        Assert.Contains("all guard me", TutorialFarm.CommandsLine);
        Assert.Contains("menu", TutorialFarm.CommandsLine);
    }
}
