// S5 spike: verification that ServUO's JourneyToTheAthenaeumIsleQuest, translated onto
// ModernUO's ML quest engine, registers, is offered, tracks both of its objective types
// and pays the right reward. See shard-migration/notes/s5-quest-spike.md.
//
// These tests exist to answer one question the build cannot: does ModernUO's engine take
// the same inputs, fire on the same trigger, and produce the same player-visible result
// as ServUO's for each objective kind? Each [Fact] pins one leg of that.
//
// What a MODERNUO_COMMIT bump has to re-run, and why:
//
//   1. Registration goes through MLQuestSystem.Register rather than Data/MLQuests.cfg,
//      because apply-patches.sh carries .cs only. Register is a public API that upstream
//      itself never calls - every stock quest comes from the cfg file - so it is exactly
//      the kind of API that can be quietly dropped or have its ordering changed without
//      breaking anything upstream. QuestIsRegisteredAndOfferedByItsQuester is the only
//      thing that would notice.
//
//   2. KillObjective counting runs through MLQuestSystem.HandleKill, called from
//      BaseCreature.OnDeath. If that call site moves, the build stays green and the quest
//      silently stops advancing.
//
//   3. CollectObjective counts only items flagged QuestItem, recomputed from the backpack
//      on every check rather than from a stored counter. That is a real behavioural
//      difference from ServUO's ObtainObjective, which increments a saved CurProgress.
//      CollectObjectiveCountsOnlyMarkedQuestItems pins the ModernUO semantics.

