// ImbueAllSlayersVerification.cs
//
// cc-P173 (Chase 2026-10-18 15:16, test shard, a War Axe with Snow Elemental Slayer: "imbuing is not seeing slayer as a
// disenchantable property"). The imbue catalogue (ArtificersImbueGump.cs, ImbueCatalogue) registered 14 of pinned's 28
// slayers, so a weapon with any of the other 13 showed no slayer to extract, disenchant or imbue.
//
// Facts:
//   1. A War Axe with Summer Wind (snow elemental slayer): the disenchant view's list (GetActiveProperties) has it, and
//      extracting it through the Imbuing Table's own buttons clears the slayer and delivers its essence.
//   2. That essence's row imbues the slayer onto another weapon.
//   3. Every SlayerName but None has exactly one catalogue row, and that row reads and writes that slayer.
//   4. The 14 rows saved essences already name keep their names (PropertyEssence.PropertyKey matches the row name).
//   5. The super slayers are OSI's seven (ServUO ItemPropertyInfo.cs:429-433, IDs 121-127); every other slayer is lesser.

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
public class ImbueAllSlayersVerification
{
    // ArtificersImbueGump.BtnId: DisenchantTarget 6, ApplyExtract 9, ExtractPropBase 200 (+ index in the item's list).
    private const int BtnDisenchantTarget = 6;
    private const int BtnApplyExtract = 9;
    private const int BtnExtractBase = 200;

    private const string SnowElemental = "Slayer: Summer Wind (Snow Elemental)";

    private readonly ITestOutputHelper _out;

