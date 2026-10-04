using System.Collections.Generic;
using Server.Accounting;
using Server.Custom;
using Server.Engines.MLQuests;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server;

/// <summary>
/// Developer reset and character management tools for Shattered Legacy.
///
/// Commands:
///   [ClusterFReset [username]  - Administrator: opens the reset gump for any account.
///   [ResetMyAccount            - Player (cc-P18, F-7): opens the same gump for the caller's own account
///                                and current character. TEST-SHARD TOOL: remove at the live rebirth.
///   [ClusterFDeleteChar        - target a character to force-delete (bypasses the 7-day wait).
///
/// The Reset Stone in New Haven (ClusterFResetStone.cs) opens the gump [ResetMyAccount opens. All three
/// build the gump through OpenReset and apply it through ExecuteReset, so they cannot drift apart.
///
/// Reset gump options, by scope (cc-P18):
///   This character:              Skills / Stats / Quest History
///   Every character on account:  Guilds / Exploration
///   The account:                 Achievements / Renown / Discoveries / League and flags / Bulletins
///
/// The confirm step lists what will be cleared before anything is touched.
/// </summary>
public static class ClusterFDevTools
{
    public static void Configure()
    {
        CommandSystem.Register("ClusterFReset",      AccessLevel.Administrator, OnResetCommand);
        CommandSystem.Register("ClusterFDeleteChar", AccessLevel.Administrator, OnDeleteCharCommand);

        // TEST-SHARD TOOL (cc-P18, F-7): remove with the Reset Stone when the live shard is reborn.
        CommandSystem.Register("ResetMyAccount",     AccessLevel.Player,        OnResetMyAccountCommand);
    }

