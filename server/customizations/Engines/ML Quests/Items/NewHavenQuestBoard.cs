// The New Haven training board: our own content, not a port. Deviation D-90.
// See shard-migration/notes/new-haven-quest-board.md.
//
// A standing bulletin board that offers the 26 stock New Haven skill-training quests
// (Engines/ML Quests/Definitions/NewHavenSkillTraining.cs) through the stock ML quest path:
// MLQuestSystem.OnDoubleClick picks the quest, the stock QuestOfferGump shows it, and the
// stock CanOffer filters held, completed and past-50 quests. Nothing here decides who gets
// what.
//
// Why it exists: every one of those quests is GainSkillObjective(skill, 500, useReal: true),
// and GainSkillObjective.CanOffer refuses for good once the skill reaches 50.0. With
// ClusterFSkillGain at 3 attempts and 5x gain, that point arrives in minutes, before a new
// player has found the trainers.
//
// What a MODERNUO_COMMIT bump has to re-check:
//   1. QuestGiverItem.OnDoubleClick still only adds the backpack check we drop here. If it
//      gains another guard, OnDoubleClick below silently skips it.
//   2. MLQuestSystem.Register(quest, params Type[]) is still public. Upstream never calls it;
//      every stock quest comes from Data/MLQuests.cfg. NewHavenQuestBoardVerification fails
//      if it stops registering.
//   3. MLQuestInstance still saves its quester by serial only (MLQuestEntry.cs:520, and the
//      TODO at :536). A held quest whose board is gone is dropped from the player's log at the
//      next load, which is why Place() moves a board and never replaces one.

using System;
using ModernUO.Serialization;
using Server.Engines.MLQuests.Definitions;
using Server.Mobiles;

namespace Server.Engines.MLQuests.Items;

[SerializationGenerator(0, false)]
public partial class NewHavenQuestBoard : QuestGiverItem
{
    // The 26 classes deriving MLQuest in pinned NewHavenSkillTraining.cs, in file order.
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
        list.Add("One quest at a time here. Say no to see a different one.");
        list.Add("Bring it back to this board, not the trainer. You can carry 10 quests in all.");
    }

    // The one override. QuestGiverItem requires IsChildOf(from.Backpack), which a board
    // standing in the town square never is. Everything else is the stock body.
    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
        else if (MLQuestSystem.Enabled && CanGiveMLQuest && from is PlayerMobile mobile)
        {
            MLQuestSystem.OnDoubleClick(this, mobile);
        }
    }

    [Usage("PlaceNewHavenQuestBoard")]
    [Description("Places the New Haven training board at the town square, or moves the existing one back there.")]
    private static void PlaceNewHavenQuestBoard_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(Place());
    }

    // Never deletes: a board already in the world is moved, not replaced. Every quest a player
    // holds from a board is saved against that board's serial and is dropped on the next load
    // if the board is gone.
    public static string Place()
    {
        NewHavenQuestBoard existing = null;
        var count = 0;

        foreach (var item in World.Items.Values)
        {
            if (item is NewHavenQuestBoard { Deleted: false } board && board.Map == Map.Trammel)
            {
                existing ??= board;
                count++;
            }
        }

        if (existing == null)
        {
            new NewHavenQuestBoard().MoveToWorld(HomeLocation, Map.Trammel);
            return $"New Haven training board placed at {HomeLocation} Trammel.";
        }

        if (existing.Location != HomeLocation)
        {
            existing.MoveToWorld(HomeLocation, Map.Trammel);
            return $"New Haven training board moved to {HomeLocation} Trammel ({count} on Trammel).";
        }

        return $"New Haven training board already at {HomeLocation} Trammel ({count} on Trammel).";
    }
}
