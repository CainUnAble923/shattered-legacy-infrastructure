// S5 spike: ServUO's JourneyToTheAthenaeumIsleQuest translated onto ModernUO's ML quest
// engine. Source: ServUO Scripts/Quests/JourneyToTheAthenaeumIsleQuest.cs (the quest) and
// Scripts/Mobiles/NPCs/Zhah.cs (the quester).
//
// Why this quest: of ServUO's 408 BaseQuest subclasses it is the ONLY one that uses two
// different standard objective types in one definition (Slay + Obtain). Everything else is
// single-type or uses a hand-written BaseObjective subclass. See notes/s5-quest-spike.md
// section 2 for the scan.
//
// Objective mapping, verbatim from the ServUO source:
//
//   SlayObjective(typeof(MinionOfScelestus), "Minion of Scelestus", 10)
//     -> KillObjective(10, [typeof(MinionOfScelestus)], "Minion of Scelestus")
//
//   ObtainObjective(m_Types[i], m_Names[i], 1)  x10
//     -> CollectObjective(1, m_Types[i], 1150933 + i)  x10, ShowDetailed = false
//
//     ServUO passes plain-English strings ("Obtain Gargish Document - Challenge Rite") to
//     the objective and then throws them away: its RenderObjective override draws clilocs
//     1150933..1150942 instead, one plain line per document. ModernUO has no per-quest
//     gump override, but CollectObjective.ShowDetailed = false renders exactly that same
//     one-line form, so the cliloc goes into the objective where ServUO put the string.
//     The player-visible result is the OSI line, not the developer string.
//
//   BaseReward(typeof(ChronicleOfTheGargoyleQueen1), 1, "Chronicle...")
//     -> ItemReward("Chronicle of the Gargoyle Queen Vol. I", typeof(...))
//
// Deviations are listed in notes/s5-quest-spike.md section 6.

using System;
using ModernUO.Serialization;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Objectives;
using Server.Engines.MLQuests.Rewards;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.MLQuests.Definitions
{
    public class JourneyToTheAthenaeumIsle : MLQuest
    {
        // ServUO stores these as parallel Type[]/string[] tables and renders clilocs
        // 1150933..1150942 for them. Kept in source order so the quest log reads the same.
        private static readonly Type[] _documentTypes =
        {
            typeof(ChallengeRite), typeof(AnthenaeumDecree), typeof(LetterFromTheKing),
            typeof(OnTheVoid), typeof(ShilaxrinarsMemorial), typeof(ToTheHighScholar),
            typeof(ToTheHighBroodmother), typeof(ReplyToTheHighScholar), typeof(AccessToTheIsle),
            typeof(InMemory)
        };

        public JourneyToTheAthenaeumIsle()
        {
            Activated = true;
            Title = 1150929;             // Journey to the Athenaeum Isle
            Description = 1150902;       // Greetings, adventurer. As you know, my people have suffered ...
            RefusalMessage = 1150930;    // Understood. Perhaps you are not as brave as I initially thought.
            InProgressMessage = 1150931; // You have returned. Did you manage to slay the beasts ...
            CompletionMessage = 1150903; // You have returned! I cannot thank you enough ...

            Objectives.Add(new KillObjective(10, new[] { typeof(MinionOfScelestus) }, "Minion of Scelestus"));

            for (var i = 0; i < _documentTypes.Length; i++)
            {
                Objectives.Add(new GargishDocumentObjective(_documentTypes[i], 1150933 + i));
            }

            // Chronicle of the Gargoyle Queen Vol. I
            Rewards.Add(new ItemReward("Chronicle of the Gargoyle Queen Vol. I", typeof(ChronicleOfTheGargoyleQueen1)));
        }

        // ShowDetailed = false is ModernUO's own escape hatch for an objective whose gump
        // line is a single localized sentence rather than "Obtain <n> <item>" plus an item
        // image. It is what ServUO's RenderObjective override hand-draws here, and it also
        // avoids CollectObjective.LabelToItemID deriving a nonsense item id from a cliloc
        // that is not an item label. Precedent: Heritage.cs:102, BlightedGrove.cs:105.
        private class GargishDocumentObjective : CollectObjective
        {
            public GargishDocumentObjective(Type type, int cliloc) : base(1, type, cliloc)
            {
            }

            public override bool ShowDetailed => false;
        }
    }

    // ServUO's MondainQuester declares its quests with an overridden Type[] Quests property.
    // ModernUO maps quester type -> quest list in MLQuestSystem, populated from
    // Distribution/Data/MLQuests.cfg. That file is not a .cs, so apply-patches.sh does not
    // carry it and it would need a .patch against a CRLF data file. MLQuestSystem.Register
    // is a public API for exactly this and runs before MLQuestSystem.Initialize(), because
    // Main.cs invokes every Configure() before any Initialize(). See notes/s5-quest-spike.md
    // section 4.
    public static class AthenaeumIsleQuests
    {
        public static void Configure()
        {
            MLQuestSystem.Register(new JourneyToTheAthenaeumIsle(), typeof(QueenZhah));
        }
    }
}

namespace Server.Mobiles
{
    [QuesterName("Zhah the Gargoyle Queen")]
    [SerializationGenerator(0, false)]
    public partial class QueenZhah : BaseCreature
    {
        [Constructible]
        public QueenZhah() : base(AIType.AI_Vendor, FightMode.None, 2)
        {
            Title = "the Gargoyle Queen";
            Female = true;
            Race = Race.Gargoyle;
            Body = 667;

            SetSpeed(0.5, 2.0);
            InitStats(100, 100, 25);

            SpeechHue = Utility.RandomDyedHue();
            Hue = Race.RandomSkinHue();

            HairItemID = 0x42AB;
            HairHue = Race.RandomHairHue();

            // Deviation D-S5-3: ServUO dresses her in LeatherTalons, GargishLeatherChest,
            // GargishLeatherLegs, GargishClothWingArmor, GargishLeatherArms,
            // GargishLeatherKilt and a SerpentStoneStaff. All seven are absent from pinned
            // ModernUO (~470 ServUO lines across 7 files). Cosmetic only; logged rather
            // than ported, so the spike stays a quest spike.
        }

        public override bool IsInvulnerable => true;
        public override string DefaultName => "Zhah";

        public override bool CanShout => true;

        public override void Shout(PlayerMobile pm)
        {
            // I seek an adventurer to aid me in a matter of some urgency.
            MLQuestSystem.Tell(this, pm, 1150932);
        }
    }
}
