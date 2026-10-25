// cc-P290: the tutorial's wing trials, part 1: combat, healing and magic. Notes: shard-migration
// notes/cc-P290-tutorial-wing-trials-combat-magic.md; the station list and what each covers: notes/cc-P80-new-player-tutorial.md.
//
// Eight stations (TutorialStations keys): combat.weapons, combat.healing, magic.magery, magic.necromancy,
// magic.chivalry, magic.bushido, magic.ninjitsu, magic.mysticism. One shape for all of them:
//   - Rook's menu (or [Tutorial station <key>) starts the trial: a page and an arrow to the training yard, east of the
//     farm gate (Yard). On arrival the trial "arms": it lends what the lesson needs (loaners: a weapon, bandages, a
//     book and reagents), lends skill for the lesson's length (a skill mod named LoanName, never saved, so the base
//     skill and its gains stay the player's own), and brings its own training creature or wounded villager beside
//     the player (TutorialTrainingEcho, a TutorialCreature: only the trainee can touch it).
//   - Done is server-checked evidence, never a button: three hits with the picked weapon's skill; a bandage that
//     healed the player and one that healed the villager; for a spell, a successful roll of the school's casting skill
//     and then damage (or, for Chivalry, a heal) on the trial's creature within EvidenceWindow; for Bushido and
//     Ninjitsu, a hit landed while that school's move is readied.
//   - Loaners never leave the tutorial area: each tick, a loaner that is not worn or in the backpack is deleted; going
//     LeaveRange tiles from the yard (or off Trammel), logging out, a reset or the end of the trial takes everything
//     back (loaners, the skill and tithing loans, the creatures). Loaners are named "tutorial loaner: ..." and any left
//     by a restart are deleted when the world loads.
//   - Resettable: "Reset this trial" starts it again from nothing; a finished station can be replayed.
//
// cc-P291: the other 20 stations (gathering, the crafting yard, animals, bard, thieving and the four tours) are the other
// part of this class, TutorialTradeTrials.cs, on the same shape: one trial at a time, the same loans and returns.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;

namespace Server.Tutorial
{
    public static partial class TutorialWingTrials
    {
        public const string LoanName = "tutorial loan";
        public const string LoanerPrefix = "tutorial loaner: ";
        public const int LeaveRange = 14;
        public const int HitsToFinish = 3;
        public static readonly TimeSpan EvidenceWindow = TimeSpan.FromSeconds(10);

        /// <summary>The training yard: open ground east of the farm gate (checked standable from the map files, notes cc-P290).</summary>
        public static readonly Point2D Yard = new(3411, 2617);

        public static Point3D Ground(int x, int y) => new(x, y, Map.Trammel.GetAverageZ(x, y));
        public static Point3D YardSpot => Ground(Yard.X, Yard.Y);
        public static Point3D TargetSpot => Ground(Yard.X + 2, Yard.Y);
        public static Point3D PatientSpot => Ground(Yard.X - 2, Yard.Y);

        // Fake map seam for tests; the real check is the loaded server map.
        public static Func<Point3D, bool> Fits { get; set; } = p => Map.Trammel.CanFit(p, 16, false, false);

        public static readonly string[] Keys =
        [
            "combat.weapons", "combat.healing", "magic.magery", "magic.necromancy",
            "magic.chivalry", "magic.bushido", "magic.ninjitsu", "magic.mysticism"
        ];

        /// <summary>Pick-your-weapon: the skill, its label, the loaner (null: fists), and whether a hand is free for a buckler.</summary>
        public static readonly (SkillName Skill, string Label, Func<Item?> Weapon, bool OneHanded)[] Weapons =
        [
            (SkillName.Swords, "Swords: a longsword", () => new Longsword(), true),
            (SkillName.Macing, "Macing: a mace", () => new Mace(), true),
            (SkillName.Fencing, "Fencing: a narrow kryss sword", () => new Kryss(), true),
            (SkillName.Archery, "Archery: a bow and arrows", () => new Bow(), false),
            (SkillName.Throwing, "Throwing: a boomerang for gargoyles", () => new Boomerang(), true),
            (SkillName.Wrestling, "Wrestling: fight with empty hands", () => null, true)
        ];

        public const string ResistingSpellsLine =
            "Resisting Spells helps you resist some spell effects. It can improve when certain spells are cast on you; this lesson does not train it.";
        public const string SpellweavingLine =
            "Spellweaving starts with a quest, not just a spellbook. In Heartwood, look for the quest called Patience to begin learning it.";

