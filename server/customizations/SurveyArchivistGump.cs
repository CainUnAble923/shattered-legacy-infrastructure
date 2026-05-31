using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Miners' Compact Survey Archivist (Velara Thorne).
///
/// Views:
///   MainMenu    — introduction and navigation
///   Discoveries — lists unreported ore discoveries with reward previews;
///                 "Report All" submits them, marks as Reported, grants rewards
/// </summary>
public class SurveyArchivistGump : Gump
{
    public enum View { MainMenu, Discoveries }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    // ── Reward table: Standing, Vouchers, and Gold per vein report ───────────────
    // Awarded per vein location (not just per ore type — every new vein is reportable).
    // Scales with ore rarity/tier. Gold is deposited directly to the player's bank box.
    // Internal so the Prospector's Logbook remote-submit feature can preview rewards.
    internal static readonly Dictionary<string, (int Standing, int Vouchers, int Gold)> RewardTable = new()
    {
        // Vanilla ore tier (DullCopper → Valorite)
        { "DullCopper",  (  50,  2,   500) },
        { "ShadowIron",  (  75,  3,   750) },
        { "Copper",      (  75,  3,   750) },
        { "Bronze",      ( 100,  4,  1000) },
        { "Gold",        ( 100,  4,  1000) },
        { "Agapite",     ( 150,  6,  1500) },
        { "Verite",      ( 200,  8,  2000) },
        { "Valorite",    ( 300, 12,  3000) },
        // Extended ore tier (Platinum → Celestial)
        { "Platinum",    ( 200,  8,  2000) },
        { "Toxic",       ( 200,  8,  2000) },
        { "Blaze",       ( 250, 10,  2500) },
        { "Frost",       ( 250, 10,  2500) },
        { "Obsidian",    ( 300, 12,  3000) },
        { "Mythril",     ( 350, 14,  3500) },
        { "Adamantium",  ( 400, 16,  4000) },
        { "Celestial",   ( 500, 20,  5000) },
    };

    // Ore display names
    private static string DisplayName(string key) => key switch
    {
        "DullCopper" => "Dull Copper",
        "ShadowIron" => "Shadow Iron",
        _            => key,
    };

