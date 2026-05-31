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
///   GuildReferrals -- which guilds the League recommends; direction to liaisons
///   WhereToStart   -- step-by-step guidance for new arrivals
///
/// Opened by: LeagueRegistrar.OnDoubleClick
/// </summary>
public class LeagueRegistrarGump : Gump
{
    public enum View { MainMenu, CitizenStatus, AboutRenown, GuildReferrals, WhereToStart }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    public LeagueRegistrarGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - 120, 12, 1154, "League of Extraordinary Citizens");
        AddLabel(W / 2 - 75,  28, 999,  "New Haven Field Office");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        // ── View content ──────────────────────────────────────────────────
        switch (view)
        {
            case View.MainMenu:      DrawMainMenu(data);      break;
            case View.CitizenStatus: DrawCitizenStatus(data); break;
            case View.AboutRenown:   DrawAboutRenown(data);   break;
            case View.GuildReferrals: DrawGuildReferrals(data); break;
            case View.WhereToStart:  DrawWhereToStart(data);  break;
        }

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(10, H - 38, W - 20, 2, 9304);

        if (view != View.MainMenu)
        {
            AddButton(18, H - 28, 4014, 4015, 1);
            AddLabel(40, H - 26, 999, "Back");
        }

        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    private void DrawMainMenu(ClusterFAccountData data)
    {
        var joined = ClusterFLeagueSystem.IsJoined(data);
        var status = ClusterFLeagueSystem.GetStatus(data);
        var label  = ClusterFLeagueSystem.GetStatusLabel(status);
        var color  = ClusterFLeagueSystem.GetStatusColor(status);

        AddLabel(18, 56, 999, $"Welcome, {_pm.Name}.");
        AddHtml(18, 72, W - 36, 20,
            $"<BASEFONT COLOR=#{color}>Status: {label}</BASEFONT>", false, false);
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
        AddButton(18, y, 4011, 4012, 17); AddLabel(44, y + 2, 999, "Where to go first?");
    }

    private void DrawCitizenStatus(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Citizen Status");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var status = ClusterFLeagueSystem.GetStatus(data);
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
            $"<BASEFONT COLOR=#{color}>{label}</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
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

        var html =
            "<BASEFONT COLOR=#AAAAAA>The League maintains partnerships with the professional guilds " +
            "of New Haven. As a citizen, you are encouraged to seek membership in any guild that " +
            "matches your calling.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FFD700>Miners' Compact</BASEFONT><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The Miners' Compact represents those who work the stone and ore " +
            "of Britannia. Their liaison is stationed near the south mountain mine." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Additional guild liaisons will be established at this field office " +
            "as the League expands its presence throughout New Haven.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 170, html, false, true);

        AddButton(18, H - 80, 4011, 4012, 20);
        AddLabel(44, H - 78, 999, "Find the Miners' Compact Liaison");
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
            $"<BASEFONT COLOR=#5599FF>{step++}. Visit a guild liaison.</BASEFONT> " +
            "<BASEFONT COLOR=#AAAAAA>The Miners' Compact Liaison is near the south mountain mine " +
            "(around 3508, 2748). Introduce yourself and learn what the guild offers.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#5599FF>{step}. Explore.</BASEFONT> " +
            "<BASEFONT COLOR=#AAAAAA>The ruins of Old Haven lie to the southwest. " +
            "Use [achievements to track your progress across Shattered Legacy.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    // ── Response ──────────────────────────────────────────────────────────────

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

            case 15: // Guild Referrals
                if (data != null)
                    ClusterFLeagueSystem.OnGuildReferralSeen(_pm, "mining");
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

            case 20: // Direction to Miners' Compact Liaison — set quest arrow
            {
                _pm.QuestArrow?.Stop();

                // Find the live Miners' Compact Liaison NPC — QuestArrow requires a non-null Mobile target
                Mobile? liaison = null;
                foreach (var mob in World.Mobiles.Values)
                {
                    if (mob is MinersCompactLiaison && !mob.Deleted)
                    {
                        liaison = mob;
                        break;
                    }
                }

                if (liaison != null)
                {
                    new MinersCompactDirectionArrow(_pm, liaison);
                    _pm.SendMessage(999,
                        "Head south from the League office to the mountain mine — follow the arrow to the Miners' Compact Liaison.");
                }
                else
                {
                    _pm.SendMessage(999,
                        "The Miners' Compact Liaison is near the south mountain mine, around coordinates 3510, 2748.");
                }

                _pm.SendGump(new LeagueRegistrarGump(_pm, View.GuildReferrals));
                break;
            }
        }
    }
}

/// <summary>
/// Quest arrow pointing players from the League field office south to the
/// Miners' Compact Liaison NPC. Right-clicking dismisses it.
/// Auto-stops after 10 minutes so it doesn't linger indefinitely.
///
/// Note: QuestArrow requires a non-null Mobile target — Target.Serial is used
/// in the SendSetArrow/SendCancelArrow packets. We pass the actual liaison NPC.
/// </summary>
public class MinersCompactDirectionArrow : QuestArrow
{
    public MinersCompactDirectionArrow(PlayerMobile m, Mobile liaison) : base(m, liaison)
    {
        // Auto-dismiss after 10 minutes
        Timer.DelayCall(TimeSpan.FromMinutes(10), () => { if (Running) Stop(); });
    }

    public override void OnClick(bool rightClick)
    {
        if (rightClick)
            Stop();
    }
}
