// cc-P123: Bulk orders part 2, the six 2016 crafts (carpentry, fletching, tinkering, cooking, alchemy, inscription) and
// their guild givers. customizations/Services/BulkOrders/ (CraftBODs, CraftBulkEntries, CraftRewardCalculators,
// BulkOrderBooks, BulkOrderGivers, the vendors in BulkOrderVendors) and three patches (BOB-all-crafts, CraftItem-chefs-apron,
// Spellbook-improvement-talisman).
//
// Facts, each for all six crafts unless named:
//   1. Offers at the right skill: Bulk Order Info at the craft's town vendor shows the accept gump; no skill, no entry; a
//      large deed only from 70.1.
//   2. A small deed fills (a stack fills its count at once; the material and the exceptional rule hold), and a large deed
//      combines the small deeds of its list (tinkering's left-hand spoon fills the plain spoon's entry; a jewel's gem).
//   3. The turn-in at the town vendor pays gold to the bank and the deed's points, pending.
//   4. The book stores each craft's deeds, filters by craft (the smith metals no longer show tinkering's), and rebuilds
//      them as the same deed.
//   5. Each guild master gives and takes its craft's deeds for its members only (Tinkers' Union, Arcane Society, Maritime
//      Brotherhood as shopkeepers; Foresters' Union, two crafts, and the Order of the Alembic, not shopkeepers).
//   6. The calculators: ServUO's points; the corrected gold cells; a deed never asks for what no item can be.
//   7. The reward lists are ServUO's, every real reward builds, and the placeholders are named.
//   8. The reward items: the apron's exceptional bonus, the spellbook talisman's extra property, the keg, the dye.
//   9. The save: the new deeds (a jewel's gem, a large deed's counts, a wood) and the reward items round-trip.
//
// Host notes as BulkOrderSystemVerification's (cc-P122): accounts, skill checks, craft systems, guilds, the book's entries.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BulkOrderCraftsVerification
{
    private readonly ITestOutputHelper _out;

    public BulkOrderCraftsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();

        if (!_bookEntries)
        {
            BOBEntries.Configure();
            _bookEntries = true;
        }
    }

    private static bool _bookEntries;

    private static readonly Point3D Spot = new(1540, 1530, 0);

    public static readonly TheoryData<BODType> Crafts = new()
    {
        BODType.Carpentry, BODType.Fletching, BODType.Tinkering, BODType.Cooking, BODType.Alchemy, BODType.Inscription
    };

    private sealed class Crafter : IDisposable
    {
        public readonly Account Account = new($"p123{Guid.NewGuid():N}"[..16], "p123-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Npcs = new();

        public Crafter(double skill, params SkillName[] skills)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());

            foreach (var s in skills)
            {
                Pm.Skills[s].Cap = 200.0;
                Pm.Skills[s].Base = skill;
            }

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

        public void Join(string guildKey) => ClusterFAccountPersistence.GetOrCreateGuild(Pm).JoinedGuilds.Add(guildKey);

        public BODContext Context => BulkOrderSystem.GetContext(Pm);

        public int BankGold => Pm.BankBox.GetAmount(typeof(Gold));

        public void Ready() => Pm.NextBODTurnInTime = DateTime.MinValue;

        public T Pack<T>(T item) where T : Item
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

    private static bool HasOfferGump(PlayerMobile pm) =>
        pm.FindGump<SmallBODAcceptGump>() != null || pm.FindGump<LargeBODAcceptGump>() != null;

    private static void CloseOfferGumps(PlayerMobile pm)
    {
        pm.CloseGump<SmallBODAcceptGump>();
        pm.CloseGump<LargeBODAcceptGump>();
    }

    // -- Each craft's sample: its skill, town vendor, a small entry, an item that fills it, and a large list ------------

    private static SkillName Skill(BODType t) => BulkOrderSystem.GetSkillForBOD(t);

    private static BaseVendor TownVendor(BODType t) =>
        t switch
        {
            BODType.Carpentry   => new Carpenter(),
            BODType.Fletching   => new Bowyer(),
            BODType.Tinkering   => new Tinker(),
            BODType.Cooking     => new Cook(),
            BODType.Alchemy     => new Alchemist(),
            BODType.Inscription => new Scribe(),
            _                   => throw new ArgumentException()
        };

    private static Type SampleType(BODType t) =>
        t switch
        {
            BODType.Carpentry   => typeof(QuarterStaff),
            BODType.Fletching   => typeof(Arrow),
            BODType.Tinkering   => typeof(SpoonLeft),
            BODType.Cooking     => typeof(Muffins),
            BODType.Alchemy     => typeof(HealPotion),
            BODType.Inscription => typeof(ClumsyScroll),
            _                   => throw new ArgumentException()
        };

    private static SmallBOD Small(BODType t, int amountCur, int amountMax = 10, Type type = null)
    {
        type ??= SampleType(t);
        const int number = 1020000;
        const int graphic = 0x1;

        return t switch
        {
            BODType.Carpentry => new SmallCarpentryBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None),
            BODType.Fletching => new SmallFletchingBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None),
            BODType.Tinkering => new SmallTinkerBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None),
            BODType.Cooking   => new SmallCookingBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None),
            BODType.Alchemy   => new SmallAlchemyBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None),
            _ => new SmallInscriptionBOD(amountCur, amountMax, type, number, graphic, false, BulkMaterialType.None)
        };
    }

    private static SmallBulkEntry[] LargeList(BODType t) =>
        t switch
        {
            BODType.Carpentry   => CraftBulkEntries.LargeWeapons,
            BODType.Fletching   => CraftBulkEntries.LargeHumanBows1,
            BODType.Tinkering   => CraftBulkEntries.LargeDining,
            BODType.Cooking     => CraftBulkEntries.LargeMiso,
            BODType.Alchemy     => CraftBulkEntries.LargeRegular,
            BODType.Inscription => CraftBulkEntries.LargeCircle1,
            _                   => throw new ArgumentException()
        };

    private static LargeBOD Large(BODType t, SmallBulkEntry[] list, int amountMax = 10)
    {
        LargeBOD large = t switch
        {
            BODType.Carpentry   => new LargeCarpentryBOD(amountMax, false, BulkMaterialType.None, null),
            BODType.Fletching   => new LargeFletchingBOD(amountMax, false, BulkMaterialType.None, null),
            BODType.Tinkering   => new LargeTinkerBOD(amountMax, false, BulkMaterialType.None, null),
            BODType.Cooking     => new LargeCookingBOD(amountMax, false, BulkMaterialType.None, null),
            BODType.Alchemy     => new LargeAlchemyBOD(amountMax, false, BulkMaterialType.None, null),
            _                   => new LargeInscriptionBOD(amountMax, false, BulkMaterialType.None, null)
        };

        large.Entries = LargeBulkEntry.ConvertEntries(large, list);
        return large;
    }

    private static Item Make(Type type, int amount = 1)
    {
        // Pinned's stackables take an optional amount: the constructor whose parameters are all optional.
        var ctor = type.GetConstructors().First(c => c.GetParameters().All(p => p.IsOptional));
        var item = (Item)ctor.Invoke(ctor.GetParameters().Select(_ => Type.Missing).ToArray());

        if (amount > 1)
        {
            item.Amount = amount;
        }

        return item;
    }

    // ---------------------------------------------------------------- 1. Offers at the right skill

    [Theory]
    [MemberData(nameof(Crafts))]
    public void BulkOrderInfoAtTheTownVendorOffersTheCraftsDeed(BODType craft)
    {
        using var c = new Crafter(60, Skill(craft));
        var vendor = c.Near(TownVendor(craft));

        Assert.Equal(craft, BulkOrderVendors.GetVendorBODType(vendor));
        var info = Assert.Single(Entries(vendor, c.Pm), e => e.Number == 3006152); // Bulk Order Info
        Assert.Contains(Entries(vendor, c.Pm), e => e is BulkOrderVendors.ClaimRewardsEntry);

        info.OnClick(c.Pm, vendor);
        Assert.True(HasOfferGump(c.Pm));

        var offered = c.Pm.FindGump<SmallBODAcceptGump>() is not null;
        _out.WriteLine($"{craft}: {vendor.GetType().Name} offers (small: {offered})");

        using var none = new Crafter(0, Skill(craft));
        var other = none.Near(TownVendor(craft), 2);
        Assert.DoesNotContain(Entries(other, none.Pm), e => e.Number == 3006152);
    }

    [Theory]
    [MemberData(nameof(Crafts))]
    public void ALargeDeedNeeds70Point1AndEveryDeedIsTheCrafts(BODType craft)
    {
        using var low = new Crafter(70.0, Skill(craft));
        using var high = new Crafter(120.0, Skill(craft));

        var lows = Enumerable.Range(0, 150).Select(_ => BulkOrderSystem.CreateBulkOrder(low.Pm, craft, true)).ToList();
        var highs = Enumerable.Range(0, 150).Select(_ => BulkOrderSystem.CreateBulkOrder(high.Pm, craft, true)).ToList();

        _out.WriteLine(
            $"{craft} at 70.0: {lows.Count(k => k is LargeBOD)} large, {lows.Count(k => k == null)} none of 150; " +
            $"at 120: {highs.Count(k => k is LargeBOD)} large"
        );

        Assert.DoesNotContain(lows, k => k is LargeBOD);
        Assert.Contains(highs, k => k is LargeBOD);
        Assert.Contains(lows, k => k is SmallBOD);
        Assert.All(lows.Concat(highs).Where(k => k != null), k => Assert.Equal(craft, BulkOrderSystem.GetBODType(k)));

        lows.Concat(highs).ToList().ForEach(k => k?.Delete());
    }

    // ---------------------------------------------------------------- 2. Fill and combine

    [Theory]
    [MemberData(nameof(Crafts))]
    public void ASmallDeedFillsAndALargeDeedCombinesTheCraftsSmallDeeds(BODType craft)
    {
        using var c = new Crafter(100, Skill(craft));

        var small = c.Pack(Small(craft, 0));

        for (var i = 0; i < 10 && small.AmountCur < 10; i++)
        {
            small.EndCombine(c.Pm, c.Pack(Make(small.Type)));
        }

        Assert.Equal(10, small.AmountCur);
        Assert.True(small.Complete);

        // A wrong item is refused.
        var wrong = c.Pack(new Gold(1));
        var refill = c.Pack(Small(craft, 0));
        refill.EndCombine(c.Pm, wrong);
        Assert.Equal(0, refill.AmountCur);

        var large = c.Pack(Large(craft, LargeList(craft)));

        foreach (var entry in large.Entries)
        {
            // Tinkering's list asks for the plain spoon, fork and knife; the left-hand ones the craft makes fill it.
            var type = craft == BODType.Tinkering && entry.Details.Type == typeof(Spoon) ? typeof(SpoonLeft)
                : craft == BODType.Tinkering && entry.Details.Type == typeof(Fork) ? typeof(ForkLeft)
                : craft == BODType.Tinkering && entry.Details.Type == typeof(Knife) ? typeof(KnifeLeft)
                : entry.Details.Type;

            var part = c.Pack(Small(craft, 10, 10, type));
            large.EndCombine(c.Pm, part);
            Assert.True(part.Deleted, $"{craft}: {type.Name} for {entry.Details.Type.Name}");
        }

        _out.WriteLine($"{craft}: small filled, large of {large.Entries.Length} combined");
        Assert.True(large.Complete);
    }

    [Fact]
    public void AStackFillsItsCountAtOnceAndNoMore()
    {
        using var c = new Crafter(100, SkillName.Inscribe, SkillName.Fletching);

        var scrolls = c.Pack(Small(BODType.Inscription, 0, 15));
        scrolls.EndCombine(c.Pm, c.Pack(Make(typeof(ClumsyScroll), 10)));
        Assert.Equal(10, scrolls.AmountCur);

        var tooMany = c.Pack(Make(typeof(ClumsyScroll), 10));
        scrolls.EndCombine(c.Pm, tooMany); // 1157222: more than the deed asks for
        Assert.Equal(10, scrolls.AmountCur);
        Assert.False(tooMany.Deleted);

        var arrows = c.Pack(Small(BODType.Fletching, 0, 20));
        arrows.EndCombine(c.Pm, c.Pack(Make(typeof(Arrow), 20)));
        Assert.True(arrows.Complete);
    }

    [Fact]
    public void AWoodDeedTakesOnlyItsWoodAndAnExceptionalDeedOnlyExceptional()
    {
        using var c = new Crafter(100, SkillName.Carpentry, SkillName.Fletching);

        var oak = c.Pack(
            new SmallCarpentryBOD(0, 10, typeof(QuarterStaff), 1025005, 0xE89, false, BulkMaterialType.OakWood)
        );

        var plain = c.Pack(new QuarterStaff());
        oak.EndCombine(c.Pm, plain);
        Assert.Equal(0, oak.AmountCur);

        var oakStaff = c.Pack(new QuarterStaff { Resource = CraftResource.OakWood });
        oak.EndCombine(c.Pm, oakStaff);
        Assert.Equal(1, oak.AmountCur);

        var exc = c.Pack(new SmallFletchingBOD(0, 10, typeof(Bow), 1025041, 0x13B2, true, BulkMaterialType.None));
        var normalBow = c.Pack(new Bow());
        exc.EndCombine(c.Pm, normalBow);
        Assert.Equal(0, exc.AmountCur);

        exc.EndCombine(c.Pm, c.Pack(new Bow { Quality = WeaponQuality.Exceptional }));
        Assert.Equal(1, exc.AmountCur);
    }

    [Fact]
    public void AJewelDeedTakesOnlyItsGem()
    {
        using var c = new Crafter(100, SkillName.Tinkering);

        var number = SmallTinkerBOD.GemNumber(typeof(GoldRing), GemType.Ruby);
        var deed = c.Pack(
            new SmallTinkerBOD(0, 10, typeof(GoldRing), number, 0x108A, false, BulkMaterialType.None, GemType.Ruby)
        );

        deed.EndCombine(c.Pm, c.Pack(new GoldRing { GemType = GemType.Emerald }));
        Assert.Equal(0, deed.AmountCur);

        deed.EndCombine(c.Pm, c.Pack(new GoldRing { GemType = GemType.Ruby }));
        Assert.Equal(1, deed.AmountCur);

        // A silver ring is the gold ring's twin (ServUO's tinker table).
        deed.EndCombine(c.Pm, c.Pack(new SilverRing { GemType = GemType.Ruby }));
        Assert.Equal(2, deed.AmountCur);

        Assert.Equal(GemType.Ruby, SmallTinkerBOD.GemFromNumber(typeof(GoldRing), number));
    }

    // ---------------------------------------------------------------- 3. The turn-in

    [Theory]
    [MemberData(nameof(Crafts))]
    public void TheTurnInAtTheTownVendorPaysGoldAndTheDeedsPoints(BODType craft)
    {
        using var c = new Crafter(80, Skill(craft));
        var vendor = c.Near(TownVendor(craft));
        var deed = c.Pack(Small(craft, 10));
        c.Ready();

        var points = BulkOrderSystem.ComputePoints(deed);
        var bank = c.BankGold;

        Assert.True(vendor.OnDragDrop(c.Pm, deed));

        _out.WriteLine($"{craft}: +{c.BankGold - bank} gold, {points} points pending");
        Assert.True(deed.Deleted);
        Assert.True(c.BankGold > bank);
        Assert.True(points > 0);
        Assert.Equal(points, c.Context.GetPendingRewardFor(craft));
        Assert.NotNull(c.Pm.FindGump<BODConfirmBankPointsGump>());

        // Another craft's deed is for some other shopkeeper.
        c.Context.RemovePending(craft);
        var other = c.Pack(Small(craft == BODType.Cooking ? BODType.Alchemy : BODType.Cooking, 10));
        c.Ready();
        Assert.False(vendor.OnDragDrop(c.Pm, other));
        Assert.False(other.Deleted);
    }

    [Fact]
    public void BakersTakeCookingDeedsAsP118sTableSays()
    {
        using var c = new Crafter(80, SkillName.Cooking);
        var baker = c.Near(new Baker());

        Assert.Equal(BODType.Cooking, BulkOrderVendors.GetVendorBODType(baker));
        Assert.Contains(Entries(baker, c.Pm), e => e.Number == 3006152);

        var deed = c.Pack(Small(BODType.Cooking, 10));
        var points = BulkOrderSystem.ComputePoints(deed);
        c.Ready();
        Assert.True(baker.OnDragDrop(c.Pm, deed));
        Assert.True(deed.Deleted);
        Assert.Equal(points, c.Context.GetPendingRewardFor(BODType.Cooking)); // the 2016 turn-in, not pinned's old one
    }

    // ---------------------------------------------------------------- 4. The book

    private static int CraftFilter(BODType t) =>
        t switch
        {
            BODType.Tinkering   => BulkOrderBooks.TinkeringFilter,
            BODType.Carpentry   => BulkOrderBooks.CarpentryFilter,
            BODType.Fletching   => BulkOrderBooks.FletchingFilter,
            BODType.Alchemy     => BulkOrderBooks.AlchemyFilter,
            BODType.Inscription => BulkOrderBooks.InscriptionFilter,
            BODType.Cooking     => BulkOrderBooks.CookingFilter,
            _                   => 0
        };

    [Theory]
    [MemberData(nameof(Crafts))]
    public void TheBookStoresTheCraftFiltersByItAndRebuildsTheDeed(BODType craft)
    {
        using var c = new Crafter(80, Skill(craft), SkillName.Blacksmith);
        var book = c.Pack(new BulkOrderBook());

        var small = c.Pack(Small(craft, 4));
        var large = c.Pack(Large(craft, LargeList(craft)));
        large.Entries[0].Amount = 10;
        var smith = c.Pack(new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None));

        Assert.True(book.OnDragDrop(c.Pm, small));
        Assert.True(book.OnDragDrop(c.Pm, large));
        Assert.True(book.OnDragDrop(c.Pm, smith));
        Assert.Equal(new[] { craft, craft, BODType.Smith }, book.Entries.Select(e => e.DeedType));

        book.Filter.Material = CraftFilter(craft);
        var gump = new BOBGump(c.Pm, book);
        Assert.Equal(new[] { true, true, false }, book.Entries.Select(gump.CheckFilter));

        book.Filter.Material = 1; // Blacksmithy
        gump = new BOBGump(c.Pm, book);
        Assert.Equal(new[] { false, false, true }, book.Entries.Select(gump.CheckFilter));

        var backSmall = (SmallBOD)book.Entries[0].Reconstruct();
        Assert.Equal(small.GetType(), backSmall.GetType());
        Assert.Equal((4, small.Type), (backSmall.AmountCur, backSmall.Type));

        var backLarge = (LargeBOD)book.Entries[1].Reconstruct();
        Assert.Equal(large.GetType(), backLarge.GetType());
        Assert.Equal(10, backLarge.Entries[0].Amount);

        backSmall.Delete();
        backLarge.Delete();
    }

    [Fact]
    public void TheSmithMetalFiltersNoLongerShowTinkeringDeedsAndTheBookKeepsAJewelsGem()
    {
        using var c = new Crafter(80, SkillName.Tinkering, SkillName.Blacksmith);
        var book = c.Pack(new BulkOrderBook());

        var tinker = c.Pack(new SmallTinkerBOD(0, 10, typeof(Scales), 1024223, 0x1852, false, BulkMaterialType.DullCopper));
        var smith = c.Pack(
            new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.DullCopper)
        );
        var ring = c.Pack(
            new SmallTinkerBOD(
                0, 10, typeof(GoldRing), SmallTinkerBOD.GemNumber(typeof(GoldRing), GemType.Sapphire), 0x108A, false,
                BulkMaterialType.None, GemType.Sapphire
            )
        );

        book.OnDragDrop(c.Pm, tinker);
        book.OnDragDrop(c.Pm, smith);
        book.OnDragDrop(c.Pm, ring);

        book.Filter.Material = 4; // smith: Dull Copper
        Assert.Equal(new[] { false, true, false }, book.Entries.Select(new BOBGump(c.Pm, book).CheckFilter));

        book.Filter.Material = 24; // tinkering: Dull Copper (ServUO's number)
        Assert.Equal(new[] { true, false, false }, book.Entries.Select(new BOBGump(c.Pm, book).CheckFilter));

        var back = (SmallTinkerBOD)book.Entries[2].Reconstruct();
        Assert.Equal(GemType.Sapphire, back.GemType);
        back.Delete();
    }

    // ---------------------------------------------------------------- 5. The guild masters

    public static readonly TheoryData<string> VendorGuildmasters = new() { "tinkers", "arcane", "maritime" };

    private static (BaseVendor Master, BODType Craft) VendorGuildmaster(string key) =>
        key switch
        {
            "tinkers" => (new TinkerGuildmaster(), BODType.Tinkering),
            "arcane"  => (new MageGuildmaster(), BODType.Inscription),
            _         => (new FisherGuildmaster(), BODType.Cooking)
        };

    [Theory]
    [MemberData(nameof(VendorGuildmasters))]
    public void TheShopkeeperGuildmastersServeTheirMembersOnly(string key)
    {
        var (master, craft) = VendorGuildmaster(key);
        using var c = new Crafter(80, Skill(craft));
        c.Near(master);

        Assert.False(master.SupportsBulkOrders(c.Pm));
        Assert.DoesNotContain(Entries(master, c.Pm), e => e.Number == 3006152);

        var refused = c.Pack(Small(craft, 10));
        c.Ready();
        Assert.False(master.OnDragDrop(c.Pm, refused));
        Assert.False(refused.Deleted);

        c.Join(key);
        Assert.True(master.SupportsBulkOrders(c.Pm));
        Assert.Contains(Entries(master, c.Pm), e => e.Number == 3006152);
        Assert.True(BulkOrderVendors.TryOffer(master, c.Pm));
        Assert.True(HasOfferGump(c.Pm));

        c.Ready();
        var bank = c.BankGold;
        Assert.True(master.OnDragDrop(c.Pm, refused));
        Assert.True(refused.Deleted);
        Assert.True(c.BankGold > bank);
        _out.WriteLine($"{master.GetType().Name} ({key}): {craft} deeds for members");
    }

    [Fact]
    public void TheAlembicGuildmasterServesItsMembersOnly()
    {
        using var c = new Crafter(80, SkillName.Alchemy);
        var master = c.Near(new AlembicGuildmaster());

        Assert.DoesNotContain(Entries(master, c.Pm), e => e.Number == 3006152);

        var deed = c.Pack(Small(BODType.Alchemy, 10));
        c.Ready();
        Assert.False(master.OnDragDrop(c.Pm, deed));
        Assert.False(deed.Deleted);

        c.Join(ClusterFAlembicGuild.Key);
        var info = Assert.Single(Entries(master, c.Pm), e => e.Number == 3006152);
        Assert.Contains(Entries(master, c.Pm), e => e is BulkOrderGivers.ClaimRewardsEntry);

        info.OnClick(c.Pm, master);
        Assert.True(HasOfferGump(c.Pm));

        c.Ready();
        var bank = c.BankGold;
        Assert.True(master.OnDragDrop(c.Pm, deed));
        Assert.True(deed.Deleted);
        Assert.True(c.BankGold > bank);
        Assert.True(c.Context.GetPendingRewardFor(BODType.Alchemy) > 0);

        // It takes no cooking deed.
        c.Context.RemovePending(BODType.Alchemy);
        var cooking = c.Pack(Small(BODType.Cooking, 10));
        c.Ready();
        Assert.False(master.OnDragDrop(c.Pm, cooking));
        Assert.False(cooking.Deleted);
    }

    [Fact]
    public void TheForestersGuildmasterGivesCarpentryAndFletchingAndAsksWhichToAMemberWithBoth()
    {
        using var c = new Crafter(80, SkillName.Carpentry, SkillName.Fletching);
        var master = c.Near(new ForestersGuildmaster());

        Assert.DoesNotContain(Entries(master, c.Pm), e => e.Number == 3006152);

        c.Join(BulkOrderGivers.ForestersKey);
        var info = Assert.Single(Entries(master, c.Pm), e => e.Number == 3006152);

        info.OnClick(c.Pm, master); // both skills: which craft?
        var gump = c.Pm.FindGump<BulkOrderCraftGump>();
        Assert.NotNull(gump);
        Assert.False(HasOfferGump(c.Pm));

        BulkOrderGivers.Act(master, c.Pm, BODType.Fletching, false);
        Assert.True(HasOfferGump(c.Pm));
        CloseOfferGumps(c.Pm);

        // Both crafts' deeds are taken.
        foreach (var craft in new[] { BODType.Carpentry, BODType.Fletching })
        {
            c.Context.RemovePending(BODType.Carpentry);
            var deed = c.Pack(Small(craft, 10));
            c.Ready();
            Assert.True(master.OnDragDrop(c.Pm, deed), $"{craft}");
            Assert.True(deed.Deleted);
        }

        // With carpentry only, Bulk Order Info offers carpentry at once.
        using var carpenter = new Crafter(80, SkillName.Carpentry);
        carpenter.Join(BulkOrderGivers.ForestersKey);
        Assert.Equal(new List<BODType> { BODType.Carpentry }, BulkOrderGivers.CraftsFor(master, carpenter.Pm));
        Assert.Single(Entries(master, carpenter.Pm), e => e.Number == 3006152).OnClick(carpenter.Pm, master);
        Assert.True(HasOfferGump(carpenter.Pm));
        Assert.Null(carpenter.Pm.FindGump<BulkOrderCraftGump>());
    }

    // ---------------------------------------------------------------- 6. The calculators

    [Fact]
    public void ThePointsAreServUOs()
    {
        // Rewards.cs ComputePoints: quantity 10/25/50, exceptional 200, wood 300..550, metal 200 + 50 a step, part counts.
        Assert.Equal(50 + 200 + 350, CarpentryRewardCalculator.Instance.ComputePoints(20, true, BulkMaterialType.AshWood, 1, typeof(QuarterStaff)));
        Assert.Equal(10 + 300 + 250, CarpentryRewardCalculator.Instance.ComputePoints(10, false, BulkMaterialType.OakWood, 4, typeof(RedArmoire)));
        Assert.Equal(25 + 550, FletchingRewardCalculator.Instance.ComputePoints(15, false, BulkMaterialType.Frostwood, 1, typeof(Bow)));
        Assert.Equal(50 + 300 + (200 + 50 * 2) + 200, TinkeringRewardCalculator.Instance.ComputePoints(20, true, BulkMaterialType.Copper, 4, typeof(Spoon)));
        Assert.Equal(10 + 450, CookingRewardCalculator.Instance.ComputePoints(10, false, BulkMaterialType.None, 4, typeof(MisoSoup)));
        Assert.Equal(10 + 250, AlchemyRewardCalculator.Instance.ComputePoints(10, false, BulkMaterialType.None, 3, typeof(HealPotion)));
        Assert.Equal(10 + 300, AlchemyRewardCalculator.Instance.ComputePoints(10, false, BulkMaterialType.None, 3, typeof(GreaterAgilityPotion)));
        Assert.Equal(50 + 350, InscriptionRewardCalculator.Instance.ComputePoints(20, false, BulkMaterialType.None, 5, typeof(ChainLightningScroll)));
    }

    [Fact]
    public void TheSlippedGoldCellsAreCorrected()
    {
        // ServUO's 5-part regular fletching deed of 10 in yew reads 60000; its neighbours run 5000, 5000, 6000.
        // A 5-part exceptional carpentry deed of 20 in ash reads 1250 between 10000 and 15000 (ServUO's index reads the
        // carpentry table's "4-part exceptional" row for 5 parts).
        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(FletchingRewardCalculator.Instance.ComputeGold(10, false, BulkMaterialType.YewWood, 5, null), 5400, 6667);
            Assert.InRange(CarpentryRewardCalculator.Instance.ComputeGold(20, true, BulkMaterialType.AshWood, 5, null), 11250, 13889);
        }

        // A list that lost an item the shard lacks (2 parts) pays the 3-part row, not an empty one.
        Assert.InRange(CookingRewardCalculator.Instance.ComputeGold(10, false, BulkMaterialType.None, 2, null), 3600, 4445);
    }

    [Fact]
    public void ADeedNeverAsksForWhatNoItemCanBe()
    {
        using var c = new Crafter(120, SkillName.Cooking, SkillName.Fletching, SkillName.Carpentry);

        var cooking = Enumerable.Range(0, 200).Select(_ => BulkOrderSystem.CreateBulkOrder(c.Pm, BODType.Cooking, true))
            .OfType<BaseBOD>().ToList();
        var fletching = Enumerable.Range(0, 200).Select(_ => BulkOrderSystem.CreateBulkOrder(c.Pm, BODType.Fletching, true))
            .OfType<BaseBOD>().ToList();
        var carpentry = Enumerable.Range(0, 300).Select(_ => BulkOrderSystem.CreateBulkOrder(c.Pm, BODType.Carpentry, true))
            .OfType<BaseBOD>().ToList();

        _out.WriteLine(
            $"cooking exceptional {cooking.Count(d => d.RequireExceptional)}/{cooking.Count}; fletching " +
            $"{fletching.Count(d => d.RequireExceptional)}/{fletching.Count}; carpentry material " +
            $"{carpentry.Count(d => d.Material != BulkMaterialType.None)}/{carpentry.Count}"
        );

        // Pinned's food has no quality: no cooking deed asks for exceptional. Bows do: some fletching deeds ask.
        Assert.DoesNotContain(cooking, d => d.RequireExceptional);
        Assert.Contains(fletching, d => d.RequireExceptional);

        // Pinned's furniture records no wood: a material is asked only of items that record one.
        Assert.All(
            carpentry.Where(d => d.Material != BulkMaterialType.None),
            d => Assert.True(CraftBOD.CanUseMaterial(BulkOrderSystem.GetTypeFromBOD(d)))
        );

        // A large list drops what the craft cannot make: no large toxic deed asks for parasitic or darkglow potions.
        var toxic = new LargeAlchemyBOD(10, false, BulkMaterialType.None, null);
        toxic.Entries = CraftBOD.Convert(toxic, CraftBulkEntries.LargeToxic, DefAlchemy.CraftSystem);
        Assert.DoesNotContain(toxic.Entries, e => e.Details.Type == typeof(ParasiticPotion));
        Assert.Contains(toxic.Entries, e => e.Details.Type == typeof(DeadlyPoisonPotion));

        cooking.Concat(fletching).Concat(carpentry).ToList().ForEach(d => d.Delete());
        toxic.Delete();
    }

    // ---------------------------------------------------------------- 7. The reward lists

    [Fact]
    public void TheRewardListsAreServUOsWithThePlaceholdersNamed()
    {
        var counts = new Dictionary<BODType, int>
        {
            [BODType.Tinkering] = 25, [BODType.Carpentry] = 27, [BODType.Inscription] = 14, [BODType.Cooking] = 13,
            [BODType.Fletching] = 17, [BODType.Alchemy] = 18
        };

        foreach (var (craft, count) in counts)
        {
            var list = BODRewardCollections.Get(craft);
            var placeholders = list.Where(r => r.IsPlaceholder).Select(r => r.Placeholder).ToList();
            _out.WriteLine($"{craft}: {list.Count} rewards, {placeholders.Count} placeholders: {string.Join("; ", placeholders)}");

            Assert.Equal(count, list.Count);
            Assert.Equal(list.Select(r => r.Points).OrderBy(p => p), list.Select(r => r.Points).OrderBy(p => p));

            foreach (var reward in list.Where(r => !r.IsPlaceholder))
            {
                var item = reward.Construct();
                Assert.NotNull(item);
                item.Delete();
            }
        }

        Assert.Contains(BODRewardCollections.Get(BODType.Cooking), r => r.Placeholder == "FermentationBarrel");
        Assert.Contains(BODRewardCollections.Get(BODType.Cooking), r => !r.IsPlaceholder && r.Tooltip == 1157227); // apron
    }

    // ---------------------------------------------------------------- 8. The reward items

    [Fact]
    public void TheChefsApronAddsItsBonusToTheExceptionalChance()
    {
        using var c = new Crafter(100, SkillName.Blacksmith);
        var craft = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Longsword));

        var without = craft.GetExceptionalChance(DefBlacksmithy.CraftSystem, 1.0, c.Pm);
        var apron = new MasterChefsApron();
        c.Pm.AddItem(apron);
        var with = craft.GetExceptionalChance(DefBlacksmithy.CraftSystem, 1.0, c.Pm);

        _out.WriteLine($"apron bonus {apron.Bonus}%: {without:0.000} -> {with:0.000}");
        Assert.Equal(without + apron.Bonus / 100.0, with, 6);
    }

    [Fact]
    public void TheImprovementTalismanAddsAPropertyAndSpendsACharge()
    {
        using var c = new Crafter(100, SkillName.Inscribe);
        Assert.Equal(0, GuaranteedSpellbookImprovementTalisman.ExtraProperty(c.Pm));

        var talisman = new GuaranteedSpellbookImprovementTalisman(2);
        c.Pm.AddItem(talisman);

        Assert.Equal(1, GuaranteedSpellbookImprovementTalisman.ExtraProperty(c.Pm));
        Assert.Equal(1, talisman.Charges);
        Assert.Equal(1, GuaranteedSpellbookImprovementTalisman.ExtraProperty(c.Pm));
        Assert.True(talisman.Deleted);
    }

    [Fact]
    public void TheKegHoldsPowderAndTheDyeColours()
    {
        using var c = new Crafter(100, SkillName.Tinkering);

        var keg = c.Pack(new PowderOfFortKeg());
        var powder = c.Pack(new PowderOfTemperament(10));
        keg.OnDragDrop(c.Pm, powder);
        Assert.Equal(10, keg.Charges);
        Assert.True(powder.Deleted);

        keg.OnDoubleClick(c.Pm);
        Assert.Equal(9, keg.Charges);
        Assert.Equal(1, c.Pm.Backpack.FindItemByType<PowderOfTemperament>()?.UsesRemaining);

        var dye = c.Pack(new SpecialNaturalDye(DyeType.DullRuby));
        var robe = c.Pack(new Robe());
        dye.Dye(c.Pm, robe);
        Assert.Equal(2731, robe.Hue);
        Assert.Equal(4, dye.UsesRemaining);

        var bookDye = c.Pack(new SpecialNaturalDye(DyeType.WindAzul, true));
        var shirt = c.Pack(new Shirt());
        bookDye.Dye(c.Pm, shirt);
        Assert.NotEqual(2741, shirt.Hue);
        Assert.Equal(5, bookDye.UsesRemaining);
    }

    // ---------------------------------------------------------------- 9. The save

    private static T RoundTrip<T>(T original, Func<Serial, T> make) where T : Item
    {
        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = make(original.Serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void TheNewDeedsAndRewardsSurviveASaveRoundTrip()
    {
        var number = SmallTinkerBOD.GemNumber(typeof(GoldRing), GemType.Ruby);
        var ring = new SmallTinkerBOD(3, 15, typeof(GoldRing), number, 0x108A, true, BulkMaterialType.None, GemType.Ruby);
        var ringCopy = RoundTrip(ring, s => new SmallTinkerBOD(s));
        Assert.Equal(
            (3, 15, typeof(GoldRing), number, true, GemType.Ruby),
            (ringCopy.AmountCur, ringCopy.AmountMax, ringCopy.Type, ringCopy.Number, ringCopy.RequireExceptional, ringCopy.GemType)
        );

        var jewels = new LargeTinkerBOD(10, false, BulkMaterialType.None, null, GemType.Emerald);
        jewels.Entries = LargeBulkEntry.ConvertEntries(jewels, CraftBulkEntries.LargeJewelry);
        jewels.Entries[1].Amount = 10;
        var jewelsCopy = RoundTrip(jewels, s => new LargeTinkerBOD(s));
        Assert.Equal(GemType.Emerald, jewelsCopy.GemType);
        Assert.Equal(10, jewelsCopy.Entries[1].Amount);

        var wood = new LargeCarpentryBOD(20, true, BulkMaterialType.Heartwood, null);
        wood.Entries = LargeBulkEntry.ConvertEntries(wood, CraftBulkEntries.LargeWeapons);
        var woodCopy = RoundTrip(wood, s => new LargeCarpentryBOD(s));
        Assert.Equal((20, true, BulkMaterialType.Heartwood), (woodCopy.AmountMax, woodCopy.RequireExceptional, woodCopy.Material));
        Assert.Equal(wood.Entries.Length, woodCopy.Entries.Length);

        var apron = new MasterChefsApron();
        var apronCopy = RoundTrip(apron, s => new MasterChefsApron(s));
        Assert.Equal(apron.Bonus, apronCopy.Bonus);

        var dye = new SpecialNaturalDye(DyeType.ZentoOrchid, true) { UsesRemaining = 3 };
        var dyeCopy = RoundTrip(dye, s => new SpecialNaturalDye(s));
        Assert.Equal((DyeType.ZentoOrchid, 3, true), (dyeCopy.DyeType, dyeCopy.UsesRemaining, dyeCopy.BooksOnly));

        var keg = new PowderOfFortKeg(120);
        var kegCopy = RoundTrip(keg, s => new PowderOfFortKeg(s));
        Assert.Equal(120, kegCopy.Charges);

        _out.WriteLine(
            $"round trip: ring {ringCopy.GemType}, jewels {jewelsCopy.GemType}, wood {woodCopy.Material}, apron " +
            $"{apronCopy.Bonus}%, dye {dyeCopy.DyeType}, keg {kegCopy.Charges}"
        );

        foreach (var item in new Item[] { ring, jewels, wood, apron, dye, keg })
        {
            item.Delete();
        }
    }
}
