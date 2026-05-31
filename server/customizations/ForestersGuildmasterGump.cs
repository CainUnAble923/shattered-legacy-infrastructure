using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Foresters' Guildmaster NPC.
///
/// Views:
///   MainMenu        -- topic list; shows Join or Dashboard depending on membership
///   AboutForesters  -- guild identity and purpose
///   WhatWeHarvest   -- wood types and resources the guild works with
///   JoiningReqs     -- what is required to join
///   Rewards         -- standing and Timber Tokens awarded on join
///   MemberDashboard -- standing, rank, Timber Token balance; links to work orders
/// </summary>
public class ForestersGuildmasterGump : Gump
{
    public enum View
    {
        MainMenu, AboutForesters, WhatWeHarvest, JoiningReqs, Rewards, MemberDashboard, Discoveries
    }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    public ForestersGuildmasterGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        ClusterFLeagueSystem.OnGuildReferralSeen(pm, "foresters");

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 70, 12, 1154, "Foresters' Union");
        AddLabel(W / 2 - 65, 28, 999,  "Cedric Rowanwood");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        switch (view)
        {
            case View.MainMenu:        DrawMainMenu(data);        break;
            case View.AboutForesters:  DrawAboutForesters();      break;
            case View.WhatWeHarvest:   DrawWhatWeHarvest();       break;
            case View.JoiningReqs:     DrawJoiningReqs();         break;
            case View.Rewards:         DrawRewards();             break;
            case View.MemberDashboard: DrawMemberDashboard(data); break;
            case View.Discoveries:     DrawDiscoveries(data);     break;
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
        var isMember = data.JoinedGuilds.Contains("foresters");

        AddLabel(18, 56, 999, "What brings you to the wood yard, friend?");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var y = 82;
        AddButton(18, y, 4011, 4012, 11); AddLabel(44, y + 2, 999, "About the Foresters' Union");  y += 28;
        AddButton(18, y, 4011, 4012, 12); AddLabel(44, y + 2, 999, "What We Harvest");             y += 28;
        AddButton(18, y, 4011, 4012, 13); AddLabel(44, y + 2, 999, "Joining Requirements");        y += 28;
        AddButton(18, y, 4011, 4012, 14); AddLabel(44, y + 2, 999, "Rewards and Timber Tokens");   y += 28;

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
            AddLabel(44, y + 2, 999, "Join the Foresters' Union");
        }
    }

    private void DrawAboutForesters()
    {
        AddLabel(18, 56, 1154, "About the Foresters' Union");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Foresters' Union is the guild of those who work the wood — " +
            "the axe-wielders, saw-workers, and bark-readers who turn standing timber into " +
            "the boards, beams, and planks that hold Britannia together.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>We supply the carpenters, the shipwrights, and the builders. " +
            "We keep the Rangers' field camps stocked and the miners' shaft supports sound. " +
            "Every house in New Haven rests on our work.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The Union recognises all grades of woodworker, from the " +
            "novice with a hand axe to the master who knows which grove holds heartwood and " +
            "where the bloodwood grows. We have contracts for every skill level, and we pay " +
            "fairly for every board delivered.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawWhatWeHarvest()
    {
        AddLabel(18, 56, 1154, "What We Harvest");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Union buys all grades of timber cut from Britannia's forests." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Common Boards</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Cut from the pines and oaks of the lowland forests. " +
            "The backbone of carpentry and construction.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Oak and Ash</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Harder and more durable than common wood. Found in " +
            "the older stands of the inland groves.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Yew</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — The bowyer's wood. Dense and flexible, prized by " +
            "ranged weapon crafters and high-end furniture makers.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Heartwood</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Found in the oldest forest giants. Difficult to cut " +
            "but worth the effort for fine construction work.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Bloodwood and Frostwood</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — The rarest timbers, found only in the deepest wilderness. " +
            "The Union pays premium for these. Only veteran foresters reach them.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawJoiningReqs()
    {
        AddLabel(18, 56, 1154, "Joining Requirements");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def     = ClusterFGuildSystem.GetDef("foresters");
        var reqText = def?.TaskDescription
            ?? "Speak with the Foresters' Guildmaster to learn the current requirements.";

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Union is open to any who make their living from the wood. " +
            "We ask only that you demonstrate basic competence with an axe before we put " +
            "you on the contract rolls.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFD700>{reqText}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Click 'Join the Foresters' Union' on the main menu " +
            "when you are ready.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawRewards()
    {
        AddLabel(18, 56, 1154, "Rewards and Timber Tokens");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def = ClusterFGuildSystem.GetDef("foresters");

        var repLine = def != null
            ? $"<BASEFONT COLOR=#FFD700>+{def.JoinReputation} Foresters' Standing</BASEFONT><BR>" +
              $"<BASEFONT COLOR=#FFD700>+{def.JoinScrip} Timber Tokens</BASEFONT><BR><BR>"
            : "";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Upon joining the Foresters' Union you receive:</BASEFONT><BR><BR>" +
            repLine +
            "<BASEFONT COLOR=#AAAAAA>Timber Tokens are the guild's trade currency. " +
            "They can be redeemed for tools, supplies, and quality crafting materials " +
            "from the guild store.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Foresters' Standing tracks your rank within the Union. " +
            "Higher rank unlocks more demanding contracts and better rewards.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Ranks: Woodcutter > Sawyer > Forester > Arborist > " +
            "Grove Warden > Master of the Wood > Legendary Forester</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawMemberDashboard(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Member Dashboard");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        data.GuildReputation.TryGetValue("foresters", out var standing);
        data.GuildCurrency.TryGetValue("foresters",   out var tokens);
        var rank = GetRankName(standing);

        var html =
            $"<BASEFONT COLOR=#FFD700>Rank: {rank}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Foresters' Standing: {standing:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Timber Tokens: {tokens}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Woodcutter (0) > Sawyer (1,000) > Forester (5,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Arborist (15,000) > Grove Warden (40,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Master of the Wood (80,000) > Legendary Forester (150,000)</BASEFONT>";

        AddHtml(16, 78, W - 32, 130, html, false, false);

        AddImageTiled(10, 216, W - 20, 1, 9304);

        AddButton(18, 226, 4011, 4012, 31);
        AddLabel(44, 228, 999, "Timber Contracts");

        AddButton(18, 254, 4011, 4012, 32);
        AddLabel(44, 256, 999, "Forest Discoveries");

        AddImageTiled(10, 280, W - 20, 1, 9304);

        var y2 = 290;

        var hasLogbook = _pm.Backpack?.FindItemByType<Items.ForestersLogbook>() != null;
        if (hasLogbook)
        {
            AddLabel(18, y2, 0x3B2, "Forester's Logbook — in your backpack.");
        }
        else
        {
            AddButton(18, y2, 4011, 4012, 33);
            AddLabel(44, y2 + 2, 0x44, "Replace Forester's Logbook (5 Timber Tokens)");
        }
        y2 += 24;

        var hasSatchel = _pm.Backpack?.FindItemByType<Items.ForestersLumberSatchel>() != null;
        if (hasSatchel)
        {
            AddLabel(18, y2, 0x3B2, "Lumber Satchel — in your backpack.");
        }
        else
        {
            AddButton(18, y2, 4011, 4012, 34);
            AddLabel(44, y2 + 2, 0x44, "Replace Lumber Satchel (5 Timber Tokens)");
        }

        AddImageTiled(10, 348, W - 20, 1, 9304);

        AddButton(18, 356, 4011, 4012, 50);
        AddLabel(44, 358, 1154, "Cross-Guild Exchange (Pack Mule)");
    }

    private static readonly string[] _extendedWoodKeys =
        { "Ironwood", "Ghostwood", "Emberbark", "Frostbark", "Shadowbark", "Runewood", "Voidwood", "Starwood" };

    private void DrawDiscoveries(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Forest Discoveries");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var discoveries = data.WoodDiscoveries;
        var anyUnreported = false;

        foreach (var key in _extendedWoodKeys)
        {
            if (discoveries.TryGetValue(key, out var e) && e.State == DiscoveryState.Discovered)
            {
                anyUnreported = true;
                break;
            }
        }

        var y = 78;

        if (anyUnreported)
        {
            AddButton(18, y, 4011, 4012, 40);
            AddLabel(44, y + 2, 0x44, "Report All Unreported");
            y += 28;
        }

        AddButton(18, y, 4011, 4012, 41);
        AddLabel(44, y + 2, 999, "Open Forester's Logbook");
        y += 28;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 6;

        foreach (var key in _extendedWoodKeys)
        {
            int stateHue;
            string stateLabel;

            if (discoveries.TryGetValue(key, out var entry))
            {
                if (entry.State == DiscoveryState.Reported)
                {
                    stateHue   = 0x3F;  // green — reported
                    stateLabel = "Reported";
                }
                else
                {
                    stateHue   = 0x44;  // orange — pending report
                    stateLabel = "Discovered";
                }
            }
            else
            {
                stateHue   = 0x3B2; // grey — not found
                stateLabel = "Not Found";
            }

            AddLabel(18,  y, 999,      key);
            AddLabel(200, y, stateHue, stateLabel);
            y += 20;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string GetRankName(int standing) => standing switch
    {
        >= 150000 => "Legendary Forester",
        >= 80000  => "Master of the Wood",
        >= 40000  => "Grove Warden",
        >= 15000  => "Arborist",
        >= 5000   => "Forester",
        >= 1000   => "Sawyer",
        _         => "Woodcutter"
    };

    // ── Helpers ── replace items ──────────────────────────────────────────────

    private const int LogbookReplaceCost = 5;  // Timber Tokens
    private const int SatchelReplaceCost = 5;  // Timber Tokens

    private void HandleReplaceSatchel(ClusterFAccountData data)
    {
        if (!data.JoinedGuilds.Contains("foresters"))
        {
            _pm.SendMessage(0x22, "You must be a member of the Foresters' Union to replace your satchel.");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        if (pack.FindItemByType<Items.ForestersLumberSatchel>() != null)
        {
            _pm.SendMessage(0x22, "Your Lumber Satchel is already in your backpack.");
            return;
        }

        data.GuildCurrency.TryGetValue("foresters", out var tokens);

        var bypass = Items.DevTestingCrystal.IsActive(_pm);
        if (!bypass && tokens < SatchelReplaceCost)
        {
            _pm.SendMessage(0x22,
                $"Replacing your Lumber Satchel costs {SatchelReplaceCost} Timber Tokens " +
                $"(you have {tokens}).");
            return;
        }

        if (!bypass)
            data.GuildCurrency["foresters"] = tokens - SatchelReplaceCost;

        pack.DropItem(new Items.ForestersLumberSatchel());
        _pm.SendMessage(0x44, "A replacement Foresters' Lumber Satchel has been added to your backpack.");
    }

    private void HandleReplaceLogbook(ClusterFAccountData data)
    {
        if (!data.JoinedGuilds.Contains("foresters"))
        {
            _pm.SendMessage(0x22, "You must be a member of the Foresters' Union to replace your logbook.");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        if (pack.FindItemByType<Items.ForestersLogbook>() != null)
        {
            _pm.SendMessage(0x22, "Your Forester's Logbook is already in your backpack.");
            return;
        }

        data.GuildCurrency.TryGetValue("foresters", out var tokens);

        var bypass = Items.DevTestingCrystal.IsActive(_pm);
        if (!bypass && tokens < LogbookReplaceCost)
        {
            _pm.SendMessage(0x22,
                $"Replacing your Forester's Logbook costs {LogbookReplaceCost} Timber Tokens " +
                $"(you have {tokens}).");
            return;
        }

        if (!bypass)
            data.GuildCurrency["foresters"] = tokens - LogbookReplaceCost;

        pack.DropItem(new Items.ForestersLogbook());
        _pm.SendMessage(0x44, "A replacement Forester's Logbook has been added to your backpack.");
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1)
        {
            _pm.SendGump(new ForestersGuildmasterGump(_pm, View.MainMenu));
            return;
        }

        var acct = _pm.Account as IAccount;

        switch (info.ButtonID)
        {
            case 11: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.AboutForesters)); break;
            case 12: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.WhatWeHarvest));  break;
            case 13: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.JoiningReqs));    break;
            case 14: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.Rewards));        break;

            case 20:
            {
                var def = ClusterFGuildSystem.GetDef("foresters");
                if (def != null && acct != null)
                    _pm.SendGump(new GuildTaskDetailGump(_pm, def, acct));
                break;
            }

            case 30: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.MemberDashboard)); break;
            case 31: _pm.SendGump(new GuildContractLedgerGump(_pm, "foresters"));           break;
            case 32: _pm.SendGump(new ForestersGuildmasterGump(_pm, View.Discoveries));     break;
            case 50: _pm.SendGump(new CrossGuildExchangeGump(_pm));                         break;

            case 33:
            {
                if (acct == null) break;
                var data = ClusterFAccountPersistence.GetOrCreate(acct);
                HandleReplaceLogbook(data);
                _pm.SendGump(new ForestersGuildmasterGump(_pm, View.MemberDashboard));
                break;
            }

            case 34:
            {
                if (acct == null) break;
                var data = ClusterFAccountPersistence.GetOrCreate(acct);
                HandleReplaceSatchel(data);
                _pm.SendGump(new ForestersGuildmasterGump(_pm, View.MemberDashboard));
                break;
            }

            case 40:
            {
                ForestersLogbookGump.SubmitReport(_pm);
                _pm.SendGump(new ForestersGuildmasterGump(_pm, View.Discoveries));
                break;
            }

            case 41:
            {
                if (acct == null) break;
                var data = ClusterFAccountPersistence.GetOrCreate(acct);
                _pm.CloseGump<ForestersLogbookGump>();
                _pm.SendGump(new ForestersLogbookGump(_pm, data));
                break;
            }
        }
    }
}
