// The New Haven training board: our own content, not a port. Deviation D-90.
// See shard-migration/notes/new-haven-quest-board.md and notes/new-haven-quest-picker.md.
//
// A standing bulletin board that offers the 26 stock New Haven skill-training quests
// (Engines/ML Quests/Definitions/NewHavenSkillTraining.cs) through the stock ML quest path.
// Double-clicking it opens our own menu listing all 26 with their state (Q-058). Picking one
// calls the stock path on the chosen quest: quest.SendOffer for one on offer, and the stock
// progress, report-back or reward gump on the player's own instance for one they hold. The
// offer, accept, progress, report-back and reward gumps, the quest text, the rewards, the
// 50-point lockout and the 10-quest cap are all stock. Only the choosing is ours.
//
// Why it exists: every one of those quests is GainSkillObjective(skill, 500, useReal: true),
// and GainSkillObjective.CanOffer refuses for good once the skill reaches 50.0. With
// ClusterFSkillGain at 3 attempts and 5x gain, that point arrives in minutes, before a new
// player has found the trainers.
//
// The menu holds no state. Nothing here is serialized beyond what the class saved before the
// menu existed: a player's held board quest is saved against the board's serial, so the save
// shape is pinned byte for byte by NewHavenQuestBoardVerification.SaveShapeIsUnchanged.
//
// What a MODERNUO_COMMIT bump has to re-check:
//   1. QuestGiverItem.OnDoubleClick still only adds the backpack check we drop here. If it
//      gains another guard, CanUse below silently skips it.
//   2. MLQuestSystem.Register(quest, params Type[]) is still public. Upstream never calls it;
//      every stock quest comes from Data/MLQuests.cfg. NewHavenQuestBoardVerification fails
//      if it stops registering.
//   3. MLQuestInstance still saves its quester by serial only (MLQuestEntry.cs:520, and the
//      TODO at :536). A held quest whose board is gone is dropped from the player's log at the
//      next load, which is why Place() moves a board and never replaces one.
//   4. GetRowState and OnPick restate two private pieces of MLQuestSystem (FindQuest and
//      OnDoubleClick, MLQuestSystem.cs:315-411): step 1's "held from this quester" predicate,
//      and the ClaimReward / IsCompleted / progress branch for a held quest. If either changes
//      upstream, the menu's copy does not follow.
//   5. The gump system checks a response's button id only for legacy Gump, not DynamicGump
//      (GumpSystem.IncomingPackets.cs). The picker bounds-checks its own ids and re-verifies
//      everything at the click; do not remove that on the strength of a gump-system check.

using System;
using ModernUO.Serialization;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Objectives;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.MLQuests.Items;

[SerializationGenerator(0, false)]
public partial class NewHavenQuestBoard : QuestGiverItem
{
    // The 26 classes deriving MLQuest in pinned NewHavenSkillTraining.cs, in file order. The
    // menu's rows and button ids follow this order; it is code, not a snapshot of anyone's state.
    public static readonly Type[] QuestTypes =
    {
        typeof(CleansingOldHaven), typeof(TheRudimentsOfSelfDefense), typeof(CrushingBonesAndTakingNames),
        typeof(SwiftAsAnArrow), typeof(EnGuarde), typeof(TheArtOfWar), typeof(TheWayOfTheBlade),
        typeof(ThouAndThineShield), typeof(DefyingTheArcane), typeof(StoppingTheWorld),
        typeof(ScribingArcaneKnowledge), typeof(TheMagesApprentice), typeof(ScholarlyTask),
        typeof(TheRightToolForTheJob), typeof(KnowThineEnemy), typeof(BruisesBandagesAndBlood),
        typeof(TheInnerWarrior), typeof(TheArtOfStealth), typeof(BecomingOneWithTheShadows),
        typeof(WalkingSilently), typeof(EyesOfARanger), typeof(TheWayOfTheSamurai),
        typeof(TheAllureOfDarkMagic), typeof(ChannelingTheSupernatural), typeof(TheDeluciansLostMine),
        typeof(ItsHammerTime)
    };

    // The corner of the New Haven town square, one tile from where every new character
    // arrives (CharacterCreation: 3503, 2574, 14 Trammel, where Sir Helper stands).
    public static readonly Point3D HomeLocation = new(3502, 2573, 14);

    public const int UseRange = 2;

    [Constructible]
    public NewHavenQuestBoard() : base(0xA0C5) // standing bulletin board
    {
        Movable = false;
    }

    public override string DefaultName => "New Haven training board";

    public static void Configure()
    {
        Register();
        CommandSystem.Register("PlaceNewHavenQuestBoard", AccessLevel.Administrator, PlaceNewHavenQuestBoard_OnCommand);
    }

