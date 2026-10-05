// AchievementsLayoutVerification.cs
//
// cc-P57 Part F (bug-list D80, Chase screenshot 2026-10-05, [achievements page 2 of 11): rows overlapped (a row's progress
// line sat under the next row's title), the tab row cut "Discov" and wrapped "Quests", the gump was translucent, and one
// row read "Requires? Getting Into It". Measured with the client's own font widths (GumpTextWidth).
// Notes: shard-migration notes/cc-P57-batch-6.md, Part F.
//
// Facts:
//   F1. Every page of every tab: each row's HTML block is at least as tall as its wrapped lines, rows and headings never
//       overlap, icons end inside their own row, and everything sits between the tab rule and the pager.
//   F2. The tabs are nine full words on two even rows (five and four) in fixed columns, each label clear of its button
//       and ending before the next column.
//   F3. Opaque: no alpha region, the solid tile under the inside of the frame; the same on the Achievement Unlocked popup.
//   F4. A locked row whose prerequisite is not earned says "Requires: <title>" on a line of its own.
//   F5. Every label and every row's text is ASCII (every character has a glyph in the client's font) and ends inside the
//       frame.
//   F6. The popup: for every achievement, its title, flavor and Earned line fit the popup's text block.

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
public class AchievementsLayoutVerification
{
    private readonly ITestOutputHelper _out;

    public AchievementsLayoutVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p57f{Guid.NewGuid():N}"[..16], "p57-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Name = "Layout Fixture" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1480, 1760, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private const int FrameRight = AchievementsGump.GumpWidth - 10;
    private const int IconArtHeight = 44; // the tallest achievement icon in EA's art (0x1B76, 50 x 44; notes Part F)

    private static string Plain(string html) =>
        Regex.Replace(html.Replace("<BR>", "\n"), "<[^>]+>", "");

    private static IEnumerable<AchievementCategory?> Tabs() =>
        AchievementsGump.CategoryTabs.Select(t => t.Cat).Append(AchievementCategory.League);

    private static List<AchievementsGump> EveryPage(PlayerMobile pm, AchievementCategory? cat)
    {
        var pages = new List<AchievementsGump>();
        for (var i = 0; i < 60; i++)
        {
            var g = new AchievementsGump(pm, cat, false, i);
            var counter = g.Entries.OfType<GumpLabel>().Select(l => Regex.Match(l.Text, @"^(\d+)/(\d+)$"))
                .FirstOrDefault(m => m.Success);
            pages.Add(g);
            if (counter == null || counter.Groups[1].Value == counter.Groups[2].Value)
            {
                break;
            }
        }

        return pages;
    }

    // ---------------------------------------------------------------- F1, F5

    [Fact]
    public void EveryRowHasItsFullHeightAndNothingOverlaps()
    {
        using var p = new Player();
        ClusterFAchievementSystem.TryGrant(p.Account, "combat.first_blood");
        ClusterFAchievementSystem.TryGrant(p.Account, "exploration.citizen");

        var checkedRows = 0;
        var tallest = (Title: "", Lines: 0);
        foreach (var cat in Tabs())
        {
            var pages = EveryPage(p.Pm, cat);
            for (var pi = 0; pi < pages.Count; pi++)
            {
                var g = pages[pi];
                var where = $"{cat?.ToString() ?? "All"} page {pi + 1}/{pages.Count}";

                // Bands: each row's vertical extent. Headings are labels at y + 4 with their rule at y + 22.
                var bands = new List<(int Top, int Bottom, string What)>();
                foreach (var h in g.Entries.OfType<GumpHtml>())
                {
                    var text = Plain(h.Text);
                    var lines = GumpTextWidth.Lines(text, h.Width);
                    Assert.True(h.Height >= lines * GumpTextWidth.LineHeight,
                        $"{where}: \"{text.Split('\n')[0]}\" needs {lines} lines, its block is {h.Height} high");
                    Assert.False(h.Scrollbar);
                    Assert.True(h.X + h.Width <= FrameRight, $"{where}: a block ends at {h.X + h.Width}");
                    bands.Add((h.Y, h.Y + h.Height, text.Split('\n')[0]));
                    checkedRows++;
                    if (lines > tallest.Lines)
                    {
                        tallest = (text.Split('\n')[0], lines);
                    }
                }

                foreach (var l in g.Entries.OfType<GumpLabel>().Where(l => l.Y >= AchievementsGump.ListY && l.Y < AchievementsGump.ContentBottom))
                {
                    bands.Add((l.Y - 4, l.Y + 20, l.Text));
                }

                foreach (var item in g.Entries.OfType<GumpItem>())
                {
                    var row = bands.Single(b => b.Top == item.Y - 2);
                    var next = bands.Where(b => b.Top > row.Top).Select(b => b.Top).DefaultIfEmpty(AchievementsGump.ContentBottom).Min();
                    Assert.True(item.Y + IconArtHeight <= next, $"{where}: the icon of \"{row.What}\" runs into the next row");
                }

                bands.Sort();
                for (var i = 0; i < bands.Count; i++)
                {
                    Assert.True(bands[i].Top >= AchievementsGump.ListY - 4 && bands[i].Bottom <= AchievementsGump.ContentBottom,
                        $"{where}: \"{bands[i].What}\" spans {bands[i].Top}..{bands[i].Bottom}");
                    if (i > 0)
                    {
                        Assert.True(bands[i].Top >= bands[i - 1].Bottom,
                            $"{where}: \"{bands[i].What}\" starts at {bands[i].Top}, above the end of \"{bands[i - 1].What}\" ({bands[i - 1].Bottom})");
                    }
                }

                // F5: every label is ASCII and ends inside the frame.
                foreach (var l in g.Entries.OfType<GumpLabel>())
                {
                    Assert.True(l.X + GumpTextWidth.Label(l.Text) <= FrameRight, $"{where}: \"{l.Text}\" ends past the frame");
                }
            }
        }

        _out.WriteLine($"{checkedRows} rows checked over every page of {Tabs().Count()} tabs; tallest: \"{tallest.Title}\", {tallest.Lines} lines");
        Assert.True(checkedRows > 70);
    }

