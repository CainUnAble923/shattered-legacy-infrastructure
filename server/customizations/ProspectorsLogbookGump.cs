using System;
using System.Collections.Generic;
using System.Text;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Prospector's Logbook.
///
/// Two views:
///   Logbook     — scrollable HTML list of every ore discovery, all vein locations,
///                 first-found dates, region/facet, coordinates, amount mined.
///                 Reported veins shown in gold. Unreported count shown in footer.
///                 "Field Report" button opens the submission view if veins are pending.
///
///   FieldReport — table of unreported veins with per-vein reward previews.
///                 "Submit Field Report" does the same reward grant as visiting
///                 Velara Thorne in person — standing, vouchers, and gold to bank.
///                 Remote submission is the primary quality-of-life use of the logbook.
/// </summary>
public class ProspectorsLogbookGump : Gump
{
    public enum View { Logbook, FieldReport }

    private readonly PlayerMobile        _pm;
    private readonly ClusterFAccountData _data;
    private readonly View                _view;

    private const int W = 540;
    private const int H = 510;

    // Ore display order — standard tiers first, then extended.
    private static readonly string[] OreOrder =
    {
        "DullCopper", "ShadowIron", "Copper", "Bronze", "Gold",
        "Agapite", "Verite", "Valorite",
        "Platinum", "Toxic", "Blaze", "Frost",
        "Obsidian", "Mythril", "Adamantium", "Celestial",
    };

    // Ore display name and HTML color per ore key.
    private static readonly Dictionary<string, (string Name, string Color)> OreDisplay = new()
    {
        { "DullCopper",  ("Dull Copper",  "#A0522D") },
        { "ShadowIron",  ("Shadow Iron",  "#8899AA") },
        { "Copper",      ("Copper",       "#B87333") },
        { "Bronze",      ("Bronze",       "#CD7F32") },
        { "Gold",        ("Gold",         "#DAA520") },
        { "Agapite",     ("Agapite",      "#FF69B4") },
        { "Verite",      ("Verite",       "#32CD32") },
        { "Valorite",    ("Valorite",     "#6495ED") },
        { "Platinum",    ("Platinum",     "#C0C0C0") },
        { "Toxic",       ("Toxic",        "#7FFF00") },
        { "Blaze",       ("Blaze",        "#FF6030") },
        { "Frost",       ("Frost",        "#87CEEB") },
        { "Obsidian",    ("Obsidian",     "#AAAAAA") },
        { "Mythril",     ("Mythril",      "#00BFFF") },
        { "Adamantium",  ("Adamantium",   "#9370DB") },
        { "Celestial",   ("Celestial",    "#FFD700") },
    };

    private static string DisplayName(string key) =>
        OreDisplay.TryGetValue(key, out var d) ? d.Name : key;

    public ProspectorsLogbookGump(PlayerMobile pm, ClusterFAccountData data, View view = View.Logbook)
        : base(70, 50)
    {
        _pm   = pm;
        _data = data;
        _view = view;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────────
        AddLabel(W / 2 - 80, 12, 1154, "Prospector's Logbook");
        AddLabel(W / 2 - 60, 28, 999,  pm.Name);
        AddImageTiled(10, 48, W - 20, 2, 9304);

        switch (view)
        {
            case View.Logbook:     DrawLogbook();     break;
            case View.FieldReport: DrawFieldReport(); break;
        }
    }

    // ── Logbook view ──────────────────────────────────────────────────────────

    private void DrawLogbook()
    {
        var discoveries = _data.OreDiscoveries;

        if (discoveries.Count == 0)
        {
            AddLabel(18, 62, 999,  "No ore discoveries recorded yet.");
            AddLabel(18, 80, 999,  "Mine colored ore to log your findings.");
            AddLabel(18, 98, 0x22, "Iron is not tracked — only colored and rare ores.");
        }
        else
        {
            // Scrollable body: y=56, height=400 → ends at y=456
            var html = BuildHtml(discoveries);
            AddHtml(10, 56, W - 20, 400, html, false, true);
        }

        // ── Footer ────────────────────────────────────────────────────────────
        AddImageTiled(10, H - 46, W - 20, 2, 9304);

        var unreported   = SurveyArchivistGump.CountUnreportedVeins(_data);
        var totalVeins   = 0;
        var reportedOres = 0;
        foreach (var e in discoveries.Values)
        {
            if (e.State == DiscoveryState.Reported) reportedOres++;
            totalVeins += e.Locations.Count;
        }

        AddLabel(18, H - 32, 999,
            $"Ore types: {discoveries.Count}   Veins: {totalVeins}   Reported: {reportedOres}");

        if (unreported > 0)
        {
            // "Field Report" button — left of X, shows pending count
            AddButton(W - 200, H - 36, 4011, 4012, 10);
            AddLabel(W - 174,  H - 34, 1154, $"Field Report ({unreported})");
        }

        AddButton(W - 50, H - 36, 4023, 4025, 0); // Close (X)
        AddLabel(W - 28, H - 34, 1154, "X");
    }