    public static void Register()
    {
        if (MLQuestSystem.FindQuestList(typeof(NewHavenQuestBoard)).Count != 0)
        {
            return;
        }

        foreach (var type in QuestTypes)
        {
            // Reuse the instance the trainers already hold from Data/MLQuests.cfg, so the board
            // and the NPC share one quest object and one set of done-quest records.
            var quest = MLQuestSystem.FindQuest(type) ?? type.CreateInstance<MLQuest>();
            MLQuestSystem.Register(quest, typeof(NewHavenQuestBoard));
        }
    }

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        // Five lines at most: ObjectPropertyList has five "~1_NOTHING~" string slots.
        list.Add("Get these early! When a skill reaches 50, its quest is gone for good.");
        list.Add("Your whole skill counts, not just what you gain after. At 45, you only need 5 more.");
        list.Add("The number in your skill list is what counts, and magic items raise it. Take quests first.");
        list.Add("Double-click for the Guild Directory. All 26 quests are its second tab.");
        list.Add("Bring them back to this board, not the trainer. You can carry 10 quests in all.");
    }

    // QuestGiverItem requires IsChildOf(from.Backpack), which a board standing in the town
    // square never is. Everything else is the stock body's checks, then our menu in place of
    // MLQuestSystem.OnDoubleClick's single pick.
    //
    // cc-P15 (F-9 Decision 5): the board's first page is the Guild Directory; the menu of 26 is its
    // second tab, "Training quests", unchanged. The board is the same item at the same spot.
    public override void OnDoubleClick(Mobile from)
    {
        if (CanUse(from) && from is PlayerMobile pm)
        {
            pm.CloseGump<GuildProgressGump>();
            pm.SendGump(new GuildProgressGump(pm, pm.Account as Accounting.IAccount, this));
        }
    }

    // The directory's "Training quests" tab: the menu, behind the same checks as a double-click.
    public void OpenTrainingQuests(PlayerMobile pm)
    {
        if (CanUse(pm))
        {
            pm.SendGump(new NewHavenQuestPickerGump(this, pm));
        }
    }

    // The checks a double-click runs, run again for every button that comes back from the
    // menu, because the menu is a snapshot the client can answer at any time.
    public bool CanUse(Mobile from)
    {
        if (Deleted || from?.Deleted != false)
        {
            return false;
        }

        if (Map == null || Map == Map.Internal || from.Map != Map || !from.InRange(GetWorldLocation(), UseRange))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return false;
        }

        // The stock gates: QuestGiverItem.OnDoubleClick, then MLQuestSystem.OnDoubleClick.
        return MLQuestSystem.Enabled && CanGiveMLQuest && from is PlayerMobile { Alive: true };
    }

    public enum RowState
    {
        Available,      // not held, and the stock pick would offer it
        InProgress,     // held from this board, objective not met
        ReadyToTurnIn,  // held from this board, objective met or reward waiting
        TakenElsewhere, // held, but from a trainer: it goes back to them, not here
        Done,           // turned in; OneTimeOnly
        LockedAtFifty,  // the objective refuses: the skill is at 50 or more
        NoRoom,         // would be on offer, but the player already holds MaxConcurrentQuests
        NotOffered      // the stock CanOffer refuses for some other reason
    }

    // Every state is read off a stock predicate. Available is exactly RandomStarterQuest's
    // eligible pool (MLQuestSystem.cs:663-695): not chain-triggered, not being done, and
    // quest.CanOffer. Every other not-held state is a case in which quest.CanOffer is false,
    // labelled by which of its own checks refuses.
    public RowState GetRowState(PlayerMobile pm, MLQuestContext context, MLQuest quest, out MLQuestInstance instance)
    {
        instance = context?.FindInstance(quest);

        if (instance != null)
        {
            // FindQuest step 1 (MLQuestSystem.cs:327-331), private upstream, restated.
            if (instance.Quester != this && (quest.IsEscort || instance.QuesterType != GetType()))
            {
                return RowState.TakenElsewhere;
            }

            return instance.ClaimReward || instance.IsCompleted() ? RowState.ReadyToTurnIn : RowState.InProgress;
        }

        if (quest.IsChainTriggered)
        {
            return RowState.NotOffered;
        }

        if (quest.OneTimeOnly && context?.HasDoneQuest(quest) == true)
        {
            return RowState.Done;
        }

        foreach (var objective in quest.Objectives)
        {
            if (!objective.CanOffer(this, pm, false))
            {
                return RowState.LockedAtFifty;
            }
        }

        if (context?.IsFull == true)
        {
            return RowState.NoRoom;
        }

        return quest.CanOffer(this, pm, context, false) ? RowState.Available : RowState.NotOffered;
    }

    // A button from the menu. Trusts nothing from it but which row was pressed, and resolves
    // that row against the world as it is now.
    public void OnPick(PlayerMobile pm, Type questType)
    {
        if (!CanUse(pm))
        {
            return;
        }

        var quest = MLQuestSystem.FindQuest(questType);

        if (quest == null || !MLQuests.Contains(quest))
        {
            return;
        }

        var context = MLQuestSystem.GetContext(pm);

        switch (GetRowState(pm, context, quest, out var instance))
        {
            case RowState.Available:
                {
                    quest.SendOffer(this, pm);
                    break;
                }
            case RowState.InProgress:
            case RowState.ReadyToTurnIn:
                {
                    // MLQuestSystem.OnDoubleClick's branch for a held quest, on the player's own instance.
                    if (instance.Failed)
                    {
                        return;
                    }

                    if (instance.ClaimReward)
                    {
                        instance.SendRewardOffer();
                    }
                    else if (instance.IsCompleted())
                    {
                        instance.SendReportBackGump();
                    }
                    else
                    {
                        instance.SendProgressGump();
                    }

                    break;
                }
            case RowState.TakenElsewhere:
                {
                    pm.SendMessage(0x3B2, "You took that quest from a trainer. Take it back to them.");
                    break;
                }
            default:
                {
                    // The stock refusal, spoken by the board: 1075454, 1077772 or 1080107.
                    quest.CanOffer(this, pm, context, true);

                    if (context?.IsFull == true)
                    {
                        pm.SendMessage(0x3B2, FullMessage);
                    }

                    break;
                }
        }
    }

    public static readonly string FullMessage =
        $"You already carry {MLQuestSystem.MaxConcurrentQuests} quests, the most you can. Finish or drop one to take another.";

    [Usage("PlaceNewHavenQuestBoard")]
    [Description("Places the New Haven training board at the town square, or moves the existing one back there.")]
    private static void PlaceNewHavenQuestBoard_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(Place());
    }

    // Never deletes: a board already in the world is moved, not replaced. Every quest a player
    // holds from a board is saved against that board's serial and is dropped on the next load
    // if the board is gone. Searches every map, Map.Internal and containers included: a board
    // the search cannot see is a board Place() would duplicate.
    public static string Place()
    {
        NewHavenQuestBoard existing = null;
        var onTrammel = 0;
        var elsewhere = 0;

        foreach (var item in World.Items.Values)
        {
            if (item is not NewHavenQuestBoard { Deleted: false } board)
            {
                continue;
            }

            if (board.Map == Map.Trammel)
            {
                onTrammel++;
            }
            else
            {
                elsewhere++;
            }

            if (existing == null || PlaceRank(board) > PlaceRank(existing))
            {
                existing = board;
            }
        }

        var found = $"found {onTrammel} on Trammel, {elsewhere} elsewhere";
        var extra = onTrammel + elsewhere > 1 ? " More than one board exists; the others were left where they are." : "";

        if (existing == null)
        {
            new NewHavenQuestBoard().MoveToWorld(HomeLocation, Map.Trammel);
            return $"New Haven training board placed at {HomeLocation} Trammel ({found}).";
        }

        if (PlaceRank(existing) == 2)
        {
            return $"New Haven training board already at {HomeLocation} Trammel ({found}).{extra}";
        }

        var from = existing.Parent != null ? "a container" : $"{existing.Location} {existing.Map}";
        existing.MoveToWorld(HomeLocation, Map.Trammel);
        return $"New Haven training board moved to {HomeLocation} Trammel from {from} ({found}).{extra}";
    }

    // Which board Place() keeps: one already home, then one standing on Trammel, then any.
    private static int PlaceRank(NewHavenQuestBoard board) =>
        board.Map != Map.Trammel || board.Parent != null ? 0 : board.Location == HomeLocation ? 2 : 1;
}

