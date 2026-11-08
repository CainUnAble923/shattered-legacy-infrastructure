// cc-P802: New Haven tutorial notes, batch 2 (Chase playing 2026.11.03.1, and the isle wake of 11-07). Notes:
// shard-migration notes/cc-P802-haven-notes-2.md.
//
//   1. Lessons teach in their window: a tour stop's lesson, a lesson's explained skill and its last line stay in the window.
//   2. The travel lesson: a blank rune, the lesson's rune to Rook's bench, a Mark scroll and two Recall scrolls; marked,
//      away, back; Mark is allowed on Haven Island only while it runs.
//   4. New Haven's old farm goes at every start by itself.
//   7. The wake is one window: the wake text, how to get up, the young status and other login notes; the starting skills'
//      caps are set at creation (no "Your skill in ... has increased" at the first login); the client's own open-chat
//      request at login is not answered with "The chat system has been disabled."

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterCreation;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HavenNotes2Verification : IDisposable
{
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public HavenNotes2Verification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWingTrials.EnsureHooks();
        TutorialWingTrials.Fits = _ => true;
    }

    public void Dispose() => TutorialWingTrials.Fits = _fits;

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p802{Guid.NewGuid():N}"[..16], "p802-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at, bool tutorial = true)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            if (tutorial)
            {
                Assert.True(ClusterFTutorial.TryEnter(Pm));
            }
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;
        public TutorialWingTrials.Trial Trial => TutorialWingTrials.Current(Pm)!;

        public void Dispose()
        {
            TutorialWingTrials.Cancel(Pm);
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static TutorialWingTrials.Trial Begin(Player p, string key)
    {
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(key)!);
        var trial = p.Trial;
        Assert.Equal(key, trial.Key);
        Assert.True(trial.Armed);
        return trial;
    }

    private static void Tick() => ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));

    private static List<T> Held<T>(PlayerMobile pm) where T : Item
    {
        var list = new List<T>();
        foreach (var item in pm.Backpack!.FindItemsByType<T>()) { list.Add(item); }
        return list;
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void P802_TheTravelLessonHandsRunesAndScrollsAndFinishesOnTheReturn()
    {
        using var p = new Player(HavenSpots.PublicMoongate);
        var mageryBefore = p.Pm.Skills.Magery.Value;
        var trial = Begin(p, TutorialWingTrials.TravelKey);

        var runes = Held<RecallRune>(p.Pm);
        Assert.Equal(2, runes.Count);
        var own = Assert.Single(runes, r => !r.Marked);
        var lessons = Assert.Single(runes, r => r.Marked);
        Assert.Same(own, trial.TravelRune);
        Assert.Equal(TutorialWingTrials.TravelLessonTarget, lessons.Target);
        Assert.Equal(Map.Trammel, lessons.TargetMap);
        Assert.Equal(1, Held<MarkScroll>(p.Pm).Sum(s => s.Amount));
        Assert.Equal(2, TutorialWingTrials.RecallScrollsLeft(p.Pm));
        Assert.True(p.Pm.Skills.Magery.Value >= 100); // the scrolls always work in the lesson
        Assert.Equal(0, p.Pm.Skills.Magery.Base);     // a loan, never the player's own

        // The steps are in the window, with the runebook note.
        var gump = p.Pm.FindGump<TutorialWingTrialGump>();
        Assert.NotNull(gump);
        Assert.Contains("Mark scroll", gump.Text);
        Assert.Contains("Recall scroll", gump.Text);
        Assert.Equal(TutorialWingTrials.RunebookLine, gump.Note);
        Assert.Contains("runebook", TutorialWingTrials.RunebookLine);

        // Recalling to Rook first counts for nothing: the rune is not marked yet.
        p.Pm.MoveToWorld(TutorialWingTrials.TravelLessonTarget, Map.Trammel);
        Tick();
        Assert.Empty(trial.Steps);
        p.Pm.MoveToWorld(HavenSpots.PublicMoongate, Map.Trammel);

        own.Mark(p.Pm);
        Tick();
        Assert.Contains("mark", trial.Steps);

        Held<RecallScroll>(p.Pm)[0].Consume();
        p.Pm.MoveToWorld(TutorialWingTrials.TravelLessonTarget, Map.Trammel); // the recall to Rook's bench
        Tick();
        Assert.Contains("away", trial.Steps);
        Assert.NotNull(TutorialWingTrials.Current(p.Pm)); // 66 tiles from the moongate: still in the lesson

        Assert.DoesNotContain(TutorialWingTrials.TravelKey, p.Record.StationsDone);
        Held<RecallScroll>(p.Pm)[0].Consume();
        p.Pm.MoveToWorld(own.Target, Map.Trammel); // the recall home
        Tick();
        Assert.Contains(TutorialWingTrials.TravelKey, p.Record.StationsDone);
        Assert.Empty(Held<RecallRune>(p.Pm)); // the loans went back
        Assert.Equal(0.0, p.Pm.Skills.Magery.Base);
        Assert.Equal(mageryBefore, p.Pm.Skills.Magery.Value); // the loan went back
        Assert.Empty(TutorialWingTrials.Current(p.Pm)?.SkillMods ?? []);
    }

    [Fact]
    public void P802_MarkIsAllowedOnTheIslandOnlyDuringTheTravelLesson()
    {
        using var p = new Player(HavenSpots.PublicMoongate);
        Assert.False(TutorialIslandGate.CanTravel(p.Pm, Map.Trammel, p.Pm.Location, TravelCheckType.Mark));
        Begin(p, TutorialWingTrials.TravelKey);
        Assert.True(TutorialIslandGate.CanTravel(p.Pm, Map.Trammel, p.Pm.Location, TravelCheckType.Mark));
        // Recall still keeps a trainee on the island.
        Assert.False(TutorialIslandGate.CanTravel(p.Pm, Map.Trammel, new Point3D(1434, 1699, 0), TravelCheckType.RecallTo));
        TutorialWingTrials.Cancel(p.Pm);
        Assert.False(TutorialIslandGate.CanTravel(p.Pm, Map.Trammel, p.Pm.Location, TravelCheckType.Mark));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void P802_ATourStopsLessonStaysInTheWindow()
    {
        using var p = new Player(new Point3D(TutorialWingTrials.TourCentre.X, TutorialWingTrials.TourCentre.Y, 0));
        var trial = Begin(p, "housing.basics");
        p.Pm.MoveToWorld(TutorialWingTrials.HousingStop(0), Map.Trammel);
        Tick();
        Assert.Equal(TutorialWingTrials.HousingStop1, trial.Latest);
        Assert.Equal(TutorialWingTrials.HousingStop1, p.Pm.FindGump<TutorialWingTrialGump>()!.Latest);
        Assert.DoesNotContain("System window", TutorialWingTrials.Trade("housing.basics")!.Hint);
    }

    [Theory]
    [InlineData("combat.weapons")]
    [InlineData("gathering.camping")]
    [InlineData("crafting.blacksmithy")]
    [InlineData("thieving.trial")]
    public void P802_ALessonsExplainedSkillIsInItsWindow(string key)
    {
        var station = TutorialStations.Find(key)!;
        var note = TutorialWingTrials.NoteOf(key);
        Assert.False(string.IsNullOrEmpty(note));
        var gump = new TutorialWingTrialGump(station, -1, false, true, null, note, null);
        Assert.Equal(note, gump.Note);
        Assert.Contains("lesson window", TutorialWingTrials.EchoLine(station));
    }

    // ---------------------------------------------------------------- 7, the isle lessons: a few words in the System window

    [Fact]
    public void P802_AnIsleLessonsSystemLineOnlySaysWhereItsStepsAre()
    {
        foreach (var station in TutorialStations.Core)
        {
            var echo = TutorialCoreTrials.EchoLine(station);
            Assert.Contains("lesson window", echo);
            Assert.True(echo.Length < TutorialCoreTrials.Hint(station.Key).Length || echo.Length < 90, station.Key);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void P802_TheOldFarmGoesAtEveryStartByItself()
    {
        var map = HavenSpots.Map;
        var testShard = TutorialFarm.IsTestShard;
        TutorialFarm.IsTestShard = () => true;
        try
        {
            Assert.False(TutorialFarm.SeedHaven(false, true).Refused); // an older world's farm
        }
        finally
        {
            TutorialFarm.IsTestShard = testShard;
        }

        Assert.NotNull(TutorialFarm.FindAddon<TutorialFarmAddon>(map, TutorialFarmLayout.Anchor));
        ClusterFTutorial.Enabled = false; // the tutorial seed would skip the world; the farm goes anyway
        try
        {
            Assert.Contains("took away", TutorialFarm.ClearHavenOnLoad());
        }
        finally
        {
            ClusterFTutorial.Enabled = true;
        }

        Assert.Null(TutorialFarm.FindAddon<TutorialFarmAddon>(map, TutorialFarmLayout.Anchor));
        Assert.Null(TutorialFarm.FindDoor(map, TutorialFarmLayout.DoorSpot));
        Assert.Equal("old New Haven farm: took away 0 thing(s).", TutorialFarm.ClearHavenOnLoad());
    }

    // ---------------------------------------------------------------- 7

    private static bool _creationHooks;

    [Fact]
    public void P802_TheStartingSkillsGetTheShardsCapsAtCreation()
    {
        if (!_creationHooks)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _creationHooks = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        ClusterFSkillCaps.ReloadForTests();
        var account = new Account($"p802cap{Guid.NewGuid():N}"[..16], "p802-test-only");
        var ns = PacketTestUtilities.CreateTestNetState();
        var stats = ns.NewCharacterCreation ? new byte[] { 30, 30, 30 } : new byte[] { 30, 25, 25 };
        var city = new CityInfo("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel);
        (SkillName, byte)[] skills = [(SkillName.Swords, 30), (SkillName.Tactics, 30), (SkillName.Anatomy, 30), (SkillName.Healing, 30)];
        var args = new CharacterCreatedEventArgs(ns, account, "Capped", false, 0, stats, city, skills, 0, 0, 0, 0, 0, 0, 0, Race.Human);
        CharacterCreation.CharacterCreatedEvent(args);
        var m = args.Mobile;
        try
        {
            Assert.NotNull(m);
            var cap = (int)Math.Round(ClusterFSkillCaps.IndividualSkillCap * 10);
            // Every cap is the shard's already, so the first login's pass (ClusterFSkillCaps.OnLogin) changes none and sends
            // no single-skill update: TazUO printed one "has increased by 30.0" line per starting skill.
            for (var i = 0; i < m.Skills.Length; i++)
            {
                Assert.Equal(cap, m.Skills[i].CapFixedPoint);
            }
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void P802_TheClientsOwnChatRequestAtLoginIsNotAnswered()
    {
        using var p = new Player(HavenSpots.Start, tutorial: false);
        TutorialWelcome.OnLogin(p.Pm);
        Assert.True(TutorialWelcome.IsLoginChatRequest(p.Pm));
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialWelcome.QuietChatSeconds + 1));
        Assert.False(TutorialWelcome.IsLoginChatRequest(p.Pm)); // a player pressing Chat later still hears it is off
    }

    [Fact]
    public void P802_ALoginNoteGoesToTheSystemWindowWhenNotWaking()
    {
        using var p = new Player(HavenSpots.Start, tutorial: false);
        Assert.False(TutorialWelcome.Note(p.Pm, ClusterFYoungPlayer.LoginMessage(TimeSpan.FromDays(3))));
        Assert.Empty(TutorialWelcome.NotesOf(p.Pm));
    }

    // ---------------------------------------------------------------- 6

    public static IEnumerable<object[]> MovedLessons() =>
        TutorialWingTrials.Trades.Where(t => TutorialWingTrials.HavenPlace(t.Key) != null).Select(t => new object[] { t.Key });

    [Theory]
    [MemberData(nameof(MovedLessons))]
    public void P802_AMovedLessonArmsInItsBuildingNotOnTheOpenYard(string key)
    {
        var place = TutorialWingTrials.HavenPlace(key)!;
        Assert.Equal(new Point2D(place.Arm.X, place.Arm.Y), TutorialWingTrials.Trade(key)!.Place);
        Assert.Contains(place.Where, TutorialWingTrials.FollowLine(key));
        Assert.DoesNotContain("trades yard", TutorialStations.Find(key)!.Where);

        using (var yard = new Player(new Point3D(TutorialWingTrials.TradesYard.X, TutorialWingTrials.TradesYard.Y, 0)))
        {
            ClusterFTutorial.RunStation(yard.Pm, TutorialStations.Find(key)!);
            Assert.False(yard.Trial.Armed); // the old open yard is not its place any more
        }

        using var p = new Player(place.Arm);
        var trial = Begin(p, key);
        Assert.Equal(place.Short, TutorialWingTrials.PointAt(trial).Name);
        Assert.All(trial.Props.Where(i => i.Parent == null), i => Assert.True(i.GetDistanceToSqrt(place.Arm) <= 3)); // a chest's purse is inside it
    }

    // ---------------------------------------------------------------- 3 and 8

    [Fact]
    public void P802_MysticismIsInTheMagerySchoolWithItsElvenAdjunctInstructor()
    {
        Assert.Equal(new Point3D(3485, 2494, 52), TutorialWingTrials.PlaceOf("magic.mysticism"));
        Assert.Contains("Mage hall", TutorialWingTrials.FollowLine("magic.mysticism"));
        Assert.Contains("Mage hall", TutorialStations.Find("magic.mysticism")!.Where);
        Assert.Equal(TutorialWingTrials.MysticEchoSpot, TutorialWingTrials.TargetOf("magic.mysticism"));

        TutorialWingTrials.FindTeacher("magic.mysticism")?.Delete();
        TutorialWingTrials.FindTeacher("magic.necromancy")?.Delete();
        Assert.Contains("placed 2", TutorialWingTrials.SeedTeachers(Map.Trammel, false));
        var ilyra = TutorialWingTrials.FindTeacher("magic.mysticism")!;
        try
        {
            Assert.Same(Race.Elf, ilyra.Race);
            Assert.Equal(0x25E, ilyra.Body.BodyID);
            Assert.Contains("adjunct instructor of Mysticism", ilyra.Title);
            Assert.Equal(TutorialWingTrials.MysticInstructorSpot, ilyra.Location);
            Assert.Equal(Direction.South, ilyra.Direction);
            Assert.True(ilyra.IsInvulnerable);
            ilyra.Direction = Direction.North; // an older world's, turned: the next seed puts her right
            Assert.Contains("placed 0, moved 1", TutorialWingTrials.SeedTeachers(Map.Trammel, false));
            Assert.Equal(Direction.South, ilyra.Direction);

            using var p = new Player(TutorialWingTrials.PlaceOf("magic.mysticism"));
            ilyra.OnDoubleClick(p.Pm);
            Assert.Equal("magic.mysticism", p.Trial.Key);
            Assert.True(p.Trial.Armed);
            Assert.Equal(TutorialWingTrials.MysticEchoSpot, p.Trial.Target!.Location);
        }
        finally
        {
            TutorialWingTrials.FindTeacher("magic.mysticism")?.Delete();
            TutorialWingTrials.FindTeacher("magic.necromancy")?.Delete();
        }
    }

    [Fact]
    public void P802_NecromancyLeadsToTheShrineWithItsKeeper()
    {
        Assert.Equal(new Point3D(3555, 2463, 15), TutorialWingTrials.PlaceOf("magic.necromancy"));
        Assert.Contains("Necromancers' Guild Hall", TutorialWingTrials.FollowLine("magic.necromancy"));
        Assert.Contains("Necromancers' Guild Hall", TutorialStations.Find("magic.necromancy")!.Where);
        Assert.Equal(TutorialWingTrials.ShrineEchoSpot, TutorialWingTrials.TargetOf("magic.necromancy"));

        // The release has no school gate (P714): the lesson runs at the hall, with its echo there.
        using (var there = new Player(TutorialWingTrials.PlaceOf("magic.necromancy")))
        {
            var trial = Begin(there, "magic.necromancy");
            Assert.Equal(TutorialWingTrials.ShrineEchoSpot, trial.Target!.Location);
        }

        TutorialWingTrials.FindTeacher("magic.necromancy")?.Delete();
        TutorialWingTrials.SeedTeachers(Map.Trammel, false);
        var keeper = TutorialWingTrials.FindTeacher("magic.necromancy")!;
        try
        {
            Assert.Equal(TutorialWingTrials.ShrineKeeperSpot, keeper.Location);
            Assert.Equal(Direction.South, keeper.Direction);
        }
        finally
        {
            TutorialWingTrials.FindTeacher("magic.mysticism")?.Delete();
            keeper.Delete();
        }
    }
}
