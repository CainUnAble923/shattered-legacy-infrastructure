// RequirementColorsVerification.cs
//
// cc-P169 (Chase 2026-10-18, the Hammer of Hephaestus "Upgrade to T2" screenshot: the rank line yellow at 25 / 5,000, met
// lines yellow, headings green, the window see-through). Every upgrade, restore and rank-up menu shows its requirements
// one way (RequirementList): met green with the check box, not met red with an X and how much more, info gray.
// Notes: shard-migration notes/cc-P169-requirement-colors.md.
//
// Facts:
//   H1. The helper's met, not met and info lines: their words, hues and art; the summary's words.
//   H2. Drawn, a line is its art and a label 24 to the right on the same row; an info line is a gray label, no art.
//   H3. The footer: the summary, and the action live only when every line is met; otherwise its art and words in the
//       disabled gray with no button behind them.
//   M1. Each menu (the Hammer's restore and upgrade, Jacob's T2 to T5, the Liaison's restoration and satchel upgrade,
//       the Mule Exchange, the League's rank page) for a character who meets everything: every line green with a check,
//       "All requirements met", the button live; opaque (tile 2624, no alpha region); no green (1154) heading; every
//       label inside the frame and clear of the footer rule.
//   M2. The same menus for a character who meets nothing: the requirement lines red with an X, "N requirements not met"
//       where N is those lines, no button; pressing the button's id anyway changes nothing (the server checks again).
//   R1. The rank line is met only at the rank's standing: 25 / 5,000 is red "Rank: Journeyman (need 4,975 more
//       Standing)", 5,000 is green; the same in Jacob's T2 at 999 / 1,000 and the satchel upgrade.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RequirementColorsVerification
{
    private const int Met = 0x44;
    private const int NotMet = 0x22;
    private const int Info = 999;
    private const int Disabled = 0x3B2;
    private const int Check = 211;
    private const int Cross = 5231;

    private readonly ITestOutputHelper _out;

    public RequirementColorsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private sealed class Probe : Gump
    {
        public Probe() : base(0, 0)
        {
        }
    }

    private static readonly string[] Guilds =
        ["smithing", "mining", "rangers", "foresters", "tinkers", "arcane", "artificers", "warriors"];

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p169{Guid.NewGuid():N}"[..16], "p169-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(bool rich)
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

            foreach (var key in Guilds)
            {
                Guild.JoinedGuilds.Add(key);
                Guild.GuildReputation[key] = rich ? 200_000 : 25;
                Guild.GuildCurrency[key] = rich ? 10_000 : 0;
            }

            Pm.Skills.Blacksmith.Cap = Pm.Skills.Mining.Cap = 120.0;
            Pm.Skills.Blacksmith.Base = Pm.Skills.Mining.Base = rich ? 120.0 : 0.0;

            if (rich)
            {
                Pm.Backpack.DropItem(new IronIngot(5000));
                Pm.Backpack.DropItem(new DullCopperIngot(5000));
                Pm.Backpack.DropItem(new AgapiteIngot(5000));
                Pm.Backpack.DropItem(new ValoriteIngot(5000));
                Pm.Backpack.DropItem(new AdamantiumIngot(5000));
                for (var i = 0; i < 6; i++)
                {
                    Pm.BankBox.DropItem(new Gold(60_000));
                }
            }
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public T Give<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public void Dispose()
        {
            foreach (var m in MuleUpgrades.MulesOf(Pm))
            {
                m.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // One menu: its name, how to set the player up for it, the gump, its action buttons, the press check.
    private sealed record Menu(
        string Name,
        Action<Player> Setup,
        Func<Player, Gump> Open,
        int[] Buttons,
        Func<Player, string> State
    );

    private static IEnumerable<Menu> Menus() =>
    [
        new("Hammer of Hephaestus", p => p.Give(new HammerOfHephaestus()), p => new HammerRestoreGump(p.Pm), [10, 20],
            p => $"T1 {p.Pm.Backpack.FindItemByType<HammerOfHephaestus>() != null}, T2 {p.Pm.Backpack.FindItemByType<ReinforcedHammerOfHephaestus>() != null}, seals {p.Guild.GuildCurrency["smithing"]}"),
        new("Jacob's T2", p => p.Give(new JacobsPickaxe()), p => new JacobsUpgradeGump(p.Pm), [10],
            p => $"T2 {p.Pm.Backpack.FindItemByType<JacobsReinforcedPickaxe>() != null}"),
        new("Jacob's T3", p => p.Give(new JacobsReinforcedPickaxe()), p => new JacobsT3UpgradeGump(p.Pm), [10],
            p => $"T3 {p.Pm.Backpack.FindItemByType<JacobsProspectorPickaxe>() != null}"),
        new("Jacob's T4", p => p.Give(new JacobsProspectorPickaxe()), p => new JacobsT4UpgradeGump(p.Pm), [10],
            p => $"T4 {p.Pm.Backpack.FindItemByType<JacobsDeepdelverPickaxe>() != null}"),
        new("Jacob's T5", p => p.Give(new JacobsDeepdelverPickaxe()), p => new JacobsT5UpgradeGump(p.Pm), [10],
            p => $"T5 {p.Pm.Backpack.FindItemByType<JacobsWorldbreakerPickaxe>() != null}"),
        new("Liaison restoration (T5)",
            p => ClusterFRestorationRegistry.Unlock(p.Account, "legacy.jacobs_worldbreaker_pickaxe", "test"),
            p => new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.Restoration), [64],
            p => $"T5 {p.Pm.Backpack.FindItemByType<JacobsWorldbreakerPickaxe>() != null}"),
        new("Liaison satchel upgrade", p => p.Give(new CompactOreSatchel()),
            p => new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.UpgradeSatchel), [91],
            p => $"T2 satchel {p.Pm.Backpack.FindItemByType<ReinforcedOreSatchel>() != null}"),
        new("Mule Exchange (tier 4 to 5)", p => PackMule.SummonFor(p.Pm, 4, MuleGenes.Fresh(), "here"),
            p => new CrossGuildExchangeGump(p.Pm), [100],
            p => $"tier {MuleUpgrades.MulesOf(p.Pm).Single().Tier}")
    ];

    // ---------------------------------------------------------------- H1

    [Fact]
    public void H1_TheHelpersLinesHaveTheirWordsHuesAndArt()
    {
        var list = new RequirementList()
            .Amount("Smithing Seals", 30, 25)
            .Amount("Smithing Seals", 10, 25)
            .Skill("Blacksmithy", 119.0, 80)
            .Skill("Mining", 62.5, 75)
            .Rank("Journeyman", 25, 5000)
            .Rank("Journeyman", 5000, 5000)
            .Has("Hammer of Hephaestus in pack", false)
            .Info("500 iron ingots: 200 in pack, 300 in bank");

        var expected = new (RequirementState, string)[]
        {
            (RequirementState.Met, "Smithing Seals 30 / 25"),
            (RequirementState.NotMet, "Smithing Seals 10 / 25 (need 15 more)"),
            (RequirementState.Met, "Blacksmithy 119.0 / 80"),
            (RequirementState.NotMet, "Mining 62.5 / 75 (need 12.5 more)"),
            (RequirementState.NotMet, "Rank: Journeyman (need 4,975 more Standing)"),
            (RequirementState.Met, "Rank: Journeyman 5,000 / 5,000 Standing"),
            (RequirementState.NotMet, "Hammer of Hephaestus in pack (missing)"),
            (RequirementState.Info, "500 iron ingots: 200 in pack, 300 in bank")
        };

        Assert.Equal(expected, list.Lines.Select(l => (l.State, l.Text)));

        Assert.Equal((Met, Check), (RequirementList.HueOf(RequirementState.Met), RequirementList.ArtOf(RequirementState.Met)));
        Assert.Equal((NotMet, Cross), (RequirementList.HueOf(RequirementState.NotMet), RequirementList.ArtOf(RequirementState.NotMet)));
        Assert.Equal(Info, RequirementList.HueOf(RequirementState.Info));

        // Green only means met: the heading and info hues are neither met nor not met.
        Assert.DoesNotContain(RequirementStyle.HeadingHue, new[] { Met, NotMet });
        Assert.DoesNotContain(RequirementStyle.InfoHue, new[] { Met, NotMet });

        Assert.Equal(4, list.NotMetCount);
        Assert.False(list.AllMet);
        Assert.Equal("4 requirements not met", list.Summary);
        Assert.Equal("1 requirement not met", RequirementList.Summarize(1));
        Assert.Equal("All requirements met", RequirementList.Summarize(0));
    }

    // ---------------------------------------------------------------- H2, H3

    [Fact]
    public void H2_H3_DrawnLinesAndTheFooter()
    {
        var met = new RequirementList().Amount("Gold", 10, 5).Info("Gold counts from your pack and bank.");
        var g = new Probe();
        var y = met.Draw(g, 18, 100);
        met.DrawFooter(g, 18, y + 4, 77, "Do it");

        var labels = g.Entries.OfType<GumpLabel>().ToList();
        var images = g.Entries.OfType<GumpImage>().ToList();

        var gold = labels.Single(l => l.Text == "Gold 10 / 5");
        Assert.Equal((42, Met), (gold.X, gold.Hue));
        Assert.Contains(images, i => i.GumpID == Check && i.X == 18 && Math.Abs(i.Y - gold.Y) <= 3);

        var info = labels.Single(l => l.Text == "Gold counts from your pack and bank.");
        Assert.Equal((42, Info), (info.X, info.Hue));
        Assert.DoesNotContain(images, i => i.Y >= info.Y - 3 && i.Y <= info.Y + 3);

        Assert.Equal(Met, labels.Single(l => l.Text == "All requirements met").Hue);
        Assert.Contains(g.Entries.OfType<GumpButton>(), b => b.ButtonID == 77);
        Assert.Equal(RequirementStyle.HeadingHue, labels.Single(l => l.Text == "Do it").Hue);

        var notMet = new RequirementList().Amount("Gold", 1, 5);
        var h = new Probe();
        notMet.DrawFooter(h, 18, notMet.Draw(h, 18, 100) + 4, 77, "Do it");
        labels = h.Entries.OfType<GumpLabel>().ToList();

        Assert.Equal(NotMet, labels.Single(l => l.Text == "Gold 1 / 5 (need 4 more)").Hue);
        Assert.Equal(NotMet, labels.Single(l => l.Text == "1 requirement not met").Hue);
        Assert.Empty(h.Entries.OfType<GumpButton>());
        Assert.Contains(h.Entries.OfType<GumpImage>(), i => i.GumpID == RequirementStyle.ActionArt && i.Hue == Disabled);
        Assert.Equal(Disabled, labels.Single(l => l.Text == "Do it").Hue);
    }

    // ---------------------------------------------------------------- M1, M2

    // What every requirement menu looks like, met or not.
    private void AssertLook(string name, Gump g, int[] buttons, bool allMet)
    {
        var frame = g.Entries.OfType<GumpBackground>().First();
        var labels = g.Entries.OfType<GumpLabel>().ToList();
        var images = g.Entries.OfType<GumpImage>().ToList();

        foreach (var l in labels)
        {
            _out.WriteLine($"{name}: ({l.X},{l.Y}) hue {l.Hue}: {l.Text}");
        }

        // Opaque.
        Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());
        var panel = g.Entries.OfType<GumpImageTiled>().FirstOrDefault(t => t.GumpID == RequirementStyle.PanelTile);
        Assert.NotNull(panel);
        Assert.Equal((8, 8, frame.Width - 16, frame.Height - 16), (panel.X, panel.Y, panel.Width, panel.Height));

        // No green heading: 1154 draws hues.mul's "green highlight".
        Assert.DoesNotContain(labels, l => l.Hue == 1154);

        // Each check or X has its line beside it, in its colour; nothing else is drawn in those colours.
        var checks = images.Where(i => i.GumpID == Check).ToList();
        var crosses = images.Where(i => i.GumpID == Cross).ToList();
        foreach (var (art, hue) in checks.Select(i => (i, Met)).Concat(crosses.Select(i => (i, NotMet))))
        {
            Assert.Contains(labels, l => l.X == art.X + 24 && Math.Abs(l.Y - art.Y) <= 3 && l.Hue == hue);
        }

        Assert.Equal(checks.Count, labels.Count(l => l.Hue == Met));
        Assert.Equal(crosses.Count, labels.Count(l => l.Hue == NotMet));

        // The summary and the action, for each action.
        var summaries = labels.Where(l => Regex.IsMatch(l.Text, @"^(All requirements met|\d+ requirements? not met)$")).ToList();
        Assert.Equal(buttons.Length, summaries.Count);

        if (allMet)
        {
            Assert.Empty(crosses);
            Assert.All(summaries, s => Assert.Equal(("All requirements met", Met), (s.Text, s.Hue)));
            Assert.All(buttons, id => Assert.Contains(g.Entries.OfType<GumpButton>(), b => b.ButtonID == id));
        }
        else
        {
            // Each summary counts the X's between it and the summary above it (each list's own lines).
            var above = 0;
            foreach (var s in summaries.OrderBy(s => s.Y))
            {
                var n = crosses.Count(c => c.Y > above && c.Y < s.Y - 3);
                Assert.Equal((RequirementList.Summarize(n), NotMet), (s.Text, s.Hue));
                Assert.True(n > 0, $"{name}: a summary with nothing not met above it");
                above = s.Y + 3;
            }

            Assert.All(buttons, id => Assert.DoesNotContain(g.Entries.OfType<GumpButton>(), b => b.ButtonID == id));
            Assert.Equal(buttons.Length, images.Count(i => i.GumpID == RequirementStyle.ActionArt && i.Hue == Disabled));
        }

        // No label crosses a rule (tile 9304): the Hammer's second status line did, at 100 (the old menu's too).
        foreach (var rule in g.Entries.OfType<GumpImageTiled>().Where(t => t.GumpID == 9304))
        {
            Assert.DoesNotContain(labels, l => rule.Y > l.Y + 2 && rule.Y < l.Y + 16);
        }

        // Inside the frame (its 9270 border is 10 pixels), and clear of the footer rule at H - 38.
        foreach (var l in labels)
        {
            Assert.True(l.X + GumpTextWidth.Label(l.Text) <= frame.Width - 10, $"{name}: \"{l.Text}\" ends past the frame");
            Assert.True(l.Y >= frame.Height - 38 || l.Y + GumpTextWidth.LineHeight <= frame.Height - 38,
                $"{name}: \"{l.Text}\" at {l.Y} runs into the footer rule at {frame.Height - 38}");
        }
    }

    [Fact]
    public void M1_EachMenuForACharacterWhoMeetsEverything()
    {
        foreach (var menu in Menus())
        {
            using var p = new Player(rich: true);
            menu.Setup(p);
            AssertLook(menu.Name, menu.Open(p), menu.Buttons, allMet: true);
        }
    }

    [Fact]
    public void M2_EachMenuForACharacterWhoMeetsNothing()
    {
        foreach (var menu in Menus())
        {
            using var p = new Player(rich: false);
            menu.Setup(p);
            var g = menu.Open(p);
            AssertLook(menu.Name, g, menu.Buttons, allMet: false);

            // The button is not there, and its id pressed anyway does nothing.
            var before = menu.State(p);
            foreach (var id in menu.Buttons)
            {
                Press(menu.Open(p), p.Ns, id);
            }

            Assert.Equal(before, menu.State(p));
            _out.WriteLine($"{menu.Name}: pressed {string.Join(", ", menu.Buttons)} anyway: still {before}");
        }
    }

    [Fact]
    public void M1_M2_TheLeagueRankPage()
    {
        LeaguePromotionReport Report(bool met) => new(
            1, 2, null,
            new LeagueCheck("Lifetime Renown", met ? LeagueCheckState.Passed : LeagueCheckState.Failed, "", met ? 150 : 40, 100),
            new LeagueCheck("Guild ranks", met ? LeagueCheckState.Passed : LeagueCheckState.Failed, "", met ? 1 : 0, 1,
                "Guilds at Apprentice or higher"),
            new LeagueCheck("Promotion job", met ? LeagueCheckState.Passed : LeagueCheckState.Failed,
                met ? "done" : "turn in the Dull Copper Citizen promotion job"),
            new LeagueCheck("Story trial, chapter 1", LeagueCheckState.Waived, "not yet required")
        );

        foreach (var met in new[] { true, false })
        {
            var reqs = Report(met).Requirements();
            Assert.Equal(met, reqs.AllMet);
            Assert.Equal(Report(met).CanPromote, reqs.AllMet);

            Assert.Equal(met
                    ? new[] { "Lifetime Renown 150 / 100", "Guilds at Apprentice or higher 1 / 1", "Promotion job: done",
                        "Story trial, chapter 1: not yet required" }
                    : new[] { "Lifetime Renown 40 / 100 (need 60 more)", "Guilds at Apprentice or higher 0 / 1 (need 1 more)",
                        "Promotion job: turn in the Dull Copper Citizen promotion job", "Story trial, chapter 1: not yet required" },
                reqs.Lines.Select(l => l.Text));
            Assert.Equal(RequirementState.Info, reqs.Lines[3].State);
            Assert.Equal(met ? "All requirements met" : "3 requirements not met", reqs.Summary);
        }
    }

    // ---------------------------------------------------------------- screenshots

    // Not a fact: each menu for the screenshot's kind of character (rank not met, some materials short, the rest met),
    // its entries as one JSON line ("GUMPJSON <name> <json>"), for the before / after renders in the notes.
    [Fact]
    public void DumpsEachMenuForTheScreenshots()
    {
        foreach (var menu in Menus())
        {
            using var p = new Player(rich: true);
            menu.Setup(p);
            p.Guild.GuildReputation["smithing"] = 25;
            p.Guild.GuildReputation["mining"] = 25;
            p.Guild.GuildCurrency["smithing"] = 10;
            p.Guild.GuildCurrency["mining"] = 10;
            p.Pm.Skills.Blacksmith.Base = 119.0;

            var entries = menu.Open(p).Entries.Select(e =>
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

            _out.WriteLine($"GUMPJSON {menu.Name.Replace(' ', '_')} {System.Text.Json.JsonSerializer.Serialize(entries)}");
        }
    }

    // ---------------------------------------------------------------- R1

    private static GumpLabel RankLine(Gump g) => g.Entries.OfType<GumpLabel>().Single(l => l.Text.StartsWith("Rank: "));

    [Fact]
    public void R1_TheRankLineIsMetOnlyAtTheRanksStanding()
    {
        using var p = new Player(rich: true);
        p.Give(new HammerOfHephaestus());
        p.Give(new JacobsPickaxe());
        p.Give(new CompactOreSatchel());

        var cases = new (string Guild, int Standing, Func<Gump> Open, string Text, int Hue)[]
        {
            ("smithing", 25, () => new HammerRestoreGump(p.Pm), "Rank: Journeyman (need 4,975 more Standing)", NotMet),
            ("smithing", 4_999, () => new HammerRestoreGump(p.Pm), "Rank: Journeyman (need 1 more Standing)", NotMet),
            ("smithing", 5_000, () => new HammerRestoreGump(p.Pm), "Rank: Journeyman 5,000 / 5,000 Standing", Met),
            ("mining", 999, () => new JacobsUpgradeGump(p.Pm), "Rank: Apprentice (need 1 more Standing)", NotMet),
            ("mining", 1_000, () => new JacobsUpgradeGump(p.Pm), "Rank: Apprentice 1,000 / 1,000 Standing", Met),
            ("mining", 999, () => new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.UpgradeSatchel),
                "Rank: Apprentice (need 1 more Standing)", NotMet),
            ("mining", 1_000, () => new MinersCompactLiaisonGump(p.Pm, MinersCompactLiaisonGump.View.UpgradeSatchel),
                "Rank: Apprentice 1,000 / 1,000 Standing", Met)
        };

        foreach (var (guild, standing, open, text, hue) in cases)
        {
            p.Guild.GuildReputation[guild] = standing;
            var g = open();
            var line = RankLine(g);
            _out.WriteLine($"{guild} {standing}: \"{line.Text}\" hue {line.Hue}");
            Assert.Equal((text, hue), (line.Text, line.Hue));
            Assert.Contains(g.Entries.OfType<GumpImage>(),
                i => i.GumpID == (hue == Met ? Check : Cross) && Math.Abs(i.Y - line.Y) <= 3);
        }
    }
}