        public sealed class Trial(PlayerMobile player, TutorialStation station)
        {
            public PlayerMobile Player { get; } = player;
            public TutorialStation Station { get; } = station;
            public string Key => Station.Key;
            public bool Armed;
            public bool Finished;
            public int Choice = -1;                      // combat.weapons: index in Weapons
            public readonly List<Item> Loaners = new();
            public readonly List<SkillMod> SkillMods = new();
            public StatMod? MindLoan;
            public int TithingLoan, TithingBefore;
            public TutorialTrainingEcho? Target;
            public TutorialTrainingEcho? Patient;
            public int Hits;
            public bool SelfHealed, PatientHealed;
            public DateTime LastCast = DateTime.MinValue;
            public int BandageHits = -1;                 // the player's hits when a bandage on themself began
            public TimerExecutionToken Timer;

            // cc-P291 (TutorialTradeTrials.cs): the trade trials' evidence and their own things.
            public readonly HashSet<string> Steps = new();             // steps done (TradeTrial.Steps)
            public readonly List<Item> Props = new();                  // things it put in the world: a vein, an anvil, a chest
            public readonly List<BaseCreature> Creatures = new();      // its goat, its marks
            public readonly Dictionary<Type, int> ProductsBefore = new();
            public double HidingBefore = -1;                           // the Hiding base before the Stealth loan (-1: none)
        }

        private static readonly Dictionary<Serial, Trial> Active = new();
        private static bool _hooked;

        public static void Initialize()
        {
            EnsureHooks();
            TutorialYardRegion.EnsureRegistered();
            SweepLoaners();
        }

        /// <summary>The handlers and the skill hook, once. The test host calls it: Initialize never runs there.</summary>
        public static void EnsureHooks()
        {
            foreach (var key in Keys) { TutorialStations.Handlers[key] = Start; }
            foreach (var t in Trades) { TutorialStations.Handlers[t.Key] = Start; }
            if (_hooked) { return; }
            _hooked = true;
            SkillEvents.SkillUsed += OnSkillUsed;
        }

        /// <summary>Loaners left behind by a restart (trials live in memory only) are deleted once the world is up.</summary>
        public static int SweepLoaners()
        {
            var gone = new List<Item>();
            foreach (var item in World.Items.Values)
            {
                if (item.Name?.StartsWith(LoanerPrefix, StringComparison.Ordinal) == true ||
                    item.Name?.StartsWith(PropPrefix, StringComparison.Ordinal) == true) // cc-P291: the trades' props
                {
                    gone.Add(item);
                }
            }
            foreach (var item in gone) { item.Delete(); }
            return gone.Count;
        }

        public static Trial? Current(PlayerMobile pm) => Active.GetValueOrDefault(pm.Serial);

        public static bool IsDoing(PlayerMobile pm, string key) =>
            ClusterFTutorial.IsParticipant(pm) && Current(pm) is { Finished: false } t && t.Key == key;

        public static string Hint(string key, int choice = -1) => key switch
        {
            "combat.weapons" when choice < 0 =>
                "Choose a weapon below; it is loaned for this lesson. Hit the blue human figure named 'a sparring echo' three times with that weapon, or with empty hands for Wrestling.",
            "combat.weapons" =>
                $"Drag the loaned weapon onto your character's picture if needed, or empty your hands for Wrestling. Press Tab to enter war mode, then double-click the blue sparring echo and land three hits with {Weapons[choice].Skill}.",
            "combat.healing" =>
                "Double-click a loaned bandage in your backpack, then click yourself and wait for it to heal you. Use another bandage on the wounded villager beside you.",
            "magic.magery" =>
                "Double-click the loaned spellbook, double-click Magic Arrow, then click the blue human figure named 'a training echo'. The sulfurous ash needed for this spell is in your backpack.",
            "magic.necromancy" =>
                "Double-click the loaned necromancer spellbook, double-click Pain Spike, then click the blue training echo. The grave dust and pig iron needed for this spell are in your backpack.",
            "magic.chivalry" =>
                "Stand within two steps of the wounded villager, double-click the loaned Book of Chivalry, then double-click Close Wounds and click the villager. The lesson supplies the tithing points this spell costs.",
            "magic.bushido" =>
                "Drag the loaned katana onto your character's picture, then double-click the Book of Bushido and click Honorable Execution. Press Tab for war mode, then double-click the blue training echo.",
            "magic.ninjitsu" =>
                "Drag the loaned katana onto your character's picture, then double-click the Book of Ninjitsu and click Focus Attack. Press Tab for war mode, then double-click the blue training echo.",
            "magic.mysticism" =>
                "Double-click the loaned mystic book, double-click Eagle Strike, then click the blue training echo. The bloodmoss, bone, spider's silk, and mandrake root needed for this spell are in your backpack.",
            _ => TradeHint(key)
        };

