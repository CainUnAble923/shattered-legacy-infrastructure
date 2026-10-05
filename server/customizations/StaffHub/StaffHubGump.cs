// StaffHubGump.cs
//
// cc-P51 (F-30). The Staff Hub gump [SL opens, drawn to Chase's approved mockup (Design canvas "Staff Hub Mockup",
// transcribed in the cc-P51 brief): 560 x 440 on 9270, four tabs on 2445 at y 42, the content panel at (16,74)
// 528 x 324, the footer at (20,408). cc-P52 Part F (D67): the panel was an alpha region and the paperdoll and world
// showed through the text; it is now the solid black tile 2624 with no alpha region, so it reads over a busy screen. Go arrows 4005, close and delete 4017, category gems 2117/2118, text
// buttons on 2445. What each tab shows and does is ClusterFStaffHub's; this file only draws and dispatches.
//
// Hues, taken from our other gumps' named constants: gold headings 0x386 (ClusterFStatInspect.HueHdr, "gold - section
// headers"), white text 1153 (the commonest body hue in our gumps after 999, and white where 999 is grey), grey notes
// 0x3B2 (ForestersGuildmasterGump, "grey"), red 0x20 (ClusterFStatInspect.HueNeg), green 0x44 (DevTestingCrystal
// HueActive, "Bright green"). Command names are bold HTML in the same colours as hex.
//
// Button IDs are one scheme across every tab (the constants below), so a response is routed by its ID alone and no two
// tabs share one. Every row button indexes this gump's own snapshot of what it drew, and each action re-checks the live
// state (the command still registered, the character still there and online), so a stale gump fails safely.

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server;

public enum StaffHubPrompt
{
    Run,
    Grant,
    SaveSpot
}

public class StaffHubGump : Gump
{
    public const int Width = 560;
    public const int Height = 440;

    public const int HueGold = 0x386;
    public const int HueWhite = 1153;
    public const int HueGrey = 0x3B2;
    public const int HueRed = 0x20;
    public const int HueGreen = 0x44;

    private const string HexWhite = "#FFFFFF";
    private const string HexRed = "#FF5A5A";
    private const string HexGrey = "#9A9A9A";
    private const string HexText = "#D8D8D8";

    // ---- button IDs (one scheme for every tab) ----
    public const int BtnClose = 0;
    public const int BtnTabBase = 1;          // 1 Commands, 2 Player, 3 Travel (Events has no button yet)
    public const int BtnCategoryBase = 10;    // 10..15
    public const int BtnPagePrev = 20;
    public const int BtnPageNext = 21;
    public const int BtnPlayerTarget = 30;
    public const int BtnPlayerSearch = 31;
    public const int BtnPlayerGo = 32;
    public const int BtnPlayerBring = 33;
    public const int BtnPlayerProps = 34;
    public const int BtnSaveHere = 40;
    public const int BtnGoTo = 41;
    public const int BtnHallsBack = 42;
    public const int BtnSpotsPrev = 43;
    public const int BtnSpotsNext = 44;
    public const int BtnRunBase = 1000;
    public const int BtnDryRunBase = 2000;
    public const int BtnGrantBase = 3000;
    public const int BtnPickBase = 3500;
    public const int BtnPlaceBase = 4000;
    public const int BtnHallBase = 4100;
    public const int BtnSpotGoBase = 4200;
    public const int BtnSpotDeleteBase = 4300;
    public const int BtnRecentBase = 4400;
    public const int BtnFacetBase = 4500;     // cc-P52: 4500..4505, ClusterFStaffHub.ChangeFacets

    // ---- text entry IDs ----
    public const int TextSearch = 1;
    public const int TextGoX = 10;
    public const int TextGoY = 11;
    public const int TextGoZ = 12;
    public const int TextGoFacet = 13;

    public const int RowsPerPage = 4;
    private const int RowHeight = 72;

    // cc-P52 Part F: the content panel, solid (no alpha region), and the Player tab's rows block.
    public const int PanelTile = 2624;
    public const int PanelX = 16, PanelY = 74, PanelW = 528, PanelH = 324;
    public const int PlayerRowsX = 26, PlayerRowsY = 136, PlayerRowsW = 300, PlayerRowsH = 234;
    public const int LineHeight = 18;
    public const int SpotsPerPage = 5;
    public const int MaxGrantRows = 9;
    public const int MaxHallRows = 10;

