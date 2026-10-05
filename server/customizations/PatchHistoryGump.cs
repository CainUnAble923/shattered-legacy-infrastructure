// PatchHistoryGump.cs
//
// cc-P64 (F-15 follow-up, bug-list D87). Every version in CHANGELOG.md in one paged gump: [version opens it, and the
// login bulletin's All patch notes button. Newest first; each version's heading ("Shattered Legacy 2026.10.05.2", its
// date on the right, "(you are here)" beside the version this server runs), then its Added, Changed and Fixed lists.
// Staff (AccessLevel above Player) also see the "### Staff" list, in lavender and labeled as hidden from players.
//
// Lines wrap: each item is its own HTML block, as tall as ClusterFGumpText says its words need at the client's font
// widths (measured 8 pixels short of the block, as the achievements gump does), so no line is ever cut at the edge.
// Pages hold whole rows; a heading is kept with the row under it, and a page that starts inside a version or a list
// repeats its heading with "(continued)".
//
// Reads ShardVersion.All, the changelog ShardVersion parsed at server start; nothing here reads a file.
// Look: opaque like the Staff Hub and the achievements gump (D67, cc-P57 F): 9270 frame, solid tile 2624 inside it,
// no alpha region. Hues are the Staff Hub's (StaffHubGump.HueGold and the rest).

using System;
using System.Collections.Generic;
using Server.Network;

namespace Server.Gumps;

public class PatchHistoryGump : Gump
{
    public const int Width = 600;
    public const int Height = 500;
    public const int FrameRight = Width - 10;

    public const int ListY = 66;
    public const int ContentBottom = 440;

    public const int BtnClose = 0;
    public const int BtnPrev = 1;
    public const int BtnNext = 2;

    public const int VersionRowH = 24;
    public const int SectionRowH = 20;
    public const int ItemX = 40;
    public const int ItemW = Width - ItemX - 24;
    private const int RowGap = 2;

    public const int HueLavender = 1645; // the bulletin's Personal Notice hue
    private const string HexText = "#D8D8D8";
    private const string HexStaff = "#D7B8F0";

    private enum RowKind { Version, Section, Item }

    private sealed record Row(RowKind Kind, PatchNoteVersion Version, string Section, string Text, int Height,
        bool Continued = false);

    private readonly int _page;

    public PatchHistoryGump(Mobile viewer, int page = 0) : base(50, 50)
    {
        var staff = viewer?.AccessLevel > AccessLevel.Player;
        var pages = Paginate(BuildRows(ShardVersion.All, staff));

        PageCount = pages.Count;
        _page = Math.Clamp(page, 0, Math.Max(0, PageCount - 1));

        AddPage(0);
        AddBackground(0, 0, Width, Height, 9270);
        AddImageTiled(10, 10, Width - 20, Height - 20, StaffHubGump.PanelTile);

        AddLabel(20, 14, StaffHubGump.HueGold, $"{ShardVersion.ShardName} patch notes");
        AddLabel(20, 34, StaffHubGump.HueGrey, ShardVersion.Current.Length > 0
            ? $"You are on {ShardVersion.Current}. Newest first."
            : "Newest first.");
        AddImageTiled(10, 58, Width - 20, 2, 9304);

        var y = ListY;
        if (PageCount > 0)
        {
            foreach (var row in pages[_page])
            {
                DrawRow(row, y, staff);
                y += row.Height;
            }
        }
        else
        {
            AddLabel(20, y, StaffHubGump.HueGrey, "There are no patch notes yet.");
        }

        AddImageTiled(10, Height - 52, Width - 20, 2, 9304);

        var footY = Height - 38;
        if (_page > 0)
        {
            AddButton(20, footY, 4014, 4016, BtnPrev);
            AddLabel(54, footY + 2, StaffHubGump.HueWhite, "Previous");
        }

        var counter = $"Page {_page + 1} of {Math.Max(1, PageCount)}";
        AddLabel(Width / 2 - (ClusterFGumpText.Width(counter) + 2) / 2, footY + 2, StaffHubGump.HueGrey, counter);

        if (_page < PageCount - 1)
        {
            AddLabel(Width - 200, footY + 2, StaffHubGump.HueWhite, "Next");
            AddButton(Width - 166, footY, 4005, 4007, BtnNext);
        }

        AddLabel(Width - 90, footY + 2, StaffHubGump.HueWhite, "Close");
        AddButton(Width - 54, footY, 4017, 4019, BtnClose);
    }

    /// <summary>How many pages the history takes for this viewer.</summary>
    public int PageCount { get; }

    public int Page => _page;

