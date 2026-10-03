// SmithBODPathsVerification.cs
//
// cc-P32 Part B, PT-11: Smith BODs go through two paths on purpose (Chase, 2026-10-02). Regular smiths give
// and take them and pay OSI's rewards (pinned Blacksmith.cs:76-127, BaseVendor.cs:1078-1150); the Blacksmith
// Guildmaster and the Smithing Guild Book give and take them for Society members and pay Smith Seals,
// standing and skill checks (customizations/ClusterFSmithBODSystem.cs).
//
// Facts:
//   1. A deed crosses paths and pays once: a guild deed turned in at a regular smith, and a regular smith's
//      deed turned in at the guildmaster, are each accepted and deleted; a small combined into a large is
//      deleted by the combine, so it cannot be paid on its own.
//   2. The request gate holds at turn-in. cc-P32 made it Journeyman rank; cc-P42 Part F (Chase, 2026-10-03)
//      made it 70.1 Blacksmithy and nothing else, for requesting and turning in alike. A complete large deed
//      pays a member at 70.0 nothing at the guildmaster or the book and stays in the pack; at 70.1 with no
//      standing at all it pays.
//
// cc-P46 Part F (bug-list D56): fact 2 failed in Chase's 2026-10-03 09:40 rebuild with a NullReferenceException at
// pinned SkillCheck.cs:72. The turn-in's CheckSkill (ClusterFSmithBODSystem.cs:272) reached AntiMacroSystem.Settings,
// which the fixture patch did not configure (it installs SkillCheck's handlers) and only the craft facts did, so it
// depended on a craft fact running first; and on its random deed being of a coloured ore, since an iron deed's range
// (0-55) is "no challenge" at 70.1 and never reaches that line. The fixture patch configures AntiMacro now, both facts
// set up what they need (ShardTestHost), and fact 2's deed is a Dull Copper one (40-72), so its skill check is real
// every run.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithBODPathsVerification
{
    private readonly ITestOutputHelper _out;

    public SmithBODPathsVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
    }

    // ---------------------------------------------------------------- helpers

    // new Account(...) needs Accounts.Configure and a password algorithm; the turn-in's CheckSkill needs the skill-check
    // handler and AntiMacro's settings (cc-P46 Part F, ShardTestHost).
    private static void EnsureStartupHooks()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
    }

    private static readonly Point3D Spot = new(1500, 1500, 0);

    private static (Account, PlayerMobile) NewMember(int standing)
    {
        var account = new Account($"p32{Guid.NewGuid():N}"[..16], "p32-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        pm.Skills.Blacksmith.Base = 100.0;

        var guild = ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);
        guild.JoinedGuilds.Add("smithing");
        guild.AddReputation("smithing", standing);
        return (account, pm);
    }

    private static void Cleanup(Account account, params IEntity[] others)
    {
        foreach (var e in others)
        {
            e?.Delete();
        }

        ClusterFAccountPersistence.Get(account)?.ClearGuildData();
        if (account[0] is PlayerMobile pm)
        {
            pm.Delete();
        }

        Accounts.Remove(account);
    }

    private static CharacterGuildData Guild(Account account, PlayerMobile pm) =>
        ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);

    private static T Near<T>(T vendor) where T : Mobile
    {
        vendor.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return vendor;
    }

    private static SmallSmithBOD CompleteSmall() =>
        new(10, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None);

    // cc-P46 Part F: a complete Dull Copper plate deed, so the turn-in's skill range (40-72, GetSkillRange) holds 70.1
    // and its CheckSkill is a real check, not the "no challenge" an iron deed (0-55) gives.
    private static LargeSmithBOD CompleteDullCopperLarge()
    {
        var deed = new LargeSmithBOD(10, false, BulkMaterialType.DullCopper, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        foreach (var entry in deed.Entries)
        {
            entry.Amount = deed.AmountMax;
        }

        Assert.True(deed.Complete);
        return deed;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ADeedCrossesPathsAndPaysOnce()
    {
        var (account, pm) = NewMember(10_000);
        var smith = Near(new Blacksmith());
        var master = Near(new BlacksmithGuildmaster());

        try
        {
            // A deed the guild issued, turned in at a regular smith: OSI's reward, deed gone.
            var guildDeed = CompleteSmall();
            pm.Backpack.DropItem(guildDeed);
            var goldBefore = pm.Backpack.GetAmount(typeof(Gold));
            Assert.True(smith.OnDragDrop(pm, guildDeed));
            Assert.True(guildDeed.Deleted);
            _out.WriteLine($"regular smith paid {pm.Backpack.GetAmount(typeof(Gold)) - goldBefore} gold for a guild deed");

            // A regular smith's deed turned in at the guildmaster: Seals, deed gone.
            pm.NextBODTurnInTime = DateTime.MinValue;
            var vendorDeed = CompleteSmall();
            pm.Backpack.DropItem(vendorDeed);
            var seals = Guild(account, pm).GetCurrency("smithing");
            Assert.True(master.OnDragDrop(pm, vendorDeed));
            Assert.True(vendorDeed.Deleted);
            Assert.True(Guild(account, pm).GetCurrency("smithing") > seals);

            // A small combined into a large is consumed by the combine (pinned LargeBOD.cs:148).
            var large = new LargeSmithBOD();
            var entry = large.Entries[0];
            var small = new SmallSmithBOD(large.AmountMax, large.AmountMax, entry.Details.Type, entry.Details.Number,
                entry.Details.Graphic, large.RequireExceptional, large.Material);
            pm.Backpack.DropItem(large);
            pm.Backpack.DropItem(small);
            large.EndCombine(pm, small);
            Assert.True(small.Deleted);
            Assert.Equal(large.AmountMax, entry.Amount);
            large.Delete();
        }
        finally
        {
            Cleanup(account, smith, master);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ALargeDeedNeedsSeventyPointOneBlacksmithyAtTheGuild()
    {
        var (account, pm) = NewMember(0);
        pm.Skills.Blacksmith.Base = 70.0;
        var master = Near(new BlacksmithGuildmaster());

        try
        {
            var deed = CompleteDullCopperLarge();
            pm.Backpack.DropItem(deed);

            var seals = Guild(account, pm).GetCurrency("smithing");
            var standing = Guild(account, pm).GetReputation("smithing");

            var atMaster = master.OnDragDrop(pm, deed);
            var atBook = BlacksmithGuildmaster.TurnInBOD(pm, deed);
            _out.WriteLine($"at 70.0 Blacksmithy, {standing} standing: guildmaster {atMaster}, book {atBook}; " +
                           $"seals {seals} -> {Guild(account, pm).GetCurrency("smithing")}");

            Assert.False(atMaster);
            Assert.False(atBook);
            Assert.False(deed.Deleted);
            Assert.True(deed.IsChildOf(pm.Backpack));
            Assert.Equal(seals, Guild(account, pm).GetCurrency("smithing"));
            Assert.Equal(standing, Guild(account, pm).GetReputation("smithing"));

            // At 70.1, still with no standing, it pays.
            pm.Skills.Blacksmith.Base = 70.1;
            Assert.True(master.OnDragDrop(pm, deed));
            Assert.True(deed.Deleted);
            Assert.True(Guild(account, pm).GetCurrency("smithing") > seals);
            _out.WriteLine($"at 70.1, 0 standing: seals {seals} -> {Guild(account, pm).GetCurrency("smithing")}");
        }
        finally
        {
            Cleanup(account, master);
        }
    }
}
