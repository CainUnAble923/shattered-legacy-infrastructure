// cc-P276 (F-35): the New Haven tutorial farm. Notes: shard-migration notes/cc-P276-tutorial-farm-build.md; the design
// and the approved site: docs/feature-queue.md F-35 (the west site, the v3 farmhouse, Chase 2026-10-04 "looks good").
//
// What it is, west of New Haven by the Bardic Guild (field fence x 3381-3399, y 2603-2619, the farmhouse at 3377,2611
// against its west fence, the gate on the east side at 3399,2614-2615 facing town):
//   - TutorialFarmAddon: the farmhouse, fence, pen, crop rows, hay, scarecrows, well and coop, every piece stock art at
//     the approved tile and Z (TutorialFarmLayout, generated from the v3 layout). One addon, server-placed.
//   - A real dark wood door in the farmhouse's east wall (the layout's 0x06AD), so the porch opens into the field.
//   - The Farmhand (Wren Tallow) at the gate, who runs the farm's lesson (below).
//   - Two spawners: the field's pests (tutorial-only: they engage only tutorial participants, TutorialCreature) and the
//     pen's stock goats, sheep, cows and chickens.
// The map patch that clears the two trees inside the fence (player-package/vendor/shattered-legacy-map/trammel.mapops)
// must be on the server's map first: the seeder refuses while the trees are still there, unless "force".
//
// The lesson (F-35's order: tame the tutorial pet, fight the pests with it, heal), the station "animals.taming":
//   1. The Farmhand offers a dog or a cat, the evo pet (EvoPet.GivePet: bonded at once, the player's hue pick).
//      Key farm.pet. An account that took its pet here is not offered the pick again at graduation.
//   2. Clear the field: PestsToClear pests that the player or the pet damaged, killed. Key farm.pests; completes the
//      station animals.taming. The count is kept in memory (a restart mid-step starts the count again; noted).
//   3. Patch up: the Farmhand hands bandages; a successful Healing or Veterinary use (SkillEvents.SkillUsed) finishes it.
//      Key farm.heal; completes the station combat.healing.
// Keys are TutorialRecord.StationsDone strings (no schema change, RootdeepVault's pattern); only the two station keys
// count toward the core, and neither is a core station.
//
// The seeder is a staff command, test shard only for now:
//   [ClusterFSeedTutorialFarm [dryrun | force]   places what is missing; moves and deletes nothing; idempotent.
// It refuses on any shard that is not the test shard (ClusterFStaffHub.IsTestShard) unless clusterf.tutorialFarm.allowLive
// is true, which nobody sets until Chase says the farm goes live.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial
{
    public static class TutorialFarm
    {
        public const string PetKey = "farm.pet";
        public const string PestsKey = "farm.pests";
        public const string HealKey = "farm.heal";
        public const string TamingStation = "animals.taming";
        public const string HealingStation = "combat.healing";

        public static readonly string[] FarmPetLines = ["dog", "cat"];

        /// <summary>Just outside the gate, beside the sign (3401,2613).</summary>
        public static readonly Point3D FarmhandSpot = new(3400, 2614, 0);

        /// <summary>The crop rows the pests live in, and the pen (inside its own fence, x 3393-3398, y 2604-2608).</summary>
        public static readonly Point2D PestSpot = new(3388, 2614);
        public const int PestRange = 3;
        public const int PestCount = 3;
        public static readonly Point2D PenSpot = new(3395, 2606);
        public const int PenRange = 2;
        public const int PenCount = 4;

        public static readonly string[] PestTypes = [nameof(FarmRat), nameof(FieldRat), nameof(GrassSnake)];
        public static readonly string[] PenTypes = ["Goat", "Sheep", "Cow", "Chicken"];

        public static int PestsToClear => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.pestsToClear", 3);
        public static int BandagesGiven => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.bandages", 10);
        public static bool AllowLive => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.allowLive", false);

        /// <summary>Whether this is the test shard. Tests replace it.</summary>
        public static Func<bool> IsTestShard { get; set; } = () => ClusterFStaffHub.IsTestShard;

        /// <summary>Whether the server's Trammel has the map patch (the two trees gone). Tests replace it.</summary>
        public static Func<bool> MapPatched { get; set; } = () => TreesLeft(Map.Trammel) == 0;

        // The Farmhand's lines (ours, for Chase's eye) and the Echo's (the "System:" voice).
        public const string NotTraineeLine = "Farm's for the League's trainees. Nothing personal. The pests would only embarrass you.";
        public const string PetOfferLine = "Every farm needs a dog. Or a cat, if you like being ignored. Pick one; it's yours to keep.";
        public const string NoSlotLine = "You've no room for another follower. Come back with a free slot.";
        public const string PestsLine = "Rats in the cabbages again. Take your friend into the rows and clear out {0}. They don't hit back hard.";
        public const string PestsLeftLine = "{0} more. Mind the snake; it sulks.";
        public const string HealLine = "You're bleeding on my carrots. Here, bandages. Patch yourself up, or your friend. Then we're square.";
        public const string DoneLine = "That's the farm. Come back if your friend ever needs a hand; the Bond Trial's mine to give, when it's time.";
        public const string EchoPetLine = "A pet. It will follow you through every loop. Try to be worth following.";
        public const string EchoPestsLine = "Three vermin dead. A career is born.";
        public const string EchoHealLine = "Bandaged. You will do that a great deal.";

        // Pests counted per character this run (not saved: a restart starts the step's count again).
        private static readonly Dictionary<Serial, int> _pestsKilled = new();

        private static bool _hooked;

        // The command and the hooks are only needed once the world is up, so Initialize (adds no Configure to pinned's list).
        public static void Initialize()
        {
            CommandSystem.Register("ClusterFSeedTutorialFarm", AccessLevel.Administrator, OnSeedCommand);
            EnsureHooks();
        }

        /// <summary>The station's handler and the heal hook, once. The test host calls it: Initialize never runs there.</summary>
        public static void EnsureHooks()
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            TutorialStations.Handlers[TamingStation] = (pm, station) => PointTo(pm);
            SkillEvents.SkillUsed += OnSkillUsed;
        }

        public static TutorialRecord? RecordOf(PlayerMobile pm) => ClusterFTutorial.RecordOf(pm);

        public static bool Has(PlayerMobile pm, string key) => RecordOf(pm)?.StationsDone.Contains(key) == true;

        public static int PestsKilled(PlayerMobile pm) => _pestsKilled.TryGetValue(pm.Serial, out var n) ? n : 0;

        /// <summary>The step a participant is on: 1 pet, 2 pests, 3 heal, 4 done; 0 when not a participant.</summary>
        public static int StepOf(PlayerMobile pm) =>
            !ClusterFTutorial.IsParticipant(pm) ? 0 :
            !Has(pm, PetKey) ? 1 :
            !Has(pm, PestsKey) ? 2 :
            !Has(pm, HealKey) ? 3 : 4;

        /// <summary>The station's handler: a line and a quest arrow to the Farmhand.</summary>
        public static void PointTo(PlayerMobile pm)
        {
            ClusterFTutorial.SystemLine(pm, "The farm is west of town, past the Bardic Guild. The Farmhand is at the gate. Follow the arrow.");
            if (TutorialFarmhand.Find() is { } hand && hand.Map == pm.Map)
            {
                pm.QuestArrow = new GuildDirectionArrow(pm, hand, hand.Location, hand.Map, "the Farmhand");
            }
        }

        /// <summary>The player talks to the Farmhand (double-click): the next step. Returns the step it answered for.</summary>
        public static int Talk(PlayerMobile pm, Mobile hand)
        {
            var step = StepOf(pm);

            switch (step)
            {
                case 0:
                    hand.SayTo(pm, NotTraineeLine);
                    break;
                case 1:
                    hand.SayTo(pm, PetOfferLine);
                    pm.CloseGump<TutorialFarmPetGump>();
                    pm.SendGump(new TutorialFarmPetGump());
                    break;
                case 2:
                    var left = Math.Max(0, PestsToClear - PestsKilled(pm));
                    hand.SayTo(pm, PestsKilled(pm) == 0 ? string.Format(PestsLine, PestsToClear) : string.Format(PestsLeftLine, left));
                    break;
                case 3:
                    hand.SayTo(pm, HealLine);
                    GiveBandages(pm);
                    break;
                default:
                    hand.SayTo(pm, DoneLine);
                    break;
            }

            return step;
        }

        /// <summary>Step 1: the pet. Null when refused (not on this step, not a farm line, or no follower slot).</summary>
        public static EvoPet? ChoosePet(PlayerMobile pm, string line)
        {
            if (StepOf(pm) != 1 || Array.IndexOf(FarmPetLines, line) < 0)
            {
                return null;
            }

            var pet = EvoPet.GivePet(pm, line);

            if (pet == null)
            {
                pm.SendMessage(0x22, NoSlotLine);
                return null;
            }

            RecordOf(pm)!.StationsDone.Add(PetKey);
            ClusterFTutorial.SystemLine(pm, EchoPetLine);
            return pet;
        }

        /// <summary>Step 2: a pest died. Every participant on step 2 who (or whose pet) damaged it counts one.</summary>
        public static void OnPestDeath(FarmPest pest)
        {
            var credited = new HashSet<PlayerMobile>();

            foreach (var entry in pest.DamageEntries.ToArray())
            {
                var who = entry.Damager is BaseCreature { Controlled: true } pet ? pet.ControlMaster : entry.Damager;

                if (who is PlayerMobile pm && StepOf(pm) == 2 && credited.Add(pm))
                {
                    CountPest(pm);
                }
            }
        }

        public static void CountPest(PlayerMobile pm)
        {
            var n = PestsKilled(pm) + 1;
            _pestsKilled[pm.Serial] = n;

            if (n < PestsToClear)
            {
                pm.SendMessage(0x3B2, $"Pests cleared: {n} of {PestsToClear}.");
                return;
            }

            _pestsKilled.Remove(pm.Serial);
            RecordOf(pm)!.StationsDone.Add(PestsKey);
            ClusterFTutorial.SystemLine(pm, EchoPestsLine);
            ClusterFTutorial.CompleteStation(pm, TamingStation);
            ClusterFTutorial.SystemLine(pm, "Go back to the Farmhand.");
        }

        public static void GiveBandages(PlayerMobile pm)
        {
            if (pm.Backpack?.FindItemByType<Bandage>() == null)
            {
                pm.AddToBackpack(new Bandage(BandagesGiven));
            }
        }

        // Step 3: a bandage that worked. Healing (self or another) and Veterinary (the pet) both roll their skill on a
        // successful heal (pinned Bandage.cs), and SkillCheck tells SkillEvents.SkillUsed.
        private static void OnSkillUsed(Mobile from, Skill skill, bool success)
        {
            if (success && skill.SkillName is SkillName.Healing or SkillName.Veterinary && from is PlayerMobile pm)
            {
                OnHealed(pm);
            }
        }

        public static bool OnHealed(PlayerMobile pm)
        {
            if (StepOf(pm) != 3)
            {
                return false;
            }

            RecordOf(pm)!.StationsDone.Add(HealKey);
            ClusterFTutorial.SystemLine(pm, EchoHealLine);
            ClusterFTutorial.CompleteStation(pm, HealingStation);
            return true;
        }

        // -- The seeder --

        public readonly record struct SeedResult(bool Refused, int Addons, int Doors, int Farmhands, int Spawners, int Present, string Message);

        /// <summary>How many of the two trees' four statics the server's map still holds (0 once the patch is on).</summary>
        public static int TreesLeft(Map map)
        {
            var n = 0;
            foreach (var (id, x, y, z) in TutorialFarmLayout.RemovedTrees)
            {
                foreach (var tile in map.Tiles.GetStaticTiles(x, y))
                {
                    if (tile.ID == id && tile.Z == z)
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        [Usage("ClusterFSeedTutorialFarm [dryrun | force]")]
        [Description("Places the New Haven tutorial farm (the farmhouse and field addon, its door, the Farmhand and the pest and pen spawners) where missing. Test shard only unless clusterf.tutorialFarm.allowLive; refuses while the map still has the two trees the farm's map patch removes, unless force. Moves and deletes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the New Haven tutorial farm (farmhouse, field, Farmhand, spawners) on the test shard. Moves and deletes nothing.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
            if (mode is not ("" or "dryrun" or "force"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedTutorialFarm [dryrun | force]");
                return;
            }

            e.Mobile.SendMessage(Seed(mode == "dryrun", mode == "force").Message);
        }

        public static SeedResult Seed(bool dryRun, bool force = false)
        {
            if (!IsTestShard() && !AllowLive)
            {
                return new SeedResult(true, 0, 0, 0, 0, 0,
                    "Tutorial farm: not seeded. This is not the test shard (and clusterf.tutorialFarm.allowLive is off).");
            }

            if (!force && !MapPatched())
            {
                return new SeedResult(true, 0, 0, 0, 0, 0,
                    "Tutorial farm: not seeded. The server's map still has the two trees inside the fence; load the map patch first (or use force).");
            }

            var map = Map.Trammel;
            int addons = 0, doors = 0, hands = 0, spawners = 0, present = 0;

            if (FindAddon(map) != null) { present++; }
            else
            {
                if (!dryRun) { new TutorialFarmAddon().MoveToWorld(TutorialFarmLayout.Anchor, map); }
                addons++;
            }

            if (FindDoor(map) != null) { present++; }
            else
            {
                if (!dryRun) { MakeDoor().MoveToWorld(TutorialFarmLayout.DoorSpot, map); }
                doors++;
            }

            if (TutorialFarmhand.Find() != null) { present++; }
            else
            {
                if (!dryRun)
                {
                    var spot = new Point3D(FarmhandSpot.X, FarmhandSpot.Y, map.GetAverageZ(FarmhandSpot.X, FarmhandSpot.Y));
                    new TutorialFarmhand { Direction = Direction.East, Home = spot, RangeHome = 0 }.MoveToWorld(spot, map);
                }

                hands++;
            }

            foreach (var (spot, range, count, types) in SpawnerRows)
            {
                if (FindSpawner(map, spot, types[0]) != null) { present++; continue; }

                if (!dryRun)
                {
                    var s = new Spawner(count, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), 0, default, types)
                    {
                        Name = $"Tutorial farm: {string.Join(", ", types)}"
                    };
                    s.MoveToWorld(new Point3D(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)), map);
                    s.HomeRange = range; // after the move: the bounds are centred on the spawner (BaseSpawner.cs:189-219)
                    s.Respawn();
                }

                spawners++;
            }

            var verb = dryRun ? "would place" : "placed";
            return new SeedResult(false, addons, doors, hands, spawners, present,
                $"Tutorial farm{(dryRun ? " (dry run)" : "")}: {verb} {addons} farm addon, {doors} door, {hands} Farmhand, {spawners} spawners; already there {present}.");
        }

        public static readonly (Point2D Spot, int Range, int Count, string[] Types)[] SpawnerRows =
        [
            (PestSpot, PestRange, PestCount, PestTypes),
            (PenSpot, PenRange, PenCount, PenTypes)
        ];

        /// <summary>The layout's door art 0x06AD: dark wood, facing SouthCW = 4 (closed id 0x06A5 + 2 x facing, Doors.cs DarkWoodDoor).</summary>
        public static BaseDoor MakeDoor() => new DarkWoodDoor(DoorFacing.SouthCW);

        public static TutorialFarmAddon? FindAddon(Map map)
        {
            foreach (var item in map.GetItemsInRange<TutorialFarmAddon>(TutorialFarmLayout.Anchor, 2))
            {
                if (!item.Deleted)
                {
                    return item;
                }
            }

            return null;
        }

        public static BaseDoor? FindDoor(Map map)
        {
            foreach (var item in map.GetItemsInRange<BaseDoor>(TutorialFarmLayout.DoorSpot, 0))
            {
                if (!item.Deleted)
                {
                    return item;
                }
            }

            return null;
        }

        public static Spawner? FindSpawner(Map map, Point2D spot, string firstType)
        {
            foreach (var item in map.GetItemsInRange<Spawner>(new Point3D(spot.X, spot.Y, 0), 2))
            {
                if (!item.Deleted && item.Entries.Count > 0 && item.Entries[0].SpawnedName.Equals(firstType, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }

    /// <summary>The Farmhand's offer: a dog or a cat.</summary>
    public class TutorialFarmPetGump : Gump
    {
        public TutorialFarmPetGump() : base(140, 140)
        {
            AddPage(0);
            AddBackground(0, 0, 360, 130, 9270);
            AddAlphaRegion(10, 10, 340, 110);
            AddLabel(24, 18, 1154, "The Farmhand's dog or cat");
            for (var i = 0; i < TutorialFarm.FarmPetLines.Length; i++)
            {
                AddButton(24, 50 + i * 30, 4005, 4007, 1 + i);
                AddLabel(60, 52 + i * 30, 999, TutorialFarm.FarmPetLines[i]);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;
            if (sender.Mobile is PlayerMobile pm && i >= 0 && i < TutorialFarm.FarmPetLines.Length)
            {
                TutorialFarm.ChoosePet(pm, TutorialFarm.FarmPetLines[i]);
            }
        }
    }
}

namespace Server.Items
{
    using Server.Tutorial;

    /// <summary>The tutorial farm: every piece of the approved v3 layout, anchored on the farmhouse's centre.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialFarmAddon : BaseAddon
    {
        [Constructible]
        public TutorialFarmAddon()
        {
            var a = TutorialFarmLayout.Anchor;
            foreach (var (id, x, y, z) in TutorialFarmLayout.Pieces)
            {
                AddComponent(new AddonComponent(id), x - a.X, y - a.Y, z - a.Z);
            }
        }

        public override string DefaultName => "the League farm";
    }
}

namespace Server.Mobiles
{
    using Server.Items;
    using Server.Tutorial;

    /// <summary>Wren Tallow, at the farm gate: runs the farm's lesson (TutorialFarm.Talk).</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialFarmhand : BaseCreature
    {
        [Constructible]
        public TutorialFarmhand() : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            Name = "Wren Tallow";
            Title = "the League farmhand";
            Body = 0x191;
            Female = true;
            Hue = 0x83EA;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            CantWalk = true;

            AddItem(new Shirt { Movable = false, Hue = 0x2C });
            AddItem(new ShortPants { Movable = false, Hue = 0x455 });
            AddItem(new HalfApron { Movable = false, Hue = 0x8A5 });
            AddItem(new Boots { Movable = false });
            AddItem(new StrawHat { Movable = false });
            AddItem(new Pitchfork { Movable = false });
            AddItem(new Item(0x203C) { Movable = false, Hue = 0x2C, Layer = Layer.Hair });
        }

        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!pm.InRange(Location, 4))
            {
                pm.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            TutorialFarm.Talk(pm, this);
        }

        public static TutorialFarmhand? Find()
        {
            foreach (var m in Map.Trammel.GetMobilesInRange(TutorialFarm.FarmhandSpot, 10))
            {
                if (m is TutorialFarmhand h && !h.Deleted)
                {
                    return h;
                }
            }

            return null;
        }
    }

    /// <summary>A farm pest: a tutorial-only creature (it engages only participants) that counts for the farm's step 2.</summary>
    [SerializationGenerator(0, false)]
    public abstract partial class FarmPest : TutorialCreature
    {
        protected FarmPest()
        {
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            TutorialFarm.OnPestDeath(this);
        }
    }

    [SerializationGenerator(0, false)]
    public partial class FarmRat : FarmPest
    {
        [Constructible]
        public FarmRat()
        {
            Name = "a farm rat";
            Body = 0xEE;
            BaseSoundID = 0xCC;
            SetStr(15);
            SetDex(30);
            SetInt(5);
            SetHits(10);
            SetDamage(1, 2);
            SetSkill(SkillName.Wrestling, 15.0);
            SetSkill(SkillName.Tactics, 15.0);
        }

        public override string CorpseName => "a rat corpse";
    }

    [SerializationGenerator(0, false)]
    public partial class FieldRat : FarmPest
    {
        [Constructible]
        public FieldRat()
        {
            Name = "a field rat";
            Body = 0xD7;
            BaseSoundID = 0x188;
            SetStr(25);
            SetDex(35);
            SetInt(8);
            SetHits(16);
            SetDamage(2, 3);
            SetSkill(SkillName.Wrestling, 20.0);
            SetSkill(SkillName.Tactics, 20.0);
        }

        public override string CorpseName => "a rat corpse";
    }

    [SerializationGenerator(0, false)]
    public partial class GrassSnake : FarmPest
    {
        [Constructible]
        public GrassSnake()
        {
            Name = "a grass snake";
            Body = 0x34;
            BaseSoundID = 0xDB;
            Hue = 0x59F;
            SetStr(20);
            SetDex(30);
            SetInt(5);
            SetHits(14);
            SetDamage(1, 3);
            SetSkill(SkillName.Wrestling, 20.0);
            SetSkill(SkillName.Tactics, 20.0);
        }

        public override string CorpseName => "a snake corpse";
    }
}