    // ---------------------------------------------------------------- F2

    [Fact]
    public void TheTabsAreFullWordsOnTwoEvenRows()
    {
        using var p = new Player();
        var g = new AchievementsGump(p.Pm);
        var words = new[] { "All", "Combat", "Skills", "Exploration", "Mining", "Crafting", "Legacy", "Discovery", "Quests" };
        var buttons = g.Entries.OfType<GumpButton>().Where(b => b.ButtonID is >= 100 and <= 108).OrderBy(b => b.ButtonID).ToList();
        Assert.Equal(9, buttons.Count);

        for (var i = 0; i < words.Length; i++)
        {
            var b = buttons[i];
            var label = Assert.Single(g.Entries.OfType<GumpLabel>(), l => l.Y == b.Y + 2 && l.X > b.X && l.X < b.X + 108);
            Assert.Equal(words[i], label.Text);
            Assert.True(label.X >= b.X + 30, $"\"{label.Text}\" starts under its button");
            var right = label.X + GumpTextWidth.Label(label.Text);
            var nextButton = i % 5 < 4 && i + 1 < buttons.Count && buttons[i + 1].Y == b.Y ? buttons[i + 1].X : FrameRight;
            Assert.True(right <= nextButton, $"\"{label.Text}\" ends at {right}, the next column starts at {nextButton}");
            _out.WriteLine($"tab {label.Text}: button {b.X},{b.Y}, label ends {right}");
        }

        Assert.Equal(new[] { 5, 4 }, buttons.GroupBy(b => b.Y).OrderBy(r => r.Key).Select(r => r.Count()).ToArray());
        Assert.Equal(buttons.Take(4).Select(b => b.X), buttons.Skip(5).Select(b => b.X)); // the same columns
    }

    // ---------------------------------------------------------------- F3

    [Fact]
    public void TheGumpAndThePopupAreOpaque()
    {
        using var p = new Player();
        var g = new AchievementsGump(p.Pm);
        Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());
        var panel = Assert.Single(g.Entries.OfType<GumpImageTiled>(), t => t.GumpID == AchievementsGump.PanelTile);
        Assert.Equal((10, 10, 560, 490), (panel.X, panel.Y, panel.Width, panel.Height));

        var popup = new AchievementEarnedGump(p.Pm, ClusterFAchievementSystem.Definitions["combat.first_blood"]);
        Assert.Empty(popup.Entries.OfType<GumpAlphaRegion>());
        var popupPanel = Assert.Single(popup.Entries.OfType<GumpImageTiled>(), t => t.GumpID == AchievementsGump.PanelTile);
        Assert.Equal((4, 4, 412, 148), (popupPanel.X, popupPanel.Y, popupPanel.Width, popupPanel.Height));
    }

    // ---------------------------------------------------------------- F4

    [Fact]
    public void APrerequisiteReadsRequiresOnItsOwnLine()
    {
        using var p = new Player();
        var fine = ClusterFAchievementSystem.Definitions["combat.thousand"];
        var into = ClusterFAchievementSystem.Definitions["combat.century"];
        Assert.Equal("This Is Fine", fine.Title);
        Assert.Equal("Getting Into It", into.Title);

        var row = EveryPage(p.Pm, AchievementCategory.Combat).SelectMany(g => g.Entries.OfType<GumpHtml>())
            .Select(h => h.Text).Single(t => t.Contains(">This Is Fine<"));
        var lines = Plain(row).Split('\n');
        _out.WriteLine(string.Join(" | ", lines));
        Assert.Contains("Requires: Getting Into It", lines);
        Assert.DoesNotContain("Requires?", Plain(row));
    }

    // ---------------------------------------------------------------- F6

    [Fact]
    public void EveryAchievementFitsThePopup()
    {
        using var p = new Player();
        var worst = (Title: "", Lines: 0);
        foreach (var def in ClusterFAchievementSystem.Definitions.Values)
        {
            var popup = new AchievementEarnedGump(p.Pm, def);
            var block = Assert.Single(popup.Entries.OfType<GumpHtml>());
            var lines = GumpTextWidth.Lines(Plain(block.Text), block.Width);
            if (lines > worst.Lines)
            {
                worst = (def.Title, lines);
            }

            Assert.True(lines * GumpTextWidth.LineHeight <= block.Height,
                $"{def.Title}: {lines} lines in a {block.Height}-high block");
        }

        _out.WriteLine($"{ClusterFAchievementSystem.Definitions.Count} popups; the most lines: \"{worst.Title}\", {worst.Lines}");
    }
}
