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
//  10. cc-P451 (Codex's P418 review, Crystal's P429): a repair target needs its deed or kit still in the pack; the Blaze
//      repair is for worn gear only (Chase, 10-22); resources reach a guild satchel inside the Field Satchel; a full
//      satchel is accepted, a fifth refused, and a refused drop costs nothing; a full pack takes nothing and drops
//      nothing; the bank counts a Field Satchel's contents; a nested deed is turned in; the items save and load.

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

    // cc-P451 (Chase, 10-22): the Blaze repair stays free, for the gear the member is wearing only.
    [Fact]
    public void BlazeRepairsOnlyTheGearTheMemberWears()
    {
        using var c = new Citizen("Toxic"); // the rank below Blaze
        var worn = Damaged();
        c.Pm.AddItem(worn); // on its layer, worn (the equip checks are not what this fact is about)
        Assert.Same(c.Pm, worn.Parent);
        var carried = c.Put(Damaged());
        var bag = c.Put(new Bag());
        var bagged = Damaged();
        bag.DropItem(bagged);
        var banked = Damaged();
        c.Pm.BankBox.DropItem(banked);
        using var other = new Citizen("Blaze");
        var theirs = Damaged();
        other.Pm.AddItem(theirs);

        Assert.Equal(0, LeagueRegisterSystem.RepairAll(c.Pm, out var refused));
        Assert.Equal(10, worn.HitPoints);

        ClusterFLeagueRanks.SetRank(c.Pm, R("Blaze"));
        var repaired = LeagueRegisterSystem.RepairAll(c.Pm, out var done);
        _out.WriteLine($"below Blaze: {refused}; Blaze: {done}; worn {worn.HitPoints}, carried {carried.HitPoints}, bagged {bagged.HitPoints}, banked {banked.HitPoints}, another's {theirs.HitPoints}");
        Assert.Equal(1, repaired);
        Assert.Equal(50, worn.HitPoints);
        Assert.All(new[] { carried, bagged, banked, theirs }, s => Assert.Equal(10, s.HitPoints));
        worn.Delete();
        banked.Delete();
        theirs.Delete();
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

    // cc-P451 (P418 #1): the target remembers the deed or kit; it must still be in that player's pack when it lands.
    [Fact]
    public void ARepairTargetNeedsItsDeedOrKitStillInThePack()
    {
        using var a = new Citizen();
        using var b = new Citizen();

        // The deed handed over while A's target is pending: B repairs with it, A's target then repairs nothing.
        var deed = a.Put(new FieldRepairDeed());
        deed.OnDoubleClick(a.Pm);
        var aTarget = a.Pm.Target;
        b.Put(deed);
        deed.OnDoubleClick(b.Pm);
        var bSword = b.Put(Damaged());
        b.Pm.Target.Invoke(b.Pm, bSword);
        Assert.Equal(50, bSword.HitPoints);
        Assert.True(deed.Deleted);
        var aSword = a.Put(Damaged());
        aTarget.Invoke(a.Pm, aSword);
        _out.WriteLine($"deed handed over: B's {bSword.HitPoints}, A's {aSword.HitPoints}");
        Assert.Equal(10, aSword.HitPoints);

        // A deed banked after the target opened: no repair, and the deed is kept.
        var banked = a.Put(new FieldRepairDeed());
        banked.OnDoubleClick(a.Pm);
        a.Pm.BankBox.DropItem(banked);
        a.Pm.Target.Invoke(a.Pm, aSword);
        Assert.Equal(10, aSword.HitPoints);
        Assert.False(banked.Deleted);
        banked.Delete();

        // A kit's last charge spent by B while A's target is pending.
        var kit = a.Put(new FieldRepairKit());
        kit.UsesRemaining = 1;
        kit.OnDoubleClick(a.Pm);
        aTarget = a.Pm.Target;
        b.Put(kit);
        kit.OnDoubleClick(b.Pm);
        var bSword2 = b.Put(Damaged());
        b.Pm.Target.Invoke(b.Pm, bSword2);
        Assert.True(kit.Deleted);
        aTarget.Invoke(a.Pm, aSword);
        _out.WriteLine($"kit handed over: B's {bSword2.HitPoints}, A's {aSword.HitPoints}");
        Assert.Equal(10, aSword.HitPoints);
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

    // A guild satchel holding `stacks` separate stacks (DropItem: the setup, not the path under test).
    private static CompactOreSatchel OreSatchel(int stacks)
    {
        var satchel = new CompactOreSatchel();
        for (var i = 0; i < stacks; i++)
        {
            satchel.DropItem(new IronIngot(1) { Hue = i + 1 });
        }

        return satchel;
    }

    private static int OnGround<T>() where T : Item =>
        World.Items.Values.Count(i => i is T && !i.Deleted && i.Parent == null && i.Map != Map.Internal);

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

        // cc-P451 (P418 #3): a satchel of 20 stacks goes in, through the drop itself, and the pack sees no more items.
        var ore = OreSatchel(20);
        var before = c.Pm.Backpack.TotalItems;
        Assert.False(field.OnDragDrop(c.Pm, ore)); // no Compact currency yet
        Assert.Null(ore.Parent);
        c.Guild.AddCurrency("mining", 60);
        Assert.True(field.OnDragDrop(c.Pm, ore));
        var after = c.Pm.Backpack.TotalItems;
        _out.WriteLine($"pack items {before} -> {after}; mining currency left {c.Guild.GetCurrency("mining")}");
        Assert.Same(field, ore.Parent);
        Assert.Equal(10, c.Guild.GetCurrency("mining"));
        Assert.Equal(before, after); // the field satchel is one item, whatever it holds
        Assert.Equal(c.Pm.Backpack.Items.Count, after);
        Assert.Same(ore, c.Pm.Backpack.FindItemByType<CompactOreSatchel>()); // still found where it is
    }

    // cc-P451 (P418 #2): resources go into a guild satchel inside the Field Satchel, by drop and by the routing hooks.
    [Fact]
    public void OreDroppedOnASatchelInsideTheFieldSatchelLandsThere()
    {
        using var c = new Citizen("Bronze", "mining", "foresters");
        var field = c.Put(new LeagueFieldSatchel());
        c.Guild.AddCurrency("mining", 50);
        c.Guild.AddCurrency("foresters", 50);
        var oreSatchel = new CompactOreSatchel();
        var lumber = new ForestersLumberSatchel();
        Assert.True(field.OnDragDrop(c.Pm, oreSatchel));
        Assert.True(field.OnDragDropInto(c.Pm, lumber, new Point3D(40, 40, 0)));

        var ore = new IronOre(5);
        var dropped = oreSatchel.OnDragDrop(c.Pm, ore);
        var ingots = new IronIngot(7);
        var into = oreSatchel.OnDragDropInto(c.Pm, ingots, new Point3D(30, 30, 0));
        var routed = new IronOre(3) { Hue = 0x21 };
        var hooked = oreSatchel.TryDropItem(c.Pm, routed, false); // what CompactOreSatchelRoutingHook does
        var log = new Log(4);
        var logged = lumber.TryDropItem(c.Pm, log, false); // ClusterFLumberjackingExtension's routing
        _out.WriteLine($"drop {dropped}, drop into {into}, ore routing {hooked}, lumber routing {logged}");
        Assert.True(dropped);
        Assert.True(into);
        Assert.True(hooked);
        Assert.True(logged);
        Assert.Same(oreSatchel, ore.Parent);
        Assert.Same(oreSatchel, ingots.Parent);
        Assert.Same(lumber, log.Parent);

        // Still only satchels directly in the Field Satchel.
        var gold = new Gold(5);
        Assert.False(field.OnDragDrop(c.Pm, gold));
        Assert.False(field.OnDragDropInto(c.Pm, gold, new Point3D(10, 10, 0)));
        gold.Delete();
    }

    // cc-P451 (P418 #3, #4): four families fit; a refused satchel costs nothing; moving one inside it costs nothing.
    [Fact]
    public void AFifthSatchelIsRefusedWithNoCharge()
    {
        using var c = new Citizen("Bronze");
        var field = c.Put(new LeagueFieldSatchel());
        foreach (var key in new[] { "mining", "foresters", "rangers", "artificers" })
        {
            c.Guild.AddCurrency(key, 100);
        }

        var satchels = new Item[] { OreSatchel(20), new ForestersLumberSatchel(), new HuntersSatchel(), new ArtificersSatchel() };
        foreach (var satchel in satchels)
        {
            Assert.True(field.OnDragDrop(c.Pm, satchel), satchel.GetType().Name);
        }

        Assert.Equal(4, field.Items.Count);
        Assert.Equal(50, c.Guild.GetCurrency("mining"));

        var fifth = OreSatchel(2);
        Assert.False(field.OnDragDrop(c.Pm, fifth));
        Assert.False(field.OnDragDropInto(c.Pm, fifth, new Point3D(20, 20, 0)));
        _out.WriteLine($"fifth refused; mining currency {c.Guild.GetCurrency("mining")}");
        Assert.Equal(50, c.Guild.GetCurrency("mining")); // no charge for a refused drop
        fifth.Delete();

        // A satchel lifted and dropped back inside the Field Satchel (what Mobile.Lift records): free.
        var moved = satchels[0];
        moved.RecordBounce();
        moved.Internalize();
        Assert.True(field.OnDragDropInto(c.Pm, moved, new Point3D(60, 60, 0)));
        Assert.Same(field, moved.Parent);
        Assert.Equal(50, c.Guild.GetCurrency("mining"));

        // A satchel the pack has no room for (by weight) is refused, with no charge.
        var lumber = satchels[1];
        c.Pm.Backpack.DropItem(lumber); // out of the Field Satchel, into the pack
        lumber.RecordBounce();
        lumber.Internalize(); // lifted from the pack
        var pack = c.Pm.Backpack;
        var ballast = c.Put(new Item(0x1BF2) { Weight = pack.MaxWeight - pack.TotalWeight });
        var foresters = c.Guild.GetCurrency("foresters");
        Assert.False(field.OnDragDrop(c.Pm, lumber));
        Assert.Equal(foresters, c.Guild.GetCurrency("foresters"));
        ballast.Delete();
        Assert.True(field.OnDragDrop(c.Pm, lumber));
        Assert.Equal(foresters - 50, c.Guild.GetCurrency("foresters"));
    }

    // cc-P451 (P418 #6): the one-item discount is for the owner's backpack only; banks and other containers count it all.
    [Fact]
    public void ABankCountsAPopulatedFieldSatchelsContents()
    {
        using var c = new Citizen("Bronze");
        var field = c.Put(new LeagueFieldSatchel());
        c.Guild.AddCurrency("mining", 50);
        var packBefore = c.Pm.Backpack.TotalItems;
        var ore = OreSatchel(20);
        Assert.True(field.OnDragDrop(c.Pm, ore));
        Assert.Equal(packBefore, c.Pm.Backpack.TotalItems);

        var bank = c.Pm.BankBox;
        var bankBefore = bank.TotalItems;
        bank.DropItem(field);
        _out.WriteLine($"bank items {bankBefore} -> {bank.TotalItems}; field reports {field.TotalItems}");
        Assert.Equal(bankBefore + 22, bank.TotalItems); // the field satchel, the ore satchel and its 20 stacks
        Assert.Equal(packBefore - 1, c.Pm.Backpack.TotalItems);

        // Ore added while it is banked is counted there too, and nothing drifts on the way back to the pack.
        ore.DropItem(new IronIngot(1) { Hue = 0x40 });
        Assert.Equal(bankBefore + 23, bank.TotalItems);
        c.Pm.Backpack.DropItem(field);
        Assert.Equal(bankBefore, bank.TotalItems);
        Assert.Equal(packBefore, c.Pm.Backpack.TotalItems);

        // In a bag in the pack it is not directly in the backpack, so its contents count.
        var bag = c.Put(new Bag());
        bag.DropItem(field);
        Assert.Equal(packBefore + 23, c.Pm.Backpack.TotalItems); // the bag, and the field satchel counted whole

        // The cached totals agree with a recount.
        var cached = c.Pm.Backpack.TotalItems;
        c.Pm.UpdateTotals();
        Assert.Equal(cached, c.Pm.Backpack.TotalItems);
    }

    // cc-P451 (P418 #5): a full pack (by items, then by weight) refuses the purchase or the book, and takes nothing.
    [Fact]
    public void AFullPackTakesNothingAndDropsNothing()
    {
        using var c = new Citizen("Bronze", "smithing", "mining", "foresters");
        c.Data.Renown = 20_000;
        c.Put(new Gold(5_000));
        var register = LeagueRegisterSystem.Issue(c.Pm, out _);
        c.Put(new SmithGuildBook());
        Assert.True(LeagueRegisterSystem.Bind(c.Pm, "smithing", out _));
        var oldA = c.Put(new ProspectorsLogbook());
        var oldB = c.Put(new ForestersLogbook());
        var pack = c.Pm.Backpack;
        var ground = (OnGround<LeagueFieldSatchel>(), OnGround<CombinedLogbook>(), OnGround<SmithGuildBook>(), OnGround<LeagueRegister>());

        void Check(string how)
        {
            var renown = c.Data.Renown;
            var gold = pack.GetAmount(typeof(Gold));

            Assert.False(LeagueConsolidation.BuySatchel(c.Pm, out var satchel), how);
            Assert.False(LeagueConsolidation.BuyLogbook(c.Pm, out var logbook), how);
            Assert.False(LeagueRegisterSystem.Unbind(c.Pm, "smithing", out var unbind), how);
            Assert.Null(LeagueRegisterSystem.Issue(c.Pm, out var issue));
            _out.WriteLine($"{how}: {satchel} / {logbook} / {unbind} / {issue}");

            Assert.Equal(renown, c.Data.Renown);
            Assert.Equal(gold, pack.GetAmount(typeof(Gold)));
            Assert.False(oldA.Deleted);
            Assert.False(oldB.Deleted);
            Assert.Contains("smithing", c.League.RegisterGuilds);
            Assert.False(register.Deleted);
            Assert.True(LeagueRegisterSystem.IsCurrent(register, c.Pm));
            Assert.Equal(ground, (OnGround<LeagueFieldSatchel>(), OnGround<CombinedLogbook>(), OnGround<SmithGuildBook>(), OnGround<LeagueRegister>()));
            Assert.Contains("room", satchel);
        }

        pack.MaxItems = pack.TotalItems; // full by items
        Check("items");

        pack.MaxItems = Container.GlobalMaxItems;
        var ballast = c.Put(new Item(0x1BF2) { Weight = pack.MaxWeight - pack.TotalWeight }); // full by weight
        Assert.Equal(pack.MaxWeight, pack.TotalWeight);
        Check("weight");
        ballast.Delete();

        // With room, all of them work.
        Assert.True(LeagueRegisterSystem.Unbind(c.Pm, "smithing", out var m), m);
        Assert.True(LeagueConsolidation.BuySatchel(c.Pm, out m), m);
        Assert.True(pack.FindItemByType<LeagueFieldSatchel>().IsChildOf(pack));
    }

    // cc-P451 (P418 #7): a complete deed in a bag in the pack is turned in with the rest.
    [Fact]
    public void ANestedCompleteDeedIsTurnedIn()
    {
        using var c = new Citizen("Verite", "smithing");
        c.Put(new SmithGuildBook());
        LeagueRegisterSystem.Bind(c.Pm, "smithing", out _);
        var bag = c.Put(new Bag());
        var deed = new SmallSmithBOD(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);
        bag.DropItem(deed);

        Assert.Equal(1, LeagueRegisterSystem.TurnInAll(c.Pm, out var done));
        _out.WriteLine(done);
        Assert.True(deed.Deleted);
    }

    // cc-P451 (P418 #10): the League items save and load whole, and the reader ends where the writer did.
    [Fact]
    public void TheLeagueItemsSaveAndLoad()
    {
        using var c = new Citizen("Bronze");
        var register = LeagueRegisterSystem.Issue(c.Pm, out _);
        var kit = c.Put(new FieldRepairKit());
        kit.UsesRemaining = 4;
        var field = c.Put(new LeagueFieldSatchel());
        var ore = OreSatchel(3);
        field.DropItem(ore);
        c.League.PackTier = 4;
        LeagueBackpack.Apply(c.Pm);
        Assert.Equal(250, c.Pm.Backpack.MaxItems);

        // The copies are never registered with the world, and are left for the collector, not deleted: deleting one
        // would delete the live original's children with it.
        T RoundTrip<T>(T original, Func<Serial, T> make) where T : Item
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            original.Serialize(writer);
            var length = writer.Position;
            writer.Flush();
            var copy = make(original.Serial);
            var reader = new BufferReader(buffer);
            copy.Deserialize(reader);
            _out.WriteLine($"{typeof(T).Name}: wrote {length}, read {reader.Position}");
            Assert.Equal(length, reader.Position);
            return copy;
        }

        Assert.Same(c.Pm, RoundTrip(register, s => new LeagueRegister(s)).Owner);
        Assert.Equal(4, RoundTrip(kit, s => new FieldRepairKit(s)).UsesRemaining);
        var fieldCopy = RoundTrip(field, s => new LeagueFieldSatchel(s));
        Assert.Equal(new Item[] { ore }, fieldCopy.Items);
        Assert.Equal(250, RoundTrip(c.Pm.Backpack, s => new Backpack(s)).MaxItems);
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
