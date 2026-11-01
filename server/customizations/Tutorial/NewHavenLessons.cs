// cc-P743: Chase's New Haven notes of 2026-10-30, the wing lessons part (shard-migration
// notes/cc-P743-new-haven-tutorial-fixes.md). One more part of TutorialWingTrials (P290/P291's partial class):
//
//   1. The sparring lesson (combat.weapons) is in the Proving Grounds, New Haven's warrior training yard (PlaceOf).
//   3. The mining lesson is at the cave on the mountain path south of town, on its real cave floor (no practice vein);
//      a signpost marks where the path starts.
//   4. The animals lesson is in the stables: an injured horse in the sick-bay stall, seen only by its trainee
//      (IPrivateMobile), bandaged (Veterinary), then tamed, which always works once it is healed. The first horse a
//      character tames is theirs to keep and ride (TutorialStableHorseStore); a replay's horse goes back to its stall.
//   5. Bard: a successful Provocation sets the two echoes fighting each other for about 12 s (BrawlSeconds), then they
//      are healed and the lesson goes on (or finishes).
//   7. The magery lesson is in the Haven Magery School, where a mage echo casts Magic Arrow at the training echo a few
//      times, slowly, with the words of power, until the trainee has cast it; then it fades. The bard lesson is in the
//      Bardic Guild's hall.
//   8. Thieving: while Remove Trap is still to do, the practice chest always has a trap: one that went off (opening the
//      chest fires it) is set again at once, with a line saying so.
//   2. New Haven's old tutorial farm (P276's addon, door and pen spawner at the ML map's west farm, 3372-3401 x
//      2603-2619) is taken away at every seed; nothing uses it now.
//   6. Rook sits on a bench by the Albatross tavern (HavenSpots.Rook); an older world's Rook is moved there.
//   9. The housing lesson: three stops, each with a signpost and one plain goal (Trades row housing.basics).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Tutorial
{
    /// <summary>cc-P743: a creature kept for one player: PlayerMobile.CanSee shows it only to them (and staff).</summary>
    public interface IPrivateMobile
    {
        bool ShowsTo(Mobile viewer);
    }

    public static partial class TutorialWingTrials
    {
        /// <summary>cc-P743: every line these lessons say, through the tutorial's System voice; a seam for the tests.</summary>
        public static Action<Mobile, string>? Said { get; set; }

        public static void Line(Mobile m, string text)
        {
            ClusterFTutorial.SystemLine(m, text);
            Said?.Invoke(m, text);
        }

        // -- Places and words (items 1, 3, 4, 7, 9) --

        public const string MiningWhere =
            "the cave on the mountain path south of town (out past the Albatross tavern, south along the east shore, then west round the hill)";

        public const string StablesWhere = "the stables beside Uzeraan's mansion, west of the square (the injured horse is in the stall on the left)";

        public const string BardWhere = "the Bardic Guild's hall, north of the square";

        /// <summary>The bard lesson's two echoes, side by side on the hall's open floor.</summary>
        public static readonly Point3D BardEchoA = new(3668, 2530, 0);
        public static readonly Point3D BardEchoB = new(3669, 2531, 0);

        /// <summary>The mining cave's floor (cave floor statics at Z 9), whose ore banks start full when the lesson arms.</summary>
        public static readonly Point3D[] MiningTiles =
        [
            new(3610, 2742, 9), new(3611, 2742, 9), new(3612, 2742, 9), new(3613, 2742, 9),
            new(3610, 2743, 9), new(3611, 2743, 9), new(3612, 2743, 9), new(3613, 2743, 9), new(3613, 2744, 9)
        ];

        /// <summary>The housing lesson's first two stops (the third is the bank).</summary>
        public static Point3D HousingStop(int i) => i == 0 ? Ground(3709, 2594) : Ground(3704, 2605); // stop 2 in the open grass, clear of the trees

        public const string HousingStop1 =
            "Stop 1 of 3: this cottage is a small house. A house comes from a house deed: double-click the deed, pick a spot, and the house appears there. " +
            "Inside your own house you can lock items down so they stay put, and secure a chest so only you can open it. " +
            "Tip: TazUO's Options can make roofs and walls see-through when you walk inside. Next: follow the arrow to stop 2, the field just south of here.";

        public const string HousingStop2 =
            "Stop 2 of 3: you cannot build a house here. Towns and all of Haven island are no-building areas. " +
            "Out in the wilds, on flat open ground, you can: when you use a deed, your client shows the house's outline, green where it fits and red where it does not. " +
            "Next: follow the arrow to stop 3, the bank on the square.";

        public const string HousingStop3 =
            "Stop 3 of 3: keep a house deed in your bank until you are ready to use it. Once your house stands, visit it every so often to refresh it, or it slowly decays. " +
            "That is the end of the housing lesson; double-click Rook for another.";


        /// <summary>Where a P290 lesson (not a trade) arms: its own building, or the training yard.</summary>
        public static Point3D PlaceOf(string key) => key switch
        {
            "combat.weapons" => HavenSpots.ProvingGrounds,
            "magic.magery" => HavenSpots.MageryHall,
            _ => YardSpot
        };

        /// <summary>Where its training echo stands.</summary>
        public static Point3D TargetOf(string key) => key switch
        {
            "combat.weapons" => new Point3D(3733, 2580, 40),
            "magic.magery" => new Point3D(3654, 2472, 0),
            _ => TargetSpot
        };

        /// <summary>cc-P743 item 7: where the mage echo stands to show the spell.</summary>
        public static readonly Point3D MageDemoSpot = new(3650, 2471, 0);

        public static string PlaceName(string key) => key switch
        {
            "combat.weapons" => "the Proving Grounds, the fenced warrior training yard on the east side of town",
            "magic.magery" => "the Haven Magery School, the stone hall at the north end of town",
            _ => "the training yard, behind the Little Shop of Magic"
        };

        public static string PlaceShort(string key) => key switch
        {
            "combat.weapons" => "the Proving Grounds",
            "magic.magery" => "the Haven Magery School",
            _ => "the training yard"
        };

        public static bool NearWing(PlayerMobile pm, string key, int range = 4)
        {
            var at = PlaceOf(key);
            return pm.Map == Map.Trammel && pm.InRange(new Point3D(at.X, at.Y, pm.Z), range);
        }

        // -- Item 7: the mage echo shows Magic Arrow --

        public const string MageDemoLine =
            "Watch the mage echo: it says the words of power, In Por Ylem, raises its hands, and a Magic Arrow flies at the training echo. Then cast it yourself.";

        public static void StartMageDemo(Trial trial)
        {
            if (trial.Key != "magic.magery" || trial.Target == null) { return; }
            var demo = new TutorialMageEcho { TrialOwner = trial.Player.Serial };
            demo.MoveToWorld(MageDemoSpot, Map.Trammel);
            trial.Demo = demo;
            Line(trial.Player, MageDemoLine);
            Server.Timer.StartTimer(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), () =>
            {
                if (trial.Finished || demo.Deleted || trial.Target is not { Deleted: false } target)
                {
                    trial.DemoTimer.Cancel();
                    return;
                }

                demo.ShowMagicArrow(target);
            }, out trial.DemoTimer);
        }

        public static void EndMageDemo(Trial trial)
        {
            trial.DemoTimer.Cancel();
            if (trial.Demo is { Deleted: false } demo)
            {
                Effects.SendLocationParticles(EffectItem.Create(demo.Location, demo.Map, EffectItem.DefaultDuration), 0x3728, 10, 15, 5042);
                demo.Delete();
            }

            trial.Demo = null;
        }

        // -- Item 5: provoked echoes fight --

        public static readonly TimeSpan BrawlSeconds = TimeSpan.FromSeconds(12);
        public static readonly TimeSpan BrawlBeat = TimeSpan.FromSeconds(1.5);

        public const string BrawlLine =
            "Provocation worked: the two echoes now fight each other instead of you. A provoked creature attacks the one you pointed it at. Watch them for a few seconds.";

        public const string BrawlOverLine = "The echoes stop fighting and catch their breath. Provocation lasts only a short while.";

        public static void StartBrawl(Trial trial)
        {
            if (trial.Brawling || trial.Creatures.Count < 2 ||
                trial.Creatures[0] is not TutorialTrainingEcho a || trial.Creatures[1] is not TutorialTrainingEcho b)
            {
                return;
            }

            trial.Brawling = true;
            a.BrawlFoe = b;
            b.BrawlFoe = a;
            var pm = trial.Player;
            Line(pm, BrawlLine);
            BardGuildmasterSays(pm, "Ha! See them go at each other? That is Provocation. Keep your distance while it lasts.");
            var beats = 0;
            var total = (int)(BrawlSeconds.TotalMilliseconds / BrawlBeat.TotalMilliseconds);
            Server.Timer.StartTimer(TimeSpan.FromMilliseconds(300), BrawlBeat, () =>
            {
                if (trial.Finished && !trial.FinishAfterBrawl || a.Deleted || b.Deleted)
                {
                    trial.BrawlTimer.Cancel();
                    return;
                }

                var (hitter, hit) = beats % 2 == 0 ? (a, b) : (b, a);
                hitter.SwingAt(hit);
                if (++beats >= total)
                {
                    EndBrawl(trial);
                }
            }, out trial.BrawlTimer);
        }

        public static void EndBrawl(Trial trial)
        {
            trial.BrawlTimer.Cancel();
            if (!trial.Brawling) { return; }
            trial.Brawling = false;
            foreach (var c in trial.Creatures)
            {
                if (c is TutorialTrainingEcho e)
                {
                    e.BrawlFoe = null;
                    e.Hits = e.HitsMax;
                    e.Combatant = null;
                }
            }

            Line(trial.Player, BrawlOverLine);
            if (trial.FinishAfterBrawl)
            {
                trial.FinishAfterBrawl = false;
                Finish(trial);
            }
        }

        private static void BardGuildmasterSays(PlayerMobile pm, string line)
        {
            foreach (var m in pm.Map.GetMobilesInRange(pm.Location, 14))
            {
                if (m is BaseVendor v && v.GetType().Name.Contains("Bard", StringComparison.Ordinal))
                {
                    v.SayTo(pm, line);
                    return;
                }
            }
        }

        // -- Item 8: the practice chest keeps a trap while Remove Trap is to do --

        public const string ChestRetrapLine =
            "The practice chest's dart trap went off when the chest was opened, so the lesson set a new one. Use Remove Trap on the wooden practice chest.";

        public static void KeepChestTrapped(Trial trial)
        {
            if (trial.Key != "thieving.trial" || trial.Steps.Contains(SkillName.RemoveTrap.ToString())) { return; }
            foreach (var item in trial.Props)
            {
                if (item is WoodenChest { Deleted: false } chest && chest.TrapType == TrapType.None)
                {
                    chest.TrapType = TrapType.DartTrap;
                    chest.TrapPower = 1;
                    chest.TrapLevel = 1;
                    trial.ChestResets++;
                    Line(trial.Player, ChestRetrapLine);
                }
            }
        }

        // -- Item 4: the stable horse --

        public const string HorseLine =
            "An injured horse lies in the sick-bay stall, bleeding from a cut. Double-click a bandage, then click the horse to treat it (Veterinary). Once it is healed, use Animal Taming from your Skills on it. It is yours to keep.";

        public const string HorseHealedLine =
            "The horse's wound is bandaged and it calms down. Now open Skills on your character window, use Animal Taming, and click the horse.";

        public const string HorseKeptLine =
            "The horse trusts you and is yours to keep. Double-click it to ride; say 'all follow me' to lead it. A stablemaster can keep it for you.";

        public const string HorseReplayLine =
            "You already have a horse from this lesson, so this one goes back to its stall when the lesson ends.";

        public const string HorseNoSlotLine =
            "Your pets already fill your follower slots, so you cannot tame the horse yet. Stable a pet with a stablemaster (or release one), then come back.";

        public static void ArmStable(Trial trial)
        {
            var pm = trial.Player;
            if (pm.Backpack?.GetAmount(typeof(Bandage)) is null or 0)
            {
                Lend(trial, new Bandage(10));
            }

            LendSkill(trial, SkillName.Veterinary, 60);
            LendSkill(trial, SkillName.AnimalLore, 60);
            LendSkill(trial, SkillName.AnimalTaming, 60);
            var horse = new TutorialStableHorse(pm);
            horse.MoveToWorld(HavenSpots.SickStall, Map.Trammel);
            trial.Horse = horse;
            Line(pm, HorseLine);
            if (TutorialStableHorseStore.Has(pm))
            {
                Line(pm, HorseReplayLine);
            }
            else if (pm.Followers + horse.ControlSlots > pm.FollowersMax)
            {
                Line(pm, HorseNoSlotLine);
            }
        }

        public static void TickStable(Trial trial)
        {
            if (trial.Horse is not { Deleted: false } horse) { return; }
            var pm = trial.Player;
            if (horse.Healed && !trial.Steps.Contains(SkillName.Veterinary.ToString()))
            {
                Step(trial, SkillName.Veterinary.ToString());
            }

            if (horse.Controlled && horse.ControlMaster == pm && !trial.Steps.Contains(SkillName.AnimalTaming.ToString()))
            {
                if (!TutorialStableHorseStore.Has(pm))
                {
                    TutorialStableHorseStore.Add(pm);
                    horse.Kept = true;
                    Line(pm, HorseKeptLine);
                }

                Step(trial, SkillName.AnimalTaming.ToString());
            }
        }

        /// <summary>At the lesson's end: a kept horse stays with its owner; any other goes back to its stall (deleted).</summary>
        public static void ReturnStable(Trial trial)
        {
            if (trial.Horse is { Deleted: false } horse && !horse.Kept)
            {
                horse.Delete();
            }

            trial.Horse = null;
        }

        // -- Seeding in New Haven (items 2, 3, 4, 6, 9) --

        public sealed record HavenPiece(int Id, Point3D At, string Name);

        /// <summary>The fixed pieces this prompt adds to New Haven. Each sign stands north of its path, beside it.</summary>
        public static readonly HavenPiece[] HavenPieces =
        [
            new(0x0B2C, HavenSpots.RookBench[0], "wooden bench"),
            new(0x0B2C, HavenSpots.RookBench[1], "wooden bench"),
            new(0xA854, new Point3D(3702, 2745, 0), "The mountain path: the mining lesson's cave is west, round the hill"),
            new(0xA854, new Point3D(3610, 2613, 0), "The stables: the sick-bay stall is inside, on the left"),
            new(0x0FFA, new Point3D(3605, 2620, 0), "bucket of water"),
            new(0x1036, new Point3D(3606, 2619, 0), "clean straw"),
            new(0x0F34, new Point3D(3605, 2619, 0), "clean straw"),
            new(0xA854, new Point3D(3713, 2591, 0), "Housing lesson, stop 1: a small house"),
            new(0xA854, new Point3D(3704, 2604, 0), "Housing lesson, stop 2: where houses can be built")
        ];

        /// <summary>
        /// Places what is missing in New Haven, moves an older world's Rook to his bench and takes away the old west farm.
        /// Returns a line for the log.
        /// </summary>
        public static string SeedHaven(bool dryRun)
        {
            var map = HavenSpots.Map;
            var placed = 0;
            var moved = MoveRook(dryRun) ? 1 : 0;
            // Upgrade in place (the building guide, section 7): a named piece found anywhere but its spot is taken away.
            var moved743 = 0;
            foreach (var piece in HavenPieces)
            {
                foreach (var item in StrayPieces(map, piece))
                {
                    moved743++;
                    if (!dryRun) { item.Delete(); }
                }
            }

            foreach (var piece in HavenPieces)
            {
                if (HasPiece(map, piece)) { continue; }
                placed++;
                if (!dryRun)
                {
                    // A signpost stands on the land; a prop indoors keeps its floor's Z.
                    var z = piece.Id == 0xA854 ? map.GetAverageZ(piece.At.X, piece.At.Y) : piece.At.Z;
                    new Static(piece.Id) { Name = piece.Name }.MoveToWorld(new Point3D(piece.At.X, piece.At.Y, z), map);
                }
            }

            var farm = TutorialFarm.ClearHaven(dryRun);
            return $"New Haven (cc-P743){(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {placed} pieces (took away {moved743} from old spots), moved {moved} Rook, {farm}";
        }

        private static List<Item> StrayPieces(Map map, HavenPiece piece)
        {
            var stray = new List<Item>();
            foreach (var item in map.GetItemsInRange(piece.At, 30))
            {
                if (item is Static && item.ItemID == piece.Id && item.Name == piece.Name &&
                    !Array.Exists(HavenPieces, q => q.Id == piece.Id && q.Name == piece.Name && q.At.X == item.X && q.At.Y == item.Y))
                {
                    stray.Add(item);
                }
            }

            return stray;
        }

        private static bool HasPiece(Map map, HavenPiece piece)
        {
            foreach (var item in map.GetItemsAt(piece.At.X, piece.At.Y))
            {
                if (item.ItemID == piece.Id && item.Name == piece.Name) { return true; }
            }

            return false;
        }

        /// <summary>An older world's Rook (anywhere in town, not at his bench) moves to the bench, seated, facing the square.</summary>
        public static bool MoveRook(bool dryRun)
        {
            if (FindOldRook() is not { } rook)
            {
                return false;
            }

            if (!dryRun)
            {
                rook.MoveToWorld(HavenSpots.Rook, HavenSpots.Map);
                rook.Home = HavenSpots.Rook;
                rook.Direction = Direction.North;
            }

            return true;
        }

        /// <summary>A Rook in town that is not on his bench (an older world's, on the square), or null.</summary>
        public static TutorialRecruiter? FindOldRook()
        {
            foreach (var m in HavenSpots.Map.GetMobilesInRange(HavenSpots.Centre, 100))
            {
                if (m is TutorialRecruiter { Deleted: false } rook && rook.Location != HavenSpots.Rook)
                {
                    return rook;
                }
            }

            return null;
        }
    }

    /// <summary>cc-P743: the characters that have kept their stable horse (one per character, never again).</summary>
    public class TutorialStableHorseStore : GenericPersistence
    {
        private static TutorialStableHorseStore? _instance;
        internal static readonly HashSet<Serial> Kept = new();

        public TutorialStableHorseStore() : base("TutorialStableHorses", 10)
        {
        }

        public static TutorialStableHorseStore Instance => _instance ??= new TutorialStableHorseStore();

        public static void Configure() => _ = Instance;

        public static bool Has(Mobile m) => Kept.Contains(m.Serial);

        public static void Add(Mobile m) => Kept.Add(m.Serial);

        public override void Serialize(IGenericWriter writer)
        {
            writer.WriteEncodedInt(0);
            writer.WriteEncodedInt(Kept.Count);
            foreach (var s in Kept)
            {
                writer.Write(s);
            }
        }

        public override void Deserialize(IGenericReader reader)
        {
            reader.ReadEncodedInt();
            Kept.Clear();
            var n = reader.ReadEncodedInt();
            for (var i = 0; i < n; i++)
            {
                Kept.Add(reader.ReadSerial());
            }
        }
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    /// <summary>cc-P743 item 7: the magery lesson's mage echo, who shows Magic Arrow at the training echo.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialMageEcho : TutorialCreature
    {
        public Serial TrialOwner { get; set; }

        public TutorialMageEcho() : base(AIType.AI_Animal)
        {
            Name = "a mage echo";
            Body = 0x190;
            Hue = 0x4F2;
            FightMode = FightMode.None;
            CantWalk = true;
            Blessed = true;
            SetStr(40);
            SetDex(40);
            SetInt(80);
            SetHits(100);
            AddItem(new Robe { Movable = false, Hue = 0x4F2 });
            AddItem(new WizardsHat { Movable = false, Hue = 0x4F2 });
        }

        public override bool ClickTitle => false;

        /// <summary>The words of power, the cast motion, then the bolt (pinned MagicArrowSpell's effect and sound).</summary>
        public void ShowMagicArrow(Mobile target)
        {
            Direction = GetDirectionTo(target);
            PublicOverheadMessage(MessageType.Spell, 0x3B2, false, "In Por Ylem");
            Animate(16, 7, 1, true, false, 0);
            Server.Timer.StartTimer(TimeSpan.FromSeconds(1.2), () =>
            {
                if (Deleted || target.Deleted) { return; }
                MovingParticles(target, 0x36E4, 5, 0, false, true, 3006, 4006, 0);
                target.PlaySound(0x1E5);
            });
        }

        [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
    }

    /// <summary>
    /// cc-P743 item 4: the stables' injured horse. Only its trainee sees it until it is tamed; it cannot be tamed until
    /// that trainee has bandaged it, and then always can (MinTameSkill 0 against the lesson's 60, no anger). Bleeds while
    /// hurt. A tamed, kept horse is an ordinary owned mount from then on.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialStableHorse : Horse, IPrivateMobile
    {
        [SerializableField(0)]
        private Mobile? _trainee;

        [SerializableField(1)]
        private bool _kept;

        public bool Healed { get; private set; }
        private DateTime _nextBleed;

        public TutorialStableHorse(Mobile trainee)
        {
            Name = "an injured horse";
            _trainee = trainee;
            Tamable = false;
            MinTameSkill = 0.0;
            Hits = Math.Max(1, HitsMax / 10);
            CantWalk = true;
            Direction = Direction.North;
        }

        public bool ShowsTo(Mobile viewer) => Kept || Controlled || viewer == Trainee;

        public override void OnHeal(ref int amount, Mobile from)
        {
            base.OnHeal(ref amount, from);
            if (!Healed && from == Trainee && amount > 0)
            {
                Healed = true;
                Tamable = true;
                Name = "a calmer horse";
                if (Trainee != null) { TutorialWingTrials.Line(Trainee, TutorialWingTrials.HorseHealedLine); }
            }
        }

        public override void OnThink()
        {
            base.OnThink();
            if (Controlled)
            {
                CantWalk = false;
                if (Name is "an injured horse" or "a calmer horse") { Name = "a horse"; }
                return;
            }

            if (!Healed && Core.Now >= _nextBleed)
            {
                _nextBleed = Core.Now + TimeSpan.FromSeconds(4);
                new Blood(Utility.RandomList(0x122A, 0x122B, 0x122D)).MoveToWorld(Location, Map);
            }
        }

        [AfterDeserialization(false)]
        private void ClearAbandonedLesson()
        {
            if (!Kept && !Controlled) { Delete(); }
        }
    }
}
