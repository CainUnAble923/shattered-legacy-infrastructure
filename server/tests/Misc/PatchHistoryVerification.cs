// PatchHistoryVerification.cs
//
// cc-P64 (F-15 follow-up, bug-list D87): the Patch History gump ([version, and the bulletin's All patch notes button),
// and the bulletin's own note lines. D87: the bulletin drew each note line in one 18-pixel HTML row, so "Changed:
// Crafting in Platinum to Celestial now raises Blacksmithy past" ended at the gump's edge. Measured with the client's
// own font widths (GumpTextWidth). Notes: shard-migration notes/cc-P64-patch-history-gump.md.
//
// Facts:
//   H1. No line is ever cut: on every page of the history (player and staff) and in the bulletin, each HTML block is
//       at least as tall as its wrapped lines, ends inside the frame, and no two rows overlap; every label ends inside
//       the frame; the D87 line appears whole.
//   H2. Every version and every Added, Changed and Fixed item in the changelog appears, newest first, with its date.
//       Staff items and the Staff heading never reach a player; a staff viewer sees every Staff item too.
//   H3. Next walks from the first page to the last ("Page N of N", the oldest version, no Next button there);
//       Previous walks back. The current version is marked "(you are here)". The gump is opaque.
//   H4. The bulletin has no wiki button and no "full notes" text; All patch notes opens the history, sends no
//       open-URL packet, and sets the seen flag. More lines than fit say "... and N more (All patch notes)".

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PatchHistoryVerification : IDisposable
{
    // The D87 line, as CHANGELOG.md has it in 2026.10.05.2.
    private const string D87 =
        "Crafting in Platinum to Celestial now raises Blacksmithy past the item's own limit, up to the next metal's " +
        "need: Platinum to 125, Toxic to 137.5, Blaze to 150, Frost to 162.5, Obsidian to 175, Mythril to 187.5, " +
        "Adamantium and Celestial to 200. The chance to make the item is unchanged.";

    private const string Unbroken = "Supercalifragilisticexpialidocious-" +
        "WordsWithNoSpaceAtAllRunPastTheEdgeOfTheBlockAndMustBreakSomewhereInsideIt-" +
        "WordsWithNoSpaceAtAllRunPastTheEdgeOfTheBlockAndMustBreakSomewhereInsideIt";

    private static readonly string Sample = BuildSample();

    // Twelve versions, newest first: a real-shaped top one (the D87 line, a Staff list), then eleven of three to five
    // items each, one with only a Staff list, so the history takes several pages.
    private static string BuildSample()
    {
        var sb = new StringBuilder();
        sb.Append("# Shattered Legacy changelog\n\n<!--\nHow this file works.\n\n## 2026.09.01\n\n### Added\n- Example.\n-->\n\n");
        sb.Append("## 2026.10.05.2\n\n### Changed\n- ").Append(D87).Append('\n');
        sb.Append("- \"Orders that still teach me\" counts the order's metal.\n- ").Append(Unbroken).Append("\n\n");
        sb.Append("### Fixed\n- Earned achievements no longer show a progress line.\n\n");
        sb.Append("### Staff\n- Shard Console: a Player package tab builds the player zip.\n\n");
        sb.Append("## 2026.10.05\n\n### Staff\n- Only staff changed in this version.\n\n");
        for (var d = 4; d >= 1; d--)
        {
            for (var n = 3; n >= 1; n--)
            {
                var v = n == 1 ? $"2026.10.0{d}" : $"2026.10.0{d}.{n}";
                sb.Append($"## {v}\n\n### Added\n");
                for (var i = 0; i < 2 + n; i++)
                {
                    sb.Append($"- {v} item {i}: a Society of Smiths member can bank a smith deed for Smithing Seals at any smith in Britannia, town blacksmiths included.\n");
                }

                sb.Append($"\n### Fixed\n- {v} fix: the lookout no longer faces the wall.\n\n");
                if (d == 2 && n == 2)
                {
                    sb.Append($"### Staff\n- {v} staff: a console tab.\n\n");
                }
            }
        }

        return sb.ToString();
    }

    private readonly ITestOutputHelper _out;

    public PatchHistoryVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardVersion.Load(Sample, "2026.10.05.2");
    }

    public void Dispose() => ShardVersion.Reset();

    private sealed class Viewer : IDisposable
    {
        public readonly Account Account = new($"p64h{Guid.NewGuid():N}"[..16], "p64-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Viewer(AccessLevel level = AccessLevel.Player)
        {
            Pm = new PlayerMobile { Player = true, Name = "History Fixture", AccessLevel = level };
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1480, 1762, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static string Plain(string html) => Regex.Replace(html.Replace("<BR>", "\n"), "<[^>]+>", "");

    /// <summary>
    /// Presses a button as the client's reply does: the gump system takes the gump off the open list first
    /// (pinned GumpSystem.IncomingPackets.cs:157) and then calls OnResponse (:166), so FindGump finds the new one.
    /// </summary>
    private static void Press<T>(T g, Viewer v, int button) where T : Gump
    {
        v.Pm.CloseGump<T>();
        g.OnResponse(v.Ns, new RelayInfo(button, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    private static List<PatchHistoryGump> EveryPage(Mobile m)
    {
        var first = new PatchHistoryGump(m);
        var pages = new List<PatchHistoryGump> { first };
        for (var i = 1; i < first.PageCount; i++)
        {
            pages.Add(new PatchHistoryGump(m, i));
        }

        return pages;
    }

    private static string AllText(IEnumerable<Gump> gumps) => string.Join("\n", gumps.SelectMany(g =>
        g.Entries.Select(e => e switch
        {
            GumpLabel l => l.Text,
            GumpHtml h  => Plain(h.Text),
            _           => null
        }).Where(t => t != null)));

    /// <summary>H1 for one gump: blocks tall enough and inside the frame, labels inside, rows in order.</summary>
    private static int CheckFits(Gump g, int frameRight, int top, int bottom, string where)
    {
        var bands = new List<(int Top, int Bottom, string What)>();
        foreach (var h in g.Entries.OfType<GumpHtml>())
        {
            var text = Plain(h.Text);
            var lines = GumpTextWidth.Lines(text, h.Width);
            Assert.True(h.Height >= lines * GumpTextWidth.LineHeight,
                $"{where}: \"{text}\" needs {lines} lines, its block is {h.Height} high");
            Assert.False(h.Scrollbar);
            Assert.True(h.X + h.Width <= frameRight, $"{where}: a block ends at {h.X + h.Width}");
            if (h.Y >= top && h.Y < bottom)
            {
                bands.Add((h.Y, h.Y + h.Height, text));
            }
        }

        foreach (var l in g.Entries.OfType<GumpLabel>())
        {
            Assert.True(l.X + GumpTextWidth.Label(l.Text) <= frameRight, $"{where}: \"{l.Text}\" ends at {l.X + GumpTextWidth.Label(l.Text)}");
        }

        // Rows: labels in the list are one line (18) each; labels on the same y (a heading and its date) are one band.
        foreach (var y in g.Entries.OfType<GumpLabel>().Where(l => l.Y >= top && l.Y < bottom).Select(l => l.Y).Distinct())
        {
            bands.Add((y, y + GumpTextWidth.LineHeight, $"label at {y}"));
        }

        bands.Sort();
        for (var i = 0; i < bands.Count; i++)
        {
            Assert.True(bands[i].Top >= top - 4 && bands[i].Bottom <= bottom,
                $"{where}: \"{bands[i].What}\" spans {bands[i].Top}..{bands[i].Bottom}, the list is {top}..{bottom}");
            if (i > 0)
            {
                Assert.True(bands[i].Top >= bands[i - 1].Bottom,
                    $"{where}: \"{bands[i].What}\" starts at {bands[i].Top}, above the end of \"{bands[i - 1].What}\" ({bands[i - 1].Bottom})");
            }
        }

        return g.Entries.OfType<GumpHtml>().Count();
    }

    // ---------------------------------------------------------------- H1

    [Fact]
    public void NoLineIsEverCut()
    {
        var blocks = 0;
        foreach (var level in new[] { AccessLevel.Player, AccessLevel.GameMaster })
        {
            using var v = new Viewer(level);
            var pages = EveryPage(v.Pm);
            Assert.True(pages.Count >= 3, $"{level}: {pages.Count} page(s); the sample should need several");
            for (var i = 0; i < pages.Count; i++)
            {
                blocks += CheckFits(pages[i], PatchHistoryGump.FrameRight, PatchHistoryGump.ListY, PatchHistoryGump.ContentBottom,
                    $"{level} page {i + 1}/{pages.Count}");
            }

            var d87 = Assert.Single(pages[0].Entries.OfType<GumpHtml>(), h => Plain(h.Text).Contains("Crafting in Platinum"));
            Assert.Equal($"- {D87}", Plain(d87.Text));
            _out.WriteLine($"{level}: {pages.Count} pages; the D87 line takes {GumpTextWidth.Lines(Plain(d87.Text), d87.Width)} rows in a {d87.Height}-high block");
        }

        // The bulletin's own note lines.
        using var p = new Viewer();
        var bulletin = new BulletinGump(p.Pm, new List<BulletinEntry>(), ShardVersion.CurrentNotes);
        blocks += CheckFits(bulletin, 480 - 10, 72, 1000, "bulletin");
        var line = Assert.Single(bulletin.Entries.OfType<GumpHtml>(), h => Plain(h.Text).Contains("Crafting in Platinum"));
        Assert.Equal($"Changed: {D87}", Plain(line.Text));
        Assert.True(line.Height >= 2 * GumpTextWidth.LineHeight, "the D87 line in the bulletin is one row high again");

        _out.WriteLine($"{blocks} HTML blocks checked");
    }

    // ---------------------------------------------------------------- H2

    [Fact]
    public void EveryVersionAndSectionAppearsAndStaffOnlyForStaff()
    {
        var versions = PatchNotes.Parse(Sample);
        Assert.Equal(14, versions.Count);
        Assert.Equal("October 5, 2026", versions[0].Date);
        Assert.Equal("October 1, 2026", versions[^1].Date);

        using var player = new Viewer();
        using var staff = new Viewer(AccessLevel.GameMaster);
        var playerText = AllText(EveryPage(player.Pm));
        var staffText = AllText(EveryPage(staff.Pm));

        var last = -1;
        foreach (var v in versions)
        {
            var at = playerText.IndexOf($"Shattered Legacy {v.Version}\n", StringComparison.Ordinal);
            Assert.True(at > last, $"{v.Version} is missing or out of order");
            last = at;
            Assert.Contains(v.Date, playerText);

            foreach (var (section, items) in v.Sections())
            {
                foreach (var item in items)
                {
                    Assert.Contains($"- {item}", playerText);
                    Assert.Contains($"- {item}", staffText);
                }
            }

            foreach (var item in v.Staff)
            {
                Assert.DoesNotContain(item, playerText);
                Assert.Contains($"- {item}", staffText);
            }
        }

        Assert.Equal(3, versions.Sum(v => v.Staff.Count));
        Assert.DoesNotContain("Staff", playerText);
        Assert.Contains("Staff (players do not see this list)", staffText);
        Assert.Contains("Nothing players see changed in this version.", playerText); // 2026.10.05 has only Staff

        // The wiki page and the bulletin leave Staff out, as before.
        Assert.DoesNotContain("Shard Console", PatchNotes.RenderDokuWiki(versions));
        Assert.DoesNotContain(PatchNotes.BulletinLines(versions[0]), l => l.Contains("Shard Console"));
    }

    // ---------------------------------------------------------------- H3

    [Fact]
    public void NextReachesTheLastVersionAndPreviousComesBack()
    {
        using var v = new Viewer();

        ShardVersion.Version_OnCommand(new CommandEventArgs(v.Pm, "version", "", []));
        var g = v.Pm.FindGump<PatchHistoryGump>();
        Assert.NotNull(g);
        Assert.Equal(0, g.Page);
        var count = g.PageCount;

        // Opaque, and the current version is marked.
        Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());
        Assert.Contains(g.Entries.OfType<GumpImageTiled>(), t => t.GumpID == StaffHubGump.PanelTile && t.Width == PatchHistoryGump.Width - 20);
        var here = Assert.Single(g.Entries.OfType<GumpLabel>(), l => l.Text == "(you are here)");
        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Y == here.Y && l.Text == "Shattered Legacy 2026.10.05.2");
        Assert.DoesNotContain(g.Entries.OfType<GumpButton>(), b => b.ButtonID == PatchHistoryGump.BtnPrev);

        for (var i = 1; i < count; i++)
        {
            Assert.Contains(g.Entries.OfType<GumpButton>(), b => b.ButtonID == PatchHistoryGump.BtnNext);
            Press(g, v, PatchHistoryGump.BtnNext);
            g = v.Pm.FindGump<PatchHistoryGump>();
            Assert.Equal(i, g.Page);
        }

        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == $"Page {count} of {count}");
        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text.StartsWith("Shattered Legacy 2026.10.01", StringComparison.Ordinal) && !l.Text.Contains("2026.10.01."));
        Assert.DoesNotContain(g.Entries.OfType<GumpButton>(), b => b.ButtonID == PatchHistoryGump.BtnNext);
        Assert.Contains("fix: the lookout", AllText([g]));
        _out.WriteLine($"{count} pages for a player");

        for (var i = count - 2; i >= 0; i--)
        {
            Press(g, v, PatchHistoryGump.BtnPrev);
            g = v.Pm.FindGump<PatchHistoryGump>();
            Assert.Equal(i, g.Page);
        }

        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Text == $"Page 1 of {count}");
    }

    // ---------------------------------------------------------------- H4

    [Fact]
    public void TheBulletinHasNoWikiButtonAndAllPatchNotesOpensTheHistory()
    {
        using var v = new Viewer();
        var notes = Assert.IsType<PatchNoteVersion>(ShardVersion.UnseenNotesFor(v.Account));

        var g = new BulletinGump(v.Pm, ClusterFBulletinSystem.UnreadFor(v.Account), notes);
        var text = AllText([g]);
        Assert.DoesNotContain("wiki", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("full notes", text, StringComparison.OrdinalIgnoreCase);
        var button = Assert.Single(g.Entries.OfType<GumpButton>(), b => b.ButtonID == BulletinGump.AllNotesButton);
        Assert.Contains(g.Entries.OfType<GumpLabel>(), l => l.Y == button.Y + 2 && l.Text == "All patch notes");

        var from = v.Ns.SendBuffer.GetReadSpan().Length;
        Press(g, v, BulletinGump.AllNotesButton);
        var sent = v.Ns.SendBuffer.GetReadSpan()[from..];

        Assert.NotNull(v.Pm.FindGump<PatchHistoryGump>());
        Assert.True(sent.IndexOf(Encoding.ASCII.GetBytes("http")) < 0, "an open-URL packet was sent");
        Assert.Null(ShardVersion.UnseenNotesFor(v.Account)); // seen

        // More lines than fit: the rest point to All patch notes.
        var many = new PatchNoteVersion("2026.10.06");
        for (var i = 0; i < 20; i++)
        {
            many.Added.Add($"Line {i}: {D87}");
        }

        var crowded = new BulletinGump(v.Pm, new List<BulletinEntry>(), many);
        var shown = crowded.Entries.OfType<GumpHtml>().Count(h => Plain(h.Text).StartsWith("Added: Line", StringComparison.Ordinal));
        Assert.InRange(shown, 1, 19);
        Assert.Contains(crowded.Entries.OfType<GumpLabel>(), l => l.Text == $"... and {20 - shown} more (All patch notes)");
        CheckFits(crowded, 480 - 10, 72, 1000, "crowded bulletin");
        _out.WriteLine($"a crowded bulletin shows {shown} of 20 lines whole");
    }
}
