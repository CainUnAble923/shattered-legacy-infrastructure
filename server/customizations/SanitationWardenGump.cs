using System;
using Server.Accounting;
using Server.Engines.CleanUpBritannia;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// The Custodians guild warden gump.
///
/// Non-members see a join screen. Members see rank, lifetime and current Clean Up Britannia points, the rank ladder,
/// and two buttons: the Clean Up Britannia store and a free trash bag.
///
/// cc-P33 (F-3): the Civic Token shop is retired. The store is the one Clean Up Britannia store the Cleanup Officer
/// also opens (Services/CleanUpBritannia/CleanUpBritanniaRewards.cs), open to members and non-members alike, as
/// OSI's is; it needs a Warden or Officer within 5 tiles, as OSI's confirm does. The trash bag is free and one per
/// character: asking again deletes the old one wherever it is (TrashBag.IssueTo).
/// </summary>
public class SanitationWardenGump : Gump
{
    // -- Layout ----------------------------------------------------------------

    private const int W = 400;

    // -- Button IDs ------------------------------------------------------------

    private const int BtnClose    = 0;
    private const int BtnJoin     = 1;
    private const int BtnStore    = 3;
    private const int BtnTrashBag = 5;

    // A store trader this close lets the gump open the store (ServUO's confirm range, BaseRewardGump.cs:238).
    public const int StoreRange = 5;

    // -- State -----------------------------------------------------------------

    private readonly PlayerMobile _pm;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;

    // -- Construction ----------------------------------------------------------

    public SanitationWardenGump(PlayerMobile pm, GuildDef def, IAccount acct) : base(100, 80)
    {
        _pm   = pm;
        _def  = def;
        _acct = acct;

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        var isMember = ClusterFGuildSystem.IsJoined(pm, "custodians");
        var H        = isMember ? 415 : 380;

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // Shared header
        AddLabel(W / 2 - 55, 14, 1153, "The Custodians");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        if (isMember)
        {
            BuildMemberView(pm);
        }
        else
        {
            BuildJoinView(def);
        }
    }

    // -- Member main view ------------------------------------------------------

    private void BuildMemberView(PlayerMobile pm)
    {
        var lifetime = ClusterFCustodianSystem.GetLifetimePoints(pm);
        var points   = ClusterFCustodianSystem.GetPoints(pm);
        var standing = (int)Math.Floor(lifetime);
        var rank     = ClusterFCustodianSystem.GetCustodianRank(standing);
        var (nextThreshold, nextRankName) = ClusterFCustodianSystem.GetNextRank(standing);

        // -- Status block -----------------------------------------------------

        AddLabel(18, 46, 999,  "Rank:");
        AddLabel(70, 46, 1153, rank);

        AddLabel(18, 66, 999,  "Lifetime points:");
        AddLabel(130, 66, 68,  ClusterFCustodianSystem.FormatPoints(lifetime));

        if (nextThreshold > 0)
        {
            AddLabel(230, 66, 1154, $"->  {nextRankName}");
            AddLabel(230, 82, 999,  $"   ({nextThreshold - lifetime:#,0.##} to go)");
        }
        else
        {
            AddLabel(230, 66, 1154, "MAX RANK");
        }

        AddLabel(18, 86, 999,  "Clean Up points:");
        AddLabel(130, 86, 68,  ClusterFCustodianSystem.FormatPoints(points));

        AddImageTiled(10, 108, W - 20, 2, 9304);

        // -- Buttons -----------------------------------------------------------

        AddButton(18, 118, 4005, 4007, BtnStore, GumpButtonType.Reply, 0);
        AddLabel(54, 120, 1154, "Clean Up Britannia store");

        AddButton(220, 118, 4005, 4007, BtnTrashBag, GumpButtonType.Reply, 0);
        AddLabel(256, 120, 1154, "Trash bag (free)");

        AddImageTiled(10, 146, W - 20, 2, 9304);

        // -- How-to tips -------------------------------------------------------

        AddLabel(18, 156, 999, "How to earn Clean Up Britannia points:");

        AddHtml(18, 176, W - 36, 120,
            "<BASEFONT COLOR=#AAAAAA>" +
            "Use <B>[cleanup</B> to target individual items on the ground.<BR>" +
            "Use <B>[cleanupall</B> to sweep a 10-tile area (60s cooldown).<BR>" +
            "Place items in your Trash Bag and use <B>Dump Now</B> to cash in, or throw them in any trash barrel.<BR><BR>" +
            "Items earn what Clean Up Britannia pays for them. Corpses earn what is inside.<BR>" +
            "Every 5 items of value cleaned in a batch earns 1 civic waste bundle - " +
            "turn these in for Custodian work order rewards." +
            "</BASEFONT>",
            false, false);

        AddImageTiled(10, 302, W - 20, 2, 9304);

        // -- Rank ladder -------------------------------------------------------

        var ranks = ClusterFCustodianSystem.Ranks;
        var ladder = string.Join(" -> ", Array.ConvertAll(ranks, r => r.Name));
        var thresholds = string.Join(" / ", Array.ConvertAll(ranks[1..], r => r.Threshold.ToString("N0")));

        AddHtml(18, 312, W - 36, 88,
            "<BASEFONT COLOR=#888888>" +
            $"Ranks: {ladder}<BR>" +
            $"({thresholds} lifetime Clean Up points; spending points never lowers your rank)" +
            "</BASEFONT>",
            false, false);
    }

