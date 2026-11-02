// cc-P769: the first outside playtester's notes (11-01), the Wakening Isle part. Notes: shard-migration
// notes/cc-P769-playtester-ux-notes-1.md.
//
// Facts, each seen failing first (on a mutant that undoes its part; notes/cc-P769-tools/):
//   1. The wake hint says how to get up (right mouse button, or an arrow key).
//   2. The backpack lesson's steps are in the order the client shows them: one click on yourself, Open Paperdoll, the
//      backpack picture, the blue practice bag.
//   3. Lessons carry one-line client tips (hover, the game view and black space, the Journal, doors and gates, seeing in
//      buildings), shown under the hint in the lesson window; the optional client tips page has them too.
//   4. "lesson" said aloud (no bracket) opens the lesson window; the hint says the bracket is part of [lesson and names
//      Tamsin. Anyone not in the lessons is not answered.
//   5. The League lesson says what to do with Elara; on the isle she does not say New Haven's "register today".
//   6. A fresh trainee who walks into the bank before starting the bank lesson and says "bank" gets the lesson and the
//      blue token at once, and finishes it by depositing the token.
//   7. The death lesson's steps are logged (no assertion on the log; the steps still run).
//   8. Each isle lesson grants its own achievement once per account; a New Haven lesson grants none; claiming a quest's
//      rewards grants Gainfully Employed and, for a New Haven trainer quest, The Tutorial Has a Checkbox at once.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Items;
using Server.Engines.MLQuests.Objectives;
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
public class PlaytesterNotesVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;

    public PlaytesterNotesVerification()
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
        ClusterFTutorial.Enabled = true;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p769{Guid.NewGuid():N}"[..16], "p769-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x191, Female = true, Name = "Paige", RawStr = 60, RawDex = 60, RawInt = 60 };
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
            ClusterFFanfare.FlushAll();
            foreach (var m in Pm.AllFollowers?.ToList() ?? new List<Mobile>())
            {
                m.Delete();
            }

            MLQuestSystem.HandleDeletion(Pm);
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    /// <summary>Into the tutorial on the isle, and up off the sand.</summary>
    private static void Awake(Player p)
    {
        TutorialTestStart.Begin(p.Pm);
        Assert.True(TutorialWake.OnMoveRequest(p.Pm));
        TutorialWake.Stand(p.Pm, true);
    }

    private static void Say(PlayerMobile pm, string text) => pm.DoSpeech(text, [], MessageType.Regular, 0x3B2);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void P769_TheWakeHintSaysHowToGetUp()
    {
        Assert.Contains("get up", TutorialWake.MoveHintLine);
        Assert.Contains("right mouse button", TutorialWake.MoveHintLine);
        Assert.Contains("arrow key", TutorialWake.MoveHintLine);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void P769_TheBackpackStepsAreInTheOrderTheClientShowsThem()
    {
        var hint = TutorialCoreTrials.Hint("core.backpack");
        var order = new[] { "click your character once", "Open Paperdoll", "backpack picture", "blue practice bag" }
            .Select(s => hint.IndexOf(s, StringComparison.Ordinal)).ToArray();
        Assert.All(order, i => Assert.True(i >= 0, hint));
        Assert.Equal(order.OrderBy(i => i), order);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void P769_LessonsCarryClientTipsUnderTheHint()
    {
        Assert.Contains("hold the mouse over", TutorialCoreTrials.Tip("core.backpack"));
        Assert.Contains("black space", TutorialCoreTrials.Tip("core.movement"));
        Assert.Contains("Journal", TutorialCoreTrials.Tip("core.chat"));
        Assert.Contains("double-click a door or gate", TutorialCoreTrials.Tip("core.farm"));
        Assert.Contains("circle of transparency", TutorialCoreTrials.Tip("core.league"));

        var gump = new TutorialTrialGump(TutorialStations.Find("core.chat")!, false);
        Assert.Equal(TutorialCoreTrials.Tip("core.chat"), gump.Tip);
        Assert.Null(new TutorialTrialGump(TutorialStations.Find("core.chat")!, true).Tip); // a finished lesson's window

        var titles = TutorialClientTips.Tips.Select(t => t.Title).ToList();
        Assert.Contains("Read chat again", titles);
        Assert.Contains("See inside buildings", titles);
        Assert.Contains("A bigger game view", titles);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void P769_SayingLessonOpensTheLessonWindow()
    {
        Assert.Contains("[lesson (with the bracket)", TutorialCoreTrials.ReopenLine);
        Assert.Contains("Tamsin", TutorialCoreTrials.ReopenLine);

        using var p = new Player();
        Awake(p);
        p.Pm.CloseGump<TutorialTrialGump>();
        Assert.Null(p.Pm.FindGump<TutorialTrialGump>());
        Say(p.Pm, "lesson");
        Assert.NotNull(p.Pm.FindGump<TutorialTrialGump>());

        p.Pm.CloseGump<TutorialTrialGump>();
        Say(p.Pm, "Lesson.");
        Assert.NotNull(p.Pm.FindGump<TutorialTrialGump>());

        p.Pm.CloseGump<TutorialTrialGump>();
        Say(p.Pm, "a lesson for later"); // only the word alone
        Assert.Null(p.Pm.FindGump<TutorialTrialGump>());
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void P769_TheLeagueLessonSaysWhatToDoWithElara()
    {
        var hint = TutorialCoreTrials.Hint("core.league");
        Assert.Contains("Double-click Elara Voss", hint);
        Assert.Contains("choose Talk", hint);
        Assert.Contains("Join the League", hint);
        Assert.Contains("lesson is done", hint);
        Assert.Contains("Join the League", LeagueRegistrar.LessonWindowLine);
        Assert.Contains("Talk", TutorialGreeting.ElaraLine);

        var elara = new LeagueRegistrar();
        try
        {
            elara.MoveToWorld(new Point3D(TutorialWorld.Elara.X, TutorialWorld.Elara.Y, 0), TutorialWorld.Isle);
            Assert.True(elara.OnTheIsle); // so New Haven's "register today" is not said there
            elara.MoveToWorld(new Point3D(3662, 2549, 0), Map.Trammel);
            Assert.False(elara.OnTheIsle);
        }
        finally
        {
            elara.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void P769_SayingBankInTheBankStartsTheBankLessonWithItsToken()
    {
        using var p = new Player();
        Awake(p);
        TutorialCoreTrials.Cancel(p.Pm);
        foreach (var station in TutorialStations.Core.TakeWhile(s => s.Key != "core.banking"))
        {
            p.Record.StationsDone.Add(station.Key);
        }

        Assert.Equal("core.banking", ClusterFTutorial.CoreLeft(p.Record).First().Key);
        Assert.Null(TutorialCoreTrials.Current(p.Pm)); // the lesson not started: walked in past its signpost

        p.Pm.MoveToWorld(new Point3D(TutorialWorld.Banker.X, TutorialWorld.Banker.Y + 1, p.Pm.Z), p.Pm.Map);
        Assert.True(TutorialCorePlacement.InBank(p.Pm.Location));
        Say(p.Pm, "bank");

        Assert.True(TutorialCoreTrials.IsDoing(p.Pm, "core.banking"));
        var token = p.Pm.Backpack!.FindItemByType<TutorialBankToken>();
        Assert.NotNull(token);

        // The token gone missing: "bank" again gives another.
        token.Delete();
        Say(p.Pm, "bank");
        token = p.Pm.Backpack!.FindItemByType<TutorialBankToken>();
        Assert.NotNull(token);

        p.Pm.BankBox.DropItem(token);
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Contains("core.banking", p.Record.StationsDone);
    }

    [Fact]
    public void P769_BankSaidOutsideTheBankOrOutOfOrderStartsNothing()
    {
        using var p = new Player();
        Awake(p);
        TutorialCoreTrials.Cancel(p.Pm);

        // In the bank, but the bank lesson is not next.
        p.Pm.MoveToWorld(new Point3D(TutorialWorld.Banker.X, TutorialWorld.Banker.Y + 1, p.Pm.Z), p.Pm.Map);
        Say(p.Pm, "bank");
        Assert.Null(TutorialCoreTrials.Current(p.Pm));
        Assert.Null(p.Pm.Backpack!.FindItemByType<TutorialBankToken>());
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void P769_EachIsleLessonGrantsItsOwnAchievementOnce()
    {
        using var p = new Player();
        Awake(p);
        TutorialCoreTrials.Cancel(p.Pm);

        var core = TutorialStations.Core.ToList();
        Assert.Equal(11, core.Count);
        foreach (var station in core)
        {
            var key = TutorialLessonAchievements.KeyFor(station.Key)!;
            Assert.True(ClusterFAchievementSystem.Definitions.ContainsKey(key), key);
            Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, key));
            ClusterFTutorial.CompleteStation(p.Pm, station.Key);
            Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, key), key);
            Assert.False(TutorialLessonAchievements.Grant(p.Pm, station.Key)); // once per account
        }

        Assert.Equal(11, core.Select(s => TutorialLessonAchievements.KeyFor(s.Key)).Distinct().Count());
        Assert.Null(TutorialLessonAchievements.KeyFor("crafting.tinkering")); // a New Haven lesson has none
        Assert.Null(TutorialLessonAchievements.KeyFor(TutorialClientTips.Key));
    }

    [Fact]
    public void P769_ClaimingAQuestGrantsTheFirstQuestAchievementAtOnce()
    {
        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        NewHavenQuestBoard.Register();

        using var p = new Player();
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(new Point3D(p.Pm.X + 1, p.Pm.Y, p.Pm.Z), p.Pm.Map);
        try
        {
            var quest = MLQuestSystem.FindQuest(typeof(ItsHammerTime));
            Assert.NotNull(quest);
            quest.OnAccept(board, p.Pm);
            foreach (var objective in quest.Objectives.OfType<GainSkillObjective>())
            {
                p.Pm.Skills[objective.Skill].BaseFixedPoint = 500;
            }

            var instance = MLQuestSystem.GetOrCreateContext(p.Pm).FindInstance(quest);
            Assert.NotNull(instance);
            Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFQuestMilestones.FirstQuestKey));
            Assert.False(ClusterFAchievementSystem.HasEarned(p.Account, "legacy.new_haven_1"));

            instance.ContinueReportBack(false);
            instance.ClaimRewards();

            Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, ClusterFQuestMilestones.FirstQuestKey));
            Assert.True(ClusterFAchievementSystem.HasEarned(p.Account, "legacy.new_haven_1")); // not waiting for a login
        }
        finally
        {
            board.Delete();
        }
    }
}