    public ImbueAllSlayersVerification(ITestOutputHelper output)
    {
        _out = output;
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

        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private static bool _startupHooksRun;

    private sealed class Artificer : IDisposable
    {
        public readonly Account Account = new($"p173{Guid.NewGuid():N}"[..16], "p173-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Artificer()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            Pm.Skills[SkillName.Imbuing].Base = 100.0;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1232, 1232, 0), Map.Trammel);

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

    private static IEnumerable<Item> Deep(Container c) =>
        c.Items.SelectMany(i => i is Container sub ? Deep(sub).Prepend(i) : [i]);

    private static List<ImbuePropertyDef> SlayerRows => ImbueCatalogue.All.Where(d => d.Group == "Slayer").ToList();

    // ---------------------------------------------------------------- 1, 2

    [Fact]
    public void ASnowElementalSlayerWarAxeIsSeenExtractedAndImbuedAgain()
    {
        using var a = new Artificer();
        var axe = new WarAxe { Slayer = SlayerName.SummerWind };
        a.Pm.Backpack.DropItem(axe);

        var active = ImbueCatalogue.GetActiveProperties(axe).Select(p => p.Def.Name).ToList();
        _out.WriteLine($"disenchant sees: {string.Join(", ", active)}");
        Assert.Contains(SnowElemental, active);

        var index = ImbueCatalogue.ForItem(axe).FindIndex(d => d.Name == SnowElemental);
        Assert.True(index >= 0, $"{SnowElemental} is not in the catalogue for a War Axe");

        var table = new ArtificersImbueGump(a.Pm);
        Press(table, a.Ns, BtnDisenchantTarget);
        Assert.NotNull(a.Pm.Target);
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, axe);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(view);
        var confirm = Next(a, view, BtnExtractBase + index);
        Assert.NotNull(confirm);
        Next(a, confirm, BtnApplyExtract);

        Assert.Equal(SlayerName.None, axe.Slayer);
        var essence = Deep(a.Pm.Backpack).OfType<PropertyEssence>().SingleOrDefault();
        Assert.NotNull(essence);
        Assert.Equal(SnowElemental, essence.PropertyKey);

        var row = ImbueCatalogue.All.Single(d => d.Name.Equals(essence.PropertyKey, StringComparison.OrdinalIgnoreCase));
        var sword = new Longsword();
        try
        {
            row.Set(sword, 1);
            Assert.Equal(SlayerName.SummerWind, sword.Slayer);
            Assert.Equal(1, row.Get(sword));
        }
        finally
        {
            sword.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EverySlayerNameHasExactlyOneRow()
    {
        var missing = new List<SlayerName>();
        foreach (var sn in Enum.GetValues<SlayerName>().Where(s => s != SlayerName.None))
        {
            var axe = new WarAxe { Slayer = sn };
            var blank = new WarAxe();
            try
            {
                var rows = SlayerRows.Where(d => d.Get(axe) == 1).ToList();
                if (rows.Count != 1)
                {
                    missing.Add(sn);
                    continue;
                }

                rows[0].Set(blank, 1);
                Assert.Equal(sn, blank.Slayer);
                rows[0].Set(blank, 0);
                Assert.Equal(SlayerName.None, blank.Slayer);
            }
            finally
            {
                axe.Delete();
                blank.Delete();
            }
        }

        _out.WriteLine($"{SlayerRows.Count} slayer rows: {string.Join("; ", SlayerRows.Select(d => d.Name))}");
        Assert.True(missing.Count == 0, $"no single row for: {string.Join(", ", missing)}");
        Assert.Equal(Enum.GetValues<SlayerName>().Length - 1, SlayerRows.Count);
    }

    // ---------------------------------------------------------------- 4

    [Theory]
    [InlineData("Slayer: Silver (Undead)")]
    [InlineData("Slayer: Repond (Humanoid)")]
    [InlineData("Slayer: Dragon Slaying")]
    [InlineData("Slayer: Daemon Dismissal")]
    [InlineData("Slayer: Fey")]
    [InlineData("Slayer: Elemental Health")]
    [InlineData("Slayer: Arachnid Doom")]
    [InlineData("Slayer: Reptilian Death")]
    [InlineData("Slayer: Troll Slaughter")]
    [InlineData("Slayer: Ogre Thrashing")]
    [InlineData("Slayer: Snake's Bane")]
    [InlineData("Slayer: Spider's Death")]
    [InlineData("Slayer: Scorpion's Bane")]
    [InlineData("Slayer: Ophidian")]
    public void AnOldEssenceKeyStillResolves(string key)
    {
        var essence = new PropertyEssence(key);
        try
        {
            Assert.Single(ImbueCatalogue.All, d => d.Name.Equals(essence.PropertyKey, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            essence.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    // The table's buttons are PropBase 100 + the row's index in ForItem (imbue), ExtractPropBase 200 + that index
    // (extract) and CraftPropBase 300 + the index in All (craft), below ConfirmBase 1000 (ArtificersImbueGump.BtnId).
    // The 13 rows cc-P173 added must not push one range into the next.
    [Fact]
    public void TheButtonRangesStillDoNotOverlap()
    {
        Item[] items = [new WarAxe(), new PlateChest(), new GoldRing(), new Robe(), new Spellbook()];
        try
        {
            foreach (var item in items)
            {
                var count = ImbueCatalogue.ForItem(item).Count;
                _out.WriteLine($"{item.GetType().Name}: {count} rows");
                Assert.True(count < 100, $"{item.GetType().Name} has {count} rows; imbue buttons would reach the extract range");
            }

            _out.WriteLine($"All: {ImbueCatalogue.All.Count} rows");
            Assert.True(ImbueCatalogue.All.Count < 700);
        }
        finally
        {
            foreach (var item in items)
            {
                item.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheSuperSlayersAreOsisSeven()
    {
        SlayerName[] super =
        [
            SlayerName.Silver, SlayerName.Repond, SlayerName.ReptilianDeath, SlayerName.Exorcism, SlayerName.ArachnidDoom,
            SlayerName.ElementalBan, SlayerName.Fey
        ];

        foreach (var sn in Enum.GetValues<SlayerName>().Where(s => s != SlayerName.None))
        {
            var axe = new WarAxe { Slayer = sn };
            try
            {
                var row = SlayerRows.Single(d => d.Get(axe) == 1);
                Assert.Equal(super.Contains(sn) ? ImbueFamilies.SuperSlayer : ImbueFamilies.Slayer, row.KnowledgeKey);
            }
            finally
            {
                axe.Delete();
            }
        }
    }
}