// The menu. It holds the board and the player it was built for and nothing else: every row is
// computed when the gump is compiled, and every button is re-verified when it comes back.
public class NewHavenQuestPickerGump : DynamicGump
{
    // 26 rows do not fit one screen. Two client-side pages of 13: paging costs no round trip
    // and no server state, and a gump has no scrolling container that can hold buttons.
    public const int RowsPerPage = 13;
    public const int Width = 520;
    private const int RowTop = 80;
    private const int RowHeight = 22;
    public const int Height = RowTop + RowsPerPage * RowHeight + 45;

    private readonly NewHavenQuestBoard _board;
    private readonly PlayerMobile _player;

    public NewHavenQuestPickerGump(NewHavenQuestBoard board, PlayerMobile player) : base(50, 50)
    {
        _board = board;
        _player = player;
    }

    public override bool Singleton => true;

    public static int PageCount => (NewHavenQuestBoard.QuestTypes.Length + RowsPerPage - 1) / RowsPerPage;

    public static bool HasButton(NewHavenQuestBoard.RowState state) =>
        state is NewHavenQuestBoard.RowState.Available or NewHavenQuestBoard.RowState.InProgress
            or NewHavenQuestBoard.RowState.ReadyToTurnIn;

    public static string Label(NewHavenQuestBoard.RowState state) =>
        state switch
        {
            NewHavenQuestBoard.RowState.Available      => "Available",
            NewHavenQuestBoard.RowState.InProgress     => "Taken, in progress",
            NewHavenQuestBoard.RowState.ReadyToTurnIn  => "Ready to turn in",
            NewHavenQuestBoard.RowState.TakenElsewhere => "Taken from a trainer",
            NewHavenQuestBoard.RowState.Done           => "Done",
            NewHavenQuestBoard.RowState.LockedAtFifty  => "Past 50: gone",
            NewHavenQuestBoard.RowState.NoRoom         => "No room: 10 held",
            _                                          => "Not offered"
        };