    [Usage("ClusterFReset [username]")]
    [Description("Opens the developer reset gump for the specified account (or your own if omitted).")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void OnResetCommand(CommandEventArgs e)
    {
        IAccount?     account = null;
        PlayerMobile? pm      = null;

        if (e.Length >= 1)
        {
            var username = e.GetString(0);
            account = Accounts.GetAccount(username);
            if (account == null) { e.Mobile.SendMessage($"Account '{username}' not found."); return; }
            for (var i = 0; i < account.Length; i++)
                if (account[i] is PlayerMobile found) { pm = found; break; }
        }
        else
        {
            pm      = e.Mobile as PlayerMobile;
            account = pm?.Account as IAccount;
        }

        if (account == null) { e.Mobile.SendMessage("Could not resolve an account."); return; }

        OpenReset(e.Mobile, pm, account, selfService: false);
    }

    [Usage("ResetMyAccount")]
    [Description("Test shard: opens the account reset for your own account and this character. Takes no arguments.")]
    [ShardCommand(CommandCategory.Player, Shard = CommandShard.TestOnly)]
    private static void OnResetMyAccountCommand(CommandEventArgs e) => OpenSelfReset(e.Mobile);

    /// <summary>
    /// [ResetMyAccount and the Reset Stone: the caller's own account and current character, always.
    /// There is no username and no target, so no other account can be reached. Returns the gump sent,
    /// or null when the caller has no account (a test host player).
    /// </summary>
    public static DevResetGump? OpenSelfReset(Mobile from)
    {
        if (from is not PlayerMobile pm || pm.Account is not IAccount account)
        {
            from.SendMessage("Could not resolve your account.");
            return null;
        }

        return OpenReset(pm, pm, account, selfService: true);
    }

    /// <summary>The one way the reset gump is built and sent, for the admin tool and the player's.</summary>
    public static DevResetGump OpenReset(Mobile caller, PlayerMobile? pm, IAccount account, bool selfService)
    {
        var gump = new DevResetGump(caller, pm, account, new ResetOptions(), selfService);
        caller.SendGump(gump);
        return gump;
    }

    [Usage("ClusterFDeleteChar")]
    [Description("Target a player character to force-delete, bypassing the 7-day wait.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void OnDeleteCharCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the character to force-delete.");
        e.Mobile.Target = new DeleteCharTarget();
    }

    // -- Core reset logic ------------------------------------------------------

    public static void ExecuteReset(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts)
    {
        // -- This character ---------------------------------------------------

        if (opts.Skills && pm != null)
        {
            for (var i = 0; i < pm.Skills.Length; i++)
                pm.Skills[i].Base = 0.0;
            admin.SendMessage("Skills reset to 0.");
        }

        if (opts.Stats && pm != null)
        {
            pm.RawStr = 10;
            pm.RawDex = 10;
            pm.RawInt = 10;
            pm.Hits   = pm.HitsMax;
            pm.Stam   = pm.StamMax;
            pm.Mana   = pm.ManaMax;
            admin.SendMessage("Stats reset to 10 / 10 / 10.");
        }

        if (opts.Quests)
        {
            if (pm != null)
            {
                MLQuestSystem.Contexts.Remove(pm);
                admin.SendMessage("ML Quest history cleared.");
            }
            else
            {
                admin.SendMessage("Quest reset skipped - no character found on this account.");
            }
        }

        var data = ClusterFAccountPersistence.GetOrCreate(account);

        // -- Every character on the account -----------------------------------

        if (opts.Guilds)
        {
            // Membership, Apprentice marks, reputation, scrip, work orders, smith commissions and the
            // Artificer order (cc-P18), and the guild starter records (tools, starter items, welcome;
            // cc-P15), for every character of the account, so the whole starter path runs again.
            data.ClearGuildData();
            data.ClearGuildStarterRecords();

            // cc-P33 (F-3): a Custodian's rank is its lifetime Clean Up points, so they go with the guilds.
            for (var i = 0; i < account.Length; i++)
                if (account[i] is PlayerMobile character)
                    Server.Engines.Points.CleanUpBritanniaData.Instance.RemoveEntry(character);

            admin.SendMessage("Guild membership, rank, reputation, scrip, work orders, commissions, starter records and Clean Up Britannia points cleared for every character.");
        }

        if (opts.Exploration)
        {
            data.ClearExploration();

            // A character online now gets its (empty) map at once rather than at its next login.
            for (var i = 0; i < account.Length; i++)
                if (account[i] is PlayerMobile online && online.NetState != null)
                    ClusterFExplorationManager.OnPlayerLogin(online);

            admin.SendMessage("Exploration (fog of war) cleared for every character.");
        }

        // -- The account ------------------------------------------------------

        if (opts.Achievements)
        {
            ClusterFAchievementSystem.ResetForAccount(account.Username);
            data.AchievementPoints = 0;
            admin.SendMessage("Achievements and AP cleared.");
        }

        if (opts.Renown)
        {
            // cc-P48: lifetime Renown too, so the account starts over. League rank already held is kept: rank
            // never drops on Renown; "League, flags" is what clears it.
            data.Renown = 0;
            data.LifetimeRenown = 0;
            data.RestorationRegistry.Clear();
            admin.SendMessage("Renown, lifetime Renown and the restoration registry cleared. League rank is kept.");
        }

        if (opts.Discoveries)
        {
            data.OreDiscoveries.Clear();
            data.WoodDiscoveries.Clear();
            data.ImbuingDiscoveries.Clear();
            data.EncounteredCreatures.Clear();
            admin.SendMessage("Ore, wood and imbuing discoveries and encountered creatures cleared.");
        }

        if (opts.Flags)
        {
            // cc-P48: registration is the "league.joined" flag; every character's League rank goes with it.
            data.Flags.Clear();
            data.FlagValues.Clear();
            data.ClearLeagueData();

            for (var i = 0; i < account.Length; i++)
                if (account[i] is PlayerMobile ranked)
                    ClusterFBankLimit.Apply(ranked, false);

            admin.SendMessage("League registration, every character's League rank, and every account flag cleared.");
        }

        if (opts.Bulletins)
        {
            data.LastSeenBulletinId = 0;
            admin.SendMessage("Bulletin read position reset - all bulletins will show on next login.");
        }

        admin.SendMessage($"[ClusterFReset] Done for account '{account.Username}'.");
    }
}

// -- Reset options -------------------------------------------------------------

public class ResetOptions
{
    // This character
    public bool Skills;
    public bool Stats;
    public bool Quests;

