using System.Collections.Generic;
using Server.Accounting;
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
///   [ClusterFReset [username]  — opens a selective reset gump for the account.
///   [ClusterFDeleteChar        — target a character to force-delete (bypasses the 7-day wait).
///
/// Reset gump lets you choose any combination of:
///   Skills / Stats / Achievements / Account Data / Bulletins / Quest History
///
/// The confirm step lists what will be cleared before anything is touched.
/// </summary>
public static class ClusterFDevTools
{
    public static void Configure()
    {
        CommandSystem.Register("ClusterFReset",      AccessLevel.Administrator, OnResetCommand);
        CommandSystem.Register("ClusterFDeleteChar", AccessLevel.Administrator, OnDeleteCharCommand);
    }

    [Usage("ClusterFReset [username]")]
    [Description("Opens the developer reset gump for the specified account (or your own if omitted).")]
    private static void OnResetCommand(CommandEventArgs e)
    {
        IAccount?     account = null;
        PlayerMobile? pm      = null;

        if (e.Length >= 1)
        {
            var username = e.GetString(0);
            account = Accounts.GetAccount(username);
            if (account == null) { e.Mobile.SendMessage($"Account '{username}' not found."); return; }
            for (var i = 0; i < account.Count; i++)
                if (account[i] is PlayerMobile found) { pm = found; break; }
        }
        else
        {
            pm      = e.Mobile as PlayerMobile;
            account = pm?.Account as IAccount;
        }

        if (account == null) { e.Mobile.SendMessage("Could not resolve an account."); return; }

        e.Mobile.SendGump(new DevResetGump(e.Mobile, pm, account, new ResetOptions()));
    }

    [Usage("ClusterFDeleteChar")]
    [Description("Target a player character to force-delete, bypassing the 7-day wait.")]
    private static void OnDeleteCharCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the character to force-delete.");
        e.Mobile.Target = new DeleteCharTarget();
    }

    // ── Core reset logic ──────────────────────────────────────────────────────

    public static void ExecuteReset(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts)
    {
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

        if (opts.Achievements)
        {
            ClusterFAchievementSystem.ResetForAccount(account.Username);
            ClusterFAccountPersistence.GetOrCreate(account).AchievementPoints = 0;
            admin.SendMessage("Achievements and AP cleared.");
        }

        if (opts.AccountData)
        {
            var data = ClusterFAccountPersistence.GetOrCreate(account);
            data.Renown = 0;
            data.GuildReputation.Clear();
            data.GuildCurrency.Clear();
            data.RestorationRegistry.Clear();
            admin.SendMessage("Account data (Renown, guild rep/currency, restoration registry) cleared.");
        }

        if (opts.Bulletins)
        {
            ClusterFAccountPersistence.GetOrCreate(account).LastSeenBulletinId = 0;
            admin.SendMessage("Bulletin read position reset — all bulletins will show on next login.");
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
                admin.SendMessage("Quest reset skipped — no character found on this account.");
            }
        }

        admin.SendMessage($"[ClusterFReset] Done for account '{account.Username}'.");
    }
}

// ── Reset options ─────────────────────────────────────────────────────────────

public class ResetOptions
{
    public bool Skills;
    public bool Stats;
    public bool Achievements;
    public bool AccountData;
    public bool Bulletins;
    public bool Quests;

    public bool Any => Skills || Stats || Achievements || AccountData || Bulletins || Quests;

    public ResetOptions() { }

    public ResetOptions(bool all)
    {
        Skills = Stats = Achievements = AccountData = Bulletins = Quests = all;
    }
}

// ── Dev Reset Gump ────────────────────────────────────────────────────────────

/// <summary>
/// Checkbox-based gump for selecting which data to reset.
/// Opens a confirmation gump before applying any changes.
/// </summary>
public class DevResetGump : Gump
{
    private readonly Mobile       _admin;
    private readonly PlayerMobile? _pm;
    private readonly IAccount     _account;
    private readonly ResetOptions _opts;

    private const int GumpWidth  = 460;
    private const int HeaderH    = 70;
    private const int RowH       = 44;
    private const int NumRows    = 6;
    private const int FooterH    = 52;
    private const int GumpHeight = HeaderH + NumRows * RowH + FooterH;

    // Switch IDs (must match row index for ReadSwitches)
    private const int SwSkills       = 0;
    private const int SwStats        = 1;
    private const int SwAchievements = 2;
    private const int SwAccountData  = 3;
    private const int SwBulletins    = 4;
    private const int SwQuests       = 5;

    // Button IDs
    private const int BtnCancel    = 0;
    private const int BtnSelectAll = 1;
    private const int BtnClearAll  = 2;
    private const int BtnReset     = 10;

    private static readonly (string Label, string Note, int SwId)[] Rows =
    [
        ("Skills",       "Resets all skill values to 0.0.",                          SwSkills),
        ("Stats",        "Resets Str/Dex/Int to 10, restores Hits/Stam/Mana.",      SwStats),
        ("Achievements", "Clears all earned achievements and resets AP to 0.",       SwAchievements),
        ("Account Data", "Clears Renown, guild rep/currency, restoration items.",    SwAccountData),
        ("Bulletins",    "Resets bulletin read state — all bulletins show on login.",SwBulletins),
        ("Quest History","Clears all New Haven trainer quest completion history.",    SwQuests),
    ];

