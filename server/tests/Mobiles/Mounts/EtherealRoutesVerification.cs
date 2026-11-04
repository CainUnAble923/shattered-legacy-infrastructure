// EtherealRoutesVerification.cs
//
// cc-P794: how the ethereal looks are earned (server/customizations/Mobiles/Mounts/EtherealLooks.cs, Chase's choice of 11-04).
//
// Facts:
//   1. The League shop sells four, at their prices, through the real purchase: AP opens the row, exactly the Renown goes.
//   2. No loot box can draw one.
//   3. Three achievements name their look, are live, and are not retired.
//   4. Earned online: one statuette at once, and no second one at the next login.
//   5. Earned offline, or before the looks existed: nothing yet, then one at login, on one character of the account only.
//   6. The prices are settings.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class EtherealRoutesVerification
{
    // Its own spot: shared spots can hold another fact's leftovers (cc-P793).
    private static readonly Point3D Here = new(1796, 1183, 0);

    public EtherealRoutesVerification()
    {
        ShardTestHost.EnsureAccounts();

        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    public static TheoryData<string, Type, int> ShopRows => new()
    {
        { "ethereal.peacock", typeof(EtherealPeacock), 1500 },
        { "ethereal.windrunner", typeof(EtherealWindrunner), 2000 },
        { "ethereal.tiger", typeof(EtherealTiger), 2000 },
        { "ethereal.light-tiger", typeof(EtherealLightTiger), 2000 }
    };

    public static TheoryData<string, Type> Achieved => new()
    {
        { "legacy.new_haven_all", typeof(EtherealLasher) },
        { "mining.master_prospector", typeof(EtherealBoura) },
        { "skills.triple_master", typeof(EtherealAncientHellHound) }
    };

    private static Account NewAccount() => new($"p794{Guid.NewGuid():N}"[..16], "p794-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(Here, Map.TerMur);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            (account[i] as PlayerMobile)?.Delete();
        }

        Accounts.Remove(account);
    }

    private static ClusterFAccountData Data(Account account) => ClusterFAccountPersistence.GetOrCreate(account);

    private static int Held(PlayerMobile pm, Type type) => pm.Backpack!.Items.Count(i => i.GetType() == type);

    /// <summary>Grants an achievement through its prerequisites, as a player reaches them.</summary>
    private static void Earn(Account account, string key)
    {
        var def = ClusterFAchievementSystem.Definitions[key];
        if (def.PrerequisiteKey != null && !ClusterFAchievementSystem.HasEarned(account, def.PrerequisiteKey))
        {
            Earn(account, def.PrerequisiteKey);
        }

        Assert.True(ClusterFAchievementSystem.TryGrant(account, key));
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [MemberData(nameof(ShopRows))]
    public void TheShopSellsItThroughTheRealPurchase(string key, Type type, int renown)
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var row = LeagueShop.Find(key);
            Assert.NotNull(row);
            Assert.Equal(LeagueShopKind.Mount, row.Kind);
            Assert.Equal(1750, row.ApRequired);
            Assert.Equal(renown, row.Renown);

            var data = Data(account);
            data.GrantRenown(renown + 7, RenownSource.Achievement);
            data.AchievementPoints = 1749;
            Assert.Equal(LeagueShopResult.NeedsAp, LeagueShop.Purchase(pm, row));
            Assert.Equal(0, Held(pm, type));
            Assert.Equal(renown + 7, data.Renown);

            data.AchievementPoints = 1750;
            Assert.Equal(LeagueShopResult.Bought, LeagueShop.Purchase(pm, row));
            Assert.Equal(1, Held(pm, type));
            Assert.Equal(7, data.Renown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void NoLootBoxDrawsOne()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            foreach (var tier in Enum.GetValues<LeagueShop.BoxTier>())
            {
                foreach (var draw in LeagueShop.BoxPool(tier))
                {
                    var item = draw(pm);
                    Assert.IsNotAssignableFrom<EtherealMount>(item);
                    item.Delete();
                }
            }
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [MemberData(nameof(Achieved))]
    public void TheAchievementNamesItsLook(string key, Type type)
    {
        var def = ClusterFAchievementSystem.Definitions[key];
        Assert.False(def.Retired);
        Assert.Equal(type, Assert.Single(def.RewardItems));
        Assert.Contains(EtherealLooks.Achieved, a => a.Achievement == key && a.Type == type);
    }

    // ---------------------------------------------------------------- 4

    [Theory]
    [MemberData(nameof(Achieved))]
    public void EarnedOnlineItIsPaidOnceAndNotAgainAtLogin(string key, Type type)
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        try
        {
            Earn(account, key);
            Assert.Equal(1, Held(pm, type));

            ClusterFAchievementSystem.GrantAtLogin(pm);
            ClusterFAchievementSystem.GrantAtLogin(pm);
            Assert.Equal(1, Held(pm, type));
        }
        finally
        {
            pm.NetState = null;
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Theory]
    [MemberData(nameof(Achieved))]
    public void APastEarnerIsPaidAtLoginOnceForTheAccount(string key, Type type)
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var alt = NewCharacter(account, 1);
        try
        {
            Earn(account, key); // no character online: nothing can be handed over
            Assert.Equal(0, Held(pm, type));
            Assert.Equal(0, ClusterFAchievementSystem.GetCounter(account.Username, EtherealLooks.PaidCounter(key)));

            ClusterFAchievementSystem.GrantAtLogin(pm);
            Assert.Equal(1, Held(pm, type));
            Assert.Equal(1, ClusterFAchievementSystem.GetCounter(account.Username, EtherealLooks.PaidCounter(key)));

            ClusterFAchievementSystem.GrantAtLogin(pm);
            ClusterFAchievementSystem.GrantAtLogin(alt);
            Assert.Equal(1, Held(pm, type));
            Assert.Equal(0, Held(alt, type));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void NobodyIsPaidForWhatTheyHaveNotEarned()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            EtherealLooks.PayOwed(pm);
            Assert.DoesNotContain(pm.Backpack!.Items, i => i is EtherealMount);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ThePricesAreSettings()
    {
        var key = EtherealLooks.RenownSettingPrefix + "peacock";
        var ap = ServerConfiguration.GetSetting(EtherealLooks.ShopApSetting, EtherealLooks.DefaultShopAp);
        var renown = ServerConfiguration.GetSetting(key, 1500);
        try
        {
            ServerConfiguration.SetSetting(EtherealLooks.ShopApSetting, "1800");
            ServerConfiguration.SetSetting(key, "1234");
            var row = EtherealLooks.ShopRows().Single(r => r.Key == "ethereal.peacock");
            Assert.Equal((1800, 1234), (row.ApRequired, row.Renown));
        }
        finally
        {
            ServerConfiguration.SetSetting(EtherealLooks.ShopApSetting, ap.ToString());
            ServerConfiguration.SetSetting(key, renown.ToString());
        }
    }
}