    public SurveyArchivistGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - 80, 12, 1154, "Survey Archivist");
        AddLabel(W / 2 - 60, 28, 999,  "Velara Thorne");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        switch (view)
        {
            case View.MainMenu:    DrawMainMenu(data);    break;
            case View.Discoveries: DrawDiscoveries(data); break;
        }

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(10, H - 42, W - 20, 2, 9304);
        AddButton(W - 50, H - 32, 4023, 4025, 0);
        AddLabel(W - 28, H - 30, 1154, "X");
    }

    // ── Main menu ─────────────────────────────────────────────────────────────

    private void DrawMainMenu(ClusterFAccountData data)
    {
        var unreported = CountUnreportedVeins(data);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Miners' Compact maintains geological records " +
            "of ore deposits across all facets. Every vein you discover is valuable — " +
            "the more locations we chart, the better we serve the guild." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Bring your Prospector's Logbook vein records to me " +
            "and I will log them in the Compact archives. In return, the guild rewards " +
            "each vein report with Compact Standing, Mining Vouchers, and gold deposited " +
            "directly to your bank." +
            "</BASEFONT><BR><BR>";

        if (unreported > 0)
        {
            html += $"<BASEFONT COLOR=#FFD700>You have {unreported} unreported " +
                    $"vein{(unreported == 1 ? "" : "s")} ready to submit." +
                    "</BASEFONT>";
        }
        else if (data.OreDiscoveries.Count > 0)
        {
            html += "<BASEFONT COLOR=#44AA44>All known veins have been reported. " +
                    "Keep mining — every new deposit will appear in your Prospector's Logbook." +
                    "</BASEFONT>";
        }
        else
        {
            html += "<BASEFONT COLOR=#888888>You have no ore discoveries recorded yet. " +
                    "Mine colored ores and the findings will appear in your Prospector's Logbook." +
                    "</BASEFONT>";
        }

        AddHtml(16, 56, W - 32, H - 130, html, false, true);

        // "Review Veins" button — always shown, disabled label if nothing to report
        if (unreported > 0)
        {
            AddButton(18, H - 68, 4011, 4012, 10);
            AddLabel(44, H - 66, 999, $"Review Vein Reports ({unreported} pending)");
        }
        else
        {
            AddLabel(18, H - 66, 999, "No pending veins to report.");
        }
    }

    // ── Discoveries view ──────────────────────────────────────────────────────

    private void DrawDiscoveries(ClusterFAccountData data)
    {
        AddButton(18, H - 32, 4014, 4016, 20); // Back
        AddLabel(44, H - 30, 999, "Back");

        var pending = GetUnreportedVeins(data);

        if (pending.Count == 0)
        {
            AddLabel(18, 60, 999, "All vein discoveries have been reported.");
            AddLabel(18, 78, 999, "Continue mining — every new deposit is reportable.");
            return;
        }

        // Column headers
        AddLabel(18,  58, 1154, "Ore Vein");
        AddLabel(230, 58, 1154, "Standing");
        AddLabel(300, 58, 1154, "Vouchers");
        AddLabel(370, 58, 1154, "Gold");
        AddImageTiled(10, 74, W - 20, 1, 9304);

        var y        = 80;
        var totStand = 0;
        var totVouch = 0;
        var totGold  = 0;
        var shown    = 0;

        foreach (var (oreKey, locIdx, veinNum) in pending)
        {
            var (standing, vouchers, gold) = RewardTable.TryGetValue(oreKey, out var r) ? r : (50, 2, 500);
            totStand += standing;
            totVouch += vouchers;
            totGold  += gold;

            if (y <= H - 110)
            {
                var entry      = data.OreDiscoveries[oreKey];
                var label      = entry.Locations.Count > 1
                    ? $"{DisplayName(oreKey)} #{veinNum}"
                    : DisplayName(oreKey);

                AddLabel(18,  y, 999,  label);
                AddLabel(230, y, 0x44, $"+{standing}");
                AddLabel(300, y, 0x44, $"+{vouchers}");
                AddLabel(370, y, 0x44, $"{gold:N0}gp");
                y += 20;
                shown++;
            }
        }

        if (shown < pending.Count)
            AddLabel(18, y, 999, $"... and {pending.Count - shown} more");

        // Totals line
        var totY = Math.Min(y, H - 110) + (shown < pending.Count ? 20 : 4);
        AddImageTiled(10, totY, W - 20, 1, 9304);
        AddLabel(18,  totY + 6,  1154, "Total:");
        AddLabel(230, totY + 6,  0x44, $"+{totStand} Standing");
        AddLabel(300, totY + 6,  0x44, $"+{totVouch} Vouchers");
        AddLabel(370, totY + 6,  0x44, $"{totGold:N0}gp → Bank");

        // Report All button
        AddButton(18,    totY + 28, 4011, 4012, 30);
        AddLabel(44,     totY + 30, 999,  $"Report All ({pending.Count} vein{(pending.Count == 1 ? "" : "s")})");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Canonical ore display order
    private static readonly string[] _oreOrder =
    {
        "DullCopper", "ShadowIron", "Copper", "Bronze", "Gold",
        "Agapite", "Verite", "Valorite",
        "Platinum", "Toxic", "Blaze", "Frost",
        "Obsidian", "Mythril", "Adamantium", "Celestial",
    };

    /// <summary>Returns total count of unreported vein locations across all ore entries.</summary>
    internal static int CountUnreportedVeins(ClusterFAccountData data)
    {
        var count = 0;
        foreach (var entry in data.OreDiscoveries.Values)
            foreach (var loc in entry.Locations)
                if (!loc.Reported)
                    count++;
        return count;
    }

    /// <summary>
    /// Returns all unreported vein locations in ore-tier order.
    /// Each element is (oreKey, locationIndex, 1-based vein number for that ore type).
    /// Internal so the Prospector's Logbook can use the same list for remote submission.
    /// </summary>
    internal static List<(string OreKey, int LocIdx, int VeinNum)> GetUnreportedVeins(ClusterFAccountData data)
    {
        var list = new List<(string, int, int)>();

        // Walk in canonical tier order
        foreach (var key in _oreOrder)
        {
            if (!data.OreDiscoveries.TryGetValue(key, out var entry)) continue;
            for (var i = 0; i < entry.Locations.Count; i++)
                if (!entry.Locations[i].Reported)
                    list.Add((key, i, i + 1));
        }

        // Catch any non-standard keys not in the canonical list
        foreach (var kvp in data.OreDiscoveries)
        {
            var known = false;
            foreach (var k in _oreOrder) if (k == kvp.Key) { known = true; break; }
            if (known) continue;

            for (var i = 0; i < kvp.Value.Locations.Count; i++)
                if (!kvp.Value.Locations[i].Reported)
                    list.Add((kvp.Key, i, i + 1));
        }

        return list;
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return; // close

        if (info.ButtonID == 10) // Review Vein Reports
        {
            _pm.SendGump(new SurveyArchivistGump(_pm, View.Discoveries));
            return;
        }

        if (info.ButtonID == 20) // Back
        {
            _pm.SendGump(new SurveyArchivistGump(_pm, View.MainMenu));
            return;
        }

        if (info.ButtonID == 30) // Report All
        {
            HandleReportAll();
            _pm.SendGump(new SurveyArchivistGump(_pm, View.MainMenu));
        }
    }

    private void HandleReportAll() => SubmitReport(_pm);

    /// <summary>
    /// Submits all unreported vein discoveries for a player, grants rewards, and sends
    /// a confirmation message. Called by both the Survey Archivist gump (in-person) and
    /// the Prospector's Logbook gump (remote field report).
    /// </summary>
    internal static void SubmitReport(PlayerMobile pm)
    {
        var acct = pm.Account as IAccount;
        if (acct == null) return;

        var data    = ClusterFAccountPersistence.GetOrCreate(acct);
        var pending = GetUnreportedVeins(data);

        if (pending.Count == 0)
        {
            pm.SendMessage(0x22, "You have no unreported vein discoveries.");
            return;
        }

        var totalStanding = 0;
        var totalVouchers = 0;
        var totalGold     = 0;

        foreach (var (oreKey, locIdx, _) in pending)
        {
            if (!data.OreDiscoveries.TryGetValue(oreKey, out var entry)) continue;
            if (locIdx >= entry.Locations.Count) continue;

            // Mark this individual vein as reported
            entry.Locations[locIdx].Reported = true;

            // Promote entry state to Reported on the first turnin — this is the
            // ore-availability gate for extended ores (Platinum → Celestial).
            if (entry.State == DiscoveryState.Discovered)
                entry.State = DiscoveryState.Reported;

            var (standing, vouchers, gold) = RewardTable.TryGetValue(oreKey, out var r) ? r : (50, 2, 500);
            totalStanding += standing;
            totalVouchers += vouchers;
            totalGold     += gold;
        }

        data.AddReputation("mining", totalStanding);
        data.AddCurrency("mining",   totalVouchers);

        // Deposit gold directly to the player's bank box
        if (totalGold > 0)
            Banker.Deposit(pm, totalGold);

        pm.PlaySound(0x57);
        pm.SendMessage(0x44,
            $"Survey report submitted: {pending.Count} vein{(pending.Count == 1 ? "" : "s")} recorded. " +
            $"Rewarded: +{totalStanding} Compact Standing, +{totalVouchers} Mining Vouchers, " +
            $"{totalGold:N0}gp deposited to your bank.");
    }
}
