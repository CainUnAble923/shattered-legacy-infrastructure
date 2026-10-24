// StoneMiningGateVerification.cs
//
// cc-P416 (Chase 2026-10-22): stone mining is learned before stone work; the two raw-ore contracts become mining quotas;
// gold can stand in for missing vouchers on a pickaxe restoration (customizations ClusterFWorkOrderSystem.cs,
// GuildContractLedgerGump.cs, MinersCompactLiaisonGump.cs, CompactOreSatchelRoutingHook.cs). Notes in shard-migration
// notes/cc-P416-stone-mining-gate.md.
//
// Facts:
//   1. A Miner who has not learned stone mining sees no Stone Contract in the ledger (not even a locked card), through the
//      Dispatch Ledger too, and sees the line that says where to learn it. After reading Mining For Quality Stone they
//      appear and the line is gone.
//   2. A Stone Contract already held stays finishable without stone mining.
//   3. The dashboard sells the book: offered with the rank, the price and the 100 Mining rule; refused below Journeyman;
//      sold at the rank for 50 vouchers (the pinned book); not offered, and refused, once stone mining is learned.
//   4. The ore contracts are mining quotas, one amount per metal: ore mined after taking one counts and completes it;
//      ore mined before taking it does not; ore bought or picked up (in the pack and bank) does not. Nothing is taken at
//      turn-in.
//   5. A contract taken before quotas (a version 2 entry) may be finished either way: its old turn-in, or by mining. The
//      flag and the mined count survive a save.
//   6. Restoration: enough vouchers pays as before; short by N, the member restores with N x the rate in gold on top of the
//      base, the vouchers they have and the same materials; not enough gold refuses with the whole shortfall shown.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Accounting;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StoneMiningGateVerification
{
    private static readonly Point3D Spot = new(1436, 1770, 0);

    private static readonly string[] Ores = ["DullCopper", "ShadowIron", "Copper", "Bronze", "Gold", "Agapite", "Verite", "Valorite"];

    private readonly ITestOutputHelper _out;

    public StoneMiningGateVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        ClusterFWorkOrderSystem.Configure();
    }

    private sealed class Miner : IDisposable
    {
        public readonly Account Account = new($"p416{Guid.NewGuid():N}"[..16], "p416-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Miner(int standing = 100_000, int vouchers = 0, double mining = 100.0)
        {
            Pm = new PlayerMobile { Player = true, Body = 0x190 };
            Pm.AddItem(new Backpack());
            Pm.RawStr = 100;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Pm.Skills.Mining.Base = mining;

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Guild.JoinedGuilds.Add("mining");
            Guild.GuildReputation["mining"] = standing;
            Guild.GuildCurrency["mining"] = vouchers;
        }

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);

        public CharacterGuildData Guild => Data.GetOrCreateGuildData(Pm.Serial);

        public int Vouchers => Guild.GuildCurrency.TryGetValue("mining", out var v) ? v : 0;

        public void ReportAll()
        {
            foreach (var ore in Ores)
            {
                Data.OreDiscoveries[ore] = new OreDiscoveryEntry(ore) { State = DiscoveryState.Reported };
            }
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static WorkOrderDef Order(string key) => ClusterFWorkOrderSystem.Get(key)!;

    private static List<WorkOrderDef> StoneContracts() =>
        ClusterFWorkOrderSystem.GetForGuild("mining").Where(d => d.Type == WorkOrderType.StoneContract).ToList();

    // Every label and html text the Available tab draws, over every scroll position.
    private static List<string> AvailableTexts(PlayerMobile pm)
    {
        var texts = new List<string>();
        for (var offset = 0; offset < 80; offset++)
        {
            var g = new GuildContractLedgerGump(pm, "mining", GuildContractLedgerGump.Tab.Available, offset);
            texts.AddRange(g.Entries.OfType<GumpLabel>().Select(l => l.Text));
            texts.AddRange(g.Entries.OfType<GumpHtml>().Select(h => h.Text));
        }

        return texts;
    }

    private static List<string> Labels(Gump g) => g.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0 ||
               span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // Ore as the harvest hands it over (Mining.Give, the hook the quotas and the Logbook share); at the feet when the pack
    // is too heavy, as a real harvest may.
    private static void Mine(PlayerMobile pm, Item ore) => Assert.True(Mining.System.Give(pm, ore, true));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AnUnlearnedMinerSeesNoStoneContractsAndTheHintThenTheyAppearAfterTheBook()
    {
        using var m = new Miner();
        m.ReportAll();
        // One colored vein found but not yet reported: its Stone Contract would show as a locked card.
        m.Data.OreDiscoveries["DullCopper"].State = DiscoveryState.Discovered;

        var stone = StoneContracts();
        Assert.Equal(10, stone.Count);
        Assert.False(m.Pm.StoneMining);

        var before = AvailableTexts(m.Pm);
        foreach (var def in stone)
        {
            Assert.DoesNotContain(before, t => t.StartsWith(def.Title, StringComparison.Ordinal));
            Assert.False(def.IsEligible(m.Pm, m.Data));
            Assert.Equal(ClusterFWorkOrderSystem.StoneMiningHint, def.IneligibleReason(m.Pm, m.Data));
        }

        Assert.Contains(before, t => t.Contains("Mining For Quality Stone") && t.Contains("Journeyman"));
        Assert.Contains(before, t => t == "Iron Shipment"); // the other contracts are still listed
        _out.WriteLine($"hint: {ClusterFWorkOrderSystem.StoneMiningHint}");

        // The Dispatch Ledger opens the same list.
        var ledger = new CompactDispatchLedger();
        m.Pm.Backpack.DropItem(ledger);
        ledger.OnDoubleClick(m.Pm);
        Assert.True(m.Pm.HasGump<GuildContractLedgerGump>());

        // Reading the pinned book (100.0 Mining) teaches stone mining and opens them.
        var book = new StoneMiningBook();
        m.Pm.Backpack.DropItem(book);
        book.OnDoubleClick(m.Pm);
        Assert.True(m.Pm.StoneMining);
        Assert.True(book.Deleted);

        var after = AvailableTexts(m.Pm);
        Assert.Contains(after, t => t == "Granite Haul");
        Assert.Contains(after, t => t == "Valorite Stone Tribute");
        Assert.Contains(after, t => t == "Dull Copper Stone Delivery  [Locked]");
        Assert.DoesNotContain(after, t => t.Contains("Mining For Quality Stone"));
        Assert.True(Order("mining.granite_haul_s").IsEligible(m.Pm, m.Data));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AHeldStoneContractStaysFinishable()
    {
        using var m = new Miner();
        var def = Order("mining.granite_haul_s");

        // Taken before the gate (or by a character who has since lost the knowledge): held, not eligible now.
        var entry = ClusterFWorkOrderSystem.Accept(m.Pm, m.Guild, def);
        Assert.False(def.IsEligible(m.Pm, m.Data));

        for (var i = 0; i < 50; i++)
        {
            m.Pm.Backpack.DropItem(new Granite());
        }

        Assert.True(def.RequirementsMet(m.Pm, entry));
        var standing = m.Guild.GetReputation("mining");
        Assert.True(GuildContractLedgerGump.TurnIn(m.Pm, m.Guild, "mining", entry, true));
        Assert.Equal(0, m.Pm.Backpack.GetAmount(typeof(Granite)));
        Assert.Equal(standing + def.StandingReward, m.Guild.GetReputation("mining"));
        Assert.Equal(def.VoucherReward, m.Vouchers);
        Assert.DoesNotContain(m.Guild.ActiveWorkOrders, e => e == entry);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheDashboardSellsTheBookAtTheRankAndPriceAndNotOnceLearned()
    {
        Assert.Equal(5000, MinersCompactLiaisonGump.StoneBookStanding);
        Assert.Equal(50, MinersCompactLiaisonGump.StoneBookVouchers);
        Assert.Equal("Journeyman", MinersCompactLiaisonGump.GetRankName(MinersCompactLiaisonGump.StoneBookStanding));

        using var m = new Miner(standing: 4999, vouchers: 60, mining: 99.0);

        var dash = Labels(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.MemberDashboard));
        Assert.Contains("Learn Stone Mining", dash);

        var page = Labels(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.StoneMiningBook));
        _out.WriteLine(string.Join(" | ", page));
        Assert.Contains("Rank: Journeyman (need 1 more Standing)", page);
        Assert.Contains("Mining Vouchers 60 / 50", page);
        Assert.Contains(page, l => l.Contains("Reading it needs 100.0 Mining") && l.Contains("99.0"));

        // Below the rank: refused, nothing taken.
        Assert.False(MinersCompactLiaisonGump.TryBuyStoneMiningBook(m.Pm, m.Guild, out var refused));
        Assert.Contains("Journeyman", refused);
        Assert.Equal(60, m.Vouchers);
        Assert.Null(m.Pm.Backpack.FindItemByType<StoneMiningBook>());

        // At the rank: sold for 50 vouchers, the pinned book (the 100.0 rule applies on reading).
        m.Guild.GuildReputation["mining"] = 5000;
        Assert.True(MinersCompactLiaisonGump.TryBuyStoneMiningBook(m.Pm, m.Guild, out var sold));
        _out.WriteLine(sold);
        Assert.Equal(10, m.Vouchers);
        var book = Assert.IsType<StoneMiningBook>(m.Pm.Backpack.FindItemByType<StoneMiningBook>());
        book.OnDoubleClick(m.Pm);
        Assert.False(m.Pm.StoneMining); // 99.0 Mining: not yet
        Assert.False(book.Deleted);

        // Learned: not offered, and refused.
        m.Pm.Skills.Mining.Base = 100.0;
        book.OnDoubleClick(m.Pm);
        Assert.True(m.Pm.StoneMining);

        dash = Labels(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.MemberDashboard));
        Assert.DoesNotContain("Learn Stone Mining", dash);
        Assert.Contains("Stone mining: learned", dash);

        m.Guild.GuildCurrency["mining"] = 100;
        Assert.False(MinersCompactLiaisonGump.TryBuyStoneMiningBook(m.Pm, m.Guild, out var again));
        Assert.Contains("already learned", again);
        Assert.Equal(100, m.Vouchers);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AMiningQuotaCountsOnlyOreMinedAfterItWasTaken()
    {
        // One amount per metal, titles that say mine, and the Dull Copper quota at the skill that can mine it.
        var quotas = ClusterFWorkOrderSystem.GetForGuild("mining").Where(ClusterFWorkOrderSystem.HasMiningQuota).ToList();
        Assert.Equal(new[] { "mining.iron_ore_s", "mining.ore_refinement_s" }, quotas.Select(d => d.Key).OrderBy(k => k));
        Assert.Equal(quotas.Count, quotas.Select(d => d.Requirements.Single().ItemType).Distinct().Count());
        Assert.All(quotas, d => Assert.StartsWith("Mine ", d.Title));
        Assert.All(quotas, d => Assert.Contains("Mine ", d.Description));
        var iron = Order("mining.iron_ore_s");
        var dc = Order("mining.ore_refinement_s");
        Assert.Equal((typeof(IronOre), 100), (iron.Requirements.Single().ItemType, iron.Requirements.Single().Amount));
        Assert.Equal((75, 4, 60), (iron.StandingReward, iron.VoucherReward, iron.GoldReward)); // the Raw Ore Sample's pay
        Assert.Equal((typeof(DullCopperOre), 50), (dc.Requirements.Single().ItemType, dc.Requirements.Single().Amount));
        Assert.Equal((65.0, "DullCopper"), (dc.MinSkill, dc.RequiredDiscovery));
        Assert.Empty(iron.ItemCosts());

        using var m = new Miner();

        // Mined before the contract existed: never counts.
        Mine(m.Pm, new IronOre(70));
        var entry = ClusterFWorkOrderSystem.Accept(m.Pm, m.Guild, iron);
        Assert.Equal(0, ClusterFWorkOrderSystem.MinedSince(entry, iron.Requirements[0]));

        // Bought or picked up: in the pack and the bank, never counts.
        m.Pm.Backpack.DropItem(new IronOre(200));
        m.Pm.BankBox.DropItem(new IronOre(200));
        Assert.False(iron.RequirementsMet(m.Pm, entry));

        // Another ore mined does not count toward iron.
        Mine(m.Pm, new ShadowIronOre(30));
        Assert.Equal(0, ClusterFWorkOrderSystem.MinedSince(entry, iron.Requirements[0]));

        Mine(m.Pm, new IronOre(60));
        Assert.Equal(60, ClusterFWorkOrderSystem.MinedSince(entry, iron.Requirements[0]));
        Assert.False(iron.RequirementsMet(m.Pm, entry));
        Assert.False(GuildContractLedgerGump.TurnIn(m.Pm, m.Guild, "mining", entry, true));

        Mine(m.Pm, new IronOre(40));
        Assert.True(iron.RequirementsMet(m.Pm, entry));

        var carried = m.Pm.Backpack.GetAmount(typeof(IronOre)) + m.Pm.BankBox.GetAmount(typeof(IronOre));
        Assert.True(GuildContractLedgerGump.TurnIn(m.Pm, m.Guild, "mining", entry, true));
        Assert.Equal(carried, m.Pm.Backpack.GetAmount(typeof(IronOre)) + m.Pm.BankBox.GetAmount(typeof(IronOre))); // nothing taken
        Assert.Equal(4, m.Vouchers);

        // The Dull Copper quota counts dull copper only.
        var second = ClusterFWorkOrderSystem.Accept(m.Pm, m.Guild, dc);
        Mine(m.Pm, new IronOre(50));
        Assert.False(dc.RequirementsMet(m.Pm, second));
        Mine(m.Pm, new DullCopperOre(50));
        Assert.True(dc.RequirementsMet(m.Pm, second));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AContractTakenBeforeQuotasMayBeFinishedEitherWay()
    {
        using var m = new Miner();
        var iron = Order("mining.iron_ore_s");

        WorkOrderEntry Version2(string key)
        {
            // As the cc-P414 build wrote it (WorkOrderEntry.Serialize, version 2).
            var buffer = new byte[4096];
            var w = new BufferWriter(buffer, true);
            w.Write(2);
            w.Write(key);
            w.Write("mining");
            w.Write(DateTime.UtcNow.AddDays(-1));
            w.Write(false); // not completed
            w.Write(0);     // no taming progress
            w.Write(false); // no Clean Up baseline
            w.Flush();
            return new WorkOrderEntry(new BufferReader(buffer));
        }

        // The old way: its 100 iron ore turned in and taken.
        var old = Version2(iron.Key);
        Assert.True(old.FromBeforeQuotas);
        m.Guild.ActiveWorkOrders.Add(old);
        Assert.False(iron.RequirementsMet(m.Pm, old));
        m.Pm.Backpack.DropItem(new IronOre(130));
        Assert.True(iron.RequirementsMet(m.Pm, old));
        Assert.True(GuildContractLedgerGump.TurnIn(m.Pm, m.Guild, "mining", old, true));
        Assert.Equal(30, m.Pm.Backpack.GetAmount(typeof(IronOre)));

        // Or by mining: nothing taken.
        var other = Version2(iron.Key);
        m.Guild.ActiveWorkOrders.Add(other);
        Mine(m.Pm, new IronOre(100));
        var carried = m.Pm.Backpack.GetAmount(typeof(IronOre));
        Assert.True(iron.RequirementsMet(m.Pm, other));

        // The flag and the count survive a save (version 3).
        var buffer3 = new byte[4096];
        var w3 = new BufferWriter(buffer3, true);
        other.Serialize(w3);
        w3.Flush();
        var back = new WorkOrderEntry(new BufferReader(buffer3));
        Assert.True(back.FromBeforeQuotas);
        Assert.Equal(100, ClusterFWorkOrderSystem.MinedSince(back, iron.Requirements[0]));

        Assert.True(GuildContractLedgerGump.TurnIn(m.Pm, m.Guild, "mining", other, true));
        Assert.Equal(carried, m.Pm.Backpack.GetAmount(typeof(IronOre)));

        // A new contract is not from before quotas: carried ore never finishes it.
        var fresh = ClusterFWorkOrderSystem.Accept(m.Pm, m.Guild, iron);
        Assert.False(fresh.FromBeforeQuotas);
        m.Pm.Backpack.DropItem(new IronOre(500));
        Assert.False(iron.RequirementsMet(m.Pm, fresh));
    }

    // ---------------------------------------------------------------- 6

    private static readonly string TierTwoKey = "legacy.jacobs_reinforced_pickaxe";

    private static void ReadyTierTwo(Miner m, int gold)
    {
        ClusterFRestorationRegistry.Unlock(m.Account, TierTwoKey, "test");
        m.Pm.Backpack.DropItem(new IronIngot(100));
        m.Pm.Backpack.DropItem(new DullCopperIngot(15));
        if (gold > 0)
        {
            m.Pm.Backpack.DropItem(new Gold(gold));
        }
    }

    [Fact]
    public void GoldStandsInForMissingVouchersOnARestoration()
    {
        Assert.Equal(100, MinersCompactLiaisonGump.GoldPerMissingVoucher);
        var price = MinersCompactLiaisonGump.GetRestoreCost(2); // 4V + 100 iron + 15 dull copper + 1,000gp
        Assert.Equal((4, 1000), (price.Vouchers, price.Gold));

        // Enough vouchers: as today.
        using (var m = new Miner(vouchers: 6))
        {
            ReadyTierTwo(m, 1000);
            Press(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.Restoration), m.Ns, 61);
            Assert.Equal(2, m.Vouchers);
            Assert.Equal(0, m.Pm.Backpack.GetAmount(typeof(Gold)));
            Assert.NotNull(m.Pm.Backpack.FindItemByType<JacobsReinforcedPickaxe>());
            Assert.Equal((4, 1000, 0), (MinersCompactLiaisonGump.GetRestoreCostCoveringVouchers(2, 6).Vouchers,
                MinersCompactLiaisonGump.GetRestoreCostCoveringVouchers(2, 6).Gold,
                MinersCompactLiaisonGump.GetRestoreCostCoveringVouchers(2, 6).Missing));
        }

        // Short by 3: the 1 voucher held, the same materials, and 1,000 + 3 x 100 gold. Both prices are shown first.
        using (var m = new Miner(vouchers: 1))
        {
            ReadyTierTwo(m, 1350);

            var restore = Labels(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.Restoration));
            Assert.Contains(restore, l => l.Contains("Short 3 vouchers") && l.Contains("300 gold"));

            var page = Labels(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.RestoreWithGold));
            _out.WriteLine(string.Join(" | ", page));
            Assert.Contains(page, l => l.StartsWith("With vouchers: 4V") && l.Contains("1,000gp"));
            Assert.Contains(page, l => l.Contains("3 missing vouchers x 100 = 300 gold more"));
            Assert.Contains("Mining Vouchers 1 / 1", page);
            Assert.Contains(page, l => l.StartsWith("Gold 1,350 / 1,300"));

            // The plain restore still refuses: the vouchers are short.
            Press(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.Restoration), m.Ns, 61);
            Assert.Null(m.Pm.Backpack.FindItemByType<JacobsReinforcedPickaxe>());
            Assert.Equal(1, m.Vouchers);

            Press(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.RestoreWithGold), m.Ns, 82);
            Assert.NotNull(m.Pm.Backpack.FindItemByType<JacobsReinforcedPickaxe>());
            Assert.Equal(0, m.Vouchers);
            Assert.Equal(50, m.Pm.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(0, m.Pm.Backpack.GetAmount(typeof(IronIngot)));
            Assert.Equal(0, m.Pm.Backpack.GetAmount(typeof(DullCopperIngot)));
        }

        // Short by 4 with only 1,000 gold: refused, the whole shortfall named, nothing taken.
        using (var m = new Miner(vouchers: 0))
        {
            ReadyTierTwo(m, 1000);
            var mark = Mark(m.Ns);
            Press(new MinersCompactLiaisonGump(m.Pm, MinersCompactLiaisonGump.View.RestoreWithGold), m.Ns, 82);

            Assert.True(SentText(m.Ns, mark,
                "costs 1,400 gold: the 1,000 base plus 4 missing vouchers x 100 = 400. You have 1,000 in your pack and bank: 400 short."));
            Assert.Null(m.Pm.Backpack.FindItemByType<JacobsReinforcedPickaxe>());
            Assert.Equal(1000, m.Pm.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(100, m.Pm.Backpack.GetAmount(typeof(IronIngot)));
        }
    }
}
