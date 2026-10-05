// SmithBackButtonsVerification.cs
//
// cc-P52 Part E (bug-list D65, Chase 2026-10-04). The Smith Seal catalog had only Close: no way back to the menu that
// opened it (the guild book, SmithGuildBook.cs, or the guildmaster gump, SmithGuildmasterGump.cs). Its footer note
// "* Not yet available ..." was a label at H - 28 that ran under the Close label and OK button. The other sub-gumps of
// the guildmaster menu were checked for the same gap: Commissions, Smithing Seals info, the Civic Mule Exchange and
// the bulk order choice had no Back; the contract ledger drew one that did nothing for the smith guild. Each now takes
// a Back from its opener and keeps it through its own refreshes.
//
// Facts:
//   E1. The catalog opened from the guildmaster has Back, kept through a category switch and a buy; Back reopens the
//       guildmaster gump.
//   E2. Opened from the guild book's Commissions page, Back reopens the book on that page.
//   E3. Every sub-gump the guildmaster opens draws Back, and Back reopens the guildmaster gump.
//   E4. The contract ledger's Back reopens the guildmaster gump for the smith guild.
//   E5. Layout: the coming-soon note is HTML that ends above the footer line, on the longest category too, and is no
//       longer a label in the footer.

using System;
using System.Linq;
using System.Reflection;
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
public class SmithBackButtonsVerification
{
    private readonly ITestOutputHelper _out;