    private static readonly string[] TabNames = ["Commands", "Player", "Travel", "Events"];

    private readonly StaffHubState _state;

    // What this gump drew, by row, for the buttons to index.
    private readonly List<string> _rows = [];
    private readonly List<string> _grants = [];
    private readonly List<Mobile> _matches = [];
    private readonly List<ClusterFStaffHub.ShardPlace> _places = [];
    private readonly List<ClusterFStaffHub.ShardPlace> _halls = [];
    private readonly List<StaffSpot> _spots = [];
    private readonly List<int> _spotIndex = [];
    private readonly List<StaffSpot> _recent = [];

    public StaffHubGump(Mobile from, StaffHubState state) : base(40, 40)
    {
        _state = state ?? new StaffHubState();

        if (_state.Selected?.Deleted == true)
        {
            _state.Selected = null;
        }

        AddPage(0);
        AddBackground(0, 0, Width, Height, 9270);

        AddLabel(20, 14, HueGold, "Shattered Legacy Staff Hub");
        if (ClusterFStaffHub.IsTestShard)
        {
            AddLabel(400, 16, HueGold, "TEST");
        }
        else
        {
            AddLabel(400, 16, HueGreen, "LIVE");
        }

        AddButton(514, 10, 4017, 4019, BtnClose);

        for (var i = 0; i < TabNames.Length; i++)
        {
            var x = 20 + i * 112;
            var tab = (StaffHubTab)i;
            if (tab == StaffHubTab.Events)
            {
                AddImage(x, 42, 2445);
                CenterLabel(x, 44, 108, HueGrey, "Events (soon)");
            }
            else
            {
                AddButton(x, 42, 2445, 2445, BtnTabBase + i);
                CenterLabel(x, 44, 108, _state.Tab == tab ? HueGold : HueWhite, TabNames[i]);
            }
        }

        AddImageTiled(PanelX, PanelY, PanelW, PanelH, PanelTile);

        switch (_state.Tab)
        {
            case StaffHubTab.Player: DrawPlayer(from); break;
            case StaffHubTab.Travel: DrawTravel(from); break;
            case StaffHubTab.Events:
            case StaffHubTab.Commands:
            default: DrawCommands(from); break;
        }

        AddLabel(20, 408, HueGrey, "[SL to open. GameMaster and up.");
    }

    public StaffHubState State => _state;

    // What each row button of this gump points at, in button order (read by the facts).
    public IReadOnlyList<string> Rows => _rows;
    public IReadOnlyList<string> Grants => _grants;
    public IReadOnlyList<Mobile> Matches => _matches;
    public IReadOnlyList<ClusterFStaffHub.ShardPlace> Places => _places;
    public IReadOnlyList<ClusterFStaffHub.ShardPlace> Halls => _halls;
    public IReadOnlyList<StaffSpot> Spots => _spots;
    public IReadOnlyList<StaffSpot> Recent => _recent;

    private void CenterLabel(int x, int y, int width, int hue, string text) =>
        AddLabel(x + Math.Max(0, (width - text.Length * 7) / 2), y, hue, text);

    private void TextButton(int x, int y, int id, string text, bool enabled = true)
    {
        if (enabled)
        {
            AddButton(x, y, 2445, 2445, id);
        }
        else
        {
            AddImage(x, y, 2445);
        }

        CenterLabel(x, y + 2, 108, enabled ? HueWhite : HueGrey, text);
    }

    private void EntryBox(int x, int y, int width, int id, string text)
    {
        AddBackground(x, y, width, 24, 9350);
        AddTextEntry(x + 4, y + 2, width - 8, 20, 0, id, text ?? "");
    }

    // ---------------------------------------------------------------- Commands

