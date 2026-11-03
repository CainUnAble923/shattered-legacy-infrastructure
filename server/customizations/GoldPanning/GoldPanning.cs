// cc-P781: gold panning, a Mining harvest by water. The idea is from manwitch's Gold Panning (by Hammerhand and Milva,
// servuo.dev, assessed in cc-P778); the code, the water rule, the numbers and the words are ours. Notes in
// shard-migration notes/cc-P781-gold-panning.md.
//
//   The tool      ProspectorsPan (a stock harvest tool): double-click, target water within maxRange.
//   The water     a land water tile or a water static (gold-panning.json), and shallow river or lake water only: the
//                 water must have dry land within enclosureRange tiles in at least enclosureDirections of the 8
//                 directions (IsSheltered). Measured on the shard's Trammel map (the SoS map) in the notes: its sea is
//                 land water, its coastal shallows are water statics over a sunken bed, so neither the tile kind nor
//                 the land under it tells a lake from the coast; enclosure does.
//   Gates         Mining base minSkill (60) to pan at all; not mounted unless allowMounted; the pan in the backpack.
//   The roll      stock HarvestSystem.FinishHarvesting: the bank must hold a find; the Mining check (successMinSkill to
//                 successMaxSkill) passes or only silt is found; then the size is drawn by weight from the sizes the
//                 panner's Mining base opens (PickSize). One find takes one from the bank. Then the ML gem bonus roll.
//   Banks         pinned's HarvestBank per bankWidth x bankHeight tiles, minTotal to maxTotal finds, refilling
//                 minRespawnMinutes to maxRespawnMinutes after it is first drawn on (HarvestBank.Consume).
//   Rewards       nuggets and gems only: crafting and trade goods. Nothing grants skill or stat points (Mining gains
//                 by use, as any harvest), power scrolls, bless deeds or artifacts.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.GoldPanning;

/// <summary>A place to pan, given directly (staff tools and the facts): the map, the spot and the tile there.</summary>
public sealed record PanSpot(Map Map, Point3D Location, int TileID, bool IsLand);

public class GoldPanning : HarvestSystem
{
    private static GoldPanning _system;

    public static GoldPanning System => _system ??= new GoldPanning();

    /// <summary>Whether a tile is water (a land water tile or a water static there); a fact swaps it for a drawn lake.</summary>
    public static Func<Map, int, int, bool> WaterAt { get; set; } = MapWaterAt;

    public HarvestDefinition Water { get; }

    private static readonly (int Dx, int Dy)[] Compass = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private GoldPanning()
    {
        Water = new HarvestDefinition
        {
            RangedTiles = true,
            Skill = SkillName.Mining,
            ConsumedPerHarvest = 1,
            ConsumedPerFeluccaHarvest = 1,
            PlaceAtFeetIfFull = false,
            EffectActions = [32],     // bending to the water
            EffectSounds = [0x364],   // a splash
            EffectCounts = [3],
            EffectDelay = TimeSpan.FromSeconds(1.6),
            EffectSoundDelay = TimeSpan.FromSeconds(0.9),
            NoResourcesMessage = "The silt here has been worked clean. Try further along the water.",
            DoubleHarvestMessage = "Someone has worked this silt before you. Try further along the water.",
            TimedOutOfRangeMessage = "You have moved away from the water.",
            OutOfRangeMessage = "That water is too far away to pan.",
            FailMessage = "You swirl the pan and tip out nothing but silt and pebbles.",
            PackFullMessage = "Your pack is full; the nugget slips back into the water.",
            ToolBrokeMessage = "The bottom of your pan has worn through.",
            RaceBonus = false,
        };

        Definitions = [Water];
        Apply(GoldPanningTable.Current);
        GoldPanningTable.Changed += () => Apply(GoldPanningTable.Current);
    }

    /// <summary>Puts the table's numbers into the definition. Changed bank sizes start every bank afresh.</summary>
    public void Apply(GoldPanningTable t)
    {
        var bank = t.Bank;
        if (Water.BankWidth != bank.BankWidth || Water.BankHeight != bank.BankHeight || Water.MinTotal != bank.MinTotal ||
            Water.MaxTotal != bank.MaxTotal)
        {
            Water.Banks.Clear();
        }

        Water.BankWidth = bank.BankWidth;
        Water.BankHeight = bank.BankHeight;
        Water.MinTotal = bank.MinTotal;
        Water.MaxTotal = bank.MaxTotal;
        Water.MinRespawn = TimeSpan.FromMinutes(bank.MinRespawnMinutes);
        Water.MaxRespawn = TimeSpan.FromMinutes(bank.MaxRespawnMinutes);
        Water.MaxRange = t.MaxRange;
        Water.LandTiles = t.LandRanges;
        Water.StaticTiles = t.StaticRanges;

        // One resource: the Mining check of a pan. The size is drawn in GetResourceType.
        var silt = new HarvestResource(t.MinSkill, t.SuccessMinSkill, t.SuccessMaxSkill, "", typeof(SmallGoldNugget));
        Water.Resources = [silt];
        Water.Veins = [new HarvestVein(1000, 0.0, silt, null)];

        var bonus = new List<BonusHarvestResource>();
        var chance = t.Enabled ? t.Gems.ChancePercent : 0;
        bonus.Add(new BonusHarvestResource(0, 100.0 - chance, null, null)); // nothing
        if (chance > 0 && t.GemTypes.Count > 0)
        {
            foreach (var type in t.GemTypes)
                bonus.Add(new BonusHarvestResource(t.Gems.ReqSkill, chance / t.GemTypes.Count,
                    $"Something glints in the silt: {GemName(type)}!", type));
        }

        Water.BonusResources = bonus.ToArray();
    }