    private static string BuildHtml(Dictionary<string, OreDiscoveryEntry> discoveries)
    {
        var sb    = new StringBuilder();
        var first = true;

        foreach (var key in OreOrder)
        {
            if (!discoveries.TryGetValue(key, out var entry)) continue;

            if (!OreDisplay.TryGetValue(key, out var disp))
                disp = (key, "#AAAAAA");

            if (!first) sb.Append("<BR>");
            first = false;

            // Ore type header
            var stateTag = entry.State == DiscoveryState.Reported
                ? " <BASEFONT COLOR=#44AA44>[Reported]</BASEFONT>"
                : string.Empty;

            sb.Append(
                $"<BASEFONT COLOR={disp.Color}><B>{disp.Name}</B></BASEFONT>" +
                $"<BASEFONT COLOR=#AAAAAA> — {entry.Locations.Count} vein{(entry.Locations.Count != 1 ? "s" : "")}, " +
                $"{entry.TotalMined:N0} total mined{stateTag}</BASEFONT><BR>");

            // Location rows
            foreach (var loc in entry.Locations)
            {
                var dateStr  = loc.DiscoveredAt.ToString("yyyy-MM-dd");
                var repColor = loc.Reported ? "#44AA44" : "#888888";
                sb.Append(
                    $"<BASEFONT COLOR={repColor}>  • {dateStr} | " +
                    $"{loc.RegionName}, {loc.FacetName} | " +
                    $"({loc.Location.X}, {loc.Location.Y}) | " +
                    $"{loc.AmountMined:N0} ore</BASEFONT><BR>");
            }
        }

        return sb.ToString();
    }

    // ── Field Report view ─────────────────────────────────────────────────────

    private void DrawFieldReport()
    {
        var pending = SurveyArchivistGump.GetUnreportedVeins(_data);

        // Sub-header
        AddLabel(18, 56, 999,
            "Your logbook contains the following unreported vein discoveries:");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        if (pending.Count == 0)
        {
            AddLabel(18, 82, 999, "All vein discoveries have been submitted.");
            AddLabel(18, 100, 999, "Continue mining — every new deposit is reportable.");

            // Footer
            AddImageTiled(10, H - 46, W - 20, 2, 9304);
            AddButton(18,     H - 36, 4014, 4016, 20); // Back
            AddLabel(44,      H - 34, 999, "Back");
            AddButton(W - 50, H - 36, 4023, 4025, 0);
            AddLabel(W - 28,  H - 34, 1154, "X");
            return;
        }

        // Column headers
        AddLabel(18,  78, 1154, "Ore Vein");
        AddLabel(270, 78, 1154, "Standing");
        AddLabel(345, 78, 1154, "Vouchers");
        AddLabel(420, 78, 1154, "Gold");
        AddImageTiled(10, 94, W - 20, 1, 9304);

        var y        = 100;
        var totStand = 0;
        var totVouch = 0;
        var totGold  = 0;

        foreach (var (oreKey, locIdx, veinNum) in pending)
        {
            var (standing, vouchers, gold) = SurveyArchivistGump.RewardTable.TryGetValue(oreKey, out var rw)
                ? rw : (50, 2, 500);
            totStand += standing;
            totVouch += vouchers;
            totGold  += gold;

            if (y <= H - 150)
            {
                var entry = _data.OreDiscoveries[oreKey];
                var label = entry.Locations.Count > 1
                    ? $"{DisplayName(oreKey)} #{veinNum}"
                    : DisplayName(oreKey);

                AddLabel(18,  y, 999,  label);
                AddLabel(270, y, 0x44, $"+{standing}");
                AddLabel(345, y, 0x44, $"+{vouchers}");
                AddLabel(420, y, 0x44, $"{gold:N0}gp");
                y += 20;
            }
        }

        if (y > H - 150)
            AddLabel(18, H - 150, 999, $"... and more (submit to claim all)");

        // Totals
        var totY = Math.Min(y, H - 148) + 4;
        AddImageTiled(10, totY, W - 20, 1, 9304);
        AddLabel(18,  totY + 6, 1154, "Total:");
        AddLabel(270, totY + 6, 0x44, $"+{totStand} Standing");
        AddLabel(345, totY + 6, 0x44, $"+{totVouch} Vouchers");
        AddLabel(420, totY + 6, 0x44, $"{totGold:N0}gp → Bank");

        // Submit button
        AddButton(18,     totY + 28, 4011, 4012, 30);
        AddLabel(44,      totY + 30, 999,
            $"Submit Field Report ({pending.Count} vein{(pending.Count == 1 ? "" : "s")})");

        // Footer
        AddImageTiled(10, H - 46, W - 20, 2, 9304);
        AddButton(18,     H - 36, 4014, 4016, 20); // Back
        AddLabel(44,      H - 34, 999, "Back");
        AddButton(W - 50, H - 36, 4023, 4025, 0);
        AddLabel(W - 28,  H - 34, 1154, "X");
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return; // close

        if (info.ButtonID == 10) // Open Field Report view
        {
            _pm.CloseGump<ProspectorsLogbookGump>();
            _pm.SendGump(new ProspectorsLogbookGump(_pm, _data, View.FieldReport));
            return;
        }

        if (info.ButtonID == 20) // Back to Logbook view
        {
            _pm.CloseGump<ProspectorsLogbookGump>();
            _pm.SendGump(new ProspectorsLogbookGump(_pm, _data, View.Logbook));
            return;
        }

        if (info.ButtonID == 30) // Submit Field Report
        {
            SurveyArchivistGump.SubmitReport(_pm);
            // Reopen logbook so the player sees the updated reported state
            _pm.CloseGump<ProspectorsLogbookGump>();
            _pm.SendGump(new ProspectorsLogbookGump(_pm, _data, View.Logbook));
        }
    }
}