    private void DrawCommands(Mobile from)
    {
        var categories = ClusterFStaffHub.Categories;
        for (var i = 0; i < categories.Length; i++)
        {
            var y = 82 + i * 26;
            var selected = _state.Category == categories[i];
            AddButton(26, y, selected ? 2118 : 2117, 2118, BtnCategoryBase + i);
            AddLabel(48, y - 2, selected ? HueGold : HueWhite, ClusterFStaffHub.CategoryLabel(categories[i]));
        }

        var commands = ClusterFStaffHub.CommandsIn(from, _state.Category);
        var pages = Math.Max(1, (commands.Count + RowsPerPage - 1) / RowsPerPage);
        _state.CommandPage = Math.Clamp(_state.CommandPage, 0, pages - 1);

        if (commands.Count == 0)
        {
            AddLabel(156, 84, HueGrey,
                _state.Category == CommandCategory.WorldRemoval && from.AccessLevel < ClusterFStaffHub.RemovalAccess
                    ? "World removal needs Administrator."
                    : "No commands in this category.");
        }

        for (var k = 0; k < RowsPerPage; k++)
        {
            var index = _state.CommandPage * RowsPerPage + k;
            if (index >= commands.Count)
            {
                break;
            }

            var c = commands[index];
            var y = 82 + k * RowHeight;
            var refusal = ClusterFStaffHub.CanRun(from, c);
            var runnable = refusal == ClusterFStaffHub.Refusal.None;
            var color = !runnable ? HexGrey : c.NeedsConfirm ? HexRed : HexWhite;

            AddHtml(156, y, 264, 18, $"<B><BASEFONT COLOR={color}>{ClusterFStaffHub.Html(c.Name)}</BASEFONT></B>");
            AddHtml(156, y + 16, 264, 34, $"<BASEFONT COLOR={HexText}>{ClusterFStaffHub.Html(c.Summary)}</BASEFONT>");
            AddLabelCropped(156, y + 50, 264, 18, HueGrey, ClusterFStaffHub.CommandMeta(c));

            _rows.Add(c.Name);
            var row = _rows.Count - 1;

            if (runnable)
            {
                TextButton(428, y + 2, BtnRunBase + row, "Run");
                if (c.HasDryRun)
                {
                    TextButton(428, y + 28, BtnDryRunBase + row, "Dry run");
                }
            }
            else
            {
                AddLabelCropped(428, y + 4, 108, 18, HueGrey,
                    refusal == ClusterFStaffHub.Refusal.TestOnlyOnLive ? "Test shard only" : "Not available");
            }
        }

        if (pages > 1)
        {
            if (_state.CommandPage > 0)
            {
                AddButton(156, 374, 4014, 4016, BtnPagePrev);
            }

            AddLabel(192, 376, HueGrey, $"Page {_state.CommandPage + 1} of {pages}");

            if (_state.CommandPage < pages - 1)
            {
                AddButton(300, 374, 4005, 4007, BtnPageNext);
            }
        }
    }

    // ---------------------------------------------------------------- Player