    private static string GemName(Type type)
    {
        var words = string.Concat(type.Name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
        return $"a {words}";
    }

    // -- Gates ------------------------------------------------------------------------------------------

    /// <summary>Why this character may not pan now, or null.</summary>
    public static string WhyNot(Mobile from, Item tool)
    {
        var t = GoldPanningTable.Current;
        if (!t.Enabled) return "Gold panning is closed for now.";
        if (from.Skills[SkillName.Mining].Base < t.MinSkill) return $"You need at least {t.MinSkill:0.#} Mining to read the silt for gold.";
        if (!t.AllowMounted && from.Mounted) return "You cannot pan from the saddle.";
        if (tool == null || tool.Deleted || from.Backpack == null || !tool.IsChildOf(from.Backpack)) return "The pan must be in your backpack.";
        return null;
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

        if (!IsSheltered(map, loc.X, loc.Y))
        {
            from.SendMessage("That is open water. Gold settles where rivers run and lakes lie still, not by the sea.");
            return false;
        }

        return true;
    }

    public override void OnBadHarvestTarget(Mobile from, Item tool, object toHarvest) =>
        from.SendMessage("Dip the pan in a river or lake. Nothing settles there.");

    public override bool GetHarvestDetails(Mobile from, Item tool, object toHarvest, out int tileID, out Map map, out Point3D loc, out bool isLand)
    {
        if (toHarvest is PanSpot spot)
        {
            tileID = spot.TileID;
            map = spot.Map;
            loc = spot.Location;
            isLand = spot.IsLand;
            return map != null && map != Map.Internal;
        }

        return base.GetHarvestDetails(from, tool, toHarvest, out tileID, out map, out loc, out isLand);
    }

    // -- The water --------------------------------------------------------------------------------------

    /// <summary>The map's own tiles: a land water tile, or a water static, at x, y.</summary>
    public static bool MapWaterAt(Map map, int x, int y)
    {
        if (map == null || map == Map.Internal || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            return false;

        var t = GoldPanningTable.Current;
        if (GoldPanningTable.InRanges(t.LandRanges, map.Tiles.GetLandTile(x, y).ID))
            return true;

        foreach (var st in map.Tiles.GetStaticTiles(x, y))
            if (GoldPanningTable.InRanges(t.StaticRanges, st.ID))
                return true;

        return false;
    }

    /// <summary>
    /// Shallow river or lake water: water at x, y with dry land within enclosureRange tiles in at least
    /// enclosureDirections of the 8 compass directions. A coast fails: on its far side lies open sea.
    /// </summary>
    public static bool IsSheltered(Map map, int x, int y)
    {
        var t = GoldPanningTable.Current;
        if (!WaterAt(map, x, y))
            return false;

        var shores = 0;
        foreach (var (dx, dy) in Compass)
        {
            for (var r = 1; r <= t.EnclosureRange; r++)
            {
                if (!WaterAt(map, x + dx * r, y + dy * r))
                {
                    shores++;
                    break;
                }
            }
        }

        return shores >= t.EnclosureDirections;
    }

    // -- The find ---------------------------------------------------------------------------------------

    /// <summary>The sizes this Mining base opens, in the table's order.</summary>
    public static List<NuggetSizeRow> SizesFor(double miningBase) =>
        GoldPanningTable.Current.Sizes.Where(s => miningBase >= s.ReqSkill && s.Weight > 0).ToList();

    /// <summary>The nugget size for a Mining base and a roll from 0 (inclusive) to 1, by weight among the open sizes.</summary>
    public static NuggetSizeRow PickSize(double miningBase, double roll)
    {
        var open = SizesFor(miningBase);
        if (open.Count == 0)
            return null;

        var total = open.Sum(s => s.Weight);
        var pick = roll * total;
        foreach (var s in open)
        {
            if (pick < s.Weight)
                return s;
            pick -= s.Weight;
        }

        return open[^1];
    }

    public static Type TypeFor(string size) => size?.ToLowerInvariant() switch
    {
        "large"  => typeof(LargeGoldNugget),
        "medium" => typeof(MediumGoldNugget),
        _        => typeof(SmallGoldNugget),
    };

    /// <summary>The roll the size draw uses; a fact swaps it.</summary>
    public static Func<double> SizeRoll { get; set; } = Utility.RandomDouble;

    public override Type GetResourceType(Mobile from, Item tool, HarvestDefinition def, Map map, Point3D loc, HarvestResource resource) =>
        PickSize(from.Skills[SkillName.Mining].Base, SizeRoll()) is { } size ? TypeFor(size.Size) : null;

    public override void SendSuccessTo(Mobile from, Item item, HarvestResource resource)
    {
        var what = item is GoldNugget n ? $"a {n.SizeKey} gold nugget" : "something";
        from.SendMessage(0x3B2, $"You swirl the pan, tip off the silt, and find {what}.");
    }

    public override void OnHarvestStarted(Mobile from, Item tool, HarvestDefinition def, object toHarvest)
    {
        base.OnHarvestStarted(from, tool, def, toHarvest);
        from.SendMessage("You crouch at the water's edge and start to pan.");
    }
}
