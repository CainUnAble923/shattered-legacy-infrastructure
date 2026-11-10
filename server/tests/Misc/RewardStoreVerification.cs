// RewardStoreVerification.cs
//
// cc-P760: the reward stores (guild and society stores, the Festival store, the Achievement store) and the League shop's
// new rows. Notes in shard-migration notes/cc-P760-reward-stores.md; the plan Chase approved is
// notes/cc-P760-stores-plan.md.
//
// For each currency (guild points, Clean Up points, Festival Tokens, spendable AP, Renown):
//   earning adds to it; a buy takes exactly the price and hands the items over; a buy it cannot afford is refused and
//   takes nothing; the balance cannot go negative; the lifetime total (standing and rank, lifetime Clean Up points, tokens
//   earned, the AP total, LifetimeRenown) never moves on a spend.
// And: every row of every store can be made, and nothing sold is gear, a power scroll or a magic item; row keys are
// unique and the price table round-trips; tokens drop only in season; the Festival stall is placed and removed with the
// season; the store window greys a locked row and the confirm step buys.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.LeagueRewards;
using Server.Engines.Points;
using Server.Engines.RewardStores;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RewardStoreVerification
{
    private readonly ITestOutputHelper _out;

    public RewardStoreVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }
    }

    private static bool _achievements;

    // ---------------------------------------------------------------- helpers

    private static Account NewAccount() => new($"p760{Guid.NewGuid():N}"[..16], "p760-test-only");

    private static PlayerMobile NewCharacter(Account account)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                CleanUpBritanniaData.Instance.RemoveEntry(pm);
                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static ClusterFAccountData Data(Account account) => ClusterFAccountPersistence.GetOrCreate(account);

    private static int Count<T>(PlayerMobile pm) where T : Item => pm.Backpack.Items.OfType<T>().Count();

    private static GuildStoreDef Guild(string key) => new(key, null) { RequireGuildmaster = false };

    private static FestivalStoreDef Festival() => new(null) { RequirePeddler = false };

    private static RewardStoreRow Row(RewardStoreDef store, string key) =>
        store.Find(key) ?? throw new InvalidOperationException(key);

    private static void Join(PlayerMobile pm, string guild, int standing, int points)
    {
        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        data.JoinedGuilds.Add(guild);
        data.GuildReputation[guild] = standing;
        data.GuildCurrency[guild] = points;
    }

    // ---------------------------------------------------------------- guild points

    [Fact]
    public void GuildPointsAreEarnedFromBodsAndRequisitions()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "tailoring", 0, 0);
            var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);

            // 1 per 50 bulk order points, at least 1; nothing for a deed worth none.
            Assert.Equal(0, GuildPointEarning.ForBodPoints(0));
            Assert.Equal(1, GuildPointEarning.ForBodPoints(10));
            Assert.Equal(4, GuildPointEarning.ForBodPoints(200));

            Assert.Equal(4, GuildPointEarning.OnBodTurnIn(pm, "tailoring", 200));
            Assert.Equal(4, data.GetCurrency("tailoring"));

            // Not a member of the deed's guild, or the Smiths' deed (they bank Seals their own way): nothing.
            Assert.Equal(0, GuildPointEarning.OnBodTurnIn(pm, "tinkers", 200));
            Join(pm, "smithing", 0, 0);
            Assert.Equal(0, GuildPointEarning.OnBodTurnIn(pm, "smithing", 200));
            Assert.Equal(0, data.GetCurrency("smithing"));

            // Requisitions: 1 per 10 standing, to a member; the Custodians keep no scrip.
            Assert.Equal(5, GuildPointEarning.OnRequisition(pm, "tailoring", 50));
            Assert.Equal(9, data.GetCurrency("tailoring"));
            Assert.Equal(0, GuildPointEarning.OnRequisition(pm, "arcane", 50));
            Join(pm, "custodians", 0, 0);
            Assert.Equal(0, GuildPointEarning.OnRequisition(pm, "custodians", 50));

            // The named currencies (the plan's eleven).
            Assert.Equal("Tailors' Thimbles", MuleUpgrades.CurrencyName("tailoring"));
            Assert.Equal("Alembic Drops", MuleUpgrades.CurrencyName("alembic"));
            Assert.Equal("Keepers' Candles", MuleUpgrades.CurrencyName("keepers"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AGuildStoreBuyTakesExactlyThePriceAndNeverStandingOrRank()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "tailoring", 6_200, 1_000); // Journeyman (rank index 2)
            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            var store = Guild("tailoring");
            var rankBefore = ClusterFGuildSystem.GuildRankIndex("tailoring", guild);
            Assert.Equal(ClusterFGuildSystem.RankJourneyman, rankBefore);

            var cloth = Row(store, "guild.tailoring.cloth");
            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, cloth));
            Assert.Equal(1_000 - cloth.Price, guild.GetCurrency("tailoring"));
            Assert.Equal(50, pm.Backpack.Items.OfType<Cloth>().Sum(c => c.Amount));

            var form = Row(store, "guild.tailoring.dressform");
            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, form));
            Assert.Equal(1_000 - cloth.Price - form.Price, guild.GetCurrency("tailoring"));
            Assert.Equal(1, Count<Dressform>(pm));

            // Standing and rank never move; the spent counter records what the store took.
            Assert.Equal(6_200, guild.GetReputation("tailoring"));
            Assert.Equal(rankBefore, ClusterFGuildSystem.GuildRankIndex("tailoring", guild));
            Assert.Equal(cloth.Price + form.Price,
                ClusterFAchievementSystem.GetCounter(account.Username, GuildPointsCurrency.SpentCounter(pm, "tailoring")));

            // A Master row (the runic sewing kit) is locked at Journeyman: refused, nothing taken.
            var runic = Row(store, "guild.tailoring.runic");
            var before = guild.GetCurrency("tailoring");
            Assert.Equal(RewardStoreResult.Locked, RewardStore.Purchase(pm, store, runic));
            Assert.Equal(before, guild.GetCurrency("tailoring"));
            Assert.Equal(0, Count<RunicSewingKit>(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AGuildBuyItCannotAffordIsRefusedAndTheBalanceCannotGoNegative()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var store = Guild("healers");
            var bandages = Row(store, "guild.healers.bandages");
            Join(pm, "healers", 0, bandages.Price - 1);
            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);

            Assert.Equal(RewardStoreResult.NotEnough, RewardStore.Purchase(pm, store, bandages));
            Assert.Equal(bandages.Price - 1, guild.GetCurrency("healers"));
            Assert.Equal(0, Count<Bandage>(pm));

            Assert.False(store.Currency.TrySpend(pm, bandages.Price));
            Assert.False(store.Currency.TrySpend(pm, -5));
            Assert.Equal(bandages.Price - 1, guild.GetCurrency("healers"));
            Assert.True(store.Currency.Balance(pm) >= 0);

            // Not a member: the store refuses to sell at all.
            var outsider = Guild("bards");
            Assert.False(outsider.CanShop(pm, out var why));
            Assert.Contains("members", why);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheCustodiansStoreSpendsCleanUpPointsAndNeverTheLifetimeTotal()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "custodians", 0, 0);
            CleanUpBritanniaData.Instance.AwardPoints(pm, 1_000, false, false);
            var lifetime = CleanUpBritanniaData.Instance.GetLifetimePoints(pm);
            Assert.Equal(1_000, lifetime);

            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            guild.GuildReputation["custodians"] = 1_000_000; // a rank high enough for every row
            var store = Guild("custodians");
            Assert.Equal("Clean Up points", store.Currency.Name);

            var barrel = Row(store, "guild.custodians.barrel");
            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, barrel));
            Assert.Equal(1_000 - barrel.Price, store.Currency.Balance(pm));
            Assert.Equal(lifetime, CleanUpBritanniaData.Instance.GetLifetimePoints(pm));
            Assert.Equal(1, Count<TrashBarrel>(pm));

            Assert.False(store.Currency.TrySpend(pm, 1_000_000));
            Assert.Equal(1_000 - barrel.Price, store.Currency.Balance(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AGuildTitleIsBoughtOnceAtTheTopRankAndCanBeShown()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "bards", 100_000, 5_000); // top rank on the generic ladder
            var store = Guild("bards");
            var title = Row(store, "guild.bards.title");
            Assert.True(title.IsTitle);

            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, title));
            Assert.Equal(5_000 - title.Price, store.Currency.Balance(pm));
            Assert.Contains(RewardStoreTitles.GuildTitleKey("bards"), Data(account).LeagueTitles.Owned);

            Assert.Equal(RewardStoreResult.AlreadyOwned, RewardStore.Purchase(pm, store, title));
            Assert.Equal(5_000 - title.Price, store.Currency.Balance(pm));

            Assert.True(LeagueTitles.Choose(pm, RewardStoreTitles.GuildTitleKey("bards")));
            Assert.Equal("the Songkeeper", pm.Title);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- Festival Tokens

    [Fact]
    public void FestivalTokensAreEarnedOnlyInSeasonAndSpendingNeverLowersTheEarnedCount()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var bc = new Mongbat { Fame = 5_000 };
        try
        {
            FestivalSeasons.Forced = "none";
            bc.RegisterDamage(10, pm);
            Assert.False(FestivalTokens.TryDrop(bc, 0.0));
            Assert.False(FestivalTokens.OnDailyLogin(pm, new DateTime(2026, 7, 1)));
            Assert.Equal(0, FestivalTokens.Instance.Balance(pm));

            // The drop chance: none under 1,000 fame, 10% + 1% per 1,000, at most 25%.
            Assert.Equal(0.0, FestivalTokens.DropChance(999));
            Assert.Equal(0.11, FestivalTokens.DropChance(1_000), 6);
            Assert.Equal(0.15, FestivalTokens.DropChance(5_000), 6);
            Assert.Equal(0.25, FestivalTokens.DropChance(40_000), 6);

            FestivalSeasons.Forced = FestivalSeasons.Halloween;
            Assert.True(FestivalTokens.TryDrop(bc, 0.0));          // the roll under the chance: the top damager gets 1
            Assert.False(FestivalTokens.TryDrop(bc, 0.99));        // over it: none
            bc.Fame = 500;
            Assert.False(FestivalTokens.TryDrop(bc, 0.0));         // under 1,000 fame: none
            Assert.Equal(1, FestivalTokens.Instance.Balance(pm));

            FestivalTokens.OnSorcerersArtifact(pm);                 // 2 per artifact
            Assert.Equal(3, FestivalTokens.Instance.Balance(pm));

            Assert.True(FestivalTokens.OnDailyLogin(pm, new DateTime(2026, 10, 30, 9, 0, 0)));
            Assert.False(FestivalTokens.OnDailyLogin(pm, new DateTime(2026, 10, 30, 21, 0, 0))); // once a day
            Assert.True(FestivalTokens.OnDailyLogin(pm, new DateTime(2026, 10, 31, 9, 0, 0)));
            Assert.Equal(5, FestivalTokens.Instance.Balance(pm));
            Assert.Equal(5, FestivalTokens.Earned(account.Username));

            var store = Festival();
            var treats = Row(store, "festival.halloween.treats");
            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, treats));
            Assert.Equal(5 - treats.Price, FestivalTokens.Instance.Balance(pm));
            Assert.Equal(5, FestivalTokens.Earned(account.Username)); // the earned count never moves
            Assert.Equal(treats.Price, FestivalTokens.Spent(account.Username));

            // Cannot afford: refused, nothing taken; cannot go negative.
            var guillotine = Row(store, "festival.halloween.guillotine");
            var balance = FestivalTokens.Instance.Balance(pm);
            Assert.Equal(RewardStoreResult.NotEnough, RewardStore.Purchase(pm, store, guillotine));
            Assert.Equal(balance, FestivalTokens.Instance.Balance(pm));
            Assert.False(FestivalTokens.Instance.TrySpend(pm, balance + 1));
            Assert.False(FestivalTokens.Instance.TrySpend(pm, -1));
            Assert.Equal(balance, FestivalTokens.Instance.Balance(pm));

            // Out of season the store sells nothing and a stale row buys nothing.
            FestivalSeasons.Forced = "none";
            Assert.Empty(store.Rows());
            Assert.Equal(RewardStoreResult.Unavailable, RewardStore.Purchase(pm, store, treats));
            Assert.Equal(balance, FestivalTokens.Instance.Balance(pm));
        }
        finally
        {
            FestivalSeasons.Forced = null;
            bc.Delete();
            Cleanup(account);
        }
    }

    [Fact]
    public void EachSeasonSellsItsOwnStockAndTheSeasonsFollowTheSwitches()
    {
        try
        {
            FestivalSeasons.Forced = null;
            Assert.Equal(FestivalSeasons.Halloween, FestivalSeasons.At(new DateTime(2026, 10, 31)));
            Assert.Equal(FestivalSeasons.Winter, FestivalSeasons.At(new DateTime(2026, 12, 25)));
            Assert.Equal(FestivalSeasons.Spring, FestivalSeasons.At(new DateTime(2027, 4, 1)));
            Assert.Null(FestivalSeasons.At(new DateTime(2027, 7, 4)));

            foreach (var season in FestivalSeasons.All)
            {
                FestivalSeasons.Forced = season;
                var rows = Festival().Rows();
                Assert.All(rows, r => Assert.StartsWith($"festival.{season}.", r.Key));
                Assert.Single(rows, r => r.IsTitle);
                Assert.Single(rows, r => r.Key.EndsWith(".box"));
                _out.WriteLine($"{season}: {rows.Count} rows");
            }
        }
        finally
        {
            FestivalSeasons.Forced = null;
        }
    }

    [Fact]
    public void TheFestivalStallIsPlacedInSeasonAndRemovedWhenItEnds()
    {
        try
        {
            FestivalMarket.Down();
            ClusterFSeasonalState.SetLast(FestivalMarket.StateName, null);

            FestivalSeasons.Forced = "none";
            Assert.Empty(FestivalMarket.Sync()); // a world first seen out of season is left as it is
            Assert.False(FestivalMarket.IsUp);

            FestivalSeasons.Forced = FestivalSeasons.Winter;
            FestivalMarket.Sync();
            Assert.True(FestivalMarket.IsUp);
            Assert.Equal(FestivalMarket.Pieces.Length + 2, FestivalMarket.Placed().Count); // cc-P858: the pieces, the peddler and the wagon
            var peddler = FestivalMarket.Peddler;
            Assert.NotNull(peddler);
            Assert.Equal(FestivalMarket.Anchor, peddler.Location);
            Assert.Empty(FestivalMarket.Sync());           // no change, nothing done
            FestivalMarket.Up();                          // place twice places once
            Assert.Equal(FestivalMarket.Pieces.Length + 2, FestivalMarket.Placed().Count); // cc-P858: the pieces, the peddler and the wagon

            var serials = FestivalMarket.Placed().ToList();
            FestivalSeasons.Forced = "none";
            FestivalMarket.Sync();
            Assert.False(FestivalMarket.IsUp);
            Assert.Empty(FestivalMarket.Placed());
            Assert.All(serials, s => Assert.True(World.FindEntity(s) is null or { Deleted: true }));
        }
        finally
        {
            FestivalSeasons.Forced = null;
            FestivalMarket.Down();
        }
    }

    // ---------------------------------------------------------------- spendable AP

    [Fact]
    public void TheAchievementStoreSpendsApButTheApTotalNeverDrops()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            data.AchievementPoints = 500;
            LeagueTitles.Sync(data);
            var titles = data.LeagueTitles.Owned.Count;
            var store = new AchievementStoreDef();
            Assert.Equal(500, SpendableAp.Instance.Balance(pm));

            var robe = Row(store, "ap.robe");
            Assert.Equal(RewardStoreResult.Bought, RewardStore.Purchase(pm, store, robe));
            Assert.Equal(500 - robe.Price, SpendableAp.Instance.Balance(pm));
            Assert.Equal(500, data.AchievementPoints);                  // the total never drops
            Assert.Equal(titles, data.LeagueTitles.Owned.Count);       // nor does an AP title go
            Assert.Equal(1, Count<Robe>(pm));
            Assert.Equal(robe.Price, SpendableAp.Spent(account.Username));

            // Earning: a new achievement's AP adds to the balance.
            data.AchievementPoints += 25;
            Assert.Equal(525 - robe.Price, SpendableAp.Instance.Balance(pm));

            // Cannot afford: refused, nothing taken; cannot go negative.
            ClusterFAchievementSystem.AddToCounter(account.Username, SpendableAp.SpentCounter, 525 - robe.Price - 10);
            Assert.Equal(10, SpendableAp.Instance.Balance(pm));
            var painting = store.Rows().First(r => r.Key.StartsWith("ap.painting."));
            Assert.Equal(RewardStoreResult.NotEnough, RewardStore.Purchase(pm, store, painting));
            Assert.Equal(10, SpendableAp.Instance.Balance(pm));
            Assert.False(SpendableAp.Instance.TrySpend(pm, 11));
            Assert.False(SpendableAp.Instance.TrySpend(pm, -1));
            Assert.Equal(10, SpendableAp.Instance.Balance(pm));

            // A row opens by the AP total; a category title needs the category.
            Assert.Equal(RewardStoreResult.Locked, RewardStore.Check(pm, store, Row(store, "ap.title.completionist")));
            Assert.Equal(RewardStoreResult.Locked, RewardStore.Check(pm, store, Row(store, "ap.title.curious")));

            // The plaque is engraved with the buyer's name and AP total.
            Assert.Contains("Tester, 525 AP", AchievementStock.Plaque(pm).Name);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- Renown (the League shop's new rows)

    [Fact]
    public void TheLeagueShopsNewRowsSpendRenownAndNeverLifetimeRenown()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            data.AchievementPoints = 1_750;
            data.GrantRenown(3_000, RenownSource.Achievement);
            var lifetime = data.LifetimeRenown;

            var runebook = LeagueShop.Find("runebook")!;
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, runebook));
            Assert.Equal(3_000 - runebook.Renown, data.Renown);
            Assert.Equal(lifetime, data.LifetimeRenown);
            var book = pm.Backpack.Items.OfType<Runebook>().Single();
            Assert.Equal(LeagueShop.LeagueHue, book.Hue);
            Assert.Equal(LootType.Blessed, book.LootType);

            var bag = LeagueShop.Find("bagofsending")!;
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, bag));
            Assert.Equal(30, pm.Backpack.Items.OfType<BagOfSending>().Single().Charges);

            var champion = LeagueShop.Find("title.champion")!;
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, champion));
            Assert.Contains(RewardStoreTitles.LeagueChampion, data.LeagueTitles.Owned);
            Assert.Equal(lifetime, data.LifetimeRenown);

            // The Platinum box: five draws, never a mount and never a row added since F-13.
            var allowed = LeagueShop.BoxPool(LeagueShop.BoxTier.Platinum).Select(f => f(pm)).ToList();
            var types = allowed.Select(i => i.GetType()).ToHashSet();
            allowed.ForEach(i => i.Delete());
            Assert.DoesNotContain(typeof(NameChangeDeed), types);
            Assert.DoesNotContain(typeof(PetBondingPotion), types);
            for (var n = 0; n < 10; n++)
            {
                var box = LeagueShop.MakeBox(pm, LeagueShop.BoxTier.Platinum);
                Assert.Equal(5, box.Contents.Count);
                Assert.All(box.Contents, i => Assert.Contains(i.GetType(), types));
                box.Delete();
            }

            // The Gold box still draws what it drew before: none of the new rows.
            var gold = LeagueShop.BoxPool(LeagueShop.BoxTier.Gold).Select(f => f(pm)).ToList();
            Assert.DoesNotContain(gold, i => i is Runebook or BagOfSending or FurnitureDyeTub or NameChangeDeed or RewardDecor);
            gold.ForEach(i => i.Delete());
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- every row, the guardrails, the price table

    private static IEnumerable<(string Store, RewardStoreRow Row)> EveryStoreRow() =>
        RewardStorePrices.AllStores().SelectMany(s => s.AllRows().Select(r => (s.Title, r)));

    // Our registered art (registry.csv): the guild hall decor (cc-P265), the community art (cc-P237), F-22's winter art.
    private static bool IsOurRegisteredArt(int id) =>
        id is >= GuildHallDecorArt.FirstId and <= GuildHallDecorArt.LastId ||
        id is >= ClusterFCommunityArtRecords.FirstId and <= ClusterFCommunityArtRecords.LastId ||
        id is >= 0x3C40 and <= 0x3C6E;

    // Every ID range our art package claims (registry.csv): an ID inside one must be a registered piece.
    private static bool IsInOurArtRanges(int id) => id is >= 0x3C10 and <= 0x3D0D or >= 0x6D70 and <= 0x6E86 or >= 0x3384 and <= 0x33B6;

    [Fact]
    public void EveryRowOfEveryStoreCanBeMadeAndNoneIsGearOrMagic()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var made = 0;
        try
        {
            Data(account).AchievementPoints = 1_000;

            var rows = EveryStoreRow().Select(x => (x.Store, x.Row.Key, x.Row.Create, x.Row.IsTitle))
                .Concat(LeagueShop.Stock.Select(r => ("League", r.Key, r.Create, r.IsTitle)))
                .ToList();

            foreach (var (store, key, create, isTitle) in rows)
            {
                if (isTitle)
                {
                    continue;
                }

                Assert.NotNull(create);
                var items = create!(pm);
                Assert.NotEmpty(items);
                var all = items.Concat(items.OfType<Container>().SelectMany(c => c.Items))
                    .Concat(items.OfType<LootBox>().SelectMany(b => b.Contents)).ToList();

                foreach (var item in all)
                {
                    // (Not BasePigmentsOfTokuno.IsValidItem, the League shop's test: it reads the Tokuno config, which a
                    // filtered run never loads. Gear, jewels, properties and the special scrolls are checked directly.)
                    Assert.IsNotType<PowerScroll>(item);
                    Assert.IsNotType<StatCapScroll>(item);
                    // A plain harvest tool (the hatchets and the prospector's tool: stock tools built on BaseWeapon) is allowed with no properties.
                    if (item is BaseWeapon weapon && item is Hatchet or Pickaxe or ProspectorsTool)
                    {
                        Assert.True(weapon.Attributes.IsEmpty && weapon.WeaponAttributes.IsEmpty && weapon.SkillBonuses.IsEmpty,
                            $"{store} {key} has properties");
                    }
                    else
                    {
                        Assert.False(item is BaseWeapon or BaseArmor or BaseJewel, $"{store} {key} hands over gear");
                    }
                    if (item is BaseClothing clothing)
                    {
                        Assert.True(clothing.Attributes.IsEmpty && clothing.SkillBonuses.IsEmpty, $"{store} {key} has properties");
                    }

                    if (item is RewardDecor decor && IsInOurArtRanges(decor.ItemID))
                    {
                        Assert.True(IsOurRegisteredArt(decor.ItemID), $"{store} {key}: 0x{decor.ItemID:X4} is not registered art");
                    }
                }

                made++;
                foreach (var item in items)
                {
                    item.Delete();
                }
            }

            _out.WriteLine($"{made} rows made across {RewardStorePrices.AllStores().Count()} stores and the League shop");
            Assert.True(made > 150);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void RowKeysAreUniqueAndThePriceTableRoundTrips()
    {
        var keys = EveryStoreRow().Select(x => x.Row.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());

        // Every guild has a store with its title and cloak.
        Assert.Equal(18, GuildStores.All().Count());
        Assert.All(GuildStores.All(), s => Assert.Contains(s.AllRows(), r => r.IsTitle));
        Assert.All(GuildStores.All(), s => Assert.Contains(s.AllRows(), r => r.Key.EndsWith(".cloak")));

        // The defaults as the file read back as the defaults; a changed price applies; a bad file is refused whole.
        var errors = new List<string>();
        var table = RewardStorePrices.Parse(RewardStorePrices.DefaultsJson(), "test", errors);
        Assert.Empty(errors);
        Assert.NotNull(table);
        Assert.Equal(keys.Count, table!.Rows.Count);

        var previous = RewardStorePrices.Current;
        try
        {
            var lines = RewardStorePrices.Apply("{ \"rows\": { \"ap.robe\": { \"price\": 77 } } }", "test");
            Assert.Contains("Loaded", lines[0]);
            Assert.Equal(77, new AchievementStoreDef().Find("ap.robe")!.Price);
            Assert.Equal(500, new AchievementStoreDef().Find("ap.robe")!.OpensAt);

            lines = RewardStorePrices.Apply("{ \"rows\": { \"no.such.row\": { \"price\": 1 }, \"ap.robe\": { \"price\": -1 } } }", "bad");
            Assert.Contains("REFUSED", lines[0]);
            Assert.Equal(77, new AchievementStoreDef().Find("ap.robe")!.Price);
        }
        finally
        {
            RewardStorePrices.Current = previous;
        }
    }

    // ---------------------------------------------------------------- where a guild store opens

    [Fact]
    public void AGuildStoreOpensForAMemberAtTheGuildmasterBySpeechOrCommand()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var gm = new TailorGuildmaster();
        var ns = PacketTestUtilities.CreateTestNetState();
        try
        {
            gm.MoveToWorld(new Point3D(pm.X, pm.Y - 2, pm.Z), Map.Trammel);
            ns.Account = account;
            pm.NetState = ns;
            ns.Mobile = pm;
            RewardStoreCommands.Configure();

            // Not a member: refused.
            CommandSystem.Handle(pm, $"{CommandSystem.Prefix}guildstore");
            Assert.False(pm.HasGump<RewardStoreGump>());

            Join(pm, "tailoring", 0, 10);
            CommandSystem.Handle(pm, $"{CommandSystem.Prefix}guildstore");
            Assert.True(pm.HasGump<RewardStoreGump>());
            pm.CloseGump<RewardStoreGump>();

            // "guild store" said to the guildmaster, through the engine's speech.
            pm.DoSpeech("guild store", [0x6], MessageType.Regular, 0x3B2);
            Assert.True(pm.HasGump<RewardStoreGump>());
            pm.CloseGump<RewardStoreGump>();

            // The Rangers' League guildmaster is its own NPC (not a stock guildmaster): it answers the phrase too.
            gm.Delete();
            var outrider = new OutridersGuildmaster();
            try
            {
                outrider.MoveToWorld(new Point3D(pm.X + 2, pm.Y, pm.Z), Map.Trammel);
                Join(pm, "rangers", 0, 10);
                pm.DoSpeech("guild store", [0x6], MessageType.Regular, 0x3B2);
                Assert.Equal("guild.rangers", pm.FindGump<RewardStoreGump>()?.Store.Key);
            }
            finally
            {
                outrider.Delete();
            }
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            gm.Delete();
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- the window and the confirm step

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    [Fact]
    public void TheWindowGreysALockedRowAndTheConfirmStepBuys()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var ns = PacketTestUtilities.CreateTestNetState();
        try
        {
            ns.Account = account;
            pm.NetState = ns;
            ns.Mobile = pm;

            var data = Data(account);
            data.AchievementPoints = 150;
            var store = new AchievementStoreDef();
            var gump = new RewardStoreGump(pm, store);
            var labels = gump.Entries.OfType<GumpLabel>().ToList();
            var buttons = gump.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToHashSet();

            // The robe opens at 500 AP total: greyed, "Requires 500 AP total", no buy button.
            var robe = gump.Rows.ToList().FindIndex(r => r.Key == "ap.robe");
            Assert.Contains(labels, l => l.Text == "Requires 500 AP total" && l.Hue == RequirementStyle.NotMetHue);
            Assert.Contains(labels, l => l.Text == gump.Rows[robe].Label && l.Hue == RequirementStyle.DisabledHue);
            Assert.DoesNotContain(RewardStoreGump.BtnRowBase + robe, buttons);

            // A sash: open, affordable: a buy button; the confirm step buys it for exactly its price.
            var sash = gump.Rows.ToList().FindIndex(r => r.Key == "ap.sash.gold");
            Assert.Contains(RewardStoreGump.BtnRowBase + sash, buttons);

            var confirm = new RewardStoreConfirmGump(pm, store, gump.Rows[sash]);
            Press(confirm, ns, RewardStoreConfirmGump.BtnBuy);
            Assert.Equal(1, Count<BoundCosmeticSash>(pm));
            Assert.Equal(150 - gump.Rows[sash].Price, SpendableAp.Instance.Balance(pm));

            // Every store's window draws (a guild store, the Festival store in season).
            Join(pm, "mining", 0, 100);
            Assert.NotEmpty(new RewardStoreGump(pm, Guild("mining")).Entries);
            FestivalSeasons.Forced = FestivalSeasons.Spring;
            Assert.NotEmpty(new RewardStoreGump(pm, Festival()).Rows);
        }
        finally
        {
            FestivalSeasons.Forced = null;
            pm.NetState = null;
            ns.Mobile = null;
            Cleanup(account);
        }
    }
}
