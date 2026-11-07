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
//   cc-P717 (Chase, 2026-10-28) locked New Haven per account; cc-P762 (Chase, 2026-11-01) replaced it with a shard-wide
//   lock (NewCharacterRules): see the cc-P762 section at the end. The eligibility table (3) is the switch-on table.
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
            "core.backpack" => [TutorialAction.OpenedPaperdoll, TutorialAction.OpenedBag], // cc-P718: the character window first
            "core.paperdoll" => [TutorialAction.Equipped],
            "core.chat" => [TutorialAction.SaidPhrase],
            "core.farm" => [TutorialAction.FarmDone],
            "core.healing" => [TutorialAction.Healed],
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
            // cc-P817: the farm (with the pet pick) moved to last before the moongate; healing stays after chat.
            ["core.movement", "core.backpack", "core.paperdoll", "core.chat", "core.healing", "core.death", "core.banking",
             "core.league", "core.loop", "core.farm", "core.travel"],
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

        foreach (var key in new[] { "core.chat", "core.healing", "core.death", "core.banking", "core.league", "core.loop" })
        {
            Play(pm, key);
            Assert.False(ClusterFTutorial.CoreDone(t.Record));
        }

        Play(pm, "core.farm"); // cc-P817: the farm last before the moongate
        // The old travel key counts now that the rest are done: no lesson repeated, the core finished.
        Assert.True(ClusterFTutorial.IsDone(t.Record, "core.travel"));
        Assert.True(ClusterFTutorial.CoreDone(t.Record));
        Assert.Equal(TutorialState.InWings, t.Record.State);
        Assert.Equal(11, t.Record.StationsDone.Count(k => k.StartsWith("core.", StringComparison.Ordinal))); // cc-P726: core.healing
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
        // cc-P762: these rows are New Haven's switch on (newCharacters.newHavenStart); locked, see the cc-P762 section.
        [Before.Fresh, TutorialStart.Choice.Isle, TutorialStart.Outcome.Isle],
        [Before.Fresh, TutorialStart.Choice.Haven, TutorialStart.Outcome.Skip],
        [Before.InCoreElsewhere, TutorialStart.Choice.Isle, TutorialStart.Outcome.Elsewhere],
        [Before.InCoreElsewhere, TutorialStart.Choice.Haven, TutorialStart.Outcome.Haven],
        [Before.Graduated, TutorialStart.Choice.Isle, TutorialStart.Outcome.Finished],
        [Before.Graduated, TutorialStart.Choice.Haven, TutorialStart.Outcome.Haven],
        [Before.Skipped, TutorialStart.Choice.Isle, TutorialStart.Outcome.Finished],
        [Before.Skipped, TutorialStart.Choice.Haven, TutorialStart.Outcome.Haven],
        [Before.DeletedMidCore, TutorialStart.Choice.Isle, TutorialStart.Outcome.Isle],
        [Before.DeletedMidCore, TutorialStart.Choice.Haven, TutorialStart.Outcome.Skip]
    ];

    [Theory]
    [MemberData(nameof(Rows))]
    public void EachRowOfTheEligibilityTable(Before before, TutorialStart.Choice choice, TutorialStart.Outcome expected)
    {
        using var open = new Switches(havenStart: true);
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
                    FinishCore(t.Record);
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
        // cc-P762: entered by staff ([Tutorial enter), not a new character, so lessons left are still refused; a new
        // character runs through as the gate's skip (the cc-P762 section).
        Assert.True(ClusterFTutorial.TryEnter(pm, quiet: true));
        TutorialStart.SendToIsle(pm);
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
        // cc-P717, cc-P762: a fresh account cannot skip at creation (New Haven is locked); it skips from the isle.
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

    // ---------------------------------------------------------------- helpers

    private static string[] CityNames(IAccount account) =>
        Server.Engines.CharacterCreation.CharacterCreation.GetStartingCities(account).Select(c => c.City).ToArray();

    private static void FinishCore(TutorialRecord record)
    {
        foreach (var s in TutorialStations.Core)
        {
            record.StationsDone.Add(s.Key);
        }
    }

    /// <summary>cc-P762: sets the two new-character switches for one fact, and puts them back.</summary>
    private sealed class Switches : IDisposable
    {
        private readonly string? _haven = ServerConfiguration.GetSetting(NewCharacterRules.HavenStartKey, (string?)null);
        private readonly string? _races = ServerConfiguration.GetSetting(NewCharacterRules.RacesKey, (string?)null);

        public Switches(bool havenStart = false, string races = NewCharacterRules.DefaultRaces)
        {
            ServerConfiguration.SetSetting(NewCharacterRules.HavenStartKey, havenStart);
            ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, races);
        }

        public void Dispose()
        {
            ServerConfiguration.SetSetting(NewCharacterRules.HavenStartKey, _haven ?? "False");
            ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, _races ?? NewCharacterRules.DefaultRaces);
        }
    }

    // ---------------------------------------------------------------- cc-P762: the new-character lockdown

    [Fact]
    public void P762_APlayerAccountSeesOnlyTheIsle()
    {
        // cc-P817 (Chase: "I also want New Haven removed from map start"): P762 showed New Haven locked; now the isle alone.
        using var locked = new Switches();
        using var t = new Trainees();
        var cities = Server.Engines.CharacterCreation.CharacterCreation.GetStartingCities(t.Account);
        Assert.Equal([TutorialStart.IsleCity], cities.Select(c => c.City).ToArray());
        Assert.Equal([ShardClilocs.TutorialIslandCity], cities.Select(c => c.Description).ToArray());
        // An old list's locked entry still reads as the isle, should one come back.
        Assert.Equal(TutorialStart.Choice.Isle, TutorialStart.ChoiceOf(new CityInfo(TutorialStart.LockedHavenCity, "", 0, 0, 0, 0, Map.Trammel)));
    }

    // ---------------------------------------------------------------- cc-P817: creation (Chase 11-07, update 11)

    // Item 3: the start map is the isle alone for a player account; staff still see New Haven as well.
    [Fact]
    public void P817_TheStartMapIsTutorialIslandAloneAndStaffKeepBoth()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        Assert.Equal(["Tutorial Island"], CityNames(t.Account));

        var staff = new Account($"p817s{Guid.NewGuid():N}"[..16], "p817-test-only") { AccessLevel = AccessLevel.GameMaster };
        try
        {
            Assert.Equal(["Tutorial Island", "New Haven"], CityNames(staff));
        }
        finally
        {
            staff.Delete();
        }
    }

    // Item 1: an old or third-party client that asks for an elf or a gargoyle gets a Human, and is told.
    [Theory]
    [InlineData("Elf")]
    [InlineData("Gargoyle")]
    public void P817_AnElfOrGargoyleAskedForIsMadeHuman(string asked)
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var race = asked == "Elf" ? Race.Elf : Race.Gargoyle;
        Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, race, out var refused));
        Assert.True(refused);
        Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Human, out refused));
        Assert.False(refused);
    }

    // Item 2: the trade choice (the client's Blacksmith, the shard's Maker template, profession 3) makes no character.
    [Fact]
    public void P817_TheTradeProfessionIsRefused()
    {
        Assert.Null(ClusterFStartChoices.Resolve(3, [], [60, 15, 15], Race.Human, true, out var why));
        Assert.Contains("no longer offered", why);
        Assert.NotNull(ClusterFStartChoices.Resolve(1, [], [45, 35, 10], Race.Human, true, out _)); // the Fighter still is
        Assert.NotNull(ClusterFStartChoices.Resolve(2, [], [25, 20, 45], Race.Human, true, out _)); // and the Mage
    }

    [Fact]
    public void P762_NewHavenSelectedStartsOnTheIsleInTheLessons()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Haven); // a client that sends the unlocked list's "New Haven" anyway
        Assert.True(TutorialWorld.IsOnIsle(pm));
        Assert.Equal(TutorialStart.Outcome.Isle, TutorialStart.ConsumeAtLogin(pm));
        Assert.True(TutorialCopies.IsCopy(pm.Map));
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.False(t.Record.WasSkipped);
        Assert.True(ClusterFTutorial.IsParticipant(pm));
        Assert.True(TutorialStart.IsIsleStarter(pm));
    }

    [Theory]
    [InlineData(TutorialStart.Choice.Isle)]
    [InlineData(TutorialStart.Choice.Haven)]
    public void P762_ASecondCharacterWhileTheFirstIsInTheTutorialStartsOnTheIsle(TutorialStart.Choice choice)
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var first = t.Add();
        Create(first, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(first);
        Assert.Equal(TutorialState.InCore, t.Record.State);

        var second = t.Add();
        Create(second, choice);
        Assert.True(TutorialWorld.IsOnIsle(second)); // placed on the isle at creation
        Assert.Equal(TutorialStart.Outcome.Visit, TutorialStart.ConsumeAtLogin(second));
        Assert.True(TutorialCopies.IsCopy(second.Map)); // its own copy, not New Haven
        Assert.Equal(TutorialWorld.Arrival, second.Location);
        Assert.False(ClusterFTutorial.IsParticipant(second)); // visiting: the lessons stay with the first
        Assert.Equal((uint)first.Serial, t.Record.CharacterSerial);
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.True(TutorialStart.StillOnFirstVisit(second));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void P762_AfterAnAccountResetTheNextCharacterTakesTheLessonsOnTheIsle(bool graduate)
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var first = t.Add();
        Create(first, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(first);
        if (graduate)
        {
            FinishCore(t.Record);
            Assert.Null(ClusterFTutorial.Graduate(first));
        }
        else
        {
            Assert.True(ClusterFTutorial.Skip(first));
        }

        // [ResetMyAccount and [ClusterFReset both run ExecuteReset; its "account flags" box clears the tutorial record too.
        ClusterFDevTools.ExecuteReset(first, first, t.Account, new ResetOptions { Flags = true });
        Assert.Equal(TutorialState.NotStarted, t.Record.State);
        Assert.Equal([TutorialStart.IsleCity], CityNames(t.Account)); // cc-P817: the isle alone

        var next = t.Add();
        Create(next, TutorialStart.Choice.Haven);
        Assert.Equal(TutorialStart.Outcome.Isle, TutorialStart.ConsumeAtLogin(next));
        Assert.True(TutorialWorld.IsOnIsle(next));
        Assert.Equal(TutorialState.InCore, t.Record.State);
        Assert.Equal((uint)next.Serial, t.Record.CharacterSerial);
    }

    [Theory]
    [InlineData(TutorialState.Graduated)]
    [InlineData(TutorialState.Skipped)]
    public void P762_AFinishedAccountsNewCharacterVisitsTheIsle(TutorialState finished)
    {
        using var locked = new Switches();
        using var t = new Trainees();
        t.Record.State = finished;
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Haven);
        Assert.Equal(TutorialStart.Outcome.Visit, TutorialStart.ConsumeAtLogin(pm));
        Assert.True(TutorialWorld.IsOnIsle(pm));
        Assert.Equal(finished, t.Record.State);
    }

    [Fact]
    public void P762_ElfAndGargoyleAreMadeHumanForPlayers()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Elf, out var elfRefused));
        Assert.True(elfRefused);
        Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Gargoyle, out var gargRefused));
        Assert.True(gargRefused);
        Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Human, out var humanRefused));
        Assert.False(humanRefused);
        Assert.Equal([Race.Human], NewCharacterRules.AllowedRaces.ToArray());
    }

    [Fact]
    public void P762_StaffAreExemptFromBothLocks()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        t.Account.AccessLevel = AccessLevel.GameMaster;
        Assert.Equal([TutorialStart.IsleCity, TutorialStart.HavenCity], CityNames(t.Account));
        Assert.Same(Race.Elf, NewCharacterRules.RaceFor(t.Account, Race.Elf, out var refused));
        Assert.False(refused);
        Assert.Same(Race.Gargoyle, NewCharacterRules.RaceFor(t.Account, Race.Gargoyle, out _));

        var gm = t.Add();
        gm.AccessLevel = AccessLevel.GameMaster;
        var (at, map) = TutorialStart.OnCreated(gm, TutorialStart.Cities()[1]);
        Assert.Equal((HavenSpots.Start, HavenSpots.Map), (at, map)); // New Haven, as asked
        Assert.Equal(TutorialStart.Choice.None, TutorialStart.Pending(gm));
    }

    [Fact]
    public void P762_EachSwitchReopensItsLock()
    {
        using var t = new Trainees();
        using (new Switches(havenStart: true))
        {
            Assert.Equal([TutorialStart.IsleCity, TutorialStart.HavenCity], CityNames(t.Account));
            var pm = t.Add();
            Create(pm, TutorialStart.Choice.Haven);
            Assert.Equal(TutorialStart.Outcome.Skip, TutorialStart.ConsumeAtLogin(pm));
            Assert.Same(HavenSpots.Map, pm.Map);
            Assert.Equal(HavenSpots.Start, pm.Location);
            // the race lock is the other switch: still Human only
            Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Elf, out _));
        }

        using (new Switches(races: "Human, Elf"))
        {
            Assert.Equal([TutorialStart.IsleCity], CityNames(t.Account)); // cc-P817: the isle alone
            Assert.Same(Race.Elf, NewCharacterRules.RaceFor(t.Account, Race.Elf, out var refused));
            Assert.False(refused);
            Assert.Same(Race.Human, NewCharacterRules.RaceFor(t.Account, Race.Gargoyle, out _));
        }
    }

    [Fact]
    public void P762_SwitchesDefaultToLocked()
    {
        var haven = ServerConfiguration.GetSetting(NewCharacterRules.HavenStartKey, (string?)null);
        var races = ServerConfiguration.GetSetting(NewCharacterRules.RacesKey, (string?)null);
        try
        {
            // not set to anything this build reads (as when absent from modernuo.json): locked
            ServerConfiguration.SetSetting(NewCharacterRules.HavenStartKey, "");
            ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, "");
            Assert.False(NewCharacterRules.HavenStartOpen);
            Assert.Equal([Race.Human], NewCharacterRules.AllowedRaces.ToArray());
            NewCharacterRules.Configure(); // writes the defaults where absent, never opens them
            Assert.False(NewCharacterRules.HavenStartOpen);
            Assert.Equal([Race.Human], NewCharacterRules.AllowedRaces.ToArray());
        }
        finally
        {
            ServerConfiguration.SetSetting(NewCharacterRules.HavenStartKey, haven ?? "False");
            ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, races ?? NewCharacterRules.DefaultRaces);
        }
    }

    private static List<PetChoiceScroll> ScrollsIn(PlayerMobile pm) =>
        pm.Backpack!.Items.OfType<LootBox>().SelectMany(b => (IEnumerable<Item>?)b.Contents ?? Array.Empty<Item>()).OfType<PetChoiceScroll>().ToList();

    private static PetChoiceScroll? ScrollIn(PlayerMobile pm) => ScrollsIn(pm).FirstOrDefault();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void P762_RunningForTheMoongateBeforeTheLessonsGivesTheAchievementAndTheBoxOnce(bool step)
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        Play(pm, "core.movement"); // one lesson in, ten left
        var gate = Gate(t);
        pm.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);

        if (step)
        {
            Assert.True(gate.OnMoveOver(pm));
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
        }
        else
        {
            Assert.True(TutorialTravelGate.Use(pm, gate.Location));
        }

        Assert.Same(HavenSpots.Map, pm.Map); // the normal way out
        Assert.Equal(HavenSpots.MoongateExit, pm.Location);
        Assert.True(ClusterFAchievementSystem.HasEarned(t.Account, ClusterFTutorial.GateSkipAchievement));
        Assert.False(ClusterFAchievementSystem.HasEarned(t.Account, ClusterFTutorial.SkippedAchievement));
        Assert.Equal(TutorialState.Skipped, t.Record.State);
        Assert.True(TutorialStart.GateSkipped(pm));
        Assert.Equal(1, ClusterFLoops.LoopOf(pm)); // can get to Haven and past it, as a skip
        var scroll = ScrollIn(pm);
        Assert.NotNull(scroll);

        // Once: not eligible again, and the box's source is recorded on the character.
        Assert.False(TutorialGateSkip.Eligible(pm, earlyInLessons: true));
        TutorialGateSkip.Rewards(pm);
        Assert.Single(ScrollsIn(pm));

        // Final: Rook will not send this character back.
        Assert.False(ClusterFTutorial.StartRemedial(pm));
        Assert.Same(HavenSpots.Map, pm.Map);
    }

    [Fact]
    public void P762_AVisitorRunningForTheMoongateGetsItsOwnBox()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var first = t.Add();
        Create(first, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(first);
        var visitor = t.Add();
        Create(visitor, TutorialStart.Choice.Isle);
        Assert.Equal(TutorialStart.Outcome.Visit, TutorialStart.ConsumeAtLogin(visitor));
        var gate = Gate(t);
        visitor.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);
        Assert.True(TutorialTravelGate.Use(visitor, gate.Location));
        Assert.Same(HavenSpots.Map, visitor.Map);
        Assert.NotNull(ScrollIn(visitor));
        Assert.True(TutorialStart.GateSkipped(visitor));
        Assert.Equal(TutorialState.InCore, t.Record.State); // the first character's lessons are untouched
        Assert.Equal((uint)first.Serial, t.Record.CharacterSerial);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void P762_TheScrollGivesADogOrACatThatFollowsWithoutTaming(bool dog)
    {
        using var t = new Trainees();
        var pm = t.Add();
        var taming = pm.Skills.AnimalTaming.Base;
        var scroll = new PetChoiceScroll();
        pm.Backpack!.DropItem(scroll);

        var pet = scroll.Choose(pm, dog);
        Assert.NotNull(pet);
        t.Litter.Add(pet!);
        Assert.IsType(dog ? typeof(Dog) : typeof(Cat), pet);
        Assert.True(pet!.Controlled);
        Assert.Same(pm, pet.ControlMaster);
        Assert.Equal(OrderType.Follow, pet.ControlOrder);
        Assert.False(pet.IsBonded);
        Assert.True(scroll.Deleted);
        Assert.Equal(taming, pm.Skills.AnimalTaming.Base);
        Assert.Equal(1, pm.Followers);
    }

    [Fact]
    public void P762_TheChoiceWindowHasTwoButtonsDogAndCat()
    {
        using var t = new Trainees();
        var pm = t.Add();
        var scroll = new PetChoiceScroll();
        pm.Backpack!.DropItem(scroll);
        var g = new PetChoiceGump(scroll);
        Assert.Equal([1, 2], g.Entries.OfType<GumpButton>().Select(b => b.ButtonID).OrderBy(x => x).ToArray());
        g.OnResponse(pm.NetState!, new RelayInfo(2, default, default, default, default));
        Assert.True(scroll.Deleted);
        var cat = pm.AllFollowers.OfType<Cat>().Single();
        t.Litter.Add(cat);
    }

    [Fact]
    public void P762_AFinisherDoesNotGetTheSkipReward()
    {
        using var locked = new Switches();
        using var t = new Trainees();
        var pm = t.Add();
        Create(pm, TutorialStart.Choice.Isle);
        ClusterFTutorial.OnLogin(pm);
        foreach (var key in TutorialStations.Core.Select(s => s.Key).TakeWhile(k => k != "core.travel"))
        {
            Play(pm, key);
        }

        var gate = Gate(t);
        pm.MoveToWorld(TutorialWorld.Ground(TutorialWorld.DockEnd), TutorialWorld.Isle);
        ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.travel")!);
        Assert.True(TutorialTravelGate.Use(pm, gate.Location));
        Assert.Same(HavenSpots.Map, pm.Map);
        Assert.Equal(TutorialState.InWings, t.Record.State); // the lessons finished, graduation waits at Rook
        Assert.False(ClusterFAchievementSystem.HasEarned(t.Account, ClusterFTutorial.GateSkipAchievement));
        Assert.Null(ScrollIn(pm));
        Assert.False(TutorialStart.GateSkipped(pm));
        Assert.Equal(TutorialStart.ExitLeft, TutorialStart.IsleExit(pm));
    }

    [Fact]
    public void P762_TheBotsStaffCommandMakesOnlyBotStaffAccountsGameMaster()
    {
        var name = $"slbot-staff{Guid.NewGuid():N}"[..20];
        var bot = new Account(name, "p762-test-only");
        var other = new Account($"p762{Guid.NewGuid():N}"[..16], "p762-test-only");
        try
        {
            Assert.StartsWith("Refused", NewCharacterRules.MakeBotStaff(name, "not a test shard"));
            Assert.Equal(AccessLevel.Player, bot.AccessLevel);
            Assert.Contains("Only the playtest bot", NewCharacterRules.MakeBotStaff(other.Username, null));
            Assert.Equal(AccessLevel.Player, other.AccessLevel);
            Assert.Equal($"{name} {NewCharacterRules.BotStaffDone}.", NewCharacterRules.MakeBotStaff(name, null));
            Assert.Equal(AccessLevel.GameMaster, bot.AccessLevel);
        }
        finally
        {
            Accounts.Remove(bot);
            Accounts.Remove(other);
        }
    }
}
