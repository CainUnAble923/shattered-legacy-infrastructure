using System;
using System.Collections.Generic;
using System.Text;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Forester's Logbook.
///
/// Two views:
///   Logbook         — scrollable HTML list of all tracked timber types.
///                     Vanilla colored woods (Oak → Frostwood) show total
///                     chopped and first-found date. Extended rare timbers
///                     (Ironwood → Starwood) additionally show Discovered /
///                     Reported state. "Discovery Report" button appears if
///                     any extended finds are pending submission.
///
///   DiscoveryReport — lists all extended timbers in Discovered state with
///                     reward previews (standing + Timber Tokens).
///                     "Submit" grants rewards and marks them Reported,
///                     identical to visiting Cedric Rowanwood in person.
/// </summary>
public class ForestersLogbookGump : Gump
{
    public enum View { Logbook, DiscoveryReport }

    private readonly PlayerMobile        _pm;
    private readonly ClusterFAccountData _data;
    private readonly View                _view;

    private const int W = 540;
    private const int H = 510;

    // Display order: vanilla colored woods first, then extended
    private static readonly string[] WoodOrder =
    {
        "OakWood", "AshWood", "YewWood", "Heartwood", "Bloodwood", "Frostwood",
        "Ironwood", "Ghostwood", "Emberbark", "Frostbark",
        "Shadowbark", "Runewood", "Voidwood", "Starwood",
    };

