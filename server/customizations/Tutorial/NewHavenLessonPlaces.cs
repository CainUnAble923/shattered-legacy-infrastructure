// cc-P802 items 3, 6 and 8 (Chase 11-05: "All lessons should take place in appropriate location on island", "mysticism
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
        public static readonly Point3D ShrineEchoSpot = new(3688, 2475, 0);
        public static readonly Point3D MysticEchoSpot = new(3654, 2475, 0);
        public static readonly Point3D MysticInstructorSpot = new(3652, 2473, 0);
        public static readonly Point3D ShrineKeeperSpot = new(3693, 2475, 2);

        public const string ShrineWhere =
            "the necromancer's shrine, the stone hall at the north-east of the loop road (enter by its south door, then walk east to the skull candles)";

        public const string MysticWhere = "the Haven Magery School, the stone hall at the north end of town (the elven instructor in its south room)";

        /// <summary>cc-P802 item 6: the lesson's building, or null for a lesson that keeps its old place.</summary>
        public static HavenLessonPlace? HavenPlace(string key) => key switch
        {
            "gathering.lumberjacking" => new(new(3744, 2699, 40),
                "the Woodsman's lodge on the hill at the south-east edge of town (the oak tree south of its door)", "the Woodsman's lodge",
                [], new Point3D(3744, 2700, 40)),
            "gathering.fishing" => new(new(3650, 2667, -2),
                "the west pier of the docks, south of the docks street", "the docks", [], new Point3D(3651, 2667, -5)),
            "gathering.camping" => new(new(3709, 2606, 20),
                "the camp spot on the grass beside the road to the east fields, south of the Proving Grounds", "the camp spot",
                [new(3710, 2605, 20), new(3711, 2607, 20)]),
            "crafting.blacksmithy" => new(new(3645, 2616, 0),
                "the Hammer and Steel smithy, the open forge on the street south of the bank", "the smithy", [new(3646, 2617, 0)]),
            "crafting.tailoring" => new(new(3676, 2475, 0),
                "A Stitch in Time, the west room of the long stone house at the north-east of the loop road", "A Stitch in Time", []),
            "crafting.carpentry" => new(new(3642, 2499, 0),
                "Carpenters of Haven, the workshop behind the counter, on the north side of the loop road", "the carpenters", []),
            "crafting.tinkering" => new(new(3666, 2506, 0),
                "Haven Clockworks and Tinker Shop, on the north side of the loop road east of the carpenters", "the tinker shop", []),
            "crafting.alchemy" => new(new(3628, 2547, 0),
                "the reagent kitchen at the back of the mage's shop, on the courtyard north of the bank", "the reagent kitchen", []),
            "crafting.fletching" => new(new(3745, 2579, 40),
                "the Bullseye Marksmen archery range, east of the Proving Grounds", "the archery range", []),
            "crafting.cooking" => new(new(3667, 2643, 0),
                "the kitchen of the Albatross Bar and Grill, on the docks street", "the Albatross kitchen", []),
            "crafting.inscription" => new(new(3612, 2475, 0),
                "the Haven Public Library, at the reading tables, at the north-west corner of the loop road", "the library", []),
            "crafting.cartography" => new(new(3630, 2634, 0),
                "Mapmakers of Haven, the stone house on the north side of the docks street", "the mapmakers", []),
            "thieving.trial" => new(new(3696, 2509, 0),
                "the Haven Thieves' Guild, the hall at the east end of the loop road", "the Thieves' Guild",
                [new(3697, 2509, 0), new(3699, 2509, 0)]),
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
            var t = new TutorialLessonTeacher { LessonKey = "magic.necromancy", Name = "Morvain Ashgrave", Title = "keeper of the necromancer's shrine" };
            t.Body = 0x190;
            t.Hue = 0x83EA;
            t.HairItemID = 0x203C;
            t.HairHue = 0x455;
            t.AddItem(new Robe { Hue = 0x455 });
            t.AddItem(new Sandals { Hue = 0x455 });
            t.Direction = Direction.West; // to the room, over the candles
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
                ? "Welcome to the Magery School. Stand by me and I will show you Eagle Strike."
                : "Come in, quietly. Stand before the candles and watch.");
            ClusterFTutorial.RunStation(pm, station);
        }
    }
}
