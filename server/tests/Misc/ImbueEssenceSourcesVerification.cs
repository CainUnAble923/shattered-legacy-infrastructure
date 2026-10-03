// ImbueEssenceSourcesVerification.cs
//
// cc-P27, Part B (Chase, 2026-10-01): Imbuing essences are drawn from the pack first, then the bank, through
// GuildResources (F-11), with its order and exclusions. Notes in shard-migration notes/cc-P27-defect-batch-1.md.
//
// Every fact imbues through the Imbuing Table's own buttons (target the item, pick the property, pick tier 1,
// Apply), as a player would. The property is Swing Speed Increase, the first weapon property; at standing 15,000
// its tier 1 has difficulty about 54, so Imbuing 100 always passes the skill check.
//
// Facts:
//   1. An essence in the pack only: the imbue succeeds and takes it.
//   2. An essence in the bank only: the imbue succeeds and takes it (pack-only before cc-P27).
//   3. One in the pack and two in the bank: the pack's is taken, both bank ones stay, and the gump says where they
//      are ("3 essences of Swing Speed Increase: 1 in pack, 2 in bank").
//   4. None of the right property anywhere (other properties' essences in both): refused, and nothing is taken from
//      the pack, the bank, the shards or the gold; the item is unchanged.
//   5. An essence inside the Artificers' Essence Satchel in the pack is used, after a loose one in the bank is not:
//      pack before bank is the outer rule, satchel or not.

using System;
using System.Collections.Generic;
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
public class ImbueEssenceSourcesVerification
{
    private const string Property = "Swing Speed Increase";
    private const string Other = "Damage Increase";

    // ArtificersImbueGump.BtnId: ImbueTarget 1, ApplyImbue 5, PropBase 100 (+ index in the item's list),
    // ConfirmBase 1000 (+ tier).
    private const int BtnImbueTarget = 1;
    private const int BtnApplyImbue = 5;
    private const int BtnFirstProperty = 100;
    private const int BtnTier1 = 1001;

    private const int Shards = 100;
    private const int StartGold = 5000;

    private readonly ITestOutputHelper _out;

    public ImbueEssenceSourcesVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
        ClusterFGuildSystem.EnsureRegistered();
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

