using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Gump for the Artificers' Order Guildmaster.
///
/// Views:
///   MainMenu        — topic list; shows Join or Dashboard depending on membership
///   About           — guild identity and purpose
///   WhatWeEnhance   — what imbuing does and what items it works on
///   JoiningReqs     — what is required to join
///   Rewards         — Essence Shards and rank bonuses on join
///   MemberDashboard — standing, rank, shard balance; link to Imbuing Table
/// </summary>
public class ArtificersGuildmasterGump : Gump
{
    public enum View
    {
        MainMenu, About, WhatWeEnhance, JoiningReqs, Rewards, MemberDashboard
    }

    private readonly PlayerMobile _pm;
    private readonly View         _view;

    private const int W    = 460;
    private const int H    = 440;
    private const int BgId = 9270;

    public ArtificersGuildmasterGump(PlayerMobile pm, View view = View.MainMenu) : base(100, 80)
    {
        _pm   = pm;
        _view = view;

        Closable   = true;
        Disposable = true;

        ClusterFLeagueSystem.OnGuildReferralSeen(pm, "artificers");

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 80, 12, 1153, "Artificers' Order");
        AddLabel(W / 2 - 70, 28, 999,  "Thornwick Vesper");    // guildmaster's name
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        switch (view)
        {
            case View.MainMenu:        DrawMainMenu(data);        break;
            case View.About:           DrawAbout();               break;
            case View.WhatWeEnhance:   DrawWhatWeEnhance();       break;
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
        AddLabel(W - 28, H - 26, 1153, "X");
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    private void DrawMainMenu(ClusterFAccountData data)
    {
        var isMember = data.JoinedGuilds.Contains("artificers");

        AddLabel(18, 56, 999, "Greetings. What knowledge do you seek?");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var y = 82;
        AddButton(18, y, 4011, 4012, 11); AddLabel(44, y + 2, 999, "About the Artificers' Order");   y += 28;
        AddButton(18, y, 4011, 4012, 12); AddLabel(44, y + 2, 999, "What We Enhance");               y += 28;
        AddButton(18, y, 4011, 4012, 13); AddLabel(44, y + 2, 999, "Joining Requirements");          y += 28;
        AddButton(18, y, 4011, 4012, 14); AddLabel(44, y + 2, 999, "Rewards and Essence Shards");    y += 28;

        y += 8;
        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        if (isMember)
        {
            AddButton(18, y, 4011, 4012, 30);
            AddLabel(44, y + 2, 999, "Member Dashboard");
            y += 28;
            AddButton(18, y, 4011, 4012, 50);
            AddLabel(44, y + 2, 1153, "Open Imbuing Table");
        }
        else
        {
            AddButton(18, y, 4011, 4012, 20);
            AddLabel(44, y + 2, 999, "Join the Artificers' Order");
            y += 28;
            AddButton(18, y, 4011, 4012, 51);
            AddLabel(44, y + 2, 0x3B2, "Open Imbuing Table (non-member, limited)");
        }
    }

    private void DrawAbout()
    {
        AddLabel(18, 56, 1153, "About the Artificers' Order");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Artificers' Order is Britannia's foremost guild of enchanters " +
            "and item-shapers. We preserve the ancient art of Imbuing — the practice of weaving " +
            "magical properties directly into weapons, armour, and jewellery.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Our craft requires patience, deep knowledge of magical resonance, " +
            "and a steady hand. An unskilled practitioner can shatter a weapon's enchantment or " +
            "worse — fuse conflicting properties into a dangerous combination.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The Order maintains workshops in Ter Mur's Royal City, where " +
            "the gargoyle tradition of essence-work has flourished for centuries. We welcome all " +
            "who would learn, regardless of race or origin.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Guild currency: Essence Shards — gathered from disenchanting " +
            "and imbuing work, spent at the Order's shop for reagents, tools, and enhanced recipes." +
            "</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawWhatWeEnhance()
    {
        AddLabel(18, 56, 1153, "What We Enhance");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var html =
            "<BASEFONT COLOR=#AAAAAA>Imbuing allows you to inscribe magical properties onto " +
            "almost any equippable item. The process requires the Imbuing skill and Essence Shards." +
            "</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Weapons</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Add damage bonuses, hit chance, swing speed, hit spells, " +
            "leeches, and elemental damage modifiers.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Armour &amp; Shields</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Boost resistances, self-repair, lower stat requirements, " +
            "and general defensive attributes.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Jewellery</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Enhance skill bonuses, attribute bonuses, and magical " +
            "properties like lower mana cost.</BASEFONT><BR>" +
            "<BASEFONT COLOR=#CCCCCC>Clothing</BASEFONT>" +
            "<BASEFONT COLOR=#888888> — Apply skill and attribute bonuses to robes, cloaks, " +
            "and other worn pieces.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#FFD700>Order members can imbue already-enchanted items and reach " +
            "property intensities beyond vanilla limits. Higher ranks unlock greater caps." +
            "</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawJoiningReqs()
    {
        AddLabel(18, 56, 1153, "Joining Requirements");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def     = ClusterFGuildSystem.GetDef("artificers");
        var reqText = def?.TaskDescription
            ?? "Speak with the Artificers' Guildmaster to learn the current requirements.";

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Order asks only that you demonstrate some facility " +
            "with the craft before we share our deeper secrets. Even a novice with basic Imbuing " +
            "knowledge is welcome — we will teach you the rest.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#FFD700>{reqText}</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Click 'Join the Artificers' Order' on the main menu " +
            "when you are ready.</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawRewards()
    {
        AddLabel(18, 56, 1153, "Rewards and Essence Shards");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        var def = ClusterFGuildSystem.GetDef("artificers");

        var repLine = def != null
            ? $"<BASEFONT COLOR=#FFD700>+{def.JoinReputation} Artificers' Standing</BASEFONT><BR>" +
              $"<BASEFONT COLOR=#FFD700>+{def.JoinScrip} Essence Shards</BASEFONT><BR><BR>"
            : "";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Upon joining the Artificers' Order you receive:</BASEFONT><BR><BR>" +
            repLine +
            "<BASEFONT COLOR=#AAAAAA>Essence Shards are the Order's trade currency. They are " +
            "spent at the Imbuing Table to add and enhance magical properties on items. " +
            "Earn more Shards by disenchanting unwanted magical items.</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>Artificers' Standing tracks your rank within the Order. " +
            "Higher rank unlocks greater imbuing capabilities:</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Apprentice (0): max 5 props, vanilla intensity caps</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Journeyman (1,000): imbue magic items, +1 prop slot, 120% caps</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Artificer (5,000): +1 prop slot, 150% caps</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Master Artificer (15,000): +1 prop slot, 175% caps</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Arcane Artisan (40,000+): 8 prop slots, 200% caps</BASEFONT>";

        AddHtml(16, 78, W - 32, H - 128, html, false, true);
    }

    private void DrawMemberDashboard(ClusterFAccountData data)
    {
        AddLabel(18, 56, 1153, "Member Dashboard");
        AddImageTiled(10, 72, W - 20, 1, 9304);

        data.GuildReputation.TryGetValue("artificers", out var standing);
        data.GuildCurrency.TryGetValue("artificers",   out var shards);
        var rank    = GetRankName(standing);
        var maxCap  = ArtificersImbueGump.GetIntensityCap(standing);
        var maxSlots= ArtificersImbueGump.GetMaxPropertySlots(standing);

        var html =
            $"<BASEFONT COLOR=#FFD700>Rank: {rank}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Artificers' Standing: {standing:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>Essence Shards: {shards}</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR=#CCCCCC>Your imbuing limits:</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>  Max property slots: {maxSlots}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#AAAAAA>  Max intensity: {maxCap}%</BASEFONT><BR><BR>" +
            "<BASEFONT COLOR=#888888>Apprentice (0) → Journeyman (1,000) → Artificer (5,000)</BASEFONT><BR>" +
            "<BASEFONT COLOR=#888888>Master Artificer (15,000) → Arcane Artisan (40,000)</BASEFONT>";

        AddHtml(16, 78, W - 32, 175, html, false, false);

        AddImageTiled(10, 262, W - 20, 1, 9304);

        AddButton(18, 272, 4011, 4012, 50);
        AddLabel(44, 274, 1153, "Open Imbuing Table");

        AddButton(18, 300, 4011, 4012, 31);
        AddLabel(44, 302, 999, "Guild Work Orders");

        AddImageTiled(10, 328, W - 20, 1, 9304);

        AddButton(18, 338, 4011, 4012, 52);
        AddLabel(44, 340, 1154, "Cross-Guild Exchange (Pack Mule)");
    }

    // ── Rank helpers ──────────────────────────────────────────────────────────

    public static string GetRankName(int standing) => standing switch
    {
        >= 40000 => "Arcane Artisan",
        >= 15000 => "Master Artificer",
        >= 5000  => "Artificer",
        >= 1000  => "Journeyman",
        _        => "Apprentice"
    };

    /// <summary>
    /// Returns the minimum standing required to attempt imbuing a property
    /// based on its discovery threshold (a proxy for power level).
    ///
    ///   Threshold  3  (GoldBase &lt;  500) → Apprentice — 0 standing
    ///   Threshold  5  (GoldBase &lt; 1500) → Journeyman — 1,000 standing
    ///   Threshold  8  (GoldBase &lt; 3000) → Artificer  — 5,000 standing
    ///   Threshold 10  (GoldBase ≥ 3000 + all slayers) → Master Artificer — 15,000 standing
    /// </summary>
    public static int GetMinStanding(int discoveryThreshold) => discoveryThreshold switch
    {
        >= 10 => 15000,
        >= 8  => 5000,
        >= 5  => 1000,
        _     => 0
    };

    /// <summary>
    /// Returns the rank name required for a given discovery threshold.
    /// </summary>
    public static string GetRequiredRankName(int discoveryThreshold) =>
        GetRankName(GetMinStanding(discoveryThreshold));

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1)
        {
            _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.MainMenu));
            return;
        }

        var acct = _pm.Account as IAccount;

        switch (info.ButtonID)
        {
            case 11: _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.About));         break;
            case 12: _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.WhatWeEnhance)); break;
            case 13: _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.JoiningReqs));   break;
            case 14: _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.Rewards));       break;

            case 20:
            {
                var def = ClusterFGuildSystem.GetDef("artificers");
                if (def != null && acct != null)
                    _pm.SendGump(new GuildTaskDetailGump(_pm, def, acct));
                break;
            }

            case 30: _pm.SendGump(new ArtificersGuildmasterGump(_pm, View.MemberDashboard)); break;
            case 31: _pm.SendGump(new ArtificerWorkOrderGump(_pm));                           break;
            case 50: _pm.SendGump(new ArtificersImbueGump(_pm));                             break;
            case 51: _pm.SendGump(new ArtificersImbueGump(_pm));                             break; // non-member access
            case 52: _pm.SendGump(new CrossGuildExchangeGump(_pm));                          break;
        }
    }
}