        // -- Start, arm, finish, cancel --

        public static void Start(PlayerMobile pm, TutorialStation station)
        {
            if (!ClusterFTutorial.IsParticipant(pm) || !IsWingTrial(station.Key)) { return; }
            if (!pm.Alive)
            {
                ClusterFTutorial.SystemLine(pm, "Visit Sera the healer at the farm gate and double-click Sera. Click Resurrect to return to life before starting a lesson.");
                return;
            }

            if (Current(pm) is { Finished: false } current && current.Key == station.Key)
            {
                Show(pm, current);
                return;
            }

            TutorialCoreTrials.Cancel(pm);
            Cancel(pm, null);
            var trial = new Trial(pm, station);
            Active[pm.Serial] = trial;
            Server.Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), () => Tick(trial), out trial.Timer);
            if (station.Key == "combat.weapons") { ClusterFTutorial.SystemLine(pm, ResistingSpellsLine); }
            if (station.Key == "magic.mysticism") { ClusterFTutorial.SystemLine(pm, SpellweavingLine); }
            if (Trade(station.Key)?.Line is { } line) { ClusterFTutorial.SystemLine(pm, line); }
            if (NearPlace(pm, station.Key)) { Arm(trial); }
            Show(pm, trial);
        }

        public static bool NearYard(PlayerMobile pm, int range = 4) =>
            pm.Map == Map.Trammel && pm.InRange(new Point3D(Yard.X, Yard.Y, pm.Z), range);

        private static void Show(PlayerMobile pm, Trial trial)
        {
            if (!trial.Armed)
            {
                ClusterFTutorial.SystemLine(pm, FollowLine(trial.Key));
            }
            else if (!trial.Finished) // cc-P291: the done line is enough; the hint again read as a new task
            {
                ClusterFTutorial.SystemLine(pm, Hint(trial.Key, trial.Choice));
            }

            if (pm.Map == Map.Trammel && !trial.Finished)
            {
                var (spot, name) = trial.Armed && trial.Target != null ? (trial.Target.Location, "the blue training echo") : PointAt(trial);
                TutorialCoreTrials.Point(pm, spot, name);
            }

            pm.CloseGump<TutorialWingTrialGump>();
            pm.SendGump(new TutorialWingTrialGump(trial.Station, trial.Choice, trial.Finished, trial.Armed, Progress(trial)));
        }

        /// <summary>At the yard: the loans and the creatures. False when the yard is blocked (nothing is lent then).</summary>
        public static bool Arm(Trial trial)
        {
            var pm = trial.Player;
            if (trial.Armed || trial.Finished) { return trial.Armed; }
            if (Trade(trial.Key) != null) { return ArmTrade(trial); }
            var needsTarget = trial.Key is not ("combat.healing" or "magic.chivalry");
            var needsPatient = trial.Key is "combat.healing" or "magic.chivalry";
            if (needsTarget && !Fits(TargetSpot) || needsPatient && !Fits(PatientSpot))
            {
                ClusterFTutorial.SystemLine(pm, "There is no room to set up this lesson in the training yard. Ask a staff member for help.");
                return false;
            }

            trial.Armed = true;
            switch (trial.Key)
            {
                case "combat.weapons":
                    break; // armed by the pick (PickWeapon)
                case "combat.healing":
                    Lend(trial, new Bandage(10));
                    LendSkill(trial, SkillName.Healing, 50);
                    pm.Hits = Math.Max(1, Math.Min(pm.Hits, pm.HitsMax * 6 / 10)); // the lesson's scrape
                    break;
                case "magic.magery":
                    Lend(trial, new Spellbook(1UL << 4)); // Magic Arrow (SpellRegistry 4)
                    Lend(trial, new SulfurousAsh(10));
                    LendSkill(trial, SkillName.Magery, 40);
                    LendMind(trial);
                    break;
                case "magic.necromancy":
                    Lend(trial, new NecromancerSpellbook(1UL << 8)); // Pain Spike (108)
                    Lend(trial, new GraveDust(10));
                    Lend(trial, new PigIron(10));
                    LendSkill(trial, SkillName.Necromancy, 40);
                    LendMind(trial);
                    break;
                case "magic.chivalry":
                    Lend(trial, new BookOfChivalry(1UL << 1)); // Close Wounds (201)
                    trial.TithingBefore = pm.TithingPoints;
                    trial.TithingLoan = 100;
                    pm.TithingPoints += trial.TithingLoan;
                    LendSkill(trial, SkillName.Chivalry, 40);
                    LendMind(trial);
                    break;
                case "magic.bushido":
                    Lend(trial, new BookOfBushido());
                    Wield(trial, new Katana());
                    LendSkill(trial, SkillName.Bushido, 50);
                    LendMind(trial);
                    break;
                case "magic.ninjitsu":
                    Lend(trial, new BookOfNinjitsu());
                    Wield(trial, new Katana()); // Focus Attack needs a melee weapon in hand
                    LendSkill(trial, SkillName.Ninjitsu, 50);
                    LendMind(trial);
                    break;
                case "magic.mysticism":
                    Lend(trial, new MysticSpellbook(1UL << 5)); // Eagle Strike (682)
                    Lend(trial, new Bloodmoss(10));
                    Lend(trial, new Bone(10));
                    Lend(trial, new SpidersSilk(10));
                    Lend(trial, new MandrakeRoot(10));
                    LendSkill(trial, SkillName.Mysticism, 50);
                    LendMind(trial);
                    break;
            }

            if (needsTarget)
            {
                trial.Target = new TutorialTrainingEcho(trial.Key == "combat.weapons", false) { TrialOwner = pm.Serial };
                trial.Target.MoveToWorld(TargetSpot, Map.Trammel);
            }

            if (needsPatient)
            {
                trial.Patient = new TutorialTrainingEcho(false, true) { TrialOwner = pm.Serial };
                trial.Patient.MoveToWorld(PatientSpot, Map.Trammel);
                trial.Patient.Hits = trial.Patient.HitsMax / 2;
            }

            pm.Mana = pm.ManaMax;
            ClusterFTutorial.SystemLine(pm, "Keep the loaned supplies with you in this lesson area. They disappear when you leave or finish the lesson.");
            return true;
        }

        /// <summary>combat.weapons: the pick. Lends the weapon (and a buckler for one hand, arrows for the bow) and its skill.</summary>
        public static bool PickWeapon(PlayerMobile pm, int choice)
        {
            if (Current(pm) is not { Key: "combat.weapons", Finished: false } trial || choice < 0 || choice >= Weapons.Length)
            {
                return false;
            }

            if (!trial.Armed && !Arm(trial))
            {
                return false;
            }

            if (trial.Choice >= 0)
            {
                ClusterFTutorial.SystemLine(pm, "You already chose a weapon for this lesson. Click Restart this lesson to choose another.");
                return false;
            }

            var (skill, _, make, oneHanded) = Weapons[choice];
            if (skill == SkillName.Throwing && pm.Race != Race.Gargoyle)
            {
                ClusterFTutorial.SystemLine(pm, "Only gargoyles can use throwing weapons. Choose a different weapon for this lesson.");
                return false;
            }

            trial.Choice = choice;
            var weapon = make();
            if (weapon != null)
            {
                Lend(trial, weapon);
                if (pm.FindItemOnLayer(Layer.OneHanded) == null && pm.FindItemOnLayer(Layer.TwoHanded) == null)
                {
                    pm.EquipItem(weapon);
                }
            }

            if (skill == SkillName.Archery) { Lend(trial, new Arrow(50)); }
            if (oneHanded)
            {
                var shield = new Buckler();
                Lend(trial, shield);
                if (pm.FindItemOnLayer(Layer.TwoHanded) == null) { pm.EquipItem(shield); }
            }

            LendSkill(trial, skill, 30);
            Show(pm, trial);
            return true;
        }

        private static void Lend(Trial trial, Item item)
        {
            item.Name = LoanerPrefix + (item.Name ?? CliName(item));
            item.LootType = LootType.Blessed;
            trial.Loaners.Add(item);
            if (!trial.Player.AddToBackpack(item))
            {
                item.Delete();
            }
        }

        /// <summary>Lends a weapon and puts it in the player's hands when both are free.</summary>
        private static void Wield(Trial trial, Item weapon)
        {
            Lend(trial, weapon);
            var pm = trial.Player;
            if (!weapon.Deleted && pm.FindItemOnLayer(Layer.OneHanded) == null && pm.FindItemOnLayer(Layer.TwoHanded) == null)
            {
                pm.EquipItem(weapon);
            }
        }

        private static string CliName(Item item) => item.GetType().Name switch
        {
            "Spellbook" => "spellbook", "NecromancerSpellbook" => "necromancer spellbook", "BookOfChivalry" => "book of chivalry",
            "BookOfBushido" => "book of bushido", "BookOfNinjitsu" => "book of ninjitsu", "MysticSpellbook" => "mystic book",
            "SulfurousAsh" => "sulfurous ash", "GraveDust" => "grave dust", "PigIron" => "pig iron", "Bloodmoss" => "bloodmoss",
            "Bone" => "bone", "SpidersSilk" => "spider's silk", "MandrakeRoot" => "mandrake root", "Bandage" => "bandage",
            "Arrow" => "arrow", "Buckler" => "buckler", "Longsword" => "longsword", "Mace" => "mace", "Kryss" => "kryss",
            "Bow" => "bow", "Boomerang" => "boomerang", "Katana" => "katana",
            _ => TradeCliName(item)
        };

        /// <summary>Raises a skill to at least the value for the lesson, as a mod (never saved; the base is untouched).</summary>
        private static void LendSkill(Trial trial, SkillName skill, double atLeast)
        {
            var lift = atLeast - trial.Player.Skills[skill].Base;
            if (lift <= 0) { return; }
            var mod = new DefaultSkillMod(skill, LoanName, true, lift);
            trial.Player.AddSkillMod(mod);
            trial.SkillMods.Add(mod);
        }

        /// <summary>Mana for a new character's spells and moves (Focus Attack alone costs 20): +30 Intelligence for the lesson.</summary>
        private static void LendMind(Trial trial)
        {
            trial.MindLoan = new StatMod(StatType.Int, LoanName, 30, TimeSpan.Zero);
            trial.Player.AddStatMod(trial.MindLoan);
        }

        private static void Finish(Trial trial)
        {
            var pm = trial.Player;
            if (trial.Finished) { return; }
            trial.Finished = true;
            Return(trial);
            ClusterFTutorial.CompleteStation(pm, trial.Key);
            ClusterFTutorial.SystemLine(pm, $"{trial.Station.Title}: finished. The loaned supplies have been returned.");
            Show(pm, trial);
        }

        /// <summary>Takes everything back: the loaners, the skill, Intelligence and tithing loans, the trial's creatures.</summary>
        private static void Return(Trial trial)
        {
            var pm = trial.Player;
            foreach (var item in trial.Loaners) { item.Delete(); }
            trial.Loaners.Clear();
            foreach (var mod in trial.SkillMods) { pm.RemoveSkillMod(mod); }
            trial.SkillMods.Clear();
            if (trial.MindLoan != null) { pm.RemoveStatMod(LoanName); trial.MindLoan = null; }
            if (trial.TithingLoan > 0)
            {
                // What the lesson spent came out of the loan first; only the rest of the loan is taken back.
                var spent = Math.Clamp(trial.TithingBefore + trial.TithingLoan - pm.TithingPoints, 0, trial.TithingLoan);
                pm.TithingPoints = Math.Max(0, pm.TithingPoints - (trial.TithingLoan - spent));
                trial.TithingLoan = 0;
            }

            trial.Target?.Delete();
            trial.Patient?.Delete();
            trial.Target = trial.Patient = null;
            ReturnTrade(trial);
        }

        /// <summary>Ends the player's wing trial, taking every loan back. The reason, if any, is said to the player.</summary>
        public static void Cancel(PlayerMobile pm, string? why = null)
        {
            if (!Active.Remove(pm.Serial, out var trial)) { return; }
            trial.Timer.Cancel();
            Return(trial);
            if (why != null && !trial.Finished) { ClusterFTutorial.SystemLine(pm, why); }
            pm.CloseGump<TutorialWingTrialGump>();
        }

        public static void Reset(PlayerMobile pm, TutorialStation station)
        {
            if (!ClusterFTutorial.IsParticipant(pm)) { return; }
            Cancel(pm, null);
            Start(pm, station);
        }

        [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
        public static void OnLogin(PlayerMobile pm) => Cancel(pm, null);

        private static void Tick(Trial trial)
        {
            var pm = trial.Player;
            if (Current(pm) != trial) { trial.Timer.Cancel(); return; }
            if (pm.Deleted || pm.NetState == null || !ClusterFTutorial.IsParticipant(pm)) { Cancel(pm, null); return; }
            if (trial.Finished) { Active.Remove(pm.Serial); trial.Timer.Cancel(); return; }

            if (!trial.Armed)
            {
                if (pm.Alive && NearPlace(pm, trial.Key) && Arm(trial)) { Show(pm, trial); }
                return;
            }

            if (pm.Map != Map.Trammel || !InReach(pm, trial.Key))
            {
                Cancel(pm, LeftLine(trial.Key));
                return;
            }

            // A loaner that is not worn or in the backpack (dropped, banked, traded, put on a vendor) goes back at once.
            for (var i = trial.Loaners.Count - 1; i >= 0; i--)
            {
                var item = trial.Loaners[i];
                if (item.Deleted) { trial.Loaners.RemoveAt(i); continue; }
                if (item.Parent != pm && !item.IsChildOf(pm.Backpack))
                {
                    item.Delete();
                    trial.Loaners.RemoveAt(i);
                    ClusterFTutorial.SystemLine(pm, "That loaned item disappeared because you dropped it or moved it out of your belongings. Keep lesson supplies equipped or in your backpack.");
                }
            }

            if (trial.Key == "combat.healing") { WatchSelfBandage(trial); }
            else if (Trade(trial.Key) != null) { TickTrade(trial); }
        }

        // combat.healing, the self step: a bandage on yourself (BandageContext with you as the patient) that ends with
        // your hits clearly higher than when it began (a Healing-lent bandage heals at least 14; regeneration is slower).
        private static void WatchSelfBandage(Trial trial)
        {
            var pm = trial.Player;
            var context = BandageContext.GetContext(pm);
            if (context?.Patient == pm)
            {
                if (trial.BandageHits < 0) { trial.BandageHits = pm.Hits; }
                return;
            }

            if (trial.BandageHits >= 0)
            {
                if (pm.Hits >= trial.BandageHits + 5) { SelfHealed(pm); }
                trial.BandageHits = -1;
            }
        }

        // -- Evidence --

        /// <summary>combat.healing: the player bandaged themself.</summary>
        public static void SelfHealed(PlayerMobile pm)
        {
            if (Current(pm) is not { Key: "combat.healing", Armed: true, Finished: false } trial || trial.SelfHealed) { return; }
            trial.SelfHealed = true;
            if (trial.PatientHealed) { Finish(trial); }
            else { ClusterFTutorial.SystemLine(pm, "You healed yourself. Double-click another bandage, then click the wounded villager beside you."); }
        }

        /// <summary>A successful roll of a skill (SkillEvents.SkillUsed): a spell's cast went off.</summary>
        public static void OnSkillUsed(Mobile from, Skill skill, bool success)
        {
            TradeSkillUsed(from, skill, success);
            if (success && from is PlayerMobile pm && Current(pm) is { Armed: true, Finished: false } trial &&
                CastSkill(trial.Key) == skill.SkillName)
            {
                trial.LastCast = Core.Now;
            }
        }

        public static SkillName? CastSkill(string key) => key switch
        {
            "magic.magery" => SkillName.Magery,
            "magic.necromancy" => SkillName.Necromancy,
            "magic.chivalry" => SkillName.Chivalry,
            "magic.mysticism" => SkillName.Mysticism,
            _ => null
        };

        /// <summary>The trial's creature took damage from its trainee.</summary>
        public static void OnTargetDamaged(TutorialTrainingEcho echo, Mobile? from)
        {
            if (from is not PlayerMobile pm || Current(pm) is not { Armed: true, Finished: false } trial || trial.Target != echo)
            {
                return;
            }

            switch (trial.Key)
            {
                // An empty hand is Wrestling (pinned's Fists).
                case "combat.weapons" when trial.Choice >= 0 && ((pm.Weapon as BaseWeapon)?.Skill ?? SkillName.Wrestling) == Weapons[trial.Choice].Skill:
                    trial.Hits++;
                    if (trial.Hits < HitsToFinish)
                    {
                        ClusterFTutorial.SystemLine(pm, $"Hits landed: {trial.Hits} of {HitsToFinish}.");
                    }
                    else
                    {
                        Finish(trial);
                    }

                    break;
                case "magic.bushido" when SpecialMove.GetCurrentMove(pm) is { MoveSkill: SkillName.Bushido }:
                case "magic.ninjitsu" when SpecialMove.GetCurrentMove(pm) is { MoveSkill: SkillName.Ninjitsu }:
                    Finish(trial);
                    break;
                case "magic.magery" or "magic.necromancy" or "magic.mysticism" when Core.Now - trial.LastCast <= EvidenceWindow:
                    Finish(trial);
                    break;
            }
        }

        /// <summary>The trial's wounded villager was healed by its trainee (a bandage, or Close Wounds).</summary>
        public static void OnPatientHealed(TutorialTrainingEcho patient, Mobile? from)
        {
            if (from is not PlayerMobile pm || Current(pm) is not { Armed: true, Finished: false } trial || trial.Patient != patient)
            {
                return;
            }

            if (trial.Key == "magic.chivalry" && Core.Now - trial.LastCast <= EvidenceWindow)
            {
                Finish(trial);
            }
            else if (trial.Key == "combat.healing" && !trial.PatientHealed && BandageContext.GetContext(pm) == null)
            {
                trial.PatientHealed = true;
                if (trial.SelfHealed) { Finish(trial); }
                else { ClusterFTutorial.SystemLine(pm, "You healed the villager. Double-click another bandage, then click yourself."); }
            }
        }
    }

    /// <summary>A wing trial's page: what to do, the weapon pick, hint, reset, stop.</summary>
    public class TutorialWingTrialGump : Gump
    {
        public const int BtnHint = 1;
        public const int BtnReset = 2;
        public const int BtnStop = 3;
        public const int BtnWeaponBase = 10;

        private readonly TutorialStation _station;

        public TutorialWingTrialGump(TutorialStation station, int choice, bool done, bool armed, string? progress = null) : base(80, 80)
        {
            _station = station;
            var pick = station.Key == "combat.weapons" && armed && choice < 0 && !done;
            var steps = done || !armed || progress == null ? 0 : progress.Split('\n').Length;
            var h = 250 + (pick ? TutorialWingTrials.Weapons.Length * 24 : 0) + steps * 20;
            TutorialStyle.Panel(this, 500, h);
            AddLabel(24, 18, TutorialStyle.HeadingHue, station.Title);
            var text = done ? "Lesson finished and loaned supplies returned. Double-click Rook to choose another lesson." :
                !armed ? TutorialWingTrials.FollowLine(station.Key) :
                TutorialWingTrials.Hint(station.Key, choice);
            AddHtml(24, 46, 452, 90, TutorialStyle.Html(text), false, false);
            var y = 140;
            if (steps > 0)
            {
                // cc-P291: a trade trial's steps, each marked done or still to do.
                foreach (var line in progress!.Split('\n'))
                {
                    AddLabel(24, y, line.StartsWith("Done", StringComparison.Ordinal) ? TutorialStyle.DoneHue : TutorialStyle.TextHue, line);
                    y += 20;
                }
            }
            if (pick)
            {
                for (var i = 0; i < TutorialWingTrials.Weapons.Length; i++, y += 24)
                {
                    AddButton(24, y, 4005, 4007, BtnWeaponBase + i);
                    AddLabel(60, y + 2, TutorialStyle.ActionHue, TutorialWingTrials.Weapons[i].Label);
                }
            }

            AddButton(24, y + 10, 4005, 4007, BtnHint); AddLabel(60, y + 12, done ? TutorialStyle.DoneHue : TutorialStyle.ActionHue, done ? "Back to Rook" : "Hint and arrow");
            AddButton(24, y + 40, 4017, 4019, BtnReset); AddLabel(60, y + 42, TutorialStyle.TextHue, "Restart this lesson");
            if (!done) { AddButton(260, y + 40, 4017, 4019, BtnStop); AddLabel(296, y + 42, TutorialStyle.TextHue, "Stop and return supplies"); }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm || !ClusterFTutorial.IsParticipant(pm)) { return; }
            var i = info.ButtonID - BtnWeaponBase;
            if (i >= 0 && i < TutorialWingTrials.Weapons.Length)
            {
                if (!TutorialWingTrials.PickWeapon(pm, i)) { pm.SendGump(new TutorialWingTrialGump(_station, -1, false, true)); }
                return;
            }

            switch (info.ButtonID)
            {
                case BtnHint when TutorialWingTrials.Current(pm) is { Finished: true }:
                    TutorialRecruiter.PointTo(pm);
                    break;
                case BtnHint:
                    ClusterFTutorial.RunStation(pm, _station);
                    break;
                case BtnReset:
                    TutorialWingTrials.Reset(pm, _station);
                    break;
                case BtnStop:
                    TutorialWingTrials.Cancel(pm, "Lesson stopped and loaned supplies returned. Double-click Rook to start another lesson.");
                    break;
            }
        }
    }

    /// <summary>
    /// The training yard, a child of the region it stands in (New Haven's town region), so guards, music and every other
    /// rule stay the parent's. One rule of its own: a trainee may heal their own trial's wounded villager. In Trammel a
    /// player may not heal an uncontrolled creature (pinned Notoriety.Mobile_AllowBeneficial, Misc/Notoriety.cs:135-138:
    /// "Players cannot heal uncontrolled mobiles"), which refused the bandage and Close Wounds on the first test-world run.
    /// </summary>
    public class TutorialYardRegion : BaseRegion
    {
        public const string RegionName = "the training yard";
        public const int Reach = 6;

        public static TutorialYardRegion? Instance { get; private set; }

        public static Rectangle3D Area => new(
            new Point3D(TutorialWingTrials.Yard.X - Reach, TutorialWingTrials.Yard.Y - Reach, MinZ),
            new Point3D(TutorialWingTrials.Yard.X + Reach + 1, TutorialWingTrials.Yard.Y + Reach + 1, MaxZ));

        public TutorialYardRegion(Region? parent) : base(RegionName, Map.Trammel, parent, (parent?.Priority ?? DefaultPriority) + 1, Area)
        {
        }

        /// <summary>Once, after the map's regions are loaded (they load before Initialize, Main.cs).</summary>
        public static void EnsureRegistered()
        {
            if (Instance != null || Map.Trammel == null) { return; }
            var here = Find(new Point3D(TutorialWingTrials.Yard.X, TutorialWingTrials.Yard.Y, 0), Map.Trammel);
            Instance = new TutorialYardRegion(here == Map.Trammel.DefaultRegion ? null : here);
            Instance.Register();
        }

        public override bool AllowBeneficial(Mobile from, Mobile target) =>
            target is TutorialTrainingEcho { IsPatient: true, Deleted: false } patient && patient.TrialOwner == from.Serial
                ? from.Alive
                : base.AllowBeneficial(from, target);
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    /// <summary>
    /// A wing trial's creature: a sparring echo that fights back lightly (combat.weapons), a training echo that only
    /// stands and takes hits and spells, or a wounded villager to heal. Tutorial-only (TutorialCreature), never a kill:
    /// it cannot die, and it belongs to one trial (deleted with it, and on load).
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialTrainingEcho : TutorialCreature
    {
        public Serial TrialOwner { get; set; }

        public bool IsPatient { get; private set; }

        public TutorialTrainingEcho(bool spars, bool patient) : base(spars ? AIType.AI_Melee : AIType.AI_Animal)
        {
            IsPatient = patient;
            Name = patient ? "a wounded villager" : spars ? "a sparring echo" : "a training echo";
            Body = 0x190;
            Hue = patient ? 0x83EA : 0x4F2;
            FightMode = spars ? FightMode.Closest : FightMode.None;
            CantWalk = !spars;
            SetStr(60);
            SetDex(40);
            SetInt(10);
            SetHits(200);
            SetDamage(1, 2);
            SetSkill(SkillName.Wrestling, 10.0);
            SetSkill(SkillName.Tactics, 10.0);
            SetSkill(SkillName.MagicResist, 0.0);
        }

        public override bool IsInvulnerable => IsPatient;

        // A human body reads innocent (Notoriety.MobileNotoriety, Misc/Notoriety.cs:485-492), so a spell at the echo was
        // refused (Notoriety.cs:241, then "You cannot perform negative acts", Mobile.cs:8367-8374) and a blow would
        // flag the trainee criminal. Gray instead: attackable by its trainee, never a crime. The villager stays
        // invulnerable (yellow).
        public override bool AlwaysAttackable => !IsPatient;
        public override bool ClickTitle => false;

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);
            TutorialWingTrials.OnTargetDamaged(this, from);
        }

        // Never dies: a trial's creature tops itself up instead.
        public override void Damage(int amount, Mobile? from = null, bool informMount = true, bool ignoreEvilOmen = false)
        {
            if (!IsPatient && Hits - amount < 20) { Hits = HitsMax; }
            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }

        public override void OnHeal(ref int amount, Mobile from)
        {
            base.OnHeal(ref amount, from);
            TutorialWingTrials.OnPatientHealed(this, from);
        }

        [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
    }
}
