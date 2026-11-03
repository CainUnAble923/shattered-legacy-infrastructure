// GraveDiggingVerification.cs
//
// cc-P782: grave digging. Notes in shard-migration notes/cc-P782-grave-digging.md. Each fact was seen red first (the
// notes' red table).
//
// Facts:
//   1. The default table validates, writes as JSON and reads back the same; a file with an unknown setting, a find that
//      is not an item, a bonus that would make magic gear or a weapon is refused whole and the table in force is kept.
//   2. The region gate: a grave in a default cemetery area (Trammel and Felucca) and one in a region named "...
//      Graveyard" give a find; the same grave tile outside any cemetery is refused and says why; a headstone or plain
//      ground is not a grave.
//   3. Node depletion: one grave gives exactly its total, to whoever digs it (a second digger finds it dug out), then
//      "given up all it will"; the next grave still gives.
//   4. The tiers: under Forensics 50 only bones; from 50 bones and fertile dirt by weight; from 75 the necromancy
//      reagents too; a find's amount stays within its min and max.
//   5. The bonus odds: none under the bonus minSkill; at the table's chance exactly; the entries open by Forensics grade
//      (map from 75, coffin from 90) and are drawn by weight; a hit gives the item through the loot boxes' factory,
//      unblessed, and a miss gives none.
//   6. The rules: no bonus entry or tier find is gear, a scroll of power or a bless deed; the treasure map is the lowest
//      (classic level 1, the revamp's Stash).
//   7. Tinkers sell the gravedigger's spade at the table's price.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.GraveDigging;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GraveDiggingVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];
    private readonly List<Region> _regions = [];
    private readonly Func<double> _roll = GraveDigging.Roll;
    private readonly Func<double> _bonus = GraveDigging.BonusChanceRoll;

    private const int Grave = 0xEE1;

    // Spots inside the default Jhelom cemetery area (1278..1301, 3721..3753), one per fact so their nodes do not meet.
    private static Point3D Jhelom(int dx, int dy) => new(1280 + dx, 3723 + dy, 0);

    // Far from any default area and from other facts' regions.
    private static readonly Point3D Wilds = new(3410, 410, 0);

    public GraveDiggingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        GraveDiggingTable.Current = GraveDiggingTable.Defaults;
        GraveDigging.Roll = () => 0.0;
        GraveDigging.BonusChanceRoll = () => 99.0; // no bonus unless a fact asks
    }

    public void Dispose()
    {
        GraveDigging.Roll = _roll;
        GraveDigging.BonusChanceRoll = _bonus;
        GraveDiggingTable.Current = GraveDiggingTable.Defaults;
        foreach (var r in _regions)
            r.Unregister();
        foreach (var e in _made)
            e?.Delete();
        foreach (var a in _accounts)
            Accounts.Remove(a);
    }

    // ---------------------------------------------------------------- helpers

    private PlayerMobile Digger(Point3D near, Map map = null, double forensics = 100.0)
    {
        var account = new Account($"p782{Guid.NewGuid():N}"[..16], "p782-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Digger" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.Skills[SkillName.Forensics].Base = forensics;
        pm.MoveToWorld(new Point3D(near.X - 1, near.Y, near.Z), map ?? Map.Trammel);
        _made.Add(pm);
        return pm;
    }

    private GravediggersSpade Spade(PlayerMobile pm)
    {
        var spade = new GravediggersSpade();
        pm.Backpack.DropItem(spade);
        _made.Add(spade);
        return spade;
    }

    /// <summary>One whole dig at a grave, through pinned's harvest flow: the tile, the gates, the node, the check, the find.</summary>
    private static void Dig(PlayerMobile pm, GravediggersSpade spade, GraveSpot spot)
    {
        var system = GraveDigging.System;
        var def = system.GetDefinition(spot.TileID, false);
        if (def == null)
        {
            system.OnBadHarvestTarget(pm, spade, spot);
            return;
        }

        if (!system.CheckHarvest(pm, spade, def, spot) || !system.CheckRange(pm, spade, def, spot.Map, spot.Location, false) ||
            !system.CheckResources(pm, spade, def, spot.Map, spot.Location, false))
            return;

        system.FinishHarvesting(pm, spade, def, spot, spade);
    }

    private static GraveSpot At(Point3D p, Map map = null, int tile = Grave) => new(map ?? Map.Trammel, p, tile);

    private static int Finds(PlayerMobile pm) =>
        pm.Backpack.Items.Where(i => i is not GravediggersSpade).Sum(i => i.Amount);

    private static GraveDiggingTable Copy() =>
        GraveDiggingTable.Parse(GraveDiggingTable.ToJson(GraveDiggingTable.Defaults), "copy", new List<string>());

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheDefaultTableValidatesRoundTripsAndABadFileIsRefusedWhole()
    {
        var errors = new List<string>();
        GraveDiggingTable.Validate(GraveDiggingTable.Defaults, errors);
        Assert.Empty(errors);
        Assert.Equal(24, GraveDiggingTable.Defaults.Areas.Count);

        var json = GraveDiggingTable.ToJson(GraveDiggingTable.Defaults);
        var copy = GraveDiggingTable.Parse(json, "round trip", errors);
        Assert.Empty(errors);
        Assert.Equal(json, GraveDiggingTable.ToJson(copy));
        Assert.Equal(GraveDiggingTable.Defaults.GraveRanges, copy.GraveRanges);

        Assert.Null(GraveDiggingTable.Parse(json.Replace("\"minSkill\"", "\"minSkil\": 1, \"minSkill\""), "unknown", errors));
        Assert.NotEmpty(errors);
        errors.Clear();
        Assert.Null(GraveDiggingTable.Parse(json.Replace("\"NoxCrystal\"", "\"NoxCrystle\""), "find", errors));
        Assert.Contains(errors, e => e.Contains("NoxCrystle"));
        errors.Clear();
        Assert.Null(GraveDiggingTable.Parse(json.Replace("\"@TreasureMap\"", "\"@MagicGear\""), "magic", errors));
        Assert.Contains(errors, e => e.Contains("@MagicGear"));
        errors.Clear();
        Assert.Null(GraveDiggingTable.Parse(json.Replace("\"BonePile\"", "\"Longsword\""), "gear", errors));
        Assert.Contains(errors, e => e.Contains("Longsword"));
        _out.WriteLine(string.Join("\n", errors));

        Assert.Same(GraveDiggingTable.Defaults, GraveDiggingTable.Current);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void OnlyGravesInACemeteryCanBeDug()
    {
        var def = GraveDigging.System.Graves;
        Assert.True(def.Validate(0xED3, false));
        Assert.True(def.Validate(0xEE0, false));
        Assert.True(def.Validate(0xEE2, false));
        Assert.True(def.Validate(0xEE8, false));
        Assert.False(def.Validate(0x1165, false), "a headstone is not dug");
        Assert.False(def.Validate(0xEE1, true), "nor plain ground");

        // A default area, on Trammel and on Felucca.
        var tram = Digger(Jhelom(0, 0));
        Dig(tram, Spade(tram), At(Jhelom(0, 0)));
        Assert.True(Finds(tram) > 0, "the Jhelom cemetery, Trammel");

        var fel = Digger(Jhelom(0, 0), Map.Felucca);
        Dig(fel, Spade(fel), At(Jhelom(0, 0), Map.Felucca));
        Assert.True(Finds(fel) > 0, "the Jhelom cemetery, Felucca");

        // Outside every cemetery: refused.
        Assert.False(GraveDigging.InCemetery(Map.Trammel, Wilds));
        var wild = Digger(Wilds);
        Dig(wild, Spade(wild), At(Wilds));
        Assert.Equal(0, Finds(wild));

        // A region named "... Graveyard" (as Britain's and Vesper's in regions-trammel.json).
        var yard = new BaseRegion("p782 Test Graveyard", Map.Trammel, 1,
            new Rectangle3D(new Point3D(Wilds.X - 5, Wilds.Y - 5, -128), new Point3D(Wilds.X + 5, Wilds.Y + 5, 127)));
        yard.Register();
        _regions.Add(yard);
        _out.WriteLine($"region at {Wilds}: {Region.Find(Wilds, Map.Trammel)?.Name}");
        Assert.True(GraveDigging.InCemetery(Map.Trammel, Wilds));
        Dig(wild, wild.Backpack.Items.OfType<GravediggersSpade>().First(), At(Wilds));
        Assert.True(Finds(wild) > 0, "inside the graveyard region");

        var dry = Digger(Jhelom(4, 0));
        Dig(dry, Spade(dry), At(Jhelom(4, 0), tile: 0x1165));
        Assert.Equal(0, Finds(dry));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AGraveGivesItsTotalToWhoeverDigsItThenIsDugOut()
    {
        var grave = Jhelom(8, 10);
        var first = Digger(grave);
        var second = Digger(new Point3D(grave.X + 2, grave.Y, 0));
        var s1 = Spade(first);
        var s2 = Spade(second);
        var node = GraveDigging.System.Graves.GetBank(Map.Trammel, grave.X, grave.Y);
        var total = node.Current;
        _out.WriteLine($"the grave holds {total}");
        Assert.InRange(total, GraveDiggingTable.Current.Node.MinTotal, GraveDiggingTable.Current.Node.MaxTotal);

        for (var i = 0; i < total; i++)
            Dig(i % 2 == 0 ? first : second, i % 2 == 0 ? s1 : s2, At(grave));

        Assert.Equal(0, node.Current);
        Assert.Equal(2 * GraveDiggingTable.Current.Spade.Uses - total, s1.UsesRemaining + s2.UsesRemaining); // every dig found something
        Assert.False(GraveDigging.System.CheckResources(second, s2, GraveDigging.System.Graves, Map.Trammel, grave, false));

        var before = Finds(first) + Finds(second);
        Dig(first, s1, At(grave));
        Assert.Equal(before, Finds(first) + Finds(second));

        // The next grave tile is its own node.
        var next = new Point3D(grave.X + 1, grave.Y, 0);
        Dig(first, s1, At(next));
        Assert.True(Finds(first) + Finds(second) > before);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void WhatADigFindsRisesWithForensics()
    {
        Assert.Equal(["Bone"], new[] { 0.0, 0.5, 0.99 }.Select(r => GraveDigging.PickFind(0.0, r).Item).Distinct());
        Assert.Equal(["Bone"], new[] { 0.0, 0.5, 0.99 }.Select(r => GraveDigging.PickFind(49.9, r).Item).Distinct());

        // From 50: bones 6, fertile dirt 4.
        Assert.Equal("Bone", GraveDigging.PickFind(50.0, 0.59).Item);
        Assert.Equal("FertileDirt", GraveDigging.PickFind(50.0, 0.6).Item);
        Assert.Equal("FertileDirt", GraveDigging.PickFind(74.9, 0.99).Item);

        // From 75: the reagents (weights 4, 3, 3, 2, 2, 2, 2 of 18).
        var at75 = Enumerable.Range(0, 18).Select(i => GraveDigging.PickFind(75.0, (i + 0.5) / 18).Item).ToList();
        Assert.Equal(4, at75.Count(x => x == "Bone"));
        Assert.Equal(3, at75.Count(x => x == "GraveDust"));
        Assert.Equal(2, at75.Count(x => x == "PigIron"));
        Assert.Contains("NoxCrystal", at75);
        Assert.Contains("BatWing", at75);
        Assert.Contains("DaemonBlood", at75);

        // A real dig at 75 with the roll on grave dust gives 2 to 5 of it.
        GraveDigging.Roll = () => 8.0 / 18;
        var pm = Digger(Jhelom(14, 20), forensics: 100.0);
        Assert.Equal("GraveDust", GraveDigging.PickFind(100.0, 8.0 / 18).Item);
        Dig(pm, Spade(pm), At(Jhelom(14, 20)));
        var dust = Assert.Single(pm.Backpack.Items.OfType<GraveDust>());
        Assert.InRange(dust.Amount, 2, 5);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheBonusRollKeepsItsOddsAndItsGrades()
    {
        var b = GraveDiggingTable.Current.Bonus;
        Assert.False(GraveDigging.BonusHits(49.9, 0.0), "under the bonus minSkill");
        Assert.True(GraveDigging.BonusHits(50.0, b.ChancePercent - 0.01));
        Assert.False(GraveDigging.BonusHits(50.0, b.ChancePercent));
        Assert.Equal(4.0, b.ChancePercent);

        // Over a uniform spread of rolls, exactly the table's share hits.
        var hits = Enumerable.Range(0, 10000).Count(i => GraveDigging.BonusHits(100.0, i / 100.0));
        Assert.Equal(400, hits);

        // Grades: 50 (grade 2) remains and decor; 75 (3) adds the map; 90 (4) adds the coffin.
        string[] Names(double f) => GraveDigging.BonusFor(GraveDiggingTable.Current.GradeFor(f)).Select(e => e.Item).Distinct().ToArray();
        Assert.Equal(["RibCage", "BonePile"], Names(0.0));
        Assert.Equal(["RibCage", "BonePile", "@Decor"], Names(50.0));
        Assert.Equal(["RibCage", "BonePile", "@Decor", "@TreasureMap"], Names(75.0));
        Assert.Equal(["RibCage", "BonePile", "@Decor", "@TreasureMap", "WoodenCoffinDeed"], Names(90.0));

        // By weight at 90 (30, 30, 12, 8, 10, 4 of 94): a spread of rolls lands on each entry its weight's share.
        var spread = Enumerable.Range(0, 94).Select(i => GraveDigging.PickBonus(90.0, (i + 0.5) / 94)).ToList();
        Assert.Equal(30, spread.Count(e => e.Item == "RibCage"));
        Assert.Equal(20, spread.Count(e => e.Item == "@Decor"));
        Assert.Equal(10, spread.Count(e => e.Item == "@TreasureMap"));
        Assert.Equal(4, spread.Count(e => e.Item == "WoodenCoffinDeed"));

        // A hit through a real dig gives the item; a miss gives none.
        var grave = Jhelom(18, 26);
        var pm = Digger(grave, forensics: 100.0);
        var spade = Spade(pm);
        GraveDigging.BonusChanceRoll = () => 0.0;
        GraveDigging.Roll = () => 0.999; // the last entry: the coffin deed (and the last find of the tier)
        Dig(pm, spade, At(grave));
        var deed = Assert.Single(pm.Backpack.Items.OfType<WoodenCoffinDeed>());
        Assert.Equal(LootType.Regular, deed.LootType);

        GraveDigging.BonusChanceRoll = () => 99.0;
        Dig(pm, spade, At(grave));
        Assert.Single(pm.Backpack.Items.OfType<WoodenCoffinDeed>());
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void NothingFromAGraveIsGearPowerOrBlessed()
    {
        var pm = Digger(Jhelom(2, 30));
        foreach (var e in GraveDiggingTable.Current.Bonus.Entries)
        {
            Assert.Null(GraveDiggingTable.WhyNotBonus(e));
            for (var i = 0; i < 5; i++)
            {
                var item = GraveDigging.MakeBonus(pm, e);
                Assert.NotNull(item);
                _made.Add(item);
                Assert.False(item is BaseWeapon or BaseArmor or BaseJewel or BaseClothing or BaseTalisman or PowerScroll, $"{e.Item} made {item.GetType().Name}");
                Assert.NotEqual(LootType.Blessed, item.LootType);
                if (item is TreasureMap map) // classic level 1: the revamp's lowest map, a Stash (TreasureMapInfo.ConvertLevel)
                    Assert.Equal((int)TreasureLevel.Stash, map.Level);
            }
        }

        foreach (var f in GraveDiggingTable.Current.Tiers.SelectMany(t => t.Finds))
            Assert.False(typeof(BaseWeapon).IsAssignableFrom(f.Type) || typeof(BaseArmor).IsAssignableFrom(f.Type), f.Item);

        // Every special a box can make that would make gear is refused here.
        foreach (var special in new[] { "@MagicGear", "@SlayerWeapon", "@RunicTool", "@BoxInBox", "@RarePick", "@Mount", "@LeagueTitle" })
            Assert.NotNull(GraveDiggingTable.WhyNotBonus(new PoolEntry { Item = special, Weight = 1 }));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TinkersSellTheSpadeAtTheTablesPrice()
    {
        var row = Assert.Single(new SBTinker().BuyInfo, b => b.Type == typeof(GravediggersSpade));
        Assert.Equal(GraveDiggingTable.Current.Spade.Price, row.Price);
        var spade = new GravediggersSpade();
        _made.Add(spade);
        Assert.Equal(GraveDiggingTable.Current.Spade.Uses, spade.UsesRemaining);
        Assert.Same(GraveDigging.System, spade.HarvestSystem);
    }
}
