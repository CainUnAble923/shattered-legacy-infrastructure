using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// ── Guild task type ───────────────────────────────────────────────────────────

public enum GuildTaskType
{
    SkillOnly,
    ItemOnly,
    SkillOrItem,
}

// ── Guild definition ──────────────────────────────────────────────────────────

public class GuildDef
{
    public string          Key               { get; }
    public string          Name              { get; }
    public string          Pitch             { get; }   // guildmaster's greeting
    public string          TaskDescription   { get; }   // shown in detail gump
    public Type            GuildmasterType   { get; }
    public GuildTaskType   TaskType          { get; }
    public SkillName       TaskSkill         { get; }
    public double          TaskSkillMin      { get; }
    public Type?           TaskItemType      { get; }
    public int             TaskItemCount     { get; }
    public int             JoinReputation    { get; }   // rep awarded on join
    public int             JoinScrip         { get; }   // scrip awarded on join

    public GuildDef(
        string key, string name, string pitch, string taskDesc,
        Type guildmasterType,
        GuildTaskType taskType,
        SkillName skill, double skillMin,
        Type? itemType, int itemCount,
        int joinRep, int joinScrip)
    {
        Key             = key;
        Name            = name;
        Pitch           = pitch;
        TaskDescription = taskDesc;
        GuildmasterType = guildmasterType;
        TaskType        = taskType;
        TaskSkill       = skill;
        TaskSkillMin    = skillMin;
        TaskItemType    = itemType;
        TaskItemCount   = itemCount;
        JoinReputation  = joinRep;
        JoinScrip       = joinScrip;
    }
}

// ── Guild system ──────────────────────────────────────────────────────────────

/// <summary>
/// Shattered Legacy guild membership system.
///
/// Each account can join multiple guilds. Membership is stored in
/// ClusterFAccountData.JoinedGuilds (HashSet{string}).
///
/// Join flow:
///   Right-click a guildmaster → "Guild Membership" → GuildTaskDetailGump
///   Player meets task → clicks [Join] → membership granted, rep and scrip awarded.
///
/// Commands:
///   [guild   — opens GuildProgressGump showing all 12 guilds and membership status.
/// </summary>
public static class ClusterFGuildSystem
{
    private static readonly Dictionary<string, GuildDef>  _byKey  = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type,   GuildDef>  _byNpc  = new();

    public static IReadOnlyDictionary<string, GuildDef> AllGuilds => _byKey;

    public static void Configure()
    {
        RegisterAll();
        CommandSystem.Register("guild", AccessLevel.Player, OnGuildCommand);
    }

