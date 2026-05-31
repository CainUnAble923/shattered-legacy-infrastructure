using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Outriders' Guildmaster NPC.
///
/// Views:
///   MainMenu        -- topic list; shows Join or Dashboard depending on membership
///   AboutOutriders  -- guild identity and purpose
///   WhatWeHunt      -- wilderness resources and creatures the Outriders work with
///   JoiningReqs     -- what is required to join
///   Rewards         -- standing and Trail Marks awarded on join
///   MemberDashboard -- standing, rank, Trail Mark balance; links to work orders
/// </summary>
public class OutridersGuildmasterGump : Gump
{
    public enum View
    {
        MainMenu, AboutOutriders, WhatWeHunt, JoiningReqs, Rewards, MemberDashboard
    }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    public OutridersGuildmasterGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        ClusterFLeagueSystem.OnGuildReferralSeen(pm, "rangers");

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 75, 12, 1154, "Rangers' League");
        AddLabel(W / 2 - 65, 28, 999,  "The Outriders");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        switch (view)
        {
            case View.MainMenu:        DrawMainMenu(data);        break;
            case View.AboutOutriders:  DrawAboutOutriders();      break;
            case View.WhatWeHunt:      DrawWhatWeHunt();          break;
            case View.JoiningReqs:     DrawJoiningReqs();         break;
            case View.Rewards:         DrawRewards();             break;
            case View.MemberDashboard: DrawMemberDashboard(data); break;
        }

        AddImageTiled(10, H - 38, W - 20, 2, 9304);

        if (view is not View.MainMenu)
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
        var isMember = data.JoinedGuilds.Contains("rangers");

        AddLabel(18, 56, 999, "What can I do for you, traveller?");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var y = 82;
        AddButton(18, y, 4011, 4012, 11); AddLabel(44, y + 2, 999, "About the Outriders");    y += 28;
        AddButton(18, y, 4011, 4012, 12); AddLabel(44, y + 2, 999, "What We Hunt and Tame");  y += 28;
        AddButton(18, y, 4011, 4012, 13); AddLabel(44, y + 2, 999, "Joining Requirements");   y += 28;
        AddButton(18, y, 4011, 4012, 14); AddLabel(44, y + 2, 999, "Rewards and Trail Marks"); y += 28;

        y += 8;
        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        if (isMember)
        {
            AddButton(18, y, 4011, 4012, 30);
            AddLabel(44, y + 2, 999, "Member Dashboard");
        }
        else
        {
            AddButton(18, y, 4011, 4012, 20);
            AddLabel(44, y + 2, 999, "Join the Rangers' League");
        }
    }

    private void DrawAboutOutriders()
    {
        AddLabel(18, 56, 1154, "About the Outriders");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Outriders are the Rangers' League field company — " +
            "the ones who actually go out there. While other guilds work their forges and " +
            "counting houses, we range the wilderness, track the beasts, and keep the roads " +
            "between New Haven and the deep country passable.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>We are animal tamers, hunters, scouts, and expedition " +
            "guides. Our pack animals carry what miners dig up. Our hides go to the smiths. " +
            "We are the connective tissue of the field economy.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The Outriders do not compete for prestige with guilds " +
            "that work behind walls. We measure ourselves by how far we've ranged and how " +
            "well our animals trust us.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawWhatWeHunt()
    {
        AddLabel(18, 56, 1154, "What We Hunt and Tame");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Outriders work the full range of Britannia's wilderness." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Leather and Hides</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — From wolves, hinds, and wilderness beasts. " +
            "Tougher creatures yield spined, horned, and barbed hides.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Feathers</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Collected from birds of all kinds. Used in fletching " +
            "and alchemical reagent work.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Lumber</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Cut from the forest for camp construction and " +
            "expedition rigging.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Pack Animals</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Tamed mules and horses are the Outriders' " +
            "primary logistics tool. A tamed mule can carry far more than any backpack." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Veterans speak of rarer things in the deep wilderness — " +
            "creatures whose hides and bones the guild will pay well for. " +
            "Build your rank and you'll learn what they mean.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawJoiningReqs()
    {
        AddLabel(18, 56, 1154, "Joining Requirements");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def     = ClusterFGuildSystem.GetDef("rangers");
        var reqText = def?.TaskDescription
            ?? "Speak with the Outriders' Guildmaster to learn the current requirements.";

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Outriders take anyone willing to work the field. " +
            "We do not care much for titles. We care whether you can survive out there." +
            "</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFD700>{reqText}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Click 'Join the Rangers' League' on the main menu " +
            "when you are ready.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawRewards()
    {
        AddLabel(18, 56, 1154, "Rewards and Trail Marks");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def = ClusterFGuildSystem.GetDef("rangers");

        var repLine = def != null
            ? $"<BASEFONT COLOR=#FFD700>+{def.JoinReputation} Outriders Standing</BASEFONT><BR>" +
              $"<BASEFONT COLOR=#FFD700>+{def.JoinScrip} Trail Marks</BASEFONT><BR><BR>"
            : "";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Upon joining the Rangers' League you receive:</BASEFONT><BR><BR>" +
            repLine +
            "<BASEFONT COLOR=#AAAAAA>Trail Marks are the guild's field currency. " +
            "They buy supplies, animals, and expedition equipment from the guild store.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Outriders Standing tracks your rank within the League. " +
            "Higher rank unlocks harder contracts and better equipment.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Ranks: Wanderer > Scout > Outrider > Trailblazer > " +
            "Beastmaster > Warden of the Wild > Legendary Outrider</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawMemberDashboard(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Member Dashboard");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        data.GuildReputation.TryGetValue("rangers", out var standing);
        data.GuildCurrency.TryGetValue("rangers",   out var marks);
        var rank = GetRankName(standing);

        var html =
            $"<BASEFONT COLOR=#FFD700>Rank: {rank}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Outriders Standing: {standing:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Trail Marks: {marks}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Wanderer (0) > Scout (1,000) > Outrider (5,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Trailblazer (15,000) > Beastmaster (40,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Warden of the Wild (80,000) > Legendary Outrider (150,000)</BASEFONT>";

        AddHtml(16, 78, W - 32, 130, html, false, false);

        AddImageTiled(10, 216, W - 20, 1, 9304);

        AddButton(18, 226, 4011, 4012, 31);
        AddLabel(44, 228, 999, "Field Contracts");

        data.GuildCurrency.TryGetValue("rangers", out var curMarks);

        // Crook replacement — only offered if the member has no crook of any tier
        if (HasAnyCrook(_pm))
        {
            AddLabel(18, 256, 999, "Outrider's Crook: in your possession");
        }
        else
        {
            var isFree    = curMarks < CrookReplacementCost;
            var costLabel = isFree ? "Free (first issue)" : $"{CrookReplacementCost} Trail Marks";
            AddButton(18, 256, 4011, 4012, 32);
            AddLabel(44, 258, 999, $"Replace Outrider's Crook ({costLabel})");
        }

        // Satchel replacement — only offered if the member has no satchel of any tier
        if (HasAnySatchel(_pm))
        {
            AddLabel(18, 280, 999, "Hunter's Satchel: in your possession");
        }
        else
        {
            var isFree    = curMarks < SatchelReplacementCost;
            var costLabel = isFree ? "Free (first issue)" : $"{SatchelReplacementCost} Trail Marks";
            AddButton(18, 280, 4011, 4012, 33);
            AddLabel(44, 282, 999, $"Replace Hunter's Satchel ({costLabel})");
        }

        AddImageTiled(10, 308, W - 20, 1, 9304);

        AddButton(18, 316, 4011, 4012, 50);
        AddLabel(44, 318, 1154, "Cross-Guild Exchange (Pack Mule)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private const int CrookReplacementCost   = 50;
    private const int SatchelReplacementCost = 25;

    private static bool HasAnyCrook(PlayerMobile pm)
    {
        if (pm.Backpack?.FindItemByType<OutridersCrook>() != null) return true;
        if (pm.FindItemOnLayer(Layer.OneHanded) is OutridersCrook)  return true;
        if (pm.FindItemOnLayer(Layer.TwoHanded) is OutridersCrook)  return true;
        return false;
    }

    private static bool HasAnySatchel(PlayerMobile pm) =>
        pm.Backpack?.FindItemByType<HuntersSatchel>() != null;

    public static string GetRankName(int standing) => standing switch
    {
        >= 150000 => "Legendary Outrider",
        >= 80000  => "Warden of the Wild",
        >= 40000  => "Beastmaster",
        >= 15000  => "Trailblazer",
        >= 5000   => "Outrider",
        >= 1000   => "Scout",
        _         => "Wanderer"
    };

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1)
        {
            _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MainMenu));
            return;
        }

        var acct = _pm.Account as IAccount;

        switch (info.ButtonID)
        {
            case 11: _pm.SendGump(new OutridersGuildmasterGump(_pm, View.AboutOutriders)); break;
            case 12: _pm.SendGump(new OutridersGuildmasterGump(_pm, View.WhatWeHunt));     break;
            case 13: _pm.SendGump(new OutridersGuildmasterGump(_pm, View.JoiningReqs));    break;
            case 14: _pm.SendGump(new OutridersGuildmasterGump(_pm, View.Rewards));        break;

            case 20:
            {
                var def = ClusterFGuildSystem.GetDef("rangers");
                if (def != null && acct != null)
                    _pm.SendGump(new GuildTaskDetailGump(_pm, def, acct));
                break;
            }

            case 30: _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MemberDashboard)); break;
            case 31: _pm.SendGump(new GuildContractLedgerGump(_pm, "rangers"));             break;
            case 50: _pm.SendGump(new CrossGuildExchangeGump(_pm));                         break;

            case 32:
            {
                if (acct == null) break;
                var data = ClusterFAccountPersistence.GetOrCreate(acct);

                if (!data.JoinedGuilds.Contains("rangers"))
                {
                    _pm.SendMessage(0x22, "You are not a member of the Rangers' League.");
                    break;
                }

                if (HasAnyCrook(_pm))
                {
                    _pm.SendMessage(0x22, "You already have an Outrider's Crook.");
                    _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MemberDashboard));
                    break;
                }

                if (_pm.Backpack == null)
                {
                    _pm.SendMessage(0x22, "You have no backpack to receive the crook.");
                    break;
                }

                data.GuildCurrency.TryGetValue("rangers", out var curMarks);

                // If the player can't afford the fee they get a free first-issue crook.
                // This covers members who joined before the crook giveaway was implemented
                // and have no way to earn Trail Marks without one.
                if (curMarks >= CrookReplacementCost)
                    data.GuildCurrency["rangers"] = curMarks - CrookReplacementCost;

                _pm.Backpack.DropItem(new OutridersCrook());
                _pm.SendMessage(0x44, curMarks >= CrookReplacementCost
                    ? "The Guildmaster hands you a replacement Outrider's Crook."
                    : "The Guildmaster issues you an Outrider's Crook to get you started.");
                _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MemberDashboard));
                break;
            }

            case 33:
            {
                if (acct == null) break;
                var data = ClusterFAccountPersistence.GetOrCreate(acct);

                if (!data.JoinedGuilds.Contains("rangers"))
                {
                    _pm.SendMessage(0x22, "You are not a member of the Rangers' League.");
                    break;
                }

                if (HasAnySatchel(_pm))
                {
                    _pm.SendMessage(0x22, "You already have a Hunter's Satchel.");
                    _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MemberDashboard));
                    break;
                }

                if (_pm.Backpack == null)
                {
                    _pm.SendMessage(0x22, "You have no backpack to receive the satchel.");
                    break;
                }

                data.GuildCurrency.TryGetValue("rangers", out var curMarks);

                if (curMarks >= SatchelReplacementCost)
                    data.GuildCurrency["rangers"] = curMarks - SatchelReplacementCost;

                _pm.Backpack.DropItem(new HuntersSatchel());
                _pm.SendMessage(0x44, curMarks >= SatchelReplacementCost
                    ? "The Guildmaster hands you a replacement Hunter's Satchel."
                    : "The Guildmaster issues you a Hunter's Satchel to get you started.");
                _pm.SendGump(new OutridersGuildmasterGump(_pm, View.MemberDashboard));
                break;
            }
        }
    }
}
