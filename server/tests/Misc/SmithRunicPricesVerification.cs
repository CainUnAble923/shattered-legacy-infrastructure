// SmithRunicPricesVerification.cs
//
// cc-P46 Part E (Chase, 2026-10-03). With cc-P42's x3 Seals one large Celestial exceptional deed bought a Valorite runic
// hammer outright, so the runic hammers from Gold up cost three times what they did (Gold 1,200 -> 3,600, Agapite
// 1,800 -> 5,400, Verite 3,000 -> 9,000, Valorite 5,000 -> 15,000), and the "coming soon" Platinum..Celestial entries
// too (7,500 .. 50,000 -> 22,500 .. 150,000), so the ladder stays in order. Dull Copper..Bronze and every non-runic
// item are unchanged. Notes: shard-migration notes/cc-P46-smith-orders-2.md, Part E. cc-P57 Part A multiplied every catalog
// price by SmithSealCatalogGump.PriceFactor (3); the numbers below are the cc-P46 ones times that factor.
//
// Facts:
//   1. The catalog's rows carry the new prices, the unchanged ones as they were, and the ladder rises.
//   2. Through the catalog's own buy button: one Seal short of the new price buys nothing; the price buys the hammer and
//      leaves no Seals. Gold to Valorite.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithRunicPricesVerification
{
    private readonly ITestOutputHelper _out;

    public SmithRunicPricesVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
    }

    private static int[] Costs(SmithSealCatalogGump.Cat cat) => SmithSealCatalogGump.Rows(cat).Select(r => r.Cost).ToArray();

    // cc-P57 Part A: the cc-P46 prices times three.
    private static int[] X3(params int[] old) => old.Select(p => p * 3).ToArray();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheCatalogCarriesTheNewRunicPrices()
    {
        Assert.Equal(X3(200, 350, 550, 800, 3_600, 5_400, 9_000, 15_000), Costs(SmithSealCatalogGump.Cat.RunicVanilla));
        Assert.Equal(X3(22_500, 30_000, 39_000, 51_000, 66_000, 84_000, 108_000, 150_000),
            Costs(SmithSealCatalogGump.Cat.RunicPostVal));
        Assert.All(SmithSealCatalogGump.Rows(SmithSealCatalogGump.Cat.RunicPostVal), r => Assert.True(r.ComingSoon));

        // Tools and supplies, smithy hammers: unchanged by cc-P46, times three since cc-P57.
        Assert.Equal(X3(50, 50, 100, 200, 300, 600, 150, 250), Costs(SmithSealCatalogGump.Cat.Tools));
        Assert.Equal(X3(100, 200, 500, 1_000), Costs(SmithSealCatalogGump.Cat.AncientHammers));

        var ladder = Costs(SmithSealCatalogGump.Cat.RunicVanilla).Concat(Costs(SmithSealCatalogGump.Cat.RunicPostVal)).ToArray();
        _out.WriteLine($"runic ladder: {string.Join(", ", ladder.Select(c => c.ToString("N0")))}");
        for (var i = 1; i < ladder.Length; i++)
        {
            Assert.True(ladder[i] > ladder[i - 1], $"the ladder falls at row {i}");
        }
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData(4, CraftResource.Gold, 10_800)]
    [InlineData(5, CraftResource.Agapite, 16_200)]
    [InlineData(6, CraftResource.Verite, 27_000)]
    [InlineData(7, CraftResource.Valorite, 45_000)]
    public void TheBuyButtonChargesTheNewPrice(int row, CraftResource ore, int price)
    {
        var account = new Account($"p46e{Guid.NewGuid():N}"[..16], "p46-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1590, 1590, 0), Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = account;
        pm.NetState = ns;
        ns.Mobile = pm;
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        guild.JoinedGuilds.Add("smithing");

        void Buy() => new SmithSealCatalogGump(pm, SmithSealCatalogGump.Cat.RunicVanilla).OnResponse(ns,
            new RelayInfo(100 + row, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty, ReadOnlySpan<Range>.Empty,
                ReadOnlySpan<byte>.Empty));

        RunicHammer Hammer() => pm.Backpack.Items.OfType<RunicHammer>().SingleOrDefault(h => h.Resource == ore);

        try
        {
            guild.AddCurrency("smithing", price - 1);
            Buy();
            Assert.Null(Hammer());
            Assert.Equal(price - 1, guild.GetCurrency("smithing"));

            guild.AddCurrency("smithing", 1);
            Buy();
            Assert.NotNull(Hammer());
            Assert.Equal(0, guild.GetCurrency("smithing"));
            _out.WriteLine($"{ore} runic hammer: {price:N0} Seals");
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            ClusterFAccountPersistence.Get(account)?.ClearGuildData();
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
