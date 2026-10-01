// GuildResourcesVerification.cs
//
// cc-P22, F-16 and F-11. Notes in shard-migration notes/cc-P22-small-features-1.md.
//
// F-16 (Jacob's Pickaxe restoration, through the Liaison gump's own buttons):
//   a. T1 charges 1 voucher, 25 iron ingots and 100 gold; T2 charges 4 vouchers, 100 iron, 15 dull copper and
//      1,000 gold.
//   b. One short of any of them is refused and takes nothing.
// F-11 (guild upgrades, restorations and work order turn-ins draw from the bank too; GuildResources):
//   1. Half in the pack and half in the bank: the upgrade succeeds and takes the pack's first.
//   2. Short by one across both: refused, and nothing is taken from either.
//   3. Loose ingots are used before a satchel's.
//   4. An equipped, blessed, or locked-container item is never taken for a turn-in.
//   5. A work order turn-in that needs crafted items takes them from the bank when the pack has none.
//   6. The order is one list: pack loose, pack satchels, bank loose, bank satchels.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.MLQuests;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildResourcesVerification
{
    private const string OneSword = "p22.test.longsword1";
    private const string TwoSwords = "p22.test.longswords2";

    private readonly ITestOutputHelper _out;

    public GuildResourcesVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();

        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        ClusterFGuildSystem.EnsureRegistered();

        ClusterFWorkOrderSystem.Register(new WorkOrderDef(
            OneSword, "smithing", "Test: one longsword", "cc-P22 test order", WorkOrderType.CraftedSupply,
            new() { new(typeof(Longsword), 1, "Longswords") }, 0, null, 0, 1, 1, 0));
        ClusterFWorkOrderSystem.Register(new WorkOrderDef(
            TwoSwords, "smithing", "Test: two longswords", "cc-P22 test order", WorkOrderType.CraftedSupply,
            new() { new(typeof(Longsword), 2, "Longswords") }, 0, null, 0, 1, 1, 0));
    }

    // ---------------------------------------------------------------- helpers

    private static bool _startupHooksRun;

    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p22{Guid.NewGuid():N}"[..16], "p22-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1230, 1230, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public Container Pack => Pm.Backpack;
        public BankBox Bank => Pm.BankBox;

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            MLQuestSystem.HandleDeletion(Pm);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static T Put<T>(Container c, T item) where T : Item
    {
        c.DropItem(item);
        return item;
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static int Amount<T>(Container c) where T : Item => c.GetAmount(typeof(T));

    private static void MiningMember(Player p, int vouchers, int standing = 0)
    {
        p.Guild.JoinedGuilds.Add("mining");
        p.Guild.GuildCurrency["mining"] = vouchers;
        p.Guild.GuildReputation["mining"] = standing;
    }

    private static void Restore(Player p, int tier) =>
        Press(new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.Restoration), p.Ns, 59 + tier);

    private static readonly string[] TierKeys =
    [
        "", "legacy.jacobs_pickaxe", "legacy.jacobs_reinforced_pickaxe"
    ];

    // The materials one tier's restoration asks for: vouchers, iron, dull copper, gold.
    private static readonly (int V, int Iron, int Dc, int Gold)[] Price =
    [
        default, (1, 25, 0, 100), (4, 100, 15, 1000)
    ];

    // ---------------------------------------------------------------- F-16 a

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void F16_RestorationChargesTheNewPrice(int tier)
    {
        var price = Price[tier];
        Assert.Equal(price, (MinersCompactLiaisonGump.GetRestoreCost(tier).Vouchers,
            MinersCompactLiaisonGump.GetRestoreCost(tier).Materials[0].Amount,
            tier == 2 ? MinersCompactLiaisonGump.GetRestoreCost(tier).Materials[1].Amount : 0,
            MinersCompactLiaisonGump.GetRestoreCost(tier).Gold));

        using var p = new Player();
        MiningMember(p, price.V + 7);
        ClusterFRestorationRegistry.Unlock(p.Account, TierKeys[tier], "test");

        Put(p.Pack, new IronIngot(price.Iron + 3));
        if (price.Dc > 0)
        {
            Put(p.Pack, new DullCopperIngot(price.Dc + 2));
        }

        Put(p.Pack, new Gold(price.Gold + 11));

        Restore(p, tier);

        Assert.Equal(7, p.Guild.GuildCurrency["mining"]);
        Assert.Equal(3, Amount<IronIngot>(p.Pack));
        Assert.Equal(price.Dc > 0 ? 2 : 0, Amount<DullCopperIngot>(p.Pack));
        Assert.Equal(11, Amount<Gold>(p.Pack));
        Assert.True(ClusterFRestorationRegistry.HasActiveCopy(p.Account, TierKeys[tier]));
        Assert.Single(p.Pack.Items, i => tier == 1 ? i.GetType() == typeof(JacobsPickaxe) : i.GetType() == typeof(JacobsReinforcedPickaxe));
    }

    // ---------------------------------------------------------------- F-16 b

    [Theory]
    [InlineData(1, "V")] [InlineData(1, "Iron")] [InlineData(1, "Gold")]
    [InlineData(2, "V")] [InlineData(2, "Iron")] [InlineData(2, "Dc")] [InlineData(2, "Gold")]
    public void F16_OneShortOfAnythingIsRefusedAndTakesNothing(int tier, string shortOf)
    {
        var price = Price[tier];
        int Less(string what, int n) => what == shortOf ? n - 1 : n;

        using var p = new Player();
        MiningMember(p, Less("V", price.V));
        ClusterFRestorationRegistry.Unlock(p.Account, TierKeys[tier], "test");

        // Split across pack and bank where there is a bank half, so "nothing from either" is tested too.
        var iron = Less("Iron", price.Iron);
        Put(p.Pack, new IronIngot(iron / 2 + 1));
        Put(p.Bank, new IronIngot(iron - (iron / 2 + 1)));
        if (price.Dc > 0)
        {
            Put(p.Bank, new DullCopperIngot(Less("Dc", price.Dc)));
        }

        Put(p.Pack, new Gold(Less("Gold", price.Gold)));

        var before = (p.Guild.GuildCurrency["mining"], Amount<IronIngot>(p.Pack), Amount<IronIngot>(p.Bank),
            Amount<DullCopperIngot>(p.Bank), Amount<Gold>(p.Pack));

        Restore(p, tier);

        Assert.Equal(before, (p.Guild.GuildCurrency["mining"], Amount<IronIngot>(p.Pack), Amount<IronIngot>(p.Bank),
            Amount<DullCopperIngot>(p.Bank), Amount<Gold>(p.Pack)));
        Assert.False(ClusterFRestorationRegistry.HasActiveCopy(p.Account, TierKeys[tier]));
        Assert.DoesNotContain(p.Pack.Items, i => i is JacobsPickaxe or JacobsReinforcedPickaxe);
    }

    // ---------------------------------------------------------------- F-11 1 and 2 (satchel upgrade, T1 to T2)

    [Fact]
    public void F11_1_HalfInThePackHalfInTheBankTheUpgradeTakesThePacksFirst()
    {
        using var p = new Player();
        MiningMember(p, 25, standing: 1000);
        Put(p.Pack, new CompactOreSatchel());
        Put(p.Pack, new IronIngot(150));
        Put(p.Bank, new IronIngot(150));
        Put(p.Bank, new DullCopperIngot(50));
        Put(p.Pack, new Gold(5000));

        Press(new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.UpgradeSatchel), p.Ns, 91);

        Assert.Single(p.Pack.Items, i => i.GetType() == typeof(ReinforcedOreSatchel));
        Assert.Equal(0, Amount<IronIngot>(p.Pack));
        Assert.Equal(100, Amount<IronIngot>(p.Bank));
        Assert.Equal(0, Amount<DullCopperIngot>(p.Bank));
        Assert.Equal(0, p.Guild.GuildCurrency["mining"]);
    }

    [Fact]
    public void F11_2_ShortByOneAcrossBothIsRefusedAndNothingIsTaken()
    {
        using var p = new Player();
        MiningMember(p, 25, standing: 1000);
        var satchel = Put(p.Pack, new CompactOreSatchel());
        // Iron is all there (200 of 200); dull copper is 49 of 50, split across both. The iron is checked first, so
        // a helper that took each material as it passed would take the iron before finding the copper short.
        Put(p.Pack, new IronIngot(100));
        Put(p.Bank, new IronIngot(100));
        Put(p.Pack, new DullCopperIngot(25));
        Put(p.Bank, new DullCopperIngot(24));
        Put(p.Pack, new Gold(5000));

        Press(new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.UpgradeSatchel), p.Ns, 91);

        Assert.False(satchel.Deleted);
        Assert.DoesNotContain(p.Pack.Items, i => i is ReinforcedOreSatchel);
        Assert.Equal(100, Amount<IronIngot>(p.Pack));
        Assert.Equal(100, Amount<IronIngot>(p.Bank));
        Assert.Equal(25, Amount<DullCopperIngot>(p.Pack));
        Assert.Equal(24, Amount<DullCopperIngot>(p.Bank));
        Assert.Equal(5000, Amount<Gold>(p.Pack));
        Assert.Equal(25, p.Guild.GuildCurrency["mining"]);
    }

    // ---------------------------------------------------------------- F-11 3

    [Fact]
    public void F11_3_LooseIngotsAreUsedBeforeASatchels()
    {
        using var p = new Player();
        MiningMember(p, 1);
        ClusterFRestorationRegistry.Unlock(p.Account, TierKeys[1], "test");

        var bag = Put(p.Pack, new Bag());
        var satchel = Put(p.Pack, new CompactOreSatchel());
        Put(satchel, new IronIngot(50));
        Put(p.Pack, new IronIngot(10));
        Put(bag, new IronIngot(5)); // a plain bag is not a satchel: loose
        Put(p.Pack, new Gold(100));

        Restore(p, 1);

        Assert.True(ClusterFRestorationRegistry.HasActiveCopy(p.Account, TierKeys[1]));
        Assert.Equal(0, bag.GetAmount(typeof(IronIngot)));
        Assert.Equal(40, satchel.GetAmount(typeof(IronIngot))); // 25 = 10 loose + 5 in the bag + 10 from the satchel
        Assert.Equal(40, Amount<IronIngot>(p.Pack));
    }

    // ---------------------------------------------------------------- F-11 4 and 5 (the work order ledger)

    private static void TurnIn(Player p, string key)
    {
        p.Guild.JoinedGuilds.Add("smithing");
        if (!p.Guild.ActiveWorkOrders.Any(e => e.DefKey == key))
        {
            p.Guild.ActiveWorkOrders.Add(new WorkOrderEntry(key, "smithing"));
        }

        var idx = p.Guild.ActiveWorkOrders
            .Where(e => e.GuildKey.Equals("smithing", StringComparison.OrdinalIgnoreCase))
            .ToList()
            .FindIndex(e => e.DefKey == key);

        Press(new GuildContractLedgerGump(p.Pm, "smithing", GuildContractLedgerGump.Tab.Active), p.Ns, 200 + idx);
    }

    [Fact]
    public void F11_4_AnEquippedBlessedOrLockedUpItemIsNeverTakenForATurnIn()
    {
        using var p = new Player();

        var equipped = new Longsword();
        p.Pm.AddItem(equipped); // on its layer, as worn
        Assert.Same(p.Pm, equipped.Parent);
        var blessed = Put(p.Pack, new Longsword { LootType = LootType.Blessed });
        var lockedBox = Put(p.Pack, new MetalBox());
        var locked = Put(lockedBox, new Longsword());
        lockedBox.Locked = true;
        var bankBox = Put(p.Bank, new MetalBox());
        var lockedInBank = Put(bankBox, new Longsword());
        bankBox.Locked = true;

        Assert.Equal(0, GuildResources.Count(p.Pm, GuildCost.Of<Longsword>(1)).Total);

        TurnIn(p, OneSword);

        Assert.Contains(p.Guild.ActiveWorkOrders, e => e.DefKey == OneSword);
        Assert.All(new Item[] { equipped, blessed, locked, lockedInBank }, i => Assert.False(i.Deleted));
        Assert.Same(p.Pm, equipped.Parent);

        // A plain one in the bank is the one taken; the four stay.
        var plain = Put(p.Bank, new Longsword());
        TurnIn(p, OneSword);

        Assert.True(plain.Deleted);
        Assert.DoesNotContain(p.Guild.ActiveWorkOrders, e => e.DefKey == OneSword);
        Assert.All(new Item[] { equipped, blessed, locked, lockedInBank }, i => Assert.False(i.Deleted));
    }

    [Fact]
    public void F11_5_ATurnInThatNeedsCraftedItemsTakesThemFromTheBank()
    {
        using var p = new Player();
        var a = Put(p.Bank, new Longsword());
        var b = Put(p.Bank, new Longsword());
        Assert.Equal(0, Amount<Longsword>(p.Pack));

        TurnIn(p, TwoSwords);

        Assert.True(a.Deleted);
        Assert.True(b.Deleted);
        Assert.DoesNotContain(p.Guild.ActiveWorkOrders, e => e.DefKey == TwoSwords);
        Assert.Contains(p.Guild.CompletedWorkOrders, e => e.DefKey == TwoSwords);
    }

    // ---------------------------------------------------------------- F-11 6

    [Fact]
    public void F11_6_TheOrderIsPackLoosePackSatchelBankLooseBankSatchel()
    {
        using var p = new Player();
        var packSatchel = Put(p.Pack, new CompactOreSatchel());
        var bankSatchel = Put(p.Bank, new CompactOreSatchel());

        var inBankSatchel = Put(bankSatchel, new IronIngot(1));
        var bankLoose = Put(p.Bank, new IronIngot(2));
        var inPackSatchel = Put(packSatchel, new IronIngot(3));
        var packLoose = Put(p.Pack, new IronIngot(4));

        var order = GuildResources.Candidates(p.Pm, GuildCost.Of<IronIngot>(10));
        Assert.Equal(new Item[] { packLoose, inPackSatchel, bankLoose, inBankSatchel }, order);

        var stock = GuildResources.Count(p.Pm, GuildCost.Of<IronIngot>(10));
        Assert.Equal(new GuildStock(7, 3), stock);
        _out.WriteLine(GuildResources.Describe(p.Pm, GuildCost.Of<IronIngot>(9)));
        Assert.Equal("9 iron ingots: 7 in pack, 2 in bank", GuildResources.Describe(p.Pm, GuildCost.Of<IronIngot>(9)));

        // Two costs on the same items cannot both be paid from them.
        Assert.False(GuildResources.TryConsume(p.Pm, GuildCost.Of<IronIngot>(6), GuildCost.Of<IronIngot>(5)));
        Assert.Equal(10, Amount<IronIngot>(p.Pack) + Amount<IronIngot>(p.Bank));
    }
}