using System;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Objectives;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AthenaeumIsleQuestVerification
{
    private static readonly Type[] DocumentTypes =
    {
        typeof(ChallengeRite), typeof(AnthenaeumDecree), typeof(LetterFromTheKing),
        typeof(OnTheVoid), typeof(ShilaxrinarsMemorial), typeof(ToTheHighScholar),
        typeof(ToTheHighBroodmother), typeof(ReplyToTheHighScholar), typeof(AccessToTheIsle),
        typeof(InMemory)
    };

    private readonly ITestOutputHelper _out;

    public AthenaeumIsleQuestVerification(ITestOutputHelper output)
    {
        _out = output;

        // Configure() is what the server calls. Re-registering would append a second copy
        // of the quest to QueenZhah's list, so only register when it is absent.
        if (MLQuestSystem.FindQuest(typeof(JourneyToTheAthenaeumIsle)) == null)
        {
            AthenaeumIsleQuests.Configure();
        }
    }

    // This class used to carry eight lines registering a single SpeedLevel.Medium speed row,
    // because no BaseCreature could be constructed in the S4 test host at all. S8 moved that
    // into the route: server/patches/UOContentFixture-npc-speeds.patch loads the real
    // Data/npc-speeds.json for every test, so QueenZhah and MinionOfScelestus below construct
    // with nothing in this file. The workaround is deliberately gone rather than kept
    // alongside - see notes/s8-test-route.md - and apply-patches.sh now fails the build for a
    // test file that registers speeds by hand.

    private static MLQuest Quest => MLQuestSystem.FindQuest(typeof(JourneyToTheAthenaeumIsle));

    private static PlayerMobile NewPlayer()
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        return pm;
    }

    [Fact]
    public void QuestIsRegisteredAndOfferedByItsQuester()
    {
        var quest = Quest;
        Assert.NotNull(quest);
        Assert.True(quest.Activated);

        var zhah = new QueenZhah();
        _out.WriteLine($"quester {zhah.Name} {zhah.Title}: {zhah.MLQuests.Count} quest(s)");

        Assert.Contains(quest, zhah.MLQuests);
        Assert.True(zhah.CanGiveMLQuest);

        var pm = NewPlayer();

        // CanOffer is the gate MLQuestSystem.OnDoubleClick goes through before it sends
        // the offer gump. A fresh player with no context must pass it.
        Assert.True(quest.CanOffer(zhah, pm, false));

        pm.Delete();
        zhah.Delete();
    }

    [Fact]
    public void ObjectivesTranslateOneForOneFromServUO()
    {
        var quest = Quest;

        // ServUO: 1 SlayObjective + 10 ObtainObjective, AllObjectives = true, 1 reward.
        Assert.Equal(11, quest.Objectives.Count);
        Assert.Equal(ObjectiveType.All, quest.ObjectiveType);
        Assert.Single(quest.Rewards);

        var kill = Assert.IsType<KillObjective>(quest.Objectives[0]);
        Assert.Equal(10, kill.DesiredAmount);
        Assert.Equal(new[] { typeof(MinionOfScelestus) }, kill.AcceptedTypes);
        Assert.Equal("Minion of Scelestus", kill.Name.String);

        for (var i = 0; i < DocumentTypes.Length; i++)
        {
            var collect = Assert.IsAssignableFrom<CollectObjective>(quest.Objectives[i + 1]);

            Assert.Equal(1, collect.DesiredAmount);
            Assert.Equal(DocumentTypes[i], collect.AcceptedType);

            // ServUO renders 1150933..1150942 for these ten, one line each, from its own
            // RenderObjective override. ModernUO carries the same clilocs on the objective.
            Assert.Equal(1150933 + i, collect.Name.Number);
            Assert.False(collect.ShowDetailed);
        }

        // ServUO's BaseQuest defaults: DoneOnce false, RestartDelay zero.
        Assert.False(quest.OneTimeOnly);
        Assert.False(quest.HasRestartDelay);
        Assert.True(quest.RequiresCollection);
    }

    [Fact]
    public void KillObjectiveCountsThroughHandleKill()
    {
        var quest = Quest;
        var zhah = new QueenZhah();
        var pm = NewPlayer();

        var instance = quest.CreateInstance(zhah, pm);
        var kill = Assert.IsType<KillObjectiveInstance>(instance.Objectives[0]);

        Assert.Equal(0, kill.Slain);
        Assert.False(kill.IsCompleted());

        // The trigger BaseCreature.OnDeath uses. Ten kills, one at a time, so an
        // off-by-one in AddKill's completion branch would show.
        for (var i = 0; i < 10; i++)
        {
            var minion = new MinionOfScelestus();
            MLQuestSystem.HandleKill(pm, minion);
            minion.Delete();

            Assert.Equal(i + 1, kill.Slain);
        }

        Assert.True(kill.IsCompleted());
        _out.WriteLine($"slain={kill.Slain} completed={kill.IsCompleted()}");

        instance.Remove();
        pm.Delete();
        zhah.Delete();
    }

    [Fact]
    public void CollectObjectiveCountsOnlyMarkedQuestItems()
    {
        var quest = Quest;
        var zhah = new QueenZhah();
        var pm = NewPlayer();

        var instance = quest.CreateInstance(zhah, pm);
        var collect = Assert.IsType<CollectObjectiveInstance>(instance.Objectives[1]);

        var doc = new ChallengeRite();
        pm.Backpack.DropItem(doc);

        // In the pack but unmarked: ModernUO does not count it. ServUO does not either -
        // ObtainObjective.Update is what sets QuestItem and increments, and it is driven by
        // the same player action.
        Assert.False(doc.QuestItem);
        Assert.False(collect.IsCompleted());

        // The trigger: the player toggles quest-item status on the item.
        Assert.True(MLQuestSystem.MarkQuestItem(pm, doc));
        Assert.True(doc.QuestItem);
        Assert.True(collect.IsCompleted());

        // Unmarking must take it back off, as ServUO's toggle does.
        doc.QuestItem = false;
        Assert.False(collect.IsCompleted());

        instance.Remove();
        doc.Delete();
        pm.Delete();
        zhah.Delete();
    }

    [Fact]
    public void QuestCompletesAndPaysTheRightReward()
    {
        var quest = Quest;
        var zhah = new QueenZhah();
        var pm = NewPlayer();

        var instance = quest.CreateInstance(zhah, pm);

        Assert.False(instance.IsCompleted());

        for (var i = 0; i < 10; i++)
        {
            var minion = new MinionOfScelestus();
            MLQuestSystem.HandleKill(pm, minion);
            minion.Delete();
        }

        Assert.False(instance.IsCompleted()); // kills alone are not enough

        foreach (var type in DocumentTypes)
        {
            var doc = type.CreateInstance<Item>();
            pm.Backpack.DropItem(doc);
            Assert.True(MLQuestSystem.MarkQuestItem(pm, doc));
        }

        Assert.True(instance.IsCompleted());

        // The real hand-in is two steps, not one, and they are separated by a gump
        // round-trip that a headless test cannot make:
        //
        //   ContinueReportBack  - the player clicks "Continue" on the report-back gump.
        //                         This is where the collected items are consumed and
        //                         ClaimReward is set.
        //   ClaimRewards        - the player clicks the reward on the reward gump.
        //                         This is where the reward item is created.
        //
        // Setting ClaimReward directly and calling ClaimRewards pays the reward but leaves
        // the quest items in the pack, because consumption lives in the first step. Drive
        // both, in order, exactly as MLQuestSystem.OnDoubleClick would.
        instance.ContinueReportBack(false);
        Assert.True(instance.ClaimReward);

        instance.ClaimRewards();

        var reward = pm.Backpack.FindItemByType<ChronicleOfTheGargoyleQueen1>();
        Assert.NotNull(reward);
        Assert.Equal(500, reward.Charges);
        _out.WriteLine($"reward: {reward.GetType().Name} charges={reward.Charges}");

        // The ten documents are consumed on claim, exactly as ServUO's QuestHelper does.
        foreach (var type in DocumentTypes)
        {
            Assert.Null(pm.Backpack.FindItemByType(type));
        }

        Assert.True(instance.Removed);

        pm.Delete();
        zhah.Delete();
    }
}