    private void DrawPlayer(Mobile from)
    {
        TextButton(26, 80, BtnPlayerTarget, "Target");
        AddLabel(142, 82, HueGrey, "or name");
        EntryBox(194, 80, 116, TextSearch, _state.Search);
        AddButton(314, 80, 4005, 4007, BtnPlayerSearch);
        AddLabel(440, 82, HueGrey, $"Online: {ClusterFStaffHub.OnlineCount}");

        var selected = _state.Selected;

        if (selected == null && _state.Matches.Count > 1)
        {
            AddLabel(26, 112, HueGold, $"{_state.Matches.Count} characters match '{_state.Search}':");

            for (var i = 0; i < _state.Matches.Count; i++)
            {
                var m = _state.Matches[i];
                var y = 138 + i * 22;
                _matches.Add(m);
                if (m.Deleted)
                {
                    AddLabel(62, y, HueGrey, "(deleted)");
                    continue;
                }

                AddButton(26, y, 4005, 4007, BtnPickBase + i);
                AddLabelCropped(62, y, 136, 18, HueWhite, m.Name);
                AddLabelCropped(200, y, 330, 18, HueGrey,
                    $"{(ClusterFStaffHub.IsOnline(m) ? "online" : "offline")}, account {ClusterFStaffHub.AccountName(m)}");
            }

            return;
        }

        if (selected == null)
        {
            AddLabel(26, 112, HueGrey, "Target a character, or search by name (online or offline).");
            return;
        }

        var online = ClusterFStaffHub.IsOnline(selected);
        var name = selected.Name ?? "(no name)";
        AddLabelCropped(26, 112, 170, 18, HueGold, name);
        AddLabelCropped(200, 112, 136, 18, HueGrey, $"account {ClusterFStaffHub.AccountName(selected)}{(online ? "" : ", offline")}");

        // cc-P52 Part F (D67): one wrapped block, so a long value takes a second line instead of being cropped. If the
        // estimate ever outgrows the block, it scrolls rather than cut anything off.
        var rows = ClusterFStaffHub.PlayerRows(selected);
        var html = ClusterFStaffHub.PlayerRowsHtml(rows, PlayerRowsW - 20, out var lines);
        AddHtml(PlayerRowsX, PlayerRowsY, PlayerRowsW, PlayerRowsH, html, false, lines * LineHeight > PlayerRowsH);

        // The Levels and Loops placeholder, one line now (F-12 is not built), to give the rows the room.
        AddHtml(26, 374, 296, 20, $"<CENTER><BASEFONT COLOR={HexGrey}>Levels and Loops panel arrives with F-12.</BASEFONT></CENTER>");

        AddLabel(340, 114, HueGold, "Actions");
        ActionRow(340, 134, BtnPlayerGo, "Go to player", online);
        ActionRow(340, 158, BtnPlayerBring, "Bring player here", online);
        ActionRow(340, 182, BtnPlayerProps, "Props", true);

        AddLabel(340, 210, HueGold, "Grants");
        var grants = ClusterFStaffHub.CommandsIn(from, CommandCategory.Grant);
        for (var i = 0; i < grants.Count && i < MaxGrantRows; i++)
        {
            var g = grants[i];
            var y = 230 + i * 18;
            _grants.Add(g.Name);

            if (ClusterFStaffHub.CanRun(from, g) == ClusterFStaffHub.Refusal.None)
            {
                AddButton(340, y + 3, 5601, 5605, BtnGrantBase + i);
                AddLabelCropped(358, y, 178, 18, HueWhite, g.Name);
            }
            else
            {
                AddLabelCropped(358, y, 178, 18, HueGrey, $"{g.Name} (test only)");
            }
        }

        if (grants.Count > MaxGrantRows)
        {
            AddLabel(358, 230 + MaxGrantRows * 18, HueGrey, $"and {grants.Count - MaxGrantRows} more: Commands tab");
        }
    }

    private void ActionRow(int x, int y, int id, string text, bool enabled)
    {
        if (enabled)
        {
            AddButton(x, y, 4005, 4007, id);
            AddLabel(x + 36, y + 2, HueWhite, text);
        }
        else
        {
            AddLabel(x + 36, y + 2, HueGrey, $"{text} (offline)");
        }
    }

    // ---------------------------------------------------------------- Travel

