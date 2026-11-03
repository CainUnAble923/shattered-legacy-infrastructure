// cc-P782: grave digging, a Forensics harvest at graves. The idea is from Massapequa's Grave Digging (servuo.dev,
// assessed in cc-P778); the code, the cemetery rule, the numbers and the words are ours. No stock file is replaced
// (the source replaced HarvestTarget.cs); the spade is its own harvest tool on its own system. Notes in shard-migration
// notes/cc-P782-grave-digging.md.
//
//   The tool      GravediggersSpade (a stock harvest tool): double-click, target a grave within maxRange. Tinkers sell it
//                 (SBTinker-gravediggers-spade.patch).
//   Cemeteries    the grave must lie in a region whose name holds a regionKeyword, or in one of the table's areas
//                 (InCemetery). A grave anywhere else is refused.
//   Nodes         each grave tile is a node of its own (pinned HarvestBank at 1 x 1 tiles), shared by every digger: the
//                 source made a fresh node at each player's feet, so stepping aside gave endless digs.
//   The roll      stock HarvestSystem.FinishHarvesting: the node must hold a dig; the Forensics check (successMinSkill to
//                 successMaxSkill) passes or only earth turns up; then a find is drawn by weight from the digger's tier
//                 (PickFind), min to max of it. One dig takes one from the node. Then, on a find, the bonus roll.
//   The bonus     chancePercent from bonus minSkill: one entry drawn by weight from those open at the digger's Forensics
//                 grade, made by the loot boxes' own factory (LootBoxRoller.MakeRow). Never gear; given unblessed.
//   No penalty    no karma, no criminal flag, no guards (cc-P782: not for now).
//   Rewards       bones, fertile dirt, necromancy reagents, human remains, a low treasure map, grave decor. Nothing
//                 grants skill or stat points (Forensics gains by use, as any harvest), power scrolls, bless deeds or
//                 artifacts, and nothing magic is made.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.Harvest;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Engines.GraveDigging
{
    /// <summary>A grave given directly (staff tools and the facts): the map, the spot and the tile there.</summary>
    public sealed record GraveSpot(Map Map, Point3D Location, int TileID);

    public class GraveDigging : HarvestSystem
    {
        private static GraveDigging _system;

        public static GraveDigging System => _system ??= new GraveDigging();

        /// <summary>The roll the find and bonus draws use (0 inclusive to 1); a fact swaps it.</summary>
        public static Func<double> Roll { get; set; } = Utility.RandomDouble;

        /// <summary>The roll the bonus chance uses (0 inclusive to 100); a fact swaps it.</summary>
        public static Func<double> BonusChanceRoll { get; set; } = () => Utility.RandomDouble() * 100.0;

        public HarvestDefinition Graves { get; }

        // The current dig's find (set in GetResourceType, read in Give and OnHarvestFinished): the game loop is single
        // threaded, and FinishHarvesting runs one dig start to end.
        private GraveFind _pending;
        private bool _found;

        private GraveDigging()
        {
            Graves = new HarvestDefinition
            {
                RangedTiles = true,
                Skill = SkillName.Forensics,
                BankWidth = 1,
                BankHeight = 1,
                ConsumedPerHarvest = 1,
                ConsumedPerFeluccaHarvest = 1,
                PlaceAtFeetIfFull = true,
                LandTiles = [],
                EffectActions = [11],          // the digging swing
                EffectSounds = [0x125, 0x126], // spade in earth
                EffectCounts = [2],
                EffectDelay = TimeSpan.FromSeconds(1.6),
                EffectSoundDelay = TimeSpan.FromSeconds(0.9),
                NoResourcesMessage = "This grave has given up all it will for now. Try another.",
                DoubleHarvestMessage = "Someone has dug this grave out before you. Try another.",
                TimedOutOfRangeMessage = "You have stepped away from the grave.",
                OutOfRangeMessage = "That grave is too far away to dig.",
                FailMessage = "You turn over the earth but find nothing worth keeping.",
                PackFullMessage = "Your pack is full; what you dug up falls back into the grave.",
                ToolBrokeMessage = "The blade of your spade has cracked.",
                RaceBonus = false,
                BonusResources = null,
            };

            Definitions = [Graves];
            Apply(GraveDiggingTable.Current);
            GraveDiggingTable.Changed += () => Apply(GraveDiggingTable.Current);
        }

        /// <summary>Puts the table's numbers into the definition. Changed node sizes start every node afresh.</summary>
        public void Apply(GraveDiggingTable t)
        {
            if (Graves.MinTotal != t.Node.MinTotal || Graves.MaxTotal != t.Node.MaxTotal)
                Graves.Banks.Clear();

            Graves.MinTotal = t.Node.MinTotal;
            Graves.MaxTotal = t.Node.MaxTotal;
            Graves.MinRespawn = TimeSpan.FromMinutes(t.Node.MinRespawnMinutes);
            Graves.MaxRespawn = TimeSpan.FromMinutes(t.Node.MaxRespawnMinutes);
            Graves.MaxRange = t.MaxRange;
            Graves.StaticTiles = t.GraveRanges;

            // One resource: the Forensics check of a dig. The find is drawn in GetResourceType.
            var earth = new HarvestResource(t.MinSkill, t.SuccessMinSkill, t.SuccessMaxSkill, "", typeof(Bone));
            Graves.Resources = [earth];
            Graves.Veins = [new HarvestVein(1000, 0.0, earth, null)];
        }

        // -- Gates ------------------------------------------------------------------------------------------

        /// <summary>Why this character may not dig now, or null.</summary>
        public static string WhyNot(Mobile from, Item tool)
        {
            var t = GraveDiggingTable.Current;
            if (!t.Enabled) return "Grave digging is closed for now.";
            if (from.Skills[SkillName.Forensics].Base < t.MinSkill) return $"You need at least {t.MinSkill:0.#} Forensic Evaluation to dig a grave.";
            if (tool == null || tool.Deleted || from.Backpack == null || !tool.IsChildOf(from.Backpack)) return "The spade must be in your backpack.";
            return null;
        }

        /// <summary>A cemetery: a region whose name holds a keyword, or one of the table's areas.</summary>
        public static bool InCemetery(Map map, Point3D p)
        {
            if (map == null || map == Map.Internal)
                return false;

            var t = GraveDiggingTable.Current;
            foreach (var a in t.Areas)
                if (a.Contains(map, p.X, p.Y))
                    return true;

            for (var r = Region.Find(p, map); r != null; r = r.Parent)
                if (r.Name != null && t.RegionKeywords.Any(k => r.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return true;

            return false;
        }

        public override bool CheckHarvest(Mobile from, Item tool)
        {
            if (!base.CheckHarvest(from, tool))
                return false;

            if (WhyNot(from, tool) is { } why)
            {
                from.SendMessage(why);
                return false;
            }

            return true;
        }

        public override bool CheckHarvest(Mobile from, Item tool, HarvestDefinition def, object toHarvest)
        {
            if (!CheckHarvest(from, tool))
                return false;

            if (!GetHarvestDetails(from, tool, toHarvest, out _, out var map, out var loc, out _))
                return false;

            if (!InCemetery(map, loc))
            {
                from.SendMessage("That grave lies outside any cemetery. Leave the dead in the wilds where they lie.");
                return false;
            }

            return true;
        }

        public override void OnBadHarvestTarget(Mobile from, Item tool, object toHarvest) =>
            from.SendMessage("Use the spade on a grave in a cemetery.");

        public override bool GetHarvestDetails(Mobile from, Item tool, object toHarvest, out int tileID, out Map map, out Point3D loc, out bool isLand)
        {
            if (toHarvest is GraveSpot spot)
            {
                tileID = spot.TileID;
                map = spot.Map;
                loc = spot.Location;
                isLand = false;
                return map != null && map != Map.Internal;
            }

            return base.GetHarvestDetails(from, tool, toHarvest, out tileID, out map, out loc, out isLand);
        }

        // -- The find ---------------------------------------------------------------------------------------

        /// <summary>The find for a Forensics base and a roll from 0 (inclusive) to 1, by weight in the digger's tier.</summary>
        public static GraveFind PickFind(double forensics, double roll)
        {
            var tier = GraveDiggingTable.Current.TierFor(forensics);
            var finds = tier?.Finds.Where(f => f.Weight > 0 && f.Type != null).ToList();
            if (finds == null || finds.Count == 0)
                return null;

            var pick = roll * finds.Sum(f => f.Weight);
            foreach (var f in finds)
            {
                if (pick < f.Weight)
                    return f;
                pick -= f.Weight;
            }

            return finds[^1];
        }

        public override Type GetResourceType(Mobile from, Item tool, HarvestDefinition def, Map map, Point3D loc, HarvestResource resource)
        {
            _pending = PickFind(from.Skills[SkillName.Forensics].Base, Roll());
            return _pending?.Type;
        }

        // The find's own amount, min to max (pinned sets a stack to ConsumedPerHarvest before Give; the node still loses
        // one dig, as pinned consumed it before this).
        public override bool Give(Mobile m, Item item, bool placeAtFeet)
        {
            if (_pending != null && item != null && item.Stackable && item.GetType() == _pending.Type)
            {
                item.Amount = _pending.Max > _pending.Min ? Utility.RandomMinMax(_pending.Min, _pending.Max) : _pending.Min;
                _pending = null;
            }

            return base.Give(m, item, placeAtFeet);
        }

        public override void SendSuccessTo(Mobile from, Item item, HarvestResource resource)
        {
            _found = true;
            var what = item.Amount > 1 ? $"{item.Amount} {Describe(item)}" : Describe(item);
            from.SendMessage(0x3B2, $"You dig into the grave and turn up {what}.");
        }

        private static string Describe(Item item) => item.Name ?? (item.DefaultName is { Length: > 0 } n ? n : item.GetType().Name);

        // -- The bonus --------------------------------------------------------------------------------------

        /// <summary>The bonus entries open at a Forensics grade (minGrade to maxGrade), with weight.</summary>
        public static List<PoolEntry> BonusFor(int grade) =>
            GraveDiggingTable.Current.Bonus.Entries.Where(e => e.Weight > 0 && grade >= e.MinGrade && grade <= e.MaxGrade).ToList();

        /// <summary>The bonus entry for a Forensics base and a roll from 0 to 1, by weight, or null.</summary>
        public static PoolEntry PickBonus(double forensics, double roll)
        {
            var open = BonusFor(GraveDiggingTable.Current.GradeFor(forensics));
            if (open.Count == 0)
                return null;

            var pick = roll * open.Sum(e => e.Weight);
            foreach (var e in open)
            {
                if (pick < e.Weight)
                    return e;
                pick -= e.Weight;
            }

            return open[^1];
        }

        /// <summary>Whether this dig's bonus roll hits: the table's chance, from its minSkill.</summary>
        public static bool BonusHits(double forensics, double chanceRoll)
        {
            var b = GraveDiggingTable.Current.Bonus;
            return GraveDiggingTable.Current.Enabled && forensics >= b.MinSkill && chanceRoll < b.ChancePercent;
        }

        /// <summary>Makes a bonus entry for this digger through the loot boxes' factory; given unblessed. Null if it makes nothing.</summary>
        public static Item MakeBonus(PlayerMobile pm, PoolEntry e)
        {
            var item = LootBoxRoller.MakeRow(pm, e, GraveDiggingTable.Current.GradeFor(pm.Skills[SkillName.Forensics].Base));
            if (item != null && item.LootType == LootType.Blessed)
                item.LootType = LootType.Regular;
            return item;
        }

        public override void OnHarvestFinished(
            Mobile from, Item tool, HarvestDefinition def, HarvestVein vein, HarvestBank bank, HarvestResource resource, object harvested)
        {
            var found = _found;
            _found = false;
            _pending = null;

            if (!found || from is not PlayerMobile pm)
                return;

            var forensics = pm.Skills[SkillName.Forensics].Base;
            if (!BonusHits(forensics, BonusChanceRoll()) || PickBonus(forensics, Roll()) is not { } entry || MakeBonus(pm, entry) is not { } item)
                return;

            if (Give(pm, item, true))
                pm.SendMessage(0x44, $"Deeper down, your spade strikes something else: {Describe(item)}.");
            else
                item.Delete();
        }
    }

    /// <summary>Loads grave-digging.json at start and registers the staff command (cc-P782).</summary>
    public static class GraveDiggingSetup
    {
        public static void Configure()
        {
            foreach (var line in GraveDiggingTable.LoadOrWriteDefaults(GraveDiggingTable.FilePath))
                Console.WriteLine($"[GraveDigging] {line}");

            CommandSystem.Register("GraveDiggingStaff", AccessLevel.GameMaster, OnStaffCommand);
        }

        [Usage("GraveDiggingStaff [reload|status|here]")]
        [Description("reload reads Configuration/grave-digging.json again (a bad file is refused whole); status says the numbers in force; here says whether your spot is in a cemetery. The tinkers' spade price changes at the next restart.")]
        [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
        private static void OnStaffCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
            var t = GraveDiggingTable.Current;
            switch (mode)
            {
                case "reload":
                    foreach (var line in GraveDiggingTable.Reload(GraveDiggingTable.FilePath))
                        e.Mobile.SendMessage(line);
                    return;
                case "here":
                    e.Mobile.SendMessage(GraveDigging.InCemetery(e.Mobile.Map, e.Mobile.Location)
                        ? "This spot is in a cemetery: graves here can be dug."
                        : "This spot is not in a cemetery.");
                    return;
                default:
                    e.Mobile.SendMessage($"Grave digging {(t.Enabled ? "on" : "off")} ({t.Source}): {t.Areas.Count} areas, keywords " +
                                         $"{string.Join("/", t.RegionKeywords)}, nodes {t.Node.MinTotal}-{t.Node.MaxTotal}, tiers " +
                                         string.Join(", ", t.Tiers.Select(x => x.MinSkill)) + $", bonus {t.Bonus.ChancePercent}% from {t.Bonus.MinSkill}.");
                    return;
            }
        }
    }
}

namespace Server.Items
{
    /// <summary>cc-P782: the gravedigger's spade, a stock harvest tool on GraveDigging. New at version 0 (D40).</summary>
    [SerializationGenerator(0, false)]
    public partial class GravediggersSpade : BaseHarvestTool
    {
        public const int SpadeItemID = 0xF39; // a shovel
        public const int SpadeHue = 0x497;    // grave-earth grey

        [Constructible]
        public GravediggersSpade() : this(Server.Engines.GraveDigging.GraveDiggingTable.Current.Spade.Uses)
        {
        }

        [Constructible]
        public GravediggersSpade(int uses) : base(SpadeItemID, uses)
        {
            Weight = 5.0;
            Hue = SpadeHue;
        }

        public override string DefaultName => "a gravedigger's spade";

        public override HarvestSystem HarvestSystem => Server.Engines.GraveDigging.GraveDigging.System;
    }
}