    public DevResetGump(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts)
        : base(80, 60)
    {
        _admin   = admin;
        _pm      = pm;
        _account = account;
        _opts    = opts;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        AddAlphaRegion(10, 10, GumpWidth - 20, GumpHeight - 20);

        // ── Header ─────────────────────────────────────────────────────────
        AddLabel(GumpWidth / 2 - 80, 14, 37, "Developer Reset Tool");
        var acctLine = $"Account: {account.Username}" +
                       (pm != null ? $"  ({pm.Name})" : "  (offline)");
        AddLabel(GumpWidth / 2 - acctLine.Length * 4, 34, 999, acctLine);
        AddImageTiled(10, 56, GumpWidth - 20, 2, 9304);

        // ── Checkbox rows ─────────────────────────────────────────────────
        var y = HeaderH;
        for (var i = 0; i < Rows.Length; i++)
        {
            var (label, note, swId) = Rows[i];
            var isChecked = swId switch
            {
                SwSkills       => opts.Skills,
                SwStats        => opts.Stats,
                SwAchievements => opts.Achievements,
                SwAccountData  => opts.AccountData,
                SwBulletins    => opts.Bulletins,
                SwQuests       => opts.Quests,
                _              => false,
            };

            AddCheck(20, y + 10, 9720, 9721, isChecked, swId);
            AddLabel(50, y + 9,  isChecked ? 1154 : 999, label);
            AddLabel(50, y + 25, 999, note);

            if (i < Rows.Length - 1)
                AddImageTiled(10, y + RowH - 1, GumpWidth - 20, 1, 9304);

            y += RowH;
        }

        // ── Footer ─────────────────────────────────────────────────────────
        AddImageTiled(10, y + 4, GumpWidth - 20, 2, 9304);

        AddButton(20,              y + 14, 4011, 4012, BtnSelectAll);
        AddLabel(38,               y + 16, 999, "Select All");

        AddButton(130,             y + 14, 4011, 4012, BtnClearAll);
        AddLabel(148,              y + 16, 999, "Clear All");

        AddButton(GumpWidth / 2 - 30, y + 14, 4023, 4025, BtnReset);
        AddLabel(GumpWidth / 2 - 5,   y + 16, 37,  "Reset");

        AddButton(GumpWidth - 110, y + 14, 4023, 4025, BtnCancel);
        AddLabel(GumpWidth - 84,   y + 16, 999, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        switch (info.ButtonID)
        {
            case BtnCancel:
                return;

            case BtnSelectAll:
                _admin.SendGump(new DevResetGump(_admin, _pm, _account, new ResetOptions(true)));
                return;

            case BtnClearAll:
                _admin.SendGump(new DevResetGump(_admin, _pm, _account, new ResetOptions(false)));
                return;

            case BtnReset:
            {
                var opts = ReadSwitches(info);
                if (!opts.Any)
                {
                    _admin.SendMessage("Select at least one category before resetting.");
                    _admin.SendGump(new DevResetGump(_admin, _pm, _account, _opts));
                    return;
                }
                _admin.SendGump(new DevResetConfirmGump(_admin, _pm, _account, opts));
                return;
            }
        }
    }

    private static ResetOptions ReadSwitches(in RelayInfo info) => new()
    {
        Skills       = info.IsSwitched(SwSkills),
        Stats        = info.IsSwitched(SwStats),
        Achievements = info.IsSwitched(SwAchievements),
        AccountData  = info.IsSwitched(SwAccountData),
        Bulletins    = info.IsSwitched(SwBulletins),
        Quests       = info.IsSwitched(SwQuests),
    };
}

// ── Dev Reset Confirm Gump ────────────────────────────────────────────────────

public class DevResetConfirmGump : Gump
{
    private readonly Mobile        _admin;
    private readonly PlayerMobile? _pm;
    private readonly IAccount      _account;
    private readonly ResetOptions  _opts;

    public DevResetConfirmGump(Mobile admin, PlayerMobile? pm, IAccount account, ResetOptions opts)
        : base(80, 60)
    {
        _admin   = admin;
        _pm      = pm;
        _account = account;
        _opts    = opts;

        const int W = 400;

        var items = new List<string>();
        if (opts.Skills)       items.Add("All skill values (reset to 0)");
        if (opts.Stats)        items.Add("Str / Dex / Int (reset to 10)");
        if (opts.Achievements) items.Add("Achievements + Achievement Points");
        if (opts.AccountData)  items.Add("Renown, guild rep, guild currency, restoration registry");
        if (opts.Bulletins)    items.Add("Bulletin read position");
        if (opts.Quests)       items.Add("ML Quest completion history");

        var H = 76 + items.Count * 22 + 54;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);

        AddLabel(W / 2 - 62, 14, 37, "Confirm Reset");
        AddLabel(20, 36, 999, $"The following will be cleared for '{account.Username}':");
        AddImageTiled(10, 56, W - 20, 2, 9304);

        var y = 66;
        foreach (var item in items)
        {
            AddLabel(28, y, 1154, $"• {item}");
            y += 22;
        }

        AddImageTiled(10, y + 4, W - 20, 2, 9304);

        AddButton(W / 2 - 100, y + 16, 4023, 4025, 1);
        AddLabel(W / 2 - 74,   y + 18, 37, "Confirm Reset");

        AddButton(W / 2 + 30,  y + 16, 4023, 4025, 0);
        AddLabel(W / 2 + 56,   y + 18, 999, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1)
            ClusterFDevTools.ExecuteReset(_admin, _pm, _account, _opts);
        else
            _admin.SendGump(new DevResetGump(_admin, _pm, _account, _opts));
    }
}

// ── Delete char target ────────────────────────────────────────────────────────

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
            from.SendMessage($"Cannot delete '{pm.Name}' — character is currently online.");
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
