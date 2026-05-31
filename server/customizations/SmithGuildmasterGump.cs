using System;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Society of Smiths guildmaster gump.
///
/// Opened when a player talks to a BlacksmithGuildmaster.
///
/// Non-members see a join screen (Society pitch + task requirement).
/// Members see their Smith status — rank, standing, Smithing Seals — and
/// can open Work Orders or read about Smithing Seals.
/// </summary>
public class SmithGuildmasterGump : Gump
{
    // ── Layout constants ──────────────────────────────────────────────────────

    private const int W = 380;
    private const int H = 630;  // member view includes rank progression table + mule exchange

    private const int BtnBulkOrders   = 1;
    private const int BtnWorkOrders   = 2;  // Guild Contracts
    private const int BtnSealsInfo    = 3;
    private const int BtnHammer       = 4;
    private const int BtnCommissions  = 5;
    private const int BtnCatalog      = 6;
    private const int BtnMuleExchange = 20;
    private const int BtnJoin         = 10;

    // Smith rank thresholds — mirrors Phase 4 design doc
    private static readonly (int Standing, string Title)[] Ranks =
    {
        (100_000, "Legendary"),
        ( 50_000, "Grandmaster"),
        ( 15_000, "Master"),
        (  5_000, "Journeyman"),
        (  1_000, "Apprentice"),
        (      0, "Initiate"),
    };

    private readonly PlayerMobile          _pm;
    private readonly GuildDef              _def;
    private readonly IAccount              _acct;
    private readonly BlacksmithGuildmaster? _npc;

    public SmithGuildmasterGump(PlayerMobile pm, GuildDef def, IAccount acct,
        BlacksmithGuildmaster? npc = null)
        : base(100, 80)
    {
        _pm   = pm;
        _def  = def;
        _acct = acct;
        _npc  = npc;

        var isMember = ClusterFGuildSystem.IsJoined(acct, "smithing");

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // ── Header ─────────────────────────────────────────────────────────────
        AddLabel(W / 2 - 90, 14, 1153, "Society of Smiths");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        if (isMember)
            BuildMemberView(pm, acct);
        else
            BuildJoinView(pm, def, acct);
    }

    // ── Member view ───────────────────────────────────────────────────────────

