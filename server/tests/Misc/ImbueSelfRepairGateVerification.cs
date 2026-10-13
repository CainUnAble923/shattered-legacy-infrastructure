// ImbueSelfRepairGateVerification.cs
//
// cc-P42 Part A (Chase, 2026-10-02) made Self Repair dear and gated it at Master Artificer (15,000 standing). cc-P82 (the
// Artificers' Imbuing rework, shard-migration docs/artificers-imbuing-design.md) gates imbuing by property and tier
// instead, and its ladder makes Self Repair free: every account knows it at Basic, and the formula books raise it. The
// price floor of cc-P46 Part C stays. Notes: shard-migration notes/cc-P82-artificers-imbuing.md.
//
// Every imbue goes through the Imbuing Table's own buttons (Imbue target, the property's row, a tier, Apply), as a
// player's client would send them; a refusal is the server's own check, not a missing button.
//
// Facts:
//   1. At 0 standing, with nothing learned, Self Repair is listed on a weapon and on armor, and its Basic tier is
//      imbued: no rank gate (the cc-P42 gate is gone).
//   2. Its Full tier (5) is refused until the account knows Full, then imbued at the floored price.
//   3. Every Self Repair value (1 to its top) costs more shards and more gold than the highest value of every other
//      property in the catalogue, computed from the catalogue (cc-P46 Part C, now standing-free).
//   4. That floor is what the table charges: Self Repair's Basic tier, through the buttons, takes the floored price.
//   5. Another property keeps its scaling: Swing Speed Increase at Full (60, once known) costs its base 8 shards and
//      3,500 gold.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
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
public class ImbueSelfRepairGateVerification
{
    // ArtificersImbueGump.BtnId: ImbueTarget 1, NextPage 4, ApplyImbue 5, PropBase 100 (+ index), ConfirmBase 1000
    // (+ tier: 1 Basic, 2 Refined, 3 Full, 4 Overcharged).
    internal const int BtnImbueTarget = 1;
    internal const int BtnNextPage = 4;
    internal const int BtnApplyImbue = 5;
    internal const int BtnPropBase = 100;
    internal const int BtnConfirmBase = 1000;

    internal const int Shards = 1000;
    internal const int Gold = 200000;

    private readonly ITestOutputHelper _out;

    public ImbueSelfRepairGateVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
        ImbuingTable.Current = ImbuingTable.Defaults;
    }

    internal sealed class Artificer : IDisposable
    {
        public readonly Account Account = new($"p82{Guid.NewGuid():N}"[..16], "p82-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly CharacterGuildData Guild;
        public readonly ClusterFAccountData Data;

        public Artificer(int standing)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            // Above the hardest tier's top (difficulty 120 + 20), so the skill check is "no challenge" and always
            // succeeds: the facts are about price and gate, not luck.
            Pm.Skills[SkillName.Imbuing].Cap = 200.0;
            Pm.Skills[SkillName.Imbuing].Base = 200.0;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1230, 1230, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Data = ClusterFAccountPersistence.GetOrCreate(Account);
            Guild = Data.GetOrCreateGuildData(Pm.Serial);
            Guild.JoinedGuilds.Add("artificers");
            Guild.GuildReputation["artificers"] = standing;
            Guild.GuildCurrency["artificers"] = Shards;

            // Mastered, so no essence is needed: the facts are about price and gate.
            foreach (var def in ImbueCatalogue.All)
            {
                for (var i = 0; i < def.DiscoveryThreshold; i++)
                {
                    Data.IncrementDiscovery(def.Name);
                }
            }

            Banker.Deposit(Pm, Gold);
        }

        public int GoldLeft => CompactGoldHelper.GetTotalGold(Pm);
        public int ShardsLeft => Guild.GetCurrency("artificers");

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    internal static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    internal static ArtificersImbueGump Next(Artificer a, Gump gump, int buttonId)
    {
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        Press(gump, a.Ns, buttonId);
        return a.Pm.FindGump<ArtificersImbueGump>();
    }

    internal static ArtificersImbueGump View(Artificer a, Item item)
    {
        var table = new ArtificersImbueGump(a.Pm);
        Press(table, a.Ns, BtnImbueTarget);
        Assert.NotNull(a.Pm.Target);

        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, item);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(view);
        return view;
    }

    // The table, the item, the property's row, the tier (1 Basic to 4 Overcharged), Apply. Buttons are pressed whether
    // or not the gump drew them, as a client could.
    internal static void Imbue(Artificer a, Item item, string property, int tier)
    {
        var index = ImbueCatalogue.ForItem(item).FindIndex(d => d.Name == property);
        Assert.True(index >= 0, $"{property} is not in the catalogue for {item.GetType().Name}");

        var select = Next(a, View(a, item), BtnPropBase + index);
        Assert.NotNull(select);
        var confirm = Next(a, select, BtnConfirmBase + tier);
        Assert.NotNull(confirm);
        Next(a, confirm, BtnApplyImbue);
    }

    // Every label on the table for this item, paging with Next to the end.
    internal static List<string> AllLabels(Artificer a, Item item)
    {
        var labels = new List<string>();
        var gump = View(a, item);
        for (var page = 0; page < 20 && gump != null; page++)
        {
            labels.AddRange(gump.Entries.OfType<GumpLabel>().Select(l => l.Text));
            if (!gump.Entries.OfType<GumpButton>().Any(b => b.ButtonID == BtnNextPage))
            {
                break;
            }

            gump = Next(a, gump, BtnNextPage);
        }

        return labels;
    }

    [Fact]
    public void SelfRepairIsFreeAtBasicWithNoRank()
    {
        using var a = new Artificer(0);
        var sword = new Longsword();
        var chest = new PlateChest();
        a.Pm.Backpack.DropItem(sword);
        a.Pm.Backpack.DropItem(chest);

        foreach (var (item, property) in new (Item, string)[] { (sword, "Self Repair"), (chest, "Self Repair (Armor)") })
        {
            Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Data, property));
            Assert.Contains(property, AllLabels(a, item));
            Imbue(a, item, property, 1);
        }

        var basic = ImbueTiers.MaxFor(ImbueCatalogue.All.First(d => d.Name == "Self Repair"), ImbueTier.Basic);
        _out.WriteLine($"Self Repair Basic = {basic}; sword {sword.WeaponAttributes.SelfRepair}, chest {chest.ArmorAttributes.SelfRepair}");
        Assert.Equal(basic, sword.WeaponAttributes.SelfRepair);
        Assert.Equal(basic, chest.ArmorAttributes.SelfRepair);
    }

    [Fact]
    public void FullNeedsTheTierThenTakesTheFlooredPrice()
    {
        using var a = new Artificer(0);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);
        var def = ImbueCatalogue.ForItem(sword).Find(d => d.Name == "Self Repair");

        Imbue(a, sword, "Self Repair", 3);
        Assert.Equal(0, sword.WeaponAttributes.SelfRepair);
        Assert.Equal(Shards, a.ShardsLeft);
        Assert.Equal(Gold, a.GoldLeft);

        ClusterFImbueKnowledge.Grant(a.Data, "Self Repair", ImbueTier.Full);
        Imbue(a, sword, "Self Repair", 3);
        _out.WriteLine($"Self Repair 5: {def.ShardsFor(5)} shards, {def.GoldFor(5):N0} gold");
        Assert.Equal(5, sword.WeaponAttributes.SelfRepair);
        Assert.Equal(Shards - def.ShardsFor(5), a.ShardsLeft);
        Assert.Equal(Gold - def.GoldFor(5), a.GoldLeft);
    }

    [Fact]
    public void EverySelfRepairValueCostsMoreThanEveryOtherProperty()
    {
        var others = ImbueCatalogue.All.Where(d => !d.Name.StartsWith("Self Repair", StringComparison.Ordinal)).ToList();
        var mostShards = others.Max(d => d.ShardsFor(d.TopValue));
        var mostGold = others.Max(d => d.GoldFor(d.TopValue));
        var dearestShards = others.First(d => d.ShardsFor(d.TopValue) == mostShards);
        var dearestGold = others.First(d => d.GoldFor(d.TopValue) == mostGold);
        _out.WriteLine($"dearest other in shards {dearestShards.Name} {mostShards}, in gold {dearestGold.Name} {mostGold:N0}");

        foreach (var sr in ImbueCatalogue.All.Where(d => d.Name.StartsWith("Self Repair", StringComparison.Ordinal)))
        {
            Assert.True(sr.PricedAboveAll);
            for (var v = 1; v <= sr.TopValue; v++)
            {
                var shards = sr.ShardsFor(v);
                var gold = sr.GoldFor(v);
                Assert.True(shards > mostShards, $"{sr.Name} {v}: {shards} shards, {dearestShards.Name} {mostShards}");
                Assert.True(gold > mostGold, $"{sr.Name} {v}: {gold} gold, {dearestGold.Name} {mostGold}");
                Assert.Equal(Math.Max(sr.ScaledShardsFor(v), mostShards + 1), shards);
                Assert.Equal(Math.Max(sr.ScaledGoldFor(v), mostGold + 1), gold);
            }
        }
    }

    [Fact]
    public void TheTableChargesTheFloor()
    {
        using var a = new Artificer(0);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);

        Imbue(a, sword, "Self Repair", 1);

        var value = sword.WeaponAttributes.SelfRepair;
        var def = ImbueCatalogue.ForItem(sword).Find(d => d.Name == "Self Repair");
        _out.WriteLine($"Basic: Self Repair {value}, took {Shards - a.ShardsLeft} shards and {Gold - a.GoldLeft:N0} gold");

        Assert.InRange(value, 1, 5);
        Assert.Equal(def.ShardsFor(value), Shards - a.ShardsLeft);
        Assert.Equal(def.GoldFor(value), Gold - a.GoldLeft);
        Assert.True(Shards - a.ShardsLeft > ImbueCatalogue.MostShardsOfOthers());
    }

    [Fact]
    public void AnotherPropertyKeepsItsScaling()
    {
        using var a = new Artificer(0);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);
        ClusterFImbueKnowledge.Grant(a.Data, "Swing Speed Increase", ImbueTier.Full);

        Imbue(a, sword, "Swing Speed Increase", 3);

        Assert.Equal(60, sword.Attributes.WeaponSpeed);
        Assert.Equal(Shards - 8, a.ShardsLeft);
        Assert.Equal(Gold - 3500, a.GoldLeft);
    }
}
