// GoldPanningVerification.cs
//
// cc-P781: gold panning. Notes in shard-migration notes/cc-P781-gold-panning.md. Each fact was seen red first (the
// notes' red table).
//
// Facts:
//   1. The default table validates, writes as JSON and reads back the same; a file with an unknown setting, a gem that
//      is not an item or a missing size is refused whole and the table in force is kept.
//   2. Tile: the pan takes water tiles only (static water, land water), not dry land or other statics; and only
//      sheltered water: the middle of a drawn lake and a river pass, a coast with open sea beyond does not; a pan at
//      the lake finds a nugget, one at the coast finds nothing and is told why.
//   3. Skill gates: under Mining 60 the pan refuses; at 60 only small nuggets, from 75 medium, from 90 large, drawn by
//      weight; mounted or with the pan out of the pack it refuses.
//   4. Bank depletion: one water bank yields exactly its total, then "worked clean"; a spot in the next bank still
//      yields.
//   5. Price: the assayer buys each size at the table's price (and sells the pan at its price); a Compact member's
//      nuggets dropped on him become standing (size standing x amount) and are used up; a non-member's are refused
//      and kept; the numbers come from the file.
//   6. Gems: at the table's chance a pan also finds one of its gems; at 0 never.
//   7. The assayer appears by himself beside every Miners' Compact Liaison, once: seeding again places nothing.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.GoldPanning;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GoldPanningVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];
    private readonly Func<Map, int, int, bool> _water = GoldPanning.WaterAt;
    private readonly Func<double> _roll = GoldPanning.SizeRoll;

    // A drawn world: water where Water holds the point, dry land elsewhere. Far from other facts' spots.
    private readonly HashSet<Point2D> _waterTiles = [];

    // A lake: 24 x 24 water at 2000..2023, 3000..3023 (every tile has the far shore within the 24-tile reach). A river: 3 wide, running north-south at x 2100..2102.
    // A coast: water from x 2200 east for 60 tiles (open sea), y 3000..3100.
    private static readonly Point3D LakeMiddle = new(2015, 3015, 0);
    private static readonly Point3D RiverMiddle = new(2101, 3050, 0);
    private static readonly Point3D Coast = new(2201, 3050, 0);

    public GoldPanningVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        GoldPanningTable.Current = GoldPanningTable.Defaults;

        for (var x = 2000; x < 2024; x++)
            for (var y = 3000; y < 3024; y++)
                _waterTiles.Add(new Point2D(x, y));
        for (var x = 2100; x <= 2102; x++)
            for (var y = 2950; y < 3150; y++)
                _waterTiles.Add(new Point2D(x, y));
        for (var x = 2200; x < 2260; x++)
            for (var y = 3000; y < 3100; y++)
                _waterTiles.Add(new Point2D(x, y));

        GoldPanning.WaterAt = (map, x, y) => map == Map.Trammel && _waterTiles.Contains(new Point2D(x, y));
        GoldPanning.SizeRoll = () => 0.0;
    }

    public void Dispose()
    {
        GoldPanning.WaterAt = _water;
        GoldPanning.SizeRoll = _roll;
        GoldPanningTable.Current = GoldPanningTable.Defaults;
        foreach (var e in _made)
            e?.Delete();
        foreach (var a in _accounts)
        {
            ClusterFAccountPersistence.Get(a)?.ClearGuildData();
            Accounts.Remove(a);
        }
    }

    // ---------------------------------------------------------------- helpers

    private PlayerMobile Panner(Point3D near, double mining = 110.0)
    {
        var account = new Account($"p781{Guid.NewGuid():N}"[..16], "p781-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Panner" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.Skills[SkillName.Mining].Base = mining;
        pm.MoveToWorld(new Point3D(near.X - 1, near.Y, near.Z), Map.Trammel);
        _made.Add(pm);
        return pm;
    }

    private ProspectorsPan Pan(PlayerMobile pm)
    {
        var pan = new ProspectorsPan();
        pm.Backpack.DropItem(pan);
        _made.Add(pan);
        return pan;
    }

    private static PanSpot Static(Point3D at, int id = 0x1797) => new(Map.Trammel, at, id, false);

    /// <summary>One whole pan at a spot, through pinned's harvest flow: the tile, the gates, the bank, the check, the find.</summary>
    private static void PanOnce(PlayerMobile pm, ProspectorsPan pan, PanSpot spot)
    {
        var system = GoldPanning.System;
        var def = system.GetDefinition(spot.TileID, spot.IsLand);
        if (def == null)
        {
            system.OnBadHarvestTarget(pm, pan, spot);
            return;
        }

        if (!system.CheckHarvest(pm, pan, def, spot) || !system.CheckRange(pm, pan, def, spot.Map, spot.Location, false) ||
            !system.CheckResources(pm, pan, def, spot.Map, spot.Location, false))
            return;

        system.FinishHarvesting(pm, pan, def, spot, pan);
    }

    private static int Count<T>(PlayerMobile pm) where T : Item => pm.Backpack.Items.OfType<T>().Sum(i => i.Amount);

    private static int Nuggets(PlayerMobile pm) => pm.Backpack.Items.OfType<GoldNugget>().Sum(i => i.Amount);

    private static GoldPanningTable Copy() =>
        GoldPanningTable.Parse(GoldPanningTable.ToJson(GoldPanningTable.Defaults), "copy", new List<string>());

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheDefaultTableValidatesRoundTripsAndABadFileIsRefusedWhole()
    {
        var errors = new List<string>();
        GoldPanningTable.Validate(GoldPanningTable.Defaults, errors);
        Assert.Empty(errors);
        Assert.Equal(8, GoldPanningTable.Defaults.GemTypes.Count);

        var json = GoldPanningTable.ToJson(GoldPanningTable.Defaults);
        var copy = GoldPanningTable.Parse(json, "round trip", errors);
        Assert.Empty(errors);
        Assert.Equal(json, GoldPanningTable.ToJson(copy));
        Assert.Equal(GoldPanningTable.Defaults.StaticRanges, copy.StaticRanges);

        Assert.Null(GoldPanningTable.Parse(json.Replace("\"minSkill\"", "\"minSkil\": 1, \"minSkill\""), "unknown", errors));
        Assert.NotEmpty(errors);
        errors.Clear();
        Assert.Null(GoldPanningTable.Parse(json.Replace("\"FireRuby\"", "\"FireRubble\""), "gem", errors));
        Assert.Contains(errors, e => e.Contains("FireRubble"));
        errors.Clear();
        Assert.Null(GoldPanningTable.Parse(json.Replace("\"large\"", "\"huge\""), "size", errors));
        Assert.Contains(errors, e => e.Contains("\"large\""));
        _out.WriteLine(string.Join("\n", errors));

        Assert.Same(GoldPanningTable.Defaults, GoldPanningTable.Current);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ThePanTakesShelteredRiverAndLakeWaterOnlyNeverTheCoast()
    {
        var def = GoldPanning.System.Water;
        Assert.True(def.Validate(0x1797, false), "static water");
        Assert.True(def.Validate(0x179C, false));
        Assert.True(def.Validate(0x3470, false), "animated water");
        Assert.True(def.Validate(0xA8, true), "land water");
        Assert.False(def.Validate(0x3, true), "grass");
        Assert.False(def.Validate(0x1797, true), "0x1797 is not a land water tile");
        Assert.False(def.Validate(0x1BF2, false), "an ingot pile");

        Assert.True(GoldPanning.IsSheltered(Map.Trammel, LakeMiddle.X, LakeMiddle.Y), "the middle of the lake");
        Assert.True(GoldPanning.IsSheltered(Map.Trammel, RiverMiddle.X, RiverMiddle.Y), "the river");
        Assert.False(GoldPanning.IsSheltered(Map.Trammel, Coast.X, Coast.Y), "the coast: open sea to the east");
        Assert.False(GoldPanning.IsSheltered(Map.Trammel, 2015, 2990), "dry land");
        Assert.False(GoldPanning.IsSheltered(Map.Felucca, LakeMiddle.X, LakeMiddle.Y), "no water on this facet here");

        var lakePm = Panner(LakeMiddle);
        PanOnce(lakePm, Pan(lakePm), Static(LakeMiddle));
        Assert.Equal(1, Nuggets(lakePm));

        var seaPm = Panner(Coast);
        PanOnce(seaPm, Pan(seaPm), Static(Coast));
        Assert.Equal(0, Nuggets(seaPm));

        var dryPm = Panner(new Point3D(2015, 2990, 0));
        PanOnce(dryPm, Pan(dryPm), new PanSpot(Map.Trammel, new Point3D(2015, 2990, 0), 0x3, true));
        Assert.Equal(0, Nuggets(dryPm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void MiningSixtyPansAndEachSizeOpensAtItsSkill()
    {
        var low = Panner(LakeMiddle, 59.9);
        var pan = Pan(low);
        Assert.Contains("60", GoldPanning.WhyNot(low, pan));
        PanOnce(low, pan, Static(LakeMiddle));
        Assert.Equal(0, Nuggets(low));

        Assert.Equal(["small"], GoldPanning.SizesFor(60.0).Select(s => s.Size));
        Assert.Equal(["small", "medium"], GoldPanning.SizesFor(75.0).Select(s => s.Size));
        Assert.Equal(["small", "medium"], GoldPanning.SizesFor(89.9).Select(s => s.Size));
        Assert.Equal(["small", "medium", "large"], GoldPanning.SizesFor(90.0).Select(s => s.Size));

        // By weight (80, 17, 3 of 100): the roll's top 3% is large at 90, small at 60.
        Assert.Equal("small", GoldPanning.PickSize(90.0, 0.0).Size);
        Assert.Equal("small", GoldPanning.PickSize(90.0, 0.799).Size);
        Assert.Equal("medium", GoldPanning.PickSize(90.0, 0.80).Size);
        Assert.Equal("large", GoldPanning.PickSize(90.0, 0.98).Size);
        Assert.Equal("small", GoldPanning.PickSize(60.0, 0.99).Size);
        Assert.Equal("medium", GoldPanning.PickSize(75.0, 0.99).Size);
        Assert.Null(GoldPanning.PickSize(59.9, 0.5));

        var at60 = Panner(new Point3D(LakeMiddle.X + 3, LakeMiddle.Y, 0), 60.0);
        Assert.Null(GoldPanning.WhyNot(at60, Pan(at60)));

        var pm = Panner(new Point3D(LakeMiddle.X - 6, LakeMiddle.Y, 0));
        var p = Pan(pm);
        GoldPanning.SizeRoll = () => 0.99;
        PanOnce(pm, p, Static(new Point3D(LakeMiddle.X - 6, LakeMiddle.Y, 0)));
        Assert.Equal(1, Count<LargeGoldNugget>(pm));

        var horse = new Horse();
        _made.Add(horse);
        horse.Rider = pm;
        Assert.Contains("saddle", GoldPanning.WhyNot(pm, p));
        horse.Rider = null;

        var loose = new ProspectorsPan();
        _made.Add(loose);
        loose.MoveToWorld(pm.Location, pm.Map);
        Assert.Contains("backpack", GoldPanning.WhyNot(pm, loose));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AWaterBankYieldsItsTotalThenIsWorkedClean()
    {
        // A spot in a bank no other fact uses: 8x8 banks, so 2000..2007, 3000..3007.
        var spot = new Point3D(2002, 3002, 0);
        var pm = Panner(spot);
        var pan = Pan(pm);
        var def = GoldPanning.System.Water;
        var bank = def.GetBank(Map.Trammel, spot.X, spot.Y);
        var total = bank.Current;
        _out.WriteLine($"bank holds {total}");
        Assert.InRange(total, GoldPanningTable.Current.Bank.MinTotal, GoldPanningTable.Current.Bank.MaxTotal);

        for (var i = 0; i < total; i++)
            PanOnce(pm, pan, Static(new Point3D(spot.X + i % 2, spot.Y + i / 2 % 2, 0)));

        Assert.Equal(total, Nuggets(pm));
        Assert.Equal(0, bank.Current);
        Assert.False(GoldPanning.System.CheckResources(pm, pan, def, Map.Trammel, spot, false));

        PanOnce(pm, pan, Static(spot));
        Assert.Equal(total, Nuggets(pm));

        // The next bank east (2008..2015, 3000..3007) still yields.
        var east = new Point3D(2009, 3002, 0);
        pm.MoveToWorld(new Point3D(east.X - 1, east.Y, 0), Map.Trammel);
        PanOnce(pm, pan, Static(east));
        Assert.Equal(total + 1, Nuggets(pm));
        Assert.Equal(GoldPanningTable.Current.Pan.Uses - total - 1, pan.UsesRemaining);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheAssayerPaysTheTablesPriceOrCompactStandingForAMember()
    {
        var t = GoldPanningTable.Current;
        var shop = new SBCompactAssayer();
        var small = new SmallGoldNugget(3);
        var medium = new MediumGoldNugget();
        var large = new LargeGoldNugget();
        _made.AddRange([small, medium, large]);
        Assert.Equal(t.Size("small").Gold, shop.SellInfo.GetSellPriceFor(small));
        Assert.Equal(t.Size("medium").Gold, shop.SellInfo.GetSellPriceFor(medium));
        Assert.Equal(t.Size("large").Gold, shop.SellInfo.GetSellPriceFor(large));
        Assert.True(shop.SellInfo.IsSellable(small));
        var panRow = Assert.Single(shop.BuyInfo);
        Assert.Equal(typeof(ProspectorsPan), panRow.Type);
        Assert.Equal(t.Pan.Price, panRow.Price);

        // The prices come from the file.
        var edited = Copy();
        edited.Size("small").Gold = 41;
        edited.Pan.Price = 333;
        GoldPanningTable.Current = edited;
        var shop2 = new SBCompactAssayer();
        Assert.Equal(41, shop2.SellInfo.GetSellPriceFor(small));
        Assert.Equal(333, shop2.BuyInfo[0].Price);
        GoldPanningTable.Current = GoldPanningTable.Defaults;

        // The ledger: a member's nuggets become standing; a stranger's are refused and kept.
        var member = Panner(LakeMiddle);
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(member);
        guild.JoinedGuilds.Add(CompactAssayer.GuildKey);
        var before = guild.GetReputation(CompactAssayer.GuildKey);
        var given = CompactAssayer.EnterInLedger(member, small, out var reply);
        _out.WriteLine(reply);
        Assert.Equal(3 * t.Size("small").Standing, given);
        Assert.Equal(before + given, guild.GetReputation(CompactAssayer.GuildKey));
        Assert.True(small.Deleted);

        var stranger = Panner(LakeMiddle);
        Assert.Equal(0, CompactAssayer.EnterInLedger(stranger, large, out var no));
        Assert.Contains("coin", no);
        Assert.False(large.Deleted);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void AtTheTablesChanceAPanAlsoFindsAnMlGem()
    {
        var sure = Copy();
        sure.Gems.ChancePercent = 100;
        GoldPanningTable.Current = sure;

        var pm = Panner(new Point3D(2020, 3020, 0));
        var pan = Pan(pm);
        PanOnce(pm, pan, Static(new Point3D(2020, 3020, 0)));
        var gems = pm.Backpack.Items.Where(i => sure.GemTypes.Contains(i.GetType())).ToList();
        Assert.Single(gems);
        _out.WriteLine($"gem: {gems[0].GetType().Name}");

        var never = Copy();
        never.Gems.ChancePercent = 0;
        GoldPanningTable.Current = never;
        var pm2 = Panner(new Point3D(2020, 3004, 0));
        var pan2 = Pan(pm2);
        for (var i = 0; i < 5; i++)
            PanOnce(pm2, pan2, Static(new Point3D(2020, 3004 + i % 2, 0)));
        Assert.DoesNotContain(pm2.Backpack.Items, i => never.GemTypes.Contains(i.GetType()));
        Assert.True(Nuggets(pm2) > 0);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheAssayerAppearsBesideEachLiaisonOnce()
    {
        var liaison = new MinersCompactLiaison();
        _made.Add(liaison);
        liaison.MoveToWorld(new Point3D(2400, 3200, 0), Map.Trammel);

        bool Near(CompactAssayer a) => !a.Deleted && a.Map == Map.Trammel && a.InRange(liaison.Location, GoldPanningTable.Current.Assayer.SeedRange);

        Assert.DoesNotContain(World.Mobiles.Values.OfType<CompactAssayer>(), Near);
        var first = ClusterFAssayerSeeder.Seed(verbose: true);
        _out.WriteLine(string.Join("\n", first));
        var placed = Assert.Single(World.Mobiles.Values.OfType<CompactAssayer>(), Near);
        _made.Add(placed);
        Assert.Equal(Map.Trammel, placed.Map);

        ClusterFAssayerSeeder.Seed(verbose: false);
        Assert.Single(World.Mobiles.Values.OfType<CompactAssayer>(), Near);
    }
}
