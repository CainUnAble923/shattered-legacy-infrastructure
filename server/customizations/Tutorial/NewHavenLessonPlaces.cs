// cc-P802 items 3, 6 and 8 (release/isle-20261028: the ML plaza's places, notes/cc-P802-tools/lesson-places-release.json) (Chase 11-05: "All lessons should take place in appropriate location on island", "mysticism
// lesson should be at mage building in Haven with an elven adjunct instructor"; 11-07: "necromancy did not point to
// necromancy temple in new haven"). Each New Haven lesson that armed on the open grass of the trades yard or the training
// yard now arms in the building that does its work, on the Siege of Sosaria map's Haven: every tile below was checked on
// the shard's own map files and decoration (shard-migration notes/cc-P802-tools/lesson-places.json, renders in
// notes/cc-P802-review/places/). The lesson's real fixtures are used where the town has them (the smithy's anvils and
// forges, the woodsman's oak, the pier's water); its own props stand on the listed prop tiles otherwise.
//
// Two teachers stand in their halls (seeded with New Haven's pieces, moved back to their spot and facing at every seed):
//   - Ilyra Moonwhisper, an elf, the adjunct instructor of Mysticism, behind the table in the Haven Magery School's south
//     room, facing the door. Elves are not playable (P762); an elf NPC is.
//   - Morvain Ashgrave, keeper of the necromancer's shrine, on the skull-candle dais in the shrine at the north-east of
//     the loop road, facing the room.
// Double-clicking either starts their lesson. The lessons with no fitting building yet (Bushido and Ninjitsu: no dojo;
// combat.healing) stay in the training yard; the notes list them for Chase.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Tutorial
{
    /// <summary>cc-P802: a New Haven lesson's place: where it arms, the words for it, and its prop tiles and fixture.</summary>
    public sealed record HavenLessonPlace(Point3D Arm, string Where, string Short, Point3D[] Props, Point3D? Fixture = null);

    public static partial class TutorialWingTrials
    {
        public static readonly Point3D ShrineEchoSpot = new(3555, 2460, 15);
        public static readonly Point3D MysticEchoSpot = new(3487, 2493, 52);
        public static readonly Point3D MysticInstructorSpot = new(3485, 2491, 52);
        public static readonly Point3D ShrineKeeperSpot = new(3556, 2454, 15);

        public const string ShrineWhere =
            "the Necromancers' Guild Hall, the black stone temple on the hill north-east of town (Mulcivikh and Morganna's hall)";

        public const string MysticWhere = "the New Haven Mage hall, its north room with the crystal ball (the elven instructor stands behind it)";

        /// <summary>cc-P802 item 6: the lesson's building, or null for a lesson that keeps its old place.</summary>
        public static HavenLessonPlace? HavenPlace(string key) => key switch
        {
            "gathering.lumberjacking" => new(new(3433, 2661, 42),
                "the logging clearing of old stumps on the slope south of the New Haven Carpenter, at the south-west edge of town", "the logging clearing",
                [], new Point3D(3432, 2661, 43)),
            "gathering.fishing" => new(new(3504, 2616, 1),
                "the end of the west pier, New Haven Docks", "the docks",
                [], new Point3D(3504, 2617, -5)),
            "gathering.camping" => new(new(3508, 2473, 27),
                "the grass beside the north road out of town, below the hill road to the necromancers' temple", "the camp spot",
                [new(3509, 2472, 27), new(3509, 2474, 27)]),
            "crafting.blacksmithy" => new(new(3468, 2540, 36),
                "George Hephaestus's Forge and Anvil, the fenced smithy yard west of the scribe's, street side", "the Forge and Anvil",
                [new(3469, 2541, 36)]),
            "crafting.tailoring" => new(new(3498, 2547, 20),
                "the New Haven Tailor, the north workroom, west side of the square", "the tailor's",
                []),
            "crafting.carpentry" => new(new(3441, 2637, 28),
                "the New Haven Carpenter, the workshop at the south-west edge of town", "the carpenter's",
                []),
            "crafting.tinkering" => new(new(3456, 2528, 53),
                "Springs N Things, the tinker's shop north of the smithy (Amelia Youngstone's)", "Springs N Things",
                []),
            "crafting.alchemy" => new(new(3459, 2566, 35),
                "the New Haven Alchemist, the shop of bottles by the healer's", "the alchemist's",
                []),
            "crafting.fletching" => new(new(3537, 2532, 20),
                "the bowyers' corner of the Warrior's Guild yard (Andric and Kashiel, New Haven Bowyer)", "the bowyers' corner",
                []),
            "crafting.cooking" => new(new(3465, 2575, 35),
                "the Baker's shop, the bakery next to the alchemist", "the bakery",
                []),
            "crafting.inscription" => new(new(3469, 2528, 48),
                "the scribe's library room, the stone house north of the smithy (New Haven Scribe)", "the scribe's library",
                []),
            "crafting.cartography" => new(new(3494, 2590, 35),
                "the New Haven Mapmaker, the little shop above the docks (shipwright and mapmaker)", "the mapmaker's",
                []),
            "thieving.trial" => new(new(3451, 2566, 30),
                "the back alley behind the tavern, by the fighting pit, where New Haven's thief lurks", "the back alley",
                [new(3452, 2567, 30), new(3452, 2564, 30)]),
            _ => null
        };

        private static Point2D P2(Point3D p) => new(p.X, p.Y);

        private static HavenLessonPlace HavenPlaceOf(string key) => HavenPlace(key)!;
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    /// <summary>cc-P802 items 3 and 8: a New Haven lesson's teacher, standing in their hall; a double-click starts the lesson.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialLessonTeacher : BaseCreature
    {
        [SerializableField(0)]
        private string _lessonKey = "";

        [Constructible]
        public TutorialLessonTeacher() : base(AIType.AI_Vendor, FightMode.None, 2, 1)
        {
            Blessed = true;
            CantWalk = true;
            RangeHome = 0;
        }

        public static TutorialLessonTeacher Mystic()
        {
            var t = new TutorialLessonTeacher { LessonKey = "magic.mysticism", Name = "Ilyra Moonwhisper", Title = "the adjunct instructor of Mysticism" };
            t.Race = Race.Elf;
            t.Female = true;
            t.Body = 0x25E;
            t.Hue = Race.Elf.RandomSkinHue();
            t.HairItemID = 0x2FCD; // long elven hair
            t.HairHue = 0x34;
            t.AddItem(new Robe { Hue = 0x48D });
            t.AddItem(new ElvenBoots { Hue = 0x497 });
            t.Direction = Direction.South; // to the door, across her table
            return t;
        }

        public static TutorialLessonTeacher ShrineKeeper()
        {
            var t = new TutorialLessonTeacher { LessonKey = "magic.necromancy", Name = "Morvain Ashgrave", Title = "keeper of the Necromancers' Guild Hall" };
            t.Body = 0x190;
            t.Hue = 0x83EA;
            t.HairItemID = 0x203C;
            t.HairHue = 0x455;
            t.AddItem(new Robe { Hue = 0x455 });
            t.AddItem(new Sandals { Hue = 0x455 });
            t.Direction = Direction.South; // to the hall, from the foot of the north stair
            return t;
        }

        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;
        public override bool CanTeach => false;

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm || !pm.InRange(this, 4))
            {
                return;
            }

            if (!ClusterFTutorial.IsParticipant(pm) || TutorialStations.Find(LessonKey) is not { } station)
            {
                SayTo(pm, LessonKey == "magic.mysticism"
                    ? "Mysticism draws on the deep magic of the elves. Speak with Rook on his bench by the Albatross if you would take its lesson."
                    : "The dead keep their secrets close. Rook, on his bench by the Albatross, sends trainees to me.");
                return;
            }

            SayTo(pm, LessonKey == "magic.mysticism"
                ? "Welcome to the Mage hall. Stand by me and I will show you Eagle Strike."
                : "Come in, quietly. Stand before the candles and watch.");
            ClusterFTutorial.RunStation(pm, station);
        }
    }
}