    // Every character on the account
    public bool Guilds;
    public bool Exploration;

    // The account
    public bool Achievements;
    public bool Renown;
    public bool Discoveries;
    public bool Flags;
    public bool Bulletins;

    public bool Any =>
        Skills || Stats || Quests || Guilds || Exploration ||
        Achievements || Renown || Discoveries || Flags || Bulletins;

    public ResetOptions() { }

    public ResetOptions(bool all)
    {
        Skills = Stats = Quests = Guilds = Exploration = all;
        Achievements = Renown = Discoveries = Flags = Bulletins = all;
    }

    /// <summary>
    /// What will be cleared, one line each, in the gump's order. The confirm gump lists exactly these.
    /// </summary>
    public List<string> Describe(string? characterName)
    {
        var who   = characterName != null ? $"this character ({characterName})" : "this character";
        var items = new List<string>();
        if (Skills)       items.Add($"Skills, all to 0: {who}");
        if (Stats)        items.Add($"Str / Dex / Int to 10: {who}");
        if (Quests)       items.Add($"ML quest history: {who}");
        if (Guilds)       items.Add("Guilds, rank, rep, scrip, work orders, commissions, tools, starter items and Clean Up points: EVERY character");
        if (Exploration)  items.Add("Exploration (fog of war): EVERY character");
        if (Achievements) items.Add("Achievements and Achievement Points: the account");
        if (Renown)       items.Add("Renown and the restoration registry: the account");
        if (Discoveries)  items.Add("Ore, wood and imbuing discoveries, creatures encountered: the account");
        if (Flags)        items.Add("League registration, every character's League rank and all account flags: the account");
        if (Bulletins)    items.Add("Bulletin read position: the account");
        return items;
    }
}

// -- Dev Reset Gump ------------------------------------------------------------

/// <summary>
/// Checkbox-based gump for selecting which data to reset, grouped by scope.
/// Opens a confirmation gump before applying any changes.
/// </summary>
public class DevResetGump : Gump
{
    private readonly Mobile        _admin;
    private readonly PlayerMobile? _pm;
    private readonly IAccount      _account;
    private readonly ResetOptions  _opts;
    private readonly bool          _selfService;

    /// <summary>The account this gump resets. Fixed when the gump is built.</summary>
    public IAccount Account => _account;

    /// <summary>The character whose skills, stats and quests this gump resets.</summary>
    public PlayerMobile? Character => _pm;

    private const int GumpWidth  = 500;
    private const int HeaderH    = 70;
    private const int SectionH   = 24;
    private const int RowH       = 36;
    private const int FooterH    = 52;

    // Switch IDs
    private const int SwSkills       = 0;
    private const int SwStats        = 1;
    private const int SwQuests       = 2;
    private const int SwGuilds       = 3;
    private const int SwExploration  = 4;
    private const int SwAchievements = 5;
    private const int SwRenown       = 6;
    private const int SwDiscoveries  = 7;
    private const int SwFlags        = 8;
    private const int SwBulletins    = 9;

    // Button IDs
    private const int BtnCancel    = 0;
    private const int BtnSelectAll = 1;
    private const int BtnClearAll  = 2;
    private const int BtnReset     = 10;

