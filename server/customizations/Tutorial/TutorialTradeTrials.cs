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
// curio), Alchemy Taste Identification (on an apple). Camping explains Herbalism, Blacksmithy Imbuing, thieving Forensic
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

        public static readonly Point2D TourCentre = new(HavenSpots.Centre.X, HavenSpots.Centre.Y); // cc-P678: was 3460,2590

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
            "Herbalism is not available yet. In this lesson, double-click kindling to light a campfire.";
        public const string ImbuingLine =
            "Imbuing adds magical properties to equipment at a soul forge. This tutorial does not teach it.";
        public const string ForensicLine =
            "Forensic Evaluation can reveal who looted a body or picked a lock. This tutorial explains it without giving you a practice task.";

        public static readonly TradeTrial[] Trades =
        [
            // -- Gathering --
            new("gathering.mining", AtYard, TradesYard,
                [S(SkillName.Mining, "Mining: dig ore from the gray stone patch")],
                "Double-click the loaned pickaxe in your backpack, then click the gray stone patch named practice vein. Repeat until you dig up ore, which you can keep."),
            new("gathering.lumberjacking", AtYard, TradesYard,
                [S(SkillName.Lumberjacking, "Lumberjacking: chop logs from the practice tree")],
                "Double-click the loaned hatchet, then click the walnut tree beside you. Repeat until you get logs, which you can keep."),
            new("gathering.fishing", AtYard, TradesYard,
                [S(SkillName.Fishing, "Fishing: catch something in the small practice pond")],
                "Double-click the loaned fishing pole in your backpack, then click the water in the small practice pond. Repeat until you catch something, which you can keep."),
            new("gathering.camping", AtYard, TradesYard,
                [S(SkillName.Camping, "Camping: light a campfire with kindling")],
                "Double-click the loaned kindling in your backpack to light a campfire beside you.",
                HerbalismLine),

            // -- The crafting yard --
            new("crafting.blacksmithy", AtYard, TradesYard,
                [(Made, "Blacksmithy: make a dagger at the anvil"), S(SkillName.ArmsLore, "Arms Lore: use on the dagger you made")],
                "Beside the anvil and forge, double-click the loaned smith's hammer, choose Bladed, then make a Dagger. Open Skills on your character window, use Arms Lore, and click your dagger.",
                ImbuingLine, [typeof(Dagger)]),
            new("crafting.tailoring", AtYard, TradesYard,
                [(Made, "Tailoring: sew a skullcap")],
                "Double-click the loaned sewing kit in your backpack. Choose Hats, then make a Skullcap with the loaned cloth.",
                null, [typeof(SkullCap)]),
            new("crafting.carpentry", AtYard, TradesYard,
                [(Made, "Carpentry: make barrel staves")],
                "Double-click the loaned saw in your backpack. Choose Other, then make Barrel Staves with the loaned boards.",
                null, [typeof(BarrelStaves)]),
            new("crafting.tinkering", AtYard, TradesYard,
                [(Made, "Tinkering: make scissors"), S(SkillName.ItemID, "Item Identification: use on the odd blue curio")],
                "Double-click the loaned tinker's tools, choose Tools, then make Scissors. Open Skills on your character window, use Item Identification, and click the odd blue curio in your backpack.",
                null, [typeof(Scissors)]),
            new("crafting.alchemy", AtYard, TradesYard,
                [(Made, "Alchemy: make a refresh potion"), S(SkillName.TasteID, "Taste Identification: use on the loaned apple")],
                "Double-click the loaned mortar and pestle, choose Refresh, then make a Refresh Potion. Open Skills on your character window, use Taste Identification, and click the loaned apple.",
                null, [typeof(RefreshPotion)]),
            new("crafting.fletching", AtYard, TradesYard,
                [(Made, "Fletching: make kindling")],
                "Double-click the loaned fletcher's tools in your backpack. Choose Materials, then make Kindling with the loaned logs.",
                null, [typeof(Kindling)]),
            new("crafting.cooking", AtYard, TradesYard,
                [(Made, "Cooking: make dough")],
                "Double-click the loaned skillet in your backpack. Choose Ingredients, then make Dough with the loaned flour and water.",
                null, [typeof(Dough)]),
            new("crafting.inscription", AtYard, TradesYard,
                [(Made, "Inscription: write a Magic Arrow scroll")],
                "Double-click the loaned scribe's pen in your backpack. Choose First Circle, then make a Magic Arrow scroll with the loaned supplies.",
                null, [typeof(MagicArrowScroll)]),
            new("crafting.cartography", AtYard, TradesYard,
                [(Made, "Cartography: draw a local map")],
                "Double-click the loaned mapmaker's pen in your backpack. Choose Maps, then make a Local Map on a loaned blank map.",
                null, [typeof(LocalMap)]),

            // -- Animals (the farm, P276) --
            new("animals.taming", "the farm's animal pen, through the gate", FarmField,
                [
                    S(SkillName.Herding, "Herding: use the crook on the goat, then click a spot"),
                    S(SkillName.AnimalTaming, "Animal Taming: use on the practice goat"),
                    S(SkillName.AnimalLore, "Animal Lore: use on your goat or gain it while taming"),
                    S(SkillName.Veterinary, "Veterinary: double-click a bandage, then your goat")
                ],
                "Double-click the loaned crook, click the practice goat, then click a place to send it. Use Animal Taming and Animal Lore from Skills, then bandage the goat; it stays at the farm."),

            // -- Bard --
            new("bard.trial", AtYard, TradesYard,
                [
                    S(SkillName.Peacemaking, "Peacemaking: use on a blue training echo"),
                    S(SkillName.Provocation, "Provocation: use on one echo, then the other"),
                    S(SkillName.Discordance, "Discordance: use on a blue training echo")
                ],
                "Open Skills on your character window and use Peacemaking, Provocation, and Discordance on the two blue human figures. Choose the loaned lute when asked; for Provocation, click one figure, then the other."),

            // -- Thieving and stealth --
            new("thieving.trial", AtYard, TradesYard,
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
            new("housing.basics", "the first stop", TourCentre,
                [("stop0", "The farmhouse porch"), ("stop1", "The bank")],
                "Follow the arrow to the first stop. Walk close to each stop to read its lesson.",
                Stops:
                [
                    (() => Ground(3383, 2612), "the farmhouse porch",
                        "Use a house deed on open land where housing is allowed, then lock items in place and secure containers inside. TazUO's Options can make nearby roofs and walls transparent."),
                    (() => ClusterFNewHavenSeeder.BankAnchor, "the bank",
                        "Keep a house deed in your bank until you are ready to use it. Visit your house regularly to refresh it and prevent decay.")
                ]),
            new("travel.wing", "the first stop", TourCentre,
                [("stop0", "The moongate"), ("stop1", "The plaza")],
                "Follow the arrow to each stop and read the lesson when you arrive. The blank recall rune in your backpack is a loan and disappears when the tour ends.",
                Stops:
                [
                    // cc-P663: the tutorial's own gate is on the isle now; the tour starts at New Haven's public one.
                    (() => HavenSpots.PublicMoongate, "the moongate",
                        "This public moongate offers destinations across the lands; the isle's moongate only ever brings trainees to Rook. Shipwrights sell boat deeds for travel by sea."),
                    (() => TutorialRecruiter.Spot, "the plaza",
                        "A recall rune stores a location when you cast Mark on it. Return with Recall, Gate Travel, or Sacred Journey; a runebook stores up to 16 runes.")
                ]),
            new("reputation.wing", "the first stop", TourCentre,
                [("stop0", "The training yard"), ("stop1", "The healer"), ("stop2", "The plaza")],
                "Follow the arrow to the first stop. Walk close to each stop to read its lesson.",
                Stops:
                [
                    (() => YardSpot, "the training yard",
                        "Blue names usually mean innocent; gray names can be attacked, and red names mark murderers. Harming an innocent can make you gray for a while."),
                    (() => HavenSpots.Healer, "the healer",
                        "Karma and fame affect your title; killing innocent players can give you murder counts. Five murder counts make your name red, and some healers will refuse to help you."),
                    (() => TutorialRecruiter.Spot, "the plaza",
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

        public static TradeTrial? Trade(string key) => Array.Find(Trades, t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        public static bool IsWingTrial(string key) => Array.IndexOf(Keys, key) >= 0 || Trade(key) != null;

        // -- Places --

        public static bool NearPlace(PlayerMobile pm, string key) => Trade(key) is not { } t
            ? NearYard(pm)
            : pm.Map == Map.Trammel && pm.InRange(new Point3D(t.Place.X, t.Place.Y, pm.Z), t.IsTour ? TourReach : 4);

        public static bool InReach(PlayerMobile pm, string key) => Trade(key) is not { } t
            ? pm.InRange(new Point3D(Yard.X, Yard.Y, pm.Z), LeaveRange)
            : pm.InRange(new Point3D(t.Place.X, t.Place.Y, pm.Z), t.IsTour ? TourReach : LeaveRange);

        public static string FollowLine(string key) => Trade(key) is not { } t
            ? "Follow the arrow to the training yard, east of the farm gate. Your lesson begins when you arrive."
            : $"Follow the arrow to {t.Where}. Your lesson begins when you arrive.";

        public static string LeftLine(string key) => Trade(key) is not { } t
            ? "You left the training yard, so this lesson stopped and its loaned supplies disappeared. Double-click Rook to start it again."
            : t.IsTour
                ? "You went too far from New Haven, so the tour stopped. Double-click Rook to start it again."
                : $"You left {t.Where}, so this lesson stopped and its loaned supplies disappeared. Double-click Rook to start it again.";

        /// <summary>Where the arrow points: the trial's place, or a tour's next stop.</summary>
        public static (Point3D Spot, string Name) PointAt(Trial trial)
        {
            if (Trade(trial.Key) is not { } t) { return (YardSpot, "the training yard"); }
            if (t.Stops is { } stops)
            {
                var i = Math.Min(trial.Steps.Count, stops.Length - 1);
                return (stops[i].Spot(), stops[i].Name);
            }

            return (Ground(t.Place.X, t.Place.Y), t.Key == "animals.taming" ? "the farm's animal pen" : "the trades yard");
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
            "TutorialLoanCloth" => "cloth", "TutorialLoanLog" => "logs", // cc-P610
            _ => item.GetType().Name.ToLowerInvariant()
        };

        // -- Arm and return --

        /// <summary>At the place: the loans, the props and creatures, and the product counts. False when a spot is blocked.</summary>
        private static bool ArmTrade(Trial trial)
        {
            var pm = trial.Player;
            var t = Trade(trial.Key)!;
            var work = Ground(TradesYard.X + 2, TradesYard.Y);
            var spots = trial.Key switch
            {
                "animals.taming" => new[] { Ground(GoatSpot.X, GoatSpot.Y) },
                "bard.trial" => new[] { work, Ground(TradesYard.X + 2, TradesYard.Y + 2) },
                "thieving.trial" => new[] { Ground(TradesYard.X + 1, TradesYard.Y), Ground(TradesYard.X - 1, TradesYard.Y) },
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
                    Prop(trial, new Static(0x053B), work, "practice vein");
                    FreshBank(Mining.System.OreAndStone, work);
                    break;
                case "gathering.lumberjacking":
                    Wield(trial, new Hatchet());
                    LendSkill(trial, SkillName.Lumberjacking, 60);
                    Prop(trial, new Static(0x0CE0), work, "practice tree");
                    Prop(trial, new Static(0x0CE1), work, "practice tree");
                    FreshBank(Lumberjacking.System.GetDefinition(), work);
                    break;
                case "gathering.fishing":
                    Lend(trial, new FishingPole());
                    LendSkill(trial, SkillName.Fishing, 60);
                    for (var dx = 0; dx < 2; dx++)
                    {
                        for (var dy = 0; dy < 2; dy++)
                        {
                            Prop(trial, new Static(0x1797), Ground(work.X + dx, work.Y + dy), "practice pond");
                        }
                    }

                    FreshBank(Fishing.System.GetDefinition(), work);
                    break;
                case "gathering.camping":
                    Lend(trial, new Kindling(3));
                    LendSkill(trial, SkillName.Camping, 60);
                    break;
                case "crafting.blacksmithy":
                    Lend(trial, new SmithHammer());
                    Lend(trial, new IronIngot(12));
                    LendSkill(trial, SkillName.Blacksmith, 50);
                    LendSkill(trial, SkillName.ArmsLore, 60);
                    Prop(trial, new Anvil(), spots[1], "anvil");
                    Prop(trial, new Forge(), spots[2], "forge");
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
                    Lend(trial, new ShepherdsCrook());
                    Lend(trial, new Bandage(10));
                    LendSkill(trial, SkillName.Herding, 60);
                    LendSkill(trial, SkillName.AnimalTaming, 60);
                    LendSkill(trial, SkillName.AnimalLore, 60);
                    LendSkill(trial, SkillName.Veterinary, 60);
                    Creature(trial, new TutorialFarmGoat { TrialOwner = pm.Serial }, spots[0]);
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
                case "travel.wing":
                    Lend(trial, new RecallRune());
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
                    ClusterFTutorial.SystemLine(pm, stops[i].Text);
                    Step(trial, t.Steps[i].Id);
                    if (!trial.Finished)
                    {
                        var (next, name) = PointAt(trial);
                        TutorialCoreTrials.Point(pm, next, name, HavenSpots.Map);
                    }
                }
            }

            // A tamed goat that has healed: it worries the fence again, so the Veterinary step has a wound to bandage.
            if (trial.Steps.Contains(SkillName.AnimalTaming.ToString())) { WoundGoat(trial); }
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
                Step(trial, skill.SkillName.ToString());
                if (skill.SkillName == SkillName.AnimalTaming) { WoundGoat(trial); }
            }
        }

        /// <summary>Marks a step done, says so, and finishes the trial on its last step.</summary>
        public static void Step(Trial trial, string id)
        {
            var t = Trade(trial.Key);
            if (t == null || trial.Finished || !trial.Steps.Add(id)) { return; }
            if (trial.Steps.Count >= t.Steps.Length)
            {
                Finish(trial);
                return;
            }

            var label = Array.Find(t.Steps, s => s.Id == id).Label;
            ClusterFTutorial.SystemLine(trial.Player, $"Finished: {label} ({trial.Steps.Count} of {t.Steps.Length}).");
            Show(trial.Player, trial);
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
