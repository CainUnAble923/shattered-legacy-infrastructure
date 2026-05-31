using System;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Dialogue gump for the Miners' Compact Liaison NPC.
///
/// Views:
///   MainMenu        -- topic list; shows Join or Dashboard depending on membership
///   AboutCompact    -- guild history and purpose
///   WhatWeMine      -- ores and resources the Compact works with
///   JoiningReqs     -- what is required to join
///   Rewards         -- reputation and scrip awarded on join
///   MemberDashboard -- standing, rank, voucher balance; links to work orders + restoration
///   WorkOrders      -- current work order (deliver 50 Iron Ingots for 100 standing + 5 vouchers)
///   Restoration     -- restore Jacob's Pickaxe using vouchers, ingots, and gold
///
/// Opening this gump always calls ClusterFLeagueSystem.OnGuildReferralSeen(pm, "mining")
/// so the referral achievement is granted on first visit regardless of which topic is read.
/// </summary>
public class MinersCompactLiaisonGump : Gump
{
    public enum View
    {
        MainMenu, AboutCompact, WhatWeMine, JoiningReqs, Rewards,
        MemberDashboard, Restoration, ReplaceKit, UpgradeSatchel
    }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    // T1 Restoration: ~20% of upgrade cost (upgrade: ~50V + 500 Iron + 10000gp)
    private const int RestoreVoucherCost = 5;
    private const int RestoreIngotCost   = 100;
    private const int RestoreGoldCost    = 2000;

    // T2 Restoration: ~20% of upgrade cost (upgrade: 50V + 1000 Iron + 250 DC + 25000gp)
    private const int RestoreT2VoucherCost    = 10;
    private const int RestoreT2IronCost       = 200;
    private const int RestoreT2DullCopperCost = 40;
    private const int RestoreT2GoldCost       = 5000;

    // T3 Restoration: ~20% of upgrade cost (upgrade: 200V + 1500 Iron + 500 Agapite + 50000gp)
    private const int RestoreT3VoucherCost = 40;
    private const int RestoreT3IronCost    = 300;
    private const int RestoreT3AgapiteCost = 100;
    private const int RestoreT3GoldCost    = 10000;

    // T4 Restoration: ~20% of upgrade cost (upgrade: 350V + 2000 Iron + 500 Valorite + 150000gp)
    private const int RestoreT4VoucherCost  = 70;
    private const int RestoreT4IronCost     = 400;
    private const int RestoreT4ValoriteCost = 100;
    private const int RestoreT4GoldCost     = 30000;

    // T5 Restoration: ~20% of upgrade cost (upgrade: 500V + 3000 Iron + 1000 Valorite + 200 Adamantium + 300000gp)
    private const int RestoreT5VoucherCost    = 100;
    private const int RestoreT5IronCost       = 600;
    private const int RestoreT5ValoriteCost   = 200;
    private const int RestoreT5AdamantiumCost = 40;
    private const int RestoreT5GoldCost       = 60000;

    // Satchel Upgrade costs (T1→T2 through T4→T5)
    // Rank requirements: Apprentice (1k) / Surveyor (15k) / Master Delver (40k) / Deepwarden (80k)
    private const int SatchelT2VoucherCost    = 25;
    private const int SatchelT2IronCost       = 200;
    private const int SatchelT2DullCopperCost = 50;
    private const int SatchelT2GoldCost       = 5000;
    private const int SatchelT2RankReq        = 1000; // Apprentice

    private const int SatchelT3VoucherCost    = 75;
    private const int SatchelT3IronCost       = 500;
    private const int SatchelT3AgapiteCost    = 100;
    private const int SatchelT3GoldCost       = 20000;
    private const int SatchelT3RankReq        = 15000; // Surveyor

    private const int SatchelT4VoucherCost    = 150;
    private const int SatchelT4IronCost       = 1000;
    private const int SatchelT4ValoriteCost   = 200;
    private const int SatchelT4GoldCost       = 50000;
    private const int SatchelT4RankReq        = 40000; // Master Delver

    private const int SatchelT5VoucherCost    = 250;
    private const int SatchelT5IronCost       = 2000;
    private const int SatchelT5AdamantiumCost = 100;
    private const int SatchelT5GoldCost       = 100000;
    private const int SatchelT5RankReq        = 80000; // Deepwarden

    // Satchel Restore cost: 5 vouchers (same as kit item replacement)
    private const int SatchelRestoreCost = 5;