    private static readonly (string Section, (string Label, string Note, int SwId)[] Rows)[] Sections =
    [
        ("This character only",
        [
            ("Skills",        "Resets all skill values to 0.0.",                              SwSkills),
            ("Stats",         "Resets Str/Dex/Int to 10, restores Hits/Stam/Mana.",          SwStats),
            ("Quest History", "Clears ML quest history (New Haven trainer quests included).", SwQuests),
        ]),
        ("Every character on the account",
        [
            ("Guilds",        "Membership, rank, rep, scrip, work orders, commissions, tools.", SwGuilds),
            ("Exploration",   "Fog of war: every area explored is forgotten.",                SwExploration),
        ]),
        ("The account",
        [
            ("Achievements",  "Clears all earned achievements and resets AP to 0.",          SwAchievements),
            ("Renown",        "Renown and the restoration registry (legacy items).",         SwRenown),
            ("Discoveries",   "Ore, wood and imbuing discoveries; creatures encountered.",   SwDiscoveries),
            ("League, flags", "League registration and rank, and every other account flag.", SwFlags),
            ("Bulletins",     "Resets bulletin read state - all bulletins show on login.",   SwBulletins),
        ]),
    ];

    private static int RowCount
    {
        get
        {
            var n = 0;
            foreach (var (_, rows) in Sections) n += rows.Length;
            return n;
        }
    }

    public DevResetGump(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts, bool selfService = false)
        : base(80, 40)
    {
        _admin       = admin;
        _pm          = pm;
        _account     = account;
        _opts        = opts;
        _selfService = selfService;

        Closable   = true;
        Disposable = true;

        var gumpHeight = HeaderH + Sections.Length * SectionH + RowCount * RowH + FooterH;

        AddBackground(0, 0, GumpWidth, gumpHeight, 9270);
        AddAlphaRegion(10, 10, GumpWidth - 20, gumpHeight - 20);

        // -- Header ---------------------------------------------------------
        var title = selfService ? "Reset My Account (test shard)" : "Developer Reset Tool";
        AddLabel(GumpWidth / 2 - title.Length * 4, 14, 37, title);
        var acctLine = $"Account: {account.Username}" +
                       (pm != null ? $"  ({pm.Name})" : "  (offline)");
        AddLabel(GumpWidth / 2 - acctLine.Length * 4, 34, 999, acctLine);
        AddImageTiled(10, 56, GumpWidth - 20, 2, 9304);

        // -- Checkbox rows, by scope ------------------------------------------
        var y = HeaderH;
        foreach (var (section, rows) in Sections)
        {
            AddLabel(20, y + 2, 1153, section);
            y += SectionH;

            foreach (var (label, note, swId) in rows)
            {
                var isChecked = IsChecked(opts, swId);

                AddCheck(20, y + 6, 9720, 9721, isChecked, swId);
                AddLabel(50, y + 2,  isChecked ? 1154 : 999, label);
                AddLabel(170, y + 2, 999, note);
                AddImageTiled(10, y + RowH - 1, GumpWidth - 20, 1, 9304);

                y += RowH;
            }
        }

        // -- Footer ---------------------------------------------------------
        AddImageTiled(10, y + 4, GumpWidth - 20, 2, 9304);

        AddButton(20,              y + 14, 4011, 4012, BtnSelectAll);
        AddLabel(55,               y + 16, 999, "Select All");

        AddButton(140,             y + 14, 4011, 4012, BtnClearAll);
        AddLabel(175,              y + 16, 999, "Clear All");

        AddButton(GumpWidth / 2 + 10, y + 14, 4023, 4025, BtnReset);
        AddLabel(GumpWidth / 2 + 45,  y + 16, 37,  "Reset");

        AddButton(GumpWidth - 110, y + 14, 4023, 4025, BtnCancel);
        AddLabel(GumpWidth - 75,   y + 16, 999, "Cancel");
    }

