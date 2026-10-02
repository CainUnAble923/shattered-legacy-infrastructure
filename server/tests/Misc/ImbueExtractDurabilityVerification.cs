// ImbueExtractDurabilityVerification.cs
//
// cc-P29 Part C, D48 (found in play 2026-10-02: a Longsword read 89 / 58 after two extractions). Extracting a
// property lowers the item's maximum durability by a fifth (ArtificersImbueGump.HandleExtractProperty) and, before
// cc-P29, never lowered the current value with it. Pinned's HitPoints setters clamp to the maximum
// (BaseWeapon.cs:386-388), but nothing re-clamps when the maximum drops. Notes in shard-migration
// notes/cc-P29-defect-batch-2.md, Part C.
//
// Every fact extracts through the Imbuing Table's own buttons (Disenchant target, the property's Extract row,
// Apply), as a player would.
//
// Facts:
//   1. A Longsword at 89 / 89 with two properties, extracted twice: 58 / 58, current never above max.
//   2. A plate chest at 60 / 60 with one property, extracted once: 48 / 48.

using System;
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
public class ImbueExtractDurabilityVerification
{
    // ArtificersImbueGump.BtnId: DisenchantTarget 6, ApplyExtract 9, ExtractPropBase 200 (+ index in the item's list).
    private const int BtnDisenchantTarget = 6;
    private const int BtnApplyExtract = 9;
    private const int BtnExtractBase = 200;

    private readonly ITestOutputHelper _out;

    public ImbueExtractDurabilityVerification(ITestOutputHelper output)
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
        public readonly Account Account = new($"p29{Guid.NewGuid():N}"[..16], "p29-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

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
            guild.GuildReputation["artificers"] = 15000;
        }

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

    // Opens the table, targets the item for disenchanting, picks the named property's Extract row and applies it.
    private static void Extract(Artificer a, Item item, string property)
    {
        var index = ImbueCatalogue.ForItem(item).FindIndex(d => d.Name == property);
        Assert.True(index >= 0, $"{property} is not in the catalogue for {item.GetType().Name}");

        var table = new ArtificersImbueGump(a.Pm);
        Press(table, a.Ns, BtnDisenchantTarget);
        Assert.NotNull(a.Pm.Target);

        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, item);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(view);

        var confirm = Next(a, view, BtnExtractBase + index);
        Assert.NotNull(confirm);
        Next(a, confirm, BtnApplyExtract);
    }

    [Fact]
    public void ALongswordExtractedTwiceNeverReadsAboveItsMaximum()
    {
        using var a = new Artificer();
        var sword = new Longsword();
        sword.Attributes.WeaponSpeed = 10;
        sword.Attributes.WeaponDamage = 10;
        sword.MaxHitPoints = 89;
        sword.HitPoints = 89;
        a.Pm.Backpack.DropItem(sword);

        Extract(a, sword, "Swing Speed Increase");
        _out.WriteLine($"after 1: {sword.HitPoints} / {sword.MaxHitPoints}, SSI {sword.Attributes.WeaponSpeed}");
        Assert.Equal(0, sword.Attributes.WeaponSpeed); // the extraction ran
        Assert.True(sword.HitPoints <= sword.MaxHitPoints, $"{sword.HitPoints} / {sword.MaxHitPoints} after one");

        Extract(a, sword, "Damage Increase");
        _out.WriteLine($"after 2: {sword.HitPoints} / {sword.MaxHitPoints}, DI {sword.Attributes.WeaponDamage}");
        Assert.Equal(0, sword.Attributes.WeaponDamage);
        Assert.Equal(58, sword.MaxHitPoints); // 89 - 17 = 72, 72 - 14 = 58
        Assert.Equal(58, sword.HitPoints);
    }

    [Fact]
    public void APlateChestExtractedOnceNeverReadsAboveItsMaximum()
    {
        using var a = new Artificer();
        var chest = new PlateChest();
        chest.Attributes.DefendChance = 10;
        chest.MaxHitPoints = 60;
        chest.HitPoints = 60;
        a.Pm.Backpack.DropItem(chest);

        Extract(a, chest, "Defense Chance Increase");
        _out.WriteLine($"after 1: {chest.HitPoints} / {chest.MaxHitPoints}, DCI {chest.Attributes.DefendChance}");
        Assert.Equal(0, chest.Attributes.DefendChance);
        Assert.Equal(48, chest.MaxHitPoints);
        Assert.Equal(48, chest.HitPoints);
    }
}