    public SmithBackButtonsVerification(ITestOutputHelper output)
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
    }

    private sealed class Smith : IDisposable
    {
        public readonly Account Account = new($"p52e{Guid.NewGuid():N}"[..16], "p52-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly CharacterGuildData Guild;

        public Smith()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1540, 1530, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Guild = ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);
            Guild.JoinedGuilds.Add("smithing");
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
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static readonly MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(NetState), typeof(BaseGump)], null);

    // As the client does: the gump leaves the open list, then its response runs.
    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    private static Gump Open(NetState ns, Type type)
    {
        foreach (var g in ns.GetGumps())
        {
            if (g.GetType() == type)
            {
                return g as Gump;
            }
        }

        return null;
    }

    private static bool Draws(Gump gump, int buttonId) =>
        gump.Entries.OfType<GumpButton>().Any(b => b.Type == GumpButtonType.Reply && b.ButtonID == buttonId);

    private static SmithGuildmasterGump OpenGuildmaster(Smith s)
    {
        var def = ClusterFGuildSystem.GetDef("smithing");
        Assert.NotNull(def);
        var gm = new SmithGuildmasterGump(s.Pm, def, s.Account);
        s.Pm.SendGump(gm);
        return gm;
    }

    // ---------------------------------------------------------------- E1

    [Fact]
    public void TheCatalogFromTheGuildmasterGoesBackThroughSwitchesAndBuys()
    {
        using var s = new Smith();
        var gm = OpenGuildmaster(s);

        Press(gm, s.Ns, 6); // Seal Catalog
        var catalog = s.Pm.FindGump<SmithSealCatalogGump>();
        Assert.NotNull(catalog);
        Assert.True(Draws(catalog, SmithSealCatalogGump.BtnBack), "Back on the first page");

        Press(catalog, s.Ns, (int)SmithSealCatalogGump.Cat.RunicVanilla + 1);
        catalog = s.Pm.FindGump<SmithSealCatalogGump>();
        Assert.NotNull(catalog);
        Assert.True(Draws(catalog, SmithSealCatalogGump.BtnBack), "Back after a category switch");

        s.Guild.AddCurrency("smithing", 1_000);
        Press(catalog, s.Ns, 100); // Dull Copper Runic Hammer, 200
        Assert.Single(s.Pm.Backpack.Items.OfType<RunicHammer>());
        catalog = s.Pm.FindGump<SmithSealCatalogGump>();
        Assert.NotNull(catalog);
        Assert.True(Draws(catalog, SmithSealCatalogGump.BtnBack), "Back after a buy");

        Assert.Null(s.Pm.FindGump<SmithGuildmasterGump>());
        Press(catalog, s.Ns, SmithSealCatalogGump.BtnBack);
        Assert.NotNull(s.Pm.FindGump<SmithGuildmasterGump>());
        Assert.Null(s.Pm.FindGump<SmithSealCatalogGump>());
    }

    // ---------------------------------------------------------------- E2

    [Fact]
    public void TheCatalogFromTheGuildBookGoesBackToThatPage()
    {
        using var s = new Smith();
        var book = new SmithGuildBook();
        s.Pm.Backpack.DropItem(book);

        var page1 = new SmithGuildBookGump(s.Pm, book, 1);
        s.Pm.SendGump(page1);
        Press(page1, s.Ns, 4); // Seal Catalog
        var catalog = s.Pm.FindGump<SmithSealCatalogGump>();
        Assert.NotNull(catalog);
        Assert.True(Draws(catalog, SmithSealCatalogGump.BtnBack));

        Press(catalog, s.Ns, (int)SmithSealCatalogGump.Cat.RunicPostVal + 1);
        catalog = s.Pm.FindGump<SmithSealCatalogGump>();
        Press(catalog, s.Ns, SmithSealCatalogGump.BtnBack);

        var back = s.Pm.FindGump<SmithGuildBookGump>();
        Assert.NotNull(back);
        var page = (int)typeof(SmithGuildBookGump).GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(back)!;
        _out.WriteLine($"guild book reopened on page {page}");
        Assert.Equal(1, page);
    }

    // ---------------------------------------------------------------- E3

    [Theory]
    [InlineData(1, typeof(SmithBulkOrderChoiceGump), SmithBulkOrderChoiceGump.BtnBack)]
    [InlineData(3, typeof(SmithSealsInfoGump), SmithSealsInfoGump.BtnBack)]
    [InlineData(4, typeof(HammerRestoreGump), 1)]
    [InlineData(5, typeof(SmithCommissionGump), SmithCommissionGump.BtnBack)]
    [InlineData(6, typeof(SmithSealCatalogGump), SmithSealCatalogGump.BtnBack)]
    [InlineData(20, typeof(CrossGuildExchangeGump), CrossGuildExchangeGump.BtnBack)]
    public void EverySubGumpOfTheGuildmasterGoesBack(int menuButton, Type subGump, int backButton)
    {
        using var s = new Smith();
        var gm = OpenGuildmaster(s);
        Assert.True(Draws(gm, menuButton), $"the guildmaster gump draws button {menuButton}");

        Press(gm, s.Ns, menuButton);
        var sub = Open(s.Ns, subGump);
        Assert.NotNull(sub);
        Assert.Null(s.Pm.FindGump<SmithGuildmasterGump>());
        _out.WriteLine($"button {menuButton} opened {subGump.Name}; Back is button {backButton}");
        Assert.True(Draws(sub, backButton), $"{subGump.Name} draws Back");

        Press(sub, s.Ns, backButton);
        Assert.NotNull(s.Pm.FindGump<SmithGuildmasterGump>());
    }

    // ---------------------------------------------------------------- E4

    [Fact]
    public void TheSmithContractLedgerGoesBack()
    {
        using var s = new Smith();
        var gm = OpenGuildmaster(s);
        Press(gm, s.Ns, 2); // Guild Contracts
        var ledger = s.Pm.FindGump<GuildContractLedgerGump>();
        Assert.NotNull(ledger);

        Press(ledger, s.Ns, 1); // Back
        Assert.NotNull(s.Pm.FindGump<SmithGuildmasterGump>());
    }

    // ---------------------------------------------------------------- E5

    [Fact]
    public void TheComingSoonNoteClearsTheFooter()
    {
        using var s = new Smith();
        var gump = new SmithSealCatalogGump(s.Pm, SmithSealCatalogGump.Cat.RunicPostVal, _ => { });

        Assert.DoesNotContain(gump.Entries.OfType<GumpLabel>(), l => l.Text.Contains("Not yet available"));
        var note = Assert.Single(gump.Entries.OfType<GumpHtml>(), h => h.Text.Contains("Not yet available"));
        var footerTop = SmithSealCatalogGump.FooterTop;
        _out.WriteLine($"note at y {note.Y}, height {note.Height}, ends {note.Y + note.Height}; footer line at {footerTop}; " +
                       $"longest category {SmithSealCatalogGump.MostRows} rows would put it at " +
                       $"{SmithSealCatalogGump.NoteY(SmithSealCatalogGump.MostRows)}");

        Assert.True(note.Y + note.Height <= footerTop);
        Assert.True(SmithSealCatalogGump.NoteY(SmithSealCatalogGump.MostRows) + note.Height <= footerTop);

        // Nothing else in the footer band shares the note's rows.
        Assert.DoesNotContain(gump.Entries.OfType<GumpButton>(), b => b.Y >= note.Y && b.Y < note.Y + note.Height);
    }
}
