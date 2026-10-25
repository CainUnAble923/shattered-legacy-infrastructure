// BulkOrderGuildPayVerification.cs
//
// cc-P168 (Chase, 2026-10-18 13:27): a deed shows a member of its guild what handing it in pays them, in its gump
// (BulkOrderDeedGumpLines) and its tooltip (BOD-properties-guild-pay.patch), from the deed's one quote, which the turn-in
// pays (BulkOrderGuildPay.Quote). Notes: shard-migration notes/cc-P168-hammer-t3-craft-order.md, part 3.
//
// Facts, with the live random source (the gold is a roll; the quote makes shown and paid one roll):
//   1. A Society of Smiths member, small and large smith deeds: "Always bank" shows the Seals and standing the payout then
//      banks; "Always cash out" shows the gold and standing it then pays; "Ask each time" shows both.
//   2. A non-member: the 2016 points line as before, no tooltip line; the points shown are the points banked, and the
//      bank gold is the quote's.
//   3. A Tailors' Circle member with a tailor deed: the points, and a gold line whose gold the guild master's turn-in
//      puts in the bank.
//   4. A Society member holding a tailor deed (a member of a different guild only) sees what a non-member sees.
//   5. The tooltip carries the member's line (the patch's call reaches BulkOrderGuildPay).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BulkOrderGuildPayVerification
{
    private readonly ITestOutputHelper _out;

    public BulkOrderGuildPayVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private static readonly Point3D Spot = new(1540, 1540, 0);

    private sealed class Member : IDisposable
    {
        public readonly Account Account = new($"p168g{Guid.NewGuid():N}"[..16], "p168-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Npcs = new();

        public Member(params string[] guilds)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Cap = 200.0;
            Pm.Skills.Blacksmith.Base = 100.0; // the Society's large-order skill
            Pm.Skills.Tailoring.Cap = 200.0;
            Pm.Skills.Tailoring.Base = 100.0;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            foreach (var guild in guilds)
            {
                Guild.JoinedGuilds.Add(guild);
            }
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);

        public int Seals => ClusterFAccountPersistence.GetGuild(Pm)?.GetCurrency("smithing") ?? 0;

        public int Standing => ClusterFAccountPersistence.GetGuild(Pm)?.GetReputation("smithing") ?? 0;

        public int Gold => Pm.Backpack.GetAmount(typeof(Gold)) + Pm.Backpack.Items.OfType<BankCheck>().Sum(c => c.Worth);

        // Banker.Deposit puts gold in the bank as gold piles and, past a pile's room, bank checks.
        public int BankGold => Pm.BankBox.GetAmount(typeof(Gold)) + Pm.BankBox.Items.OfType<BankCheck>().Sum(c => c.Worth);

        public T Near<T>(T npc) where T : Mobile
        {
            npc.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            Npcs.Add(npc);
            return npc;
        }

        public T Put<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Npcs.ForEach(n => n.Delete());
            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static SmallSmithBOD SmallSmith() =>
        new(20, 20, typeof(PlateChest), 1025141, 0x1415, true, BulkMaterialType.Verite);

    private static LargeSmithBOD LargeSmith()
    {
        var deed = new LargeSmithBOD(15, false, BulkMaterialType.Copper, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        foreach (var entry in deed.Entries)
        {
            entry.Amount = deed.AmountMax;
        }

        return deed;
    }

    private static BaseBOD SmithDeed(bool large) => large ? LargeSmith() : SmallSmith();

    private static SmallTailorBOD SmallTailor() => new(10, 10, typeof(Shirt), 1025399, 0x1517, false, BulkMaterialType.None);

    private static List<string> TooltipLines(BaseBOD deed) => BulkOrderGuildPay.PropertyLines(deed);

    // The text is in the deed's property list (an Add(string) argument goes out as UTF-16 LE).
    private static bool InTooltip(BaseBOD deed, string text)
    {
        var opl = new ObjectPropertyList(deed);
        deed.GetProperties(opl);
        opl.Terminate();
        var needle = Encoding.Unicode.GetBytes(text);
        return opl.Buffer.AsSpan().IndexOf(needle) >= 0;
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BankShowsTheSealsAndStandingThePayoutBanks(bool large)
    {
        using var m = new Member("smithing");
        var deed = m.Put(SmithDeed(large));

        var (pay, setting) = BulkOrderGuildPay.SmithLines(m.Pm, deed);
        var q = BulkOrderGuildPay.Quote(deed);
        _out.WriteLine($"{(large ? "large" : "small")}, bank: {pay} | {setting}");
        Assert.Equal($"Society pays you: +{q.Standing:N0} standing, {q.Seals:N0} Smithing Seals", pay);
        Assert.Equal("Completed orders: Always bank", setting);
        Assert.Equal(new List<string> { pay, setting }, BulkOrderDeedGumpLines.TextLines(deed));
        Assert.Equal(new List<string> { pay, setting }, TooltipLines(deed));

        Assert.True(ClusterFSmithBODPayout.Pay(m.Pm, deed, false, null));
        _out.WriteLine($"paid: seals {m.Seals}, standing {m.Standing}");
        Assert.Equal(q.Seals, m.Seals);
        Assert.Equal(q.Standing, m.Standing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CashOutShowsTheGoldAndStandingThePayoutPays(bool large)
    {
        using var m = new Member("smithing");
        ClusterFSmithBODPayout.SetMode(m.Pm, SmithTurnInMode.CashOut);
        var deed = m.Put(SmithDeed(large));

        var (pay, setting) = BulkOrderGuildPay.SmithLines(m.Pm, deed);
        var q = BulkOrderGuildPay.Quote(deed);
        _out.WriteLine($"{(large ? "large" : "small")}, cash out: {pay} | {setting}");
        Assert.Equal($"Society pays you: +{q.Standing:N0} standing, {q.CashOutGold:N0} gold (cash out)", pay);
        Assert.Equal("Completed orders: Always cash out", setting);

        var gold = m.Gold;
        Assert.True(ClusterFSmithBODPayout.Pay(m.Pm, deed, true, null));
        _out.WriteLine($"paid: gold +{m.Gold - gold}, standing {m.Standing}, seals {m.Seals}");
        Assert.Equal(q.CashOutGold, m.Gold - gold);
        Assert.Equal(q.Standing, m.Standing);
        Assert.Equal(0, m.Seals);
    }

    [Fact]
    public void AskShowsBoth()
    {
        using var m = new Member("smithing");
        ClusterFSmithBODPayout.SetMode(m.Pm, SmithTurnInMode.Ask);
        var deed = m.Put(SmallSmith());

        var (pay, _) = BulkOrderGuildPay.SmithLines(m.Pm, deed);
        var q = BulkOrderGuildPay.Quote(deed);
        _out.WriteLine(pay);
        Assert.Equal($"Society pays you: +{q.Standing:N0} standing, {q.Seals:N0} Seals or {q.CashOutGold:N0} gold", pay);
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANonMemberKeepsThePointsLine(bool large)
    {
        using var m = new Member();
        var deed = m.Put(SmithDeed(large));
        var smith = m.Near(new Blacksmith());
        BulkOrderSystem.GetContext(m.Pm).PointsMode = PointsMode.Automatic; // bank the points at once

        BulkOrderSystem.ComputePoints(deed, out var points, out var banked);
        var lines = BulkOrderDeedGumpLines.TextLines(deed);
        _out.WriteLine($"{(large ? "large" : "small")}, non-member: {string.Join(" | ", lines)}");
        Assert.Equal(new List<string> { $"Worth {points} turn in points and {banked:0.000000} bank points" }, lines);
        Assert.Equal(48, BulkOrderDeedGumpLines.Height(deed));
        Assert.Empty(TooltipLines(deed));

        var q = BulkOrderGuildPay.Quote(deed);
        var bank = m.BankGold;
        m.Pm.NextBODTurnInTime = DateTime.MinValue;
        Assert.True(smith.OnDragDrop(m.Pm, deed));
        _out.WriteLine($"paid: bank +{m.BankGold - bank} (quote {q.StockGold}), points {BulkOrderSystem.GetPoints(m.Pm, BODType.Smith)}");
        Assert.True(deed.Deleted);
        Assert.Equal(q.StockGold, m.BankGold - bank);
        Assert.Equal(banked, BulkOrderSystem.GetPoints(m.Pm, BODType.Smith));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ATailorsCircleMemberSeesTheGoldTheirGuildmasterBanks()
    {
        using var m = new Member(TailorGuildmaster.GuildKey);
        var deed = m.Put(SmallTailor());
        var master = m.Near(new TailorGuildmaster());
        BulkOrderSystem.GetContext(m.Pm).PointsMode = PointsMode.Automatic;

        var q = BulkOrderGuildPay.Quote(deed);
        var lines = BulkOrderDeedGumpLines.TextLines(deed);
        _out.WriteLine($"tailor member: {string.Join(" | ", lines)}");
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("Worth ", lines[0]);
        Assert.Equal($"Tailors' Circle pays you: {q.StockGold:N0} gold to the bank (no guild currency or standing yet)", lines[1]);
        Assert.Equal(72, BulkOrderDeedGumpLines.Height(deed));
        Assert.Equal(new List<string> { lines[1] }, TooltipLines(deed));

        var bank = m.BankGold;
        m.Pm.NextBODTurnInTime = DateTime.MinValue;
        Assert.True(master.OnDragDrop(m.Pm, deed));
        _out.WriteLine($"paid: bank +{m.BankGold - bank}");
        Assert.True(deed.Deleted);
        Assert.Equal(q.StockGold, m.BankGold - bank);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AMemberOfADifferentGuildSeesWhatANonMemberSees()
    {
        using var m = new Member("smithing");
        var deed = m.Put(SmallTailor());

        var lines = BulkOrderDeedGumpLines.TextLines(deed);
        _out.WriteLine($"smith member, tailor deed: {string.Join(" | ", lines)}");
        Assert.Single(lines);
        Assert.StartsWith("Worth ", lines[0]);
        Assert.Equal(48, BulkOrderDeedGumpLines.Height(deed));
        Assert.Empty(TooltipLines(deed));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheTooltipCarriesTheMembersLine()
    {
        using var m = new Member("smithing");
        var deed = m.Put(SmallSmith());
        var (pay, _) = BulkOrderGuildPay.SmithLines(m.Pm, deed);

        Assert.True(InTooltip(deed, pay));

        using var other = new Member();
        var theirs = other.Put(SmallSmith());
        Assert.False(InTooltip(theirs, "Society pays you"));
    }
}
