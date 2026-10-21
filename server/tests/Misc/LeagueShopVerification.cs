// LeagueShopVerification.cs
//
// cc-P229 (F-13): the League reward shop ("AP unlocks, Renown buys") and the League titles. Notes in shard-migration
// notes/cc-P229-league-reward-shop.md.
//
// Facts:
//   1. A buy with enough Renown and AP succeeds, puts the item in the pack and takes exactly the price.
//   2. One Renown short: refused, nothing taken, nothing given.
//   3. AP one below the row: refused even with Renown to spare, nothing taken, nothing given.
//   4. A purchase leaves LifetimeRenown, League rank and the promotion's Lifetime Renown check exactly as they were.
//   5. A title row is bought once: the title is held and a second buy is refused with nothing taken.
//   6. A title unlocked by AP stays unlocked after the account's AP drops (a staff revoke), and can still be shown.
//   7. The picked title shows on the mobile (Mobile.Title, drawn after the name); no title clears it; a title the
//      account does not hold is refused.
//   8. The account's titles round-trip through a save (v21), and a v20 save reads with none.
//   9. The stock is F-13's starter table, row for row, and nothing it hands over is an artifact or enhanced magic item.
//  10. A System's loot box holds only items from rows its own AP opens (and the League sash), with no gold or Renown.
//  11. The window: a locked row is greyed with "Requires 500 AP" and has no buy button; an open one shows its price and
//      has one; the confirm step lists AP and Renown; the Registrar and the achievements gump both open the shop.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueShopVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueShopVerification(ITestOutputHelper output)
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

    private static Account NewAccount() => new($"p229{Guid.NewGuid():N}"[..16], "p229-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);
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

    private static LeagueShopEntry Row(string key) => LeagueShop.Find(key) ?? throw new InvalidOperationException(key);

    private static int PackCount<T>(PlayerMobile pm) where T : Item => pm.Backpack.Items.OfType<T>().Count();

    // ---------------------------------------------------------------- 1-3

    [Fact]
    public void ABuyWithEnoughRenownAndApTakesExactlyThePrice()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            data.AchievementPoints = 100;
            data.Renown = 200;

            var deed = Row("hairdeed");
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, deed));
            Assert.Equal(200 - deed.Renown, data.Renown);
            Assert.Equal(1, PackCount<HairRestylingDeed>(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void OneRenownShortIsRefusedAndNothingIsTaken()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            var deed = Row("hairdeed");
            data.AchievementPoints = 100;
            data.Renown = deed.Renown - 1;

            Assert.Equal(LeagueShopResult.NotEnoughRenown, LeagueShop.Purchase(pm, deed));
            Assert.Equal(deed.Renown - 1, data.Renown);
            Assert.Equal(0, PackCount<HairRestylingDeed>(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void ApBelowTheRowIsRefusedEvenWithRenownToSpare()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            var cloak = Row("cloak");
            data.AchievementPoints = cloak.ApRequired - 1;
            data.Renown = 10_000;

            Assert.Equal(LeagueShopResult.NeedsAp, LeagueShop.Purchase(pm, cloak));
            Assert.Equal(10_000, data.Renown);
            Assert.Equal(0, PackCount<LeagueCloak>(pm));

            // The title rows obey the same gate.
            var favorite = Row("title.favorite");
            Assert.Equal(LeagueShopResult.NeedsAp, LeagueShop.Purchase(pm, favorite));
            Assert.Equal(10_000, data.Renown);
            Assert.DoesNotContain(LeagueTitles.SystemsFavorite, data.LeagueTitles.Owned);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void APurchaseLeavesLifetimeRenownAndLeagueRankUnchanged()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
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

            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, Row("regalia")));
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, Row("title.favorite")));

            Assert.Equal(3_000 - 1_000 - 500, data.Renown);
            Assert.Equal(lifetime, data.LifetimeRenown);
            Assert.Equal(rank, ClusterFLeagueRanks.GetRank(pm));
            Assert.Equal(check, ClusterFLeagueRanks.Evaluate(pm).Renown.Detail);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ATitleRowIsBoughtOnce()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            data.Renown = 100;
            var row = Row("title.registered");

            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, row));
            Assert.Contains(LeagueTitles.RegisteredAdventurer, data.LeagueTitles.Owned);
            Assert.Equal(100 - row.Renown, data.Renown);

            Assert.Equal(LeagueShopResult.AlreadyOwned, LeagueShop.Purchase(pm, row));
            Assert.Equal(100 - row.Renown, data.Renown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ATitleUnlockedByApStaysUnlocked()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            Assert.False(LeagueTitles.Holds(data, "ap-500"));

            data.AchievementPoints = 500;
            Assert.True(LeagueTitles.Holds(data, "ap-500"));
            Assert.True(LeagueTitles.Holds(data, "ap-250"));
            Assert.False(LeagueTitles.Holds(data, "ap-1000"));

            data.AchievementPoints = 0; // a staff revoke
            Assert.True(LeagueTitles.Holds(data, "ap-500"));
            Assert.True(LeagueTitles.Choose(pm, "ap-500"));
            Assert.Equal("the Distinguished", pm.Title);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void EarningAnAchievementUnlocksItsApTitles()
    {
        var account = NewAccount();
        NewCharacter(account);
        try
        {
            var data = Data(account);
            data.AchievementPoints = 99;
            var def = ClusterFAchievementSystem.Definitions.Values.First(d => !d.Retired && d.AP > 0 && d.PrerequisiteKey == null && d.RewardItems.Length == 0);

            Assert.True(ClusterFAchievementSystem.TryGrant(account, def.Key));
            Assert.Contains("ap-100", data.LeagueTitles.Owned);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ThePickedTitleShowsOnTheMobile()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var other = NewCharacter(account, 1);
        try
        {
            var data = Data(account);
            Assert.False(LeagueTitles.Choose(pm, LeagueTitles.SystemsFavorite));
            Assert.Null(pm.Title);

            data.LeagueTitles.Owned.Add(LeagueTitles.SystemsFavorite);
            Assert.True(LeagueTitles.Choose(pm, LeagueTitles.SystemsFavorite));
            Assert.Equal("the System's Favorite", pm.Title);

            // One title per character: the other character on the account shows none until it picks.
            Assert.Null(other.Title);
            Assert.Null(LeagueTitles.ShownKey(data, other));

            // The name property is "~1_PREFIX~~2_NAME~~3_SUFFIX~" (1050045) with the title in the suffix.
            Assert.Contains(" the System's Favorite", pm.ApplyNameSuffix($" {pm.Title}"));

            // Login puts the account's choice back on the mobile.
            pm.Title = null;
            LeagueTitles.OnLogin(pm);
            Assert.Equal("the System's Favorite", pm.Title);

            Assert.True(LeagueTitles.Choose(pm, null));
            Assert.Null(pm.Title);
            Assert.Null(LeagueTitles.ShownKey(data, pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 8

    private static (byte[] Buffer, long Length) Write(Action<IGenericWriter> serialize)
    {
        var writer = new BufferWriter(true);
        serialize(writer);
        return (writer.Buffer, writer.Position);
    }

    [Fact]
    public void TheAccountsTitlesRoundTripThroughASave()
    {
        var original = new ClusterFAccountData { Renown = 42, AchievementPoints = 600, LifetimeRenown = 900 };
        original.LeagueTitles.Owned.Add(LeagueTitles.VeteranOfTheLeague);
        original.LeagueTitles.Owned.Add("ap-500");
        original.LeagueTitles.Shown[0x1234u] = LeagueTitles.VeteranOfTheLeague;
        original.LeagueTitles.Shown[0x5678u] = "ap-500";

        var (buffer, length) = Write(original.Serialize);
        Assert.Equal(22, BitConverter.ToInt32(buffer, 0)); // cc-P238: 22

        var reader = new BufferReader(buffer);
        var copy = new ClusterFAccountData(reader);
        Assert.Equal(length, reader.Position);

        Assert.Equal(42, copy.Renown);
        Assert.Equal(600, copy.AchievementPoints);
        Assert.Equal(900, copy.LifetimeRenown);
        Assert.Equal(original.LeagueTitles.Owned.OrderBy(k => k), copy.LeagueTitles.Owned.OrderBy(k => k));
        Assert.Equal(original.LeagueTitles.Shown.OrderBy(kv => kv.Key), copy.LeagueTitles.Shown.OrderBy(kv => kv.Key));

        // A v20 save is the same bytes with no title record and version 20: it reads with no titles. cc-P238: nor the
        // v22 block record after it (empty: its version and a zero count, 8 bytes).
        var v20 = buffer.Take((int)length - TitleRecordLength(original.LeagueTitles) - 8).ToArray();
        BitConverter.GetBytes(20).CopyTo(v20, 0);
        var old = new ClusterFAccountData(new BufferReader(v20));
        Assert.Empty(old.LeagueTitles.Owned);
        Assert.Empty(old.LeagueTitles.Shown);
        Assert.Equal(42, old.Renown);

        // A record version this build does not know is refused, not misread.
        var future = buffer.Take((int)length).ToArray();
        BitConverter.GetBytes(LeagueTitleRecord.CurrentVersion + 1)
            .CopyTo(future, (int)length - TitleRecordLength(original.LeagueTitles) - 8); // cc-P238: the v22 tail follows
        Assert.Throws<System.IO.InvalidDataException>(() => new ClusterFAccountData(new BufferReader(future)));
    }

    private static int TitleRecordLength(LeagueTitleRecord record) => (int)Write(record.Serialize).Length;

    // ---------------------------------------------------------------- 9

    // F-13's starter table (shard-migration docs/feature-queue.md, F-13, approved 2026-09-30): AP, Renown, in order.
    private static readonly (string Key, int Ap, int Renown)[] StarterTable =
    [
        ("title.registered", 0, 25), ("tabard", 0, 50), ("box.bronze", 0, 40),
        ("hairdeed", 100, 75), ("rune", 100, 60),
        ("banner", 250, 150), ("cloak", 250, 200), ("box.silver", 250, 150),
        ("trophy", 500, 250), ("pigment", 500, 300), ("title.veteran", 500, 200),
        ("box.gold", 1000, 400),
        ("regalia", 1750, 1000), ("title.favorite", 1750, 500),
    ];

    [Fact]
    public void TheStockIsF13sStarterTableAndHandsOverNoMagicItem()
    {
        Assert.Equal(StarterTable.Length, LeagueShop.Stock.Length);
        for (var i = 0; i < StarterTable.Length; i++)
        {
            var (key, ap, renown) = StarterTable[i];
            var row = LeagueShop.Stock[i];
            _out.WriteLine($"{row.ApRequired,5} AP {row.Renown,5} Renown  {row.Label}");
            Assert.Equal((key, ap, renown), (row.Key, row.ApRequired, row.Renown));
        }

        Assert.Equal(3, LeagueShop.Stock.Count(r => r.IsTitle));
        Assert.Equal(3, LeagueShop.Stock.Count(r => r.Kind == LeagueShopKind.LootBox));

        // F-13's AP totals: every achievement's AP, for the AP titles' top threshold. Counted, not quoted.
        var total = ClusterFAchievementSystem.Definitions.Values.Where(d => !d.Retired).Sum(d => d.AP);
        _out.WriteLine($"AP from every achievement not retired: {total:N0}");
        Assert.True(LeagueTitles.All.Max(t => t.FreeAtAp) <= total, "the top AP title must be reachable");

        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            foreach (var row in LeagueShop.Stock.Where(r => !r.IsTitle))
            {
                var items = row.Create!(pm);
                var all = items.Concat(items.OfType<LootBox>().SelectMany(b => b.Contents)).ToList();
                foreach (var item in all)
                {
                    _out.WriteLine($"  {row.Key}: {item.GetType().Name} 0x{item.ItemID:X4} hue {item.Hue}");
                    Assert.False(BasePigmentsOfTokuno.IsValidItem(item), $"{row.Key} hands over a magic item");
                    Assert.IsNotType<PowerScroll>(item);
                    Assert.False(item is BaseWeapon or BaseArmor or BaseJewel, $"{row.Key} hands over gear");
                }

                foreach (var item in items)
                {
                    item.Delete();
                }
            }

            // The League's hues, and the tabard refuses a dye tub.
            Assert.Equal(LeagueShop.LeagueHue, new LeagueTabard().Hue);
            Assert.Equal(LeagueShop.CloakHue, new LeagueCloak().Hue);
            Assert.Equal(LeagueShop.RegaliaHue, new LeagueRegaliaRobe().Hue);
            var tabard = new LeagueTabard();
            var tub = new DyeTub { DyedHue = 0x21 };
            Assert.False(tabard.Dye(pm, tub));
            Assert.Equal(LeagueShop.LeagueHue, tabard.Hue);
            tabard.Delete();
            tub.Delete();

            // The rune is blessed and marked one step in front of the Registrar.
            var rune = LeagueShop.MakeLeagueRune();
            Assert.True(rune.Marked);
            Assert.Equal(LootType.Blessed, rune.LootType);
            Assert.Equal(Map.Trammel, rune.TargetMap);
            Assert.Equal(ClusterFNewHavenSeeder.RegistrarAnchor.Y, rune.Target.Y);
            rune.Delete();
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void ALootBoxHoldsOnlyRowsItsOwnApOpens()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            foreach (var tier in Enum.GetValues<LeagueShop.BoxTier>())
            {
                var (_, _, draws, maxAp) = LeagueShop.BoxInfo(tier);
                var allowed = LeagueShop.Stock
                    .Where(r => r.Create != null && r.Kind != LeagueShopKind.LootBox && r.ApRequired <= maxAp)
                    .Select(r => r.Create!(pm)[0])
                    .ToList();
                var allowedTypes = allowed.Select(i => i.GetType()).Append(typeof(BoundCosmeticSash)).ToHashSet();
                allowed.ForEach(i => i.Delete());

                for (var n = 0; n < 20; n++)
                {
                    var box = LeagueShop.MakeBox(pm, tier);
                    Assert.Equal(draws, box.Contents.Count);
                    Assert.Equal(0, box.Gold);
                    Assert.Equal(0, box.Renown);
                    Assert.Equal(account.Username, box.BoundAccount);
                    Assert.All(box.Contents, i => Assert.Contains(i.GetType(), allowedTypes));
                    box.Delete();
                }
            }

            // Bronze opens at 0 AP, so it never holds the 100 AP hair deed or rune.
            var bronze = LeagueShop.BoxPool(LeagueShop.BoxTier.Bronze).Select(f => f(pm)).ToList();
            Assert.DoesNotContain(bronze, i => i is HairRestylingDeed or RecallRune);
            bronze.ForEach(i => i.Delete());
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 11

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    [Fact]
    public void TheWindowGreysLockedRowsAndTheConfirmStepListsTheRequirements()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = Data(account);
            data.AchievementPoints = 250;
            data.Renown = 100;

            var shop = new LeagueShopGump(pm);
            var labels = shop.Entries.OfType<GumpLabel>().ToList();
            var buttons = shop.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToHashSet();

            // Trophy: 500 AP, locked: greyed, "Requires 500 AP", no buy button.
            var trophy = Array.FindIndex(LeagueShop.Stock, r => r.Key == "trophy");
            Assert.Contains(labels, l => l.Text == "Requires 500 AP" && l.Hue == RequirementStyle.NotMetHue);
            Assert.Contains(labels, l => l.Text == LeagueShop.Stock[trophy].Label && l.Hue == RequirementStyle.DisabledHue);
            Assert.DoesNotContain(LeagueShopGump.BtnRowBase + trophy, buttons);

            // Hair deed: open (100 AP), affordable (75): a buy button, its price in the met colour.
            var deed = Array.FindIndex(LeagueShop.Stock, r => r.Key == "hairdeed");
            Assert.Contains(LeagueShopGump.BtnRowBase + deed, buttons);
            Assert.Contains(labels, l => l.Text == "75 Renown" && l.Hue == RequirementStyle.MetHue);

            // Cloak: open (250 AP), 200 Renown with 100 held: no button, the price in red.
            var cloak = Array.FindIndex(LeagueShop.Stock, r => r.Key == "cloak");
            Assert.DoesNotContain(LeagueShopGump.BtnRowBase + cloak, buttons);
            Assert.Contains(labels, l => l.Text == "200 Renown" && l.Hue == RequirementStyle.NotMetHue);

            var confirm = new LeagueShopConfirmGump(pm, LeagueShop.Stock[deed]);
            var text = confirm.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
            text.ForEach(_out.WriteLine);
            Assert.Contains("Achievement Points 250 / 100", text);
            Assert.Contains("Renown 100 / 75", text);
            Assert.Contains("All requirements met", text);
            Assert.True(confirm.Requirements.AllMet);

            var locked = new LeagueShopConfirmGump(pm, LeagueShop.Stock[trophy]);
            Assert.False(locked.Requirements.AllMet);
            Assert.Contains("Achievement Points 250 / 500 (need 250 more)",
                locked.Entries.OfType<GumpLabel>().Select(l => l.Text));

            // Both doors open the shop: the Registrar's menu and the achievements gump's footer.
            var registrar = new LeagueRegistrarGump(pm);
            Assert.Contains(registrar.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueRegistrarGump.BtnShop);
            Assert.Contains(registrar.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueRegistrarGump.BtnTitles);
            var achievements = new AchievementsGump(pm);
            Assert.Contains(achievements.Entries.OfType<GumpButton>(), b => b.ButtonID == AchievementsGump.BtnLeagueShop);
            Assert.Contains(achievements.Entries.OfType<GumpButton>(), b => b.ButtonID == AchievementsGump.BtnTitles);

            // The title picker: a held AP title can be pressed; a bought-only title not held says where to get it.
            var picker = new LeagueTitleGump(pm);
            var ap250 = Array.FindIndex(LeagueTitles.All, t => t.Key == "ap-250");
            Assert.Contains(picker.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueTitleGump.BtnTitleBase + ap250);
            Assert.Contains("League Shop", picker.Entries.OfType<GumpLabel>().Select(l => l.Text));
            Assert.Contains("Requires 1,000 AP", picker.Entries.OfType<GumpLabel>().Select(l => l.Text));
        }
        finally
        {
            Cleanup(account);
        }
    }
}