    private static int Hue(NewHavenQuestBoard.RowState state) =>
        state switch
        {
            NewHavenQuestBoard.RowState.Available     => 0x44,
            NewHavenQuestBoard.RowState.InProgress    => 0x35,
            NewHavenQuestBoard.RowState.ReadyToTurnIn => 0x59,
            NewHavenQuestBoard.RowState.LockedAtFifty => 0x26,
            NewHavenQuestBoard.RowState.NoRoom        => 0x26,
            _                                         => 0x3B2
        };

    public const string FullBanner = "That is the most you can carry. Finish or drop one to take another.";

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var context = MLQuestSystem.GetContext(_player);
        var held = context?.QuestInstances.Count ?? 0;

        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 9270);
        builder.AddLabel(20, 15, 0x481, "New Haven training board");

        var carry = $"Pick a quest. You carry {held} of {MLQuestSystem.MaxConcurrentQuests}.";
        builder.AddLabel(20, 38, 0x3B2, carry);

        if (context?.IsFull == true)
        {
            builder.AddLabel(20, 58, 0x26, FullBanner);
        }

        var types = NewHavenQuestBoard.QuestTypes;

        for (var page = 1; page <= PageCount; page++)
        {
            builder.AddPage(page);

            for (var row = 0; row < RowsPerPage; row++)
            {
                var index = (page - 1) * RowsPerPage + row;

                if (index >= types.Length)
                {
                    break;
                }

                var quest = MLQuestSystem.FindQuest(types[index]);

                if (quest == null)
                {
                    continue;
                }

                var y = RowTop + row * RowHeight;
                var state = _board.GetRowState(_player, context, quest, out _);

                if (HasButton(state))
                {
                    builder.AddButton(15, y, 0xFA5, 0xFA7, index + 1);
                }

                foreach (var objective in quest.Objectives)
                {
                    if (objective is GainSkillObjective skill)
                    {
                        builder.AddHtmlLocalized(50, y + 2, 125, 20, AosSkillBonuses.GetLabel(skill.Skill), 0x7FFF);
                        break;
                    }
                }

                quest.Title.AddHtmlText(ref builder, 180, y + 2, 195, 20, numberColor: 0x7FFF, stringColor: 0xFFFFFF);
                builder.AddLabel(385, y + 2, Hue(state), Label(state));
            }

            var navY = RowTop + RowsPerPage * RowHeight + 10;

            if (page < PageCount)
            {
                builder.AddButton(Width - 45, navY, 0xFA5, 0xFA7, 0, GumpButtonType.Page, page + 1);
                builder.AddLabel(Width - 125, navY + 2, 0x3B2, "More quests");
            }

            if (page > 1)
            {
                builder.AddButton(15, navY, 0xFAE, 0xFB0, 0, GumpButtonType.Page, page - 1);
                builder.AddLabel(50, navY + 2, 0x3B2, "Back");
            }
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        // 0 is the client closing the gump. Anything outside the rows is not a row, whatever
        // the client says; DynamicGump responses are not checked against the layout upstream.
        var index = info.ButtonID - 1;

        if (sender.Mobile is not PlayerMobile pm || pm != _player ||
            (uint)index >= (uint)NewHavenQuestBoard.QuestTypes.Length)
        {
            return;
        }

        _board.OnPick(pm, NewHavenQuestBoard.QuestTypes[index]);
    }
}
