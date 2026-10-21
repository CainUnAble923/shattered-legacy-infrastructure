// LeagueHallVerification.cs
//
// cc-P243 (F-13): the League Hall, house decor in the League shop (LeagueShop/LeagueHall.cs). Notes in shard-migration
// notes/cc-P243-league-hall-decor-rewards.md.
//
// Facts:
//   1. A Hall row needs its AP and takes exactly its Renown, handing over one deed bound to the buyer's account; one AP
//      short or one Renown short takes nothing. Every row is decor, priced from its settings (a changed setting is the
//      new price), and drawn with one of the Hall's own art IDs.
//   2. Buying from the Hall leaves LifetimeRenown and League rank unchanged.
//   3. The deed places only in a house its buyer owns: in the buyer's house it places, both of a display case's parts on
//      the spot, and the deed is used up; in someone else's house or outside any house it is refused and kept; a
//      character of another account cannot use or trade it. A wall trophy with no wall beside it is refused (the test
//      host loads no tiledata, so TileData.MaxItemValue is 0 and no multi tile can be a wall: hanging one on a real wall
//      is a play test).
//   4. A display case's shelf and insert dye apart (each its own hue, an owner's dye tub), and nobody else may dye them.
//   5. Every part the Hall places has the server's tiledata record (LeagueHallArt, the same as records.json), and every
//      record is used; chopping a placed piece gives back a deed bound to the same account.
// The registry rows, PNGs and credits are checked by player-package\tests\Build-UoOverrides.Tests.ps1.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueHallVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueHallVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        ShatteredLegacyArt.Apply();

        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }
    }

    private static bool _achievements;

    // ---------------------------------------------------------------- helpers

    private static Account NewAccount() => new($"p243{Guid.NewGuid():N}"[..16], "p243-test-only");

    private static PlayerMobile NewCharacter(Account account, Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static ClusterFAccountData Data(Account account) => ClusterFAccountPersistence.GetOrCreate(account);

    private static LeagueShopEntry Row(string pieceKey) =>
        LeagueHall.FindEntry(LeagueHall.KeyPrefix + pieceKey) ?? throw new InvalidOperationException(pieceKey);

    private static LeagueHallDeed BoughtDeed(PlayerMobile pm, Account account, string pieceKey)
    {
        var data = Data(account);
        data.AchievementPoints = 2_000;
        data.Renown = 5_000;
        Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, Row(pieceKey)));
        return pm.Backpack.Items.OfType<LeagueHallDeed>().Last();
    }

    // A house with a floor, as LootBoxFanfareVerification's: the host has no multi data.
    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AHallRowNeedsItsApAndTakesExactlyItsRenown()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, new Point3D(3460, 2604, 18));
        var key = LeagueHall.Find("case.books")!.RenownSetting;
        var kept = ServerConfiguration.GetOrUpdateSetting(key, LeagueHall.Find("case.books")!.DefaultRenown);
        try
        {
            var rows = LeagueHall.Stock();
            Assert.Equal(LeagueHall.Pieces.Length, rows.Length);
            Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
            Assert.All(rows, r =>
            {
                Assert.Equal(LeagueShopKind.Decor, r.Kind);
                Assert.InRange(r.ItemID, LeagueHallArt.FirstId, LeagueHallArt.LastId);
                Assert.Null(r.TitleKey);
            });
            Assert.Equal(rows.OrderBy(r => r.ApRequired).ThenBy(r => r.Renown).Select(r => r.Key), rows.Select(r => r.Key));
            Assert.DoesNotContain(LeagueShop.Stock, e => e.Key.StartsWith(LeagueHall.KeyPrefix)); // never in a loot box's pool

            var books = Row("case.books");
            var data = Data(account);

            // One AP short: refused, nothing taken.
            data.AchievementPoints = books.ApRequired - 1;
            data.Renown = 10_000;
            Assert.Equal(LeagueShopResult.NeedsAp, LeagueShop.Purchase(pm, books));
            Assert.Equal(10_000, data.Renown);
            Assert.Empty(pm.Backpack.Items.OfType<LeagueHallDeed>());

            // One Renown short: refused, nothing taken.
            data.AchievementPoints = books.ApRequired;
            data.Renown = books.Renown - 1;
            Assert.Equal(LeagueShopResult.NotEnoughRenown, LeagueShop.Purchase(pm, books));
            Assert.Equal(books.Renown - 1, data.Renown);
            Assert.Empty(pm.Backpack.Items.OfType<LeagueHallDeed>());

            // Enough of both: exactly the price, one bound deed for the piece.
            data.Renown = 1_000;
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, books));
            Assert.Equal(1_000 - books.Renown, data.Renown);
            var deed = Assert.Single(pm.Backpack.Items.OfType<LeagueHallDeed>());
            Assert.Equal("case.books", deed.PieceKey);
            Assert.Equal(account.Username, deed.BoundAccount);

            // The price is the setting's.
            ServerConfiguration.SetSetting(key, 777);
            Assert.Equal(777, Row("case.books").Renown);
        }
        finally
        {
            ServerConfiguration.SetSetting(key, kept);
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BuyingFromTheHallLeavesLifetimeRenownAndLeagueRankUnchanged()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, new Point3D(3460, 2604, 18));
        try
        {
            var data = Data(account);
            data.SetFlag(ClusterFLeagueSystem.FlagJoined);
            data.GrantRenown(3_000, RenownSource.Achievement);
            data.AchievementPoints = 2_000;
            ClusterFLeagueRanks.SetRank(pm, 5);

            var lifetime = data.LifetimeRenown;
            var rank = ClusterFLeagueRanks.GetRank(pm);
            var check = ClusterFLeagueRanks.Evaluate(pm).Renown.Detail;

            var jeweled = Row("statue.jeweled");
            var skull = Row("trophy.skull");
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, jeweled));
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, skull));

            Assert.Equal(3_000 - jeweled.Renown - skull.Renown, data.Renown);
            Assert.Equal(lifetime, data.LifetimeRenown);
            Assert.Equal(rank, ClusterFLeagueRanks.GetRank(pm));
            Assert.Equal(check, ClusterFLeagueRanks.Evaluate(pm).Renown.Detail);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheDeedPlacesOnlyInAHouseItsBuyerOwns()
    {
        var mine = new Point3D(1700, 1700, 0);
        var theirs = new Point3D(1720, 1700, 0);
        var buyerAccount = NewAccount();
        var otherAccount = NewAccount();
        var buyer = NewCharacter(buyerAccount, mine);
        var other = NewCharacter(otherAccount, theirs);
        var myHouse = new TestHouse(buyer);
        myHouse.MoveToWorld(mine, Map.Trammel);
        var theirHouse = new TestHouse(other);
        theirHouse.MoveToWorld(theirs, Map.Trammel);
        try
        {
            var deed = BoughtDeed(buyer, buyerAccount, "case.gems");

            // Someone else's house, then no house at all: refused, the deed kept.
            buyer.MoveToWorld(theirs, Map.Trammel);
            Assert.Equal(AddonFitResult.NotInHouse, deed.TryPlace(buyer, new Point3D(theirs.X + 1, theirs.Y, 0), 0));
            Assert.Equal(AddonFitResult.NotInHouse, deed.TryPlace(buyer, new Point3D(1800, 1800, 0), 0));
            Assert.False(deed.Deleted);

            // A character of another account cannot use or trade it.
            other.Backpack.DropItem(deed);
            Assert.Equal(AddonFitResult.NotInHouse, deed.TryPlace(other, new Point3D(theirs.X + 1, theirs.Y, 0), 0));
            Assert.False(deed.AllowSecureTrade(buyer, other, other, true));
            Assert.False(deed.Deleted);
            buyer.Backpack.DropItem(deed);

            // The buyer's own house: placed, both parts on the spot, in the house's addons, the deed used up.
            buyer.MoveToWorld(mine, Map.Trammel);
            var spot = new Point3D(mine.X + 1, mine.Y + 1, 0);
            Assert.Equal(AddonFitResult.Valid, deed.TryPlace(buyer, spot, 1));
            Assert.True(deed.Deleted);
            var addon = Assert.Single(myHouse.Addons.OfType<LeagueHallAddon>());
            Assert.Equal(new[] { LeagueHallArt.ShelfEast, 0x3CEF }, addon.Components.Select(c => c.ItemID).ToArray());
            Assert.All(addon.Components, c => Assert.Equal(spot, c.Location));

            // A wall trophy needs its wall: in the open middle of the house both facings are refused, and the deed is kept.
            var trophy = BoughtDeed(buyer, buyerAccount, "trophy.minotaur");
            var open = new Point3D(mine.X - 1, mine.Y - 1, 0);
            Assert.Equal(AddonFitResult.NoWall, trophy.TryPlace(buyer, open, 0));
            Assert.Equal(AddonFitResult.NoWall, trophy.TryPlace(buyer, open, 1));
            Assert.False(trophy.Deleted);
            Assert.Single(myHouse.Addons.OfType<LeagueHallAddon>());
        }
        finally
        {
            myHouse.Delete();
            theirHouse.Delete();
            Cleanup(buyerAccount);
            Cleanup(otherAccount);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ADisplayCasesShelfAndInsertDyeApart()
    {
        var at = new Point3D(1740, 1700, 0);
        var buyerAccount = NewAccount();
        var otherAccount = NewAccount();
        var buyer = NewCharacter(buyerAccount, at);
        var other = NewCharacter(otherAccount, at);
        var house = new TestHouse(buyer);
        house.MoveToWorld(at, Map.Trammel);
        var tub = new DyeTub { DyedHue = 0x21 };
        try
        {
            var deed = BoughtDeed(buyer, buyerAccount, "case.books");
            Assert.Equal(AddonFitResult.Valid, deed.TryPlace(buyer, new Point3D(at.X + 1, at.Y + 1, 0), 0));
            var addon = Assert.Single(house.Addons.OfType<LeagueHallAddon>());
            Assert.False(addon.ShareHue);
            var shelf = (LeagueHallComponent)addon.Components[0];
            var insert = (LeagueHallComponent)addon.Components[1];
            Assert.True(shelf.Dyeable && insert.Dyeable);

            Assert.True(shelf.Dye(buyer, tub));
            Assert.Equal(0x21, shelf.Hue);
            Assert.Equal(0, insert.Hue);

            tub.DyedHue = 0x44;
            Assert.True(insert.Dye(buyer, tub));
            Assert.Equal(0x44, insert.Hue);
            Assert.Equal(0x21, shelf.Hue);

            // Not the owner: refused, nothing changes.
            tub.DyedHue = 0x99;
            Assert.False(shelf.Dye(other, tub));
            Assert.Equal(0x21, shelf.Hue);

            // Only a case's parts take a dye tub.
            var statue = BoughtDeed(buyer, buyerAccount, "statue.gargoyle");
            Assert.Equal(AddonFitResult.Valid, statue.TryPlace(buyer, new Point3D(at.X - 1, at.Y + 1, 0), 0));
            var stone = (LeagueHallComponent)house.Addons.OfType<LeagueHallAddon>().Single(a => a != addon).Components[0];
            Assert.False(stone.Dye(buyer, tub));
            Assert.Equal(0, stone.Hue);
        }
        finally
        {
            tub.Delete();
            house.Delete();
            Cleanup(buyerAccount);
            Cleanup(otherAccount);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void EveryPartHasItsTiledataAndAChoppedPieceGivesItsBoundDeedBack()
    {
        var parts = LeagueHall.Pieces.SelectMany(p => p.Facings).SelectMany(f => f.Parts).Select(p => p.ItemID).ToHashSet();
        var records = LeagueHallArt.Records.Select(r => r.Id).ToHashSet();
        Assert.Equal(records.OrderBy(i => i), parts.OrderBy(i => i));
        Assert.Equal(LeagueHallArt.LastId - LeagueHallArt.FirstId + 1, records.Count);
        foreach (var r in LeagueHallArt.Records)
        {
            Assert.Equal(r.Name, TileData.ItemTable[r.Id].Name);
            Assert.Equal(r.Flags, TileData.ItemTable[r.Id].Flags);
            Assert.True(r.Name.Length <= 19 && r.Name.All(c => c < 128), r.Name);
        }

        Assert.All(LeagueHall.Pieces.Where(p => p.Kind == LeagueHallKind.WallTrophy).SelectMany(p => p.Facings),
            f => Assert.NotEqual(Point3D.Zero, f.Wall));
        Assert.All(LeagueHall.Pieces.Where(p => p.Kind != LeagueHallKind.WallTrophy).SelectMany(p => p.Facings),
            f => Assert.Equal(Point3D.Zero, f.Wall));

        var at = new Point3D(1760, 1700, 0);
        var account = NewAccount();
        var pm = NewCharacter(account, at);
        var house = new TestHouse(pm);
        house.MoveToWorld(at, Map.Trammel);
        try
        {
            var deed = BoughtDeed(pm, account, "head.oni");
            Assert.Equal(AddonFitResult.Valid, deed.TryPlace(pm, new Point3D(at.X + 1, at.Y, 0), 1));
            var addon = Assert.Single(house.Addons.OfType<LeagueHallAddon>());

            addon.OnChop(pm);
            Assert.True(addon.Deleted);
            var back = Assert.Single(pm.Backpack.Items.OfType<LeagueHallDeed>());
            Assert.Equal("head.oni", back.PieceKey);
            Assert.Equal(account.Username, back.BoundAccount);
        }
        finally
        {
            house.Delete();
            Cleanup(account);
        }
    }
}