    // Extended wood keys — these have a Discovered/Reported gate
    private static readonly HashSet<string> ExtendedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ironwood", "Ghostwood", "Emberbark", "Frostbark",
        "Shadowbark", "Runewood", "Voidwood", "Starwood",
    };

    private static readonly Dictionary<string, (string Name, string Color)> WoodDisplay = new()
    {
        { "OakWood",    ("Oak",       "#8B7355") },
        { "AshWood",    ("Ash",       "#C8B888") },
        { "YewWood",    ("Yew",       "#8B6914") },
        { "Heartwood",  ("Heartwood", "#C04040") },
        { "Bloodwood",  ("Bloodwood", "#CC2222") },
        { "Frostwood",  ("Frostwood", "#88CCEE") },
        { "Ironwood",   ("Ironwood",  "#888899") },
        { "Ghostwood",  ("Ghostwood", "#C8D8E8") },
        { "Emberbark",  ("Emberbark", "#E06030") },
        { "Frostbark",  ("Frostbark", "#88BBDD") },
        { "Shadowbark", ("Shadowbark","#556677") },
        { "Runewood",   ("Runewood",  "#88AA44") },
        { "Voidwood",   ("Voidwood",  "#8844BB") },
        { "Starwood",   ("Starwood",  "#FFD040") },
    };

    // Standing and Timber Token rewards per extended wood discovery submission
    private static readonly Dictionary<string, (int Standing, int Tokens)> RewardTable = new()
    {
        { "Ironwood",   (250, 10) },
        { "Ghostwood",  (300, 12) },
        { "Emberbark",  (300, 12) },
        { "Frostbark",  (350, 14) },
        { "Shadowbark", (400, 16) },
        { "Runewood",   (450, 18) },
        { "Voidwood",   (500, 20) },
        { "Starwood",   (600, 25) },
    };

    public ForestersLogbookGump(PlayerMobile pm, ClusterFAccountData data, View view = View.Logbook)
        : base(70, 50)
    {
        _pm   = pm;
        _data = data;
        _view = view;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 75, 12, 1154, "Forester's Logbook");
        AddLabel(W / 2 - 60, 28, 999,  pm.Name);
        AddImageTiled(10, 48, W - 20, 2, 9304);

        switch (view)
        {
            case View.Logbook:         DrawLogbook();         break;
            case View.DiscoveryReport: DrawDiscoveryReport(); break;
        }
    }

    // ── Logbook view ──────────────────────────────────────────────────────────

    private void DrawLogbook()
    {
        var discoveries = _data.WoodDiscoveries;

        if (discoveries.Count == 0)
        {
            AddLabel(18, 62, 999,  "No timber discoveries recorded yet.");
            AddLabel(18, 80, 999,  "Chop colored or exotic logs to fill the logbook.");
            AddLabel(18, 98, 0x3B2, "Regular pine and oak logs are not tracked until first found.");
        }
        else
        {
            AddHtml(10, 56, W - 20, 400, BuildHtml(discoveries), false, true);
        }

        // ── Footer ────────────────────────────────────────────────────────────
        AddImageTiled(10, H - 46, W - 20, 2, 9304);

        var pending   = CountPending(_data);
        var total     = discoveries.Count;
        var reported  = 0;
        var totalChop = 0;
        foreach (var e in discoveries.Values)
        {
            if (e.State == DiscoveryState.Reported) reported++;
            totalChop += e.TotalChopped;
        }

        AddLabel(18, H - 32, 999,
            $"Timber types: {total}   Total chopped: {totalChop:N0}   Reported: {reported}");

        if (pending > 0)
        {
            AddButton(W - 210, H - 36, 4011, 4012, 10);
            AddLabel(W - 186,  H - 34, 1154, $"Discovery Report ({pending})");
        }

        AddButton(W - 50, H - 36, 4023, 4025, 0);
        AddLabel(W - 28,  H - 34, 1154, "X");
    }

    private static string BuildHtml(Dictionary<string, WoodDiscoveryEntry> discoveries)
    {
        var sb = new StringBuilder();

        // Vanilla section header
        var anyVanilla = false;
        foreach (var key in WoodOrder)
        {
            if (!ExtendedKeys.Contains(key) && discoveries.ContainsKey(key)) { anyVanilla = true; break; }
        }
        var anyExtended = false;
        foreach (var key in WoodOrder)
        {
            if (ExtendedKeys.Contains(key) && discoveries.ContainsKey(key)) { anyExtended = true; break; }
        }

        if (anyVanilla)
        {
            sb.Append("<BASEFONT COLOR=#AAAAAA><B>── Known Timbers ──</B></BASEFONT><BR>");

            foreach (var key in WoodOrder)
            {
                if (ExtendedKeys.Contains(key)) continue;
                if (!discoveries.TryGetValue(key, out var entry)) continue;

                if (!WoodDisplay.TryGetValue(key, out var disp))
                    disp = (key, "#AAAAAA");

                var dateStr = entry.FirstFound.ToString("yyyy-MM-dd");
                sb.Append(
                    $"<BASEFONT COLOR={disp.Color}><B>{disp.Name}</B></BASEFONT>" +
                    $"<BASEFONT COLOR=#AAAAAA> — first cut {dateStr}, " +
                    $"{entry.TotalChopped:N0} logs chopped</BASEFONT><BR>");
            }
        }

        if (anyExtended)
        {
            if (anyVanilla) sb.Append("<BR>");
            sb.Append("<BASEFONT COLOR=#AAAAAA><B>── Rare Discoveries ──</B></BASEFONT><BR>");

            foreach (var key in WoodOrder)
            {
                if (!ExtendedKeys.Contains(key)) continue;
                if (!discoveries.TryGetValue(key, out var entry)) continue;

                if (!WoodDisplay.TryGetValue(key, out var disp))
                    disp = (key, "#AAAAAA");

                var dateStr  = entry.FirstFound.ToString("yyyy-MM-dd");
                var stateTag = entry.State == DiscoveryState.Reported
                    ? " <BASEFONT COLOR=#44AA44>[Reported]</BASEFONT>"
                    : " <BASEFONT COLOR=#E06030>[Pending Report]</BASEFONT>";

                sb.Append(
                    $"<BASEFONT COLOR={disp.Color}><B>{disp.Name}</B></BASEFONT>" +
                    $"<BASEFONT COLOR=#AAAAAA> — first found {dateStr}, " +
                    $"{entry.TotalChopped:N0} logs chopped{stateTag}</BASEFONT><BR>");
            }
        }

        return sb.ToString();
    }

    // ── Discovery Report view ─────────────────────────────────────────────────

    private void DrawDiscoveryReport()
    {
        AddLabel(18, 56, 999,
            "The following rare timber finds await submission:");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var pendingList = GetPendingList(_data);

        if (pendingList.Count == 0)
        {
            AddLabel(18,  82, 999, "All rare timber discoveries have been submitted.");
            AddLabel(18, 100, 999, "Keep chopping — new exotic finds can be reported here.");

            AddImageTiled(10, H - 46, W - 20, 2, 9304);
            AddButton(18,     H - 36, 4014, 4016, 20);
            AddLabel(44,      H - 34, 999, "Back");
            AddButton(W - 50, H - 36, 4023, 4025, 0);
            AddLabel(W - 28,  H - 34, 1154, "X");
            return;
        }

        AddLabel(18,  78, 1154, "Timber");
        AddLabel(280, 78, 1154, "Standing");
        AddLabel(370, 78, 1154, "Tokens");
        AddImageTiled(10, 94, W - 20, 1, 9304);

        var y        = 100;
        var totStand = 0;
        var totTok   = 0;

        foreach (var key in pendingList)
        {
            var (standing, tokens) = RewardTable.TryGetValue(key, out var rw) ? rw : (250, 10);
            totStand += standing;
            totTok   += tokens;

            if (y <= H - 150)
            {
                var dispName = WoodDisplay.TryGetValue(key, out var d) ? d.Name : key;
                AddLabel(18,  y, 999,  dispName);
                AddLabel(280, y, 0x44, $"+{standing}");
                AddLabel(370, y, 0x44, $"+{tokens}");
                y += 20;
            }
        }

        if (y > H - 150)
            AddLabel(18, H - 150, 999, "... and more (submit to claim all)");

        var totY = Math.Min(y, H - 148) + 4;
        AddImageTiled(10, totY, W - 20, 1, 9304);
        AddLabel(18,  totY + 6, 1154, "Total:");
        AddLabel(280, totY + 6, 0x44, $"+{totStand} Standing");
        AddLabel(370, totY + 6, 0x44, $"+{totTok} Timber Tokens");

        AddButton(18, totY + 28, 4011, 4012, 30);
        AddLabel(44,  totY + 30, 999,
            $"Submit Report ({pendingList.Count} discover{(pendingList.Count == 1 ? "y" : "ies")})");

        AddImageTiled(10, H - 46, W - 20, 2, 9304);
        AddButton(18,     H - 36, 4014, 4016, 20);
        AddLabel(44,      H - 34, 999, "Back");
        AddButton(W - 50, H - 36, 4023, 4025, 0);
        AddLabel(W - 28,  H - 34, 1154, "X");
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    public static int CountPending(ClusterFAccountData data)
    {
        var count = 0;
        foreach (var key in ExtendedKeys)
        {
            if (data.WoodDiscoveries.TryGetValue(key, out var e) && e.State == DiscoveryState.Discovered)
                count++;
        }
        return count;
    }

    private static List<string> GetPendingList(ClusterFAccountData data)
    {
        var list = new List<string>();
        foreach (var key in WoodOrder)
        {
            if (!ExtendedKeys.Contains(key)) continue;
            if (data.WoodDiscoveries.TryGetValue(key, out var e) && e.State == DiscoveryState.Discovered)
                list.Add(key);
        }
        return list;
    }

    /// <summary>
    /// Submits all pending extended timber discoveries for the player,
    /// granting standing and Timber Token rewards. Used by both this gump
    /// and ForestersGuildmasterGump.
    /// </summary>
    public static void SubmitReport(PlayerMobile pm)
    {
        if (pm.Account is not Server.Accounting.IAccount acct) return;

        var data    = ClusterFAccountPersistence.GetOrCreate(acct);
        var pending = GetPendingList(data);

        if (pending.Count == 0)
        {
            pm.SendMessage(0x3B2, "No unreported timber discoveries to submit.");
            return;
        }

        var totStand = 0;
        var totTok   = 0;

        foreach (var key in pending)
        {
            if (!data.WoodDiscoveries.TryGetValue(key, out var entry)) continue;
            entry.State = DiscoveryState.Reported;

            var (standing, tokens) = RewardTable.TryGetValue(key, out var rw) ? rw : (250, 10);
            data.AddReputation("foresters", standing);
            data.AddCurrency("foresters", tokens);
            totStand += standing;
            totTok   += tokens;
        }

        pm.SendMessage(0x44,
            $"You submitted {pending.Count} timber discover{(pending.Count == 1 ? "y" : "ies")} to your logbook. " +
            $"Gained +{totStand} Foresters' Standing and +{totTok} Timber Tokens.");
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 10)
        {
            _pm.CloseGump<ForestersLogbookGump>();
            _pm.SendGump(new ForestersLogbookGump(_pm, _data, View.DiscoveryReport));
            return;
        }

        if (info.ButtonID == 20)
        {
            _pm.CloseGump<ForestersLogbookGump>();
            _pm.SendGump(new ForestersLogbookGump(_pm, _data, View.Logbook));
            return;
        }

        if (info.ButtonID == 30)
        {
            SubmitReport(_pm);
            _pm.CloseGump<ForestersLogbookGump>();
            _pm.SendGump(new ForestersLogbookGump(_pm, _data, View.Logbook));
        }
    }
}