    private static void RegisterAll()
    {
        Register(new GuildDef(
            "smithing", "Smiths' Fellowship",
            "The forge never rests, and neither do we. Prove your mettle.",
            "Demonstrate Blacksmithing of at least 50.0, or bring 10 Iron Ingots as tribute.",
            typeof(BlacksmithGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Blacksmith, 50.0,
            typeof(IronIngot), 10,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "arcane", "Arcane Society",
            "Magic is discipline. Can you demonstrate yours?",
            "Demonstrate Magery of at least 50.0, or bring 5 Blank Scrolls as tribute.",
            typeof(MageGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Magery, 50.0,
            typeof(BlankScroll), 5,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "tailoring", "Tailors' Circle",
            "Cloth and needle shape the world as surely as steel.",
            "Demonstrate Tailoring of at least 50.0, or bring 10 Cloth as tribute.",
            typeof(TailorGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Tailoring, 50.0,
            typeof(Cloth), 10,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "tinkers", "Tinkers' Union",
            "Gears and ingenuity — the Foundation of civilization.",
            "Demonstrate Tinkering of at least 50.0, or bring 5 Gears as tribute.",
            typeof(TinkerGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Tinkering, 50.0,
            typeof(Gears), 5,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "healers", "Healers' Covenant",
            "Life is precious. Show us you know how to preserve it.",
            "Demonstrate Healing of at least 50.0, or bring 25 Bandages as tribute.",
            typeof(HealerGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Healing, 50.0,
            typeof(Bandage), 25,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "bards", "Bards' Consortium",
            "The world turns on a song. Do you have the gift?",
            "Demonstrate Musicianship of at least 30.0 to be considered.",
            typeof(BardGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Musicianship, 30.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "thieves", "Thieves' Den",
            "We see all. Can we trust you? Show us your light fingers.",
            "Demonstrate Stealing of at least 20.0 to earn entry.",
            typeof(ThiefGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Stealing, 20.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "warriors", "Warriors' Brotherhood",
            "Strength alone is not enough. Prove you understand the art of battle.",
            "Demonstrate Tactics of at least 30.0 to join our ranks.",
            typeof(WarriorGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Tactics, 30.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "mining", "Miners' Compact",
            "The earth yields its secrets only to those who know where to dig.",
            "Demonstrate Mining of at least 50.0, or bring 20 Iron Ore as tribute.",
            typeof(MinerGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Mining, 50.0,
            typeof(IronOre), 20,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "rangers", "Rangers' League",
            "The wild is our home. Show you belong in it.",
            "Demonstrate Archery of at least 30.0, or bring 25 Arrows as tribute.",
            typeof(OutridersGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Archery, 30.0,
            typeof(Arrow), 25,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "maritime", "Maritime Brotherhood",
            "The sea is merciless. Prove you can read its moods.",
            "Demonstrate Fishing of at least 30.0, or bring 5 Fish as tribute.",
            typeof(FisherGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Fishing, 30.0,
            typeof(Fish), 5,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "merchants", "Merchants' Exchange",
            "Gold speaks louder than any deed. Show us your means.",
            "Bring 500 Gold Coins as an initiation fee.",
            typeof(MerchantGuildmaster),
            GuildTaskType.ItemOnly,
            SkillName.ItemID, 0.0,  // ItemID as dummy skill for ItemOnly
            typeof(Gold), 500,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "foresters", "Foresters' Union",
            "The forest feeds Britannia. Show you know how to work it.",
            "Demonstrate Lumberjacking of at least 50.0, or bring 20 Boards as tribute.",
            typeof(ForestersGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Lumberjacking, 50.0,
            typeof(Board), 20,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "artificers", "Artificers' Order",
            "Magic and craft are one. Show you understand the art of imbuing.",
            "Demonstrate Imbuing of at least 20.0, or bring 3 Diamonds as tribute.",
            typeof(ArtificersGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Imbuing, 20.0,
            typeof(Diamond), 3,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "custodians", "The Custodians",
            "Britannia's streets do not clean themselves. We are its invisible backbone.",
            "Open to all citizens — simply speak with a Sanitation Warden and pledge your service.",
            typeof(SanitationWarden),
            GuildTaskType.SkillOnly,
            SkillName.ItemID, 0.0,  // 0.0 min = always eligible
            null, 0,
            joinRep: 25, joinScrip: 5));
    }

    private static void Register(GuildDef def)
    {
        _byKey[def.Key]             = def;
        _byNpc[def.GuildmasterType] = def;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public static GuildDef? GetDef(string key) =>
        _byKey.TryGetValue(key, out var d) ? d : null;

    public static GuildDef? GetDefForGuildmaster(Type npcType) =>
        _byNpc.TryGetValue(npcType, out var d) ? d : null;

    public static bool IsJoined(IAccount acct, string key) =>
        ClusterFAccountPersistence.GetOrCreate(acct).JoinedGuilds.Contains(key);

    public static bool CanJoin(PlayerMobile pm, GuildDef def, out string reason)
    {
        if (pm.Account is not IAccount acct)
        {
            reason = "Could not resolve account.";
            return false;
        }

        if (IsJoined(acct, def.Key))
        {
            reason = "Already a member.";
            return false;
        }

        switch (def.TaskType)
        {
            case GuildTaskType.SkillOnly:
                if (pm.Skills[def.TaskSkill].Base >= def.TaskSkillMin)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskSkill} ≥ {def.TaskSkillMin:F1}.";
                return false;

            case GuildTaskType.ItemOnly:
                if (pm.Backpack?.GetAmount(def.TaskItemType!) >= def.TaskItemCount)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskItemCount}x {def.TaskItemType!.Name} in your backpack.";
                return false;

            case GuildTaskType.SkillOrItem:
                if (pm.Skills[def.TaskSkill].Base >= def.TaskSkillMin)
                {
                    reason = string.Empty;
                    return true;
                }
                if (def.TaskItemType != null && pm.Backpack?.GetAmount(def.TaskItemType) >= def.TaskItemCount)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskSkill} ≥ {def.TaskSkillMin:F1} OR {def.TaskItemCount}x {def.TaskItemType!.Name}.";
                return false;

            default:
                reason = "Unknown task type.";
                return false;
        }
    }

    public static void Join(PlayerMobile pm, GuildDef def)
    {
        if (pm.Account is not IAccount acct) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        // Consume items if skill check did not pass but items are present.
        if (def.TaskType != GuildTaskType.SkillOnly &&
            pm.Skills[def.TaskSkill].Base < def.TaskSkillMin &&
            def.TaskItemType != null)
        {
            pm.Backpack?.ConsumeTotal(def.TaskItemType, def.TaskItemCount);
        }

        data.JoinedGuilds.Add(def.Key);
        data.AddReputation(def.Key, def.JoinReputation);
        data.AddCurrency(def.Key, def.JoinScrip);

        pm.SendMessage(54, $"You have been accepted into the {def.Name}!");
        pm.SendMessage(999, $"Awarded: {def.JoinReputation} reputation and {def.JoinScrip} scrip with the {def.Name}.");

        // ── Guild join bonuses ─────────────────────────────────────────────────
        // Mining: give a Compact Ore Satchel on first join.
        if (def.Key.Equals("mining", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.CompactOreSatchel());
            pm.Backpack.DropItem(new Items.ProspectorsLogbook());
            pm.Backpack.DropItem(new Items.CompactDispatchLedger());
            pm.SendMessage(0x44, "You have been issued a Compact Ore Satchel, a Prospector's Logbook, and a Compact Dispatch Ledger.");
            pm.SendMessage(0x44, "Mined ore will route to the satchel automatically. Colored ore discoveries are logged in the book.");
            pm.SendMessage(0x44, "Use the Dispatch Ledger to access work orders from anywhere in the world.");
        }

        // Smithing: give a SmithGuildBook and SmithGuildSalvageBag on first join.
        if (def.Key.Equals("smithing", StringComparison.OrdinalIgnoreCase))
        {
            SmithGuildBook.OnSmithingJoined(pm);
            Items.SmithGuildSalvageBag.OnSmithingJoined(pm);
        }

        // Rangers: issue Outrider's Crook and Hunter's Satchel on first join.
        if (def.Key.Equals("rangers", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.OutridersCrook());
            pm.Backpack.DropItem(new Items.HuntersSatchel());
            pm.SendMessage(0x44, "Welcome to the Rangers' League, Wanderer. You have been issued your first Trail Marks.");
            pm.SendMessage(0x44, "You have been issued an Outrider's Crook and a Hunter's Satchel.");
            pm.SendMessage(0x44, "Right-click the crook to deliver pets for contracts, shrink bonded animals, or instant-bond.");
            pm.SendMessage(0x44, "Speak with the Outriders' Guildmaster in New Haven to access field contracts.");
        }

        // Foresters: give a Forester's Logbook and Lumber Satchel on first join.
        if (def.Key.Equals("foresters", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.ForestersLogbook());
            pm.Backpack.DropItem(new Items.ForestersLumberSatchel());
            pm.SendMessage(0x44, "Welcome to the Foresters' Union, Woodcutter. Your Timber Tokens are ready.");
            pm.SendMessage(0x44, "You have been issued a Forester's Logbook and a Lumber Satchel.");
            pm.SendMessage(0x44, "Chopped logs and boards route to the satchel automatically. All timber discoveries are recorded in the logbook.");
        }

        // Custodians: give a TrashBag on first join.
        // Note: SanitationWardenGump.HandleJoin also calls OnCustodiansJoined directly
        // when joining via the warden — this path handles the [guild command join route.
        if (def.Key.Equals("custodians", StringComparison.OrdinalIgnoreCase))
            SanitationWarden.OnCustodiansJoined(pm);

        // Artificers' Order: issue the Essence Satchel + Toolkit and send welcome messages.
        if (def.Key.Equals("artificers", StringComparison.OrdinalIgnoreCase))
        {
            pm.Backpack.DropItem(new Items.ArtificersSatchel());
            pm.Backpack.DropItem(new Items.ArtificersToolkit());
            pm.SendMessage(0x44, "Welcome to the Artificers' Order, Apprentice. Your Essence Shards are ready.");
            pm.SendMessage(0x44, "You have been issued an Artificers' Essence Satchel and an Artificers' Toolkit.");
            pm.SendMessage(0x44, "The Satchel stores your PropertyEssences and tracks mastery progress.");
            pm.SendMessage(0x44, "The Toolkit lets you imbue and disenchant anywhere — no NPC visit required.");
            pm.SendMessage(0x44, "As your Artificers' Standing grows, you will unlock higher intensity caps and more property slots.");
        }

        ClusterFLeagueSystem.OnGuildJoined(pm);
    }

    // ── Remote member services routing ───────────────────────────────────

    /// <summary>
    /// Opens the appropriate member services gump for a guild.
    /// Used by both GuildMembershipEntry (NPC context menu) and
    /// GuildProgressGump (remote [guild command / LeagueMemberPass).
    /// For members: routes to the guild's member dashboard.
    /// For non-members: falls back to GuildTaskDetailGump (join screen).
    /// </summary>
    public static void OpenMemberServices(PlayerMobile pm, GuildDef def, IAccount acct)
    {
        var isMember = IsJoined(acct, def.Key);

        switch (def.Key.ToLowerInvariant())
        {
            case "smithing":
                pm.SendGump(new SmithGuildmasterGump(pm, def, acct));
                return;
            case "custodians":
                pm.SendGump(new SanitationWardenGump(pm, def, acct));
                return;
            case "rangers":
                pm.SendGump(isMember
                    ? new OutridersGuildmasterGump(pm, OutridersGuildmasterGump.View.MemberDashboard)
                    : new OutridersGuildmasterGump(pm));
                return;
            case "foresters":
                pm.SendGump(isMember
                    ? new ForestersGuildmasterGump(pm, ForestersGuildmasterGump.View.MemberDashboard)
                    : new ForestersGuildmasterGump(pm));
                return;
            case "artificers":
                pm.SendGump(isMember
                    ? new ArtificersGuildmasterGump(pm, ArtificersGuildmasterGump.View.MemberDashboard)
                    : new ArtificersGuildmasterGump(pm));
                return;
            case "mining":
                pm.SendGump(isMember
                    ? new MinersCompactLiaisonGump(pm, MinersCompactLiaisonGump.View.MemberDashboard)
                    : new MinersCompactLiaisonGump(pm));
                return;
        }

        // Generic fallback for guilds without a custom gump.
        pm.SendGump(new GuildTaskDetailGump(pm, def, acct));
    }

    // ── Rank name helper ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the rank title for a player's standing within a specific guild.
    /// Guild-specific ladders are used where defined; all others fall back to
    /// the generic five-tier ladder.
    /// </summary>
    public static string GetRankName(string guildKey, int standing) =>
        guildKey.ToLowerInvariant() switch
        {
            "smithing"    => SmithingRank(standing),
            "rangers"     => OutridersRank(standing),
            "foresters"   => ForestersRank(standing),
            "custodians"  => ClusterFCustodianSystem.GetCustodianRank(standing),
            "artificers"  => ArtificersGuildmasterGump.GetRankName(standing),
            _             => GenericRank(standing),
        };

    private static string ForestersRank(int standing) => standing switch
    {
        >= 150_000 => "Legendary Forester",
        >= 80_000  => "Master of the Wood",
        >= 40_000  => "Grove Warden",
        >= 15_000  => "Arborist",
        >= 5_000   => "Forester",
        >= 1_000   => "Sawyer",
        _          => "Woodcutter",
    };

    private static string OutridersRank(int standing) => standing switch
    {
        >= 150_000 => "Legendary Outrider",
        >= 80_000  => "Warden of the Wild",
        >= 40_000  => "Beastmaster",
        >= 15_000  => "Trailblazer",
        >= 5_000   => "Outrider",
        >= 1_000   => "Scout",
        _          => "Wanderer",
    };

    private static string SmithingRank(int standing) => standing switch
    {
        >= 100_000 => "Legendary",
        >= 50_000  => "Grandmaster",
        >= 15_000  => "Master",
        >= 5_000   => "Journeyman",
        >= 1_000   => "Apprentice",
        _          => "Initiate",
    };

    private static string GenericRank(int standing) => standing switch
    {
        >= 100_000 => "Legendary",
        >= 50_000  => "Grandmaster",
        >= 15_000  => "Master",
        >= 5_000   => "Journeyman",
        >= 1_000   => "Apprentice",
        _          => "Initiate",
    };

    // ── [guild command ────────────────────────────────────────────────────

    [Usage("guild")]
    [Description("Opens the guild membership and progress overview.")]
    private static void OnGuildCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;
        e.Mobile.SendGump(new GuildProgressGump(pm, acct));
    }
}

// ── Guild context menu entry ───────────────────────────────────────────────────

public class GuildMembershipEntry : ContextMenuEntry
{
    private readonly PlayerMobile _from;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;

    // Cliloc 6146 = "Talk" — confirmed from BaseQuester.TalkNumber default
    public GuildMembershipEntry(PlayerMobile from, GuildDef def, IAccount acct)
        : base(6146, 4)
    {
        _from = from;
        _def  = def;
        _acct = acct;
    }

    public override void OnClick(Mobile from, IEntity target)
    {
        ClusterFGuildSystem.OpenMemberServices(_from, _def, _acct);
    }
}

// ── Guild progress gump ───────────────────────────────────────────────────────

/// <summary>
/// Overview of all 12 guilds — shows joined/not-joined status and a [View] button
/// to open GuildTaskDetailGump for each guild.
/// </summary>
public class GuildProgressGump : Gump
{
    private readonly PlayerMobile _pm;
    private readonly IAccount     _acct;

    private static readonly GuildDef[] AllDefs = BuildDefList();

    private static GuildDef[] BuildDefList()
    {
        var list = new List<GuildDef>(ClusterFGuildSystem.AllGuilds.Values);
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list.ToArray();
    }

    private const int W        = 640;
    private const int HeaderH  = 70;
    private const int RowH     = 36;
    private const int FooterH  = 48;

    // Button IDs:
    //   0          = close
    //   100 + i    = Services / View for guild i
    //   200 + i    = Contracts for guild i (members only)
    private const int BtnClose = 0;

    public GuildProgressGump(PlayerMobile pm, IAccount acct) : base(50, 50)
    {
        _pm   = pm;
        _acct = acct;

        Closable   = true;
        Disposable = true;

        var data        = ClusterFAccountPersistence.GetOrCreate(acct);
        var joined      = data.JoinedGuilds;
        var joinedCount = 0;
        foreach (var def in AllDefs)
            if (joined.Contains(def.Key)) joinedCount++;

        var totalH = HeaderH + AllDefs.Length * RowH + FooterH;

        AddBackground(0, 0, W, totalH, 9270);
        AddAlphaRegion(10, 10, W - 20, totalH - 20);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - 50, 14, 1154, "Guild Registry");
        AddLabel(W / 2 - 70, 34, 999, $"Member of {joinedCount} / {AllDefs.Length} guilds");
        AddImageTiled(10, 56, W - 20, 2, 9304);

        // ── Column headers ────────────────────────────────────────────────
        // (implicit — layout is self-explanatory)

        // ── Guild rows ────────────────────────────────────────────────────
        var y = HeaderH;
        for (var i = 0; i < AllDefs.Length; i++)
        {
            var def      = AllDefs[i];
            var isMember = joined.Contains(def.Key);
            var nameHue  = isMember ? 1154 : 999;

            string statusTxt;
            int    statusHue;
            if (isMember)
            {
                var standing = data.GetReputation(def.Key);
                var tokens   = data.GetCurrency(def.Key);
                var rankName = ClusterFGuildSystem.GetRankName(def.Key, standing);
                statusTxt = tokens > 0 ? $"{rankName}  ({tokens:N0} scrip)" : rankName;
                statusHue = 68;
            }
            else
            {
                statusTxt = "Not joined";
                statusHue = 37;
            }

            // Guild name
            AddLabel(18, y + 9, nameHue, def.Name);

            // Rank / status
            AddLabel(230, y + 9, statusHue, statusTxt);

            // Contracts button — members only
            if (isMember)
            {
                AddButton(450, y + 7, 4011, 4012, 200 + i);
                AddLabel(474, y + 9, 999, "Contracts");
            }

            // Services / View button — always visible
            AddButton(556, y + 7, 4011, 4012, 100 + i);
            AddLabel(580, y + 9, isMember ? 1154 : 999, isMember ? "Services" : "View");

            if (i < AllDefs.Length - 1)
                AddImageTiled(10, y + RowH - 1, W - 20, 1, 9304);

            y += RowH;
        }

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(10, y + 4, W - 20, 2, 9304);
        AddButton(W / 2 - 20, y + 14, 4023, 4025, BtnClose);
        AddLabel(W / 2 + 4, y + 16, 999, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnClose) return;

        // Services / View
        if (info.ButtonID >= 100 && info.ButtonID < 200)
        {
            var idx = info.ButtonID - 100;
            if (idx < 0 || idx >= AllDefs.Length) return;
            var def = AllDefs[idx];
            ClusterFGuildSystem.OpenMemberServices(_pm, def, _acct);
            return;
        }

        // Contracts (members only)
        if (info.ButtonID >= 200 && info.ButtonID < 300)
        {
            var idx = info.ButtonID - 200;
            if (idx < 0 || idx >= AllDefs.Length) return;
            _pm.SendGump(new GuildContractLedgerGump(_pm, AllDefs[idx].Key));
            return;
        }
    }
}

// ── Guild task detail gump ────────────────────────────────────────────────────

/// <summary>
/// Detail view for a single guild. Shows pitch, task requirement, current status,
/// and a [Join] button when the player meets the requirements.
/// </summary>
public class GuildTaskDetailGump : Gump
{
    private readonly PlayerMobile _pm;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;

    private const int W = 480;

    // Button IDs
    private const int BtnBack = 0;
    private const int BtnJoin = 1;

    public GuildTaskDetailGump(PlayerMobile pm, GuildDef def, IAccount acct) : base(80, 80)
    {
        _pm   = pm;
        _def  = def;
        _acct = acct;

        Closable   = true;
        Disposable = true;

        var isMember = ClusterFGuildSystem.IsJoined(acct, def.Key);
        var reason   = string.Empty;
        var canJoin  = !isMember && ClusterFGuildSystem.CanJoin(pm, def, out reason);

        // Build task status line
        string taskStatus;
        int    taskHue;
        if (isMember)
        {
            taskStatus = "You are a member of this guild.";
            taskHue    = 68; // green
        }
        else if (canJoin)
        {
            taskStatus = "Requirements met — you may join!";
            taskHue    = 1154; // gold
        }
        else
        {
            taskStatus = reason;
            taskHue    = 37; // red
        }

        // Inventory snapshot for item tasks
        string itemStatus = string.Empty;
        if (!isMember && def.TaskType != GuildTaskType.SkillOnly && def.TaskItemType != null)
        {
            var have = pm.Backpack?.GetAmount(def.TaskItemType) ?? 0;
            itemStatus = $"In backpack: {have} / {def.TaskItemCount} {def.TaskItemType.Name}";
        }
        string skillStatus = string.Empty;
        if (!isMember && def.TaskType != GuildTaskType.ItemOnly)
        {
            var val = pm.Skills[def.TaskSkill].Base;
            skillStatus = $"{def.TaskSkill}: {val:F1} / {def.TaskSkillMin:F1} required";
        }

        // Rep / scrip reward line
        var rewardLine = $"On join: +{def.JoinReputation} reputation, +{def.JoinScrip} scrip";

        // Count content lines for dynamic height
        var lines = 0;
        if (skillStatus.Length > 0) lines++;
        if (itemStatus.Length  > 0) lines++;

        var H = 160 + lines * 22 + 60;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - def.Name.Length * 4, 14, 1154, def.Name);
        AddImageTiled(10, 34, W - 20, 2, 9304);

        // ── Pitch ─────────────────────────────────────────────────────────
        AddHtml(20, 42, W - 40, 36, $"<BASEFONT COLOR=#DDDDDD><I>{def.Pitch}</I></BASEFONT>", false, false);

        AddImageTiled(10, 84, W - 20, 2, 9304);

        // ── Task description ──────────────────────────────────────────────
        AddLabel(20, 92, 999, "Initiation Task:");
        AddHtml(20, 110, W - 40, 36, $"<BASEFONT COLOR=#DDDDDD>{def.TaskDescription}</BASEFONT>", false, false);

        var y = 152;

        if (skillStatus.Length > 0)
        {
            var shue = (!isMember && pm.Skills[def.TaskSkill].Base >= def.TaskSkillMin) ? 68 : 999;
            AddLabel(20, y, shue, skillStatus);
            y += 22;
        }
        if (itemStatus.Length > 0)
        {
            var have  = pm.Backpack?.GetAmount(def.TaskItemType!) ?? 0;
            var ihue  = have >= def.TaskItemCount ? 68 : 999;
            AddLabel(20, y, ihue, itemStatus);
            y += 22;
        }

        AddImageTiled(10, y + 4, W - 20, 2, 9304);

        // Status line
        AddLabel(20, y + 12, taskHue, taskStatus);
        y += 34;

        // Reward hint
        AddLabel(20, y, 999, rewardLine);
        y += 22;

        AddImageTiled(10, y + 4, W - 20, 2, 9304);

        // Buttons
        if (canJoin)
        {
            AddButton(W / 2 - 100, y + 14, 4023, 4025, BtnJoin);
            AddLabel(W / 2 - 76,   y + 16, 999, "Join Guild");
        }

        AddButton(W / 2 + 30, y + 14, 4023, 4025, BtnBack);
        AddLabel(W / 2 + 56,  y + 16, 999, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        switch (info.ButtonID)
        {
            case BtnBack:
                _pm.SendGump(new GuildProgressGump(_pm, _acct));
                return;

            case BtnJoin:
                if (!ClusterFGuildSystem.CanJoin(_pm, _def, out _)) return;
                ClusterFGuildSystem.Join(_pm, _def);
                _pm.SendGump(new GuildProgressGump(_pm, _acct));
                return;
        }
    }
}

// ── GM admin commands ─────────────────────────────────────────────────────────

public static class ClusterFGuildAdminCommands
{
    public static void Configure()
    {
        CommandSystem.Register("ResetGuild", AccessLevel.GameMaster, ResetGuild_OnCommand);
    }

    [Usage("ResetGuild <key|all>")]
    [Description("Resets guild membership, reputation, and currency. " +
                 "Use a guild key (e.g. 'smithing') or 'all' to reset every guild. " +
                 "Targets yourself; use [Admin to target another player first.")]
    private static void ResetGuild_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || pm.Account is not IAccount acct)
            return;

        var key  = e.Length > 0 ? e.GetString(0).Trim() : "all";
        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        if (key.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var joined = new List<string>(data.JoinedGuilds);
            foreach (var g in joined)
                DoReset(pm, data, g);

            pm.SendMessage(0x44, joined.Count > 0
                ? $"Reset {joined.Count} guild{(joined.Count == 1 ? "" : "s")}: {string.Join(", ", joined)}."
                : "No guild memberships to reset.");
        }
        else
        {
            if (!data.JoinedGuilds.Contains(key))
            {
                pm.SendMessage(0x22,
                    $"Not a member of '{key}'. " +
                    $"Current guilds: {(data.JoinedGuilds.Count > 0 ? string.Join(", ", data.JoinedGuilds) : "none")}.");
                return;
            }

            DoReset(pm, data, key);
            pm.SendMessage(0x44, $"Guild '{key}' reset — membership, reputation, currency cleared.");
        }
    }

    private static void DoReset(PlayerMobile pm, ClusterFAccountData data, string key)
    {
        data.JoinedGuilds.Remove(key);
        data.GuildReputation.Remove(key);
        data.GuildCurrency.Remove(key);

        // Guild-specific data cleanup
        if (key.Equals("smithing", StringComparison.OrdinalIgnoreCase))
            data.SmithCommissions.Clear();
    }
}