    private void DrawTravel(Mobile from)
    {
        if (_state.ShowGuildHalls)
        {
            AddButton(26, 82, 4014, 4016, BtnHallsBack);
            AddLabel(62, 82, HueGold, "Guild halls");

            var halls = ClusterFStaffHub.GuildHalls();
            for (var i = 0; i < halls.Count && i < MaxHallRows; i++)
            {
                var h = halls[i];
                var y = 106 + i * 22;
                _halls.Add(h);
                if (h.Available)
                {
                    AddButton(26, y, 4005, 4007, BtnHallBase + i);
                }

                AddLabelCropped(62, y, 150, 18, h.Available ? HueWhite : HueGrey, h.Name);
                AddLabel(214, y, HueGrey, h.Facet);
            }
        }
        else
        {
            AddLabel(26, 82, HueGold, "Shard places");

            var places = ClusterFStaffHub.ShardPlaces();
            for (var i = 0; i < places.Count; i++)
            {
                var p = places[i];
                var y = 106 + i * 24;
                _places.Add(p);

                if (p.Name == ClusterFStaffHub.GuildHallsRow)
                {
                    AddButton(26, y, 4005, 4007, BtnPlaceBase + i);
                    AddLabel(62, y, HueWhite, $"{p.Name} ({ClusterFStaffHub.GuildHalls().Count})");
                    AddLabel(214, y, HueGrey, "list");
                }
                else if (p.Available)
                {
                    AddButton(26, y, 4005, 4007, BtnPlaceBase + i);
                    AddLabel(62, y, HueWhite, p.Name);
                    AddLabel(214, y, HueGrey, p.Facet);
                }
                else
                {
                    AddLabel(62, y, HueGrey, p.Name);
                    AddLabel(214, y, HueGrey, p.Unavailable ?? "not loaded");
                }
            }
        }

        // cc-P52 Part F (D67): the same x, y on another facet, z as [Go x y picks it.
        if (!_state.ShowGuildHalls)
        {
            AddLabel(26, 298, HueGold, "Same x, y on another facet");
            var facets = ClusterFStaffHub.ChangeFacets;
            for (var i = 0; i < facets.Length; i++)
            {
                var x = 26 + i % 3 * 86;
                var y = 318 + i / 3 * 20;
                var f = facets[i];
                var here = f == from.Map;
                if (!here && f != null && f != Map.Internal)
                {
                    AddButton(x, y + 3, 5601, 5605, BtnFacetBase + i);
                }

                AddLabel(x + 16, y, here ? HueGold : HueWhite, f?.Name ?? "?");
            }
        }

        // Saved spots, newest first.
        AddLabel(292, 82, HueGold, "Saved spots");
        TextButton(428, 80, BtnSaveHere, "Save here");

        var spots = ClusterFStaffHubSpots.SpotsOf(from);
        var spotPages = Math.Max(1, (spots.Count + SpotsPerPage - 1) / SpotsPerPage);
        _state.SpotPage = Math.Clamp(_state.SpotPage, 0, spotPages - 1);

        if (spots.Count == 0)
        {
            AddLabel(292, 106, HueGrey, "None yet: Save here names where you stand.");
        }

        for (var k = 0; k < SpotsPerPage; k++)
        {
            var shown = _state.SpotPage * SpotsPerPage + k;
            if (shown >= spots.Count)
            {
                break;
            }

            var index = spots.Count - 1 - shown;
            var s = spots[index];
            var y = 106 + k * 20;
            _spots.Add(s);
            _spotIndex.Add(index);

            AddButton(292, y, 4005, 4007, BtnSpotGoBase + k);
            AddLabelCropped(326, y, 118, 18, HueWhite, s.Name);
            AddLabel(448, y, HueGrey, s.Map?.Name ?? "");
            AddButton(510, y, 4017, 4019, BtnSpotDeleteBase + k);
        }

        if (spotPages > 1)
        {
            if (_state.SpotPage > 0)
            {
                AddButton(292, 210, 4014, 4016, BtnSpotsPrev);
            }

            AddLabel(328, 212, HueGrey, $"{_state.SpotPage + 1} of {spotPages}");

            if (_state.SpotPage < spotPages - 1)
            {
                AddButton(380, 210, 4005, 4007, BtnSpotsNext);
            }
        }

        AddLabel(292, 236, HueGold, "Recent");
        var recent = ClusterFStaffHubSpots.RecentOf(from);
        if (recent.Count == 0)
        {
            AddLabel(292, 258, HueGrey, "Places you go through the hub.");
        }

        for (var k = 0; k < recent.Count; k++)
        {
            var r = recent[k];
            var y = 258 + k * 20;
            _recent.Add(r);
            AddButton(292, y, 4005, 4007, BtnRecentBase + k);
            AddLabelCropped(326, y, 150, 18, HueWhite, r.Name);
            AddLabel(480, y, HueGrey, r.Map?.Name ?? "");
        }

        AddLabel(26, 364, HueWhite, "Go to");
        AddLabel(72, 364, HueGrey, "x");
        EntryBox(84, 362, 54, TextGoX, "");
        AddLabel(144, 364, HueGrey, "y");
        EntryBox(156, 362, 54, TextGoY, "");
        AddLabel(216, 364, HueGrey, "z");
        EntryBox(228, 362, 44, TextGoZ, "");
        AddLabel(278, 364, HueGrey, "facet");
        EntryBox(314, 362, 92, TextGoFacet, from.Map?.Name ?? "");
        AddButton(412, 362, 4005, 4007, BtnGoTo);
    }

    // ---------------------------------------------------------------- responses

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        if (from == null)
        {
            return;
        }

