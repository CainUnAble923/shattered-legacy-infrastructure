// RegularSmithBODVerification.cs
//
// cc-P55 Part H (bug-list D76 / PT-11, F-25 items 3 and 6; Chase 2026-10-05).
//
//   1. Regular smiths (pinned Blacksmith and Weaponsmith, and through them GeorgeHephaestus and our GargoyleWeaponsmith)
//      keep issuing smith BODs, but asking opens a Small/Large choice. Large needs the stock skill, 70.1 Blacksmithy
//      (pinned Blacksmith.cs:96, Weaponsmith.cs:68). The stock timer stays and either choice starts it.
//   2. At a regular smith a non-member gets OSI's rewards (pinned BaseVendor.OnDragDrop), unchanged; a Society member
//      gets the guild's.
//   3. A member's turn-in, at the guildmaster, a regular smith or the guild book, is banked (Seals, the default) or
//      cashed out (half the deed's OSI gold, its fame, and OSI's item at 1 in 40), by a per-character setting (always
//      bank, always cash out, ask each time) saved with the character's Society data (CharacterGuildData v2).
// Notes: shard-migration notes/cc-P55-bug-batch-5.md, Part H.
//
// Facts:
//   H1. A regular smith's menu has the choice entry in place of the stock one, and it opens Small/Large (both vendors).
//   H2. Large is refused below 70.1 (no button; the rule checked again) and given at 70.1.
//   H3. Either choice starts the stock timer, the same hours the stock request sets, and a second request waits; a
//       choice the stock roll did not make is swapped for a stock deed of the kind chosen.
//   H4. A non-member at a regular smith gets stock rewards (gold), and no Seals.
//   H5. A member at a regular smith is paid the guild's way: banked Seals and standing, no gold.
//   H6. Cash out pays half the OSI gold and can roll OSI's item (and does not when the roll misses); no Seals.
//   H7. Banking pays more value than cash out for a small iron, a small Valorite exceptional and a large Valorite
//       exceptional deed.
//   H8. The setting defaults to bank, reads bank from a v1 record, and survives the guild data's save and load.
//   H9. "Ask" shows the two-button choice at turn-in, the deed stays until a button, and the button pays.
//   H10. No deed pays twice and no limit is dodged: a stale choice pays nothing, the large-order skill and the deed's
//        place are checked again when the button is pressed, and walking away from the smith refuses it.
//   H11. The guildmaster's Bulk Order gump and the guild book's order page show the one setting and cycle it.

