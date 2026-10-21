// TutorialVerification.cs
//
// cc-P80: the New Haven tutorial framework. Notes in shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Facts (the brief's tests):
//   1. A new character enters the core, and the state saves (the account record round-trips) and holds across a relog.
//   2. Skip: both confirmations are needed; "Didn't Read the Manual" is granted once; remedial training completes and
//      grants "Read the Manual Eventually".
//   3. The tutorial runs once per account: a second character on the account is never entered.
//   4. The loop demo leaves level, skills, loop count and Young as they were; a real loop ends Young.
//   5. A player not in the tutorial is not engaged by tutorial creatures; the tutorial player is.
// And: every station Chase listed is registered; walking the core opens the wings; graduation gives the first loot box.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialVerification
{
    private readonly ITestOutputHelper _out;

    public TutorialVerification(ITestOutputHelper output)
    {
        ShardTestClock.Arm();
        _out = output;
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        ClusterFLootBoxes.LoopProvider = _ => 1;
    }

    private static bool _startupHooksRun;

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Account? account = null, int slot = 0)
        {
            Account = account ?? new Account($"p80{Guid.NewGuid():N}"[..16], "p80-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Newbie", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[slot] = Pm;
            Pm.MoveToWorld(new Point3D(1700, 1700 + slot, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            for (var i = 0; i < Account.Length; i++)
            {
                Account[i]?.Delete();
            }

            Accounts.Remove(Account);
        }
    }

    private static void WalkTheCore(PlayerMobile pm)
    {
        foreach (var station in TutorialStations.Core.ToList())
        {
            ClusterFTutorial.RunStation(pm, station);
            Assert.DoesNotContain(station.Key, ClusterFTutorial.RecordOf(pm)!.StationsDone);
            FeedTrial(pm, station.Key);
        }
    }

    // Fakes feed the same evidence boundary used by movement, items, speech, the healer, gate and Registrar.
    private static void FeedTrial(PlayerMobile pm, string key)
    {
        TutorialAction[] actions = key switch
        {
            "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
            "core.paperdoll" => [TutorialAction.Equipped],
            "core.backpack" => [TutorialAction.OpenedBag],
            "core.chat" => [TutorialAction.SaidPhrase],
            "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
            "core.banking" => [TutorialAction.Deposited],
            "core.travel" => [TutorialAction.UsedGate],
            "core.league" => [TutorialAction.TalkedToLeague],
            "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
            _ => throw new InvalidOperationException(key)
        };
        foreach (var action in actions) { TutorialCoreTrials.Observe(pm, action); }
        Assert.Contains(key, ClusterFTutorial.RecordOf(pm)!.StationsDone);
        Assert.Null(TutorialCoreTrials.Current(pm));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ANewCharacterEntersTheCoreAndTheStateSavesAndHoldsAcrossARelog()
    {
        using var p = new Player();
        Assert.Equal(TutorialState.NotStarted, p.Record.State);

        ClusterFTutorial.OnLogin(p.Pm);
        Assert.Equal(TutorialState.InCore, p.Record.State);
        Assert.Equal((uint)p.Pm.Serial, p.Record.CharacterSerial);
        ClusterFTutorial.CompleteStation(p.Pm, "core.movement");

        // A relog: the login hook again changes nothing.
        ClusterFTutorial.OnLogin(p.Pm);
        Assert.Equal(TutorialState.InCore, p.Record.State);
        Assert.Contains("core.movement", p.Record.StationsDone);

        // A save and a load.
        var data = ClusterFAccountPersistence.GetOrCreate(p.Account);
        var w = new BufferWriter(true);
        data.Serialize(w);
        var copy = new ClusterFAccountData(new BufferReader(w.Buffer));
        Assert.Equal(TutorialState.InCore, copy.Tutorial.State);
        Assert.Equal((uint)p.Pm.Serial, copy.Tutorial.CharacterSerial);
        Assert.Contains("core.movement", copy.Tutorial.StationsDone);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SkippingNeedsBothConfirmationsGrantsItsAchievementOnceAndRemedialGrantsTheSecond()
    {
        using var p = new Player();
        ClusterFTutorial.OnLogin(p.Pm);

        // The first confirmation alone skips nothing.
        new TutorialSkipGump(1).OnResponse(p.Ns, new RelayInfo(TutorialSkipGump.BtnYes, default, default, default, default));
        Assert.Equal(TutorialState.InCore, p.Record.State);

        // Saying no at the second skips nothing either.
        new TutorialSkipGump(2).OnResponse(p.Ns, new RelayInfo(0, default, default, default, default));
        Assert.Equal(TutorialState.InCore, p.Record.State);

        new TutorialSkipGump(2).OnResponse(p.Ns, new RelayInfo(TutorialSkipGump.BtnYes, default, default, default, default));
        Assert.Equal(TutorialState.Skipped, p.Record.State);
        Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFTutorial.SkippedAchievement));
        Assert.False(ClusterFAchievementSystem.TryGrant(p.Account, ClusterFTutorial.SkippedAchievement)); // once
        Assert.False(ClusterFTutorial.Skip(p.Pm)); // a skipped account cannot skip again

        // Remedial: the full tutorial, then the second achievement.
        Assert.True(ClusterFTutorial.StartRemedial(p.Pm));
        Assert.Equal(TutorialState.Remedial, p.Record.State);
        Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFTutorial.RemedialAchievement));
        WalkTheCore(p.Pm);
        Assert.Null(ClusterFTutorial.Graduate(p.Pm));
        Assert.Equal(TutorialState.Graduated, p.Record.State);
        Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFTutorial.RemedialAchievement));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheTutorialRunsOncePerAccount()
    {
        using var first = new Player();
        ClusterFTutorial.OnLogin(first.Pm);
        Assert.Equal(TutorialState.InCore, first.Record.State);

        using var second = new Player(first.Account, 1);
        Assert.False(ClusterFTutorial.TryEnter(second.Pm));
        Assert.Equal((uint)first.Pm.Serial, second.Record.CharacterSerial);
        Assert.False(ClusterFTutorial.IsParticipant(second.Pm));
        Assert.True(ClusterFTutorial.IsParticipant(first.Pm));

        // Graduated, nobody on the account enters again.
        WalkTheCore(first.Pm);
        Assert.Null(ClusterFTutorial.Graduate(first.Pm));
        Assert.False(ClusterFTutorial.TryEnter(first.Pm));
        Assert.False(ClusterFTutorial.TryEnter(second.Pm));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheLoopVisionChangesNothingAndARealLoopEndsYoung()
    {
        using var p = new Player();
        p.Account.Young = true;
        p.Pm.Young = true;
        ClusterFTutorial.OnLogin(p.Pm);
        p.Pm.Skills[SkillName.Swords].Base = 45.0;
        p.Pm.Skills[SkillName.Mining].Base = 30.0;

        var skillsBefore = Enum.GetValues<SkillName>().Select(s => p.Pm.Skills[s].Base).ToArray();
        var levelBefore = ClusterFLootBoxes.LevelOf(p.Pm);
        var loopBefore = ClusterFLootBoxes.CurrentLoop(p.Pm);

        foreach (var station in TutorialStations.Core.Where(s => s.Key != "core.loop"))
        {
            ClusterFTutorial.RunStation(p.Pm, station);
            FeedTrial(p.Pm, station.Key);
        }
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        FeedTrial(p.Pm, "core.loop");

        Assert.Contains("core.loop", p.Record.StationsDone);
        Assert.Equal(skillsBefore, Enum.GetValues<SkillName>().Select(s => p.Pm.Skills[s].Base).ToArray());
        Assert.Equal(levelBefore, ClusterFLootBoxes.LevelOf(p.Pm));
        Assert.Equal(loopBefore, ClusterFLootBoxes.CurrentLoop(p.Pm));
        Assert.True(p.Account.Young);
        Assert.True(p.Pm.Young);

        // A real loop ends Young for the account and its characters.
        ClusterFLoops.CompleteLoop(p.Pm, 2);
        Assert.False(p.Account.Young);
        Assert.False(p.Pm.Young);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TutorialCreaturesEngageOnlyTheTutorialPlayer()
    {
        using var newbie = new Player();
        using var veteran = new Player();
        ClusterFTutorial.OnLogin(newbie.Pm);
        veteran.Record.State = TutorialState.Graduated;

        var echo = new TutorialEcho();
        echo.MoveToWorld(new Point3D(1701, 1701, 0), Map.Trammel);
        try
        {
            Assert.True(echo.IsEnemy(newbie.Pm));
            Assert.False(echo.IsEnemy(veteran.Pm));
            Assert.False(echo.CanBeHarmful(veteran.Pm, false, false));

            var hits = echo.Hits;
            echo.Damage(5, veteran.Pm);
            Assert.Equal(hits, echo.Hits); // a veteran's blows do nothing

            echo.Damage(5, newbie.Pm);
            Assert.True(echo.Hits < hits);
            Assert.True(echo.NoKillAwards);
            Assert.True(echo.IgnoreYoungProtection);
        }
        finally
        {
            echo.Delete();
        }
    }

    [Fact]
    public void ResetClearsEvidenceAndReplacesSuppliesAndAnotherCharacterCannotUseTheRecord()
    {
        using var p = new Player();
        ClusterFTutorial.OnLogin(p.Pm);
        var movement = TutorialStations.Find("core.movement")!;
        ClusterFTutorial.RunStation(p.Pm, movement);
        TutorialCoreTrials.Observe(p.Pm, TutorialAction.FirstMark);
        var old = TutorialCoreTrials.Current(p.Pm)!;
        Assert.Equal(1, old.Progress.Step);
        TutorialCoreTrials.Reset(p.Pm, movement);
        Assert.NotSame(old, TutorialCoreTrials.Current(p.Pm));
        Assert.Equal(0, TutorialCoreTrials.Current(p.Pm)!.Progress.Step);
        Assert.False(TutorialCoreTrials.Observe(p.Pm, TutorialAction.SecondMark));
        FeedTrial(p.Pm, movement.Key);
        var paperdoll = TutorialStations.Find("core.paperdoll")!;
        ClusterFTutorial.RunStation(p.Pm, paperdoll);
        var supply = TutorialCoreTrials.Current(p.Pm)!.Supply!;
        TutorialCoreTrials.Reset(p.Pm, paperdoll);
        Assert.True(supply.Deleted);
        Assert.NotSame(supply, TutorialCoreTrials.Current(p.Pm)!.Supply);
        using var other = new Player(p.Account, 1);
        ClusterFTutorial.RunStation(other.Pm, paperdoll);
        ClusterFTutorial.CompleteStation(other.Pm, paperdoll.Key);
        Assert.DoesNotContain(paperdoll.Key, p.Record.StationsDone);
        Assert.Null(TutorialCoreTrials.Current(other.Pm));
        FeedTrial(p.Pm, paperdoll.Key);
    }

    [Fact]
    public void BagSpeechBankLeagueAndTimedVisionObserveTheRealHooksWithFakePlayers()
    {
        using var p = new Player();
        using var stranger = new Player();
        ClusterFTutorial.OnLogin(p.Pm);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.movement")!);
        p.Pm.MoveToWorld(TutorialCorePlacement.FirstMark, Map.Trammel);
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.DoesNotContain("core.movement", p.Record.StationsDone);
        p.Pm.MoveToWorld(TutorialCorePlacement.SecondMark, Map.Trammel);
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.movement", p.Record.StationsDone);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.paperdoll")!);
        // The fake host has no tiledata; give this practice item's fake its real equipment layer.
        var cloak = TutorialCoreTrials.Current(p.Pm)!.Supply!;
        cloak.Layer = Layer.Cloak;
        Assert.True(p.Pm.EquipItem(cloak));
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.paperdoll", p.Record.StationsDone);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.backpack")!);
        var bag = (TutorialPracticeBag)TutorialCoreTrials.Current(p.Pm)!.Supply!;
        var gold = new Gold();
        try { Assert.False(bag.OnDragDrop(p.Pm, gold)); }
        finally { gold.Delete(); }
        bag.OnDoubleClick(stranger.Pm);
        Assert.DoesNotContain("core.backpack", p.Record.StationsDone);
        bag.OnDoubleClick(p.Pm);
        Assert.Contains("core.backpack", p.Record.StationsDone);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.chat")!);
        EventSink.InvokeSpeech(new SpeechEventArgs(p.Pm, "wrong phrase", MessageType.Regular, 0, []));
        Assert.DoesNotContain("core.chat", p.Record.StationsDone);
        EventSink.InvokeSpeech(new SpeechEventArgs(p.Pm, TutorialCoreTrials.Phrase, MessageType.Regular, 0, []));
        Assert.Contains("core.chat", p.Record.StationsDone);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.death")!);
        var echo = new TutorialDeathEcho();
        var healer = new TutorialHealer();
        try
        {
            echo.MoveToWorld(TutorialCorePlacement.DeathSpot, Map.Trammel);
            healer.MoveToWorld(TutorialCorePlacement.HealerSpot, Map.Trammel);
            p.Pm.MoveToWorld(echo.Location, Map.Trammel);
            new TutorialDeathGump(echo).OnResponse(p.Ns, new RelayInfo(1, default, default, default, default));
            Assert.False(p.Pm.Alive);
            // The curated fixture does not register generated player events; invoke this real handler explicitly.
            TutorialCoreTrials.OnDeath(p.Pm);
            Assert.Equal(TutorialCorePlacement.GhostSpot, p.Pm.Location);
            Assert.DoesNotContain("core.death", p.Record.StationsDone);
            healer.OnDoubleClick(p.Pm);
            new TutorialResurrectionGump(healer).OnResponse(p.Ns, new RelayInfo(1, default, default, default, default));
            Assert.True(p.Pm.Alive);
            Assert.Contains("core.death", p.Record.StationsDone);
            p.Pm.Corpse?.Delete();
        }
        finally { echo.Delete(); healer.Delete(); }
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.banking")!);
        var token = TutorialCoreTrials.Current(p.Pm)!.Supply!;
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.DoesNotContain("core.banking", p.Record.StationsDone);
        p.Pm.BankBox.DropItem(token);
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.banking", p.Record.StationsDone);
        Assert.True(token.Deleted);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.travel")!);
        var gate = new TutorialTravelGate();
        var fits = TutorialCorePlacement.Fits;
        try
        {
            gate.MoveToWorld(TutorialCorePlacement.GateSpot, Map.Trammel);
            p.Pm.MoveToWorld(gate.Location, Map.Trammel);
            TutorialCorePlacement.Fits = _ => false;
            gate.OnDoubleClick(p.Pm);
            Assert.DoesNotContain("core.travel", p.Record.StationsDone);
            TutorialCorePlacement.Fits = _ => true;
            gate.OnDoubleClick(p.Pm);
            Assert.Equal(TutorialCorePlacement.GateExit, p.Pm.Location);
            Assert.Contains("core.travel", p.Record.StationsDone);
        }
        finally { gate.Delete(); TutorialCorePlacement.Fits = fits; }
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.league")!);
        var registrar = new LeagueRegistrar();
        try
        {
            registrar.MoveToWorld(TutorialCorePlacement.Spot("core.league"), Map.Trammel);
            registrar.OnDoubleClick(p.Pm);
            Assert.DoesNotContain("core.league", p.Record.StationsDone);
            p.Pm.MoveToWorld(registrar.Location, Map.Trammel);
            registrar.OnDoubleClick(p.Pm);
            Assert.Contains("core.league", p.Record.StationsDone);
        }
        finally { registrar.Delete(); }
        p.Pm.MoveToWorld(TutorialCorePlacement.Spot("core.loop"), Map.Trammel);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        TutorialCoreTrials.WatchVision(p.Pm);
        Assert.DoesNotContain("core.loop", p.Record.StationsDone);
        ShardTestClock.Advance(TimeSpan.FromSeconds(9));
        Assert.Contains("core.loop", p.Record.StationsDone);
    }

    [Fact]
    public void SeederRefusesLiveAndUnpatchedMapsAndIsIdempotentWithAFakeMap()
    {
        var testWorld = TutorialCorePlacement.IsTestShard;
        var fits = TutorialCorePlacement.Fits;
        var farmWorld = TutorialFarm.IsTestShard;
        var patched = TutorialFarm.MapPatched;
        var items = new HashSet<Item>(World.Items.Values);
        var mobiles = new HashSet<Mobile>(World.Mobiles.Values);
        try
        {
            TutorialCorePlacement.IsTestShard = () => false;
            TutorialFarm.MapPatched = () => true;
            Assert.Contains("Test world only", TutorialSeeder.Seed(false));
            TutorialCorePlacement.IsTestShard = () => true;
            TutorialFarm.IsTestShard = () => true;
            TutorialFarm.MapPatched = () => false;
            Assert.Contains("map patch", TutorialSeeder.Seed(false));
            TutorialFarm.MapPatched = () => true;
            TutorialCorePlacement.Fits = _ => false;
            Assert.Contains("blocked", TutorialSeeder.Seed(false));
            TutorialCorePlacement.Fits = _ => true;
            TutorialSeeder.Seed(true);
            Assert.Equal(items.Count, World.Items.Count);
            Assert.Equal(mobiles.Count, World.Mobiles.Count);
            Assert.Contains("placed 9 station markers", TutorialSeeder.Seed(false));
            var itemCount = World.Items.Count;
            var mobileCount = World.Mobiles.Count;
            Assert.Contains("placed 0 station markers, 0 NPCs/echo, 0 gate, 0 gold mark", TutorialSeeder.Seed(false));
            Assert.Equal(itemCount, World.Items.Count);
            Assert.Equal(mobileCount, World.Mobiles.Count);
        }
        finally
        {
            TutorialCorePlacement.IsTestShard = testWorld; TutorialCorePlacement.Fits = fits;
            TutorialFarm.IsTestShard = farmWorld; TutorialFarm.MapPatched = patched;
            foreach (var item in World.Items.Values.ToArray()) { if (!items.Contains(item)) { item.Delete(); } }
            foreach (var mobile in World.Mobiles.Values.ToArray()) { if (!mobiles.Contains(mobile)) { mobile.Delete(); } }
        }
    }

    // ---------------------------------------------------------------- framework

    [Fact]
    public void EveryStationIsRegisteredAndWalkingTheCoreOpensTheWingsAndGraduationGivesTheFirstBox()
    {
        string[] expected =
        [
            "core.movement", "core.paperdoll", "core.backpack", "core.chat", "core.death", "core.banking", "core.travel",
            "core.league", "core.loop", "combat.weapons", "combat.healing", "magic.magery", "magic.necromancy",
            "magic.chivalry", "magic.bushido", "magic.ninjitsu", "magic.mysticism", "gathering.mining",
            "gathering.lumberjacking", "gathering.fishing", "gathering.camping", "crafting.blacksmithy",
            "crafting.tailoring", "crafting.carpentry", "crafting.tinkering", "crafting.alchemy", "crafting.fletching",
            "crafting.cooking", "crafting.inscription", "crafting.cartography", "animals.taming", "bard.trial",
            "thieving.trial", "housing.basics", "travel.wing", "reputation.wing", "shard.systems"
        ];
        Assert.Equal(expected.OrderBy(k => k), TutorialStations.All.Select(s => s.Key).OrderBy(k => k));
        Assert.Equal(9, TutorialStations.Core.Count());

        using var p = new Player();
        ClusterFTutorial.OnLogin(p.Pm);
        Assert.Equal("Finish the core first.", ClusterFTutorial.Graduate(p.Pm));

        WalkTheCore(p.Pm);
        Assert.Equal(TutorialState.InWings, p.Record.State);

        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("crafting.alchemy")!);
        Assert.DoesNotContain("crafting.alchemy", p.Record.StationsDone);

        Assert.Null(ClusterFTutorial.Graduate(p.Pm));
        Assert.Equal(TutorialState.Graduated, p.Record.State);
        var boxes = 0;
        foreach (var _ in p.Pm.Backpack.FindItemsByType<LootBox>())
        {
            boxes++;
        }

        Assert.Equal(1, boxes);
        Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFTutorial.RemedialAchievement));
    }
}
