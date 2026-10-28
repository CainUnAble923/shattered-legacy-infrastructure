// cc-P721: the isle's bandage wait, dying on the isle, the gain pace for meditation, the vision's strike and pace, waking
// on the beach, and the weapon and war mode before the first fight. Notes: shard-migration notes/cc-P721-isle-bandage-wait.md.
//
// Facts, each seen failing first:
//   1. On the isle a trainee's own bandage takes 4 s (pinned's 9.3 s at Dex 35); off the isle, or on someone else, pinned's
//      delay is kept. The lines say how long to wait and what a failed try means.
//   2. Pip stays hurt (his hits never come back by themselves), a heal on him finishes the step and he is cut again a
//      little later; a failed try leaves the step open and him hurt; an unhurt trainee is sent to him, with no scratch.
//   3. Dying on the isle: the full words and an arrow to Sera the first time, a reminder after, nothing in the death
//      lesson; alive again with things on the body, an arrow back to it.
//   4. The isle's gain pace covers Focus and Meditation too.
//   5. The vision: lit from its second beat with braziers, the Stranger walks up and faces the Gem to strike, 61 s long.
//   6. The first arrival lies on the sand (frozen, dark) and gets up after a few seconds with the arrow to Tamsin; a later
//      trip to the isle does not.
//   7. Wren hands one kept pitchfork after the commands; the pests wait for it in hand and war mode, and for one pest
//      struck by the trainee.

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
public class TutorialIsleBandageWaitVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;

    public TutorialIsleBandageWaitVerification()
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
        TutorialTestStart.ReleaseIdleCopies();
    }

    public void Dispose() => ClusterFLootBoxes.LoopProvider = _loopProvider;

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p721{Guid.NewGuid():N}"[..16], "p721-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Washed", RawStr = 45, RawDex = 35, RawInt = 10 };
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
        "core.farm" => [TutorialAction.FarmDone],
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

    /// <summary>Into the tutorial on the isle, awake (the wake-up run to its end).</summary>
    private static void Awake(Player p)
    {
        TutorialTestStart.Begin(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialWake.Seconds + 1));
        Assert.False(TutorialWake.IsLying(p.Pm));
    }

    /// <summary>At the farm's heal step, with Pip beside them.</summary>
    private static TutorialWoundedHelper AtTheHealStep(Player p)
    {
        Awake(p);
        PlayCoreUntil(p.Pm, "core.farm");
        p.Record.PetClaimed = true;
        p.Record.StationsDone.UnionWith([TutorialFarm.FollowKey, TutorialFarm.GuardKey, TutorialFarm.WeaponKey, TutorialFarm.WarKey, TutorialFarm.PestsKey]);
        Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
        var pip = p.Add(new TutorialWoundedHelper());
        pip.MoveToWorld(TutorialWorld.Ground(p.Pm.Map!, TutorialFarm.PipSpot), p.Pm.Map);
        p.Pm.MoveToWorld(new Point3D(pip.X, pip.Y + 1, pip.Z), pip.Map);
        return pip;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void OnTheIsleATraineesOwnBandageIsFourSecondsAndTheLinesSayWhatToExpect()
    {
        using var p = new Player();
        using var off = new Player();
        Awake(p);
        Assert.True(TutorialFarm.IsleTrainee(p.Pm));
        var pinned = 5.0 + 0.5 * ((120 - p.Pm.Dex) / 10.0); // AOS self delay
        Assert.True(pinned > 9.0);

        Assert.Equal(TutorialFarm.IsleSelfBandageSeconds, TutorialFarm.BandageSeconds!(p.Pm, p.Pm, pinned));
        Assert.Equal(2.5, TutorialFarm.BandageSeconds!(p.Pm, off.Pm, 2.5)); // on someone else: pinned's
        Assert.Equal(pinned, TutorialFarm.BandageSeconds!(off.Pm, off.Pm, pinned)); // off the isle: pinned's

        Assert.Contains("few seconds", TutorialFarm.WaitLine);
        Assert.Contains("You begin applying the bandages", TutorialFarm.WaitLine);
        Assert.Contains("You finish applying the bandages", TutorialFarm.WaitLine);
        Assert.Contains("Try again", TutorialFarm.FumbleLine);
        Assert.NotNull(TutorialFarm.BandageBegun);
        Assert.NotNull(TutorialFarm.BandageFumbled);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void PipStaysHurtUntilBandagedAndAFailedTryLeavesTheStepOpen()
    {
        using var p = new Player();
        var pip = AtTheHealStep(p);
        // A bandage may be used on him: pinned's Trammel rule refuses an uncontrolled creature (the P721 bot run's
        // "You cannot perform beneficial acts on your target"), and the farm lets Pip, and only Pip, through.
        Assert.False(NotorietyHandlers.Mobile_AllowBeneficial(p.Pm, pip));
        Assert.True(TutorialFarm.AllowBeneficial(p.Pm, pip, NotorietyHandlers.Mobile_AllowBeneficial));
        var rat = p.Add(new FarmRat());
        rat.MoveToWorld(pip.Location, pip.Map);
        Assert.False(TutorialFarm.AllowBeneficial(p.Pm, rat, NotorietyHandlers.Mobile_AllowBeneficial));
        pip.Damage(20, p.Pm); // and nothing harms him
        Assert.False(pip.CanRegenHits);
        Assert.True(pip.Hits < pip.HitsMax);
        var hurt = pip.Hits;

        // Time passes: he does not heal by himself.
        ShardTestClock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(hurt, pip.Hits);

        // An unhurt trainee bandaging themselves is sent to Pip, with no scratch of their own.
        var hits = p.Pm.Hits;
        Assert.True(TutorialFarm.Unhurt!(p.Pm, p.Pm));
        Assert.Equal(hits, p.Pm.Hits);

        // A failed try: the step stays open, and he stays hurt.
        TutorialFarm.BandageFumbled!(p.Pm, pip);
        Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
        Assert.Equal(hurt, pip.Hits);

        // A bandage that heals him finishes the step; he is cut again a little later.
        pip.Hits = hurt + 5;
        TutorialFarm.BandageHealed!(p.Pm, pip);
        Assert.Equal(4, TutorialFarm.StepOf(p.Pm));
        ShardTestClock.Advance(TutorialWoundedHelper.WoundAgainAfter + TimeSpan.FromSeconds(1));
        Assert.Equal(Math.Max(1, pip.HitsMax / 3), pip.Hits);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void DyingOnTheIsleSaysWhatToDoTheFirstTimeAndRemindsAfter()
    {
        using var p = new Player();
        Awake(p);
        Assert.False(TutorialDeathHelp.Told(p.Pm));

        p.Pm.Kill();
        Assert.False(p.Pm.Alive);
        Assert.True(TutorialDeathHelp.Told(p.Pm)); // the full words, once
        Assert.NotNull(p.Pm.QuestArrow); // to Sera
        Assert.True(TutorialDeathHelp.Help(p.Pm)); // later: the reminder (still helps)

        // Alive again with things on the body: an arrow back to it.
        p.Pm.Resurrect();
        if (p.Pm.Corpse is Corpse body)
        {
            if (body.Items.Count == 0)
            {
                body.DropItem(new Gold(5));
            }

            Assert.True(TutorialDeathHelp.Revived(p.Pm));
            Assert.NotNull(p.Pm.QuestArrow);
        }

        // In the death lesson, the lesson's own lines; nothing here.
        PlayCoreUntil(p.Pm, "core.death");
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.death")!);
        p.Pm.Kill();
        Assert.False(TutorialDeathHelp.Help(p.Pm));
        Assert.Contains(TutorialDeathHelp.FirstLines, l => l.Contains("ghost"));
        Assert.Contains("Sera", TutorialDeathHelp.ReminderLine);
    }

    // ---------------------------------------------------------------- 4

    private sealed class AlwaysGain : System.Random
    {
        protected override double Sample() => 0.0;
        public override double NextDouble() => 0.0;
    }

    [Fact]
    public void TheIslesGainPaceCoversFocusAndMeditation()
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
            Awake(p);
            TutorialSkillPace.Forget(p.Pm);
            foreach (var name in new[] { SkillName.Focus, SkillName.Meditation })
            {
                var skill = p.Pm.Skills[name];
                skill.Cap = 200.0;
                skill.Base = 25.8;
                for (var i = 0; i < 20; i++)
                {
                    p.Pm.CheckSkill(name, 0.0, 200.0); // the passive check Focus and Meditation make, and an active one
                }

                Assert.Equal(25.9, skill.Base, 1);
            }
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

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheVisionIsLitWalksUpStrikesTheGemAndRunsLonger()
    {
        Assert.Equal(61, TutorialVisionScene.Length); // P665: 45
        using var p = new Player();
        Awake(p);
        PlayCoreUntil(p.Pm, "core.loop");
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        var stones = TutorialCorePlacement.Spot("core.loop");
        p.Pm.MoveToWorld(TutorialWorld.Ground(p.Pm.Map!, new Point2D(stones.X, stones.Y)), p.Pm.Map);
        Assert.True(p.Pm.HasGump<TutorialTrialGump>());
        TutorialCoreTrials.WatchVision(p.Pm);
        Assert.True(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.False(p.Pm.HasGump<TutorialTrialGump>()); // the lesson page no longer hides the scene
        var origin = TutorialVisionScene.Current(p.Pm)!.Origin;

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Lights + 0.5));
        Assert.Equal(TutorialVisionScene.Lit, TutorialVisionScene.LightOf(p.Pm));
        var scene = TutorialVisionScene.Current(p.Pm)!;
        Assert.Equal(2, scene.Placed.OfType<Item>().Count(i => i.ItemID == TutorialVisionScene.BrazierId));
        var stranger = scene.Stranger!;
        Assert.Equal(origin.X + 6, stranger.X); // at a distance

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Face - TutorialVisionScene.Beats.Lights));
        Assert.Equal(origin.X + 3, stranger.X); // walked up beside the Gem
        Assert.Equal(Direction.Up, stranger.Direction & Direction.Mask); // facing it

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Strike - TutorialVisionScene.Beats.Face));
        Assert.Equal("The sword strikes the Gem. The shards fly.", scene.Subtitle);
        Assert.NotNull(scene.Gem); // still whole: it breaks later

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Length - TutorialVisionScene.Beats.Strike + 1));
        Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.Contains("core.loop", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheFirstArrivalLiesOnTheSandThenGetsUp()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWorld.IsOnIsle(p.Pm));
        Assert.True(TutorialWake.IsLying(p.Pm));
        Assert.True(p.Pm.Frozen);
        Assert.True(TutorialWake.LightOf(p.Pm) > 10); // dark

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialWake.Seconds - 1));
        Assert.True(TutorialWake.IsLying(p.Pm)); // still down
        ShardTestClock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(TutorialWake.IsLying(p.Pm));
        Assert.False(p.Pm.Frozen);
        Assert.Null(TutorialWake.LightOf(p.Pm));

        // A later trip to the isle (remedial, a copy reset) does not lie them down again.
        TutorialStart.SendToIsle(p.Pm);
        Assert.False(TutorialWake.IsLying(p.Pm));
        Assert.False(p.Pm.Frozen);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void WrenHandsOneKeptPitchforkAndThePestsWaitForItInHandWarModeAndAStrike()
    {
        using var p = new Player();
        Awake(p);
        PlayCoreUntil(p.Pm, "core.farm");
        var pet = TutorialFarm.ChoosePet(p.Pm, "dog")!;
        pet.ControlTarget = p.Pm;
        pet.ControlOrder = OrderType.Follow;
        TutorialFarm.CheckCommands(p.Pm);
        pet.ControlOrder = OrderType.Guard;
        Assert.False(TutorialFarm.CheckCommands(p.Pm));

        var fork = p.Pm.Backpack!.FindItemByType<TutorialPitchfork>();
        Assert.NotNull(fork);
        Assert.Equal(LootType.Blessed, fork!.LootType);
        Assert.True(fork.Nontransferable);
        Assert.Same(p.Pm, fork.Owner);
        Assert.Null(TutorialFarm.GivePitchfork(p.Pm)); // one per character
        Assert.False(TutorialFarm.PestPartOn(p.Pm));

        fork.Layer = Layer.TwoHanded; // the host has no tiledata
        Assert.True(p.Pm.EquipItem(fork));
        Assert.False(TutorialFarm.CheckCommands(p.Pm));
        Assert.Contains(TutorialFarm.WeaponKey, p.Record.StationsDone);
        Assert.False(TutorialFarm.PestPartOn(p.Pm)); // not yet in war mode
        Assert.Contains("Tab", TutorialFarm.WeaponDoneLine);

        p.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.True(TutorialFarm.PestPartOn(p.Pm));

        // The pests: the pet's kills alone do not finish it; one struck by the trainee does.
        for (var i = 0; i < TutorialFarm.PestsToClear; i++) { TutorialFarm.CountPest(p.Pm, true); }
        Assert.DoesNotContain(TutorialFarm.PestsKey, p.Record.StationsDone);
        var rat = p.Add(new FarmRat());
        rat.MoveToWorld(new Point3D(TutorialFarm.PestSpot, 0), p.Pm.Map);
        rat.Damage(1, p.Pm);
        Assert.True(TutorialFarm.HasStruck(p.Pm));
        TutorialFarm.CountPest(p.Pm, true);
        Assert.Contains(TutorialFarm.PestsKey, p.Record.StationsDone);

        // Kept: the skip takes nothing.
        Assert.True(ClusterFTutorial.Skip(p.Pm));
        Assert.False(fork.Deleted);
    }
}
