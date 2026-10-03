// ImbueSelfRepairGateVerification.cs
//
// cc-P42 Part A (Chase, 2026-10-02). After F-27 (Self Repair 1 in 500 from high-end loot only), the Artificers' imbue
// stays a way to Self Repair, but much more expensive and only from Master Artificer (15,000 standing). Before this
// the gump sold it at 5 shards / 2,000 gold base from Artificer (5,000), the tier its discovery threshold gave.
// Numbers and the comparison table: shard-migration notes/cc-P42-defect-batch-3.md, Part A.
//
// Every imbue goes through the Imbuing Table's own buttons (Imbue target, the property's row, a tier, Apply), as a
// player's client would send them; the refusal is the server's own check, not a missing button.
//
// Facts:
//   1. Below Master (14,999 standing) the Self Repair row reads "[req: Master Artificer]", and Apply refuses: no
//      property, no shards, no gold taken. Weapon and armor alike.
//   2. At Master, Self Repair 6 on a weapon costs 70 shards and 70,000 gold (was 8 and 3,500).
//   3. Every other property keeps its base price and its threshold tier; Swing Speed Increase at Master still costs
//      what it did (14 shards, 6,125 gold at 78).

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
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
    // (+ tier).
    private const int BtnImbueTarget = 1;
    private const int BtnNextPage = 4;
    private const int BtnApplyImbue = 5;
    private const int BtnPropBase = 100;
    private const int BtnConfirmBase = 1000;

    private const int Shards = 1000;
    private const int Gold = 200000;

    private readonly ITestOutputHelper _out;

    public ImbueSelfRepairGateVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
        ClusterFGuildSystem.EnsureRegistered();
    }

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

        Mobile.SkillCheckLocationHandler ??= SkillCheck.Mobile_SkillCheckLocation;
    }

    private sealed class Artificer : IDisposable
    {
        public readonly Account Account = new($"p42{Guid.NewGuid():N}"[..16], "p42-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly CharacterGuildData Guild;
        public readonly ClusterFAccountData Data;

        public Artificer(int standing)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            // Above the hardest tier's top (difficulty 115 + 20 at Master, 120 + 20 at Arcane), so the skill check is
            // "no challenge" and always succeeds: the facts are about price and gate, not luck.
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

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static ArtificersImbueGump Next(Artificer a, Gump gump, int buttonId)
    {
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        Press(gump, a.Ns, buttonId);
        return a.Pm.FindGump<ArtificersImbueGump>();
    }

    private static ArtificersImbueGump View(Artificer a, Item item)
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

    // The table, the item, the property's row, the tier (5 = the rank's maximum), Apply. Buttons are pressed whether
    // or not the gump drew them, as a client could.
    private static void Imbue(Artificer a, Item item, string property, int tier)
    {
        var index = ImbueCatalogue.ForItem(item).FindIndex(d => d.Name == property);
        Assert.True(index >= 0, $"{property} is not in the catalogue for {item.GetType().Name}");

        var select = Next(a, View(a, item), BtnPropBase + index);
        Assert.NotNull(select);
        var confirm = Next(a, select, BtnConfirmBase + tier);
        Assert.NotNull(confirm);
        Next(a, confirm, BtnApplyImbue);
    }

    // The row's second label (value and note), paging with Next until the row shows.
    private static string RowNote(Artificer a, Item item, string property)
    {
        var gump = View(a, item);
        for (var page = 0; page < 10 && gump != null; page++)
        {
            var labels = gump.Entries.OfType<GumpLabel>().ToList();
            var row = labels.FindIndex(l => l.Text == property);
            if (row >= 0)
            {
                return labels[row + 1].Text;
            }

            gump = Next(a, gump, BtnNextPage);
        }

        Assert.Fail($"no {property} row on the table");
        return null;
    }

    [Fact]
    public void BelowMasterSelfRepairIsRefusedOnWeaponAndArmor()
    {
        using var a = new Artificer(14999);
        var sword = new Longsword();
        var chest = new PlateChest();
        a.Pm.Backpack.DropItem(sword);
        a.Pm.Backpack.DropItem(chest);

        foreach (var (item, property) in new (Item, string)[] { (sword, "Self Repair"), (chest, "Self Repair (Armor)") })
        {
            var def = ImbueCatalogue.ForItem(item).Find(d => d.Name == property);
            Assert.Equal(15000, def.RequiredStanding);
            Assert.Equal("Master Artificer", def.RequiredRankName);

            var note = RowNote(a, item, property);
            _out.WriteLine($"{property} at 14,999: '{note}'");
            Assert.EndsWith("[req: Master Artificer]", note);

            Imbue(a, item, property, 5);
        }

        Assert.Equal(0, sword.WeaponAttributes.SelfRepair);
        Assert.Equal(0, chest.ArmorAttributes.SelfRepair);
        Assert.Equal(Shards, a.ShardsLeft);
        Assert.Equal(Gold, a.GoldLeft);
    }

    [Fact]
    public void AtMasterSelfRepairIsImbuedAtTheNewPrice()
    {
        using var a = new Artificer(15000);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);

        var def = ImbueCatalogue.ForItem(sword).Find(d => d.Name == "Self Repair");
        Assert.Equal(6, def.GetMaxForStanding(15000));
        _out.WriteLine($"Self Repair 6 at Master: {def.ShardsFor(6, 15000)} shards, {def.GoldFor(6, 15000):N0} gold");

        Imbue(a, sword, "Self Repair", 5);

        Assert.Equal(6, sword.WeaponAttributes.SelfRepair);
        Assert.Equal(Shards - 70, a.ShardsLeft);
        Assert.Equal(Gold - 70000, a.GoldLeft);
    }

    [Fact]
    public void OtherPropertiesKeepTheirPriceAndTier()
    {
        foreach (var def in ImbueCatalogue.All)
        {
            if (def.Name.StartsWith("Self Repair", StringComparison.Ordinal))
            {
                Assert.Equal(ImbueCatalogue.SelfRepairShards, def.ShardBase);
                Assert.Equal(ImbueCatalogue.SelfRepairGold, def.GoldBase);
                continue;
            }

            Assert.Equal(-1, def.MinStandingOverride);
            Assert.Equal(ArtificersGuildmasterGump.GetMinStanding(def.DiscoveryThreshold), def.RequiredStanding);
            Assert.True(def.ShardBase < ImbueCatalogue.SelfRepairShards, def.Name);
            Assert.True(def.GoldBase < ImbueCatalogue.SelfRepairGold, def.Name);
        }

        using var a = new Artificer(15000);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);
        Imbue(a, sword, "Swing Speed Increase", 5);

        Assert.Equal(78, sword.Attributes.WeaponSpeed);
        Assert.Equal(Shards - 14, a.ShardsLeft);
        Assert.Equal(Gold - 6125, a.GoldLeft);
    }
}
