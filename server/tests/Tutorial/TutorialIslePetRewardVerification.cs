// cc-P726: the isle pet is a reward, Pet Info, healing as its own lesson, the starter gear's regeneration, and the
// lessons in the quest log. Notes: shard-migration notes/cc-P726-isle-pet-reward.md.
//
// Facts, each seen failing first:
//   1. No pet before the pests are cleared; then Wren offers it as thanks, once, and its commands follow; the farm then
//      finishes, and the old order's records keep their progress.
//   2. Pet Info shows the right kind, stage and growth (X of Y and the bar), loyalty and how it grows; the context menu
//      entry and the command work only on your own pet.
//   4. Healing is its own lesson after the farm: bandages and the arrow to Pip on his bench inside the farmhouse, with
//      blood around him; a heal finishes it; an old record's farm heal counts for it.
//   5. The cloak and pitchfork add hit point regeneration: 3 each on the isle, 1 each elsewhere, nothing unworn.
//   6. The Quests button opens the isle's lessons for a trainee (done, now, next, steps ticked), pinned's log otherwise.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.MLQuests.Gumps;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIslePetRewardVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIslePetRewardVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
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
        public readonly Account Account = new($"p726{Guid.NewGuid():N}"[..16], "p726-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Rewarded", RawStr = 60, RawDex = 60, RawInt = 60 };
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
            TutorialWake.Stand(Pm, true);
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

    /// <summary>On the isle, awake, with the lessons before the farm done.</summary>
    private static void AtTheFarm(Player p)
    {
        TutorialTestStart.Begin(p.Pm);
        TutorialWake.OnMoveRequest(p.Pm);
        TutorialTestStart.ToTheFarm(p.Pm);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialFarm.CoreStation)!);
    }

    // The shard's hit point regeneration interval (RegenRates), read without registering it: the host keeps its own.
    private static TimeSpan HitsRate(Mobile m) =>
        (TimeSpan)typeof(Server.Misc.RegenRates).GetMethod("Mobile_HitsRegenRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [m])!;

    private static List<Static> StaticsAt(Map map, Point3D at, int range)
    {
        var list = new List<Static>();
        foreach (var s in map.GetItemsInRange<Static>(at, range))
        {
            list.Add(s);
        }

        return list;
    }

    private static void ClearThePests(Player p)
    {
        TutorialFarm.BeginCombat(p.Pm);
        var fork = p.Pm.Backpack!.FindItemByType<TutorialPitchfork>()!;
        fork.Layer = Layer.TwoHanded; // the host has no tiledata
        Assert.True(p.Pm.EquipItem(fork));
        TutorialFarm.CheckCommands(p.Pm);
        p.Pm.Warmode = true;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        TutorialFarm.NoteStruck(p.Pm);
        for (var i = 0; i < TutorialFarm.PestsToClear; i++) { TutorialFarm.CountPest(p.Pm, false); }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThePetIsTheRewardForClearingThePests()
    {
        using var p = new Player();
        AtTheFarm(p);
        var map = TutorialWorld.MapOf(p.Pm)!;
        var wren = p.Add(new TutorialFarmhand());
        wren.MoveToWorld(TutorialWorld.Ground(map, TutorialFarm.FarmhandSpot), map);

        // Before the pests: no pet window, no pet.
        Assert.Equal(1, TutorialFarm.Talk(p.Pm, wren));
        Assert.False(p.Pm.HasGump<TutorialFarmPetGump>());
        Assert.Null(TutorialFarm.ChoosePet(p.Pm, "dog"));
        Assert.False(p.Record.PetClaimed);

        ClearThePests(p);
        Assert.Contains(TutorialFarm.PestsKey, p.Record.StationsDone);
        Assert.True(p.Pm.AllFollowers == null || p.Pm.AllFollowers.Count == 0);
        Assert.Equal(2, TutorialFarm.StepOf(p.Pm));

        // Wren's thanks: the dog or the cat.
        Assert.Equal(2, TutorialFarm.Talk(p.Pm, wren));
        Assert.True(p.Pm.HasGump<TutorialFarmPetGump>());
        Assert.Contains("As thanks", TutorialFarm.PetOfferLine);
        Assert.Contains("dog or a cat", TutorialFarm.PetOfferLine);
        var pet = TutorialFarm.ChoosePet(p.Pm, "cat");
        Assert.NotNull(pet);
        Assert.Null(TutorialFarm.ChoosePet(p.Pm, "dog")); // once
        Assert.Contains("[PetInfo", TutorialFarm.PetInfoLine);
        Assert.Contains("Pet Info", TutorialFarm.PetInfoLine);

        // Its commands follow at once, then the farm is done.
        Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
        Assert.Equal(OrderType.Stay, pet!.ControlOrder);
        pet.ControlTarget = p.Pm;
        pet.ControlOrder = OrderType.Follow;
        TutorialFarm.CheckCommands(p.Pm);
        pet.ControlOrder = OrderType.Guard;
        Assert.True(TutorialFarm.CheckCommands(p.Pm));
        Assert.Equal(4, TutorialFarm.StepOf(p.Pm));
        Assert.Contains(TutorialFarm.CoreStation, p.Record.StationsDone);
        Assert.Equal(TutorialFarm.HealingStationKey, ClusterFTutorial.CoreLeft(p.Record).First().Key);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void PetInfoShowsStageAndGrowthToItsOwnerAlone()
    {
        using var owner = new Player();
        using var other = new Player();
        var pet = owner.Add(EvoPet.GivePet(owner.Pm, "dog")!);
        Assert.Same(owner.Pm, pet.ControlMaster);

        var lines = EvoPetInfo.Lines(pet);
        Assert.Contains(lines, l => l.StartsWith("Kind: dog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains($"(stage {pet.Stage} of {pet.Line.Stages.Length})"));
        var next = pet.Line.NextEnabled(pet.Stage)!;
        Assert.Contains(lines, l => l.Contains($"{pet.Xp:N0} of {EvoPetSettings.XpToEnter(next.Number):N0} experience to become a {next.Name}"));
        Assert.Contains(lines, l => l.StartsWith("Loyalty:", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("How it grows:", StringComparison.Ordinal));

        pet.AwardXp(EvoPetSettings.XpToEnter(next.Number) / 2 - pet.Xp);
        var half = EvoPetInfo.Progress(pet);
        Assert.InRange(half, 0.01, 0.99);

        // The context menu: Pet Info for the owner only.
        var mine = PooledRefList<ContextMenuEntry>.Create();
        pet.AddCustomContextEntries(owner.Pm, ref mine);
        Assert.Contains(mine.ToArray(), e => e is EvoPetInfo.PetInfoEntry && e.Number == ShardClilocs.PetInfo);
        mine.Dispose();
        var theirs = PooledRefList<ContextMenuEntry>.Create();
        pet.AddCustomContextEntries(other.Pm, ref theirs);
        Assert.DoesNotContain(theirs.ToArray(), e => e is EvoPetInfo.PetInfoEntry);
        theirs.Dispose();

        // The command's target: the window for the owner, a refusal for anyone else.
        Assert.True(EvoPetInfo.Show(owner.Pm, pet));
        Assert.NotNull(owner.Pm.FindGump<EvoPetInfoGump>());
        Assert.False(EvoPetInfo.Show(other.Pm, pet));
        Assert.Null(other.Pm.FindGump<EvoPetInfoGump>());
        Assert.False(EvoPetInfo.Show(owner.Pm, owner.Pm)); // not a pet
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void HealingIsItsOwnLessonAtPipOnHisBench()
    {
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialCorePlacement.IsTestShard = () => true;
        TutorialFarm.IsTestShard = () => true;
        TutorialTestStart.FreeAllCopies();
        using var p = new Player();
        AtTheFarm(p);
        var map = TutorialWorld.MapOf(p.Pm)!;

        // The station, after the farm; Pip on his bench in the farmhouse with blood around him.
        var core = TutorialStations.Core.Select(s => s.Key).ToList();
        Assert.Equal(core.IndexOf(TutorialFarm.CoreStation) + 1, core.IndexOf(TutorialFarm.HealingStationKey));
        var pip = TutorialFarm.FindPip(map);
        Assert.NotNull(pip);
        Assert.Equal(TutorialFarm.PipBench, pip!.Location);
        Assert.Contains(StaticsAt(map, TutorialFarm.PipBench, 0), s => s.ItemID == TutorialFarm.BenchId);
        Assert.Equal(3, StaticsAt(map, TutorialFarm.PipBench, 1).Count(s => s.Name == "blood"));
        Assert.Equal(TutorialFarm.PipBench, TutorialCorePlacement.Spot(TutorialFarm.HealingStationKey));

        // Not before the farm is done.
        Assert.False(TutorialFarm.HealingOn(p.Pm));
        p.Record.PetClaimed = true;
        p.Record.StationsDone.UnionWith([TutorialFarm.PestsKey, TutorialFarm.FollowKey, TutorialFarm.GuardKey]);
        TutorialFarm.CheckDone(p.Pm);
        Assert.Contains(TutorialFarm.CoreStation, p.Record.StationsDone);
        Assert.DoesNotContain(TutorialFarm.HealKey, p.Record.StationsDone); // the farm no longer heals

        // The lesson: bandages and the arrow to Pip; a heal finishes it.
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialFarm.HealingStationKey)!);
        Assert.True(TutorialCoreTrials.IsDoing(p.Pm, TutorialFarm.HealingStationKey));
        Assert.NotNull(p.Pm.Backpack!.FindItemByType<Bandage>());
        Assert.True(TutorialFarm.HealingOn(p.Pm));
        Assert.True(TutorialFarm.OnHealed(p.Pm));
        Assert.Contains(TutorialFarm.HealingStationKey, p.Record.StationsDone);
        Assert.False(TutorialFarm.HealingOn(p.Pm));

        // A record that bandaged at the farm (before cc-P726) has done the healing lesson.
        p.Record.StationsDone.Remove(TutorialFarm.HealingStationKey);
        Assert.True(ClusterFTutorial.IsDone(p.Record, TutorialFarm.HealingStationKey));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheStarterGearRegeneratesMoreOnTheIsle()
    {
        using var p = new Player();
        Assert.Equal(0, TutorialStarterGear.RegenHits(p.Pm));
        var cloak = p.Add(new WakeningIsleCloak(p.Pm) { Layer = Layer.Cloak });
        var fork = p.Add(new TutorialPitchfork(p.Pm) { Layer = Layer.TwoHanded });
        Assert.True(p.Pm.EquipItem(cloak));
        Assert.True(p.Pm.EquipItem(fork));

        // Off the isle (New Haven): a little.
        Assert.False(TutorialWorld.IsOnIsle(p.Pm));
        Assert.Equal(2 * TutorialStarterGear.ElsewherePoints, TutorialStarterGear.RegenHits(p.Pm));

        // On the isle: more, and the regeneration interval falls with it.
        var elsewhere = HitsRate(p.Pm);
        TutorialTestStart.Begin(p.Pm);
        TutorialWake.OnMoveRequest(p.Pm);
        Assert.True(TutorialWorld.IsOnIsle(p.Pm));
        Assert.Equal(2 * TutorialStarterGear.IslePoints, TutorialStarterGear.RegenHits(p.Pm));
        Assert.True(HitsRate(p.Pm) < elsewhere, $"{HitsRate(p.Pm)} vs {elsewhere}");

        // Unworn, nothing.
        p.Pm.Backpack!.DropItem(cloak);
        Assert.Equal(TutorialStarterGear.IslePoints, TutorialStarterGear.RegenHits(p.Pm));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheQuestsButtonShowsTheIsleLessonsToATrainee()
    {
        using var p = new Player();
        using var outsider = new Player();
        AtTheFarm(p);

        Assert.True(TutorialQuestLog.Open(p.Pm));
        Assert.NotNull(p.Pm.FindGump<TutorialQuestLogGump>());
        Assert.False(TutorialQuestLog.Open(outsider.Pm)); // not in the tutorial: pinned's log
        Assert.Null(outsider.Pm.FindGump<TutorialQuestLogGump>());

        var stations = TutorialStations.Core.ToArray();
        Assert.Equal(TutorialQuestLog.LessonState.Done, TutorialQuestLog.StateOf(p.Pm, stations[0]));
        Assert.Equal(TutorialQuestLog.LessonState.Now, TutorialQuestLog.StateOf(p.Pm, TutorialStations.Find(TutorialFarm.CoreStation)!));
        Assert.Equal(TutorialQuestLog.LessonState.Later, TutorialQuestLog.StateOf(p.Pm, stations[^1]));

        // The farm's steps tick as they are done.
        var farm = TutorialStations.Find(TutorialFarm.CoreStation)!;
        Assert.All(TutorialQuestLog.Steps(p.Pm, farm), s => Assert.False(s.Done));
        ClearThePests(p);
        var steps = TutorialQuestLog.Steps(p.Pm, farm);
        Assert.True(steps[0].Done && steps[1].Done && steps[2].Done);
        Assert.False(steps[3].Done); // the pet, next

        // A lesson's page; a finished lesson has every step ticked.
        new TutorialQuestLogGump(p.Pm).OnResponse(p.Ns, new RelayInfo(TutorialQuestLogGump.BtnLessonBase + 1, default, default, default, default));
        Assert.Equal(stations[1], p.Pm.FindGump<TutorialQuestStepsGump>()!.Station);
        Assert.All(TutorialQuestLog.Steps(p.Pm, stations[1]), s => Assert.True(s.Done));
    }
}