    private void BuildMemberView(PlayerMobile pm, IAccount acct)
    {
        var data     = ClusterFAccountPersistence.GetOrCreate(acct);
        var standing = data.GetReputation("smithing");
        var seals    = data.GetCurrency("smithing");
        var rank     = GetRank(standing);

        // ── Standing / rank block ──────────────────────────────────────────────
        AddLabel(18, 46, 999,  "Rank:");
        AddLabel(70, 46, 1153, rank);

        AddLabel(18, 66, 999,  "Standing:");
        AddLabel(90, 66, 68,   $"{standing:N0}");

        AddLabel(18, 86, 999,  "Smithing Seals:");
        AddLabel(120, 86, 68,  $"{seals:N0}");

        // ── Rank progression table ─────────────────────────────────────────────
        AddImageTiled(10, 106, W - 20, 2, 9304);
        AddLabel(18, 114, 999, "Rank Progression:");

        // Determine current rank index (0 = Legendary, 5 = Initiate)
        var currentRankIdx = Ranks.Length - 1;
        for (var ri = 0; ri < Ranks.Length; ri++)
        {
            if (standing >= Ranks[ri].Standing)
            { currentRankIdx = ri; break; }
        }

        // Ranks[] is ordered highest → lowest; display top-to-bottom same way
        var tblY = 132;
        for (var ri = 0; ri < Ranks.Length; ri++)
        {
            var (thresh, title) = Ranks[ri];

            int    nameHue, dataHue;
            string dataLabel;

            if (ri < currentRankIdx)
            {
                // higher rank — not yet achieved
                nameHue   = 999;
                dataHue   = 999;
                dataLabel = $"{thresh:N0}";
            }
            else if (ri == currentRankIdx)
            {
                // current rank — highlight in gold
                nameHue   = 68;
                dataHue   = 68;
                dataLabel = thresh == 0 ? "current" : $"{standing:N0} / {thresh:N0}";
            }
            else
            {
                // lower rank — already surpassed, dim
                nameHue   = 0x3DE;
                dataHue   = 0x3DE;
                dataLabel = thresh == 0 ? "—" : $"{thresh:N0}";
            }

            AddLabel(30,      tblY, nameHue, title);
            AddLabel(W - 155, tblY, dataHue, dataLabel);
            tblY += 16;
        }

        // ── Progress to next rank ──────────────────────────────────────────────
        tblY += 4;
        if (currentRankIdx == 0)
        {
            AddLabel(18, tblY, 1153, "Maximum rank achieved!");
        }
        else
        {
            var (nextThresh, nextTitle) = Ranks[currentRankIdx - 1];
            var needed = nextThresh - standing;
            AddLabel(18, tblY, 999, $"Next: {nextTitle}  —  {needed:N0} standing needed");
        }
        tblY += 20;

        // ── Buttons ────────────────────────────────────────────────────────────
        AddImageTiled(10, tblY + 4, W - 20, 2, 9304);
        var btnY = tblY + 18;

        // BOD button — show active count
        var (smallBods, largeBods) = BlacksmithGuildmaster.CountBODs(pm);
        var bodLabel = (smallBods + largeBods) > 0
            ? $"Bulk Orders  ({smallBods} small, {largeBods} large active)"
            : "Bulk Orders  (request a new order)";
        AddButton(18, btnY, 4005, 4007, BtnBulkOrders, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 1154, bodLabel);
        btnY += 32;

        AddButton(18, btnY, 4005, 4007, BtnWorkOrders, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999, "Guild Contracts");
        btnY += 32;

        AddButton(18, btnY, 4005, 4007, BtnCommissions, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999, "Commissions");
        btnY += 32;

        AddButton(18, btnY, 4005, 4007, BtnSealsInfo, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999, "What are Smithing Seals?");
        btnY += 32;

        // Hammer of Hephaestus — show tier/state in label
        var hammerLabel = GetHammerStatusLabel(pm);
        AddButton(18, btnY, 4005, 4007, BtnHammer, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999, hammerLabel);
        btnY += 32;

        AddButton(18, btnY, 4005, 4007, BtnCatalog, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999, "Seal Catalog");
        btnY += 32;

        AddImageTiled(10, btnY + 4, W - 20, 2, 9304);
        btnY += 18;

        AddButton(18, btnY, 4005, 4007, BtnMuleExchange, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 1154, "Cross-Guild Exchange (Pack Mule)");
        btnY += 32;

        AddImageTiled(10, btnY + 4, W - 20, 2, 9304);

        // Footer flavour
        AddHtml(18, btnY + 16, W - 36, 70,
            "<BASEFONT COLOR=#AAAAAA>The Society of Smiths honours those who turn raw ore into the tools " +
            "and arms that keep the realm standing. Earn Smithing Seals through bulk orders and work orders " +
            "— they may be spent on guild rewards as your rank grows.</BASEFONT>",
            false, false);
    }

    // ── Join view ─────────────────────────────────────────────────────────────

    private void BuildJoinView(PlayerMobile pm, GuildDef def, IAccount acct)
    {
        // Guildmaster pitch
        AddHtml(18, 46, W - 36, 60,
            $"<BASEFONT COLOR=#CCCCCC>\"{def.Pitch}\"</BASEFONT>",
            false, false);

        AddImageTiled(10, 110, W - 20, 2, 9304);

        // Task requirement
        AddLabel(18, 120, 999, "To join the Society of Smiths:");
        AddHtml(18, 140, W - 36, 40,
            $"<BASEFONT COLOR=#AAAAAA>{def.TaskDescription}</BASEFONT>",
            false, false);

        AddImageTiled(10, 188, W - 20, 2, 9304);

        // Eligibility check
        var canJoin = ClusterFGuildSystem.CanJoin(pm, def, out var reason);

        if (canJoin)
        {
            AddLabel(18, 200, 68, "You meet the requirements.");
            AddButton(18, 225, 4023, 4025, BtnJoin, GumpButtonType.Reply, 0);
            AddLabel(54, 227, 999, "Join the Society of Smiths");
        }
        else
        {
            AddLabel(18, 200, 33, "Not yet eligible:");
            AddLabel(18, 220, 999, reason);
        }
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;

        switch (info.ButtonID)
        {
            case BtnBulkOrders:
                HandleBulkOrder(pm);
                break;

            case BtnWorkOrders:
                pm.SendGump(new GuildContractLedgerGump(pm, "smithing"));
                break;

            case BtnCommissions:
                pm.SendGump(new SmithCommissionGump(pm, _npc));
                break;

            case BtnSealsInfo:
                pm.SendGump(new SmithSealsInfoGump());
                break;

            case BtnHammer:
                pm.SendGump(new HammerRestoreGump(pm));
                break;

            case BtnCatalog:
                pm.SendGump(new SmithSealCatalogGump(pm));
                break;

            case BtnMuleExchange:
                pm.SendGump(new CrossGuildExchangeGump(pm));
                break;

            case BtnJoin:
                HandleJoin(pm);
                break;
        }
    }

