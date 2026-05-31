using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Items;

public class PetMimicFormJournalGump : Gump
{
    private const int GumpW      = 400;
    private const int GumpH      = 420;
    private const int PadX       = 16;
    private const int RowH       = 22;
    private const int RowsPerPage = 12;

    private readonly Mobile   _from;
    private readonly PetMimic _mimic;
    private readonly string   _filter;
    private readonly int      _page;

    // Button ID ranges
    private const int BtnClose  = 1;
    private const int BtnSearch = 2;
    private const int BtnPrev   = 3;
    private const int BtnNext   = 4;
    private const int BtnFormBase = 100; // BtnFormBase + index = switch to that form

    private const int TextEntrySearch = 0;

    public PetMimicFormJournalGump(Mobile from, PetMimic mimic, string filter = "", int page = 0)
        : base(120, 80)
    {
        _from   = from;
        _mimic  = mimic;
        _filter = filter ?? "";
        _page   = page;

        AddBackground(0, 0, GumpW, GumpH, 9200);

        // ── Title ──────────────────────────────────────────────────────────
        AddLabel(GumpW / 2 - 72, 10, 0x386, "Pet Mimic — Known Forms");

        // ── Category lock line ─────────────────────────────────────────────
        var catText = mimic.LockedCategory == MimicCategory.None
            ? "Dormant — no forms yet"
            : $"Category: {mimic.LockedCategory}  ({mimic.KnownFormItemIDs.Count} known)";
        AddLabel(PadX, 30, 0x47E, catText);

        // ── Search bar ─────────────────────────────────────────────────────
        AddLabel(PadX, 52, 0x455, "Search:");
        AddBackground(70, 50, 240, 22, 9350);
        AddTextEntry(72, 52, 236, 18, 0x44, TextEntrySearch, _filter);
        AddButton(316, 50, 4005, 4007, BtnSearch, GumpButtonType.Reply, 0);
        AddLabel(352, 52, 0x455, "Go");

        // ── Build filtered list ────────────────────────────────────────────
        var rows = BuildRows(mimic, _filter);

        // ── Pagination ────────────────────────────────────────────────────
        var totalPages = Math.Max(1, (rows.Count + RowsPerPage - 1) / RowsPerPage);
        var clampedPage = Math.Clamp(_page, 0, totalPages - 1);
        var start = clampedPage * RowsPerPage;
        var end   = Math.Min(start + RowsPerPage, rows.Count);

        // Header
        AddImageTiled(PadX, 78, GumpW - PadX * 2, 2, 9354);
        AddLabel(PadX,       80, 0x455, "Form");
        AddLabel(PadX + 180, 80, 0x455, "Base Type");
        AddLabel(PadX + 280, 80, 0x455, "Active");
        AddImageTiled(PadX, 96, GumpW - PadX * 2, 2, 9354);

        // Rows
        int y = 100;
        if (rows.Count == 0)
        {
            AddLabel(PadX, y, 0x3B2, _filter.Length > 0
                ? "No forms match that search."
                : "No forms eaten yet.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var row       = rows[i];
                var isActive  = mimic.ActiveFormItemID == row.ItemID;
                var nameHue   = isActive ? 0x44 : 0x47E;
                var baseHue   = isActive ? 0x44 : 0x3B2;

                // Clickable form name button (reply = switch form)
                AddButton(PadX, y + 2, 4005, 4007, BtnFormBase + row.OriginalIndex, GumpButtonType.Reply, 0);
                AddLabel(PadX + 35, y, nameHue, Truncate(row.DisplayName, 22));
                AddLabel(PadX + 180, y, baseHue, Truncate(row.BaseName, 14));

                if (isActive)
                {
                    AddLabel(PadX + 280, y, 0x44, "* Active");
                }

                y += RowH;
            }
        }

        AddImageTiled(PadX, y + 4, GumpW - PadX * 2, 2, 9354);

        // ── Page controls ─────────────────────────────────────────────────
        int pageY = GumpH - 46;
        if (clampedPage > 0)
        {
            AddButton(PadX, pageY, 4014, 4016, BtnPrev, GumpButtonType.Reply, 0);
            AddLabel(PadX + 35, pageY + 2, 0x455, "Prev");
        }

        AddLabel(GumpW / 2 - 30, pageY + 2, 0x47E, $"Page {clampedPage + 1} / {totalPages}");

        if (clampedPage < totalPages - 1)
        {
            AddButton(GumpW - 90, pageY, 4005, 4007, BtnNext, GumpButtonType.Reply, 0);
            AddLabel(GumpW - 55, pageY + 2, 0x455, "Next");
        }

        // ── Close ──────────────────────────────────────────────────────────
        AddButton(GumpW / 2 - 20, GumpH - 22, 4017, 4019, BtnClose, GumpButtonType.Reply, 0);
        AddLabel(GumpW / 2 + 15, GumpH - 20, 0x455, "Close");
    }

    // ── Row model ─────────────────────────────────────────────────────────

    private readonly struct FormRow
    {
        public int    OriginalIndex { get; init; }
        public int    ItemID        { get; init; }
        public string DisplayName   { get; init; }
        public string BaseName      { get; init; }
    }

    private static List<FormRow> BuildRows(PetMimic mimic, string filter)
    {
        var ids   = mimic.KnownFormItemIDs;
        var names = mimic.KnownFormNames;
        var bases = mimic.KnownFormBaseNames;
        var rows  = new List<FormRow>(ids.Count);

        var lowerFilter = filter.Trim().ToLowerInvariant();

        for (var i = 0; i < ids.Count; i++)
        {
            var display = i < names.Count ? names[i] : $"Form #{ids[i]:X4}";
            var base_   = i < bases.Count ? bases[i] : "";

            if (lowerFilter.Length > 0)
            {
                if (!display.ToLowerInvariant().Contains(lowerFilter) &&
                    !base_.ToLowerInvariant().Contains(lowerFilter))
                {
                    continue;
                }
            }

            rows.Add(new FormRow
            {
                OriginalIndex = i,
                ItemID        = ids[i],
                DisplayName   = display,
                BaseName      = base_
            });
        }

        return rows;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    // ── Response ──────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_mimic.Deleted) { return; }

        var searchText = info.GetTextEntry(TextEntrySearch)?.Trim() ?? "";

        switch (info.ButtonID)
        {
            case BtnClose:
                return;

            case BtnSearch:
                _from.SendGump(new PetMimicFormJournalGump(_from, _mimic, searchText, 0));
                return;

            case BtnPrev:
                _from.SendGump(new PetMimicFormJournalGump(_from, _mimic, _filter, _page - 1));
                return;

            case BtnNext:
                _from.SendGump(new PetMimicFormJournalGump(_from, _mimic, _filter, _page + 1));
                return;
        }

        if (info.ButtonID >= BtnFormBase)
        {
            var index = info.ButtonID - BtnFormBase;
            _mimic.SwitchForm(_from, index);
        }
    }
}
