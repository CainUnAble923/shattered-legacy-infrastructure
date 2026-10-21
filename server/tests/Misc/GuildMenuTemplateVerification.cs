// GuildMenuTemplateVerification.cs
//
// cc-P177 (Chase 2026-10-18 20:30: "all guild menus need to be uniform in UI and style"). The proposed shared guild menu
// (GuildMenuGump) and its per-guild themes. No menu uses it yet: cc-P178 converts them once Chase approves the mockups.
// Notes: shard-migration notes/cc-P177-guild-menus-template-mockups.md.
//
// Facts:
//   T1. Every registered guild and the League has a theme; its name is the guild's own; its accent is none of the met,
//       not met, info, heading or disabled hues; its crest is a gold medallion (5545-5592) or none with placeholder art.
//   T2. Drawn, the template is opaque (frame 9270, tile 2624 under the inside, no alpha region); the header has the crest
//       (or the placeholder frame and item), the name in the accent hue and the rank line; every label and button is
//       inside the frame; the content stays right of the menu rule and above the footer rule.
//   T3. The open section is the selected art and an accent label with no button; every other section is a button
//       (10 + n). Pressing one reopens that section at page 1; next and previous move a page; at the first and last
//       page the arrow is gray art with no button, and pressing its id anyway does nothing.
//   T4. Requirements on the template are cc-P169's lines: a check or an X beside each, and the action live only when
//       every line is met.
//
// Not facts: DumpsTodaysGuildMenus and DumpsTheMockups write each gump's entries as one JSON line
// ("GUMPJSON <name> <json>") for the survey and mockup renders in the notes.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.CleanUpBritannia;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildMenuTemplateVerification
{
    private readonly ITestOutputHelper _out;

    public GuildMenuTemplateVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p177{Guid.NewGuid():N}"[..16], "p177-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
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

            // A Journeyman in the guilds the mockups show, with some of each currency.
            foreach (var key in new[] { "smithing", "mining", "artificers", "foresters", "rangers", "tailoring", "custodians" })
            {
                Guild.JoinedGuilds.Add(key);
                Guild.GuildReputation[key] = 6_200;
                Guild.GuildCurrency[key] = 1_240;
            }

            Pm.Skills.Blacksmith.Cap = Pm.Skills.Mining.Cap = Pm.Skills.Imbuing.Cap = 120.0;
            Pm.Skills.Blacksmith.Base = 92.4;
            Pm.Skills.Mining.Base = 88.0;
            Pm.Skills.Imbuing.Base = 75.0;
            Pm.BankBox.DropItem(new Gold(12_000));
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    // A menu on the template whose content is a delegate, so a test or a mockup draws what it needs. The helpers are
    // protected on the base; these wrappers let the delegate reach them.
    private sealed class Menu : GuildMenuGump
    {
        public static (int Section, int Page)? Reopened;

        private readonly Action<Menu, int> _content;

        public Menu(
            GuildMenuTheme theme, string subtitle, GuildMenuRank rank, string[] sections, int section, int page,
            int pageCount, bool hasBack, Action<Menu, int> content, int height = DefaultHeight
        ) : base(theme, height)
        {
            _content = content;
            Subtitle = subtitle;
            Rank = rank;
            Sections = sections;
            Section = section;
            Page = page;
            PageCount = pageCount;
            HasBack = hasBack;
            Build();
        }

        public int Bottom => ContentBottom;
        public int Tall => Height;

        protected override void DrawContent(int y) => _content(this, y);

        protected override void Reopen(PlayerMobile pm, int section, int page) => Reopened = (section, page);

        public int H(int y, string text) => Heading(y, text);
        public int T(int y, string text, int hue = RequirementStyle.InfoHue) => Text(y, text, hue);
        public int P(int y, int h, string text) => Paragraph(y, h, text);
        public int F(int y, string label, string value, int hue = RequirementStyle.HeadingHue) => Field(y, label, value, hue);
        public int A(int y, int id, string text, string note = null) => ActionRow(y, id, text, note);

        public int I(int y, int item, int hue, string name, string note, string price, bool can, int id) =>
            ItemRow(y, item, hue, name, note, price, can, id);

        public int R(int y, RequirementList list, int id, string action) => Requirements(y, list, id, action);
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static readonly string[] SmithSections =
        ["Overview", "Bulk Orders", "Contracts", "Seal Catalog", "Commissions", "Hammer", "Schematics", "Mule Exchange"];

    // ---------------------------------------------------------------- T1

    [Fact]
    public void T1_EveryGuildAndTheLeagueHasATheme()
    {
        var themed = GuildMenuThemes.All.Select(t => t.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reserved = new[]
        {
            RequirementStyle.MetHue, RequirementStyle.NotMetHue, RequirementStyle.InfoHue, RequirementStyle.HeadingHue,
            RequirementStyle.DisabledHue
        };

        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            Assert.Contains(def.Key, themed);
            Assert.Equal(def.Name, GuildMenuThemes.Get(def.Key).Name);
        }

        Assert.Contains(GuildMenuThemes.LeagueKey, themed);
        Assert.Equal(GuildProgressGump.LeagueRowName, GuildMenuThemes.Get(GuildMenuThemes.LeagueKey).Name);
        Assert.Equal(ClusterFGuildSystem.AllGuilds.Count + 1, GuildMenuThemes.All.Count);

        foreach (var theme in GuildMenuThemes.All)
        {
            Assert.DoesNotContain(theme.AccentHue, reserved);
            Assert.True(theme.HasCrest ? theme.CrestGump is >= 5545 and <= 5592 : theme.PlaceholderItem > 0, theme.Key);
        }

        // Two guilds are known to lack a crest until the art lane delivers one.
        Assert.Equal(new[] { "custodians", "keepers" }, GuildMenuThemes.All.Where(t => !t.HasCrest).Select(t => t.Key).Order());
        Assert.Equal(GuildMenuThemes.All.Count, GuildMenuThemes.All.Select(t => t.AccentHue).Distinct().Count());
    }

    // ---------------------------------------------------------------- T2

    private static Menu Plain(Player p, string key, int section = 0, int page = 0, int pages = 1,
        Action<Menu, int> content = null) =>
        new(GuildMenuThemes.Get(key), "Guildmaster", GuildMenuRank.For(key, p.Guild), SmithSections, section, page,
            pages, true, content ?? ((m, y) => m.T(y, "content")));

    [Fact]
    public void T2_TheFrameHeaderAndContentArea()
    {
        using var p = new Player();

        foreach (var key in new[] { "smithing", "custodians" })
        {
            var theme = GuildMenuThemes.Get(key);
            var g = Plain(p, key, content: (m, y) =>
            {
                y = m.H(y, "A heading");
                y = m.I(y, 0x13E3, 0, "An item", "a note", "100 Seals", true, 100);
                m.A(y, 101, "An action", "a note");
            });

            var bg = Assert.Single(g.Entries.OfType<GumpBackground>(), b => b.X == 0 && b.Y == 0);
            Assert.Equal((GuildMenuGump.FrameArt, GuildMenuGump.Width, g.Tall), (bg.GumpID, bg.Width, bg.Height));
            Assert.Contains(g.Entries.OfType<GumpImageTiled>(), t => t.GumpID == RequirementStyle.PanelTile && t.X == 8 && t.Y == 8);
            Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());

            if (theme.HasCrest)
            {
                Assert.Contains(g.Entries.OfType<GumpImage>(), i => i.GumpID == theme.CrestGump && i.Y < GuildMenuGump.HeaderHeight);
            }
            else
            {
                Assert.Contains(g.Entries.OfType<GumpBackground>(), b => b.X == GuildMenuGump.CrestX && b.Width == GuildMenuGump.CrestSize);
                Assert.Contains(g.Entries.OfType<GumpItem>(), i => i.ItemID == theme.PlaceholderItem && i.Y < GuildMenuGump.HeaderHeight);
            }

            Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == theme.Name && l.Hue == theme.AccentHue);
            Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == "Rank: Journeyman" || l.Text.StartsWith("Rank: "));

            foreach (var l in g.Entries.OfType<GumpLabel>())
            {
                Assert.InRange(l.X, 8, GuildMenuGump.Width - 30);
                Assert.InRange(l.Y, 8, g.Tall - 26);
            }

            foreach (var b in g.Entries.OfType<GumpButton>())
            {
                Assert.InRange(b.X, 8, GuildMenuGump.Width - 30);
                Assert.InRange(b.Y, 8, g.Tall - 26);
            }

            // The content (labels at or right of the content column, between the header and the footer) ends above the
            // footer's rule.
            var content = g.Entries.OfType<GumpLabel>()
                .Where(l => l.X >= GuildMenuGump.ContentX && l.Y > GuildMenuGump.HeaderHeight && l.Y < g.Tall - GuildMenuGump.FooterHeight)
                .ToList();
            Assert.Equal(6, content.Count);
            Assert.NotEmpty(content);
            Assert.All(content, l => Assert.True(l.Y + 18 <= g.Bottom, $"{l.Text} at {l.Y}"));
        }
    }

    // ---------------------------------------------------------------- T3

    [Fact]
    public void T3_SectionsAndPaging()
    {
        using var p = new Player();
        var g = Plain(p, "smithing", section: 3, page: 0, pages: 3);
        var accent = GuildMenuThemes.Get("smithing").AccentHue;

        var buttons = g.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToHashSet();
        for (var i = 0; i < SmithSections.Length; i++)
        {
            Assert.Equal(i != 3, buttons.Contains(GuildMenuGump.BtnSectionBase + i));
        }

        Assert.Contains(g.Entries.OfType<GumpImage>(), i => i.GumpID == GuildMenuGump.RowArtSelected);
        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == "Seal Catalog" && l.Hue == accent);
        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == "Page 1 of 3");

        // First page: previous is gray art, no button; next is live.
        Assert.DoesNotContain(GuildMenuGump.BtnPrevious, buttons);
        Assert.Contains(GuildMenuGump.BtnNext, buttons);
        Assert.Contains(g.Entries.OfType<GumpImage>(), i => i.GumpID == GuildMenuGump.BackArt && i.Hue == RequirementStyle.DisabledHue);

        Menu.Reopened = null;
        Press(g, p.Ns, GuildMenuGump.BtnPrevious);
        Assert.Null(Menu.Reopened);
        Press(g, p.Ns, GuildMenuGump.BtnNext);
        Assert.Equal<(int, int)?>((3, 1), Menu.Reopened);
        Press(g, p.Ns, GuildMenuGump.BtnSectionBase + 5);
        Assert.Equal<(int, int)?>((5, 0), Menu.Reopened);

        Menu.Reopened = null;
        Press(g, p.Ns, GuildMenuGump.BtnSectionBase + SmithSections.Length);
        Assert.Null(Menu.Reopened);

        // Last page: next is gray art, no button.
        var last = Plain(p, "smithing", section: 3, page: 2, pages: 3);
        Assert.DoesNotContain(last.Entries.OfType<GumpButton>(), b => b.ButtonID == GuildMenuGump.BtnNext);
        Menu.Reopened = null;
        Press(last, p.Ns, GuildMenuGump.BtnNext);
        Assert.Null(Menu.Reopened);
        Press(last, p.Ns, GuildMenuGump.BtnPrevious);
        Assert.Equal<(int, int)?>((3, 1), Menu.Reopened);
    }

    // ---------------------------------------------------------------- T4

    [Fact]
    public void T4_RequirementsAreTheP169Lines()
    {
        using var p = new Player();

        foreach (var met in new[] { true, false })
        {
            var list = new RequirementList()
                .Amount("Smithing Seals", met ? 30 : 10, 25)
                .Skill("Blacksmithy", 92.4, 90)
                .Info("Ingots count from your pack and bank.");
            var g = Plain(p, "smithing", content: (m, y) => m.R(y, list, 120, "Repair"));

            var labels = g.Entries.OfType<GumpLabel>().ToList();
            var seals = Assert.Single(labels, l => l.Text.StartsWith("Smithing Seals ") && l.X == GuildMenuGump.ContentX + RequirementStyle.TextIndent);
            Assert.Equal(met ? RequirementStyle.MetHue : RequirementStyle.NotMetHue, seals.Hue);
            Assert.Contains(g.Entries.OfType<GumpImage>(), i => i.Y >= seals.Y - 3 && i.Y <= seals.Y + 3 &&
                i.GumpID == (met ? RequirementStyle.CheckArt : RequirementStyle.CrossArt));
            Assert.Equal(met, g.Entries.OfType<GumpButton>().Any(b => b.ButtonID == 120));
            Assert.Contains(labels, l => l.Text == (met ? "All requirements met" : "1 requirement not met"));
        }
    }

    // ---------------------------------------------------------------- dumps

    private void Dump(string name, Gump g)
    {
        var entries = g.Entries.Select(e =>
        {
            var row = new Dictionary<string, object> { ["type"] = e.GetType().Name };
            foreach (var prop in e.GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(bool))
                {
                    row[prop.Name] = prop.GetValue(e);
                }
            }

            return row;
        });

        _out.WriteLine($"GUMPJSON {name} {System.Text.Json.JsonSerializer.Serialize(entries)}");
    }

    private void TryDump(string name, Func<Gump> open)
    {
        try
        {
            Dump(name, open());
        }
        catch (Exception e)
        {
            _out.WriteLine($"GUMPERR {name} {e.GetType().Name}: {e.Message}");
        }
    }

    // Not a fact: today's guild menus, as a Journeyman member sees them, for the survey renders.
    [Fact]
    public void DumpsTodaysGuildMenus()
    {
        using var p = new Player();
        var acct = p.Account;
        var pm = p.Pm;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var def = (string key) => ClusterFGuildSystem.GetDef(key);
        var npcs = new List<Mobile>();
        T Npc<T>(T m) where T : Mobile
        {
            npcs.Add(m);
            return m;
        }

        pm.Backpack.DropItem(new HammerOfHephaestus());
        pm.Backpack.DropItem(new JacobsPickaxe());

        TryDump("GuildDirectory", () => new GuildProgressGump(pm, acct));
        TryDump("HallPage_tailoring", () => new GuildTaskDetailGump(pm, def("tailoring"), acct));
        TryDump("HallPage_thieves", () => new GuildTaskDetailGump(pm, def("thieves"), acct));
        TryDump("HallPage_alembic", () => new GuildTaskDetailGump(pm, def("alembic"), acct));
        TryDump("GuildWelcome", () => new GuildWelcomeGump(pm));
        TryDump("StarterItems_tailoring", () => new GuildStarterItemsGump(pm, def("tailoring"), Npc(new TailorGuildmaster())));
        TryDump("Smith_Guildmaster", () => new SmithGuildmasterGump(pm, def("smithing"), acct));
        TryDump("Smith_SealsInfo", () => new SmithSealsInfoGump());
        TryDump("Smith_SealCatalog", () => new SmithSealCatalogGump(pm, SmithSealCatalogGump.Cat.AncientHammers));
        TryDump("Smith_Commission", () => new SmithCommissionGump(pm));
        TryDump("Smith_BulkOrderChoice", () => new SmithBulkOrderChoiceGump(pm));
        TryDump("Smith_GuildBook", () => new SmithGuildBookGump(pm, new SmithGuildBook()));
        TryDump("Smith_HammerRestore", () => new HammerRestoreGump(pm));
        TryDump("Smith_HammerFamiliarity", () => new HammerFamiliarityGump(pm, pm.Backpack.FindItemByType<HammerOfHephaestus>()));
        TryDump("Mining_Liaison", () => new MinersCompactLiaisonGump(pm));
        TryDump("Mining_Dashboard", () => new MinersCompactLiaisonGump(pm, MinersCompactLiaisonGump.View.MemberDashboard));
        TryDump("Mining_Restoration", () => new MinersCompactLiaisonGump(pm, MinersCompactLiaisonGump.View.Restoration));
        TryDump("Mining_ProspectorsLogbook", () => new ProspectorsLogbookGump(pm, data));
        TryDump("Mining_SurveyArchivist", () => new SurveyArchivistGump(pm));
        TryDump("Mining_JacobsT2", () => new JacobsUpgradeGump(pm));
        TryDump("Artificers_Guildmaster", () => new ArtificersGuildmasterGump(pm));
        TryDump("Artificers_Dashboard", () => new ArtificersGuildmasterGump(pm, ArtificersGuildmasterGump.View.MemberDashboard));
        TryDump("Artificers_Imbue", () => new ArtificersImbueGump(pm));
        TryDump("Artificers_Mastery", () => new ArtificersMasteryGump(pm, new ArtificersSatchel()));
        TryDump("Artificers_WorkOrders", () => new ArtificerWorkOrderGump(pm));
        TryDump("Artificers_FormulaShop", () => new ImbueFormulaShopGump(pm));
        TryDump("Artificers_Knowledge", () => new ImbueKnowledgeGump(acct, 0));
        TryDump("Foresters_Guildmaster", () => new ForestersGuildmasterGump(pm));
        TryDump("Foresters_Dashboard", () => new ForestersGuildmasterGump(pm, ForestersGuildmasterGump.View.MemberDashboard));
        TryDump("Foresters_Logbook", () => new ForestersLogbookGump(pm, data));
        TryDump("Outriders_Guildmaster", () => new OutridersGuildmasterGump(pm));
        TryDump("Outriders_Dashboard", () => new OutridersGuildmasterGump(pm, OutridersGuildmasterGump.View.MemberDashboard));
        TryDump("Custodians_Warden", () => new SanitationWardenGump(pm, def("custodians"), acct));
        TryDump("Custodians_CleanUpStore", () => new CleanUpBritanniaRewardGump(Npc(new SanitationWarden()), pm));
        TryDump("Contracts_Ledger_smithing", () => new GuildContractLedgerGump(pm, "smithing"));
        TryDump("MuleExchange", () => new CrossGuildExchangeGump(pm));
        TryDump("RecipeShop_tailoring", () => new GuildRecipeShopGump(pm, "tailoring", null));
        TryDump("Schematics_smithing", () => new GuildSchematicsGump(pm, "smithing", Npc(new BlacksmithGuildmaster())));
        TryDump("League_Registrar", () => new LeagueRegistrarGump(pm));
        TryDump("League_Rank", () => new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Rank));

        foreach (var m in npcs)
        {
            m.Delete();
        }
    }

    // Not a fact: the template for four guilds, the League and a guild book's repair page, for Chase's mockups.
    [Fact]
    public void DumpsTheMockups()
    {
        using var p = new Player();
        var g = p.Guild;

        // Society of Smiths: the Overview (rank ladder and the next rank's requirements).
        Dump("Smiths_Overview", new Menu(GuildMenuThemes.Get("smithing"), "Guildmaster, Minoc forge",
            GuildMenuRank.For("smithing", g), SmithSections, 0, 0, 1, false, (m, y) =>
            {
                y = m.H(y, "Rank ladder");
                var ladder = new RequirementList();
                var thresholds = ClusterFGuildSystem.RankThresholds("smithing");
                foreach (var t in thresholds)
                {
                    var name = ClusterFGuildSystem.GetRankName("smithing", t);
                    if (t <= 6_200)
                    {
                        ladder.Check(true, $"{name} ({t:N0})", "");
                    }
                    else
                    {
                        ladder.Info($"{name} ({t:N0})");
                    }
                }

                y = ladder.Draw(m, GuildMenuGump.ContentX, y) + 6;
                y = m.H(y, "Next rank: Master");
                new RequirementList()
                    .Rank("Master", 6_200, 15_000)
                    .Skill("Blacksmithy", 92.4, 90)
                    .Draw(m, GuildMenuGump.ContentX, y);
            }));

        // Society of Smiths: the Seal Catalog, page 1 of 3.
        Dump("Smiths_SealCatalog", new Menu(GuildMenuThemes.Get("smithing"), "Guildmaster, Minoc forge",
            GuildMenuRank.For("smithing", g), SmithSections, 3, 0, 3, false, (m, y) =>
            {
                y = m.H(y, "Ancient Hammers");
                y = m.I(y, 0x13E3, 0x482, "Ancient Smithy Hammer +10", "Bonus to Blacksmithy, 600 uses", "120 Seals", true, 100);
                y = m.I(y, 0x13E3, 0x48D, "Ancient Smithy Hammer +15", "Bonus to Blacksmithy, 600 uses", "450 Seals", true, 101);
                y = m.I(y, 0x13E3, 0x4F2, "Ancient Smithy Hammer +30", "Bonus to Blacksmithy, 600 uses", "1,800 Seals", false, 102);
                y = m.I(y, 0x13E3, 0x973, "Runic Hammer (Dull Copper)", "50 uses", "400 Seals", true, 103);
                m.I(y, 0x13E3, 0x966, "Runic Hammer (Shadow Iron)", "45 uses", "1,500 Seals", false, 104);
            }));

        // Miners' Compact: the Restoration of a T2 pickaxe, with cc-P169's requirement lines.
        Dump("Miners_Restoration", new Menu(GuildMenuThemes.Get("mining"), "New Haven Liaison",
            GuildMenuRank.For("mining", g),
            ["Overview", "Dashboard", "Restoration", "Ore Satchel", "Replace Kit", "Prospecting", "Mule Exchange"], 2, 0, 1,
            false, (m, y) =>
            {
                y = m.H(y, "Restore Jacob's Reinforced Pickaxe (T2)");
                y = m.T(y, "Back to full uses, for about a fifth of the upgrade.");
                y += 4;
                var list = new RequirementList()
                    .Has("Jacob's Reinforced Pickaxe in pack", true)
                    .Amount("Mining Vouchers", 3, 4)
                    .Amount("Iron Ingots", 250, 100)
                    .Amount("Dull Copper Ingots", 15, 15)
                    .Amount("Gold", 12_000, 1_000)
                    .Info("Ingots and gold count from your pack and bank.");
                m.R(y, list, 120, "Restore");
            }));

        // Artificers' Order: the work orders, page 1 of 2.
        Dump("Artificers_WorkOrders", new Menu(GuildMenuThemes.Get("artificers"), "Guildmaster, the Artificers' hall",
            GuildMenuRank.For("artificers", g),
            ["Overview", "Dashboard", "Work Orders", "Imbuing", "Mastery", "Formula Shop", "Mule Exchange"], 2, 0, 2, false,
            (m, y) =>
            {
                y = m.H(y, "Work orders you can take");
                y = m.A(y, 100, "Imbue a ring: 3 properties", "12 Shards, 300 standing");
                y = m.A(y, 101, "Enhance a weapon: Hit Lightning", "8 Shards, 200 standing");
                y = m.A(y, 102, "Unravel 20 magic items", "5 Shards, 120 standing");
                y = m.A(y, 103, "Imbue armor: 4 resists", "15 Shards, 400 standing");
                y += 6;
                y = m.H(y, "Your work orders");
                y = m.F(y, "Taken today", "2 of 3");
                m.F(y, "Next order in", "3 h 12 m");
            }));

        // The Custodians: the Clean Up Britannia store, page 1 of 4 (no crest yet: the placeholder).
        Dump("Custodians_Rewards", new Menu(GuildMenuThemes.Get("custodians"), "Sanitation Warden, Britain",
            new GuildMenuRank("Custodian", 340, 500, "Senior Custodian", "Clean Up points", 18_450),
            ["Overview", "Pledge", "Rewards", "Trash Bag", "Rank"], 2, 0, 4, false, (m, y) =>
            {
                y = m.H(y, "Clean Up Britannia rewards");
                y = m.I(y, 0xE77, 0, "Trash Barrel", "Deletes what you drop in it", "5,000 points", true, 100);
                y = m.I(y, 0xB025, 0, "Warden's Broom", "Sweeps up junk around you", "12,500 points", true, 101);
                y = m.I(y, 0x1F03, 0x47E, "Custodian's Robe", "Ice blue", "20,000 points", false, 102);
                y = m.I(y, 0x14F0, 0, "Recycling Bin (deed)", "For your house", "25,000 points", false, 103);
                m.I(y, 0xE76, 0x8A5, "Bag of Holding Trash", "Bigger trash bag", "40,000 points", false, 104);
            }));

        // The League: the Registrar's Rank page, to show the League fits the same base.
        Dump("League_Rank", new Menu(GuildMenuThemes.Get(GuildMenuThemes.LeagueKey), "Elara Voss, the League Registrar",
            new GuildMenuRank("Dull Copper", 340, 600, "Shadow Iron", "Renown", 210),
            ["Overview", "Citizen Status", "Rank", "Guild Referrals", "Mule Errand", "Register"], 2, 0, 1, false, (m, y) =>
            {
                y = m.H(y, "Promotion to Shadow Iron");
                var list = new RequirementList()
                    .Amount("Lifetime Renown", 340, 600)
                    .Amount("Guilds at Apprentice or higher", 2, 2)
                    .Check(false, "Promotion job done", "Promotion job: deliver 3 crates (not started)")
                    .Info("Rank never goes down when you spend Renown.");
                m.R(y, list, 120, "Promote");
            }));

        // A guild book (cc-P178): the Society of Smiths' tier-1 page, remote repair of the guild's special items.
        Dump("SmithsBook_Repair", new Menu(GuildMenuThemes.Get("smithing"), "Guild book",
            GuildMenuRank.For("smithing", g), ["Repair", "Orders", "Catalog", "Commissions"], 0, 0, 1, false, (m, y) =>
            {
                y = m.H(y, "Repair guild items (tier 1)");
                y = m.T(y, "Same price as at the guildmaster. Items in your pack.");
                y = m.I(y, 0x13E3, 0x504, "Hammer of Hephaestus", "112 / 150 uses", "40 gold, 1 Seal", true, 100);
                y = m.I(y, 0x13E3, 0x415, "Reinforced Hammer (T2)", "0 / 300 uses (exhausted)", "90 gold, 2 Seals", true, 101);
                y += 2;
                y = m.H(y, "What this book does as you rank up");
                new RequirementList()
                    .Check(true, "Tier 1, Repair: open (Initiate)", "")
                    .Check(false, "", "Tier 2, Orders: Master (need 8,800 more)")
                    .Check(false, "", "Tier 3, Catalog: Grandmaster (need 43,800)")
                    .Draw(m, GuildMenuGump.ContentX, y);
            }));
    }
}
