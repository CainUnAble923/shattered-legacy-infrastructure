// LeagueRegisterVerification.cs
//
// cc-P280: the League Register (P179's design, Chase's answers of 2026-10-21), its League actions, the backpack upgrades,
// the consolidation items and the repair items. Notes: shard-migration notes/cc-P280-league-register-build.md.
//
// Facts:
//   1. The Registrar issues the Register from Dull Copper, not below; a re-issue makes the older copy stop working.
//   2. Binding a book consumes it and records the guild; unbinding gives a fresh book back and frees the slot. A guild
//      with no book binds by membership; a non-member, a full book and a full Register are refused.
//   3. Capacity by League rank: 2 / 4 / 6 / 9 / 12 / 18 at Dull Copper / Bronze / Verite / Blaze / Mythril / Celestial.
//   4. Each League action's rank gate (Dull Copper, Bronze, Verite, Blaze), and each action doing its job at its rank:
//      the contracts ledger opens, a complete smith deed is turned in and paid, the repair pass repairs worn, carried
//      and banked gear.
//   5. The backpack upgrade: item count only, in order, at its rank, for its Renown and gold; rank is not lowered.
//   6. The repair deed (one item, once) and the kit (its charges, each one item); the crafting entries need Arms Lore at
//      the setting's threshold; the remote repair deed does the whole pass once.
//   7. Live membership: a guild left shows once (greyed) at the next open and is dropped then, its slot freed.
//   8. The League data's v2 fields survive a save and load; a v1 record loads with none.
//   9. The League Field Satchel counts as one item however much it holds and charges the merge fee; the Combined
//      Logbook replaces both logbooks; the Bank page counts the bank by kind.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Engines.LeagueRegister;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueRegisterVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueRegisterVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
        LeagueRepair.Register();
    }

    // The ladder's indexes (ClusterFLeagueRanks: one rank per metal, Iron = 1).
    private static int R(string metal) => LeagueRegisterSettings.Rank(metal);

    private sealed class Citizen : IDisposable
    {
        public readonly Account Account = new($"p280{Guid.NewGuid():N}"[..16], "p280-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Citizen(string metal = "DullCopper", params string[] guilds)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 100;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1560, 1560, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Data.SetFlag(ClusterFLeagueSystem.FlagJoined);
            ClusterFLeagueRanks.SetRank(Pm, R(metal));
            foreach (var key in guilds)
            {
                Guild.JoinedGuilds.Add(key);
            }
        }

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);
        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);
        public CharacterLeagueData League => Data.GetOrCreateLeagueData(Pm.Serial);

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

            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            ClusterFAccountPersistence.Get(Account)?.ClearLeagueData();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheRegisterIsIssuedFromDullCopperAndAReissueReplacesIt()
    {
        using var iron = new Citizen("Iron");
        Assert.Null(LeagueRegisterSystem.Issue(iron.Pm, out var refused));
        _out.WriteLine($"Iron: {refused}");
        Assert.Contains("Dull Copper", refused);

        using var c = new Citizen();
        var first = LeagueRegisterSystem.Issue(c.Pm, out _);
        Assert.NotNull(first);
        Assert.True(first.IsChildOf(c.Pm.Backpack));
        Assert.True(LeagueRegisterSystem.IsCurrent(first, c.Pm));

        var second = LeagueRegisterSystem.Issue(c.Pm, out _);
        Assert.True(first.Deleted); // the older copy is gone
        Assert.True(LeagueRegisterSystem.IsCurrent(second, c.Pm));
        Assert.Equal((uint)second.Serial, c.League.RegisterSerial);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BindingTakesTheBookAndUnbindingGivesItBack()
    {
        using var c = new Citizen("Bronze", "smithing", "tailoring");
        var book = c.Put(new SmithGuildBook());

        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "smithing", out var m1), m1);
        Assert.True(book.Deleted);
        Assert.Contains("smithing", c.League.RegisterGuilds);

        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "tailoring", out var m2), m2); // no book yet: by membership
        Assert.Equal(2, LeagueRegisterSystem.UsedSlots(c.Pm));

        Assert.True(LeagueRegisterSystem.Unbind(c.Pm, "smithing", out var m3), m3);
        Assert.DoesNotContain("smithing", c.League.RegisterGuilds);
        Assert.NotNull(c.Pm.Backpack.FindItemByType<SmithGuildBook>()); // a fresh book
        _out.WriteLine($"{m1} / {m2} / {m3}");

        // Refusals: not a member; the book missing; a book with deeds in it.
        Assert.False(LeagueRegisterSystem.Bind(c.Pm, "mining", out var notMember));
        Assert.Contains("not a member", notMember);
        c.Guild.JoinedGuilds.Add("mining");
        Assert.False(LeagueRegisterSystem.Bind(c.Pm, "mining", out var noBook));
        Assert.Contains("book", noBook);

        var full = c.Pm.Backpack.FindItemByType<SmithGuildBook>();
        full.DropItem(new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None));
        Assert.False(LeagueRegisterSystem.Bind(c.Pm, "smithing", out var notEmpty));
        Assert.False(full.Deleted);
        Assert.DoesNotContain("smithing", c.League.RegisterGuilds);
        _out.WriteLine($"{notMember} / {noBook} / {notEmpty}");
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void CapacityFollowsLeagueRank()
    {
        var expect = new (string Metal, int Capacity)[]
        {
            ("Iron", 0), ("DullCopper", 2), ("Copper", 2), ("Bronze", 4), ("Agapite", 4), ("Verite", 6), ("Toxic", 6),
            ("Blaze", 9), ("Obsidian", 9), ("Mythril", 12), ("Adamantium", 12), ("Celestial", 18)
        };

        foreach (var (metal, capacity) in expect)
        {
            _out.WriteLine($"{metal} ({R(metal)}): {LeagueRegisterSettings.CapacityAt(R(metal))}");
            Assert.Equal(capacity, LeagueRegisterSettings.CapacityAt(R(metal)));
        }

        using var c = new Citizen("DullCopper", "tailoring", "healers", "bards");
        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "tailoring", out _));
        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "healers", out _));
        Assert.False(LeagueRegisterSystem.Bind(c.Pm, "bards", out var full));
        Assert.Contains("holds 2", full);

        ClusterFLeagueRanks.SetRank(c.Pm, R("Bronze"));
        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "bards", out _));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void EachLeagueActionHasItsRank()
    {
        var gates = new (LeagueAction Action, string Metal)[]
        {
            (LeagueAction.Status, "DullCopper"), (LeagueAction.Contracts, "Bronze"),
            (LeagueAction.TurnIn, "Verite"), (LeagueAction.Repair, "Blaze")
        };

        using var c = new Citizen();
        foreach (var (action, metal) in gates)
        {
            ClusterFLeagueRanks.SetRank(c.Pm, R(metal) - 1);
            var below = LeagueRegisterSystem.RefuseAction(c.Pm, action);
            ClusterFLeagueRanks.SetRank(c.Pm, R(metal));
            var at = LeagueRegisterSystem.RefuseAction(c.Pm, action);
            _out.WriteLine($"{action}: below '{below}', at '{at ?? "open"}'");
            Assert.NotNull(below);
            Assert.Null(at);
        }
    }

    [Fact]
    public void BronzeOpensABoundGuildsContracts()
    {
        using var c = new Citizen("DullCopper", "tailoring");
        LeagueRegisterSystem.Bind(c.Pm, "tailoring", out _);

        Assert.False(LeagueRegisterSystem.OpenContracts(c.Pm, "tailoring", out var refused));
        Assert.Null(c.Pm.FindGump<GuildContractLedgerGump>());

        ClusterFLeagueRanks.SetRank(c.Pm, R("Bronze"));
        Assert.True(LeagueRegisterSystem.OpenContracts(c.Pm, "tailoring", out _));
        Assert.NotNull(c.Pm.FindGump<GuildContractLedgerGump>());
        _out.WriteLine($"Dull Copper: {refused}");
    }

    [Fact]
    public void VeriteTurnsInEveryReadyOrderAtOnce()
    {
        using var c = new Citizen("Bronze", "smithing");
        c.Put(new SmithGuildBook());
        LeagueRegisterSystem.Bind(c.Pm, "smithing", out _);
        var deeds = new[]
        {
            c.Put(new SmallSmithBOD(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None)),
            c.Put(new SmallSmithBOD(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None))
        };

        Assert.Equal(0, LeagueRegisterSystem.TurnInAll(c.Pm, out var refused));
        Assert.All(deeds, d => Assert.False(d.Deleted));

        ClusterFLeagueRanks.SetRank(c.Pm, R("Verite"));
        var seals = c.Guild.GetCurrency("smithing");
        Assert.Equal(2, LeagueRegisterSystem.TurnInAll(c.Pm, out var done));
        _out.WriteLine($"Bronze: {refused}; Verite: {done}, seals {seals} -> {c.Guild.GetCurrency("smithing")}");
        Assert.All(deeds, d => Assert.True(d.Deleted));
        Assert.True(c.Guild.GetCurrency("smithing") > seals);
    }

    private static Longsword Damaged()
    {
        var sword = new Longsword { MaxHitPoints = 50 };
        sword.HitPoints = 10;
        return sword;
    }

    [Fact]
    public void BlazeRepairsWornCarriedAndBankedGear()
    {
        using var c = new Citizen("Toxic"); // the rank below Blaze
        var worn = Damaged();
        c.Pm.AddItem(worn); // on its layer, worn (the equip checks are not what this fact is about)
        Assert.Same(c.Pm, worn.Parent);
        var carried = c.Put(Damaged());
        var banked = Damaged();
        c.Pm.BankBox.DropItem(banked);

        Assert.Equal(0, LeagueRegisterSystem.RepairAll(c.Pm, out var refused));
        Assert.Equal(10, carried.HitPoints);

        ClusterFLeagueRanks.SetRank(c.Pm, R("Blaze"));
        var repaired = LeagueRegisterSystem.RepairAll(c.Pm, out var done);
        _out.WriteLine($"below Blaze: {refused}; Blaze: {done}; worn {worn.HitPoints}, carried {carried.HitPoints}, banked {banked.HitPoints} (in bank {banked.IsChildOf(c.Pm.BankBox)})");
        Assert.Equal(3, repaired);
        Assert.All(new[] { worn, carried, banked }, s => Assert.Equal(50, s.HitPoints));
        worn.Delete();
        banked.Delete();
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheBackpackUpgradeSellsItemCountOnly()
    {
        using var c = new Citizen("Copper");
        c.Data.Renown = 100_000;
        c.Data.LifetimeRenown = 100_000;
        c.Put(new Gold(10_000));
        var weight = c.Pm.Backpack.MaxWeight;
        var lifetime = c.Data.LifetimeRenown;

        Assert.False(LeagueBackpack.Buy(c.Pm, out var belowBronze));
        Assert.Equal(Container.GlobalMaxItems, c.Pm.Backpack.MaxItems);

        ClusterFLeagueRanks.SetRank(c.Pm, R("Bronze"));
        Assert.True(LeagueBackpack.Buy(c.Pm, out var first), first);
        Assert.Equal(150, c.Pm.Backpack.MaxItems);
        Assert.Equal(100_000 - 2_000, c.Data.Renown);
        Assert.Equal(10_000 - 200, c.Pm.Backpack.GetAmount(typeof(Gold)));
        Assert.Equal(weight, c.Pm.Backpack.MaxWeight); // weight is not sold
        Assert.Equal(lifetime, c.Data.LifetimeRenown); // rank is never lowered

        Assert.False(LeagueBackpack.Buy(c.Pm, out var needsVerite)); // the next step is Verite's
        Assert.Equal(150, c.Pm.Backpack.MaxItems);

        ClusterFLeagueRanks.SetRank(c.Pm, R("Mythril"));
        Assert.True(LeagueBackpack.Buy(c.Pm, out _));
        Assert.True(LeagueBackpack.Buy(c.Pm, out _));
        Assert.True(LeagueBackpack.Buy(c.Pm, out _));
        Assert.Equal(250, c.Pm.Backpack.MaxItems);
        Assert.False(LeagueBackpack.Buy(c.Pm, out var maxed));
        _out.WriteLine($"{belowBronze} / {first} / {needsVerite} / {maxed}; Renown {c.Data.Renown}");

        // The tier is the character's and comes back at login.
        c.Pm.Backpack.MaxItems = Container.GlobalMaxItems;
        LeagueBackpack.OnLogin(c.Pm);
        Assert.Equal(250, c.Pm.Backpack.MaxItems);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheRepairDeedAndKitRepairOneItemPerUse()
    {
        using var c = new Citizen();
        var deed = c.Put(new FieldRepairDeed());
        var sword = c.Put(Damaged());

        deed.OnDoubleClick(c.Pm);
        c.Pm.Target.Invoke(c.Pm, sword);
        Assert.Equal(50, sword.HitPoints);
        Assert.True(deed.Deleted);

        var kit = c.Put(new FieldRepairKit());
        Assert.Equal(LeagueRegisterSettings.KitCharges, kit.UsesRemaining);
        Assert.Equal(10, kit.UsesRemaining);
        for (var i = 1; i <= 10; i++)
        {
            var item = c.Put(Damaged());
            kit.OnDoubleClick(c.Pm);
            c.Pm.Target.Invoke(c.Pm, item);
            Assert.Equal(50, item.HitPoints);
            Assert.Equal(i == 10, kit.Deleted);
            if (!kit.Deleted)
            {
                Assert.Equal(10 - i, kit.UsesRemaining);
            }
        }

        // An item already whole costs no charge.
        var kit2 = c.Put(new FieldRepairKit());
        kit2.OnDoubleClick(c.Pm);
        c.Pm.Target.Invoke(c.Pm, sword);
        Assert.Equal(10, kit2.UsesRemaining);

        var remote = c.Put(new RemoteRepairDeed());
        var a = c.Put(Damaged());
        var b = Damaged();
        c.Pm.BankBox.DropItem(b);
        remote.OnDoubleClick(c.Pm);
        Assert.True(remote.Deleted);
        Assert.Equal(50, a.HitPoints);
        Assert.Equal(50, b.HitPoints);
        b.Delete();
    }

    [Theory]
    [InlineData(typeof(FieldRepairDeed), 90)]
    [InlineData(typeof(FieldRepairKit), 110)]
    [InlineData(typeof(RemoteRepairDeed), 120)]
    public void CraftingARepairItemNeedsItsArmsLore(Type type, int armsLore)
    {
        var entry = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(type);
        Assert.NotNull(entry);

        using var c = new Citizen();
        c.Pm.Skills.Blacksmith.Cap = c.Pm.Skills.ArmsLore.Cap = 200;
        c.Pm.Skills.Blacksmith.Base = 130;
        c.Pm.Skills.ArmsLore.Base = armsLore - 0.1;
        entry.GetSuccessChance(c.Pm, typeof(IronIngot), DefBlacksmithy.CraftSystem, false, out var below);
        c.Pm.Skills.ArmsLore.Base = armsLore;
        entry.GetSuccessChance(c.Pm, typeof(IronIngot), DefBlacksmithy.CraftSystem, false, out var at);
        _out.WriteLine($"{type.Name}: Arms Lore {armsLore - 0.1} all skills {below}, {armsLore} {at}");
        Assert.False(below);
        Assert.True(at);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void AGuildLeftIsShownOnceAndDropped()
    {
        using var c = new Citizen("DullCopper", "tailoring", "healers");
        LeagueRegisterSystem.Bind(c.Pm, "tailoring", out _);
        LeagueRegisterSystem.Bind(c.Pm, "healers", out _);

        c.Guild.JoinedGuilds.Remove("tailoring"); // as [ResetGuild does
        new LeagueRegisterGump(c.Pm, LeagueRegisterGump.Page.Guilds); // the open that finds it: shown, then dropped
        Assert.DoesNotContain("tailoring", c.League.RegisterGuilds);
        Assert.Equal(1, LeagueRegisterSystem.UsedSlots(c.Pm));
        Assert.Empty(LeagueRegisterSystem.DropLeft(c.Pm)); // the next open has nothing left to show

        Assert.False(LeagueRegisterSystem.Bind(c.Pm, "tailoring", out var rebind));
        Assert.Contains("not a member", rebind); // no refund, no book; the freed slot is there for a guild you are in
        c.Guild.JoinedGuilds.Add("bards");
        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "bards", out _));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheLeagueDataV2RoundTrips()
    {
        var record = new CharacterLeagueData { Rank = 5, RegisterSerial = 0x4001234, PackTier = 3, MuleGranted = true };
        record.RegisterGuilds.Add("smithing");
        record.RegisterGuilds.Add("tailoring");

        var buffer = new byte[256];
        var w = new BufferWriter(buffer, true);
        record.Serialize(w);
        w.Flush();
        var back = new CharacterLeagueData(new BufferReader(buffer));
        Assert.Equal(new[] { "smithing", "tailoring" }, back.RegisterGuilds.OrderBy(k => k));
        Assert.Equal(0x4001234u, back.RegisterSerial);
        Assert.Equal(3, back.PackTier);
        Assert.True(back.MuleGranted);

        var v1 = new byte[64];
        var w1 = new BufferWriter(v1, true);
        w1.Write(1);  // version 1
        w1.Write(4);  // rank
        w1.Write(0);  // no jobs
        w1.Write(0);  // no trials
        w1.Write(2);  // mule stage
        w1.Write(true);
        w1.Flush();
        var old = new CharacterLeagueData(new BufferReader(v1));
        Assert.Equal(4, old.Rank);
        Assert.Empty(old.RegisterGuilds);
        Assert.Equal(0, old.PackTier);
        Assert.Equal(0u, old.RegisterSerial);
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheFieldSatchelCountsAsOneAndChargesTheMergeFee()
    {
        using var c = new Citizen("Bronze", "mining", "foresters");
        c.Data.Renown = 10_000;
        c.Put(new Gold(1_000));
        Assert.True(LeagueConsolidation.BuySatchel(c.Pm, out var bought), bought);
        var field = c.Pm.Backpack.FindItemByType<LeagueFieldSatchel>();
        Assert.NotNull(field);
        Assert.Equal(5_000, c.Data.Renown);

        var ore = c.Put(new CompactOreSatchel());
        for (var i = 0; i < 20; i++)
        {
            ore.DropItem(new IronOre(1) { Hue = i }); // separate stacks, 20 items
        }

        var before = c.Pm.Backpack.TotalItems;
        Assert.False(field.Merge(c.Pm, ore)); // no Compact currency yet
        c.Guild.AddCurrency("mining", 60);
        Assert.True(field.Merge(c.Pm, ore));
        field.DropItem(ore);
        var after = c.Pm.Backpack.TotalItems;
        _out.WriteLine($"pack items {before} -> {after}; mining currency left {c.Guild.GetCurrency("mining")}");
        Assert.Equal(10, c.Guild.GetCurrency("mining"));
        Assert.True(after < before);
        Assert.Equal(c.Pm.Backpack.Items.Count, after); // the field satchel is one item, whatever it holds
        Assert.Same(ore, c.Pm.Backpack.FindItemByType<CompactOreSatchel>()); // still found where it is

        var second = new CompactOreSatchel();
        Assert.False(field.CheckHold(c.Pm, second, false, true, 0, 0)); // one per kind
        Assert.False(field.CheckHold(c.Pm, new Gold(1), false, true, 0, 0)); // satchels only
        second.Delete();
    }

    [Fact]
    public void TheCombinedLogbookReplacesBothLogbooks()
    {
        using var c = new Citizen("Iron", "mining", "foresters");
        c.Data.Renown = 1_000;
        c.Put(new Gold(100));
        var a = c.Put(new ProspectorsLogbook());
        var b = c.Put(new ForestersLogbook());

        Assert.False(LeagueConsolidation.BuyLogbook(c.Pm, out var refused));
        ClusterFLeagueRanks.SetRank(c.Pm, R("DullCopper"));
        Assert.True(LeagueConsolidation.BuyLogbook(c.Pm, out var bought), bought);
        _out.WriteLine($"{refused} / {bought}");
        Assert.True(a.Deleted);
        Assert.True(b.Deleted);
        Assert.NotNull(c.Pm.Backpack.FindItemByType<CombinedLogbook>());
        Assert.Equal(500, c.Data.Renown);
    }

    [Fact]
    public void TheBankPageCountsTheBankByKind()
    {
        using var c = new Citizen();
        c.Pm.BankBox.DropItem(new IronIngot(40));
        c.Pm.BankBox.DropItem(new VeriteIngot(2));
        c.Pm.BankBox.DropItem(new Gold(300));
        c.Pm.BankBox.DropItem(new BankCheck(1_000));
        c.Pm.BankBox.DropItem(new Board(12));

        var summary = LeagueBank.Summary(c.Pm).ToDictionary(r => r.Label, r => r.Amount);
        _out.WriteLine(string.Join(", ", summary.Select(kv => $"{kv.Key} {kv.Value}")));
        Assert.Equal(42, summary["Ingots"]);
        Assert.Equal(1_300, summary["Gold"]);
        Assert.Equal(12, summary["Boards"]);
        Assert.False(summary.ContainsKey("Ore"));
    }
}
