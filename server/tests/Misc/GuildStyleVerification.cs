// GuildStyleVerification.cs
//
// cc-P436 (Chase's pick 2026-10-25: look B, stone and bronze). The guild and League gumps share one look, applied by
// GuildStyle as each gump is sent: our frame 23200 (the pieces 23200-23208 in the player package), no panel or alpha
// region inside it, the bronze edge as every rule, for our patched TazUO only. Notes: shard-migration
// notes/cc-P436-guild-league-gump-look.md.
//
// Facts:
//   S1. Every guild and League gump in step 1's table is a GuildStyledGump (one helper, so they change together).
//   S2. The real gumps, opened by a Journeyman for two clients: our client's copy has frame 23200 and no 9270, nothing
//       inside the frame, every rule our edge, and the same buttons, labels and text as the stock copy; a stock
//       client's copy is exactly what the gump built (no ID of ours). Both compile and send.
//   S3. With clusterf.guildStyle.enabled off, our client gets stock too.
//   S4. Restyle itself: only a 9270 frame, a panel or alpha region filling it (0 to 12 pixels in), and 9304 rules
//       change; a rule across takes the top edge, a rule down the left edge; other art, buttons and a mid-gump alpha
//       region are left alone.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRegister;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildStyleVerification
{
    private readonly ITestOutputHelper _out;

    public GuildStyleVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
    }

    // Step 1's table (notes/cc-P436-guild-league-gump-look.md, section 1), as names: a class that moves or is renamed
    // fails here rather than silently keeping the stock look.
    private static readonly string[] StyledGumps =
    [
        "GuildMenuGump", "MinersCompactLiaisonGump", "SmithGuildmasterGump", "SmithSealsInfoGump",
        "ForestersGuildmasterGump", "ForestersLogbookGump", "OutridersGuildmasterGump", "CrookActionsGump",
        "ArtificersGuildmasterGump", "ArtificersImbueGump", "ArtificersMasteryGump", "ArtificerWorkOrderGump",
        "SanitationWardenGump", "GuildContractLedgerGump", "CrossGuildExchangeGump", "SmithBulkOrderChoiceGump",
        "SmithCommissionGump", "SmithGuildBookGump", "SmithSealCatalogGump", "SmithTurnInChoiceGump",
        "RegularSmithBulkOrderGump", "GuildProgressGump", "GuildTaskDetailGump", "GuildWelcomeGump",
        "GuildStarterItemsGump", "GuildRecipeShopGump", "GuildSchematicsGump", "LeagueRegistrarGump",
        "LeagueRegisterGump", "CombinedLogbookGump", "LeagueHallFacingGump", "LeagueShopConfirmGump", "LeagueHallGump",
        "LeagueTitleGump"
    ];

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p436{Guid.NewGuid():N}"[..16], "p436-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(bool ourClient)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 50;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            if (ourClient)
            {
                // What our patched TazUO answers at login: the ClientCommand acknowledgement, enhanced packet id 4.
                TazUOClientCommand.OnEnhancedPacket(Ns, new SpanReader([0x00, 0x04]));
                Assert.True(TazUOClientCommand.CanRunCommands(Ns));
            }

            var guild = ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

            foreach (var key in new[] { "smithing", "mining", "artificers", "foresters", "rangers", "tailoring", "custodians" })
            {
                guild.JoinedGuilds.Add(key);
                guild.GuildReputation[key] = 6_200;
                guild.GuildCurrency[key] = 1_240;
            }

            Pm.Skills.Blacksmith.Cap = Pm.Skills.Mining.Cap = Pm.Skills.Imbuing.Cap = 120.0;
            Pm.Skills.Blacksmith.Base = 92.4;
            Pm.Skills.Mining.Base = 88.0;
            Pm.Skills.Imbuing.Base = 75.0;
            Pm.BankBox.DropItem(new Gold(12_000));
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    // The real gumps, as DumpsTodaysGuildMenus opens them (GuildMenuTemplateVerification), for one player.
    private static List<(string Name, Func<Gump> Open)> RealGumps(Player p, List<Mobile> npcs)
    {
        var pm = p.Pm;
        var acct = p.Account;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var def = (string key) => ClusterFGuildSystem.GetDef(key);

        T Npc<T>(T m) where T : Mobile
        {
            npcs.Add(m);
            return m;
        }

        return
        [
            ("GuildDirectory", () => new GuildProgressGump(pm, acct)),
            ("HallPage_tailoring", () => new GuildTaskDetailGump(pm, def("tailoring"), acct)),
            ("GuildWelcome", () => new GuildWelcomeGump(pm)),
            ("StarterItems_tailoring", () => new GuildStarterItemsGump(pm, def("tailoring"), Npc(new TailorGuildmaster()))),
            ("Smith_Guildmaster", () => new SmithGuildmasterGump(pm, def("smithing"), acct)),
            ("Smith_SealsInfo", () => new SmithSealsInfoGump()),
            ("Smith_SealCatalog", () => new SmithSealCatalogGump(pm, SmithSealCatalogGump.Cat.AncientHammers)),
            ("Smith_Commission", () => new SmithCommissionGump(pm)),
            ("Smith_BulkOrderChoice", () => new SmithBulkOrderChoiceGump(pm)),
            ("Smith_GuildBook", () => new SmithGuildBookGump(pm, new SmithGuildBook())),
            ("Mining_Liaison", () => new MinersCompactLiaisonGump(pm)),
            ("Mining_Dashboard", () => new MinersCompactLiaisonGump(pm, MinersCompactLiaisonGump.View.MemberDashboard)),
            ("Artificers_Guildmaster", () => new ArtificersGuildmasterGump(pm)),
            ("Artificers_Imbue", () => new ArtificersImbueGump(pm)),
            ("Artificers_Mastery", () => new ArtificersMasteryGump(pm, new ArtificersSatchel())),
            ("Artificers_WorkOrders", () => new ArtificerWorkOrderGump(pm)),
            ("Foresters_Guildmaster", () => new ForestersGuildmasterGump(pm)),
            ("Foresters_Logbook", () => new ForestersLogbookGump(pm, data)),
            ("Outriders_Guildmaster", () => new OutridersGuildmasterGump(pm)),
            ("Custodians_Warden", () => new SanitationWardenGump(pm, def("custodians"), acct)),
            ("Contracts_Ledger_smithing", () => new GuildContractLedgerGump(pm, "smithing")),
            ("MuleExchange", () => new CrossGuildExchangeGump(pm)),
            ("RecipeShop_tailoring", () => new GuildRecipeShopGump(pm, "tailoring", null)),
            ("Schematics_smithing", () => new GuildSchematicsGump(pm, "smithing", Npc(new BlacksmithGuildmaster()))),
            ("League_Registrar", () => new LeagueRegistrarGump(pm)),
            ("League_Register", () => new LeagueRegisterGump(pm, LeagueRegisterGump.Page.Guilds)),
            ("League_Titles", () => new LeagueTitleGump(pm))
        ];
    }

    // An entry as a comparable line: its type and every int, string and bool it carries.
    private static string Sig(GumpEntry e) =>
        e.GetType().Name + "(" + string.Join(",", e.GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(string) || p.PropertyType == typeof(bool))
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}={p.GetValue(e)}")) + ")";

    private static int Art(GumpEntry e) => e switch
    {
        GumpBackground b => b.GumpID,
        GumpImageTiled t => t.GumpID,
        _ => -1
    };

    private static bool IsLook(GumpEntry e) => e is GumpBackground or GumpImageTiled or GumpAlphaRegion;

    // ---------------------------------------------------------------- S1

    [Fact]
    public void S1_EveryGuildAndLeagueGumpIsOnTheSharedStyle()
    {
        var assembly = typeof(GuildStyledGump).Assembly;
        var missing = new List<string>();

        foreach (var name in StyledGumps)
        {
            var types = assembly.GetTypes().Where(t => t.Name == name && typeof(Gump).IsAssignableFrom(t)).ToList();
            Assert.True(types.Count == 1, $"{name}: {types.Count} gump classes of that name");

            if (!typeof(GuildStyledGump).IsAssignableFrom(types[0]))
            {
                missing.Add(name);
            }
        }

        Assert.True(missing.Count == 0, "not on GuildStyledGump: " + string.Join(", ", missing));
    }

    // ---------------------------------------------------------------- S2

    [Fact]
    public void S2_OurClientGetsStoneAndBronzeAndEveryOtherClientGetsStock()
    {
        var wasEnabled = GuildStyle.Enabled;
        GuildStyle.Enabled = true;
        var npcs = new List<Mobile>();

        try
        {
            using var ours = new Player(ourClient: true);
            using var stock = new Player(ourClient: false);
            Assert.False(TazUOClientCommand.CanRunCommands(stock.Ns));

            var ourGumps = RealGumps(ours, npcs);
            var stockGumps = RealGumps(stock, npcs);
            var failures = new List<string>();

            for (var i = 0; i < ourGumps.Count; i++)
            {
                var name = ourGumps[i].Name;
                var built = stockGumps[i].Open();
                var builtSigs = built.Entries.Select(Sig).ToList();

                // A stock client: sent exactly as built.
                stock.Pm.SendGump(built);
                if (!built.Entries.Select(Sig).SequenceEqual(builtSigs))
                {
                    failures.Add($"{name}: the stock client's copy changed");
                }

                if (built.Entries.Any(e => Art(e) is >= GuildStyle.Frame and <= GuildStyle.Frame + 8))
                {
                    failures.Add($"{name}: the stock client was sent our art");
                }

                // Our client.
                var styled = ourGumps[i].Open();
                ours.Pm.SendGump(styled);
                var entries = styled.Entries;

                if (built.Entries.Any(e => Art(e) == GuildStyle.StockFrame))
                {
                    if (!entries.OfType<GumpBackground>().Any(b => b.GumpID == GuildStyle.Frame))
                    {
                        failures.Add($"{name}: our client got no stone and bronze frame");
                    }

                    if (entries.Any(e => Art(e) == GuildStyle.StockFrame))
                    {
                        failures.Add($"{name}: our client still got frame 9270");
                    }

                    if (entries.OfType<GumpAlphaRegion>().Any(a => a.X <= 12 && a.Y <= 12))
                    {
                        failures.Add($"{name}: an alpha region still fills our frame");
                    }

                    if (entries.Any(e => Art(e) == RequirementStyle.PanelTile && e is GumpImageTiled { X: <= 12, Y: <= 12 }))
                    {
                        failures.Add($"{name}: the solid panel still fills our frame");
                    }
                }

                if (entries.Any(e => Art(e) == GuildStyle.StockRule))
                {
                    failures.Add($"{name}: our client still got a 9304 rule");
                }

                // Everything else (buttons, labels, text, item art) is the same as the stock copy, in the same order.
                var rest = entries.Where(e => !IsLook(e)).Select(Sig).ToList();
                var builtRest = built.Entries.Where(e => !IsLook(e)).Select(Sig).ToList();
                if (!rest.SequenceEqual(builtRest))
                {
                    failures.Add($"{name}: our copy's buttons or text differ from the stock copy");
                }

                _out.WriteLine($"{name}: {entries.Count} entries ours, {built.Entries.Count} stock");
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
        finally
        {
            GuildStyle.Enabled = wasEnabled;

            foreach (var m in npcs)
            {
                m.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- S3

    [Fact]
    public void S3_WithTheLookOffOurClientGetsStockToo()
    {
        var wasEnabled = GuildStyle.Enabled;

        try
        {
            using var ours = new Player(ourClient: true);
            GuildStyle.Enabled = false;

            var gump = new LeagueRegistrarGump(ours.Pm);
            var built = gump.Entries.Select(Sig).ToList();
            ours.Pm.SendGump(gump);

            Assert.Equal(built, gump.Entries.Select(Sig).ToList());
            Assert.Contains(gump.Entries, e => Art(e) == GuildStyle.StockFrame);
        }
        finally
        {
            GuildStyle.Enabled = wasEnabled;
        }
    }

    // ---------------------------------------------------------------- S4

    private sealed class Probe : Gump
    {
        public Probe() : base(0, 0)
        {
            AddBackground(0, 0, 400, 300, GuildStyle.StockFrame);
            AddAlphaRegion(6, 6, 388, 288);                             // the inside: goes
            AddImageTiled(8, 8, 384, 284, RequirementStyle.PanelTile);  // the inside, cc-P169's panel: goes
            AddAlphaRegion(50, 120, 100, 20);                           // a highlight mid-gump: stays
            AddImageTiled(100, 150, 50, 50, RequirementStyle.PanelTile); // a panel that is not the inside: stays
            AddImageTiled(10, 40, 380, 2, GuildStyle.StockRule);        // across: the top edge
            AddImageTiled(200, 44, 2, 200, GuildStyle.StockRule);       // down: the left edge
            AddBackground(20, 200, 60, 40, 9200);                       // other art: stays
            AddButton(18, 270, 4014, 4015, 1);
            AddLabel(40, 272, 0x481, "Back");
        }
    }

    [Fact]
    public void S4_RestyleChangesOnlyTheFrameItsInsideAndTheRules()
    {
        var gump = new Probe();
        var changed = GuildStyle.Restyle(gump);

        Assert.Equal(5, changed); // the frame, two insides, two rules
        Assert.Equal(
            new[]
            {
                "GumpBackground 0,0 400x300 23200",
                "GumpAlphaRegion 50,120 100x20",
                "GumpImageTiled 100,150 50x50 2624",
                "GumpImageTiled 10,40 380x2 23201",
                "GumpImageTiled 200,44 2x200 23203",
                "GumpBackground 20,200 60x40 9200",
                "GumpButton",
                "GumpLabel"
            },
            gump.Entries.Select(e => e switch
            {
                GumpBackground b => $"GumpBackground {b.X},{b.Y} {b.Width}x{b.Height} {b.GumpID}",
                GumpImageTiled t => $"GumpImageTiled {t.X},{t.Y} {t.Width}x{t.Height} {t.GumpID}",
                GumpAlphaRegion a => $"GumpAlphaRegion {a.X},{a.Y} {a.Width}x{a.Height}",
                _ => e.GetType().Name
            }).ToArray()
        );

        // Our frame's nine pieces and the edges the rules use are the registry's IDs.
        Assert.Equal(23200, GuildStyle.Frame);
        Assert.Equal(23201, GuildStyle.RuleAcross);
        Assert.Equal(23203, GuildStyle.RuleDown);
    }
}
