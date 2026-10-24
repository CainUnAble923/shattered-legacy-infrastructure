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
        private const string AtYard = "the trades yard, south-east of the training yard";

        private static (string, string) S(SkillName skill, string label) => (skill.ToString(), label);

        public const string HerbalismLine =
            "Herbalism is not built here yet: the Order of the Alembic will teach it. Camping is your fire, your rest and your safe logout outdoors.";
        public const string ImbuingLine =
            "Imbuing is not taught in this yard: it puts magic properties on crafted gear at a soul forge, and gargoyle cities teach it.";
        public const string ForensicLine =
            "Forensic Evaluation is explained, not trained here: it tells you who looted a corpse and who picked a lock.";

        public static readonly TradeTrial[] Trades =
        [
            // -- Gathering --
            new("gathering.mining", AtYard, TradesYard,
                [S(SkillName.Mining, "Mining: dig ore from the practice vein")],
                "Double-click the loaned pickaxe and click the practice vein (the gray cave floor beside you). Keep digging until ore comes up; the ore is yours to keep."),
            new("gathering.lumberjacking", AtYard, TradesYard,
                [S(SkillName.Lumberjacking, "Lumberjacking: chop logs from the practice tree")],
                "The loaned hatchet is in your hands. Double-click it and click the walnut tree beside you until logs come down; the logs are yours to keep."),
            new("gathering.fishing", AtYard, TradesYard,
                [S(SkillName.Fishing, "Fishing: catch something in the practice pond")],
                "Double-click the loaned fishing pole and click the water of the practice pond beside you. Keep casting until you catch something; it is yours to keep."),
            new("gathering.camping", AtYard, TradesYard,
                [S(SkillName.Camping, "Camping: light a campfire")],
                "Double-click the loaned kindling in your backpack to light a campfire on the ground beside you.",
                HerbalismLine),

            // -- The crafting yard --
            new("crafting.blacksmithy", AtYard, TradesYard,
                [(Made, "Blacksmithy: make a dagger at the anvil"), S(SkillName.ArmsLore, "Arms Lore: study your dagger")],
                "Stand by the anvil and forge. Double-click the loaned smith's hammer, open Bladed and make a dagger from the loaned iron ingots. Then use Arms Lore on it.",
                ImbuingLine, [typeof(Dagger)]),
            new("crafting.tailoring", AtYard, TradesYard,
                [(Made, "Tailoring: sew a skullcap")],
                "Double-click the loaned sewing kit, open Hats and make a skullcap from the loaned cloth.",
                null, [typeof(SkullCap)]),
            new("crafting.carpentry", AtYard, TradesYard,
                [(Made, "Carpentry: cut barrel staves")],
                "Double-click the loaned saw, open Other and make barrel staves from the loaned boards.",
                null, [typeof(BarrelStaves)]),
            new("crafting.tinkering", AtYard, TradesYard,
                [(Made, "Tinkering: make scissors"), S(SkillName.ItemID, "Item Identification: identify the odd curio")],
                "Double-click the loaned tinker's tools, open Tools and make scissors from the loaned ingots. Then use Item Identification on the odd curio in your backpack.",
                null, [typeof(Scissors)]),
            new("crafting.alchemy", AtYard, TradesYard,
                [(Made, "Alchemy: brew a refresh potion"), S(SkillName.TasteID, "Taste Identification: taste the apple")],
                "Double-click the loaned mortar and pestle, open Refresh and brew a refresh potion (black pearl and an empty bottle). Then use Taste Identification on the loaned apple.",
                null, [typeof(RefreshPotion)]),
            new("crafting.fletching", AtYard, TradesYard,
                [(Made, "Fletching: make kindling")],
                "Double-click the loaned fletcher's tools, open Materials and make kindling from the loaned logs.",
                null, [typeof(Kindling)]),
            new("crafting.cooking", AtYard, TradesYard,
                [(Made, "Cooking: make dough")],
                "Double-click the loaned skillet, open Ingredients and make dough from the loaned flour and the pitcher of water.",
                null, [typeof(Dough)]),
            new("crafting.inscription", AtYard, TradesYard,
                [(Made, "Inscription: write a Magic Arrow scroll")],
                "Double-click the loaned scribe's pen, open First Circle and write Magic Arrow (the loaned spellbook holds the spell; it needs a blank scroll and sulfurous ash).",
                null, [typeof(MagicArrowScroll)]),
            new("crafting.cartography", AtYard, TradesYard,
                [(Made, "Cartography: draw a local map")],
                "Double-click the loaned mapmaker's pen, open Maps and draw a local map on a loaned blank map.",
                null, [typeof(LocalMap)]),

            // -- Animals (the farm, P276) --
            new("animals.taming", "the tutorial farm's field, beside the animal pen (enter by the gate)", FarmField,
                [
                    S(SkillName.Herding, "Herding: move the tutorial goat with the crook"),
                    S(SkillName.AnimalTaming, "Animal Taming: tame the tutorial goat"),
                    S(SkillName.AnimalLore, "Animal Lore: it rolls as you tame; or study your goat"),
                    S(SkillName.Veterinary, "Veterinary: bandage it")
                ],
                "The tutorial goat is in the pen. Double-click the loaned crook, click the goat, then a spot to send it. Then use Animal Taming on it (taming rolls Animal Lore too; or use Animal Lore on it once it is yours) and a loaned bandage on it. It goes back to the farm when the lesson ends."),

            // -- Bard --
            new("bard.trial", AtYard, TradesYard,
                [
                    S(SkillName.Peacemaking, "Peacemaking: calm an echo"),
                    S(SkillName.Provocation, "Provocation: turn one echo on the other"),
                    S(SkillName.Discordance, "Discordance: weaken an echo")
                ],
                "Use Peacemaking, Provocation and Discordance on the two training echoes (Provocation: the first echo, then the second). Pick the loaned lute when asked for an instrument; Musicianship rolls every time."),

            // -- Thieving and stealth --
            new("thieving.trial", AtYard, TradesYard,
                [
                    S(SkillName.DetectHidden, "Detecting Hidden: target the practice chest"),
                    S(SkillName.RemoveTrap, "Remove Trap: disarm the practice chest"),
                    S(SkillName.Lockpicking, "Lockpicking: a loaned lockpick on the chest"),
                    S(SkillName.Stealing, "Stealing: lift the purse from the chest (hands free)"),
                    S(SkillName.Snooping, "Snooping: open the mark's backpack"),
                    S(SkillName.Begging, "Begging: beg from the mark"),
                    S(SkillName.Poisoning, "Poisoning: the loaned poison on the loaned dagger"),
                    S(SkillName.Tracking, "Tracking: use it and pick a kind"),
                    S(SkillName.Hiding, "Hiding: hide"),
                    S(SkillName.Stealth, "Stealth: while hidden, use Stealth")
                ],
                "Ten small steps on the practice chest and the mark (a tutorial-only dummy). Nothing here touches another player. Snooping and poisoning cost a little karma, as they always do. Stand beside the chest to pick it and to steal.",
                ForensicLine),

            // -- The tours --
            new("housing.basics", "the first stop", TourCentre,
                [("stop0", "The farmhouse porch"), ("stop1", "The bank")],
                "A short walk. Follow the arrow to each stop.",
                Stops:
                [
                    (() => Ground(3383, 2612), "the farmhouse porch",
                        "A house is placed from a deed on open land. Inside it you lock things down, secure containers and name co-owners and friends; boxes open in your own house, too. TazUO: Options can make roofs and walls transparent near you."),
                    (() => ClusterFNewHavenSeeder.BankAnchor, "the bank",
                        "House deeds come from architects and real estate brokers in the big cities. Keep a deed in your bank box until you place it; a house must be refreshed by visiting it, or it decays.")
                ]),
            new("travel.wing", "the first stop", TourCentre,
                [("stop0", "The moongate"), ("stop1", "The plaza")],
                "A short walk. Follow the arrow to each stop. The loaned rune is a blank recall rune: look at it, then it goes back.",
                Stops:
                [
                    (() => TutorialCorePlacement.GateSpot, "the moongate",
                        "Moongates link the cities and the facets: step in and pick where to go. Boats sail from the docks; a ship's deed comes from a shipwright."),
                    (() => TutorialRecruiter.Spot, "the plaza",
                        "A recall rune remembers a place: Mark it (Magery, fourth circle), then Recall or Gate Travel to it; Sacred Journey does it for paladins. A runebook holds sixteen. TazUO: the World Map shows where you are.")
                ]),
            new("reputation.wing", "the first stop", TourCentre,
                [("stop0", "The training yard"), ("stop1", "The healer"), ("stop2", "The plaza")],
                "A short walk. Follow the arrow to each stop.",
                Stops:
                [
                    (() => YardSpot, "the training yard",
                        "Names have colors. Blue is innocent; gray may be attacked (a criminal, or a monster); red is a murderer. Attack or steal from a blue and you turn gray for a while."),
                    (() => TutorialCorePlacement.HealerSpot, "the healer",
                        "Karma is your good name, fame your renown; together they give your title. Killing innocents earns murder counts, and five make you red. Healers resurrect the dead, but not the red."),
                    (() => TutorialRecruiter.Spot, "the plaza",
                        "Towns have guards. If a gray or red attacks you here, say \"guards\". Trammel, this facet, forbids attacking other players at all; Felucca allows it.")
                ]),
            new("shard.systems", "the first stop", TourCentre,
                [("stop0", "The League field office"), ("stop1", "The bank"), ("stop2", "Rook")],
                "A short walk. Follow the arrow to each stop.",
                Stops:
                [
                    (() => ClusterFNewHavenSeeder.RegistrarAnchor, "the League field office",
                        "The League of Extraordinary Citizens: ranks, jobs and their rewards. Elara Voss signs you up when you choose."),
                    (() => ClusterFNewHavenSeeder.BankAnchor, "the bank",
                        "Loot boxes open only in a guarded town or your own house. TazUO: Autoloot, the Scavenger and Nearby loot gather for you; vendors take advanced shop, trade and auto buy and sell."),
                    (() => TutorialRecruiter.Spot, "Rook",
                        "Levels and loops: each loop raises your level cap, and the world remembers you. Achievements pay as you play. Extra ring, bracelet and toe ring slots open as you earn them: type [accessories. TazUO: the skill progress bar, and Legion scripting for the advanced.")
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
            ? "Follow the arrow to the training yard, east of the farm gate. The lesson starts there."
            : $"Follow the arrow to {t.Where}. The lesson starts there.";

        public static string LeftLine(string key) => Trade(key) is not { } t
            ? "You left the training yard, so the lesson stopped and the yard took its loans back. Start it again at Rook."
            : t.IsTour
                ? "You went too far from New Haven, so the tour stopped. Start it again at Rook."
                : $"You left {t.Where}, so the lesson stopped and its loans went back. Start it again at Rook.";

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

        public static string TradeHint(string key) => Trade(key)?.Hint ?? "This station has no lesson. Ask a member of staff.";

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
                    ClusterFTutorial.SystemLine(pm, "This lesson's place is blocked just now. Ask a member of staff.");
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
                    Lend(trial, new Cloth(8));
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
                    Lend(trial, new Log(5));
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

            if (t.Products != null)
            {
                foreach (var type in t.Products) { trial.ProductsBefore[type] = CountProduct(pm, type); }
            }

            if (!t.IsTour)
            {
                pm.Mana = pm.ManaMax;
                ClusterFTutorial.SystemLine(pm, "The yard's loans are yours until the lesson ends. They stay here: leave and they go back.");
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
                var hiding = trial.Player.Skills[SkillName.Hiding];
                hiding.Base = trial.HidingBefore + Math.Max(0, hiding.Base - Stealth.HidingRequirement);
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
                        TutorialCoreTrials.Point(pm, next, name);
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
            ClusterFTutorial.SystemLine(trial.Player, $"Step done: {label} ({trial.Steps.Count} of {t.Steps.Length}).");
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
            LabelTo(from, Identified ? "a brass curio, worth nothing at all" : "an odd curio (unidentified)");
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
