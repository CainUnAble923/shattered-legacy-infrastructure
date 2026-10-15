// cc-P122: Bulk orders part 1, OSI's 2016 rules for smith and tailor deeds (Chase's option A and "guild masters now",
// 2026-10-15). customizations/Services/BulkOrders/ and three patches (BaseVendor-bulk-orders-2016, BOD-gumps-points-mode,
// BODType-all-crafts).
//
// Facts:
//   1. Offers: up to three at once per craft (one waiting and two cached), the fourth waits; one comes back 6 hours after
//      the last; smith and tailor are counted apart; Bulk Order Info at a Tailor shows the accept gump.
//   2. The right skill: a large deed is offered only from 70.1 (ServUO's roll), never below; no skill, no offer entry.
//   3. Small fill and large combine, with deeds the 2016 vendors issue.
//   4. A non-member's turn-in at a town vendor (Blacksmith and Tailor): gold to the bank (a small deed: half the shop price
//      of what it asks for), points pending, the bank-or-claim gump; a second turn-in waits until the points are claimed.
//   5. Points: banked at 2% (small) and 20% (large) of their value; spent in the rewards gump; a placeholder is not sold;
//      Disabled mode claims at once, Automatic banks at once; the deed gump's button cycles the mode.
//   6. The bribe: priced at the deed's worth times the vendor's multiplier, paid from the bank by handing the deed over,
//      the deed improved; a partly filled deed is refused; the multiplier rises.
//   7. The book stores both crafts' deeds and its filter picks by craft.
//   8. The Tailors' Circle guild master gives and takes tailor deeds for members only.
//   9. The save: players' offers, points and modes and vendors' bribe state round-trip; a save folder without the file
//      (a world saved before cc-P122) loads nothing. Deeds and books keep pinned's types and so their saved shape;
//      the proof that a pre-change world's deeds load is the test-world boot and its census line (notes).
//  10. The reward lists are ServUO's (40 smith, 25 tailor) with the placeholders named.
//
// Host notes (notes/port-recipe.md): a test player with no account is Young; the clock is Arm()ed before anything with a
// timer; offers are backdated through BODEntry.LastBulkOrder rather than advancing the wheel six hours.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.ContextMenus;
using Server.Collections;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BulkOrderSystemVerification
{
    private readonly ITestOutputHelper _out;

    public BulkOrderSystemVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems(); // deed generation reads the craft systems
        ClusterFGuildSystem.EnsureRegistered();

        if (!_bookEntries)
        {
            BOBEntries.Configure(); // the book's entry serials; the host runs no Configure for it
            _bookEntries = true;
        }
    }

    private static bool _bookEntries;

    private static readonly Point3D Spot = new(1530, 1530, 0);

    private sealed class Crafter : IDisposable
    {
        public readonly Account Account = new($"p122{Guid.NewGuid():N}"[..16], "p122-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Npcs = new();

        public Crafter(double smith = 0, double tailor = 0)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Cap = 200.0;
            Pm.Skills.Blacksmith.Base = smith;
            Pm.Skills.Tailoring.Cap = 200.0;
            Pm.Skills.Tailoring.Base = tailor;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public T Near<T>(T npc, int dx = 1) where T : Mobile
        {
            npc.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
            Npcs.Add(npc);
            return npc;
        }

        public BODContext Context => BulkOrderSystem.GetContext(Pm);

        public int BankGold => Pm.BankBox.GetAmount(typeof(Gold));

        public void Ready() => Pm.NextBODTurnInTime = DateTime.MinValue;

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

    private static SmallSmithBOD CompleteSmallIron() =>
        new(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);

    private static SmallTailorBOD CompleteSmallTailor() =>
        new(10, 10, typeof(Shirt), 1025399, 0x1517, false, BulkMaterialType.None);

    private static ContextMenuEntry[] Entries(Mobile target, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            target.GetContextMenuEntries(from, ref list);
            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    // ---------------------------------------------------------------- 1. Offers

    [Fact]
    public void ThreeOffersAtOnceThenOneEverySixHoursPerCraft()
    {
        using var c = new Crafter(smith: 80, tailor: 80);

        Assert.True(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith));
        Assert.True(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith));
        Assert.True(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith));
        Assert.False(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith)); // the fourth waits
        Assert.False(BulkOrderSystem.HasBulkOrder(c.Pm, BODType.Smith));

        var wait = BulkOrderSystem.GetNextBulkOrder(BODType.Smith, c.Pm);
        _out.WriteLine($"after three smith offers: next in {wait}");
        Assert.InRange(wait.TotalHours, 5.9, 6.0);

        // Tailoring has its own three.
        Assert.True(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Tailor));

        // Six hours after the last, one more (and only one).
        var entry = c.Context.Entries[BODType.Smith];
        entry.LastBulkOrder -= TimeSpan.FromHours(6.1);
        Assert.True(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith));
        Assert.False(BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith));
    }

    [Fact]
    public void BulkOrderInfoAtATailorShowsTheAcceptGumpAndThenTheWait()
    {
        using var c = new Crafter(tailor: 60);
        var tailor = c.Near(new Tailor());

        var info = Assert.Single(Entries(tailor, c.Pm), e => e.Number == 3006152); // Bulk Order Info
        Assert.Contains(Entries(tailor, c.Pm), e => e is BulkOrderVendors.BribeEntry);
        Assert.Contains(Entries(tailor, c.Pm), e => e is BulkOrderVendors.ClaimRewardsEntry);

        for (var i = 0; i < 3; i++)
        {
            info.OnClick(c.Pm, tailor);
            var gump = c.Pm.FindGump<SmallBODAcceptGump>() as BaseGump ?? c.Pm.FindGump<LargeBODAcceptGump>();
            Assert.NotNull(gump);
            c.Pm.CloseGump<SmallBODAcceptGump>();
            c.Pm.CloseGump<LargeBODAcceptGump>();
        }

        info.OnClick(c.Pm, tailor); // the fourth: no gump, the wait is said instead
        Assert.Null(c.Pm.FindGump<SmallBODAcceptGump>());
        Assert.Null(c.Pm.FindGump<LargeBODAcceptGump>());
    }

    // ---------------------------------------------------------------- 2. The right skill

    [Fact]
    public void ALargeOfferNeeds70Point1AndNoSkillGetsNoOfferEntry()
    {
        using var low = new Crafter(smith: 70.0);
        using var high = new Crafter(smith: 120.0);

        var lowKinds = Enumerable.Range(0, 200).Select(_ => BulkOrderSystem.CreateBulkOrder(low.Pm, BODType.Smith, true)).ToList();
        var highKinds = Enumerable.Range(0, 200).Select(_ => BulkOrderSystem.CreateBulkOrder(high.Pm, BODType.Smith, true)).ToList();

        _out.WriteLine($"at 70.0: {lowKinds.Count(k => k is LargeSmithBOD)} large of 200; at 120: {highKinds.Count(k => k is LargeSmithBOD)}");
        Assert.DoesNotContain(lowKinds, k => k is LargeSmithBOD);
        Assert.Contains(highKinds, k => k is LargeSmithBOD); // (120 - 40) / 300 = 27% a draw
        lowKinds.Concat(highKinds).ToList().ForEach(k => k?.Delete());

        using var none = new Crafter(smith: 0);
        var smith = none.Near(new Blacksmith());
        Assert.DoesNotContain(Entries(smith, none.Pm), e => e.Number == 3006152);
    }

    // ---------------------------------------------------------------- 3. Fill and combine

    [Fact]
    public void ASmallDeedFillsAndALargeDeedCombinesIt()
    {
        using var c = new Crafter(smith: 100);

        var small = new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);
        c.Pm.Backpack.DropItem(small);

        for (var i = 0; i < 10; i++)
        {
            var sword = new Longsword();
            c.Pm.Backpack.DropItem(sword);
            small.EndCombine(c.Pm, sword);
        }

        Assert.Equal(10, small.AmountCur);
        Assert.True(small.Complete);

        var large = new LargeSmithBOD(10, false, BulkMaterialType.None, null);
        large.Entries = LargeBulkEntry.ConvertEntries(large, LargeBulkEntry.LargeRing);
        c.Pm.Backpack.DropItem(large);

        var first = large.Entries[0];
        var fitting = new SmallSmithBOD(10, 10, first.Details.Type, first.Details.Number, first.Details.Graphic, false, BulkMaterialType.None);
        c.Pm.Backpack.DropItem(fitting);
        large.EndCombine(c.Pm, fitting);

        Assert.Equal(10, first.Amount);
        Assert.True(fitting.Deleted);
    }

    // ---------------------------------------------------------------- 4. The turn-in

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANonMembersTurnInPaysGoldToTheBankAndAsksAboutPoints(bool tailor)
    {
        using var c = new Crafter(smith: 80, tailor: 80);
        BaseVendor vendor = tailor ? c.Near(new Tailor()) : c.Near(new Blacksmith());
        BaseBOD deed = tailor ? CompleteSmallTailor() : CompleteSmallIron();
        c.Pm.Backpack.DropItem(deed);
        c.Ready();

        var type = BulkOrderSystem.GetBODType(deed)!.Value;
        Assert.True(BulkOrderSystem.ComputeGold(((SmallBOD)deed).Type, 10, out var expected));
        var points = BulkOrderSystem.ComputePoints(deed);
        var bank = c.BankGold;

        Assert.True(vendor.OnDragDrop(c.Pm, deed));

        _out.WriteLine($"{type}: +{c.BankGold - bank} gold to the bank (expected {expected}), {points} points pending");
        Assert.True(deed.Deleted);
        Assert.Equal(expected, c.BankGold - bank);
        Assert.Equal(points, c.Context.GetPendingRewardFor(type));
        Assert.NotNull(c.Pm.FindGump<BODConfirmBankPointsGump>());

        // A second turn-in waits until the first's points are claimed.
        var second = tailor ? (BaseBOD)CompleteSmallTailor() : CompleteSmallIron();
        c.Pm.Backpack.DropItem(second);
        c.Ready();
        Assert.False(vendor.OnDragDrop(c.Pm, second));
        Assert.False(second.Deleted);
        second.Delete();
    }

    [Fact]
    public void ASmallDeedPaysHalfTheShopPriceOfWhatItAsksFor()
    {
        // ServUO 2016: quantity * price / 2 (BulkOrderSystem.cs:366-376), the price a shop sells the item for.
        Assert.True(BulkOrderSystem.BuyPrices.TryGetValue(typeof(Longsword), out var price));
        Assert.True(BulkOrderSystem.ComputeGold(typeof(Longsword), 20, out var gold));
        _out.WriteLine($"Longsword sells for {price}; a 20-deed pays {gold}");
        Assert.Equal(55, price); // pinned SBBlacksmith.cs:68 and SBWeaponSmith.cs:64
        Assert.Equal(20 * 55 / 2, gold);

        // A large deed pays pinned's table, as ServUO's 2016 calculator does.
        var large = new LargeSmithBOD(10, false, BulkMaterialType.None, null);
        large.Entries = LargeBulkEntry.ConvertEntries(large, LargeBulkEntry.LargeRing);
        var paid = BulkOrderSystem.ComputeGold(large);
        Assert.InRange(paid, 2700, 3333); // 3,000 +- 10% (ringmail, 10, iron)
        large.Delete();
    }

    // ---------------------------------------------------------------- 5. Points

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void PointsBankAtTwoPercentAndSpendInTheRewardsGump()
    {
        using var c = new Crafter(smith: 80);
        var smith = c.Near(new Blacksmith());
        var deed = new SmallSmithBOD(20, 20, typeof(Longsword), 1025049, 0x0F61, true, BulkMaterialType.Valorite);
        c.Pm.Backpack.DropItem(deed);
        c.Ready();

        var points = BulkOrderSystem.ComputePoints(deed); // 50 + 200 exceptional + 550 Valorite
        Assert.Equal(800, points);

        Assert.True(smith.OnDragDrop(c.Pm, deed));
        var confirm = c.Pm.FindGump<BODConfirmBankPointsGump>();
        Assert.NotNull(confirm);
        Assert.Equal(16.0, confirm.Banked, 6);

        Press(confirm, c.Ns, BODConfirmBankPointsGump.BtnBank);
        Assert.Equal(16.0, BulkOrderSystem.GetPoints(c.Pm, BODType.Smith), 6);
        Assert.True(BulkOrderSystem.CanClaimRewards(c.Pm));

        // Bank a lot more, then buy a sturdy shovel (10 points) from the banked list.
        BulkOrderSystem.SetPoints(c.Pm, BODType.Smith, 100);
        var rewards = new BODRewardsGump(smith, c.Pm, BODType.Smith);
        var shovel = BODRewardCollections.Get(BODType.Smith).ToList().FindIndex(r => r.Tooltip == 1157084);
        rewards.OnConfirmed(shovel);

        Assert.NotNull(c.Pm.Backpack.FindItemByType<SturdyShovel>());
        Assert.Equal(106.0, BulkOrderSystem.GetPoints(c.Pm, BODType.Smith), 6);

        // A placeholder is listed but not sold.
        var title = BODRewardCollections.Get(BODType.Smith).ToList().FindIndex(r => r.IsPlaceholder);
        new BODRewardsGump(smith, c.Pm, BODType.Smith).OnConfirmed(title);
        Assert.Equal(106.0, BulkOrderSystem.GetPoints(c.Pm, BODType.Smith), 6);
    }

    [Fact]
    public void ClaimingATurnInsPointsBuysOneRewardAndClearsThem()
    {
        using var c = new Crafter(tailor: 80);
        var tailor = c.Near(new Tailor());
        c.Context.PointsMode = PointsMode.Disabled; // claim at once
        var deed = CompleteSmallTailor();
        c.Pm.Backpack.DropItem(deed);
        c.Ready();

        Assert.True(tailor.OnDragDrop(c.Pm, deed));
        var gump = c.Pm.FindGump<BODRewardsGump>();
        Assert.NotNull(gump);
        Assert.False(gump.UsingBanked);

        var kit = BODRewardCollections.Get(BODType.Tailor).ToList().FindIndex(r => r.Tooltip == 1157219); // sewing kit, 10
        gump.OnConfirmed(kit);
        Assert.NotNull(c.Pm.Backpack.FindItemByType<SewingKit>());
        Assert.Equal(0, c.Context.GetPendingRewardFor(BODType.Tailor));
        Assert.True(BulkOrderSystem.CanClaimRewards(c.Pm));
    }

    [Fact]
    public void AutomaticBanksAtOnceAndTheDeedGumpCyclesTheMode()
    {
        using var c = new Crafter(smith: 80);
        var smith = c.Near(new Blacksmith());
        c.Context.PointsMode = PointsMode.Automatic;
        var deed = CompleteSmallIron();
        c.Pm.Backpack.DropItem(deed);
        c.Ready();

        var points = BulkOrderSystem.ComputePoints(deed);
        Assert.True(smith.OnDragDrop(c.Pm, deed));
        Assert.Equal(points * 0.02, BulkOrderSystem.GetPoints(c.Pm, BODType.Smith), 6);
        Assert.True(BulkOrderSystem.CanClaimRewards(c.Pm));

        // The small deed gump's points-mode button: Automatic -> Enabled -> Disabled -> Automatic.
        var shown = CompleteSmallIron();
        c.Pm.Backpack.DropItem(shown);
        Assert.Equal(48, BulkOrderDeedGumpLines.Height(shown));

        var order = new List<PointsMode>();
        for (var i = 0; i < 3; i++)
        {
            var g = new SmallBODGump(shown);
            c.Pm.SendGump(g);
            Press(g, c.Ns, BulkOrderDeedGumpLines.ToggleButton);
            order.Add(c.Context.PointsMode);
        }

        Assert.Equal(new[] { PointsMode.Enabled, PointsMode.Disabled, PointsMode.Automatic }, order);
    }

    // ---------------------------------------------------------------- 6. The bribe

    [Fact]
    public void ABribeIsPricedPaidFromTheBankAndImprovesTheDeed()
    {
        using var c = new Crafter(smith: 80);
        var smith = c.Near(new Blacksmith());
        var state = BulkOrderSystem.GetVendorState(smith);
        state.BribeMultiplier = 3;

        var deed = new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);
        c.Pm.Backpack.DropItem(deed);
        c.Pm.BankBox.DropItem(new Gold(5000));

        var price = BulkOrderVendors.OfferBribe(smith, c.Pm, deed);
        Assert.Equal(100 * 3, price); // worth 100 (a 10-deed, no material) times the multiplier

        var before = (deed.AmountMax, deed.RequireExceptional, deed.Material);
        var bank = c.BankGold;
        c.Ready();
        Assert.False(smith.OnDragDrop(c.Pm, deed)); // the deed stays with the player, improved

        _out.WriteLine($"bribed: {before} -> {(deed.AmountMax, deed.RequireExceptional, deed.Material)}, paid {bank - c.BankGold}");
        Assert.Equal(300, bank - c.BankGold);
        Assert.NotEqual(before, (deed.AmountMax, deed.RequireExceptional, deed.Material));
        Assert.Equal(4, state.BribeMultiplier);

        // A partly filled deed is refused.
        var started = new SmallSmithBOD(1, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);
        c.Pm.Backpack.DropItem(started);
        Assert.Equal(-1, BulkOrderVendors.OfferBribe(smith, c.Pm, started));
    }

    // ---------------------------------------------------------------- 7. The book

    [Fact]
    public void TheBookStoresBothCraftsAndFiltersByCraft()
    {
        using var c = new Crafter(smith: 80, tailor: 80);
        var book = new BulkOrderBook();
        c.Pm.Backpack.DropItem(book);

        var smithDeed = CompleteSmallIron();
        var tailorDeed = CompleteSmallTailor();
        c.Pm.Backpack.DropItem(smithDeed);
        c.Pm.Backpack.DropItem(tailorDeed);

        Assert.True(book.OnDragDrop(c.Pm, smithDeed));
        Assert.True(book.OnDragDrop(c.Pm, tailorDeed));
        Assert.Equal(2, book.Entries.Count);
        Assert.Equal(new[] { BODType.Smith, BODType.Tailor }, book.Entries.Select(e => e.DeedType));

        book.Filter.Material = 2; // tailor
        var gump = new BOBGump(c.Pm, book);
        Assert.Equal(new[] { false, true }, book.Entries.Select(gump.CheckFilter));

        var back = book.Entries[1].Reconstruct();
        Assert.IsType<SmallTailorBOD>(back);
        back.Delete();
        book.Delete();
    }

    // ---------------------------------------------------------------- 8. The Tailors' Circle

    [Fact]
    public void TheTailorsCircleGuildmasterServesMembersOnly()
    {
        using var c = new Crafter(tailor: 80);
        var master = c.Near(new TailorGuildmaster());

        Assert.False(master.SupportsBulkOrders(c.Pm));
        Assert.DoesNotContain(Entries(master, c.Pm), e => e.Number == 3006152);

        ClusterFAccountPersistence.GetOrCreateGuild(c.Pm).JoinedGuilds.Add(TailorGuildmaster.GuildKey);
        Assert.True(master.SupportsBulkOrders(c.Pm));
        Assert.True(BulkOrderVendors.TryOffer(master, c.Pm));
        Assert.True(c.Pm.FindGump<SmallBODAcceptGump>() != null || c.Pm.FindGump<LargeBODAcceptGump>() != null);

        var deed = CompleteSmallTailor();
        c.Pm.Backpack.DropItem(deed);
        c.Ready();
        var bank = c.BankGold;
        Assert.True(master.OnDragDrop(c.Pm, deed));
        Assert.True(c.BankGold > bank);

        // It takes no smith deed.
        var smithDeed = CompleteSmallIron();
        c.Pm.Backpack.DropItem(smithDeed);
        c.Context.RemovePending(BODType.Tailor);
        c.Ready();
        Assert.False(master.OnDragDrop(c.Pm, smithDeed));
        smithDeed.Delete();
    }

    // ---------------------------------------------------------------- 9. The save

    private static byte[] SaveStore()
    {
        BulkOrderStore.Configure();
        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        BulkOrderStore.Instance.Serialize(writer);
        writer.Flush();
        return buffer;
    }

    [Fact]
    public void OffersPointsModesAndBribesSurviveSaveAndLoad()
    {
        using var c = new Crafter(smith: 80, tailor: 80);
        var smith = c.Near(new Blacksmith());

        BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith);
        BulkOrderSystem.CanGetBulkOrder(c.Pm, BODType.Smith);
        BulkOrderSystem.SetPoints(c.Pm, BODType.Tailor, 12.5);
        c.Context.AddPending(BODType.Smith, 210);
        c.Context.PointsMode = PointsMode.Automatic;
        var state = BulkOrderSystem.GetVendorState(smith);
        state.BribeMultiplier = 7;
        state.RecentBribes = 2;

        var before = c.Context.Entries[BODType.Smith];
        var (cached, last) = (before.CachedDeeds, before.LastBulkOrder);

        var saved = SaveStore();
        BulkOrderStore.Instance.Deserialize(new BufferReader(saved));

        var after = BulkOrderSystem.GetContext(c.Pm, false);
        Assert.NotNull(after);
        Assert.Equal(PointsMode.Automatic, after.PointsMode);
        Assert.Equal(cached, after.Entries[BODType.Smith].CachedDeeds);
        Assert.Equal(last, after.Entries[BODType.Smith].LastBulkOrder);
        Assert.Equal(210, after.Entries[BODType.Smith].PendingRewardPoints);
        Assert.Equal(12.5, after.Entries[BODType.Tailor].BankedPoints, 6);
        Assert.Equal(8, after.Entries.Count); // all eight crafts, the six of P123 included
        Assert.Equal(7, BulkOrderSystem.VendorStates[smith].BribeMultiplier);
        Assert.Equal(2, BulkOrderSystem.VendorStates[smith].RecentBribes);
    }

    [Fact]
    public void ASaveFolderWithoutTheFileLoadsNothing()
    {
        BulkOrderStore.Configure();
        using var c = new Crafter(smith: 80);
        c.Context.PointsMode = PointsMode.Disabled;

        var dir = Path.Combine(Path.GetTempPath(), $"cc-p122-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            BulkOrderStore.Instance.Deserialize(dir, new Dictionary<ulong, string>());
            Assert.Equal(PointsMode.Disabled, BulkOrderSystem.GetContext(c.Pm, false)?.PointsMode); // nothing read
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ---------------------------------------------------------------- 10. The reward lists

    [Fact]
    public void TheRewardListsAreServUOsWithThePlaceholdersNamed()
    {
        var smith = BODRewardCollections.Get(BODType.Smith);
        var tailor = BODRewardCollections.Get(BODType.Tailor);

        Assert.Equal(40, smith.Count); // ServUO Rewards.cs:321-360
        Assert.Equal(25, tailor.Count); // :673-697

        var placeholders = smith.Concat(tailor).Where(r => r.IsPlaceholder).Select(r => r.Placeholder).ToList();
        _out.WriteLine($"placeholders: {string.Join("; ", placeholders)}");
        Assert.Equal(13, placeholders.Count); // 2 + 3 title deeds, 4 smelters' talismans, 4 harvest maps
        Assert.All(placeholders, p => Assert.Matches("^(BODRewardTitleDeed|SmeltersTalisman|HarvestMap) ", p));

        // Every real reward builds (and is deleted again).
        foreach (var r in smith.Concat(tailor).Where(r => !r.IsPlaceholder))
        {
            var item = r.Construct();
            Assert.NotNull(item);
            item.Delete();
        }

        // Prices rise down the list, as ServUO's do.
        Assert.Equal(smith.Select(r => r.Points).OrderBy(p => p), smith.Select(r => r.Points));
        Assert.Equal(tailor.Select(r => r.Points).OrderBy(p => p), tailor.Select(r => r.Points));
    }
}
