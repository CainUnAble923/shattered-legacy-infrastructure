// ArtisanFestivalVerification.cs
//
// cc-P783: the Artisan Festival decor. Notes in shard-migration notes/cc-P783-artisan-festival-decor.md. Each fact was
// seen red first (the notes' red table).
//
// Facts:
//   1. The default table validates: 64 pieces, 16 each of lanterns, sculptures, pillows and tapestries, every key and
//      every item ID once; 12 stocked at 10 to 25 tokens; it writes as JSON and reads back the same; a file with a
//      duplicate key, a pillow with a lit ID or a stocked piece that does not exist is refused whole.
//   2. Every row makes its piece: a light for a lantern or sculpture, decor otherwise, named from the table, standing on
//      its first facing; a light lights to its lit ID and goes out to its unlit ID, and never burns out.
//   3. Every flip pair: each two-faced piece turns to its other facing and back when double-clicked in the pack; a lit
//      lantern turns lit; a sculpture faces one way; out of the pack nothing turns.
//   4. The spring stall sells the stocked pieces at the table's prices (each row makes its piece); no other season does.
//   5. The spring box slot holds every piece the stall does not sell, at boxWeight each, and each entry makes its
//      piece through the loot boxes' factory; other seasons' slots are as their tables say.
//   6. The rules: no piece is gear, blessed or a deed of power; everything is plain house decor.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.ArtisanFestival;
using Server.Engines.RewardStores;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ArtisanFestivalVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];

    public ArtisanFestivalVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ArtisanFestivalTable.Current = ArtisanFestivalTable.Defaults;
    }

    public void Dispose()
    {
        ArtisanFestivalTable.Current = ArtisanFestivalTable.Defaults;
        foreach (var e in _made)
            e?.Delete();
        foreach (var a in _accounts)
            Accounts.Remove(a);
    }

    private static ArtisanFestivalTable T => ArtisanFestivalTable.Current;

    private PlayerMobile Player()
    {
        var account = new Account($"p783{Guid.NewGuid():N}"[..16], "p783-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Decorator" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3420, 420, 0), Map.Trammel);
        _made.Add(pm);
        return pm;
    }

    private Item Make(string key)
    {
        var item = ArtisanFestival.Create(key);
        Assert.NotNull(item);
        _made.Add(item);
        return item;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTableHoldsTheSixtyFourPiecesAndABadFileIsRefusedWhole()
    {
        var errors = new List<string>();
        ArtisanFestivalTable.Validate(T, errors);
        Assert.Empty(errors);

        Assert.Equal(64, T.Pieces.Count);
        foreach (var kind in Enum.GetValues<ArtisanKind>())
            Assert.Equal(16, T.Pieces.Count(p => p.Kind == kind));
        Assert.Equal(64, T.Pieces.Select(p => p.Key).Distinct().Count());
        var ids = T.Pieces.SelectMany(p => p.Facings).SelectMany(f => new[] { f.Id, f.Lit }).Where(i => i != 0).ToList();
        Assert.Equal(160, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.InRange(id, 40421, 42153)); // the art checked in our client (art-check.txt)

        Assert.InRange(T.Stock.Count, 8, 12);
        Assert.All(T.Stock, s => Assert.InRange(s.Price, 10, 25));
        Assert.Equal(64 - T.Stock.Count, T.BoxPieces.Count());

        var json = ArtisanFestivalTable.ToJson(T);
        var copy = ArtisanFestivalTable.Parse(json, "round trip", errors);
        Assert.Empty(errors);
        Assert.Equal(json, ArtisanFestivalTable.ToJson(copy));

        Assert.Null(ArtisanFestivalTable.Parse(json.Replace("\"key\": \"destard-lantern\"", "\"key\": \"sacrifice-lantern\""), "dup", errors));
        Assert.Contains(errors, e => e.Contains("appears twice"));
        errors.Clear();
        var pillow = json.IndexOf("\"compassion-pillow\"", StringComparison.Ordinal);
        var lit = json.IndexOf("\"lit\": 0", pillow, StringComparison.Ordinal);
        Assert.Null(ArtisanFestivalTable.Parse(json[..lit] + "\"lit\": 40430" + json[(lit + 8)..], "lit", errors));
        Assert.Contains(errors, e => e.Contains("compassion-pillow"));
        errors.Clear();
        Assert.Null(ArtisanFestivalTable.Parse(json.Replace("\"key\": \"honor-lantern\",\n      \"price\"", "\"key\": \"honour-lantern\",\n      \"price\""), "stock", errors));
        Assert.Contains(errors, e => e.Contains("honour-lantern"));
        _out.WriteLine(string.Join("\n", errors));

        Assert.Same(ArtisanFestivalTable.Defaults, ArtisanFestivalTable.Current);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryRowMakesItsPieceAndEveryLightLightsAndGoesOut()
    {
        foreach (var p in T.Pieces)
        {
            var item = Make(p.Key);
            Assert.Equal(p.Facings[0].Id, item.ItemID);
            Assert.Equal(p.Name, item.Name ?? item.DefaultName);
            Assert.Equal(p.Key, ((IArtisanPiece)item).PieceKey);

            if (p.IsLight)
            {
                var light = Assert.IsType<ArtisanFestivalLight>(item);
                Assert.Equal(TimeSpan.Zero, light.Duration);
                Assert.False(light.Burning);
                light.Ignite();
                Assert.True(light.Burning, p.Key);
                Assert.Equal(p.Facings[0].Lit, light.ItemID);
                Assert.Equal(T.LightType, light.Light);
                light.Douse();
                Assert.Equal(p.Facings[0].Id, light.ItemID);
                Assert.False(light.BurntOut);
            }
            else
            {
                Assert.IsType<ArtisanFestivalDecor>(item);
            }
        }

        Assert.Null(ArtisanFestival.Create("no-such-piece"));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EveryFlipPairTurnsAndTurnsBack()
    {
        var pm = Player();
        foreach (var p in T.Pieces)
        {
            var item = Make(p.Key);
            pm.Backpack.DropItem(item);

            if (p.Facings.Count == 1)
            {
                item.OnDoubleClick(pm);
                Assert.Equal(p.Facings[0].Id, item.ItemID);
                Assert.Equal(0, ((IArtisanPiece)item).Facing);
                continue;
            }

            item.OnDoubleClick(pm);
            Assert.Equal(p.Facings[1].Id, item.ItemID);
            Assert.Equal(1, ((IArtisanPiece)item).Facing);
            item.OnDoubleClick(pm);
            Assert.Equal(p.Facings[0].Id, item.ItemID);

            if (item is ArtisanFestivalLight light)
            {
                light.Ignite();
                light.Turn(pm);
                Assert.Equal(p.Facings[1].Lit, light.ItemID); // a lit lantern turns lit
                light.Douse();
                Assert.Equal(p.Facings[1].Id, light.ItemID);
                light.Turn(pm);
            }
        }

        // Out of the pack it does not turn (a light lights instead).
        var pillow = Make("honor-pillow");
        pillow.MoveToWorld(pm.Location, pm.Map);
        pillow.OnDoubleClick(pm);
        Assert.Equal(T.Piece("honor-pillow").Facings[0].Id, pillow.ItemID);

        var lantern = (ArtisanFestivalLight)Make("honor-lantern");
        lantern.MoveToWorld(pm.Location, pm.Map);
        lantern.OnDoubleClick(pm);
        Assert.True(lantern.Burning);
        Assert.Equal(T.Piece("honor-lantern").Facings[0].Lit, lantern.ItemID);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheSpringStallSellsTheStockedPiecesAtTheTablesPrices()
    {
        var pm = Player();
        var spring = FestivalStock.For(FestivalSeasons.Spring);
        var rows = spring.Where(r => r.Key.StartsWith("festival.spring.artisan.", StringComparison.Ordinal)).ToList();
        Assert.Equal(T.Stock.Count, rows.Count);

        foreach (var s in T.Stock)
        {
            var row = Assert.Single(rows, r => r.Key == $"festival.spring.artisan.{s.Key}");
            Assert.Equal(s.Price, row.DefaultPrice);
            Assert.Equal(T.Piece(s.Key).Facings[0].Id, row.ItemID);
            var made = row.Create!(pm);
            var item = Assert.Single(made);
            _made.Add(item);
            Assert.Equal(s.Key, ((IArtisanPiece)item).PieceKey);
            _out.WriteLine($"{row.Label}: {s.Price} tokens");
        }

        foreach (var season in new[] { FestivalSeasons.Halloween, FestivalSeasons.Winter })
            Assert.DoesNotContain(FestivalStock.For(season), r => r.Key.Contains(".artisan.", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheSpringBoxSlotHoldsEveryOtherPieceAtItsWeight()
    {
        var pm = Player();
        var lt = LootTable.Current;
        var springDef = lt.Seasons["spring"];
        var items = LootBoxRoller.SeasonItems("spring", springDef);
        var artisan = items.Where(e => e.Item == "@ArtisanDecor").ToList();

        Assert.Equal(springDef.Items.Length + artisan.Count, items.Length);
        Assert.Equal(T.BoxPieces.Select(p => p.Key).OrderBy(k => k), artisan.Select(e => e.Name).OrderBy(k => k));
        Assert.DoesNotContain(artisan, e => T.InStock(e.Name));
        Assert.All(artisan, e => Assert.Equal(T.BoxWeight, e.Weight));

        foreach (var e in artisan)
        {
            Assert.Null(LootBoxRoller.CheckEntry(e));
            var item = LootBoxRoller.MakeRow(pm, e, 2);
            Assert.NotNull(item);
            _made.Add(item);
            Assert.Equal(e.Name, ((IArtisanPiece)item).PieceKey);
        }

        Assert.NotNull(LootBoxRoller.CheckEntry(new PoolEntry { Item = "@ArtisanDecor", Name = "no-such-piece" }));
        foreach (var season in new[] { "halloween", "winter" })
            Assert.Same(lt.Seasons[season].Items, LootBoxRoller.SeasonItems(season, lt.Seasons[season]));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void EveryPieceIsPlainHouseDecor()
    {
        foreach (var p in T.Pieces)
        {
            var item = Make(p.Key);
            Assert.False(item is BaseWeapon or BaseArmor or BaseJewel or BaseClothing or BaseTalisman or PowerScroll, p.Key);
            Assert.NotEqual(LootType.Blessed, item.LootType);
            Assert.True(item.Movable);
            Assert.False(item.Stackable);
        }
    }
}
