// cc-P291: the tutorial's wing trials, part 2: gathering, the crafting yard, animals, bard, thieving and the four tours.
// Notes: shard-migration notes/cc-P291-tutorial-wing-trials-trades.md; the station list: notes/cc-P80-new-player-tutorial.md.
//
// The other part of TutorialWingTrials (P290's TutorialWingTrials.cs): the same Start, page, arrow, loans, Tick, Reset,
// Stop and returns. What this part adds is one row per station (Trades) and three kinds of evidence:
//   - a skill step: a successful roll of that skill (SkillEvents.SkillUsed) while the trial is armed. A harvest only
//     rolls when it yields (HarvestSystem.FinishHarvesting: the resource is given after a successful CheckSkill); the
//     camping roll lights the fire; taming, lore, herding, veterinary, the bard's three and the thief's ten each roll on
//     the real use, on the trial's goat, echoes, mark and chest.
//   - "made": a craft's product turned up in the backpack (more of the product type than when the lesson began, loaners
//     not counted). Materials and the tool are lent; the product is the trainee's to keep (F-35: craft it, bank it).
//   - a tour stop: standing near each stop of a tour in turn (the arrow points at the next).
// Places: the trades yard (TradesYard, open ground south-east of P290's training yard, checked standable from the map
// files) for gathering, crafting, bard and thieving; the tutorial farm's field beside the pen for animals (P276); New
// Haven itself for the tours. Every trial puts its own things beside the trainee when it arms (a vein, a tree, a pond,
// an anvil and forge, a chest, a mark, a goat, two echoes) and takes them away with the loans. Props are named
// PropPrefix and, like loaners, any left by a restart are deleted when the world loads.
//
// Covered and explained skills: Blacksmithy also rolls Arms Lore (on the dagger), Tinkering Item Identification (on a
// curio), Alchemy Taste Identification (on an apple). Camping picks wild garlic (cc-P804), Blacksmithy Imbuing, thieving Forensic
// Evaluation (one line each). Stealth needs a Hiding base of 30 (pinned Stealth.cs:77), which a skill mod cannot give, so
// the thieving trial lifts the Hiding base to 30 for the lesson and puts it back at the end, keeping any gain made.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Tutorial
{
    public static partial class TutorialWingTrials
    {
        public const string PropPrefix = "tutorial prop: ";
        public const int StopRange = 4;
        public const int TourReach = 160;

        /// <summary>The trades yard: open ground south-east of the training yard (map files, notes cc-P291).</summary>
        public static readonly Point2D TradesYard = new(3426, 2626);

        /// <summary>The farm field beside the pen's west gap (P276's layout: pen x 3393-3398, y 2604-2608).</summary>
        public static readonly Point2D FarmField = new(3390, 2606);
        public static readonly Point2D GoatSpot = new(3395, 2606);

        public static readonly Point2D TourCentre = new(3460, 2590);

        public sealed record TradeTrial(
            string Key,
            string Where,                 // "Follow the arrow to ..."
            Point2D Place,                // where it arms (tours: anywhere in New Haven)
            (string Id, string Label)[] Steps,
            string Hint,
            string? Line = null,          // said when it starts: an explained skill
            Type[]? Products = null,      // crafting: the "made" step's product
            (Func<Point3D> Spot, string Name, string Text)[]? Stops = null)
        {
            public bool IsTour => Stops != null;
        }

        private const string Made = "made";
        private const string AtYard = "the trades yard, southeast of the training yard";

        private static (string, string) S(SkillName skill, string label) => (skill.ToString(), label);

        public const string HerbalismLine =
            "Herbalism is taught by the Order of the Alembic. Pick the wild garlic beside you, then double-click kindling to light a campfire.";
        public const string ImbuingLine =
            "Imbuing adds magical properties to equipment at a soul forge. This tutorial does not teach it.";
        public const string ForensicLine =
            "Forensic Evaluation can reveal who looted a body or picked a lock. This tutorial explains it without giving you a practice task.";

        public static readonly TradeTrial[] Trades =
        [
            // -- Gathering --
            // cc-P743 item 3: at the mine in the mountains south of town, on its own rock walls (no practice vein).
            new("gathering.mining", MiningWhere, new Point2D(HavenSpots.MiningCave.X, HavenSpots.MiningCave.Y),
                [S(SkillName.Mining, "Mining: dig ore from the rock wall")],
                "Double-click the loaned pickaxe in your backpack (your own pickaxe or shovel works too), then click the rock wall of the ravine beside you. Repeat until you dig up ore, which you can keep."),
            new("gathering.lumberjacking", HavenPlaceOf("gathering.lumberjacking").Where, P2(HavenPlaceOf("gathering.lumberjacking").Arm), // cc-P802 item 6
                [S(SkillName.Lumberjacking, "Lumberjacking: chop logs from the willow tree")],
                "Double-click the loaned hatchet, then click the willow tree beside you, in the logging clearing. Repeat until you get logs, which you can keep."),
            new("gathering.fishing", HavenPlaceOf("gathering.fishing").Where, P2(HavenPlaceOf("gathering.fishing").Arm), // cc-P802 item 6
                [S(SkillName.Fishing, "Fishing: catch something off the pier")],
                "Double-click the loaned fishing pole in your backpack, then click the sea just south of the pier's end. Repeat until you catch something, which you can keep."),
            new("gathering.camping", HavenPlaceOf("gathering.camping").Where, P2(HavenPlaceOf("gathering.camping").Arm), // cc-P802 item 6
                [S(SkillName.Camping, "Camping: light a campfire with kindling"), S(SkillName.Herbalism, "Herbalism: pick the wild garlic beside you")],
                "Double-click the wild garlic beside you to pick it. Double-click the loaned kindling in your backpack to light a campfire beside you.",
                HerbalismLine),

            // -- The crafting yard --
            new("crafting.blacksmithy", HavenPlaceOf("crafting.blacksmithy").Where, P2(HavenPlaceOf("crafting.blacksmithy").Arm), // cc-P802 item 6
                [(Made, "Blacksmithy: make a dagger at the anvil"), S(SkillName.ArmsLore, "Arms Lore: use on the dagger you made")],
                "Beside the anvil and forge, double-click the loaned smith's hammer, choose Bladed, then make a Dagger. Open Skills on your character window, use Arms Lore, and click your dagger.",
                ImbuingLine, [typeof(Dagger)]),
            new("crafting.tailoring", HavenPlaceOf("crafting.tailoring").Where, P2(HavenPlaceOf("crafting.tailoring").Arm), // cc-P802 item 6
                [(Made, "Tailoring: sew a skullcap")],
                "Double-click the loaned sewing kit in your backpack. Choose Hats, then make a Skullcap with the loaned cloth.",
                null, [typeof(SkullCap)]),
            new("crafting.carpentry", HavenPlaceOf("crafting.carpentry").Where, P2(HavenPlaceOf("crafting.carpentry").Arm), // cc-P802 item 6
                [(Made, "Carpentry: make barrel staves")],
                "Double-click the loaned saw in your backpack. Choose Other, then make Barrel Staves with the loaned boards.",
                null, [typeof(BarrelStaves)]),
            new("crafting.tinkering", HavenPlaceOf("crafting.tinkering").Where, P2(HavenPlaceOf("crafting.tinkering").Arm), // cc-P802 item 6
                [(Made, "Tinkering: make scissors"), S(SkillName.ItemID, "Item Identification: use on the odd blue curio")],
                "Double-click the loaned tinker's tools, choose Tools, then make Scissors. Open Skills on your character window, use Item Identification, and click the odd blue curio in your backpack.",
                null, [typeof(Scissors)]),
            new("crafting.alchemy", HavenPlaceOf("crafting.alchemy").Where, P2(HavenPlaceOf("crafting.alchemy").Arm), // cc-P802 item 6
                [(Made, "Alchemy: make a refresh potion"), S(SkillName.TasteID, "Taste Identification: use on the loaned apple")],
                "Double-click the loaned mortar and pestle, choose Refresh, then make a Refresh Potion. Open Skills on your character window, use Taste Identification, and click the loaned apple.",
                null, [typeof(RefreshPotion)]),
            new("crafting.fletching", HavenPlaceOf("crafting.fletching").Where, P2(HavenPlaceOf("crafting.fletching").Arm), // cc-P802 item 6
                [(Made, "Fletching: make kindling")],
                "Double-click the loaned fletcher's tools in your backpack. Choose Materials, then make Kindling with the loaned logs.",
                null, [typeof(Kindling)]),
            new("crafting.cooking", HavenPlaceOf("crafting.cooking").Where, P2(HavenPlaceOf("crafting.cooking").Arm), // cc-P802 item 6
                [(Made, "Cooking: make dough")],
                "Double-click the loaned skillet in your backpack. Choose Ingredients, then make Dough with the loaned flour and water.",
                null, [typeof(Dough)]),
            new("crafting.inscription", HavenPlaceOf("crafting.inscription").Where, P2(HavenPlaceOf("crafting.inscription").Arm), // cc-P802 item 6
                [(Made, "Inscription: write a Magic Arrow scroll")],
                "Double-click the loaned scribe's pen in your backpack. Choose First Circle, then make a Magic Arrow scroll with the loaned supplies.",
                null, [typeof(MagicArrowScroll)]),
            new("crafting.cartography", HavenPlaceOf("crafting.cartography").Where, P2(HavenPlaceOf("crafting.cartography").Arm), // cc-P802 item 6
                [(Made, "Cartography: draw a local map")],
                "Double-click the loaned mapmaker's pen in your backpack. Choose Maps, then make a Local Map on a loaned blank map.",
                null, [typeof(LocalMap)]),

            // -- Animals (the farm, P276) --
            // cc-P743 item 4: an injured horse in the stables' sick-bay stall; the trainee keeps it (NewHavenLessons.cs).
            new("animals.taming", StablesWhere, new Point2D(HavenSpots.Stables.X, HavenSpots.Stables.Y),
                [
                    S(SkillName.Veterinary, "Veterinary: bandage the injured horse"),
                    S(SkillName.AnimalTaming, "Animal Taming: tame the healed horse")
                ],
                "In the stall, double-click a bandage, then click the injured horse. When it is healed, stand beside it, open Skills on your character window, use Animal Taming, and click the horse. Use Animal Lore on it any time to see its health."),

            // -- Bard --
            new("bard.trial", BardWhere, new Point2D(HavenSpots.Theater.X, HavenSpots.Theater.Y), // cc-P743 item 7
                [
                    S(SkillName.Peacemaking, "Peacemaking: use on a blue training echo"),
                    S(SkillName.Provocation, "Provocation: use on one echo, then the other"),
                    S(SkillName.Discordance, "Discordance: use on a blue training echo")
                ],
                "Open Skills on your character window and use Peacemaking, Provocation, and Discordance on the two blue human figures. Choose the loaned lute when asked; for Provocation, click one figure, then the other."),

            // -- Thieving and stealth --
            new("thieving.trial", HavenPlaceOf("thieving.trial").Where, P2(HavenPlaceOf("thieving.trial").Arm), // cc-P802 item 6
                [
                    S(SkillName.DetectHidden, "Detecting Hidden: use on the wooden practice chest"),
                    S(SkillName.RemoveTrap, "Remove Trap: use on the wooden practice chest"),
                    S(SkillName.Lockpicking, "Lockpicking: use a loaned lockpick on the chest"),
                    S(SkillName.Stealing, "Stealing: empty your hands and steal the purse"),
                    S(SkillName.Snooping, "Snooping: double-click the mark's backpack"),
                    S(SkillName.Begging, "Begging: use on the blue practice person"),
                    S(SkillName.Poisoning, "Poisoning: select the loaned poison, then the dagger"),
                    S(SkillName.Tracking, "Tracking: use the skill and choose a category"),
                    S(SkillName.Hiding, "Hiding: use the skill until you hide"),
                    S(SkillName.Stealth, "Stealth: hide, then use Stealth")
                ],
                "Use Skills in your character window for the checklist tasks on the wooden chest and blue practice person. For Snooping, double-click the person, then its backpack; snooping and poisoning can lower your karma.",
                ForensicLine),

            // -- The tours --
            // cc-P743 item 9 (Chase 10-30, "housing tutorial is not clear"): three stops, one goal each, a signpost at each
            // stop, and each stop's text ends with where to go next.
            new("housing.basics", "the small cottage west of the square (stop 1 of 3)", TourCentre,
                [("stop0", "Stop 1: look at a small house"), ("stop1", "Stop 2: learn where houses can be built"), ("stop2", "Stop 3: the bank, where a deed is kept")],
                "Follow the arrow. At each stop, stand beside its signpost and read the lesson here in this window; then the arrow points to the next stop.",
                Stops:
                [
                    (() => HousingStop(0), "stop 1, the small cottage", HousingStop1),
                    (() => HousingStop(1), "stop 2, the lawn past the cottage", HousingStop2),
                    (() => ClusterFNewHavenSeeder.BankAnchor, "stop 3, the bank", HousingStop3)
                ]),
            // cc-P802 item 2 (Chase 11-05: "the lesson should give scrolls to have player mark/recall"): at the public
            // moongate the trainee marks a blank rune, recalls away to Rook's bench with the lesson's own rune, and recalls
            // back with their own; the return finishes it (TickTravel). The scrolls cast without Magery: the lesson lends
            // Magery 100 and +30 Intelligence (a skill mod, never saved), so each scroll always works; a gain is then very
            // unlikely, never more than a scroll gives. Everything is a loan and goes back at the end.
            new("travel.wing", TravelWhere, new Point2D(HavenSpots.PublicMoongate.X, HavenSpots.PublicMoongate.Y),
                [
                    ("mark", "Mark: read the Mark scroll and click your blank rune, here by the moongate"),
                    ("away", "Recall away: read a Recall scroll and click the lesson's rune to Rook's bench"),
                    ("back", "Recall back: read your other Recall scroll and click your own rune")
                ],
                TravelHint),
            new("reputation.wing", "the first stop", TourCentre,
                [("stop0", "The training yard"), ("stop1", "The healer"), ("stop2", "Rook's bench")],
                "Follow the arrow to the first stop. Walk close to each stop to read its lesson.",
                Stops:
                [
                    (() => YardSpot, "the training yard",
                        "Blue names usually mean innocent; gray names can be attacked, and red names mark murderers. Harming an innocent can make you gray for a while."),
                    (() => HavenSpots.Healer, "the healer",
                        "Karma and fame affect your title; killing innocent players can give you murder counts. Five murder counts make your name red, and some healers will refuse to help you."),
                    (() => TutorialRecruiter.Spot, "Rook's bench",
                        "In a guarded town, say 'guards' if someone attacks you unlawfully. Trammel generally prevents attacks on other players; Felucca allows them.")
                ]),
            new("shard.systems", "the first stop", TourCentre,
                [("stop0", "The League field office"), ("stop1", "The bank"), ("stop2", "Rook")],
                "Follow the arrow to the first stop. Walk close to each stop to read its lesson.",
                Stops:
                [
                    (() => ClusterFNewHavenSeeder.RegistrarAnchor, "the League field office",
                        "Double-click Elara Voss to read about the League's jobs, ranks, and rewards. You can choose whether to join."),
                    (() => ClusterFNewHavenSeeder.BankAnchor, "the bank",
                        "Double-click reward boxes in guarded towns or your own house to open them. TazUO also has Autoloot, Scavenger, Nearby loot, and vendor shopping tools."),
                    (() => TutorialRecruiter.Spot, "Rook",
                        "Achievements reward your progress; type [accessories to see your extra jewelry slots. Graduation records your first loop, but level resets and higher level caps are not active yet.")
                ]),
        ];

        // -- cc-P802 item 2: Mark and Recall --

        public const string TravelKey = "travel.wing";

        public const string TravelWhere = "New Haven's public moongate, in the open field east of the square";

        public const string TravelHint =
            "A recall rune remembers a place. 1. Here by the moongate, double-click the Mark scroll in your backpack and click your blank rune: it now holds this spot. " +
            "2. Double-click a Recall scroll and click the lesson's rune (it says Rook's bench): you are carried to Rook. " +
            "3. Double-click your other Recall scroll and click your own rune: it brings you back here. The scrolls work for everyone in this lesson.";

        public const string RunebookLine =
            "A runebook holds up to 16 marked runes, and you can recall from it straight away. Mages also open Gate Travel to a rune; paladins use Sacred Journey. " +
            "This moongate offers towns across the lands, and shipwrights sell boat deeds for travel by sea.";

        /// <summary>Where the lesson's own rune is marked: in front of Rook's bench (the isle moongate's way out).</summary>
        public static Point3D TravelLessonTarget => HavenSpots.MoongateExit;

        private static void ArmTravel(Trial trial)
        {
            var rune = new RecallRune();
            Lend(trial, rune);
            trial.TravelRune = rune;
            Lend(trial, new RecallRune
            {
                Target = TravelLessonTarget, TargetMap = Map.Trammel, Marked = true, Description = "Rook's bench (the lesson's rune)"
            });
            Lend(trial, new MarkScroll());
            Lend(trial, new RecallScroll(2));
            LendSkill(trial, SkillName.Magery, 100);
            LendMind(trial);
        }

        /// <summary>The loaned Recall scrolls still in the trainee's backpack.</summary>
        public static int RecallScrollsLeft(PlayerMobile pm)
        {
            var n = 0;
            if (pm.Backpack is { } pack)
            {
                foreach (var scroll in pack.FindItemsByType<RecallScroll>())
                {
                    if (IsLoaner(scroll)) { n += scroll.Amount; }
                }
            }

            return n;
        }

        /// <summary>cc-P802 item 2: the rune marked, the trainee carried to Rook's bench, then back to their own rune.</summary>
        private static void TickTravel(Trial trial)
        {
            var pm = trial.Player;
            if (trial.TravelRune is not { Deleted: false } rune || trial.Finished)
            {
                return;
            }

            if (!trial.Steps.Contains("mark"))
            {
                if (rune.Marked) { Step(trial, "mark"); }
                return;
            }

            var left = RecallScrollsLeft(pm);
            if (!trial.Steps.Contains("away"))
            {
                if (left < 2 && pm.Map == Map.Trammel && pm.InRange(TravelLessonTarget, 3)) { Step(trial, "away"); }
                return;
            }

            if (left < 1 && pm.Map == rune.TargetMap && pm.InRange(rune.Target, 3)) { Step(trial, "back"); }
        }

        public static TradeTrial? Trade(string key) => Array.Find(Trades, t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        public static bool IsWingTrial(string key) => Array.IndexOf(Keys, key) >= 0 || Trade(key) != null;

        // -- Places --

        public static bool NearPlace(PlayerMobile pm, string key) => Trade(key) is not { } t
            ? NearWing(pm, key) // cc-P743: the lesson's own building (PlaceOf)
            : pm.Map == Map.Trammel && pm.InRange(new Point3D(t.Place.X, t.Place.Y, pm.Z), t.IsTour ? TourReach : 4);

        public static bool InReach(PlayerMobile pm, string key) => Trade(key) is not { } t
            ? pm.InRange(new Point3D(PlaceOf(key).X, PlaceOf(key).Y, pm.Z), LeaveRange)
            : pm.InRange(new Point3D(t.Place.X, t.Place.Y, pm.Z), t.IsTour || t.Key == TravelKey ? TourReach : LeaveRange); // cc-P802: a recall to Rook stays in the lesson

        public static string FollowLine(string key) => Trade(key) is not { } t
            ? $"Follow the arrow to {PlaceName(key)}. Your lesson begins when you arrive."
            : $"Follow the arrow to {t.Where}. Your lesson begins when you arrive.";

        public static string LeftLine(string key) => Trade(key) is not { } t
            ? $"You left {PlaceShort(key)}, so this lesson stopped and its loaned supplies disappeared. Double-click Rook to start it again."
            : t.IsTour
                ? "You went too far from New Haven, so the tour stopped. Double-click Rook to start it again."
                : $"You left {t.Where}, so this lesson stopped and its loaned supplies disappeared. Double-click Rook to start it again.";

        /// <summary>Where the arrow points: the trial's place, or a tour's next stop.</summary>
        public static (Point3D Spot, string Name) PointAt(Trial trial)
        {
            if (Trade(trial.Key) is not { } t) { return (PlaceOf(trial.Key), PlaceShort(trial.Key)); }
            if (t.Stops is { } stops)
            {
                var i = Math.Min(trial.Steps.Count, stops.Length - 1);
                return (stops[i].Spot(), stops[i].Name);
            }

            return t.Key switch
            {
                "animals.taming" => (HavenSpots.Stables, "the stables"),
                "gathering.mining" => (HavenSpots.MiningCave, "the mine"),
                "bard.trial" => (HavenSpots.Theater, "the theater"),
                TravelKey => (HavenSpots.PublicMoongate, "the public moongate"), // cc-P802
                _ when HavenPlace(t.Key) is { } hp => (hp.Arm, hp.Short), // cc-P802 item 6: its building
                _ => (Ground(t.Place.X, t.Place.Y), "the trades yard")
            };
        }

        public static string TradeHint(string key) => Trade(key)?.Hint ?? "This lesson has no activity yet. Ask a staff member for help.";

        /// <summary>A trade trial's steps, one per line, "Done: ..." or "To do: ..." (null for P290's trials).</summary>
        public static string? Progress(Trial trial)
        {
            if (Trade(trial.Key) is not { } t) { return null; }
            var lines = new List<string>();
            foreach (var (id, label) in t.Steps) { lines.Add((trial.Steps.Contains(id) ? "Done: " : "To do: ") + label); }
            return string.Join("\n", lines);
        }

        private static string TradeCliName(Item item) => item.GetType().Name switch
        {
            "Pickaxe" => "pickaxe", "Hatchet" => "hatchet", "FishingPole" => "fishing pole", "Kindling" => "kindling",
            "SmithHammer" => "smith's hammer", "SewingKit" => "sewing kit", "Saw" => "saw", "TinkerTools" => "tinker's tools",
            "MortarPestle" => "mortar and pestle", "FletcherTools" => "fletcher's tools", "Skillet" => "skillet",
            "ScribesPen" => "scribe's pen", "MapmakersPen" => "mapmaker's pen", "IronIngot" => "iron ingots", "Cloth" => "cloth",
            "Board" => "boards", "Log" => "logs", "BlackPearl" => "black pearl", "Bottle" => "empty bottles", "Apple" => "apple",
            "SackFlour" => "sack of flour", "Pitcher" => "pitcher of water", "BlankScroll" => "blank scrolls",
            "BlankMap" => "blank maps", "ShepherdsCrook" => "shepherd's crook", "Lute" => "lute", "Lockpick" => "lockpicks",
            "Dagger" => "dagger", "LesserPoisonPotion" => "lesser poison potion", "RecallRune" => "recall rune",
            "TutorialCurio" => "odd curio",
            "MarkScroll" => "Mark scroll", "RecallScroll" => "Recall scroll", // cc-P802
            "TutorialLoanCloth" => "cloth", "TutorialLoanLog" => "logs", // cc-P610
            _ => item.GetType().Name.ToLowerInvariant()
        };

        // -- Arm and return --

        /// <summary>At the place: the loans, the props and creatures, and the product counts. False when a spot is blocked.</summary>
        private static bool ArmTrade(Trial trial)
        {
            var pm = trial.Player;
            var t = Trade(trial.Key)!;
            var hp = HavenPlace(trial.Key); // cc-P802 item 6: the lesson's building and its prop tiles
            var work = hp != null ? (hp.Props.Length > 0 ? hp.Props[0] : hp.Fixture ?? hp.Arm) : Ground(TradesYard.X + 2, TradesYard.Y);
            var spots = trial.Key switch
            {
                "animals.taming" => new[] { HavenSpots.SickStall }, // cc-P743
                "gathering.mining" => Array.Empty<Point3D>(),           // cc-P743: the ravine's own rock
                TravelKey => Array.Empty<Point3D>(),                    // cc-P802: nothing set up, only loans
                "bard.trial" => new[] { BardEchoA, BardEchoB },          // cc-P799: on the theater's stage
                "thieving.trial" => hp!.Props, // cc-P802: the mark and the chest in the back alley
                _ when hp != null => hp.Props,  // cc-P802: the building's own prop tiles
                _ when t.IsTour => Array.Empty<Point3D>(),
                // The anvil and forge stand east of the yard's centre, in reach of it (CraftItem: 2 tiles) and out of the
                // way in from town (the first test-world run walked into them at x + 1 and stopped short).
                _ => new[] { work, Ground(TradesYard.X + 2, TradesYard.Y - 1), Ground(TradesYard.X + 2, TradesYard.Y + 1) }
            };
            foreach (var spot in spots)
            {
                if (!Fits(spot))
                {
                    ClusterFTutorial.SystemLine(pm, "There is no room to set up this lesson here. Ask a staff member for help.");
                    return false;
                }
            }

            trial.Armed = true;
            switch (trial.Key)
            {
                case "gathering.mining":
                    Lend(trial, new Pickaxe());
                    LendSkill(trial, SkillName.Mining, 60);
                    // cc-P743: the ravine's floor and walls; their ore banks start full (others' digging aside).
                    foreach (var p in MiningTiles) { FreshBank(Mining.System.OreAndStone, p); }
                    break;
                case "gathering.lumberjacking":
                    Wield(trial, new Hatchet());
                    LendSkill(trial, SkillName.Lumberjacking, 60);
                    FreshBank(Lumberjacking.System.GetDefinition(), work);
                    break;
                case "gathering.fishing":
                    Lend(trial, new FishingPole());
                    LendSkill(trial, SkillName.Fishing, 60);
                    FreshBank(Fishing.System.GetDefinition(), work);
                    break;
                case "gathering.camping":
                    Lend(trial, new Kindling(3));
                    LendSkill(trial, SkillName.Camping, 60);
                    LendSkill(trial, SkillName.Herbalism, 30); // cc-P804: F-51 is built
                    Prop(trial, new Server.Engines.Plants.HerbPatch("Garlic", ""), spots[1], "wild garlic"); // cc-P802: beside the camp spot
                    break;
                case "crafting.blacksmithy":
                    Lend(trial, new SmithHammer());
                    Lend(trial, new IronIngot(12));
                    LendSkill(trial, SkillName.Blacksmith, 50);
                    LendSkill(trial, SkillName.ArmsLore, 60);
                    // cc-P802: the smithy's own anvil and forge; a lent pair only if the town's are missing.
                    Server.Engines.Craft.DefBlacksmithy.CheckAnvilAndForge(pm, 2, out var hasAnvil, out var hasForge);
                    if (!hasAnvil || !hasForge)
                    {
                        Prop(trial, new Anvil(), spots[0], "anvil");
                        Prop(trial, new Forge(), new Point3D(spots[0].X, spots[0].Y + 1, spots[0].Z), "forge");
                    }
                    break;
                case "crafting.tailoring":
                    Lend(trial, new SewingKit());
                    Lend(trial, new TutorialLoanCloth(8)); // cc-P610: cannot be cut into bandages
                    LendSkill(trial, SkillName.Tailoring, 30);
                    break;
                case "crafting.carpentry":
                    Lend(trial, new Saw());
                    Lend(trial, new Board(15));
                    LendSkill(trial, SkillName.Carpentry, 30);
                    break;
                case "crafting.tinkering":
                    Lend(trial, new TinkerTools());
                    Lend(trial, new IronIngot(8));
                    Lend(trial, new TutorialCurio());
                    LendSkill(trial, SkillName.Tinkering, 55);
                    LendSkill(trial, SkillName.ItemID, 60);
                    break;
                case "crafting.alchemy":
                    Lend(trial, new MortarPestle());
                    Lend(trial, new BlackPearl(4));
                    Lend(trial, new Bottle(4));
                    Lend(trial, new Apple());
                    LendSkill(trial, SkillName.Alchemy, 30);
                    LendSkill(trial, SkillName.TasteID, 60);
                    break;
                case "crafting.fletching":
                    Lend(trial, new FletcherTools());
                    Lend(trial, new TutorialLoanLog(5)); // cc-P610: cannot be axed into boards
                    LendSkill(trial, SkillName.Fletching, 30);
                    break;
                case "crafting.cooking":
                    Lend(trial, new Skillet());
                    Lend(trial, new SackFlour());
                    Lend(trial, new SackFlour());
                    Lend(trial, new SackFlour());
                    Lend(trial, new Pitcher(BeverageType.Water));
                    LendSkill(trial, SkillName.Cooking, 70);
                    break;
                case "crafting.inscription":
                    Lend(trial, new ScribesPen());
                    Lend(trial, new Spellbook(1UL << 4)); // Magic Arrow (SpellRegistry 4)
                    Lend(trial, new BlankScroll(4));
                    Lend(trial, new SulfurousAsh(4));
                    LendSkill(trial, SkillName.Inscribe, 30);
                    LendMind(trial);
                    break;
                case "crafting.cartography":
                    Lend(trial, new MapmakersPen());
                    Lend(trial, new BlankMap());
                    Lend(trial, new BlankMap());
                    Lend(trial, new BlankMap());
                    LendSkill(trial, SkillName.Cartography, 60);
                    break;
                case "animals.taming":
                    ArmStable(trial); // cc-P743 item 4
                    break;
                case "bard.trial":
                    Lend(trial, new Lute());
                    foreach (var skill in new[] { SkillName.Musicianship, SkillName.Peacemaking, SkillName.Provocation, SkillName.Discordance })
                    {
                        LendSkill(trial, skill, 80);
                    }

                    Creature(trial, new TutorialTrainingEcho(false, false) { TrialOwner = pm.Serial }, spots[0]);
                    Creature(trial, new TutorialTrainingEcho(false, false) { TrialOwner = pm.Serial }, spots[1]);
                    break;
                case "thieving.trial":
                    ArmThief(trial, spots[0], spots[1]);
                    break;
                case TravelKey:
                    ArmTravel(trial); // cc-P802 item 2
                    break;
            }

            if (trial.LendFailed)
            {
                Unarm(trial);
                return false;
            }

            if (t.Products != null)
            {
                foreach (var type in t.Products) { trial.ProductsBefore[type] = CountProduct(pm, type); }
            }

            if (!t.IsTour)
            {
                pm.Mana = pm.ManaMax;
                ClusterFTutorial.SystemLine(pm, "Keep the loaned supplies with you in this lesson area. They disappear when you leave or finish the lesson.");
            }

            return true;
        }

        private static void ArmThief(Trial trial, Point3D markSpot, Point3D chestSpot)
        {
            var pm = trial.Player;
            Lend(trial, new Lockpick(5));
            Lend(trial, new Dagger());
            Lend(trial, new LesserPoisonPotion());
            foreach (var skill in new[]
                     {
                         SkillName.Hiding, SkillName.Stealth, SkillName.DetectHidden, SkillName.Lockpicking, SkillName.RemoveTrap,
                         SkillName.Snooping, SkillName.Stealing, SkillName.Poisoning, SkillName.Tracking, SkillName.Begging
                     })
            {
                LendSkill(trial, skill, 60);
            }

            // Stealth reads the Hiding base (Stealth.cs:77, HidingRequirement 30 since ML); a mod cannot lift it.
            var hiding = pm.Skills[SkillName.Hiding];
            if (hiding.Base < Stealth.HidingRequirement)
            {
                trial.HidingBefore = hiding.Base;
                hiding.Base = Stealth.HidingRequirement;
                TutorialLoanLedger.NoteHiding(pm, trial.HidingBefore); // cc-P610: saved, for a restart mid-lesson
            }

            var mark = new TutorialTrainingEcho(false, false) { TrialOwner = pm.Serial, Name = "a tutorial mark" };
            mark.PackItem(new Item(0x1088) { Name = PropPrefix + "a brass trinket" });
            Creature(trial, mark, markSpot);

            var chest = new WoodenChest
            {
                Locked = true, LockLevel = 1, MaxLockLevel = 30, RequiredSkill = 1,
                TrapType = TrapType.DartTrap, TrapPower = 1, TrapLevel = 1
            };
            Prop(trial, chest, chestSpot, "practice chest");
            var purse = new Item(0xE79) { Name = PropPrefix + "a purse of glass coins" };
            trial.Props.Add(purse);
            chest.DropItem(purse);
        }

        private static void Prop(Trial trial, Item item, Point3D at, string name)
        {
            item.Name = PropPrefix + name;
            item.Movable = false;
            trial.Props.Add(item);
            item.MoveToWorld(at, Map.Trammel);
        }

        private static void Creature(Trial trial, BaseCreature c, Point3D at)
        {
            trial.Creatures.Add(c);
            c.MoveToWorld(at, Map.Trammel);
        }

        /// <summary>A harvest bank starts full when the lesson arms (others' digging must not leave a trainee empty-handed).</summary>
        private static void FreshBank(HarvestDefinition def, Point3D at)
        {
            if (def.Banks.TryGetValue(Map.Trammel, out var banks))
            {
                banks.Remove(new Point2D(at.X / def.BankWidth, at.Y / def.BankHeight));
            }
        }

        private static void ReturnTrade(Trial trial)
        {
            trial.BrawlTimer.Cancel(); // cc-P743
            trial.Brawling = false;
            ReturnStable(trial);
            foreach (var item in trial.Props) { item.Delete(); }
            trial.Props.Clear();
            foreach (var c in trial.Creatures) { c.Delete(); }
            trial.Creatures.Clear();
            if (trial.HidingBefore >= 0)
            {
                // Back to the player's own base, keeping whatever the lesson's hiding gained above the lent 30.
                TutorialLoanLedger.GiveBackHiding(trial.Player, trial.HidingBefore);
                trial.HidingBefore = -1;
            }
        }

        // -- Evidence --

        private static int CountProduct(PlayerMobile pm, Type type)
        {
            var n = 0;
            if (pm.Backpack is { } pack)
            {
                foreach (var item in pack.FindItemsByType(type))
                {
                    if (item.GetType() == type && item.Name?.StartsWith(LoanerPrefix, StringComparison.Ordinal) != true) { n += item.Amount; }
                }
            }

            return n;
        }

        private static void TickTrade(Trial trial)
        {
            var pm = trial.Player;
            var t = Trade(trial.Key)!;
            if (t.Products != null && !trial.Steps.Contains(Made))
            {
                foreach (var type in t.Products)
                {
                    if (CountProduct(pm, type) > trial.ProductsBefore.GetValueOrDefault(type)) { Step(trial, Made); break; }
                }
            }

            if (t.Stops is { } stops && trial.Steps.Count < stops.Length && !trial.Finished)
            {
                var i = trial.Steps.Count;
                var spot = stops[i].Spot();
                if (pm.InRange(new Point3D(spot.X, spot.Y, pm.Z), StopRange))
                {
                    // cc-P802 item 1: the stop's lesson in the window (it stays), the System window says where to read it.
                    trial.Latest = stops[i].Text;
                    ClusterFTutorial.SystemLine(pm, $"You reached {stops[i].Name}: read it in the lesson window.");
                    Step(trial, t.Steps[i].Id);
                    if (!trial.Finished)
                    {
                        var (next, name) = PointAt(trial);
                        TutorialCoreTrials.Point(pm, next, name, HavenSpots.Map);
                    }
                }
            }

            // cc-P743: the stable horse's steps, and the thief chest's trap while Remove Trap is to do.
            if (trial.Key == "animals.taming") { TickStable(trial); }
            if (trial.Key == TravelKey) { TickTravel(trial); } // cc-P802 item 2
            KeepChestTrapped(trial);
        }

        /// <summary>animals.taming: the goat scraped itself on the fence (Veterinary needs a hurt patient, Bandage.cs).</summary>
        private static void WoundGoat(Trial trial)
        {
            if (trial.Key == "animals.taming" && !trial.Steps.Contains(SkillName.Veterinary.ToString()) &&
                trial.Creatures.Count > 0 && trial.Creatures[0] is { Deleted: false } goat && goat.Hits >= goat.HitsMax)
            {
                goat.Hits = Math.Max(1, goat.HitsMax / 2);
            }
        }

        /// <summary>A successful skill roll (SkillEvents.SkillUsed) while a trade trial waits for that skill.</summary>
        public static void TradeSkillUsed(Mobile from, Skill skill, bool success)
        {
            if (success && from is PlayerMobile pm && Current(pm) is { Armed: true, Finished: false } trial &&
                Trade(trial.Key) is { IsTour: false } t && Array.Exists(t.Steps, s => s.Id == skill.SkillName.ToString()))
            {
                // cc-P743 item 4: the stable horse itself says when it is healed and tamed (TickStable), not any roll.
                if (trial.Key == "animals.taming") { return; }
                // cc-P743 item 5: a successful Provocation sets the echoes fighting before the step is counted.
                if (trial.Key == "bard.trial" && skill.SkillName == SkillName.Provocation) { StartBrawl(trial); }
                Step(trial, skill.SkillName.ToString());
            }
        }

        /// <summary>Marks a step done, says so, and finishes the trial on its last step.</summary>
        public static void Step(Trial trial, string id)
        {
            var t = Trade(trial.Key);
            if (t == null || trial.Finished || !trial.Steps.Add(id)) { return; }
            if (trial.Steps.Count >= t.Steps.Length)
            {
                if (trial.Brawling) { trial.FinishAfterBrawl = true; return; } // cc-P743: let the echoes fight first
                Finish(trial);
                return;
            }

            var label = Array.Find(t.Steps, s => s.Id == id).Label;
            ClusterFTutorial.SystemLine(trial.Player, $"Finished: {label} ({trial.Steps.Count} of {t.Steps.Length}).");
            Refresh(trial.Player, trial); // cc-P802 item 1: the window marks it; the steps were echoed once already
        }
    }
}

namespace Server.Items
{
    using Server.Tutorial;

    /// <summary>The tinkering lesson's odd curio: unidentified until Item Identification rolls on it (pinned ItemIdentification.cs:44).</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialCurio : Item, IIdentifiable
    {
        public TutorialCurio() : base(0x1088) => Hue = TutorialStyle.SupplyHue;

        public bool Identified { get; set; }

        public override void OnSingleClick(Mobile from)
        {
            base.OnSingleClick(from);
            LabelTo(from, Identified ? "a brass curio with no value" : "an odd blue curio (unidentified)");
        }

        [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    /// <summary>The animals lesson's goat: a stock goat in the farm pen, one per trial, deleted with it and on load.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialFarmGoat : Goat
    {
        public Serial TrialOwner { get; set; }

        public TutorialFarmGoat()
        {
            Name = "a tutorial goat";
            Hue = TutorialStyle.SupplyHue;
        }

        [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
    }
}
