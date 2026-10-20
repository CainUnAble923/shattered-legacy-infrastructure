using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Main dialogue gump for the League Registrar NPC.
///
/// Views:
///   MainMenu       -- list of available topics (Join hidden after joining)
///   CitizenStatus  -- three-tier status explained + player's current tier
///   AboutRenown    -- what Renown is and current balance
///   GuildReferrals -- the Guild Directory (cc-P15): the same rows as the board's first page and [guild
///   WhereToStart   -- step-by-step guidance for new arrivals
///   Rank           -- (cc-P48) the character's League rank, the next one, the four promotion checks, Promote
///   MuleErrand     -- (cc-P69) the Compact's Mule errand: take it, deliver ore and ingots, collect the mule, replace a lost one
///
/// Opened by: LeagueRegistrar.OnDoubleClick
/// </summary>
public class LeagueRegistrarGump : Gump
{
    public enum View { MainMenu, CitizenStatus, AboutRenown, GuildReferrals, WhereToStart, Rank, MuleErrand }

    // cc-P48: the main menu's League Rank entry and the Rank page's Promote (the free IDs from 18).
    public const int BtnRank    = 18;
    public const int BtnPromote = 19;

    // cc-P69: the main menu's errand entry and its page's actions (the free IDs from 21).
    public const int BtnMuleErrand = 21;
    public const int BtnMuleAccept = 22;
    public const int BtnMuleDeliver = 23;
    public const int BtnMuleCollect = 24;
    public const int BtnMuleReplace = 25;

    // cc-P150 (F-47): the requisitions board, on the League Rank row's right (the menu's rows are full).
    public const int BtnRequisitions = 26;

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    // The rank the page was drawn at. Promote acts only from this rank, so a stale page promotes nobody.
    private readonly int          _rankAtDraw;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    public LeagueRegistrarGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;
        _rankAtDraw = ClusterFLeagueRanks.GetRank(pm);

        Closable   = true;
        Disposable = true;

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // -- Header --------------------------------------------------------
        AddLabel(W / 2 - 120, 12, 1154, "League of Extraordinary Citizens");
        AddLabel(W / 2 - 75,  28, 999,  "New Haven Field Office");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        // -- View content --------------------------------------------------
        switch (view)
        {
            case View.MainMenu:      DrawMainMenu(data);      break;
            case View.CitizenStatus: DrawCitizenStatus(data); break;
            case View.AboutRenown:   DrawAboutRenown(data);   break;
            case View.GuildReferrals: DrawGuildReferrals(data); break;
            case View.WhereToStart:  DrawWhereToStart(data);  break;
            case View.Rank:          DrawRank(data);          break;
            case View.MuleErrand:    DrawMuleErrand();        break;
        }

        // -- Footer --------------------------------------------------------
        AddImageTiled(10, H - 38, W - 20, 2, 9304);