        Respond(
            from,
            info.ButtonID,
            info.GetTextEntry(TextSearch),
            info.GetTextEntry(TextGoX),
            info.GetTextEntry(TextGoY),
            info.GetTextEntry(TextGoZ),
            info.GetTextEntry(TextGoFacet)
        );
    }

    /// <summary>
    /// The response, with the text entries already read (null where the gump had none). Public so the facts can press
    /// a button without building a client packet.
    /// </summary>
    public void Respond(
        Mobile from, int id, string search = null, string goX = null, string goY = null, string goZ = null,
        string goFacet = null)
    {
        if (from.Deleted || from.AccessLevel < ClusterFStaffHub.Access || id == BtnClose)
        {
            return;
        }

        var reopen = true;

        if (id is >= BtnTabBase and < BtnTabBase + 3)
        {
            _state.Tab = (StaffHubTab)(id - BtnTabBase);
        }
        else if (id >= BtnCategoryBase && id < BtnCategoryBase + ClusterFStaffHub.Categories.Length)
        {
            _state.Category = ClusterFStaffHub.Categories[id - BtnCategoryBase];
            _state.CommandPage = 0;
        }
        else if (id == BtnPagePrev)
        {
            _state.CommandPage--;
        }
        else if (id == BtnPageNext)
        {
            _state.CommandPage++;
        }
        else if (id >= BtnRunBase && id < BtnRunBase + _rows.Count)
        {
            reopen = BeginRun(from, _state, StaffHubPrompt.Run, _rows[id - BtnRunBase]);
        }
        else if (id >= BtnDryRunBase && id < BtnDryRunBase + _rows.Count)
        {
            ClusterFStaffHub.DryRun(from, _rows[id - BtnDryRunBase]);
        }
        else if (id == BtnPlayerTarget)
        {
            var state = _state;
            from.SendMessage("Target a character.");
            from.BeginTarget(-1, false, TargetFlags.None, (f, targeted) =>
            {
                if (targeted is PlayerMobile pm)
                {
                    state.Selected = pm;
                    state.Matches = [];
                }
                else
                {
                    f.SendMessage("That is not a player character.");
                }

                ClusterFStaffHub.Open(f, state);
            });
            reopen = false;
        }
        else if (id == BtnPlayerSearch)
        {
            _state.Search = search?.Trim() ?? "";
            var found = ClusterFStaffHub.FindCharacters(_state.Search);
            _state.Matches = [];

            if (found.Count == 0)
            {
                from.SendMessage($"No character is named like '{_state.Search}'.");
            }
            else if (found.Count == 1)
            {
                _state.Selected = found[0];
            }
            else
            {
                _state.Selected = null;
                _state.Matches = found;
            }
        }
        else if (id >= BtnPickBase && id < BtnPickBase + _matches.Count)
        {
            var m = _matches[id - BtnPickBase];
            if (m.Deleted)
            {
                from.SendMessage("That character no longer exists.");
            }
            else
            {
                _state.Selected = m;
                _state.Matches = [];
            }
        }
        else if (id is BtnPlayerGo or BtnPlayerBring or BtnPlayerProps)
        {
            var m = _state.Selected;
            if (m == null || m.Deleted)
            {
                _state.Selected = null;
                from.SendMessage("That character no longer exists.");
            }
            else if (id == BtnPlayerGo)
            {
                ClusterFStaffHub.GoToPlayer(from, m);
            }
            else if (id == BtnPlayerBring)
            {
                ClusterFStaffHub.BringPlayer(from, m);
            }
            else
            {
                ClusterFStaffHub.Props(from, m);
            }
        }
        else if (id >= BtnGrantBase && id < BtnGrantBase + _grants.Count)
        {
            if (_state.Selected == null || _state.Selected.Deleted)
            {
                _state.Selected = null;
                from.SendMessage("Select a character first.");
            }
            else
            {
                reopen = BeginRun(from, _state, StaffHubPrompt.Grant, _grants[id - BtnGrantBase]);
            }
        }
        else if (id >= BtnPlaceBase && id < BtnPlaceBase + _places.Count)
        {
            var p = _places[id - BtnPlaceBase];
            if (p.Name == ClusterFStaffHub.GuildHallsRow)
            {
                _state.ShowGuildHalls = true;
            }
            else if (p.Available)
            {
                ClusterFStaffHub.GoTo(from, p.Name, p.Map, p.Location);
            }
        }
        else if (id >= BtnHallBase && id < BtnHallBase + _halls.Count)
        {
            var h = _halls[id - BtnHallBase];
            if (h.Available)
            {
                ClusterFStaffHub.GoTo(from, h.Name, h.Map, h.Location);
            }
        }
        else if (id == BtnHallsBack)
        {
            _state.ShowGuildHalls = false;
        }
        else if (id >= BtnSpotGoBase && id < BtnSpotGoBase + _spots.Count)
        {
            var s = _spots[id - BtnSpotGoBase];
            ClusterFStaffHub.GoTo(from, s.Name, s.Map, s.Location);
        }
        else if (id >= BtnSpotDeleteBase && id < BtnSpotDeleteBase + _spots.Count)
        {
            var k = id - BtnSpotDeleteBase;
            from.SendMessage(ClusterFStaffHubSpots.Delete(from, _spotIndex[k], _spots[k].Name)
                ? $"Deleted '{_spots[k].Name}'."
                : "That spot was already changed; nothing deleted.");
        }
        else if (id == BtnSpotsPrev)
        {
            _state.SpotPage--;
        }
        else if (id == BtnSpotsNext)
        {
            _state.SpotPage++;
        }
        else if (id >= BtnRecentBase && id < BtnRecentBase + _recent.Count)
        {
            var r = _recent[id - BtnRecentBase];
            ClusterFStaffHub.GoTo(from, r.Name, r.Map, r.Location);
        }
        else if (id == BtnSaveHere)
        {
            from.SendGump(new StaffHubPromptGump(_state, StaffHubPrompt.SaveSpot, null, ""));
            reopen = false;
        }
        else if (id >= BtnFacetBase && id < BtnFacetBase + ClusterFStaffHub.ChangeFacets.Length)
        {
            if (!ClusterFStaffHub.ChangeFacet(from, ClusterFStaffHub.ChangeFacets[id - BtnFacetBase], out var facetError))
            {
                from.SendMessage(HueRed, facetError);
            }
        }
        else if (id == BtnGoTo)
        {
            if (ClusterFStaffHub.TryParseGoTo(from, goX, goY, goZ, goFacet, out var map, out var loc, out var error))
            {
                ClusterFStaffHub.GoTo(from, $"{loc.X}, {loc.Y}, {loc.Z}", map, loc);
            }
            else
            {
                from.SendMessage(HueRed, error);
            }
        }

        if (reopen)
        {
            ClusterFStaffHub.Open(from, _state);
        }
    }

    /// <summary>
    /// A Run or Grant button: the argument prompt when [Usage] shows arguments, else the confirm when the row needs one,
    /// else run now. Returns whether the hub should reopen at once (false while a prompt is up).
    /// </summary>
    internal static bool BeginRun(Mobile from, StaffHubState state, StaffHubPrompt purpose, string name)
    {
        var c = ClusterFStaffHub.Find(name);
        var refusal = ClusterFStaffHub.CanRun(from, c);

        if (refusal != ClusterFStaffHub.Refusal.None)
        {
            from.SendMessage(HueRed, ClusterFStaffHub.RefusalMessage(refusal, name));
            return true;
        }

        if (c.TakesArguments)
        {
            from.SendGump(new StaffHubPromptGump(state, purpose, c.Name, c.Arguments));
            return false;
        }

        if (c.NeedsConfirm)
        {
            from.SendGump(new StaffHubConfirmGump(state, purpose, c.Name, ""));
            return false;
        }

        Execute(from, state, purpose, c.Name, "");
        return true;
    }

    internal static void Execute(Mobile from, StaffHubState state, StaffHubPrompt purpose, string name, string args)
    {
        if (purpose == StaffHubPrompt.Grant)
        {
            ClusterFStaffHub.RunGrant(from, name, args, state.Selected);
        }
        else
        {
            ClusterFStaffHub.Run(from, name, args);
        }
    }
}

