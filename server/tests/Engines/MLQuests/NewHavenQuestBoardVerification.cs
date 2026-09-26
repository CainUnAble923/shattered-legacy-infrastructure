// The New Haven training board (D-90). See shard-migration/notes/new-haven-quest-board.md.
//
// DoubleClickFromTheGroundReachesTheQuestSystem is the fact that goes red if the one override
// regresses: stock QuestGiverItem.OnDoubleClick answers 1042593 "That is not in your
// backpack" and the quest system never runs. It reads what the board actually put on a live
// test NetState.
//
// No fact here sends a gump. The builder stage has no native libdeflate (the runtime image
// installs it, docker/uo/Dockerfile), every gump is deflate-packed on compile, and the first
// attempt kills the test host from a finalizer. So the facts that end in a gump - the offer,
// the progress gump for a held quest, the report-back - are proven in the headless run on the
// real image instead, and the facts here stop at the decision the stock engine makes: the
// messages it sends and MLQuestSystem.RandomStarterQuest, the public method its double-click
// uses to pick the offer.
//
// SkillAtFiftyLocksTheQuestOnTheBoardToo pins the stock lockout, which the board does NOT
// lift (Q-059).

using System;
using System.Collections.Generic;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Items;
using Server.Engines.MLQuests.Objectives;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenQuestBoardVerification
{
    private const int NotInBackpack = 1042593;
    private const int CannotReach = 1019045;
    private const int KnowAllICanTeach = 1077772;
    private const int NothingForYou = 1080107;

    private static readonly Point3D BoardSpot = new(1000, 1000, 0);

    private readonly ITestOutputHelper _out;

    public NewHavenQuestBoardVerification(ITestOutputHelper output)
    {
        _out = output;

        // The server calls MLQuestSystem.Configure; the test host calls no Configure at all.
        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        NewHavenQuestBoard.Register();
    }

    private static MLQuest Quest<T>() where T : MLQuest => MLQuestSystem.FindQuest(typeof(T));

    private static SkillName SkillOf(MLQuest quest) =>
        Assert.IsType<GainSkillObjective>(Assert.Single(quest.Objectives)).Skill;

    private static NewHavenQuestBoard PlaceBoard()
    {
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(BoardSpot, Map.Trammel);
        return board;
    }

    // A player one tile from the board with every board skill at `lockedFixed` except the
    // `open` ones, which sit at 30.0 like a new character's.
    private static PlayerMobile NewPlayer(int lockedFixed, params SkillName[] open)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());

        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var skill = SkillOf(MLQuestSystem.FindQuest(type));
            pm.Skills[skill].BaseFixedPoint = Array.IndexOf(open, skill) >= 0 ? 300 : lockedFixed;
        }

        pm.MoveToWorld(new Point3D(BoardSpot.X + 1, BoardSpot.Y, BoardSpot.Z), Map.Trammel);
        return pm;
    }

    // Every 0xC1 localized message sent on `ns` since `from`, as (speaker serial, cliloc).
    private static List<(uint, int)> Messages(NetState ns, int from)
    {
        var span = ns.SendBuffer.GetReadSpan();
        var found = new List<(uint, int)>();

        for (var i = from; i + 18 <= span.Length; i++)
        {
            if (span[i] != 0xC1)
            {
                continue;
            }

            var len = (span[i + 1] << 8) | span[i + 2];
            if (len < 48 || i + len > span.Length)
            {
                continue;
            }

            var serial = (uint)((span[i + 3] << 24) | (span[i + 4] << 16) | (span[i + 5] << 8) | span[i + 6]);
            var number = (span[i + 14] << 24) | (span[i + 15] << 16) | (span[i + 16] << 8) | span[i + 17];
            found.Add((serial, number));
            i += len - 1;
        }

        return found;
    }

    private static List<(uint, int)> ClickOnTheWire(Item board, PlayerMobile pm, NetState ns)
    {
        var before = ns.SendBuffer.GetReadSpan().Length;
        board.OnDoubleClick(pm);
        return Messages(ns, before);
    }

    private static HashSet<Type> Picks(NewHavenQuestBoard board, PlayerMobile pm, int draws = 200)
    {
        var context = MLQuestSystem.GetContext(pm);
        var seen = new HashSet<Type>();

        for (var i = 0; i < draws; i++)
        {
            var pick = MLQuestSystem.RandomStarterQuest(board, pm, context);

            // RandomStarterQuest falls back to an unofferable quest so its refusal can be
            // spoken; OnDoubleClick then calls CanOffer and sends no offer. Count only offers.
            if (pick?.CanOffer(board, pm, context, false) == true)
            {
                seen.Add(pick.GetType());
            }
        }

        return seen;
    }

    private static void Cleanup(PlayerMobile pm, params Item[] items)
    {
        MLQuestSystem.HandleDeletion(pm);
        pm.NetState = null;
        pm.Delete();

        foreach (var item in items)
        {
            item.Delete();
        }
    }

    [Fact]
    public void BoardIsQuesterForExactlyTheTwentySixNewHavenTrainingQuests()
    {
        var board = new NewHavenQuestBoard();

        Assert.Equal(26, NewHavenQuestBoard.QuestTypes.Length);
        Assert.Equal(26, new HashSet<Type>(NewHavenQuestBoard.QuestTypes).Count);
        Assert.Equal(26, board.MLQuests.Count);
        Assert.True(board.CanGiveMLQuest);
        Assert.False(board.Movable);
        Assert.Equal(0xA0C5, board.ItemID);

        var skills = new HashSet<SkillName>();

        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var quest = MLQuestSystem.FindQuest(type);

            // The same instance the board holds, so trainer and board share one quest object.
            Assert.Contains(quest, board.MLQuests);
            Assert.True(quest.Activated);
            Assert.True(quest.OneTimeOnly);

            var objective = Assert.IsType<GainSkillObjective>(Assert.Single(quest.Objectives));
            Assert.Equal(500, objective.ThresholdFixed);
            Assert.True(objective.UseReal);
            Assert.True(skills.Add(objective.Skill)); // one quest per skill
        }

        // Registering twice must not append a second copy of every quest.
        NewHavenQuestBoard.Register();
        Assert.Equal(26, MLQuestSystem.FindQuestList(typeof(NewHavenQuestBoard)).Count);

        board.Delete();
    }

    [Fact]
    public void DoubleClickFromTheGroundReachesTheQuestSystem()
    {
        var board = PlaceBoard();

        // Every board skill at 50, so the engine answers with a message rather than a gump.
        var pm = NewPlayer(500);
        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;

        Assert.False(board.IsChildOf(pm.Backpack));

        var said = ClickOnTheWire(board, pm, ns);
        _out.WriteLine($"from the ground: {string.Join(", ", said)}");

        // The quest system ran: the stock objective refusal, spoken by the board itself.
        Assert.Contains(((uint)board.Serial, KnowAllICanTeach), said);
        Assert.DoesNotContain(said, m => m.Item2 == NotInBackpack);

        // Out of reach is still refused, before the quest system.
        pm.MoveToWorld(new Point3D(BoardSpot.X + 3, BoardSpot.Y, BoardSpot.Z), Map.Trammel);
        said = ClickOnTheWire(board, pm, ns);
        Assert.Contains(said, m => m.Item2 == CannotReach);
        Assert.DoesNotContain(said, m => m.Item2 == KnowAllICanTeach);

        Cleanup(pm, board);
    }

    [Fact]
    public void OffersEveryUntakenQuestAndNothingElse()
    {
        var board = PlaceBoard();

        // A fresh character: every one of the 26 is offerable.
        var fresh = NewPlayer(0);
        Assert.Equal(new HashSet<Type>(NewHavenQuestBoard.QuestTypes), Picks(board, fresh, 2000));
        Cleanup(fresh);

        // One skill open: the pick has exactly one answer.
        var pm = NewPlayer(500, SkillName.Fencing);
        Assert.Equal(new HashSet<Type> { typeof(EnGuarde) }, Picks(board, pm));

        Cleanup(pm, board);
    }

    [Fact]
    public void HeldQuestIsNeverOfferedAgain()
    {
        var board = PlaceBoard();
        var pm = NewPlayer(500, SkillName.Fencing, SkillName.Mining);
        var fencing = Quest<EnGuarde>();

        Assert.Equal(new HashSet<Type> { typeof(EnGuarde), typeof(TheDeluciansLostMine) }, Picks(board, pm));

        fencing.OnAccept(board, pm);
        var instance = MLQuestSystem.GetContext(pm).FindInstance(fencing);
        Assert.NotNull(instance);
        Assert.Equal(typeof(NewHavenQuestBoard), instance.QuesterType);

        Assert.Equal(new HashSet<Type> { typeof(TheDeluciansLostMine) }, Picks(board, pm));

        Cleanup(pm, board);
    }

    [Fact]
    public void CompletedQuestIsNeverOfferedAgain()
    {
        var board = PlaceBoard();
        var pm = NewPlayer(500, SkillName.Fencing, SkillName.Mining);
        var fencing = Quest<EnGuarde>();

        fencing.OnAccept(board, pm);
        var instance = MLQuestSystem.GetContext(pm).FindInstance(fencing);

        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        Assert.True(instance.IsCompleted());

        // The stock turn-in, minus its gumps.
        instance.ContinueReportBack(false);
        Assert.True(instance.ClaimReward);
        instance.ClaimRewards();
        Assert.True(instance.Removed);
        Assert.True(MLQuestSystem.GetContext(pm).HasDoneQuest(fencing));
        Assert.NotNull(pm.Backpack.FindItemByType<RecarosRiposte>());

        // Back under 50, so only OneTimeOnly stands between the player and a second reward.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 300;
        Assert.False(fencing.CanOffer(board, pm, false));
        Assert.Equal(new HashSet<Type> { typeof(TheDeluciansLostMine) }, Picks(board, pm));

        Cleanup(pm, board);
    }

    [Fact]
    public void SkillAtFiftyLocksTheQuestOnTheBoardToo()
    {
        var board = PlaceBoard();
        var pm = NewPlayer(500, SkillName.Fencing);
        var fencing = Quest<EnGuarde>();

        pm.Skills[SkillName.Fencing].BaseFixedPoint = 499;
        Assert.True(fencing.CanOffer(board, pm, false));
        Assert.Equal(new HashSet<Type> { typeof(EnGuarde) }, Picks(board, pm));

        // At 50.0 the stock objective refuses, from the board exactly as from Recaro.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        Assert.False(fencing.CanOffer(board, pm, false));
        Assert.Empty(Picks(board, pm));

        // Chase's character: Fencing past 50, everything else fresh. Twenty-five on offer.
        var chase = NewPlayer(0);
        chase.Skills[SkillName.Fencing].BaseFixedPoint = 510;
        var picks = Picks(board, chase, 2000);
        Assert.Equal(25, picks.Count);
        Assert.DoesNotContain(typeof(EnGuarde), picks);

        Cleanup(chase);
        Cleanup(pm, board);
    }

    [Fact]
    public void AQuestTakenAtTheBoardIsNotTheTrainersToTakeBack()
    {
        var board = PlaceBoard();
        var pm = NewPlayer(500, SkillName.Fencing);
        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;

        var fencing = Quest<EnGuarde>();
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(BoardSpot.X, BoardSpot.Y + 1, BoardSpot.Z), Map.Trammel);
        Assert.Contains(fencing, recaro.MLQuests);

        fencing.OnAccept(board, pm);

        // The quest's own text says "return to Recaro". Recaro, on the stock path, has
        // nothing for a player already doing his only quest.
        var before = ns.SendBuffer.GetReadSpan().Length;
        MLQuestSystem.OnDoubleClick(recaro, pm);
        var said = Messages(ns, before);
        _out.WriteLine($"Recaro to a board-quest holder: {string.Join(", ", said)}");
        Assert.Contains(((uint)recaro.Serial, NothingForYou), said);

        // Deleting the board does not cancel the quest while the server runs; the instance
        // keeps the board's type, which FindQuest step 1 would match a replacement on. It does
        // not survive a save: MLQuestInstance writes the quester's serial only and drops the
        // quest on load if that board is gone (MLQuestEntry.cs:520, :536-548), which is why
        // NewHavenQuestBoard.Place never deletes a board.
        board.Delete();
        var instance = MLQuestSystem.GetContext(pm).FindInstance(fencing);
        Assert.NotNull(instance);
        Assert.False(instance.Removed);
        Assert.Equal(typeof(NewHavenQuestBoard), instance.QuesterType);

        recaro.Delete();
        Cleanup(pm);
    }
}