    private static bool IsChecked(ResetOptions opts, int swId) => swId switch
    {
        SwSkills       => opts.Skills,
        SwStats        => opts.Stats,
        SwQuests       => opts.Quests,
        SwGuilds       => opts.Guilds,
        SwExploration  => opts.Exploration,
        SwAchievements => opts.Achievements,
        SwRenown       => opts.Renown,
        SwDiscoveries  => opts.Discoveries,
        SwFlags        => opts.Flags,
        SwBulletins    => opts.Bulletins,
        _              => false,
    };

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        switch (info.ButtonID)
        {
            case BtnCancel:
                return;

            case BtnSelectAll:
                _admin.SendGump(new DevResetGump(_admin, _pm, _account, new ResetOptions(true), _selfService));
                return;

            case BtnClearAll:
                _admin.SendGump(new DevResetGump(_admin, _pm, _account, new ResetOptions(false), _selfService));
                return;

            case BtnReset:
            {
                var opts = ReadSwitches(info);
                if (!opts.Any)
                {
                    _admin.SendMessage("Select at least one category before resetting.");
                    _admin.SendGump(new DevResetGump(_admin, _pm, _account, _opts, _selfService));
                    return;
                }
                _admin.SendGump(new DevResetConfirmGump(_admin, _pm, _account, opts, _selfService));
                return;
            }
        }
    }

    private static ResetOptions ReadSwitches(in RelayInfo info) => new()
    {
        Skills       = info.IsSwitched(SwSkills),
        Stats        = info.IsSwitched(SwStats),
        Quests       = info.IsSwitched(SwQuests),
        Guilds       = info.IsSwitched(SwGuilds),
        Exploration  = info.IsSwitched(SwExploration),
        Achievements = info.IsSwitched(SwAchievements),
        Renown       = info.IsSwitched(SwRenown),
        Discoveries  = info.IsSwitched(SwDiscoveries),
        Flags        = info.IsSwitched(SwFlags),
        Bulletins    = info.IsSwitched(SwBulletins),
    };
}

// -- Dev Reset Confirm Gump ----------------------------------------------------

public class DevResetConfirmGump : Gump
{
    private readonly Mobile        _admin;
    private readonly PlayerMobile? _pm;
    private readonly IAccount      _account;
    private readonly ResetOptions  _opts;
    private readonly bool          _selfService;

    public DevResetConfirmGump(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts, bool selfService = false)
        : base(80, 60)
    {
        _admin       = admin;
        _pm          = pm;
        _account     = account;
        _opts        = opts;
        _selfService = selfService;

        const int W = 560;

        var items = opts.Describe(pm?.Name);

        var H = 118 + items.Count * 22 + 54;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);

        AddLabel(W / 2 - 50, 14, 37, "Confirm Reset");
        AddLabel(20, 36, 999, $"The following will be cleared for account '{account.Username}':");
        AddLabel(20, 56, 37, "Lines marked EVERY character affect every character on this account,");
        AddLabel(20, 74, 37, "not only the one you are playing. This cannot be undone.");
        AddImageTiled(10, 98, W - 20, 2, 9304);

        var y = 108;
        foreach (var item in items)
        {
            AddLabel(28, y, 1154, $"* {item}");
            y += 22;
        }

        AddImageTiled(10, y + 4, W - 20, 2, 9304);

        AddButton(W / 2 - 100, y + 16, 4023, 4025, 1);
        AddLabel(W / 2 - 66,   y + 18, 37, "Confirm Reset");

        AddButton(W / 2 + 40,  y + 16, 4023, 4025, 0);
        AddLabel(W / 2 + 74,   y + 18, 999, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1)
            ClusterFDevTools.ExecuteReset(_admin, _pm, _account, _opts);
        else
            _admin.SendGump(new DevResetGump(_admin, _pm, _account, _opts, _selfService));
    }
}

// -- Delete char target --------------------------------------------------------

public class DeleteCharTarget : Target
{
    public DeleteCharTarget() : base(12, false, TargetFlags.None) { }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not PlayerMobile pm)
        {
            from.SendMessage("That is not a player character.");
            return;
        }

        if (pm.NetState != null)
        {
            from.SendMessage($"Cannot delete '{pm.Name}' - character is currently online.");
            return;
        }

        var name    = pm.Name;
        var account = pm.Account?.Username ?? "(none)";

        // Clean up ML quest context before deleting the Mobile so Remove can match by reference.
        MLQuestSystem.Contexts.Remove(pm);

        pm.Delete();

        from.SendMessage($"Character '{name}' deleted. Account '{account}' and its data are preserved.");
    }
}