    private void HandleBulkOrder(PlayerMobile pm)
    {
        if (_npc == null || _npc.Deleted)
        {
            pm.SendMessage(0x22, "Please speak with the Guildmaster directly.");
            return;
        }

        var bod = _npc.CreateBulkOrder(pm, true);

        if (bod is LargeSmithBOD largeBod)
            pm.SendGump(new LargeBODAcceptGump(largeBod));
        else if (bod is SmallSmithBOD smallBod)
            pm.SendGump(new SmallBODAcceptGump(smallBod));
        // null means CreateBulkOrder already sent an explanatory message
    }

    private void HandleJoin(PlayerMobile pm)
    {
        if (!ClusterFGuildSystem.CanJoin(pm, _def, out var reason))
        {
            pm.SendMessage(0x22, reason);
            return;
        }

        // Consume item tribute if required
        if (_def.TaskType is GuildTaskType.ItemOnly or GuildTaskType.SkillOrItem
            && pm.Backpack?.GetAmount(_def.TaskItemType!) >= _def.TaskItemCount)
        {
            pm.Backpack!.ConsumeTotal(_def.TaskItemType!, _def.TaskItemCount);
        }

        ClusterFGuildSystem.Join(pm, _def);
        pm.SendMessage(0x44, $"Welcome to the Society of Smiths, {pm.Name}. The forge awaits.");
        pm.PlaySound(0x57);

        // Reopen as member view
        pm.SendGump(new SmithGuildmasterGump(pm, _def, _acct, _npc));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetRank(int standing)
    {
        foreach (var (threshold, title) in Ranks)
            if (standing >= threshold)
                return title;
        return "Initiate";
    }

    /// <summary>Returns a contextual label for the Hammer Upgrades button.</summary>
    private static string GetHammerStatusLabel(PlayerMobile pm)
    {
        if (pm.Backpack == null) return "Hammer of Hephaestus Upgrades";

        // Check T2 first
        foreach (var item in pm.Backpack.Items)
        {
            if (item is ReinforcedHammerOfHephaestus t2)
                return t2.Exhausted
                    ? "Hammer of Hephaestus Upgrades (T2 — Exhausted)"
                    : "Hammer of Hephaestus Upgrades (T2)";
        }
        // Then T1
        foreach (var item in pm.Backpack.Items)
        {
            if (item is HammerOfHephaestus t1)
                return t1.Exhausted
                    ? "Hammer of Hephaestus Upgrades (T1 — Exhausted)"
                    : "Hammer of Hephaestus Upgrades (T1)";
        }
        return "Hammer of Hephaestus Upgrades";
    }
}

// ── Smithing Seals info gump ──────────────────────────────────────────────────

public class SmithSealsInfoGump : Gump
{
    public SmithSealsInfoGump() : base(120, 100)
    {
        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, 360, 260, 9270);
        AddAlphaRegion(8, 8, 344, 244);

        AddLabel(360 / 2 - 55, 14, 1153, "Smithing Seals");
        AddImageTiled(10, 34, 340, 2, 9304);

        AddHtml(18, 44, 324, 196,
            "<BASEFONT COLOR=#CCCCCC>" +
            "Smithing Seals are the Society of Smiths guild currency.<BR><BR>" +
            "You earn Seals by:<BR>" +
            "  • Completing Bulk Orders (request from the Guildmaster)<BR>" +
            "  • Completing Smith work orders<BR><BR>" +
            "Higher-tier BODs award more Seals. Coloured-metal orders " +
            "are worth significantly more than plain iron. Exceptional " +
            "requirements increase both Seals and skill check rewards.<BR><BR>" +
            "Seals may be spent on guild rewards as your rank grows — " +
            "including the Hammer of Hephaestus restoration, field forge " +
            "components, and more." +
            "</BASEFONT>",
            false, false);
    }

    public override void OnResponse(NetState sender, in RelayInfo info) { }
}