/// <summary>
/// The small text entry: a command's arguments (prefilled with what its [Usage] shows after the name), or a saved
/// spot's name.
/// </summary>
public class StaffHubPromptGump : Gump
{
    public const int BtnOk = 1;
    public const int TextValue = 1;

    private readonly StaffHubState _state;

    public StaffHubPromptGump(StaffHubState state, StaffHubPrompt purpose, string command, string prefill) : base(120, 140)
    {
        _state = state;
        Purpose = purpose;
        Command = command;
        Prefill = prefill ?? "";

        var usage = command == null ? null : ClusterFStaffHub.Find(command)?.Usage;

        AddPage(0);
        AddBackground(0, 0, 420, 150, 9270);
        AddLabel(20, 14, StaffHubGump.HueGold, purpose == StaffHubPrompt.SaveSpot ? "Save this spot" : $"[{command}");
        AddButton(374, 10, 4017, 4019, 0);
        AddLabelCropped(20, 40, 380, 18, StaffHubGump.HueGrey,
            purpose == StaffHubPrompt.SaveSpot
                ? $"A name, up to {ClusterFStaffHubSpots.MaxNameLength} characters."
                : $"Usage: {usage}{(purpose == StaffHubPrompt.Grant ? $"   (then aimed at {state.Selected?.Name})" : "")}");
        AddBackground(20, 64, 380, 26, 9350);
        AddTextEntry(26, 67, 368, 20, 0, TextValue, Prefill);
        AddButton(292, 104, 2445, 2445, BtnOk);
        AddLabel(320, 106, StaffHubGump.HueWhite, purpose == StaffHubPrompt.SaveSpot ? "Save" : "Run");
    }

