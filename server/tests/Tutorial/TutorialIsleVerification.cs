// cc-P663 (P481 F3): the core tutorial on the Wakening Isle, with the start choice. Notes: shard-migration
// notes/cc-P663-tutorial-moves-to-isle.md.
//
// Facts, each seen failing first:
//   1. The order: movement, backpack, paperdoll, chat, the farm, death, banking, league, loop, travel; a character partway
//      through keeps its done keys and resumes at its first undone stop; a travel key saved under the old order counts
//      only once the rest are done.
//   2. A pet crosses the dock: every controlled pet on the Fold goes through the moongate with its owner, wherever it
//      stands and whatever its order; someone else's pet stays.
//   3. Each row of the eligibility table, for both choices, and a character with no choice (made before go-live).
//   4. A choice survives a disconnect before the first login: it is on the account's saved data, and the first login
//      consumes it once.
//   5. A second character never consumes another's choice.
//   6. The moongate takes a trainee by a step and by a double-click, and refuses one with earlier lessons left.
//   7. A reconnect regenerates the current lesson's practice item.
//   8. Remedial: Rook's button sends a skipper (and the pet) to the isle's beach past the island gate; the moongate
//      brings them back beside Rook, who graduates them with the second achievement.
//   cc-P717 (Chase, 2026-10-28): the account's first character must take the isle. A new account's list shows New Haven
//   locked; graduating or skipping unlocks it for the whole account, kept after that character is deleted; staff see
//   both; a New Haven pick while locked starts on the isle; the reset's account flags lock it again.
//   And: the city list offers the isle first and New Haven second, both as Trammel, with our cliloc descriptions; with
//   the tutorial off (live), creation records no choice, so turning it on later skips nobody.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIsleVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Action<PlayerMobile> _rewards = ClusterFTutorial.GraduationRewards;
    private readonly Func<bool> _isleReady = TutorialWorld.IsleReady;

    public TutorialIsleVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        FoldMaps.Configure();
        ClusterFTutorial.Enabled = true;
        ClusterFTutorial.SkipCountsAsGraduation = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
        TutorialCorePlacement.FitsOn = (_, _) => true;
        ClusterFTutorial.GraduationRewards = _ => { };
        TutorialWorld.IsleReady = () => true; // cc-P705: the host's map is void at the isle; it stands in for P662's land
        TutorialTestStart.ReleaseIdleCopies(); // cc-P717: these facts' isle picks leave copies behind
    }

    public void Dispose()
    {
        ClusterFLootBoxes.LoopProvider = _loopProvider;
        TutorialCorePlacement.FitsOn = _fits;
        ClusterFTutorial.GraduationRewards = _rewards;
        TutorialWorld.IsleReady = _isleReady;
    }

    /// <summary>An account with characters made on demand, each connected and standing at the New Haven start.</summary>
    private sealed class Trainees : IDisposable
    {
        public readonly Account Account = new($"p663{Guid.NewGuid():N}"[..16], "p663-test-only");
        public readonly List<PlayerMobile> Characters = new();
        public readonly List<IEntity> Litter = new();
        private readonly List<NetState> _states = new();

        public PlayerMobile Add()
        {
            var pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = $"Isle{Characters.Count}", RawStr = 60, RawDex = 60, RawInt = 60 };
            pm.AddItem(new Backpack());
            pm.Hits = pm.HitsMax;
            Account[Characters.Count] = pm;
            pm.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
            var ns = PacketTestUtilities.CreateTestNetState();
            ns.Account = Account;
            pm.NetState = ns;
            ns.Mobile = pm;
            _states.Add(ns);
            Characters.Add(pm);
            return pm;
        }

        public TutorialRecord Record => ClusterFAccountPersistence.GetOrCreate(Account).Tutorial;

        public void Dispose()
        {
            foreach (var pm in Characters)
            {
                TutorialCoreTrials.Cancel(pm);
                foreach (var m in pm.AllFollowers?.ToList() ?? new List<Mobile>())
                {
                    m.Delete();
                }
            }

            ClusterFFanfare.FlushAll();
            foreach (var e in Litter)
            {
                e.Delete();
            }

            foreach (var ns in _states)
            {
                if (ns.Mobile != null)
                {
                    ns.Mobile.NetState = null;
                }

                ns.Mobile = null;
                ns.Dispose();
            }

            foreach (var pm in Characters)
            {
                pm.Delete();
            }

            Accounts.Remove(Account);
        }
    }

    /// <summary>Character creation's half: the choice recorded and the start placed, as CharacterCreation does it.</summary>
    private static void Create(PlayerMobile pm, TutorialStart.Choice choice)
    {
        var city = TutorialStart.Cities()[choice == TutorialStart.Choice.Isle ? 0 : 1];
        var (at, map) = TutorialStart.OnCreated(pm, city);
        pm.MoveToWorld(at, map);
    }

    private static void Play(PlayerMobile pm, string key)
    {
        ClusterFTutorial.RunStation(pm, TutorialStations.Find(key)!);
        TutorialAction[] actions = key switch
        {
            "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
            "core.backpack" => [TutorialAction.OpenedBag],
            "core.paperdoll" => [TutorialAction.Equipped],
            "core.chat" => [TutorialAction.SaidPhrase],
            "core.farm" => [TutorialAction.FarmDone],
            "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
            "core.banking" => [TutorialAction.Deposited],
            "core.league" => [TutorialAction.TalkedToLeague],
            "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
            "core.travel" => [TutorialAction.UsedGate],
            _ => throw new InvalidOperationException(key)
        };
        foreach (var a in actions)
        {
            TutorialCoreTrials.Observe(pm, a);
        }

        Assert.Contains(key, ClusterFTutorial.RecordOf(pm)!.StationsDone);
    }

    private static TutorialTravelGate Gate(Trainees t)
    {
        var gate = new TutorialTravelGate();
        gate.MoveToWorld(TutorialCorePlacement.GateSpot, TutorialWorld.Isle);
        t.Litter.Add(gate);
        return gate;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheCoreRunsWestToEastWithTheFarmAndTravelLast()
    {
        Assert.Equal(
            ["core.movement", "core.backpack", "core.paperdoll", "core.chat", "core.farm", "core.death", "core.banking",
             "core.league", "core.loop", "core.travel"],
            TutorialStations.Core.Select(s => s.Key).ToArray());

        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);

        // Saved under the old order: three lessons and travel (which came seventh then).
        t.Record.StationsDone.UnionWith(["core.movement", "core.backpack", "core.paperdoll", "core.travel"]);
        Assert.Equal("core.chat", ClusterFTutorial.CoreLeft(t.Record).First().Key);
        Assert.False(ClusterFTutorial.IsDone(t.Record, "core.travel"));
        Assert.Contains("core.travel", ClusterFTutorial.CoreLeft(t.Record).Select(s => s.Key));

        // Out of order is refused: the farm cannot start before chat.
        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.farm")!);
        Assert.Null(TutorialCoreTrials.Current(pm));

        foreach (var key in new[] { "core.chat", "core.farm", "core.death", "core.banking", "core.league" })
        {
            Play(pm, key);
            Assert.False(ClusterFTutorial.CoreDone(t.Record));
        }

        Play(pm, "core.loop");
        // The old travel key counts now that the rest are done: no lesson repeated, the core finished.
        Assert.True(ClusterFTutorial.IsDone(t.Record, "core.travel"));
        Assert.True(ClusterFTutorial.CoreDone(t.Record));
        Assert.Equal(TutorialState.InWings, t.Record.State);
        Assert.Equal(10, t.Record.StationsDone.Count(k => k.StartsWith("core.", StringComparison.Ordinal)));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void APetCrossesTheDockFromAnywhereOnTheFold()
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        foreach (var key in TutorialStations.Core.Select(s => s.Key).TakeWhile(k => k != "core.travel"))
        {
            Play(pm, key);
        }

        var pet = ClusterFTutorial.GivePet(pm, "dog")!;
        Assert.NotNull(pet);
        // Far up the island, told to stay: pinned's TeleportPets takes neither (3 tiles; guard, follow or come).
        pet.MoveToWorld(TutorialWorld.Ground(TutorialWorld.Signpost), TutorialWorld.Isle);
        pet.ControlOrder = OrderType.Stay;

        var other = t.Add();
        var theirs = ClusterFTutorial.GivePet(other, "cat")!;
        theirs.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);

        var gate = Gate(t);
        pm.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);
        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.travel")!);
        gate.OnDoubleClick(pm);

        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(HavenSpots.MoongateExit, pm.Location);
        Assert.Same(HavenSpots.Map, pet.Map);
        Assert.Equal(HavenSpots.MoongateExit, pet.Location);
        Assert.Same(TutorialWorld.Isle, theirs.Map); // not this trainee's
        Assert.Contains("core.travel", t.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 3

    public enum Before { Fresh, InCoreElsewhere, Graduated, Skipped, DeletedMidCore }

    public static IEnumerable<object[]> Rows() =>
    [
        [Before.Fresh, TutorialStart.Choice.Isle, TutorialStart.Outcome.Isle],
        // cc-P717: New Haven is locked until the account graduates or skips; a locked pick of it is the isle's row.
        [Before.Fresh, TutorialStart.Choice.Haven, TutorialStart.Outcome.Isle],
        [Before.InCoreElsewhere, TutorialStart.Choice.Isle, TutorialStart.Outcome.Elsewhere],
        [Before.InCoreElsewhere, TutorialStart.Choice.Haven, TutorialStart.Outcome.Elsewhere],
        [Before.Graduated, TutorialStart.Choice.Isle, TutorialStart.Outcome.Finished],
        [Before.Graduated, TutorialStart.Choice.Haven, TutorialStart.Outcome.Haven],
        [Before.Skipped, TutorialStart.Choice.Isle, TutorialStart.Outcome.Finished],
        [Before.Skipped, TutorialStart.Choice.Haven, TutorialStart.Outcome.Haven],
        [Before.DeletedMidCore, TutorialStart.Choice.Isle, TutorialStart.Outcome.Isle],
        [Before.DeletedMidCore, TutorialStart.Choice.Haven, TutorialStart.Outcome.Isle]
    ];

    [Theory]
    [MemberData(nameof(Rows))]
    public void EachRowOfTheEligibilityTable(Before before, TutorialStart.Choice choice, TutorialStart.Outcome expected)
    {
        using var t = new Trainees();
        PlayerMobile? first = null;
        if (before != Before.Fresh)
        {
            first = t.Add();
            Assert.True(ClusterFTutorial.TryEnter(first, quiet: true));
            t.Record.StationsDone.UnionWith(["core.movement", "core.backpack"]);
            switch (before)
            {
                case Before.Graduated:
                    FinishCore(t.Record); // cc-P717: a real graduation, which unlocks New Haven
                    Assert.Null(ClusterFTutorial.Graduate(first));
                    break;
                case Before.Skipped:
                    Assert.True(ClusterFTutorial.Skip(first));
                    break;
                case Before.DeletedMidCore:
                    first.Delete();
                    break;
            }
        }

        var pm = t.Add();
        Create(pm, choice);
        Assert.Equal(expected, TutorialStart.Decide(pm, choice));
        ClusterFTutorial.OnLogin(pm);

        var r = t.Record;
        switch (expected)
        {
            case TutorialStart.Outcome.Isle:
                Assert.Equal(TutorialState.InCore, r.State);
                Assert.Equal((uint)pm.Serial, r.CharacterSerial);
                Assert.True(TutorialCopies.IsCopy(pm.Map)); // cc-P664: their own copy of the isle
                Assert.Equal(TutorialWorld.Arrival, pm.Location);
                Assert.True(TutorialWorld.IsOnIsle(pm));
                if (before == Before.DeletedMidCore)
                {
                    Assert.Contains("core.backpack", r.StationsDone); // done keys kept
                }

                break;
            case TutorialStart.Outcome.Skip:
                Assert.Equal(TutorialState.Skipped, r.State);
                Assert.True(r.WasSkipped);
                Assert.Equal((uint)pm.Serial, r.CharacterSerial);
                Assert.Equal(1, ClusterFLoops.LoopOf(pm));
                Assert.True(ClusterFAchievementSystem.HasEarned(t.Account, ClusterFTutorial.SkippedAchievement));
                Assert.False(r.PetClaimPending); // no tutorial graduation (its box and pet pick)
                Assert.Same(HavenSpots.Map, pm.Map);
                Assert.Equal(HavenSpots.Start, pm.Location);
                break;
            default:
                Assert.Same(HavenSpots.Map, pm.Map);
                Assert.Equal(HavenSpots.Start, pm.Location);
                Assert.False(ClusterFTutorial.IsParticipant(pm));
                if (first != null && before is Before.InCoreElsewhere)
                {
                    Assert.Equal((uint)first.Serial, r.CharacterSerial);
                    Assert.Equal(TutorialState.InCore, r.State);
                }

                break;
        }

        // A skipped account's new character that chose the isle is offered remedial training at Rook.
        if (before == Before.Skipped && choice == TutorialStart.Choice.Isle)
        {
            var page = new TutorialRecruiterGump(pm);
            Assert.Contains(TutorialRecruiterGump.BtnRemedial, page.Entries.OfType<GumpButton>().Select(b => b.ButtonID));
        }

        Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(pm)); // consumed
    }

    [Fact]
    public void ACharacterFromBeforeGoLiveIsNeverEnteredOrMoved()
    {
        using var t = new Trainees();
        var veteran = t.Add(); // no choice: made before the start choice existed
        ClusterFTutorial.OnLogin(veteran);
        Assert.Equal(TutorialState.NotStarted, t.Record.State);
        Assert.Same(HavenSpots.Map, veteran.Map);
        Assert.Equal(HavenSpots.Start, veteran.Location);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AChoiceSurvivesADisconnectBeforeTheFirstLogin()
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        Assert.Same(TutorialWorld.Isle, pm.Map); // creation already set them on the isle

        // The disconnect: nothing but the account's saved data carries the choice. Write it and read it back.
        var data = ClusterFAccountPersistence.GetOrCreate(t.Account);
        var writer = new BufferWriter(true);
        data.Serialize(writer);
        var copy = new ClusterFAccountData(new BufferReader(writer.Buffer));
        Assert.Equal("isle", copy.FlagValues[$"character:{(uint)pm.Serial}:start"]);
        Assert.Equal(TutorialState.NotStarted, copy.Tutorial.State);

        Assert.Equal(TutorialStart.Outcome.Isle, TutorialStart.ConsumeAtLogin(pm));
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.Equal(TutorialStart.Outcome.None, TutorialStart.ConsumeAtLogin(pm)); // once
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ASecondCharacterNeverConsumesAnothersChoice()
    {
        using var t = new Trainees();
        var chose = t.Add();
        Create(chose, TutorialStart.Choice.Isle);
        var other = t.Add(); // no choice of its own

        ClusterFTutorial.OnLogin(other);
        Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(other));
        Assert.Equal(TutorialStart.Choice.Isle, TutorialStart.Pending(chose));
        Assert.Equal(TutorialState.NotStarted, t.Record.State);

        ClusterFTutorial.OnLogin(chose);
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.Equal((uint)chose.Serial, t.Record.CharacterSerial);
    }

    // ---------------------------------------------------------------- 6

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheMoongateTakesAStepOrADoubleClick(bool step)
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        var gate = Gate(t);
        pm.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);

        // Lessons left before it: refused, and told which comes next.
        Assert.False(TutorialTravelGate.Use(pm, gate.Location));
        Assert.Same(TutorialWorld.Isle, pm.Map);

        foreach (var key in TutorialStations.Core.Select(s => s.Key).TakeWhile(k => k != "core.travel"))
        {
            Play(pm, key);
        }

        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.travel")!);
        if (step)
        {
            Assert.True(gate.OnMoveOver(pm));
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
        }
        else
        {
            gate.OnDoubleClick(pm);
        }

        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(HavenSpots.MoongateExit, pm.Location);
        Assert.Contains("core.travel", t.Record.StationsDone);
        Assert.Equal(TutorialState.InWings, t.Record.State);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void AReconnectRegeneratesTheCurrentSupply()
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        foreach (var key in TutorialStations.Core.Select(s => s.Key).TakeWhile(k => k != "core.banking"))
        {
            Play(pm, key);
        }

        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.banking")!);
        var token = TutorialCoreTrials.Current(pm)!.Supply!;
        Assert.True(token.IsChildOf(pm.Backpack));

        ClusterFTutorial.OnLogin(pm); // the reconnect
        Assert.True(token.Deleted);
        Assert.Null(TutorialCoreTrials.Current(pm));
        Assert.True(TutorialWorld.IsOnIsle(pm)); // still where they were

        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.banking")!);
        var fresh = TutorialCoreTrials.Current(pm)!.Supply!;
        Assert.IsType<TutorialBankToken>(fresh);
        Assert.NotSame(token, fresh);
        Assert.True(fresh.IsChildOf(pm.Backpack));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void RemedialSailsToTheIsleAndTheMoongateBringsTheSkipperBackToRook()
    {
        using var t = new Trainees();
        var pm = t.Add();
        // cc-P717: a fresh account cannot skip at creation (New Haven is locked); it skips from the isle.
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        Assert.True(ClusterFTutorial.Skip(pm));
        Assert.Equal(TutorialState.Skipped, t.Record.State);

        var pet = ClusterFTutorial.GivePet(pm, "dog")!;
        pet.MoveToWorld(new Point3D(pm.X + 2, pm.Y, pm.Z), pm.Map);
        var rook = new TutorialRecruiter();
        rook.MoveToWorld(HavenSpots.Rook, HavenSpots.Map);
        t.Litter.Add(rook);

        new TutorialRecruiterGump(pm).OnResponse(pm.NetState!, new RelayInfo(TutorialRecruiterGump.BtnRemedial, default, default, default, default));
        Assert.Equal(TutorialState.Remedial, t.Record.State);
        Assert.True(TutorialCopies.IsCopy(pm.Map)); // cc-P664: their own copy of the isle
        Assert.Equal(TutorialWorld.Arrival, pm.Location);
        Assert.Same(pm.Map, pet.Map); // the pet came along

        foreach (var key in TutorialStations.Core.Select(s => s.Key).TakeWhile(k => k != "core.travel"))
        {
            Play(pm, key);
        }

        var gate = Gate(t);
        pm.MoveToWorld(gate.Location, TutorialWorld.Isle);
        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.travel")!);
        gate.OnDoubleClick(pm);
        Assert.Equal(HavenSpots.MoongateExit, pm.Location);
        Assert.Same(HavenSpots.Map, pet.Map);

        new TutorialRecruiterGump(pm).OnResponse(pm.NetState!, new RelayInfo(TutorialRecruiterGump.BtnGraduate, default, default, default, default));
        Assert.Equal(TutorialState.Graduated, t.Record.State);
        Assert.True(ClusterFAchievementSystem.HasEarned(t.Account, ClusterFTutorial.RemedialAchievement));
    }

    // ---------------------------------------------------------------- the tutorial off (live, until F4)

    [Fact]
    public void WithTheTutorialOffCreationRecordsNoChoice()
    {
        using var t = new Trainees();
        var pm = t.Add();
        try
        {
            ClusterFTutorial.Enabled = false;
            var (at, map) = TutorialStart.OnCreated(pm, TutorialStart.Cities()[1]);
            Assert.Equal((HavenSpots.Start, HavenSpots.Map), (at, map));
            Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(pm));
        }
        finally
        {
            ClusterFTutorial.Enabled = true;
        }

        // Turned on later, that character's next login skips nothing.
        ClusterFTutorial.OnLogin(pm);
        Assert.Equal(TutorialState.NotStarted, t.Record.State);
    }

    // ---------------------------------------------------------------- the city list

    [Fact]
    public void TheCityListOffersTheIsleFirstAndNewHavenSecondBothAsTrammel()
    {
        var cities = TutorialStart.Cities();
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.HavenCity], cities.Select(c => c.City).ToArray());
        Assert.All(cities, c => Assert.Same(Map.Trammel, c.Map));
        Assert.Equal([ShardClilocs.TutorialIslandCity, ShardClilocs.NewHavenSkipCity], cities.Select(c => c.Description).ToArray());
        Assert.Equal(TutorialStart.IsleDisplay, cities[0].Location);
        Assert.Equal(HavenSpots.Start, cities[1].Location);
        Assert.Equal(TutorialStart.Choice.Isle, TutorialStart.ChoiceOf(cities[0]));
        Assert.Equal(TutorialStart.Choice.Haven, TutorialStart.ChoiceOf(cities[1]));
    }

    // ---------------------------------------------------------------- cc-P705: the Codex findings on P663

    // P1: the dock's stops stand on the planks, not on the sea floor under them; a floor is stood on, a roof is not.
    [Fact]
    public void TheGroundIsTheWalkableSurfaceNotTheLandUnderIt()
    {
        // Flags and heights as stock tiledata has them (the host loads none): plank 0x07C9, cobbles 0x0515, grey slate
        // 0x049C, static water 0x1797.
        const TileFlag plank = TileFlag.Background | TileFlag.Surface | TileFlag.NoShoot;
        const TileFlag water = TileFlag.Background | TileFlag.Impassable | TileFlag.Wet;
        Assert.Equal(-2, TutorialWorld.SurfaceZ(-5, [(-3, plank, 1)]));                  // a dock plank over the sea
        Assert.Equal(0, TutorialWorld.SurfaceZ(0, [(0, plank, 0), (20, plank, 0)]));     // a floor under a slate roof
        Assert.Equal(-5, TutorialWorld.SurfaceZ(-5, [(-5, water, 0)]));                  // static water is no surface
        Assert.Equal(3, TutorialWorld.SurfaceZ(3, []));                                  // bare land
    }

    // P2: the tutorial-off list's New Haven entry follows HavenSpots, as every other New Haven spot does (S7 moves them).
    [Fact]
    public void TheTutorialOffListReadsNewHavensStartFromHavenSpots()
    {
        var start = HavenSpots.Start;
        try
        {
            HavenSpots.Start = new Point3D(3510, 2580, 15);
            var city = Assert.Single(Server.Engines.CharacterCreation.CharacterCreation.TutorialOffCities());
            Assert.Equal("New Haven", city.City);
            Assert.Equal(HavenSpots.Start, city.Location);
            Assert.Same(HavenSpots.Map, city.Map);
        }
        finally
        {
            HavenSpots.Start = start;
        }
    }

    // P2: staff who keep the default "Tutorial Island" start at New Haven's start, never at the entry's display spot.
    [Fact]
    public void StaffWhoPickTheIsleStartInNewHavenNotInTheSea()
    {
        using var t = new Trainees();
        var gm = t.Add();
        gm.AccessLevel = AccessLevel.GameMaster;
        var (at, map) = TutorialStart.OnCreated(gm, TutorialStart.Cities()[0]);
        Assert.Equal((HavenSpots.Start, HavenSpots.Map), (at, map));
        Assert.NotEqual(TutorialStart.IsleDisplay, at);
        Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(gm)); // staff are never enrolled
    }

    // The missing isle: a release with the start choice but before P662's map patch never strands a character in the
    // void. The pick starts in New Haven, and the account's tutorial stays unused.
    [Fact]
    public void WithoutTheIslesLandTheIslePickStartsInNewHaven()
    {
        using var t = new Trainees();
        var pm = t.Add();
        TutorialWorld.IsleReady = () => false;
        Create(pm, TutorialStart.Choice.Isle);
        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(HavenSpots.Start, pm.Location);
        Assert.Equal(TutorialStart.Outcome.IsleMissing, TutorialStart.ConsumeAtLogin(pm));
        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(HavenSpots.Start, pm.Location);
        Assert.Equal(TutorialState.NotStarted, t.Record.State);
        Assert.False(ClusterFTutorial.IsParticipant(pm));
        Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(pm));

        // Once the land is there, a new character of the account goes to the isle as usual.
        TutorialWorld.IsleReady = () => true;
        var next = t.Add();
        Create(next, TutorialStart.Choice.Isle);
        Assert.Equal(TutorialStart.Outcome.Isle, TutorialStart.ConsumeAtLogin(next));
        Assert.True(TutorialWorld.IsOnIsle(next));
    }

    // ---------------------------------------------------------------- cc-P717: the isle first, New Haven after

    private static string[] CityNames(IAccount account) =>
        Server.Engines.CharacterCreation.CharacterCreation.GetStartingCities(account).Select(c => c.City).ToArray();

    private static void FinishCore(TutorialRecord record)
    {
        foreach (var s in TutorialStations.Core)
        {
            record.StationsDone.Add(s.Key);
        }
    }

    [Fact]
    public void ANewAccountSeesNewHavenLockedWithItsReason()
    {
        using var t = new Trainees();
        Assert.False(TutorialStart.IsHavenUnlocked(t.Account));
        var cities = Server.Engines.CharacterCreation.CharacterCreation.GetStartingCities(t.Account);
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.LockedHavenCity], cities.Select(c => c.City).ToArray());
        Assert.Equal([ShardClilocs.TutorialIslandCity, ShardClilocs.NewHavenLockedCity], cities.Select(c => c.Description).ToArray());
        Assert.Equal(TutorialStart.Choice.Isle, TutorialStart.ChoiceOf(cities[1])); // the locked entry is the isle
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GraduatingOrSkippingUnlocksNewHavenForTheWholeAccount(bool graduate)
    {
        using var t = new Trainees();
        var first = t.Add();
        Create(first, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(first);
        Assert.False(TutorialStart.IsHavenUnlocked(t.Account));

        if (graduate)
        {
            FinishCore(t.Record);
            Assert.Null(ClusterFTutorial.Graduate(first));
        }
        else
        {
            Assert.True(ClusterFTutorial.Skip(first));
        }

        Assert.True(TutorialStart.IsHavenUnlocked(t.Account));
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.HavenCity], CityNames(t.Account));

        // Kept after the character who earned it is deleted; the account's next character may start in New Haven.
        first.Delete();
        Assert.True(TutorialStart.IsHavenUnlocked(t.Account));
        var next = t.Add();
        Create(next, TutorialStart.Choice.Haven);
        Assert.Equal(TutorialStart.Outcome.Haven, TutorialStart.ConsumeAtLogin(next));
        Assert.Same(HavenSpots.Map, next.Map);
        Assert.Equal(HavenSpots.Start, next.Location);
    }

    [Fact]
    public void StaffAccountsAlwaysSeeBoth()
    {
        using var t = new Trainees();
        t.Account.AccessLevel = AccessLevel.GameMaster;
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.HavenCity], CityNames(t.Account));
    }

    [Fact]
    public void ANewHavenPickWhileLockedStartsOnTheIsle()
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Haven); // a client that sends the unlocked list's "New Haven" anyway
        Assert.True(TutorialWorld.IsOnIsle(pm));
        Assert.Equal(TutorialStart.Outcome.Isle, TutorialStart.ConsumeAtLogin(pm));
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.False(t.Record.WasSkipped);
        Assert.Equal(0, ClusterFLoops.LoopOf(pm));
    }

    [Fact]
    public void TheResetsAccountFlagsLockNewHavenAgain()
    {
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        Assert.True(ClusterFTutorial.Skip(pm));
        Assert.True(TutorialStart.IsHavenUnlocked(t.Account));

        // [ResetMyAccount and [ClusterFReset both run ExecuteReset; its "account flags" box clears the unlock.
        ClusterFDevTools.ExecuteReset(pm, pm, t.Account, new ResetOptions { Flags = true });
        Assert.False(TutorialStart.IsHavenUnlocked(t.Account));
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.LockedHavenCity], CityNames(t.Account));

        // Earned again by the next skip or graduation.
        t.Record.State = TutorialState.InCore;
        Assert.True(ClusterFTutorial.Skip(pm));
        Assert.True(TutorialStart.IsHavenUnlocked(t.Account));
    }

    // Only the flag unlocks: a finished record would survive the reset, which clears flags but not the record. An account
    // that finished before cc-P717 sees New Haven locked, and either pick still starts in New Haven.
    [Fact]
    public void AnAccountThatFinishedBeforeThisBuildSeesItLockedAndStartsInNewHaven()
    {
        using var t = new Trainees();
        t.Record.State = TutorialState.Graduated; // no unlock flag: saved before cc-P717
        Assert.False(TutorialStart.IsHavenUnlocked(t.Account));
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Haven);
        Assert.Equal(TutorialStart.Outcome.Finished, TutorialStart.ConsumeAtLogin(pm));
        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(HavenSpots.Start, pm.Location);
        Assert.Equal(TutorialState.Graduated, t.Record.State);
    }
}