        // A real skill check: SkillCheck's handler and AntiMacro's settings (cc-P46 Part F, ShardTestHost; the fixture
        // patch makes both too).
        ShardTestHost.EnsureSkillChecks();
    }

    private sealed class Artificer : IDisposable
    {
        public readonly Account Account = new($"p27{Guid.NewGuid():N}"[..16], "p27-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly Longsword Sword;

        public Artificer()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            Pm.Skills[SkillName.Imbuing].Base = 100.0;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1230, 1230, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            var guild = ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);
            guild.JoinedGuilds.Add("artificers");
            guild.GuildCurrency["artificers"] = Shards;
            guild.GuildReputation["artificers"] = 15000;

            Pack.DropItem(new Gold(StartGold));
            Sword = new Longsword();
            Pack.DropItem(Sword);
        }

        public Container Pack => Pm.Backpack;
        public BankBox Bank => Pm.BankBox;

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public int Discoveries => ClusterFAccountPersistence.GetOrCreate(Account).GetDiscoveryCount(Property);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static PropertyEssence Essence(Container c, string key = Property)
    {
        var e = new PropertyEssence(key);
        c.DropItem(e);
        return e;
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // Presses a button on `gump` and returns the Imbuing Table gump it sends next. The test NetState keeps every
    // gump sent (OnResponse is called directly, not through the network), so the old ones are closed first.
    private static ArtificersImbueGump Next(Artificer a, Gump gump, int buttonId)
    {
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        Press(gump, a.Ns, buttonId);
        return a.Pm.FindGump<ArtificersImbueGump>();
    }

    private static string Text(Gump gump) =>
        string.Join("\n", gump.Entries.Select(e => e switch
        {
            GumpLabel l => l.Text,
            GumpHtml h  => h.Text,
            _           => null
        }).Where(t => t != null));

    // Opens the table, targets the sword, picks Swing Speed Increase at tier 1 and presses Apply. Returns the
    // Confirm stage's text, read before Apply.
    private string Imbue(Artificer a)
    {
        var table = new ArtificersImbueGump(a.Pm);
        Press(table, a.Ns, BtnImbueTarget);
        Assert.NotNull(a.Pm.Target);

        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, a.Sword);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(view);

        var select = Next(a, view, BtnFirstProperty);
        Assert.NotNull(select);
        Assert.Contains($"Imbue: {Property}", Text(select));

        var confirm = Next(a, select, BtnTier1);
        Assert.NotNull(confirm);
        var text = Text(confirm);
        _out.WriteLine($"confirm stage:\n{text}");

        Next(a, confirm, BtnApplyImbue);
        return text;
    }

    private static List<PropertyEssence> Left(Container c, string key = Property)
    {
        var left = new List<PropertyEssence>();
        foreach (var e in c.FindItemsByType<PropertyEssence>())
        {
            if (!e.Deleted && e.PropertyKey == key)
            {
                left.Add(e);
            }
        }

        return left;
    }

    // ---------------------------------------------------------------- facts

    [Fact]
    public void AnEssenceInThePackOnlyIsTaken()
    {
        using var a = new Artificer();
        Essence(a.Pack);

        Imbue(a);

        _out.WriteLine($"SSI={a.Sword.Attributes.WeaponSpeed} pack={Left(a.Pack).Count} discoveries={a.Discoveries}");
        Assert.True(a.Sword.Attributes.WeaponSpeed > 0, "the imbue did not apply");
        Assert.Empty(Left(a.Pack));
        Assert.Equal(1, a.Discoveries);
    }

    [Fact]
    public void AnEssenceInTheBankOnlyIsTaken()
    {
        using var a = new Artificer();
        Essence(a.Bank);

        Assert.Equal(new GuildStock(0, 1), ArtificersImbueGump.CountEssences(a.Pm, Property));

        Imbue(a);

        _out.WriteLine($"SSI={a.Sword.Attributes.WeaponSpeed} bank={Left(a.Bank).Count} discoveries={a.Discoveries}");
        Assert.True(a.Sword.Attributes.WeaponSpeed > 0, "an essence in the bank was not used");
        Assert.Empty(Left(a.Bank));
        Assert.Equal(1, a.Discoveries);
    }

    [Fact]
    public void SplitBetweenPackAndBankThePacksIsTakenAndTheGumpSaysWhere()
    {
        using var a = new Artificer();
        var inPack = Essence(a.Pack);
        var bank1 = Essence(a.Bank);
        var bank2 = Essence(a.Bank);

        Assert.Equal(
            "3 essences of Swing Speed Increase: 1 in pack, 2 in bank",
            ArtificersImbueGump.DescribeEssences(a.Pm, Property));

        var confirmText = Imbue(a);

        Assert.Contains("you have 3 essences of Swing Speed Increase: 1 in pack, 2 in bank", confirmText);
        Assert.True(a.Sword.Attributes.WeaponSpeed > 0, "the imbue did not apply");
        Assert.True(inPack.Deleted, "the pack's essence should be used first");
        Assert.False(bank1.Deleted);
        Assert.False(bank2.Deleted);
        Assert.Equal(2, Left(a.Bank).Count);
    }

    [Fact]
    public void NoneOfTheRightPropertyIsRefusedAndNothingIsTaken()
    {
        using var a = new Artificer();
        var otherPack = Essence(a.Pack, Other);
        var otherBank = Essence(a.Bank, Other);
        var goldBefore = CompactGoldHelper.GetTotalGold(a.Pm);

        var confirmText = Imbue(a);

        _out.WriteLine($"SSI={a.Sword.Attributes.WeaponSpeed} shards={a.Guild.GetCurrency("artificers")} " +
                       $"gold={CompactGoldHelper.GetTotalGold(a.Pm)} discoveries={a.Discoveries}");
        Assert.Contains("you have 0 essences of Swing Speed Increase: 0 in pack, 0 in bank", confirmText);
        Assert.Equal(0, a.Sword.Attributes.WeaponSpeed);
        Assert.False(otherPack.Deleted);
        Assert.False(otherBank.Deleted);
        Assert.Equal(Shards, a.Guild.GetCurrency("artificers"));
        Assert.Equal(goldBefore, CompactGoldHelper.GetTotalGold(a.Pm));
        Assert.Equal(0, a.Discoveries);
    }

    [Fact]
    public void AnEssenceInTheSatchelInThePackGoesBeforeALooseOneInTheBank()
    {
        using var a = new Artificer();
        var satchel = new ArtificersSatchel();
        a.Pack.DropItem(satchel);
        var inSatchel = Essence(satchel);
        var inBank = Essence(a.Bank);

        Assert.Equal(new GuildStock(1, 1), ArtificersImbueGump.CountEssences(a.Pm, Property));

        Imbue(a);

        Assert.True(a.Sword.Attributes.WeaponSpeed > 0, "the imbue did not apply");
        Assert.True(inSatchel.Deleted, "the satchel in the pack should be used before the bank");
        Assert.False(inBank.Deleted);
    }
}