using System;
using System.Collections.Generic;
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
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RegularSmithBODVerification
{
    private readonly ITestOutputHelper _out;

    public RegularSmithBODVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private static readonly Point3D Spot = new(1520, 1520, 0);

    private sealed class Smith : IDisposable
    {
        public readonly Account Account = new($"p55h{Guid.NewGuid():N}"[..16], "p55-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Npcs = new();

        public Smith(double skill, bool member)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Cap = 200.0;
            Pm.Skills.Blacksmith.Base = skill;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            if (member)
            {
                Guild.JoinedGuilds.Add("smithing");
            }
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);

        public int Seals => ClusterFAccountPersistence.GetGuild(Pm)?.GetCurrency("smithing") ?? 0;

        public int Standing => ClusterFAccountPersistence.GetGuild(Pm)?.GetReputation("smithing") ?? 0;

        public int Gold => Pm.Backpack.GetAmount(typeof(Gold)) + Pm.Backpack.Items.OfType<BankCheck>().Sum(c => c.Worth);

        public T Near<T>(T npc, int dx = 1) where T : Mobile
        {
            npc.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
            Npcs.Add(npc);
            return npc;
        }

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
            BuiltInRng.Reset();
        }
    }

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

    private static SmallSmithBOD SmallIron() => new(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);

    private static SmallSmithBOD SmallValoriteExceptional() =>
        new(20, 20, typeof(PlateChest), 1025141, 0x1415, true, BulkMaterialType.Valorite);

    private static LargeSmithBOD LargeValoriteExceptionalPlate()
    {
        var deed = new LargeSmithBOD(20, true, BulkMaterialType.Valorite, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        foreach (var entry in deed.Entries)
        {
            entry.Amount = deed.AmountMax;
        }

        Assert.True(deed.Complete);
        return deed;
    }

    private static List<string> Labels(Gump gump) => gump.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();

    // ---------------------------------------------------------------- H1

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARegularSmithOffersSmallOrLarge(bool weaponsmith)
    {
        using var s = new Smith(80.0, member: false);
        BaseVendor vendor = weaponsmith ? s.Near(new Weaponsmith()) : s.Near(new Blacksmith());

        var entries = Entries(vendor, s.Pm);
        _out.WriteLine($"{vendor.GetType().Name} menu: {string.Join(", ", entries.Select(e => $"{e.GetType().Name} {e.Number}"))}");
        var entry = Assert.Single(entries, e => e.Number == RegularSmithBulkOrderEntry.Cliloc);
        Assert.IsType<RegularSmithBulkOrderEntry>(entry);

        entry.OnClick(s.Pm, vendor);
        var gump = s.Pm.FindGump<RegularSmithBulkOrderGump>();
        Assert.NotNull(gump);
        var buttons = gump.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToList();
        Assert.Contains(RegularSmithBulkOrderGump.BtnSmall, buttons);
        Assert.Contains(RegularSmithBulkOrderGump.BtnLarge, buttons);
        Assert.Contains("Small bulk order", Labels(gump));
        Assert.Contains("Large bulk order", Labels(gump));
    }

    // ---------------------------------------------------------------- H2

    [Fact]
    public void LargeNeedsTheStockSkill()
    {
        using var s = new Smith(70.0, member: false);
        var vendor = s.Near(new Blacksmith());

        var below = new RegularSmithBulkOrderGump(s.Pm, vendor);
        Assert.DoesNotContain(below.Entries.OfType<GumpButton>(), b => b.ButtonID == RegularSmithBulkOrderGump.BtnLarge);
        Assert.Contains("Large bulk order: needs 70.1 Blacksmithy", Labels(below));
        Assert.Null(ClusterFRegularSmithBODs.CreateChosen(vendor, s.Pm, true));
        Assert.Equal(TimeSpan.Zero, s.Pm.NextSmithBulkOrder); // a refusal starts no timer

        s.Pm.Skills.Blacksmith.Base = 70.1;
        BuiltInRng.Generator = new ConstantRandom { Value = 0.99 }; // the stock roll says small: swapped for large
        var deed = ClusterFRegularSmithBODs.CreateChosen(vendor, s.Pm, true);
        _out.WriteLine($"at 70.0: refused; at 70.1: {deed?.GetType().Name}");
        Assert.IsType<LargeSmithBOD>(deed);
        deed.Delete();
    }

    // ---------------------------------------------------------------- H3

    [Theory]
    [InlineData(40.0, 1.0)]
    [InlineData(60.0, 2.0)]
    [InlineData(80.0, 6.0)]
    public void EitherChoiceStartsTheStockTimer(double skill, double hours)
    {
        using var s = new Smith(skill, member: false);
        using var stock = new Smith(skill, member: false);
        var vendor = s.Near(new Blacksmith());

        // The stock request, for the same skill, on another character.
        stock.Near(new Blacksmith(), 2).CreateBulkOrder(stock.Pm, true)?.Delete();

        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 }; // at 80 the stock roll says large: swapped for small
        var deed = ClusterFRegularSmithBODs.CreateChosen(vendor, s.Pm, false);
        BuiltInRng.Reset();

        _out.WriteLine($"skill {skill}: chose small, got {deed?.GetType().Name}; timer {s.Pm.NextSmithBulkOrder.TotalHours:F2} h, " +
                       $"stock {stock.Pm.NextSmithBulkOrder.TotalHours:F2} h");
        Assert.IsType<SmallSmithBOD>(deed);
        Assert.Equal(hours, Math.Round(s.Pm.NextSmithBulkOrder.TotalHours, 2));
        Assert.Equal(Math.Round(stock.Pm.NextSmithBulkOrder.TotalHours, 2), Math.Round(s.Pm.NextSmithBulkOrder.TotalHours, 2));
        Assert.Null(ClusterFRegularSmithBODs.CreateChosen(vendor, s.Pm, false)); // waiting
        deed.Delete();
    }

    // ---------------------------------------------------------------- H4

    [Fact]
    public void ANonMemberGetsStockRewards()
    {
        using var s = new Smith(80.0, member: false);
        var vendor = s.Near(new Blacksmith());
        var deed = SmallIron();
        s.Pm.Backpack.DropItem(deed);
        s.Ready();

        var gold = s.Gold;
        Assert.True(vendor.OnDragDrop(s.Pm, deed));
        _out.WriteLine($"non-member: +{s.Gold - gold} gold, seals {s.Seals}");
        Assert.True(deed.Deleted);
        Assert.True(s.Gold > gold);
        Assert.Equal(0, s.Seals);
    }

    // ---------------------------------------------------------------- H5

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMemberAtARegularSmithIsPaidTheGuildsWay(bool weaponsmith)
    {
        using var s = new Smith(80.0, member: true);
        BaseVendor vendor = weaponsmith ? s.Near(new Weaponsmith()) : s.Near(new Blacksmith());
        var deed = SmallIron();
        s.Pm.Backpack.DropItem(deed);
        s.Ready();
        var (seals, standing, _) = BlacksmithGuildmaster.ComputeGuildReward(deed);

        var gold = s.Gold;
        Assert.True(vendor.OnDragDrop(s.Pm, deed));
        _out.WriteLine($"member at {vendor.GetType().Name}: seals {s.Seals}, standing {s.Standing}, gold +{s.Gold - gold}");
        Assert.True(deed.Deleted);
        Assert.Equal(seals, s.Seals);       // an iron deed's Seals do not vary (cc-P56: 2 for the gold, 50 for the shovel)
        Assert.Equal(standing, s.Standing);
        Assert.Equal(gold, s.Gold);
        Assert.Equal(SmithTurnInMode.Bank, ClusterFSmithBODPayout.GetMode(s.Pm)); // the default
    }

    // ---------------------------------------------------------------- H6

    [Fact]
    public void CashOutPaysGoldAndCanRollTheItem()
    {
        using var s = new Smith(80.0, member: true);
        var vendor = s.Near(new Blacksmith());
        ClusterFSmithBODPayout.SetMode(s.Pm, SmithTurnInMode.CashOut);

        foreach (var (roll, item) in new[] { (0.0, true), (0.99, false) })
        {
            var deed = SmallIron();
            s.Pm.Backpack.DropItem(deed);
            s.Ready();
            BuiltInRng.Generator = new ConstantRandom { Value = roll };
            var osiGold = SmithRewardCalculator.Instance.ComputeGold(deed); // the same roll the payout makes
            var shovels = s.Pm.Backpack.GetAmount(typeof(SturdyShovel));
            var gold = s.Gold;
            var seals = s.Seals;
            var standing = s.Standing;

            Assert.True(vendor.OnDragDrop(s.Pm, deed));
            BuiltInRng.Reset();

            _out.WriteLine($"roll {roll}: +{s.Gold - gold} gold (OSI {osiGold}), shovels {shovels} -> " +
                           $"{s.Pm.Backpack.GetAmount(typeof(SturdyShovel))}, seals +{s.Seals - seals}, standing +{s.Standing - standing}");
            Assert.True(deed.Deleted);
            Assert.Equal((int)(osiGold * ClusterFSmithBODPayout.CashOutGoldShare), s.Gold - gold);
            Assert.Equal(item ? shovels + 1 : shovels, s.Pm.Backpack.GetAmount(typeof(SturdyShovel)));
            Assert.Equal(seals, s.Seals);
            Assert.True(s.Standing > standing); // standing is earned either way
        }
    }

    // ---------------------------------------------------------------- H7

    // What the notes value each sample's OSI item at, in Seals: its own Seal catalog price, or, for the 115 power scroll
    // (not sold), the midpoint of its neighbours on OSI's reward ladder (Ancient Smithy Hammer +10 at 750 points, 100 Seals,
    // and +15 at 850, 200 Seals).
    private static int ItemSeals(Item reward) => reward switch
    {
        SturdyShovel  => Price(SmithSealCatalogGump.Cat.Tools, "Sturdy Shovel"),
        PowerScroll   => (Price(SmithSealCatalogGump.Cat.AncientHammers, "Ancient Smithy Hammer (+10 uses)") +
                          Price(SmithSealCatalogGump.Cat.AncientHammers, "Ancient Smithy Hammer (+15 uses)")) / 2,
        RunicHammer r => Price(SmithSealCatalogGump.Cat.RunicVanilla, $"{CraftResources.GetName(r.Resource)} Runic Hammer"),
        _             => throw new ArgumentException(reward.GetType().Name)
    };

    private static int Price(SmithSealCatalogGump.Cat cat, string name) =>
        SmithSealCatalogGump.Rows(cat).Single(r => r.Name == name).Cost;

    [Fact]
    public void BankingPaysMoreThanCashingOut()
    {
        var deeds = new (string Name, Item Deed)[]
        {
            ("small iron", SmallIron()),
            ("small Valorite exceptional", SmallValoriteExceptional()),
            ("large Valorite exceptional plate", LargeValoriteExceptionalPlate())
        };

        try
        {
            foreach (var (name, deed) in deeds)
            {
                BuiltInRng.Generator = new ConstantRandom { Value = 0.5 }; // the middle of every gold range
                var (seals, _, _) = BlacksmithGuildmaster.ComputeGuildReward(deed);
                ((BaseBOD)deed).GetRewards(out var reward, out var osiGold, out _);
                BuiltInRng.Reset();

                // Gold at the guild's own rate (BlacksmithGuildmaster.SealsForGold: 3 Seals for 400 gold of deed value).
                var cashGold = (int)(osiGold * ClusterFSmithBODPayout.CashOutGoldShare);
                var goldSeals = cashGold * BlacksmithGuildmaster.SealMultiplier / 400.0;
                var itemSeals = ItemSeals(reward) * ClusterFSmithBODPayout.CashOutItemChance;
                var cashValue = goldSeals + itemSeals;

                _out.WriteLine($"{name}: bank {seals} Seals; cash out {cashGold} gold ({goldSeals:F1} Seals) + " +
                               $"{ClusterFSmithBODPayout.CashOutItemChance:P1} of {reward.GetType().Name} " +
                               $"({ItemSeals(reward)} Seals, {itemSeals:F1}) = {cashValue:F1} Seals");
                reward.Delete();
                Assert.True(seals > cashValue, $"{name}: bank {seals} <= cash out {cashValue:F1}");
            }
        }
        finally
        {
            foreach (var (_, deed) in deeds)
            {
                deed.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- H8

    [Fact]
    public void TheSettingDefaultsToBankAndSurvivesSaveAndLoad()
    {
        using var s = new Smith(80.0, member: false);
        ClusterFAccountPersistence.Get(s.Account)?.ClearGuildData();
        Assert.Equal(SmithTurnInMode.Bank, ClusterFSmithBODPayout.GetMode(s.Pm));
        Assert.Equal(SmithTurnInMode.Bank, new CharacterGuildData().SmithTurnIn);
        Assert.Equal(2, CharacterGuildData.CurrentVersion);

        // A v1 record (cc-P46): v0's layout and the teaching bool.
        var buffer1 = new byte[4096];
        var w1 = new BufferWriter(buffer1, true);
        w1.Write(1);
        w1.Write(1);
        w1.Write("smithing");
        for (var i = 0; i < 7; i++)
        {
            w1.Write(0);
        }

        w1.Write("");
        w1.Write(0u);
        w1.Write(false); // teaching off
        w1.Flush();
        var v1 = new CharacterGuildData(new BufferReader(buffer1));
        Assert.Contains("smithing", v1.JoinedGuilds);
        Assert.False(v1.SmithTeachingOrders);
        Assert.Equal(SmithTurnInMode.Bank, v1.SmithTurnIn);

        foreach (var mode in new[] { SmithTurnInMode.CashOut, SmithTurnInMode.Ask, SmithTurnInMode.Bank })
        {
            var g = new CharacterGuildData { SmithTurnIn = mode };
            g.AddCurrency("smithing", 7);
            var buffer = new byte[4096];
            var w = new BufferWriter(buffer, true);
            g.Serialize(w);
            w.Flush();
            var copy = new CharacterGuildData(new BufferReader(buffer));
            _out.WriteLine($"written {mode}, read {copy.SmithTurnIn}");
            Assert.Equal(mode, copy.SmithTurnIn);
            Assert.Equal(7, copy.GetCurrency("smithing"));
        }

        Assert.False(new CharacterGuildData { SmithTurnIn = SmithTurnInMode.Ask }.IsEmpty);
    }

    // ---------------------------------------------------------------- H9

    [Fact]
    public void AskShowsTheChoiceAndTheButtonPays()
    {
        using var s = new Smith(80.0, member: true);
        var vendor = s.Near(new Blacksmith());
        ClusterFSmithBODPayout.SetMode(s.Pm, SmithTurnInMode.Ask);
        var deed = SmallIron();
        s.Pm.Backpack.DropItem(deed);
        s.Ready();
        var (seals, _, _) = BlacksmithGuildmaster.ComputeGuildReward(deed);

        Assert.False(vendor.OnDragDrop(s.Pm, deed)); // goes back to the pack
        var choice = s.Pm.FindGump<SmithTurnInChoiceGump>();
        Assert.NotNull(choice);
        Assert.False(deed.Deleted);
        Assert.Equal(0, s.Seals);
        var buttons = choice.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToList();
        Assert.Contains(SmithTurnInChoiceGump.BtnBank, buttons);
        Assert.Contains(SmithTurnInChoiceGump.BtnCashOut, buttons);

        Press(choice, s.Ns, SmithTurnInChoiceGump.BtnBank);
        _out.WriteLine($"ask: deed kept until Bank; then seals {s.Seals}");
        Assert.True(deed.Deleted);
        Assert.Equal(seals, s.Seals);

        // The guildmaster and the book ask the same way.
        var master = s.Near(new BlacksmithGuildmaster(), 2);
        var second = SmallIron();
        s.Pm.Backpack.DropItem(second);
        s.Pm.CloseGump<SmithTurnInChoiceGump>();
        Assert.False(master.OnDragDrop(s.Pm, second));
        Assert.NotNull(s.Pm.FindGump<SmithTurnInChoiceGump>());
        s.Pm.CloseGump<SmithTurnInChoiceGump>();
        Assert.True(BlacksmithGuildmaster.TurnInBOD(s.Pm, second));
        Assert.NotNull(s.Pm.FindGump<SmithTurnInChoiceGump>());
        Assert.False(second.Deleted);
    }

    // ---------------------------------------------------------------- H10

    [Fact]
    public void NoDeedPaysTwiceAndNoLimitIsDodged()
    {
        using var s = new Smith(80.0, member: true);
        var vendor = s.Near(new Blacksmith());
        var master = s.Near(new BlacksmithGuildmaster(), 2);

        // 1. A choice left open, the deed paid elsewhere, then the stale button: nothing more.
        ClusterFSmithBODPayout.SetMode(s.Pm, SmithTurnInMode.Ask);
        var deed = SmallIron();
        s.Pm.Backpack.DropItem(deed);
        s.Ready();
        Assert.False(vendor.OnDragDrop(s.Pm, deed));
        var stale = s.Pm.FindGump<SmithTurnInChoiceGump>();
        ClusterFSmithBODPayout.SetMode(s.Pm, SmithTurnInMode.Bank);
        Assert.True(master.OnDragDrop(s.Pm, deed));
        var seals = s.Seals;
        var gold = s.Gold;
        Press(stale, s.Ns, SmithTurnInChoiceGump.BtnCashOut);
        Assert.Equal(seals, s.Seals);
        Assert.Equal(gold, s.Gold);

        // 2. The large-order skill is checked again at the button.
        ClusterFSmithBODPayout.SetMode(s.Pm, SmithTurnInMode.Ask);
        var large = LargeValoriteExceptionalPlate();
        s.Pm.Backpack.DropItem(large);
        s.Ready();
        Assert.False(master.OnDragDrop(s.Pm, large));
        var choice = s.Pm.FindGump<SmithTurnInChoiceGump>();
        s.Pm.Skills.Blacksmith.Base = 70.0;
        Press(choice, s.Ns, SmithTurnInChoiceGump.BtnBank);
        Assert.False(large.Deleted);
        Assert.Equal(seals, s.Seals);
        s.Pm.Skills.Blacksmith.Base = 80.0;

        // 3. A deed put away (not carried) when the button is pressed.
        Assert.False(master.OnDragDrop(s.Pm, large));
        choice = s.Pm.FindGump<SmithTurnInChoiceGump>();
        s.Pm.BankBox.DropItem(large);
        Press(choice, s.Ns, SmithTurnInChoiceGump.BtnBank);
        Assert.False(large.Deleted);
        Assert.Equal(seals, s.Seals);

        // 4. Walking away from the smith before answering.
        s.Pm.Backpack.DropItem(large);
        Assert.False(master.OnDragDrop(s.Pm, large));
        choice = s.Pm.FindGump<SmithTurnInChoiceGump>();
        s.Pm.MoveToWorld(new Point3D(Spot.X + 30, Spot.Y, Spot.Z), Map.Trammel);
        Press(choice, s.Ns, SmithTurnInChoiceGump.BtnBank);
        Assert.False(large.Deleted);
        Assert.Equal(seals, s.Seals);

        _out.WriteLine($"one payment only; large refused below 70.1, from the bank box and from 30 tiles away; seals {s.Seals}");
    }

    // ---------------------------------------------------------------- H11

    [Fact]
    public void BothGumpsShowAndCycleTheOneSetting()
    {
        using var s = new Smith(80.0, member: true);
        var book = new SmithGuildBook();
        s.Pm.Backpack.DropItem(book);

        string Shown(Gump g) => Labels(g).Single(l => l.StartsWith(ClusterFSmithBODPayout.ToggleLabel));

        Assert.Equal("Completed orders: Always bank", Shown(new SmithBulkOrderChoiceGump(s.Pm)));
        Assert.Equal("Completed orders: Always bank", Shown(new SmithGuildBookGump(s.Pm, book)));

        Press(new SmithBulkOrderChoiceGump(s.Pm), s.Ns, SmithBulkOrderChoiceGump.BtnTurnIn);
        Assert.Equal(SmithTurnInMode.CashOut, ClusterFSmithBODPayout.GetMode(s.Pm));
        Assert.Equal("Completed orders: Always cash out", Shown(new SmithGuildBookGump(s.Pm, book)));

        Press(new SmithGuildBookGump(s.Pm, book), s.Ns, SmithGuildBookGump.BtnTurnInMode);
        Assert.Equal(SmithTurnInMode.Ask, ClusterFSmithBODPayout.GetMode(s.Pm));
        Assert.Equal("Completed orders: Ask each time", Shown(new SmithBulkOrderChoiceGump(s.Pm)));

        Press(new SmithGuildBookGump(s.Pm, book), s.Ns, SmithGuildBookGump.BtnTurnInMode);
        Assert.Equal(SmithTurnInMode.Bank, ClusterFSmithBODPayout.GetMode(s.Pm));
    }
}
