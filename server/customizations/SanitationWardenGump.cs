using System;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// The Custodians guild warden gump.
///
/// Non-members see a join screen.
/// Members see rank, standing, token balance, rank-progress hint, and a link
/// to the token shop.  The shop view lets members spend Civic Tokens on
/// useful supplies.
/// </summary>
public class SanitationWardenGump : Gump
{
    // ── Layout ────────────────────────────────────────────────────────────────

    private const int W = 400;

    // ── Button IDs ────────────────────────────────────────────────────────────

    private const int BtnClose       = 0;
    private const int BtnJoin        = 1;
    private const int BtnShop        = 3;
    private const int BtnBack        = 4;
    private const int BtnBuyBandages = 10;
    private const int BtnBuyRefresh  = 11;
    private const int BtnBuyHealPots = 12;
    private const int BtnBuyTrashBag = 13;

    // ── Shop prices ───────────────────────────────────────────────────────────

    private const int CostBandages = 15;   // 100 Bandages
    private const int CostRefresh  = 30;   // 5 Refresh Potions
    private const int CostHealPots = 60;   // 3 Greater Heal Potions
    private const int CostTrashBag = 150;  // 1 replacement TrashBag

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly PlayerMobile _pm;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Opens the main member/join view.</summary>
    public SanitationWardenGump(PlayerMobile pm, GuildDef def, IAccount acct)
        : this(pm, def, acct, shopView: false) { }

    /// <param name="shopView">Pass <c>true</c> to open directly on the shop page.</param>
    private SanitationWardenGump(PlayerMobile pm, GuildDef def, IAccount acct, bool shopView)
        : base(100, 80)
    {
        _pm   = pm;
        _def  = def;
        _acct = acct;

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        var isMember = ClusterFGuildSystem.IsJoined(acct, "custodians");
        var H        = shopView ? 290 : (isMember ? 415 : 340);

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // Shared header
        AddLabel(W / 2 - 55, 14, 1153, "The Custodians");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        if (isMember)
        {
            if (shopView)
                BuildShopView(pm, acct);
            else
                BuildMemberView(pm, acct);
        }
        else
        {
            BuildJoinView(pm, def, acct);
        }
    }

    // ── Member main view ──────────────────────────────────────────────────────

    private void BuildMemberView(PlayerMobile pm, IAccount acct)
    {
        var data     = ClusterFAccountPersistence.GetOrCreate(acct);
        var standing = data.GetReputation("custodians");
        var tokens   = data.GetCurrency("custodians");
        var rank     = ClusterFCustodianSystem.GetCustodianRank(standing);
        var (nextThreshold, nextRankName) = GetNextRankInfo(standing);

        // ── Status block ─────────────────────────────────────────────────────

        AddLabel(18, 46, 999,  "Rank:");
        AddLabel(70, 46, 1153, rank);

        AddLabel(18, 66, 999,  "Standing:");
        AddLabel(90, 66, 68,   $"{standing:N0}");

        if (nextThreshold > 0)
        {
            var toNext = nextThreshold - standing;
            AddLabel(200, 66, 1154, $"→  {nextRankName}");
            AddLabel(200, 82, 999,  $"   ({toNext:N0} to go)");
        }
        else
        {
            AddLabel(200, 66, 1154, "MAX RANK");
        }

        AddLabel(18, 86, 999,  "Civic Tokens:");
        AddLabel(110, 86, 68,  $"{tokens:N0}");

        AddImageTiled(10, 108, W - 20, 2, 9304);

        // ── Token shop button ─────────────────────────────────────────────────

        AddButton(18, 118, 4005, 4007, BtnShop, GumpButtonType.Reply, 0);
        AddLabel(54, 120, 1154, "Visit Token Shop");

        AddImageTiled(10, 146, W - 20, 2, 9304);

        // ── How-to tips ───────────────────────────────────────────────────────

        AddLabel(18, 156, 999, "How to earn Civic Tokens:");

        AddHtml(18, 176, W - 36, 120,
            "<BASEFONT COLOR=#AAAAAA>" +
            "Use <B>[cleanup</B> to target individual items on the ground.<BR>" +
            "Use <B>[cleanupall</B> to sweep a 10-tile area (60s cooldown).<BR>" +
            "Place items in your Trash Bag and use <B>Dump Now</B> to cash in.<BR><BR>" +
            "Higher-value materials yield more tokens. " +
            "Corpses yield tokens based on their contents.<BR>" +
            "Every 5 items cleaned in a batch earns 1 civic waste bundle — " +
            "turn these in for Custodian work order rewards." +
            "</BASEFONT>",
            false, false);

        AddImageTiled(10, 302, W - 20, 2, 9304);

        // ── Rank ladder ───────────────────────────────────────────────────────

        AddHtml(18, 312, W - 36, 88,
            "<BASEFONT COLOR=#888888>" +
            "Ranks: Volunteer → Junior Custodian → Custodian → " +
            "Senior Custodian → Chief Custodian<BR>" +
            "(100 / 500 / 2,000 / 5,000 standing)" +
            "</BASEFONT>",
            false, false);
    }

    // ── Token shop view ───────────────────────────────────────────────────────