    // -- Join view -------------------------------------------------------------

    private void BuildJoinView(GuildDef def)
    {
        AddHtml(18, 46, W - 36, 60,
            $"<BASEFONT COLOR=#CCCCCC>\"{def.Pitch}\"</BASEFONT>",
            false, false);

        AddImageTiled(10, 110, W - 20, 2, 9304);

        AddLabel(18, 120, 68, "Open to all citizens - no requirements.");
        AddHtml(18, 142, W - 36, 40,
            $"<BASEFONT COLOR=#AAAAAA>{def.TaskDescription}</BASEFONT>",
            false, false);

        AddImageTiled(10, 188, W - 20, 2, 9304);

        AddButton(18, 200, 4023, 4025, BtnJoin, GumpButtonType.Reply, 0);
        AddLabel(54, 202, 999, "Join The Custodians");

        AddButton(220, 200, 4005, 4007, BtnStore, GumpButtonType.Reply, 0);
        AddLabel(256, 202, 1154, "Clean Up store");

        AddImageTiled(10, 230, W - 20, 2, 9304);
        AddHtml(18, 240, W - 36, 120,
            "<BASEFONT COLOR=#888888>Members clean up litter and corpses around Britannia with [cleanup and " +
            "[cleanupall, and carry a free trash bag. Every cleanup earns Clean Up Britannia points, the same points " +
            "any trash barrel pays, and lifetime points set a member's rank. Anyone may spend points at the store.</BASEFONT>",
            false, false);
    }

    // -- Response --------------------------------------------------------------

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;

        switch (info.ButtonID)
        {
            case BtnJoin:
                HandleJoin(pm);
                break;

            case BtnStore:
                OpenStore(pm);
                break;

            case BtnTrashBag:
                HandleTrashBag(pm);
                break;
        }
    }

    /// <summary>Opens the Clean Up store if a Sanitation Warden or Cleanup Officer stands within StoreRange.</summary>
    public static bool OpenStore(PlayerMobile pm)
    {
        Mobile trader = null;

        foreach (var m in pm.GetMobilesInRange(StoreRange))
        {
            if (m is SanitationWarden or TheCleanupOfficer)
            {
                trader = m;
                break;
            }
        }

        if (trader == null)
        {
            pm.SendMessage(0x22, "Visit a Sanitation Warden or a Cleanup Officer to use the Clean Up Britannia store.");
            return false;
        }

        pm.SendGump(new CleanUpBritanniaRewardGump(trader, pm));
        return true;
    }

    private void HandleTrashBag(PlayerMobile pm)
    {
        if (!ClusterFGuildSystem.IsJoined(pm, "custodians"))
        {
            return;
        }

        var replaced = TrashBag.OwnedBy(pm).Count;

        if (TrashBag.IssueTo(pm) == null)
        {
            pm.SendMessage(0x22, "You have no backpack to receive a trash bag.");
            return;
        }

        pm.SendMessage(0x44, replaced > 0
            ? "You receive a new trash bag. Your old one has been collected."
            : "You receive a trash bag.");
        pm.SendGump(new SanitationWardenGump(pm, _def, _acct));
    }

    // -- Join handler ----------------------------------------------------------

    private void HandleJoin(PlayerMobile pm)
    {
        if (!ClusterFGuildSystem.CanJoin(pm, _def, out var reason))
        {
            pm.SendMessage(0x22, reason);
            return;
        }

        ClusterFGuildSystem.Join(pm, _def);
        SanitationWarden.OnCustodiansJoined(pm);

        pm.SendMessage(0x44,
            $"Welcome to The Custodians, {pm.Name}. " +
            "Use [cleanup or [cleanupall to earn Clean Up Britannia points.");
        pm.PlaySound(0x57);

        pm.SendGump(new SanitationWardenGump(pm, _def, _acct));
    }
}