    public MinersCompactLiaisonGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        // Record the referral on every open (idempotent after first time)
        ClusterFLeagueSystem.OnGuildReferralSeen(pm, "mining");

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - 80, 12, 1154, "Miners' Compact");
        AddLabel(W / 2 - 60, 28, 999,  "New Haven Liaison");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        // ── View content ──────────────────────────────────────────────────
        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        switch (view)
        {
            case View.MainMenu:        DrawMainMenu(data);          break;
            case View.AboutCompact:    DrawAboutCompact();          break;
            case View.WhatWeMine:      DrawWhatWeMine();            break;
            case View.JoiningReqs:     DrawJoiningReqs();           break;
            case View.Rewards:         DrawRewards();               break;
            case View.MemberDashboard: DrawMemberDashboard(data);   break;
            case View.Restoration:     DrawRestoration(data, acct);    break;
            case View.ReplaceKit:      DrawReplaceKit();               break;
            case View.UpgradeSatchel:  DrawUpgradeSatchel(data, acct); break;
        }

        // ── Footer ────────────────────────────────────────────────────────
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
        var isMember = data.JoinedGuilds.Contains("mining");

        AddLabel(18, 56, 999, "Aye, what can I do for you?");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var y = 82;
        AddButton(18, y, 4011, 4012, 11); AddLabel(44, y + 2, 999, "About the Compact");    y += 28;
        AddButton(18, y, 4011, 4012, 12); AddLabel(44, y + 2, 999, "What We Mine");         y += 28;
        AddButton(18, y, 4011, 4012, 13); AddLabel(44, y + 2, 999, "Joining Requirements"); y += 28;
        AddButton(18, y, 4011, 4012, 14); AddLabel(44, y + 2, 999, "Rewards and Scrip");    y += 28;

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
            AddLabel(44, y + 2, 999, "Join the Miners' Compact");
        }
    }

    private void DrawAboutCompact()
    {
        AddLabel(18, 56, 1154, "About the Compact");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Miners' Compact is one of the oldest professional " +
            "associations in New Haven, founded by the first settlers who broke stone to build " +
            "these very walls.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>We represent miners, prospectors, and all who work the deep " +
            "veins of Britannia. Members share knowledge, tools, and the fruits of their labor. " +
            "The Compact has no tolerance for claim-jumping or unsafe practices.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The Compact maintains a working relationship with the League " +
            "of Extraordinary Citizens and is among the first guilds to station a liaison at the " +
            "League field office. We believe a well-supplied citizenry is a resilient one.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawWhatWeMine()
    {
        AddLabel(18, 56, 1154, "What We Mine");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Compact works the full range of Britannia's mineral wealth." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Iron Ore</BASEFONT>" +
            "<BASEFONT COLOR=#888888> - The foundation of every forge. Plentiful near New Haven." +
            "</BASEFONT><BR>" +
            "<BASEFONT COLOR=#FF8C00>Dull Copper</BASEFONT>" +
            "<BASEFONT COLOR=#888888> - Tougher than iron. Favoured by weapon smiths." +
            "</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CC4444>Agapite</BASEFONT>" +
            "<BASEFONT COLOR=#888888> - Rare and prized. Deep veins only." +
            "</BASEFONT><BR>" +
            "<BASEFONT COLOR=#4488FF>Valorite</BASEFONT>" +
            "<BASEFONT COLOR=#888888> - The rarest vein. Reserved for master craftsmen." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Veteran Compact members have spoken of deeper ores — veins " +
            "not found on any map — whose existence the Compact neither confirms nor denies. " +
            "A skilled prospector with the right tools may one day find them.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawJoiningReqs()
    {
        AddLabel(18, 56, 1154, "Joining Requirements");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def     = ClusterFGuildSystem.GetDef("mining");
        var reqText = def?.TaskDescription
            ?? "Speak with the Miners' Compact guildmaster at the mine camp to learn the current requirements.";

        var html =
            "<BASEFONT COLOR=#AAAAAA>To join the Miners' Compact, you must show some commitment " +
            "to the craft. We do not turn away beginners, but we expect honest effort." +
            "</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFD700>{reqText}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Click 'Join the Miners' Compact' on the main menu to start " +
            "the enrollment process.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawRewards()
    {
        AddLabel(18, 56, 1154, "Rewards and Scrip");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def = ClusterFGuildSystem.GetDef("mining");

        var repLine = def != null
            ? $"<BASEFONT COLOR=#FFD700>+{def.JoinReputation} Compact Standing</BASEFONT><BR>" +
              $"<BASEFONT COLOR=#FFD700>+{def.JoinScrip} Mining Vouchers</BASEFONT><BR><BR>"
            : "";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Upon joining the Miners' Compact you receive:</BASEFONT><BR><BR>" +
            repLine +
            "<BASEFONT COLOR=#AAAAAA>Mining Vouchers are the guild's internal currency. " +
            "Use them to request restorations and upgrades from guild liaisons." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Compact Standing tracks your rank within the guild. " +
            "Higher standing unlocks advanced guild services and work orders as you advance." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Ranks: Initiate > Apprentice > Journeyman > Surveyor > " +
            "Master Delver > Deepwarden > Legendary Prospector</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawMemberDashboard(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1154, "Member Dashboard");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var rank        = GetRankName(standing);
        var acct        = _pm.Account as IAccount;
        var highestTier = GetHighestUnlockedTier(acct);

        // Fixed-height HTML block (no scrollbar) — UO gumps do not render &gt; entities,
        // so use plain > or the text will appear literally escaped.
        var html =
            $"<BASEFONT COLOR=#FFD700>Rank: {rank}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Compact Standing: {standing:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Mining Vouchers: {vouchers}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Initiate (0) > Apprentice (1,000) > Journeyman (5,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Surveyor (15,000) > Master Delver (40,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Deepwarden (80,000) > Legendary Prospector (150,000)</BASEFONT>";

        // Fixed area: y=78, height=130. Does not extend to the button zone.
        AddHtml(16, 78, W - 32, 130, html, false, false);

        // Buttons anchored below the HTML block with a fixed separator.
        AddImageTiled(10, 216, W - 20, 1, 9304);

        AddButton(18, 226, 4011, 4012, 31);
        AddLabel(44, 228, 999, "Work Orders");

        AddButton(18, 250, 4011, 4012, 32);
        AddLabel(44, 252, 999, "Restoration (Jacob's Pickaxe)");

        // Pickaxe upgrade button — shows next available tier upgrade, or a completion note.
        var (upgradeLabel, upgradeBtn) = GetNextUpgradeButton(acct, highestTier);
        if (upgradeBtn > 0)
        {
            AddButton(18, 274, 4011, 4012, upgradeBtn);
            AddLabel(44, 276, 999, upgradeLabel);
        }
        else
        {
            AddLabel(18, 276, 0x44, upgradeLabel);
        }

        AddButton(18, 298, 4011, 4012, 39);
        AddLabel(44, 300, 999, "Upgrade Ore Satchel");

        AddButton(18, 322, 4011, 4012, 38);
        AddLabel(44, 324, 999, "Replace Starting Kit");

        AddImageTiled(10, 348, W - 20, 1, 9304);

        AddButton(18, 356, 4011, 4012, 100);
        AddLabel(44, 358, 1154, "Cross-Guild Exchange (Pack Mule)");
    }

    /// <summary>
    /// Returns the button label and button ID for the next available upgrade.
    /// Returns (label, 0) when no upgrade is available (completed or no pickaxe).
    /// </summary>
    private static (string label, int btnId) GetNextUpgradeButton(IAccount? acct, int highestTier)
    {
        return highestTier switch
        {
            0 => ("No pickaxe on record.", 0),
            1 => ("Upgrade to Tier 2 (Reinforced Pickaxe)", 33),
            2 => ("Upgrade to Tier 3 (Prospector Pickaxe)", 34),
            3 => ("Upgrade to Tier 4 (Deepdelver Pickaxe)", 35),
            4 => ("Upgrade to Tier 5 (Worldbreaker Pickaxe)", 36),
            _ => ("Tier 5 — Worldbreaker complete.", 0),
        };
    }

    private void DrawReplaceKit()
    {
        AddLabel(18, 56, 1154, "Replace Starting Kit");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var hasSatchel  = _pm.Backpack?.FindItemByType<Items.CompactOreSatchel>()  != null;
        var hasLogbook  = _pm.Backpack?.FindItemByType<Items.ProspectorsLogbook>() != null;
        var hasLedger   = _pm.Backpack?.FindItemByType<Items.CompactDispatchLedger>() != null;

        const int ReplaceCost = 5; // Mining Vouchers per item

        var html =
            "<BASEFONT COLOR=#AAAAAA>Each starting kit item costs " +
            $"{ReplaceCost} Mining Vouchers to replace. Items must be missing from " +
            "your backpack to be replaced — if you have them in a house or container, " +
            "retrieve them instead.</BASEFONT><BR><BR>" +
            (hasSatchel
                ? "<BASEFONT COLOR=#888888>Compact Ore Satchel — in your backpack.</BASEFONT><BR>"
                : $"<BASEFONT COLOR=#FFD700>Compact Ore Satchel — not in backpack ({ReplaceCost}V to replace).</BASEFONT><BR>") +
            (hasLogbook
                ? "<BASEFONT COLOR=#888888>Prospector's Logbook — in your backpack.</BASEFONT><BR>"
                : $"<BASEFONT COLOR=#FFD700>Prospector's Logbook — not in backpack ({ReplaceCost}V to replace).</BASEFONT><BR>") +
            (hasLedger
                ? "<BASEFONT COLOR=#888888>Compact Dispatch Ledger — in your backpack.</BASEFONT>"
                : $"<BASEFONT COLOR=#FFD700>Compact Dispatch Ledger — not in backpack ({ReplaceCost}V to replace).</BASEFONT>");

        AddHtml(16, 78, W - 32, 140, html, false, false);

        AddImageTiled(10, 224, W - 20, 1, 9304);

        var y = 234;

        if (!hasSatchel)
        {
            AddButton(18, y, 4011, 4012, 75);
            AddLabel(44, y + 2, 999, $"Replace Compact Ore Satchel ({ReplaceCost}V)");
            y += 28;
        }
        if (!hasLogbook)
        {
            AddButton(18, y, 4011, 4012, 76);
            AddLabel(44, y + 2, 999, $"Replace Prospector's Logbook ({ReplaceCost}V)");
            y += 28;
        }
        if (!hasLedger)
        {
            AddButton(18, y, 4011, 4012, 77);
            AddLabel(44, y + 2, 999, $"Replace Compact Dispatch Ledger ({ReplaceCost}V)");
        }

        if (hasSatchel && hasLogbook && hasLedger)
            AddLabel(18, y, 68, "All kit items are present in your backpack.");
    }

    private void DrawUpgradeSatchel(ClusterFAccountData data, IAccount? acct)
    {
        AddLabel(18, 56, 1154, "Upgrade Ore Satchel");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var bypass   = Items.DevTestingCrystal.IsActive(_pm);
        var curTier  = GetCurrentSatchelTier(_pm);
        var regTier  = GetHighestUnlockedSatchelTier(acct); // highest tier registered (ever owned)
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        data.GuildReputation.TryGetValue("mining", out var standing);

        // ── Detect "missing" satchel: registered but not in backpack ──────────
        // A higher-tier satchel may be in storage or accidentally deleted.
        bool satchelMissing = regTier > curTier;

        var tierNames = new[] { "None", "Compact", "Reinforced", "Surveyor's", "Deepdelver's", "Master Expedition" };
        var curName   = curTier  > 0 ? tierNames[curTier]  + " Ore Satchel" : "none in backpack";
        var regName   = regTier  > 0 ? tierNames[regTier]  + " Ore Satchel" : "none";

        string html;
        if (satchelMissing)
        {
            html =
                $"<BASEFONT COLOR=#FFD700>Highest tier registered: {regName}</BASEFONT><BR>" +
                $"<BASEFONT COLOR=#FF8888>Not found in your backpack.</BASEFONT><BR><BR>" +
                "<BASEFONT COLOR=#AAAAAA>If your satchel is in a house or container, retrieve it. " +
                $"Otherwise, a replacement costs {SatchelRestoreCost} Mining Vouchers.</BASEFONT><BR><BR>" +
                $"<BASEFONT COLOR=#AAAAAA>Your Mining Vouchers: {vouchers}</BASEFONT>";
        }
        else if (curTier == 0)
        {
            html =
                "<BASEFONT COLOR=#888888>No Compact Ore Satchel found in your backpack.<BR>" +
                "A Compact Ore Satchel is issued when you join the Miners' Compact.<BR><BR>" +
                "Use 'Replace Starting Kit' if yours is missing.</BASEFONT>";
        }
        else if (curTier >= 5)
        {
            html =
                $"<BASEFONT COLOR=#FFD700>Current tier: {curName}</BASEFONT><BR><BR>" +
                "<BASEFONT COLOR=#44AA44>Tier 5 — Master Expedition Satchel. " +
                "You have reached the pinnacle of Compact logistics.</BASEFONT>";
        }
        else
        {
            var (nextName, costLine, rankReq) = GetSatchelUpgradeInfo(curTier);
            var meetsRank = standing >= rankReq;
            var rankName  = GetRankName(rankReq);

            html =
                $"<BASEFONT COLOR=#FFD700>Current tier: {curName}</BASEFONT><BR>" +
                $"<BASEFONT COLOR=#AAAAAA>Next upgrade: {nextName}</BASEFONT><BR><BR>" +
                $"<BASEFONT COLOR=#AAAAAA>Cost: {costLine}</BASEFONT><BR>" +
                (meetsRank
                    ? $"<BASEFONT COLOR=#44AA44>Rank requirement: {rankName} ({rankReq:N0} standing) — met.</BASEFONT><BR>"
                    : $"<BASEFONT COLOR=#FF8888>Rank requirement: {rankName} ({rankReq:N0} standing) — you have {standing:N0}.</BASEFONT><BR>") +
                $"<BASEFONT COLOR=#AAAAAA>Your Mining Vouchers: {vouchers}</BASEFONT>" +
                (bypass ? "<BR><BASEFONT COLOR=#FF44FF>[Testing Token active — costs bypassed]</BASEFONT>" : "");
        }

        AddHtml(16, 78, W - 32, H - 148, html, false, true);

        var btnY = H - 68;

        if (satchelMissing)
        {
            // Offer recovery of the highest registered tier
            AddButton(18, btnY, 4011, 4012, 90);
            AddLabel(44, btnY + 2, bypass || vouchers >= SatchelRestoreCost ? 999 : 0x22,
                $"Recover {regName} ({SatchelRestoreCost}V)");
        }
        else if (curTier > 0 && curTier < 5)
        {
            var (_, _, rankReq) = GetSatchelUpgradeInfo(curTier);
            var canUpgrade = bypass
                || (standing >= rankReq && CanAffordSatchelUpgrade(curTier, vouchers));

            AddButton(18, btnY, 4011, 4012, 91);
            AddLabel(44, btnY + 2, canUpgrade ? 999 : 0x22,
                $"Upgrade to Tier {curTier + 1}");
        }
    }

    // ── Satchel tier helpers ──────────────────────────────────────────────────

    /// <summary>Returns the highest ore satchel tier the player currently has in their backpack.</summary>
    private static int GetCurrentSatchelTier(PlayerMobile pm)
    {
        var pack = pm.Backpack;
        if (pack == null) return 0;
        // Check highest tier first; each is a distinct class (all derive from CompactOreSatchel)
        if (pack.FindItemByType<Items.MasterExpeditionSatchel>() != null) return 5;
        if (pack.FindItemByType<Items.DeepdelversSatchel>()      != null) return 4;
        if (pack.FindItemByType<Items.SurveyorsSatchel>()        != null) return 3;
        if (pack.FindItemByType<Items.ReinforcedOreSatchel>()    != null) return 2;
        // T1: must be CompactOreSatchel exactly (not a subclass)
        if (pack.Items.Find(i => i.GetType() == typeof(Items.CompactOreSatchel)) != null) return 1;
        return 0;
    }

    /// <summary>Returns the highest tier satchel ever registered for this account (0 = only T1 issued on join).</summary>
    private static int GetHighestUnlockedSatchelTier(IAccount? acct)
    {
        if (acct == null) return 0;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "compact.satchel_t5")) return 5;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "compact.satchel_t4")) return 4;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "compact.satchel_t3")) return 3;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "compact.satchel_t2")) return 2;
        return 1; // T1 always available (issued on join, no registry key)
    }

    /// <summary>Returns upgrade info for the tier after <paramref name="curTier"/>.</summary>
    private static (string nextName, string costLine, int rankReq) GetSatchelUpgradeInfo(int curTier) =>
        curTier switch
        {
            1 => ("Reinforced Ore Satchel",
                  $"{SatchelT2VoucherCost}V + {SatchelT2IronCost} Iron + {SatchelT2DullCopperCost} Dull Copper Ingots + {SatchelT2GoldCost:N0}gp",
                  SatchelT2RankReq),
            2 => ("Surveyor's Ore Satchel",
                  $"{SatchelT3VoucherCost}V + {SatchelT3IronCost} Iron + {SatchelT3AgapiteCost} Agapite Ingots + {SatchelT3GoldCost:N0}gp",
                  SatchelT3RankReq),
            3 => ("Deepdelver's Ore Satchel",
                  $"{SatchelT4VoucherCost}V + {SatchelT4IronCost} Iron + {SatchelT4ValoriteCost} Valorite Ingots + {SatchelT4GoldCost:N0}gp",
                  SatchelT4RankReq),
            _ => ("Master Expedition Satchel",
                  $"{SatchelT5VoucherCost}V + {SatchelT5IronCost} Iron + {SatchelT5AdamantiumCost} Adamantium Ingots + {SatchelT5GoldCost:N0}gp",
                  SatchelT5RankReq),
        };

    private bool CanAffordSatchelUpgrade(int curTier, int vouchers) =>
        curTier switch
        {
            1 => vouchers >= SatchelT2VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))       ?? 0) >= SatchelT2IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.DullCopperIngot)) ?? 0) >= SatchelT2DullCopperCost
              && CompactGoldHelper.GetTotalGold(_pm) >= SatchelT2GoldCost,
            2 => vouchers >= SatchelT3VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))    ?? 0) >= SatchelT3IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.AgapiteIngot)) ?? 0) >= SatchelT3AgapiteCost
              && CompactGoldHelper.GetTotalGold(_pm) >= SatchelT3GoldCost,
            3 => vouchers >= SatchelT4VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))     ?? 0) >= SatchelT4IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.ValoriteIngot)) ?? 0) >= SatchelT4ValoriteCost
              && CompactGoldHelper.GetTotalGold(_pm) >= SatchelT4GoldCost,
            _ => vouchers >= SatchelT5VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))       ?? 0) >= SatchelT5IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.AdamantiumIngot)) ?? 0) >= SatchelT5AdamantiumCost
              && CompactGoldHelper.GetTotalGold(_pm) >= SatchelT5GoldCost,
        };

    private void DrawRestoration(ClusterFAccountData data, IAccount? acct)
    {
        AddLabel(18, 56, 1154, "Restoration");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        if (acct == null)
        {
            AddHtml(16, 78, W - 32, H - 128,
                "<BASEFONT COLOR=#FF6666>Account data unavailable.</BASEFONT>", false, false);
            return;
        }

        var bypass      = Items.DevTestingCrystal.IsActive(_pm);
        var highestTier = GetHighestUnlockedTier(acct);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);

        if (highestTier == 0)
        {
            AddHtml(16, 78, W - 32, H - 128,
                "<BASEFONT COLOR=#888888>You have no Jacob's Pickaxe on record.<BR>" +
                "Obtain one from quest givers in New Haven to begin your lineage.</BASEFONT>",
                false, false);
            return;
        }

        // Show status for the highest tier only.
        // Lower tiers were consumed in upgrades and cannot be independently restored.
        var (regKey, pickaxeName, restoreCost, restoreBtn) = GetTierRestoreInfo(highestTier);

        // Stale active-copy sanity check: verify the item is still in the player's possession.
        if (ClusterFRestorationRegistry.HasActiveCopy(acct, regKey))
        {
            if (!PlayerHasActiveTierItem(highestTier))
                ClusterFRestorationRegistry.ClearActiveCopy(acct, regKey);
        }

        var isActive   = ClusterFRestorationRegistry.HasActiveCopy(acct, regKey);
        var canRestore = !isActive && (bypass || CanAffordRestore(highestTier, vouchers));

        string statusHtml;
        if (isActive)
            statusHtml = $"<BASEFONT COLOR=#FFD700>{pickaxeName}: Active copy on hand.</BASEFONT>";
        else
            statusHtml = $"<BASEFONT COLOR=#FFD700>{pickaxeName}: Ready for restoration.</BASEFONT><BR>" +
                         $"<BASEFONT COLOR=#AAAAAA>{restoreCost}</BASEFONT>";

        var bypassLine  = bypass ? "<BR><BASEFONT COLOR=#FF44FF>[Testing Token active — all material costs bypassed]</BASEFONT>" : "";
        var balanceLine = $"<BR><BR><BASEFONT COLOR=#AAAAAA>Your Mining Vouchers: {vouchers}</BASEFONT>" + bypassLine;

        AddHtml(16, 78, W - 32, H - 128, statusHtml + balanceLine, false, true);

        if (!isActive)
        {
            AddButton(18, H - 68, 4011, 4012, restoreBtn);
            AddLabel(44, H - 66, canRestore ? 999 : 0x22, $"Restore {pickaxeName}");
        }
    }

    // ── Restoration tier helpers ──────────────────────────────────────────────

    /// <summary>Returns the highest Jacob's Pickaxe tier the player has unlocked (0 = none).</summary>
    private static int GetHighestUnlockedTier(IAccount? acct)
    {
        if (acct == null) return 0;
        var tier = 0;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_pickaxe"))              tier = 1;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_reinforced_pickaxe"))   tier = 2;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_prospector_pickaxe"))   tier = 3;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_deepdelver_pickaxe"))   tier = 4;
        if (ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_worldbreaker_pickaxe")) tier = 5;
        return tier;
    }

    private static (string regKey, string name, string cost, int btnId) GetTierRestoreInfo(int tier) =>
        tier switch
        {
            1 => ("legacy.jacobs_pickaxe",
                  "Jacob's Pickaxe",
                  $"{RestoreVoucherCost}V + {RestoreIngotCost} Iron + {RestoreGoldCost:N0}gp",
                  60),
            2 => ("legacy.jacobs_reinforced_pickaxe",
                  "Jacob's Reinforced Pickaxe",
                  $"{RestoreT2VoucherCost}V + {RestoreT2IronCost} Iron + {RestoreT2DullCopperCost} DC + {RestoreT2GoldCost:N0}gp",
                  61),
            3 => ("legacy.jacobs_prospector_pickaxe",
                  "Jacob's Prospector Pickaxe",
                  $"{RestoreT3VoucherCost}V + {RestoreT3IronCost} Iron + {RestoreT3AgapiteCost} Agapite + {RestoreT3GoldCost:N0}gp",
                  62),
            4 => ("legacy.jacobs_deepdelver_pickaxe",
                  "Jacob's Deepdelver Pickaxe",
                  $"{RestoreT4VoucherCost}V + {RestoreT4IronCost} Iron + {RestoreT4ValoriteCost} Valorite + {RestoreT4GoldCost:N0}gp",
                  63),
            _ => ("legacy.jacobs_worldbreaker_pickaxe",
                  "Jacob's Worldbreaker Pickaxe",
                  $"{RestoreT5VoucherCost}V + {RestoreT5IronCost} Iron + {RestoreT5ValoriteCost} Valorite + {RestoreT5AdamantiumCost} Adamantium + {RestoreT5GoldCost:N0}gp",
                  64),
        };

    private bool PlayerHasActiveTierItem(int tier)
    {
        Item? equipped = _pm.FindItemOnLayer(Layer.TwoHanded);
        var pack = _pm.Backpack;
        return tier switch
        {
            1 => (equipped is Items.JacobsPickaxe jp              && !jp.Exhausted)
              || (pack?.FindItemByType<Items.JacobsPickaxe>()             is Items.JacobsPickaxe            p1 && !p1.Exhausted),
            2 => (equipped is Items.JacobsReinforcedPickaxe jrp   && !jrp.Exhausted)
              || (pack?.FindItemByType<Items.JacobsReinforcedPickaxe>()   is Items.JacobsReinforcedPickaxe  p2 && !p2.Exhausted),
            3 => (equipped is Items.JacobsProspectorPickaxe jpp   && !jpp.Exhausted)
              || (pack?.FindItemByType<Items.JacobsProspectorPickaxe>()   is Items.JacobsProspectorPickaxe  p3 && !p3.Exhausted),
            4 => (equipped is Items.JacobsDeepdelverPickaxe jdp   && !jdp.Exhausted)
              || (pack?.FindItemByType<Items.JacobsDeepdelverPickaxe>()   is Items.JacobsDeepdelverPickaxe  p4 && !p4.Exhausted),
            _ => (equipped is Items.JacobsWorldbreakerPickaxe jwp && !jwp.Exhausted)
              || (pack?.FindItemByType<Items.JacobsWorldbreakerPickaxe>() is Items.JacobsWorldbreakerPickaxe p5 && !p5.Exhausted),
        };
    }

    private bool CanAffordRestore(int tier, int vouchers) =>
        tier switch
        {
            1 => vouchers >= RestoreVoucherCost,
            2 => vouchers >= RestoreT2VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))       ?? 0) >= RestoreT2IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.DullCopperIngot)) ?? 0) >= RestoreT2DullCopperCost
              && CompactGoldHelper.GetTotalGold(_pm)                           >= RestoreT2GoldCost,
            3 => vouchers >= RestoreT3VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))    ?? 0) >= RestoreT3IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.AgapiteIngot)) ?? 0) >= RestoreT3AgapiteCost
              && CompactGoldHelper.GetTotalGold(_pm)                        >= RestoreT3GoldCost,
            4 => vouchers >= RestoreT4VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))     ?? 0) >= RestoreT4IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.ValoriteIngot)) ?? 0) >= RestoreT4ValoriteCost
              && CompactGoldHelper.GetTotalGold(_pm)                         >= RestoreT4GoldCost,
            _ => vouchers >= RestoreT5VoucherCost
              && (_pm.Backpack?.GetAmount(typeof(Items.IronIngot))       ?? 0) >= RestoreT5IronCost
              && (_pm.Backpack?.GetAmount(typeof(Items.ValoriteIngot))   ?? 0) >= RestoreT5ValoriteCost
              && (_pm.Backpack?.GetAmount(typeof(Items.AdamantiumIngot)) ?? 0) >= RestoreT5AdamantiumCost
              && CompactGoldHelper.GetTotalGold(_pm)                           >= RestoreT5GoldCost,
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string GetRankName(int standing) => standing switch
    {
        >= 150000 => "Legendary Prospector",
        >= 80000  => "Deepwarden",
        >= 40000  => "Master Delver",
        >= 15000  => "Surveyor",
        >= 5000   => "Journeyman",
        >= 1000   => "Apprentice",
        _         => "Initiate"
    };

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return; // Close

        if (info.ButtonID == 1) // Back
        {
            var backView = _view is View.Restoration or View.ReplaceKit or View.UpgradeSatchel
                ? View.MemberDashboard
                : View.MainMenu;
            _pm.SendGump(new MinersCompactLiaisonGump(_pm, backView));
            return;
        }

        var acct = _pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        switch (info.ButtonID)
        {
            // ── Topic navigation ──────────────────────────────────────────
            case 11: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.AboutCompact));    break;
            case 12: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.WhatWeMine));      break;
            case 13: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.JoiningReqs));     break;
            case 14: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Rewards));         break;

            // ── Join flow ─────────────────────────────────────────────────
            case 20:
            {
                var def = ClusterFGuildSystem.GetDef("mining");
                if (def != null && acct != null)
                    _pm.SendGump(new GuildTaskDetailGump(_pm, def, acct));
                break;
            }

            // ── Member navigation ─────────────────────────────────────────
            case 30: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.MemberDashboard));         break;
            case 31: _pm.SendGump(new GuildContractLedgerGump(_pm, "mining"));                      break;
            case 32: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));             break;
            case 33: _pm.SendGump(new JacobsUpgradeGump(_pm));                                     break;
            case 34: _pm.SendGump(new JacobsT3UpgradeGump(_pm));                                   break;
            case 35: _pm.SendGump(new JacobsT4UpgradeGump(_pm));                                   break;
            case 36: _pm.SendGump(new JacobsT5UpgradeGump(_pm));                                   break;
            case 38: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.ReplaceKit));              break;
            case 39: _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.UpgradeSatchel));         break;
            case 100: _pm.SendGump(new CrossGuildExchangeGump(_pm));                               break;

            // ── Satchel upgrade / recovery ────────────────────────────────
            case 90:
                HandleSatchelRecovery(data, acct);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.UpgradeSatchel));
                break;
            case 91:
                HandleSatchelUpgrade(data, acct);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.UpgradeSatchel));
                break;

            // ── Replace kit items ─────────────────────────────────────────
            case 75:
                HandleReplaceKitItem<Items.CompactOreSatchel>(data, "Compact Ore Satchel",
                    () => new Items.CompactOreSatchel());
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.ReplaceKit));
                break;
            case 76:
                HandleReplaceKitItem<Items.ProspectorsLogbook>(data, "Prospector's Logbook",
                    () => new Items.ProspectorsLogbook());
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.ReplaceKit));
                break;
            case 77:
                HandleReplaceKitItem<Items.CompactDispatchLedger>(data, "Compact Dispatch Ledger",
                    () => new Items.CompactDispatchLedger());
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.ReplaceKit));
                break;

            // ── Restoration requests ──────────────────────────────────────
            case 60:
                HandleRestorationRequest(data, acct);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));
                break;
            case 61:
                HandleT2RestorationRequest(data, acct);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));
                break;
            case 62:
                HandleTierRestorationRequest(data, acct, 3);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));
                break;
            case 63:
                HandleTierRestorationRequest(data, acct, 4);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));
                break;
            case 64:
                HandleTierRestorationRequest(data, acct, 5);
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, View.Restoration));
                break;
        }
    }

    /// <summary>
    /// Handles restoration for T3, T4, or T5 pickaxe tiers.
    /// Consumes the appropriate materials, removes any exhausted copy, and delivers a fresh one.
    /// </summary>
    private void HandleTierRestorationRequest(ClusterFAccountData? data, IAccount? acct, int tier)
    {
        if (data == null || acct == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a Miners' Compact member to request a restoration.");
            return;
        }

        var (regKey, name, _, _) = GetTierRestoreInfo(tier);

        if (!ClusterFRestorationRegistry.HasUnlocked(acct, regKey))
        {
            _pm.SendMessage(0x22, $"You have no record of ever owning {name}.");
            return;
        }

        if (ClusterFRestorationRegistry.HasActiveCopy(acct, regKey))
        {
            _pm.SendMessage(0x22, $"You already have an active copy of {name}.");
            return;
        }

        var bypass = Items.DevTestingCrystal.IsActive(_pm);
        var pack   = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        data.GuildCurrency.TryGetValue("mining", out var vouchers);

        // Validate costs (skipped in bypass mode)
        if (!bypass)
        {
            var (vCost, ironCost, matCost, matType, mat2Cost, mat2Type, goldCost) = GetTierRestoreCosts(tier);

            if (vouchers < vCost)
            {
                _pm.SendMessage(0x22, $"You need {vCost} Mining Vouchers (you have {vouchers}).");
                return;
            }
            if (pack.GetAmount(typeof(Items.IronIngot)) < ironCost)
            {
                _pm.SendMessage(0x22, $"You need {ironCost} iron ingots.");
                return;
            }
            if (matType != null && pack.GetAmount(matType) < matCost)
            {
                _pm.SendMessage(0x22, $"You need {matCost} {matType.Name.Replace("Ingot", " Ingots")}.");
                return;
            }
            if (mat2Type != null && pack.GetAmount(mat2Type) < mat2Cost)
            {
                _pm.SendMessage(0x22, $"You need {mat2Cost} {mat2Type.Name.Replace("Ingot", " Ingots")}.");
                return;
            }
            if (CompactGoldHelper.GetTotalGold(_pm) < goldCost)
            {
                _pm.SendMessage(0x22, $"You need {goldCost:N0} gold in your backpack or bank (you have {CompactGoldHelper.GetTotalGold(_pm):N0}).");
                return;
            }

            // Consume costs
            data.GuildCurrency["mining"] = vouchers - vCost;
            pack.ConsumeTotal(typeof(Items.IronIngot), ironCost);
            if (matType != null) pack.ConsumeTotal(matType, matCost);
            if (mat2Type != null) pack.ConsumeTotal(mat2Type, mat2Cost);
            CompactGoldHelper.ConsumeGold(_pm, goldCost);
        }

        // Remove exhausted copy if present
        RemoveExhaustedTierItem(pack, tier);

        // Deliver fresh copy
        ClusterFRestorationRegistry.TryRestore(acct, regKey, out _);
        var newPickaxe = CreateTierPickaxe(tier);
        pack.DropItem(newPickaxe);

        _pm.SendMessage(0x44, $"{name} has been restored. The Compact acknowledges your commitment.");
        _pm.PlaySound(0x35D);
    }

    private static (int vCost, int ironCost, int matCost, Type? matType, int mat2Cost, Type? mat2Type, int goldCost)
        GetTierRestoreCosts(int tier) =>
        tier switch
        {
            3 => (RestoreT3VoucherCost, RestoreT3IronCost, RestoreT3AgapiteCost, typeof(Items.AgapiteIngot),    0, null,                         RestoreT3GoldCost),
            4 => (RestoreT4VoucherCost, RestoreT4IronCost, RestoreT4ValoriteCost, typeof(Items.ValoriteIngot),  0, null,                         RestoreT4GoldCost),
            _ => (RestoreT5VoucherCost, RestoreT5IronCost, RestoreT5ValoriteCost, typeof(Items.ValoriteIngot),  RestoreT5AdamantiumCost, typeof(Items.AdamantiumIngot), RestoreT5GoldCost),
        };

    private static void RemoveExhaustedTierItem(Container pack, int tier)
    {
        Item? exhausted = tier switch
        {
            3 => pack.FindItemByType<Items.JacobsProspectorPickaxe>()  is { Exhausted: true } e3 ? e3 : null,
            4 => pack.FindItemByType<Items.JacobsDeepdelverPickaxe>()  is { Exhausted: true } e4 ? e4 : null,
            5 => pack.FindItemByType<Items.JacobsWorldbreakerPickaxe>() is { Exhausted: true } e5 ? e5 : null,
            _ => null,
        };
        exhausted?.Delete();
    }

    private static Item CreateTierPickaxe(int tier) =>
        tier switch
        {
            3 => new Items.JacobsProspectorPickaxe(),
            4 => new Items.JacobsDeepdelverPickaxe(),
            _ => new Items.JacobsWorldbreakerPickaxe(),
        };

    // ── Kit replacement ───────────────────────────────────────────────────────

    private const int KitReplaceCost = 5; // Mining Vouchers per item

    /// <summary>
    /// Replaces a single starting kit item (satchel, logbook, or dispatch ledger)
    /// for KitReplaceCost Mining Vouchers, provided the item is not already in the
    /// player's backpack.  Blessed items don't drop on death, but players may have
    /// deleted them, left them in a house, or traded them away.
    /// </summary>
    private void HandleReplaceKitItem<T>(ClusterFAccountData? data, string itemName,
        Func<Item> factory) where T : Item
    {
        if (data == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a member of the Miners' Compact to replace kit items.");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        if (pack.FindItemByType<T>() != null)
        {
            _pm.SendMessage(0x22, $"You already have a {itemName} in your backpack.");
            return;
        }

        data.GuildCurrency.TryGetValue("mining", out var vouchers);

        var bypass = Items.DevTestingCrystal.IsActive(_pm);
        if (!bypass && vouchers < KitReplaceCost)
        {
            _pm.SendMessage(0x22,
                $"Replacing a {itemName} costs {KitReplaceCost} Mining Vouchers " +
                $"(you have {vouchers}).");
            return;
        }

        if (!bypass)
            data.GuildCurrency["mining"] = vouchers - KitReplaceCost;

        pack.DropItem(factory());
        _pm.SendMessage(0x44, $"A replacement {itemName} has been added to your backpack.");
    }

    // ── Satchel upgrade ───────────────────────────────────────────────────────

    private void HandleSatchelUpgrade(ClusterFAccountData? data, IAccount? acct)
    {
        if (data == null || acct == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a Miners' Compact member to upgrade your satchel.");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        var curTier = GetCurrentSatchelTier(_pm);
        if (curTier == 0)
        {
            _pm.SendMessage(0x22, "No Compact Ore Satchel found in your backpack.");
            return;
        }
        if (curTier >= 5)
        {
            _pm.SendMessage(0x22, "Your satchel is already at the maximum tier.");
            return;
        }

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);

        var (nextName, _, rankReq) = GetSatchelUpgradeInfo(curTier);
        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        if (!bypass)
        {
            if (standing < rankReq)
            {
                _pm.SendMessage(0x22,
                    $"Upgrading to a {nextName} requires {GetRankName(rankReq)} rank " +
                    $"({rankReq:N0} Compact Standing; you have {standing:N0}).");
                return;
            }

            if (!CanAffordSatchelUpgrade(curTier, vouchers))
            {
                var (_, costLine, _) = GetSatchelUpgradeInfo(curTier);
                _pm.SendMessage(0x22, $"You cannot afford the upgrade. Cost: {costLine}.");
                return;
            }

            // Consume costs
            switch (curTier)
            {
                case 1:
                    data.GuildCurrency["mining"] = vouchers - SatchelT2VoucherCost;
                    pack.ConsumeTotal(typeof(Items.IronIngot),       SatchelT2IronCost);
                    pack.ConsumeTotal(typeof(Items.DullCopperIngot), SatchelT2DullCopperCost);
                    CompactGoldHelper.ConsumeGold(_pm, SatchelT2GoldCost);
                    break;
                case 2:
                    data.GuildCurrency["mining"] = vouchers - SatchelT3VoucherCost;
                    pack.ConsumeTotal(typeof(Items.IronIngot),    SatchelT3IronCost);
                    pack.ConsumeTotal(typeof(Items.AgapiteIngot), SatchelT3AgapiteCost);
                    CompactGoldHelper.ConsumeGold(_pm, SatchelT3GoldCost);
                    break;
                case 3:
                    data.GuildCurrency["mining"] = vouchers - SatchelT4VoucherCost;
                    pack.ConsumeTotal(typeof(Items.IronIngot),     SatchelT4IronCost);
                    pack.ConsumeTotal(typeof(Items.ValoriteIngot), SatchelT4ValoriteCost);
                    CompactGoldHelper.ConsumeGold(_pm, SatchelT4GoldCost);
                    break;
                case 4:
                    data.GuildCurrency["mining"] = vouchers - SatchelT5VoucherCost;
                    pack.ConsumeTotal(typeof(Items.IronIngot),       SatchelT5IronCost);
                    pack.ConsumeTotal(typeof(Items.AdamantiumIngot), SatchelT5AdamantiumCost);
                    CompactGoldHelper.ConsumeGold(_pm, SatchelT5GoldCost);
                    break;
            }
        }

        // Find the current satchel, move its contents, then destroy and replace it.
        Items.CompactOreSatchel? oldSatchel = curTier switch
        {
            1 => pack.Items.Find(i => i.GetType() == typeof(Items.CompactOreSatchel)) as Items.CompactOreSatchel,
            2 => pack.FindItemByType<Items.ReinforcedOreSatchel>(),
            3 => pack.FindItemByType<Items.SurveyorsSatchel>(),
            4 => pack.FindItemByType<Items.DeepdelversSatchel>(),
            _ => null,
        };

        var newSatchel = CreateUpgradedSatchel(curTier + 1);

        // Transfer all contents from old to new satchel before deleting old.
        if (oldSatchel != null)
        {
            // Move items from old satchel — iterate backwards to avoid index shifting.
            for (var i = oldSatchel.Items.Count - 1; i >= 0; i--)
            {
                var content = oldSatchel.Items[i];
                if (newSatchel.CheckHold(_pm, content, false, false, 0, 0))
                    newSatchel.DropItem(content);
                else
                    pack.DropItem(content); // overflow to backpack if somehow over new cap
            }
            oldSatchel.Delete();
        }

        // Register ownership of the new tier (idempotent; fine if already present).
        var regKey = $"compact.satchel_t{curTier + 1}";
        ClusterFRestorationRegistry.Unlock(acct, regKey, "upgrade");
        pack.DropItem(newSatchel);

        _pm.SendMessage(0x44,
            $"Satchel upgraded to Tier {curTier + 1}: {nextName}. " +
            "All contents have been transferred.");
        _pm.PlaySound(0x35D);
    }

    /// <summary>
    /// Recovers a higher-tier satchel that has been registered but is not in the player's backpack.
    /// Costs SatchelRestoreCost vouchers.
    /// </summary>
    private void HandleSatchelRecovery(ClusterFAccountData? data, IAccount? acct)
    {
        if (data == null || acct == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a Miners' Compact member to recover a satchel.");
            return;
        }

        var curTier = GetCurrentSatchelTier(_pm);
        var regTier = GetHighestUnlockedSatchelTier(acct);

        if (regTier <= curTier)
        {
            _pm.SendMessage(0x22, "Your satchel is accounted for — no recovery needed.");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null) { _pm.SendMessage(0x22, "You don't have a backpack."); return; }

        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        if (!bypass && vouchers < SatchelRestoreCost)
        {
            _pm.SendMessage(0x22,
                $"Recovering a satchel costs {SatchelRestoreCost} Mining Vouchers " +
                $"(you have {vouchers}).");
            return;
        }

        if (!bypass)
            data.GuildCurrency["mining"] = vouchers - SatchelRestoreCost;

        var recovered = CreateUpgradedSatchel(regTier);
        pack.DropItem(recovered);

        var tierNames = new[] { "", "Compact", "Reinforced", "Surveyor's", "Deepdelver's", "Master Expedition" };
        _pm.SendMessage(0x44,
            $"Your {tierNames[regTier]} Ore Satchel has been recovered and added to your backpack.");
    }

    private static Items.CompactOreSatchel CreateUpgradedSatchel(int tier) =>
        tier switch
        {
            2 => new Items.ReinforcedOreSatchel(),
            3 => new Items.SurveyorsSatchel(),
            4 => new Items.DeepdelversSatchel(),
            5 => new Items.MasterExpeditionSatchel(),
            _ => new Items.CompactOreSatchel(),
        };

    private void HandleRestorationRequest(ClusterFAccountData? data, IAccount? acct)
    {
        if (data == null || acct == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a member of the Miners' Compact to request a restoration.");
            return;
        }

        if (!ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_pickaxe"))
        {
            _pm.SendMessage(0x22, "You have no record of ever owning Jacob's Pickaxe.");
            return;
        }

        if (ClusterFRestorationRegistry.HasActiveCopy(acct, "legacy.jacobs_pickaxe"))
        {
            _pm.SendMessage(0x22, "You already have an active copy of Jacob's Pickaxe.");
            return;
        }

        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        if (!bypass && vouchers < RestoreVoucherCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreVoucherCost} Mining Vouchers (you have {vouchers}).");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null)
        {
            _pm.SendMessage(0x22, "You don't have a backpack.");
            return;
        }

        if (!bypass && pack.GetAmount(typeof(IronIngot)) < RestoreIngotCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreIngotCost} iron ingots (you have {pack.GetAmount(typeof(IronIngot))}).");
            return;
        }

        if (!bypass && CompactGoldHelper.GetTotalGold(_pm) < RestoreGoldCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreGoldCost:N0} gold in your backpack or bank (you have {CompactGoldHelper.GetTotalGold(_pm):N0}).");
            return;
        }

        // All checks pass — consume costs (skipped when testing token is active)
        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - RestoreVoucherCost;
            pack.ConsumeTotal(typeof(IronIngot), RestoreIngotCost);
            CompactGoldHelper.ConsumeGold(_pm, RestoreGoldCost);
        }

        // Remove the exhausted pickaxe from the pack before delivering the fresh one
        var exhaustedT1 = pack.FindItemByType<JacobsPickaxe>();
        if (exhaustedT1 is { Exhausted: true })
            exhaustedT1.Delete();

        // Register active copy and deliver item
        ClusterFRestorationRegistry.TryRestore(acct, "legacy.jacobs_pickaxe", out _);
        var pickaxe = new JacobsPickaxe();
        pack.DropItem(pickaxe);

        _pm.SendMessage(0x44, "Jacob's Pickaxe has been restored. Handle it with care — it can be exhausted but never lost.");
        _pm.PlaySound(0x35D); // Forge/craft sound
    }

    private void HandleT2RestorationRequest(ClusterFAccountData? data, IAccount? acct)
    {
        if (data == null || acct == null) return;

        if (!data.JoinedGuilds.Contains("mining"))
        {
            _pm.SendMessage(0x22, "You must be a member of the Miners' Compact to request a restoration.");
            return;
        }

        if (!ClusterFRestorationRegistry.HasUnlocked(acct, "legacy.jacobs_reinforced_pickaxe"))
        {
            _pm.SendMessage(0x22, "You have no record of ever owning Jacob's Reinforced Pickaxe.");
            return;
        }

        if (ClusterFRestorationRegistry.HasActiveCopy(acct, "legacy.jacobs_reinforced_pickaxe"))
        {
            _pm.SendMessage(0x22, "You already have an active copy of Jacob's Reinforced Pickaxe.");
            return;
        }

        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        if (!bypass && vouchers < RestoreT2VoucherCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreT2VoucherCost} Mining Vouchers (you have {vouchers}).");
            return;
        }

        var pack = _pm.Backpack;
        if (pack == null)
        {
            _pm.SendMessage(0x22, "You don't have a backpack.");
            return;
        }

        if (!bypass && pack.GetAmount(typeof(Items.IronIngot)) < RestoreT2IronCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreT2IronCost} iron ingots (you have {pack.GetAmount(typeof(Items.IronIngot))}).");
            return;
        }

        if (!bypass && pack.GetAmount(typeof(Items.DullCopperIngot)) < RestoreT2DullCopperCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreT2DullCopperCost} Dull Copper Ingots (you have {pack.GetAmount(typeof(Items.DullCopperIngot))}).");
            return;
        }

        if (!bypass && CompactGoldHelper.GetTotalGold(_pm) < RestoreT2GoldCost)
        {
            _pm.SendMessage(0x22, $"You need {RestoreT2GoldCost:N0} gold in your backpack or bank (you have {CompactGoldHelper.GetTotalGold(_pm):N0}).");
            return;
        }

        // All checks pass — consume costs (skipped when testing token is active)
        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - RestoreT2VoucherCost;
            pack.ConsumeTotal(typeof(Items.IronIngot),       RestoreT2IronCost);
            pack.ConsumeTotal(typeof(Items.DullCopperIngot), RestoreT2DullCopperCost);
            CompactGoldHelper.ConsumeGold(_pm, RestoreT2GoldCost);
        }

        // Remove the exhausted T2 pickaxe from the pack before delivering the fresh one
        var exhaustedT2 = pack.FindItemByType<Items.JacobsReinforcedPickaxe>();
        if (exhaustedT2 is { Exhausted: true })
            exhaustedT2.Delete();

        // Register active copy and deliver item
        ClusterFRestorationRegistry.TryRestore(acct, "legacy.jacobs_reinforced_pickaxe", out _);
        var pickaxe = new Items.JacobsReinforcedPickaxe();
        pack.DropItem(pickaxe);

        _pm.SendMessage(0x44,
            "Jacob's Reinforced Pickaxe has been restored. The Compact acknowledges your continued dedication.");
        _pm.PlaySound(0x35D);
    }
}