    private void BuildShopView(PlayerMobile pm, IAccount acct)
    {
        var data   = ClusterFAccountPersistence.GetOrCreate(acct);
        var tokens = data.GetCurrency("custodians");

        AddLabel(W / 2 - 38, 44, 1154, "Token Shop");
        AddLabel(18, 64, 999, "Civic Tokens:");
        AddLabel(110, 64, 68, $"{tokens:N0}");
        AddImageTiled(10, 84, W - 20, 2, 9304);

        var y = 96;

        // 50 Bandages
        AddButton(18, y, 4005, 4007, BtnBuyBandages, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, tokens >= CostBandages ? 68 : 37,
            $"100 Bandages  —  {CostBandages} tokens");
        y += 32;

        // 5 Refresh Potions
        AddButton(18, y, 4005, 4007, BtnBuyRefresh, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, tokens >= CostRefresh ? 68 : 37,
            $"5 Refresh Potions  —  {CostRefresh} tokens");
        y += 32;

        // 3 Greater Heal Potions
        AddButton(18, y, 4005, 4007, BtnBuyHealPots, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, tokens >= CostHealPots ? 68 : 37,
            $"3 Greater Heal Potions  —  {CostHealPots} tokens");
        y += 32;

        // TrashBag replacement
        AddButton(18, y, 4005, 4007, BtnBuyTrashBag, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, tokens >= CostTrashBag ? 68 : 37,
            $"Replacement Trash Bag  —  {CostTrashBag} tokens");
        y += 32;

        AddImageTiled(10, y + 4, W - 20, 2, 9304);
        y += 18;

        // Back
        AddButton(18, y, 4014, 4016, BtnBack, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, 999, "Back");
    }

    // ── Join view ─────────────────────────────────────────────────────────────

    private void BuildJoinView(PlayerMobile pm, GuildDef def, IAccount acct)
    {
        AddHtml(18, 46, W - 36, 60,
            $"<BASEFONT COLOR=#CCCCCC>\"{def.Pitch}\"</BASEFONT>",
            false, false);

        AddImageTiled(10, 110, W - 20, 2, 9304);

        AddLabel(18, 120, 68, "Open to all citizens — no requirements.");
        AddHtml(18, 142, W - 36, 40,
            $"<BASEFONT COLOR=#AAAAAA>{def.TaskDescription}</BASEFONT>",
            false, false);

        AddImageTiled(10, 188, W - 20, 2, 9304);

        AddButton(18, 200, 4023, 4025, BtnJoin, GumpButtonType.Reply, 0);
        AddLabel(54, 202, 999, "Join The Custodians");

        AddImageTiled(10, 230, W - 20, 2, 9304);
        AddHtml(18, 240, W - 36, 80,
            "<BASEFONT COLOR=#888888>Members earn Civic Tokens by cleaning up litter and corpses " +
            "around Britannia using [cleanup and [cleanupall. " +
            "Tokens grow your rank and may be spent on guild rewards.</BASEFONT>",
            false, false);
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;

        switch (info.ButtonID)
        {
            case BtnJoin:
                HandleJoin(pm);
                break;

            case BtnShop:
                pm.SendGump(new SanitationWardenGump(pm, _def, _acct, shopView: true));
                break;

            case BtnBack:
                pm.SendGump(new SanitationWardenGump(pm, _def, _acct));
                break;

            case BtnBuyBandages:
                HandlePurchase(pm, CostBandages, () =>
                {
                    var b = new Bandage { Amount = 100 };
                    pm.Backpack?.DropItem(b);
                    pm.SendMessage(0x44, "You receive 100 bandages.");
                });
                break;

            case BtnBuyRefresh:
                HandlePurchase(pm, CostRefresh, () =>
                {
                    for (var i = 0; i < 5; i++)
                        pm.Backpack?.DropItem(new RefreshPotion());
                    pm.SendMessage(0x44, "You receive 5 refresh potions.");
                });
                break;

            case BtnBuyHealPots:
                HandlePurchase(pm, CostHealPots, () =>
                {
                    for (var i = 0; i < 3; i++)
                        pm.Backpack?.DropItem(new GreaterHealPotion());
                    pm.SendMessage(0x44, "You receive 3 greater heal potions.");
                });
                break;

            case BtnBuyTrashBag:
                HandlePurchase(pm, CostTrashBag, () =>
                {
                    pm.Backpack?.DropItem(new TrashBag());
                    pm.SendMessage(0x44, "You receive a replacement trash bag.");
                });
                break;
        }
    }

    // ── Purchase helper ───────────────────────────────────────────────────────

    private void HandlePurchase(PlayerMobile pm, int cost, Action giveItems)
    {
        if (pm.Backpack == null)
        {
            pm.SendMessage(0x22, "You have no backpack to receive items.");
            pm.SendGump(new SanitationWardenGump(pm, _def, _acct, shopView: true));
            return;
        }

        var data = ClusterFAccountPersistence.GetOrCreate(_acct);
        if (data.GetCurrency("custodians") < cost)
        {
            pm.SendMessage(0x22, "You do not have enough Civic Tokens for that.");
            pm.SendGump(new SanitationWardenGump(pm, _def, _acct, shopView: true));
            return;
        }

        data.AddCurrency("custodians", -cost);
        giveItems();
        pm.PlaySound(0x2E6);
        pm.SendGump(new SanitationWardenGump(pm, _def, _acct, shopView: true));
    }

    // ── Rank progress helper ──────────────────────────────────────────────────

    private static (int threshold, string name) GetNextRankInfo(int standing)
    {
        if (standing < 100)  return (100,  "Junior Custodian");
        if (standing < 500)  return (500,  "Custodian");
        if (standing < 2000) return (2000, "Senior Custodian");
        if (standing < 5000) return (5000, "Chief Custodian");
        return (-1, string.Empty);
    }

    // ── Join handler ──────────────────────────────────────────────────────────

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
            "Use [cleanup or [cleanupall to earn Civic Tokens.");
        pm.PlaySound(0x57);

        pm.SendGump(new SanitationWardenGump(pm, _def, _acct));
    }
}