    public StaffHubPrompt Purpose { get; }
    public string Command { get; }
    public string Prefill { get; }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } from)
        {
            Respond(from, info.ButtonID == BtnOk, info.GetTextEntry(TextValue));
        }
    }

    public void Respond(Mobile from, bool ok, string text)
    {
        if (from.Deleted || from.AccessLevel < ClusterFStaffHub.Access)
        {
            return;
        }

        if (!ok)
        {
            ClusterFStaffHub.Open(from, _state);
            return;
        }

        text = text?.Trim() ?? "";

        if (Purpose == StaffHubPrompt.SaveSpot)
        {
            ClusterFStaffHubSpots.SaveHere(from, text, out var message);
            from.SendMessage(message);
            ClusterFStaffHub.Open(from, _state);
            return;
        }

        // An unedited template ("<guildKey> [amount]") is not an argument list.
        if (text.Length > 0 && text == Prefill.Trim() && text.IndexOfAny(['<', '[', '|']) >= 0)
        {
            from.SendMessage(StaffHubGump.HueRed, "Edit the arguments first, or clear the box to run with none.");
            from.SendGump(new StaffHubPromptGump(_state, Purpose, Command, Prefill));
            return;
        }

        var c = ClusterFStaffHub.Find(Command);
        if (c?.NeedsConfirm == true)
        {
            from.SendGump(new StaffHubConfirmGump(_state, Purpose, Command, text));
            return;
        }

        StaffHubGump.Execute(from, _state, Purpose, Command, text);
        ClusterFStaffHub.Open(from, _state);
    }
}

/// <summary>Yes or no before a command that duplicates, deletes again, or removes world content.</summary>
public class StaffHubConfirmGump : Gump
{
    public const int BtnYes = 1;
    public const int BtnNo = 2;

    private readonly StaffHubState _state;

    public StaffHubConfirmGump(StaffHubState state, StaffHubPrompt purpose, string command, string args) : base(120, 140)
    {
        _state = state;
        Purpose = purpose;
        Command = command;
        Args = args ?? "";

        var c = ClusterFStaffHub.Find(command);
        var why = c == null ? "" :
            c.Category == CommandCategory.WorldRemoval ? "It removes world content." :
            c.Declaration.Rerun == CommandRerun.Duplicates ? "Running it again places a second copy." :
            c.Declaration.Rerun == CommandRerun.DeletesAgain ? "A second run deletes again whatever matches." : "";

        AddPage(0);
        AddBackground(0, 0, 420, 140, 9270);
        AddLabel(20, 14, StaffHubGump.HueRed, "Confirm");
        AddLabelCropped(20, 40, 380, 18, StaffHubGump.HueWhite, $"Run [{command}{(Args.Length > 0 ? " " + Args : "")}?");
        AddLabelCropped(20, 60, 380, 18, StaffHubGump.HueGrey, why);
        AddButton(180, 96, 2445, 2445, BtnYes);
        AddLabel(222, 98, StaffHubGump.HueRed, "Yes");
        AddButton(292, 96, 2445, 2445, BtnNo);
        AddLabel(336, 98, StaffHubGump.HueWhite, "No");
    }

    public StaffHubPrompt Purpose { get; }
    public string Command { get; }
    public string Args { get; }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } from)
        {
            Respond(from, info.ButtonID == BtnYes);
        }
    }

    public void Respond(Mobile from, bool yes)
    {
        if (from.Deleted || from.AccessLevel < ClusterFStaffHub.Access)
        {
            return;
        }

        if (yes)
        {
            StaffHubGump.Execute(from, _state, Purpose, Command, Args);
        }

        ClusterFStaffHub.Open(from, _state);
    }
}