    private void DrawRow(Row row, int y, bool staff)
    {
        switch (row.Kind)
        {
            case RowKind.Version:
                {
                    var here = row.Version.Version == ShardVersion.Current;
                    var title = $"{ShardVersion.ShardName} {row.Version.Version}{(row.Continued ? " (continued)" : "")}";
                    AddLabel(20, y + 2, here ? StaffHubGump.HueGold : StaffHubGump.HueWhite, title);
                    if (here)
                    {
                        AddLabel(20 + ClusterFGumpText.Width(title) + 12, y + 2, StaffHubGump.HueGreen, "(you are here)");
                    }

                    var date = row.Version.Date;
                    if (date.Length > 0)
                    {
                        AddLabel(FrameRight - 10 - (ClusterFGumpText.Width(date) + 2), y + 2, StaffHubGump.HueGrey, date);
                    }

                    break;
                }
            case RowKind.Section:
                {
                    var isStaff = row.Section == "Staff";
                    var text = isStaff ? "Staff (players do not see this list)" : row.Section;
                    if (row.Continued)
                    {
                        text += " (continued)";
                    }

                    AddLabel(30, y, isStaff ? HueLavender : StaffHubGump.HueGold, text);
                    break;
                }
            default:
                {
                    var color = row.Section == "Staff" && staff ? HexStaff : HexText;
                    AddHtml(ItemX, y, ItemW, row.Height - RowGap, $"<BASEFONT COLOR={color}>{row.Text}</BASEFONT>", false, false);
                    break;
                }
        }
    }

    /// <summary>An item's HTML block height: its wrapped lines, measured 8 pixels short of the block, and 4 to spare.</summary>
    public static int ItemHeight(string text) =>
        ClusterFGumpText.Lines(text, ItemW - 8) * ClusterFGumpText.LineHeight + 4;

    public static string ItemText(string item) => $"- {item}";

    private static List<Row> BuildRows(IReadOnlyList<PatchNoteVersion> versions, bool staff)
    {
        var rows = new List<Row>();
        foreach (var v in versions)
        {
            rows.Add(new Row(RowKind.Version, v, null, null, VersionRowH));

            var any = false;
            foreach (var (section, items) in Lists(v, staff))
            {
                if (items.Count == 0)
                {
                    continue;
                }

                any = true;
                rows.Add(new Row(RowKind.Section, v, section, null, SectionRowH));
                foreach (var item in items)
                {
                    var text = ItemText(item);
                    rows.Add(new Row(RowKind.Item, v, section, text, ItemHeight(text) + RowGap));
                }
            }

            if (!any)
            {
                var text = "- Nothing players see changed in this version.";
                rows.Add(new Row(RowKind.Item, v, null, text, ItemHeight(text) + RowGap));
            }
        }

        return rows;
    }

    private static IEnumerable<(string Section, List<string> Items)> Lists(PatchNoteVersion v, bool staff)
    {
        foreach (var s in v.Sections())
        {
            yield return s;
        }

        if (staff)
        {
            yield return ("Staff", v.Staff);
        }
    }

    /// <summary>
    /// Pages of whole rows that fit between ListY and ContentBottom. A heading never ends a page (it moves on with the
    /// row under it), and a page that starts inside a version or a list repeats that heading, marked continued.
    /// </summary>
    private static List<List<Row>> Paginate(List<Row> rows)
    {
        const int room = ContentBottom - ListY;
        var pages = new List<List<Row>>();
        var page = new List<Row>();
        var used = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            // The row and the headings kept with it: a version heading takes its section heading and first item along.
            var chain = rows[i].Height;
            for (var j = i; j < rows.Count - 1 && rows[j].Kind != RowKind.Item; j++)
            {
                chain += rows[j + 1].Height;
            }

            if (page.Count > 0 && used + chain > room)
            {
                pages.Add(page);
                page = new List<Row>();
                used = 0;
            }

            if (page.Count == 0 && rows[i].Kind != RowKind.Version)
            {
                var v = rows[i].Version;
                page.Add(new Row(RowKind.Version, v, null, null, VersionRowH, true));
                used += VersionRowH;
                if (rows[i].Kind == RowKind.Item && rows[i].Section != null)
                {
                    page.Add(new Row(RowKind.Section, v, rows[i].Section, null, SectionRowH, true));
                    used += SectionRowH;
                }
            }

            page.Add(rows[i]);
            used += rows[i].Height;
        }

        if (page.Count > 0)
        {
            pages.Add(page);
        }

        return pages;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var m = sender.Mobile;
        if (m == null)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case BtnPrev:
                m.SendGump(new PatchHistoryGump(m, _page - 1));
                break;
            case BtnNext:
                m.SendGump(new PatchHistoryGump(m, _page + 1));
                break;
        }
    }
}