        if (view != View.MainMenu)
        {
            AddButton(18, H - 28, 4014, 4015, 1);
            AddLabel(40, H - 26, 999, "Back");
        }

        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
    }

    // -- Views -----------------------------------------------------------------

    private void DrawMainMenu(ClusterFAccountData data)
    {
        var joined = ClusterFLeagueSystem.IsJoined(data);
        var status = ClusterFLeagueSystem.GetStatus(data, ClusterFAccountPersistence.GetGuild(_pm));
        var label  = ClusterFLeagueSystem.GetStatusLabel(status);
        var color  = ClusterFLeagueSystem.GetStatusColor(status);

        AddLabel(18, 56, 999, $"Welcome, {_pm.Name}.");
        AddHtml(18, 72, 200, 20,
            $"<BASEFONT COLOR=#{color}>Status: {label}</BASEFONT>", false, false);

        // cc-P48: the League rank beside the status, in its metal's hue.
        if (_rankAtDraw > 0)
            AddLabel(220, 72, ClusterFLeagueRanks.LabelHue(_rankAtDraw), ClusterFLeagueRanks.RankName(_rankAtDraw));

        AddImageTiled(10, 96, W - 20, 1, 9304);

        var y = 106;

        if (!joined)
        {
            AddButton(18, y, 4011, 4012, 10);
            AddLabel(44, y + 2, 999, "Join the League");
            y += 28;
        }

        AddButton(18, y, 4011, 4012, 11); AddLabel(44, y + 2, 999, "Citizen Status");    y += 28;
        AddButton(18, y, 4011, 4012, 12); AddLabel(44, y + 2, 999, "About Renown");      y += 28;
        AddButton(18, y, 4011, 4012, 13); AddLabel(44, y + 2, 999, "Achievements");      y += 28;
        AddButton(18, y, 4011, 4012, 14); AddLabel(44, y + 2, 999, "Guilds Overview");   y += 28;
        AddButton(18, y, 4011, 4012, 15); AddLabel(44, y + 2, 999, "Guild Referrals");   y += 28;
        AddButton(18, y, 4011, 4012, 16); AddLabel(44, y + 2, 999, "League Dispatch");   y += 28;
        AddButton(18, y, 4011, 4012, 17); AddLabel(44, y + 2, 999, "Where to go first?"); y += 28;
        AddButton(18, y, 4011, 4012, BtnRank); AddLabel(44, y + 2, 999, "League Rank");
        AddButton(230, y, 4011, 4012, BtnRequisitions); AddLabel(256, y + 2, 999, "Requisitions board"); y += 28;
        AddButton(18, y, 4011, 4012, BtnMuleErrand); AddLabel(44, y + 2, 999, "The Compact's Mule (errand)");
    }

    // cc-P69: the Compact's Mule errand (MuleErrand). The page says where the character is; every button re-checks.
    private void DrawMuleErrand()
    {
        AddLabel(18, 56, 1154, "The Compact's Mule");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var stage = MuleErrand.Stage(_pm);
        var refusal = MuleErrand.RefuseOffer(_pm);
        string text;
        var button = 0;
        var buttonLabel = "";

        switch (stage)
        {
            case MuleErrand.NotTaken when refusal == null:
                text = "The Miners' Compact lends a hardy pack mule to League citizens who can prove they work a vein. " +
                       $"Bring {MuleErrand.OreCount} iron ore and {MuleErrand.IngotCount} iron ingots you mined and smelted, " +
                       "and the mule is yours, bonded to you. One per citizen.";
                button = BtnMuleAccept; buttonLabel = "Take the errand";
                break;
            case MuleErrand.NotTaken:
                text = refusal;
                break;
            case MuleErrand.Taken:
                text = $"Bring {MuleErrand.OreCount} iron ore and {MuleErrand.IngotCount} iron ingots (backpack or bank; satchels count).";
                button = BtnMuleDeliver; buttonLabel = "Deliver the ore and ingots";
                break;
            case MuleErrand.Delivered:
                text = "Your mule is waiting. It needs one free follower slot.";
                button = BtnMuleCollect; buttonLabel = "Collect my mule";
                break;
            default:
                text = MuleErrand.CanReplace(_pm)
                    ? $"You have no mule. The Compact will find you another for {MuleErrand.ReplaceGold:N0} gold and {MuleErrand.ReplaceVouchers} Mining Vouchers."
                    : "You have your mule. Raise its tier at the Civic Mule Exchange in any trade guild's hall.";

                if (MuleErrand.CanReplace(_pm))
                {
                    button = BtnMuleReplace; buttonLabel = "Replace my mule";
                }

                break;
        }

        AddHtml(16, 82, W - 32, 200, $"<BASEFONT COLOR=#AAAAAA>{text}</BASEFONT>", false, false);

        if (button != 0)
        {
            AddButton(18, 300, 4011, 4012, button);
            AddLabel(44, 302, 0x44, buttonLabel);
        }
    }

    private void DrawCitizenStatus(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Citizen Status");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var status = ClusterFLeagueSystem.GetStatus(data, ClusterFAccountPersistence.GetGuild(_pm));
        var label  = ClusterFLeagueSystem.GetStatusLabel(status);
        var color  = ClusterFLeagueSystem.GetStatusColor(status);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The League recognizes three tiers of citizenship:</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>[ ] Unregistered</BASEFONT>" +
            "<BASEFONT COLOR=#555555> - Not yet enrolled with the League.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#5599FF>[+] Registered Citizen</BASEFONT>" +
            "<BASEFONT COLOR=#AAAAAA> - Enrolled with the League. Welcome to the rolls.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FFD700>[+] Recognized Citizen</BASEFONT>" +
            "<BASEFONT COLOR=#AAAAAA> - Member of at least one guild." +
            " Your deeds are noted in the League records.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Your current status: </BASEFONT>" +
            $"<BASEFONT COLOR=#{color}>{label}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Beside your status, each citizen holds a League rank, Iron Citizen to " +
            "Celestial Citizen. See League Rank on the main page.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 150, html, false, true);

        // cc-P48: this character's rank, in its metal's hue.
        AddLabel(18, H - 64, 999, "League rank:");
        AddLabel(110, H - 64, _rankAtDraw > 0 ? ClusterFLeagueRanks.LabelHue(_rankAtDraw) : 999,
            ClusterFLeagueRanks.RankName(_rankAtDraw));
    }

    // cc-P48: current rank, next rank, the four checks, Promote.
    private void DrawRank(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "League Rank");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        AddLabel(18, 80, 999, "Your rank:");
        AddLabel(110, 80, _rankAtDraw > 0 ? ClusterFLeagueRanks.LabelHue(_rankAtDraw) : 999,
            ClusterFLeagueRanks.RankName(_rankAtDraw));

        var report = ClusterFLeagueRanks.Evaluate(_pm);

        if (report.Blocker != null)
        {
            AddHtml(18, 108, W - 36, 80, $"<BASEFONT COLOR=#AAAAAA>{report.Blocker}</BASEFONT>", false, false);
            return;
        }

        AddLabel(18, 102, 999, "Next rank:");
        AddLabel(110, 102, ClusterFLeagueRanks.LabelHue(report.To), ClusterFLeagueRanks.RankName(report.To));
        AddImageTiled(10, 126, W - 20, 1, 9304);

        var y = 134;
        foreach (var check in report.Checks)
        {
            var (mark, hue) = check.State switch
            {
                LeagueCheckState.Passed      => ("[x]", 68),
                LeagueCheckState.Failed      => ("[ ]", 37),
                LeagueCheckState.Waived      => ("[-]", 999),
                _                            => ("[-]", 999),
            };

            AddLabel(18, y, hue, mark);
            AddLabel(50, y, 1153, check.Label);
            AddLabel(50, y + 18, 999, check.Detail);
            y += 44;
        }

        if (report.CanPromote)
        {
            AddButton(18, y + 4, 4023, 4025, BtnPromote);
            AddLabel(52, y + 6, 1154, $"Promote to {ClusterFLeagueRanks.RankName(report.To)}");
        }
        else
        {
            AddLabel(18, y + 6, 999, "Meet every unticked check to be promoted.");
        }
    }

    private void DrawAboutRenown(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "About Renown");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            $"<BASEFONT COLOR=#FFD700>Your Renown: {data.Renown}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Renown is the League's measure of your deeds and contributions " +
            "to the citizens of Sosaria. It is earned by completing achievements, joining guilds, " +
            "and performing services recognised by the League.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Renown is account-wide and permanent. It cannot be lost. " +
            "Future League commissions and rewards will draw upon your standing.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Achievement Points (AP) are a separate permanent prestige score " +
            "that tracks the breadth of your accomplishments. Both are visible via [achievements.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawGuildReferrals(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Guild Referrals");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        AddHtml(16, 78, W - 32, 80,
            "<BASEFONT COLOR=#AAAAAA>The League refers every citizen to the guilds of New Haven. " +
            "The Guild Directory lists each one, what it teaches, and where its guildmaster stands, " +
            "with an arrow to show you the way. Joining is free.</BASEFONT>", false, false);

        AddButton(18, 170, 4011, 4012, 20);
        AddLabel(44, 172, 999, "Open the Guild Directory");
    }

    private void DrawWhereToStart(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Where to go first?");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var joined = ClusterFLeagueSystem.IsJoined(data);
        var step   = 1;

        var html = "";

        if (!joined)
        {
            html += $"<BASEFONT COLOR=#5599FF>{step++}. Register with the League.</BASEFONT> " +
                    "<BASEFONT COLOR=#AAAAAA>Use the main menu to join. It is free and takes a moment." +
                    "</BASEFONT><BR><BR>";
        }

        html +=
            $"<BASEFONT COLOR=#5599FF>{step++}. Read the League Dispatch.</BASEFONT> " +
            "<BASEFONT COLOR=#AAAAAA>The Dispatch contains current notices and announcements from " +
            "the League. Access it from the main menu.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#5599FF>{step++}. Join a guild.</BASEFONT> " +
            "<BASEFONT COLOR=#AAAAAA>Every skill has one, and joining is free. The Guild Board in the " +
            "town square, or [guild, lists them all and shows you the way to each guildmaster.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#5599FF>{step}. Explore.</BASEFONT> " +
            "<BASEFONT COLOR=#AAAAAA>The ruins of Old Haven lie to the southwest. " +
            "Use [achievements to track your progress across Shattered Legacy.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    // -- Response --------------------------------------------------------------

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return; // Close

        // Back
        if (info.ButtonID == 1)
        {
            _pm.SendGump(new LeagueRegistrarGump(_pm, View.MainMenu));
            return;
        }

        var acct = _pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        switch (info.ButtonID)
        {
            case 10: // Join the League
                if (data != null && !ClusterFLeagueSystem.IsJoined(data))
                    ClusterFLeagueSystem.JoinLeague(_pm);
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.MainMenu));
                break;

            case 11: // Citizen Status
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.CitizenStatus));
                break;

            case 12: // About Renown
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.AboutRenown));
                break;

            case 13: // Achievements
                _pm.SendGump(new AchievementsGump(_pm));
                break;

            case 14: // Guilds Overview
                if (acct != null)
                    _pm.SendGump(new GuildProgressGump(_pm, acct));
                break;

            case 15: // Guild Referrals: the Guild Directory (cc-P15)
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.GuildReferrals));
                break;

            case 16: // League Dispatch
                if (acct != null)
                {
                    ClusterFLeagueSystem.OnDispatchRead(_pm);
                    var bulletins = new List<BulletinEntry>(ClusterFBulletinSystem.Bulletins);
                    _pm.SendGump(new BulletinGump(_pm, bulletins));
                }
                break;

            case 17: // Where to go first?
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.WhereToStart));
                break;

            case 20: // Open the Guild Directory. Its "Show me the way" replaces the Miners' Compact arrow.
                _pm.SendGump(new GuildProgressGump(_pm, acct));
                break;

            case BtnMuleErrand:
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.MuleErrand));
                break;

            case BtnMuleAccept or BtnMuleDeliver or BtnMuleCollect or BtnMuleReplace when _view == View.MuleErrand:
                {
                    string msg;
                    var ok = info.ButtonID switch
                    {
                        BtnMuleAccept  => MuleErrand.Accept(_pm, out msg),
                        BtnMuleDeliver => MuleErrand.Deliver(_pm, out msg),
                        BtnMuleCollect => MuleErrand.Collect(_pm, out msg) != null,
                        _              => MuleErrand.Replace(_pm, out msg) != null
                    };

                    if (msg != null)
                        _pm.SendMessage(ok ? 0x44 : 0x22, msg);

                    _pm.SendGump(new LeagueRegistrarGump(_pm, View.MuleErrand));
                }
                break;

            case BtnRequisitions: // cc-P150 (F-47): the League board, every guild
                Server.Items.RequisitionsBoard.OpenFor(_pm, null);
                return;

            case BtnRank: // League Rank (cc-P48)
                _pm.SendGump(new LeagueRegistrarGump(_pm, View.Rank));
                break;

            case BtnPromote: // Promote: every check again, server side, from the rank this page was drawn at
                if (_view == View.Rank)
                {
                    var promoted = ClusterFLeagueRanks.TryPromote(_pm, _rankAtDraw, out var message);
                    _pm.SendMessage(promoted ? 0x44 : 0x22, message);
                    _pm.SendGump(new LeagueRegistrarGump(_pm, View.Rank));
                }
                break;
        }
    }
}
