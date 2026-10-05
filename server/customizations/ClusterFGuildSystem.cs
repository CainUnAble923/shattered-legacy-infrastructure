using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.MLQuests.Items;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// -- Guild task type ------------------------------------------------------------------------------

public enum GuildTaskType
{
    SkillOnly,
    ItemOnly,
    SkillOrItem,
}

// -- Guild definition -----------------------------------------------------------------------------

public class GuildDef
{
    public string          Key               { get; }
    public string          Name              { get; }
    public string          Pitch             { get; }   // guildmaster's greeting
    public string          TaskDescription   { get; }   // the Apprentice task, shown in the hall page
    public Type            GuildmasterType   { get; }
    public GuildTaskType   TaskType          { get; }
    public SkillName       TaskSkill         { get; }
    public double          TaskSkillMin      { get; }
    public Type?           TaskItemType      { get; }
    public int             TaskItemCount     { get; }
    public int             JoinReputation    { get; }   // rep paid by the Apprentice task (was: on join)
    public int             JoinScrip         { get; }   // scrip paid by the Apprentice task (was: on join)

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

// -- Guild system ---------------------------------------------------------------------------------

/// <summary>
/// Shattered Legacy guild membership system.
///
/// Each character can join multiple guilds. Membership, reputation and scrip are per character
/// (CharacterGuildData, ClusterFAccountData v15, cc-P18 F-7); so is what each character has taken from
/// a guild (GuildStarterRecord, ClusterFGuildStarterPath.cs).
///
/// Join flow (cc-P15, F-9 Decision 3):
///   Talk to a guildmaster -> the guild's hall page (GuildTaskDetailGump) -> [Join], free, one click,
///   rank Initiate. The character gets the guild's tools. A second character on the same account
///   joins on its own (cc-P18); a member asking again gets "Welcome back" and any tools it lacks.
///   The old skill-or-tribute join task is the Apprentice task: meeting it later pays the reputation
///   and scrip joining used to pay and raises the rank. Nothing is consumed to join.
///
/// Commands:
///   [guild   - opens the Guild Directory (GuildProgressGump), the same rows as the board's first page.
/// </summary>
public static class ClusterFGuildSystem
{
    private static readonly Dictionary<string, GuildDef>  _byKey  = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type,   GuildDef>  _byNpc  = new();

    public static IReadOnlyDictionary<string, GuildDef> AllGuilds => _byKey;

    // How close a player must be to a guildmaster to join or take anything.
    public const int GuildmasterRange = 8;

    public static void Configure()
    {
        EnsureRegistered();
        CommandSystem.Register("guild", AccessLevel.Player, OnGuildCommand);
    }

    // Public so the test host, which runs no Configure, can register the guilds.
    public static void EnsureRegistered()
    {
        if (_byKey.Count > 0)
            return;

        Register(new GuildDef(
            "smithing", "Society of Smiths", // cc-P52 Part G (D68): display name only; the key stays "smithing"
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
            "Gears and ingenuity - the foundation of civilization.",
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
            "Demonstrate Musicianship of at least 30.0.",
            typeof(BardGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Musicianship, 30.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "thieves", "Thieves' Den",
            "We see all. Can we trust you? Show us your light fingers.",
            "Demonstrate Stealing of at least 20.0.",
            typeof(ThiefGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Stealing, 20.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "warriors", "Warriors' Brotherhood",
            "Strength alone is not enough. Prove you understand the art of battle.",
            "Demonstrate Tactics of at least 30.0.",
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
            "Open to all citizens - simply speak with a Sanitation Warden and pledge your service.",
            typeof(SanitationWarden),
            GuildTaskType.SkillOnly,
            SkillName.ItemID, 0.0,  // 0.0 min = always eligible
            null, 0,
            // cc-P33 (F-3): no standing (it follows lifetime Clean Up points) and no scrip (Civic Tokens retired).
            joinRep: 0, joinScrip: 0));

        // cc-P15: two guilds for the New Haven halls no existing guild fits (F-9 Decision 1).
        Register(new GuildDef(
            "dojo", "Twin Paths Dojo",
            "Bushido and Ninjitsu, taught side by side in the Ninja Dojo. Pick the path that suits you, or walk both.",
            "Demonstrate Bushido of at least 30.0.",
            typeof(TwinPathsDojoGuildmaster),
            GuildTaskType.SkillOnly,
            SkillName.Bushido, 30.0,
            null, 0,
            joinRep: 25, joinScrip: 10));

        Register(new GuildDef(
            "keepers", "Keepers of the Last Door",
            "Necromancy, Spirit Speak and Forensics, taught in the old Necromancers Guild Hall. Dark work, done carefully.",
            "Demonstrate Necromancy of at least 30.0, or bring 10 Grave Dust as tribute.",
            typeof(KeepersOfTheLastDoorGuildmaster),
            GuildTaskType.SkillOrItem,
            SkillName.Necromancy, 30.0,
            typeof(GraveDust), 10,
            joinRep: 25, joinScrip: 10));

        // A guild's other guildmaster types (the Miners' Compact Liaison) resolve to it too.
        foreach (var loc in GuildLocations.All)
            if (!_byNpc.ContainsKey(loc.NpcType) && _byKey.TryGetValue(loc.GuildKey, out var def))
                _byNpc[loc.NpcType] = def;
    }

    private static void Register(GuildDef def)
    {
        _byKey[def.Key]             = def;
        _byNpc[def.GuildmasterType] = def;
    }

    // -- Public API ------------------------------------------------------------------------------

    public static GuildDef? GetDef(string key) =>
        _byKey.TryGetValue(key, out var d) ? d : null;

    public static GuildDef? GetDefForGuildmaster(Type npcType) =>
        _byNpc.TryGetValue(npcType, out var d) ? d : null;

    /// <summary>Whether this character is in the guild (per character since cc-P18, F-7).</summary>
    public static bool IsJoined(Mobile m, string key) =>
        ClusterFAccountPersistence.GetGuild(m)?.JoinedGuilds.Contains(key) == true;

    /// <summary>Joining is free (F-9 Decision 3): only a character already in the guild is refused.</summary>
    public static bool CanJoin(PlayerMobile pm, GuildDef def, out string reason)
    {
        if (pm.Account is not IAccount acct)
        {
            reason = "Could not resolve account.";
            return false;
        }

        if (IsJoined(pm, def.Key))
        {
            reason = "Already a member.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>The old join task, now the Apprentice task: skill, tribute in the backpack, or either.</summary>
    public static bool MeetsApprenticeTask(PlayerMobile pm, GuildDef def, out string reason)
    {
        switch (def.TaskType)
        {
            case GuildTaskType.SkillOnly:
                if (pm.Skills[def.TaskSkill].Base >= def.TaskSkillMin)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskSkill} of {def.TaskSkillMin:F1}.";
                return false;

            case GuildTaskType.ItemOnly:
                if (GuildResources.Count(pm, GuildCost.Of(def.TaskItemType!, def.TaskItemCount)).Total >= def.TaskItemCount)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskItemCount}x {def.TaskItemType!.Name} in your backpack or bank.";
                return false;

            case GuildTaskType.SkillOrItem:
                if (pm.Skills[def.TaskSkill].Base >= def.TaskSkillMin)
                {
                    reason = string.Empty;
                    return true;
                }
                if (def.TaskItemType != null &&
                    GuildResources.Count(pm, GuildCost.Of(def.TaskItemType, def.TaskItemCount)).Total >= def.TaskItemCount)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"Requires {def.TaskSkill} of {def.TaskSkillMin:F1} or {def.TaskItemCount}x {def.TaskItemType!.Name}.";
                return false;

            default:
                reason = "Unknown task type.";
                return false;
        }
    }

    public static bool IsApprentice(CharacterGuildData data, string key) => data.ApprenticeGuilds.Contains(key);

    /// <summary>
    /// The Apprentice task: pays the reputation and scrip joining used to pay and raises the rank.
    /// Tribute is taken only when the skill half is not met, as the old join did.
    /// </summary>
    public static bool CompleteApprenticeTask(PlayerMobile pm, GuildDef def, out string reason)
    {
        if (pm.Account is null || !IsJoined(pm, def.Key))
        {
            reason = "Join the guild first.";
            return false;
        }

        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        if (IsApprentice(data, def.Key))
        {
            reason = "Your Apprentice task is already done.";
            return false;
        }

        if (!MeetsApprenticeTask(pm, def, out reason))
            return false;

        if (def.TaskType != GuildTaskType.SkillOnly &&
            pm.Skills[def.TaskSkill].Base < def.TaskSkillMin &&
            def.TaskItemType != null)
        {
            // F-11: the tribute comes from the pack, then the bank, all or nothing.
            if (!GuildResources.TryConsume(pm, GuildCost.Of(def.TaskItemType, def.TaskItemCount)))
            {
                reason = $"Requires {def.TaskItemCount}x {def.TaskItemType.Name} in your backpack or bank.";
                return false;
            }
        }

        data.ApprenticeGuilds.Add(def.Key);
        data.AddReputation(def.Key, def.JoinReputation);
        data.AddCurrency(def.Key, def.JoinScrip);

        pm.SendMessage(54, $"You are now {GetRankName(def.Key, data)} of the {def.Name}.");
        pm.SendMessage(999, $"Awarded: {def.JoinReputation} reputation and {def.JoinScrip} scrip with the {def.Name}.");
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Joins this character to the guild (free, rank Initiate) if it is not already a member, and hands
    /// it the guild's tools if it has not had them ("Welcome back" for a member asking again).
    /// </summary>
    public static void Join(PlayerMobile pm, GuildDef def)
    {
        if (pm.Account is null) return;

        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        var newMember = data.JoinedGuilds.Add(def.Key);

        if (newMember)
        {
            pm.SendMessage(54, $"You have joined the {def.Name} as {GetRankName(def.Key, data)}. Joining is free.");
            pm.SendMessage(999, $"Its Apprentice task is waiting when you are ready: {def.TaskDescription}");
        }
        else
        {
            pm.SendMessage(54, $"Welcome back to the {def.Name}.");
        }

        ClusterFGuildStarter.GiveTools(pm, def);

        if (newMember)
            ClusterFLeagueSystem.OnGuildJoined(pm);
    }

    /// <summary>The guild's own join bonus, once per character with its tools (fixes PT-06).</summary>
    public static void GiveJoinBonus(PlayerMobile pm, GuildDef def)
    {
        // Mining: Compact Ore Satchel, Prospector's Logbook, Compact Dispatch Ledger.
        if (def.Key.Equals("mining", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.CompactOreSatchel());
            pm.Backpack.DropItem(new Items.ProspectorsLogbook());
            pm.Backpack.DropItem(new Items.CompactDispatchLedger());
            pm.SendMessage(0x44, "You have been issued a Compact Ore Satchel, a Prospector's Logbook, and a Compact Dispatch Ledger.");
            pm.SendMessage(0x44, "Mined ore will route to the satchel automatically. Colored ore discoveries are logged in the book.");
            pm.SendMessage(0x44, "Use the Dispatch Ledger to access work orders from anywhere in the world.");
        }

        // Smithing: SmithGuildBook and SmithGuildSalvageBag.
        if (def.Key.Equals("smithing", StringComparison.OrdinalIgnoreCase))
        {
            SmithGuildBook.OnSmithingJoined(pm);
            Items.SmithGuildSalvageBag.OnSmithingJoined(pm);
        }

        // Rangers: Outrider's Crook and Hunter's Satchel.
        if (def.Key.Equals("rangers", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.OutridersCrook());
            pm.Backpack.DropItem(new Items.HuntersSatchel());
            pm.SendMessage(0x44, "Welcome to the Rangers' League, Wanderer.");
            pm.SendMessage(0x44, "You have been issued an Outrider's Crook and a Hunter's Satchel.");
            pm.SendMessage(0x44, "Right-click the crook to deliver pets for contracts, shrink bonded animals, or instant-bond.");
            pm.SendMessage(0x44, "Speak with the Outriders' Guildmaster in New Haven to access field contracts.");
        }

        // Foresters: Forester's Logbook and Lumber Satchel.
        if (def.Key.Equals("foresters", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.ForestersLogbook());
            pm.Backpack.DropItem(new Items.ForestersLumberSatchel());
            pm.SendMessage(0x44, "Welcome to the Foresters' Union, Woodcutter.");
            pm.SendMessage(0x44, "You have been issued a Forester's Logbook and a Lumber Satchel.");
            pm.SendMessage(0x44, "Chopped logs and boards route to the satchel automatically. All timber discoveries are recorded in the logbook.");
        }

        // Custodians: a TrashBag (SanitationWardenGump's own join path also calls this; it gives one bag).
        if (def.Key.Equals("custodians", StringComparison.OrdinalIgnoreCase))
            SanitationWarden.OnCustodiansJoined(pm);

        // Artificers' Order: the Essence Satchel and Toolkit.
        if (def.Key.Equals("artificers", StringComparison.OrdinalIgnoreCase) && pm.Backpack != null)
        {
            pm.Backpack.DropItem(new Items.ArtificersSatchel());
            pm.Backpack.DropItem(new Items.ArtificersToolkit());
            pm.SendMessage(0x44, "Welcome to the Artificers' Order.");
            pm.SendMessage(0x44, "You have been issued an Artificers' Essence Satchel and an Artificers' Toolkit.");
            pm.SendMessage(0x44, "The Satchel stores your PropertyEssences and tracks mastery progress.");
            pm.SendMessage(0x44, "The Toolkit lets you imbue and disenchant anywhere - no NPC visit required.");
            pm.SendMessage(0x44, "As your Artificers' Standing grows, you will unlock higher intensity caps and more property slots.");
        }
    }

    // -- The guild hall page ---------------------------------------------------------------------

    public static bool IsNearGuildmaster(PlayerMobile pm, Mobile guildmaster) =>
        guildmaster is { Deleted: false } && guildmaster.Map == pm.Map && pm.InRange(guildmaster, GuildmasterRange);

    /// <summary>A guildmaster of this guild within reach of the player, or null.</summary>
    public static Mobile FindGuildmasterNear(PlayerMobile pm, GuildDef def)
    {
        if (pm.Map == null || pm.Map == Map.Internal)
            return null;

        foreach (var m in pm.Map.GetMobilesInRange(pm.Location, GuildmasterRange))
        {
            if (!m.Deleted && GetDefForGuildmaster(m.GetType()) == def)
                return m;
        }

        return null;
    }

    public static void OpenGuildHall(PlayerMobile pm, GuildDef def, Mobile guildmaster)
    {
        if (pm.Account is not IAccount acct) return;
        pm.CloseGump<GuildTaskDetailGump>();
        pm.SendGump(new GuildTaskDetailGump(pm, def, acct, guildmaster));
    }

    // -- Remote member services routing --------------------------------------------------------

    /// <summary>
    /// Opens the appropriate member services gump for a guild.
    /// Used by the hall page's Services button and the Guild Directory.
    /// For members: routes to the guild's member dashboard.
    /// For non-members: falls back to GuildTaskDetailGump (the hall page).
    /// </summary>
    public static void OpenMemberServices(PlayerMobile pm, GuildDef def, IAccount acct)
    {
        var isMember = IsJoined(pm, def.Key);

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

    // -- Rank name helper -----------------------------------------------------------------------

    /// <summary>
    /// Returns the rank title for a player's standing within a specific guild.
    /// Guild-specific ladders are used where defined; all others fall back to
    /// the generic ladder.
    /// </summary>
    public static string GetRankName(string guildKey, int standing) =>
        guildKey.ToLowerInvariant() switch
        {
            "smithing"    => SmithingRank(standing),
            "rangers"     => OutridersRank(standing),
            "foresters"   => ForestersRank(standing),
            "custodians"  => ClusterFCustodianSystem.GetCustodianRank(standing),
            "artificers"  => ArtificersGuildmasterGump.GetRankName(standing),
            "mining"      => MinersCompactLiaisonGump.GetRankName(standing), // cc-P48: was missing (cc-P47)
            _             => GenericRank(standing),
        };

    // -- Rank index (cc-P48) ----------------------------------------------------------------------
    // One number for "how high in this guild", whatever the guild's own names: 0 = the ladder's first rank
    // (Initiate) to 5 = its top rank. Used by League promotion (ClusterFLeagueRanks), which asks for
    // "Apprentice in 2 guilds" and the like.

    public const int RankInitiate    = 0;
    public const int RankApprentice  = 1;
    public const int RankJourneyman  = 2;
    public const int RankMaster      = 3;
    public const int RankGrandmaster = 4;
    public const int RankTop         = 5;

    private static readonly int[] GenericThresholds = [0, 1_000, 5_000, 15_000, 50_000, 100_000];
    private static readonly int[] SevenRungThresholds = [0, 1_000, 5_000, 15_000, 40_000, 80_000, 150_000];
    private static readonly int[] ArtificerThresholds = [0, 1_000, 5_000, 15_000, 40_000];

    /// <summary>
    /// The standing at which each rank of this guild's ladder starts, lowest first: the same steps GetRankName
    /// takes (GuildRankIndexVerification holds the two together). Generic and Smithing have 6 ranks; Mining,
    /// Outriders and Foresters 7; Artificers and Custodians 5.
    /// </summary>
    public static int[] RankThresholds(string guildKey) =>
        guildKey.ToLowerInvariant() switch
        {
            "rangers" or "foresters" or "mining" => SevenRungThresholds,
            "artificers"                          => ArtificerThresholds,
            "custodians"                          => Array.ConvertAll(ClusterFCustodianSystem.Ranks, r => r.Threshold),
            _                                     => GenericThresholds,
        };

    /// <summary>
    /// 0 (Initiate) to 5 (top rank) for this standing on this guild's ladder. Rungs 0 to 3 are Initiate, Apprentice,
    /// Journeyman and Master on every ladder; the top rung is 5; a seven-rank ladder's two rungs between are both 4,
    /// and a five-rank ladder has no 4.
    /// </summary>
    public static int GuildRankIndex(string guildKey, int standing)
    {
        var thresholds = RankThresholds(guildKey);
        var rung = 0;

        for (var i = 1; i < thresholds.Length; i++)
            if (standing >= thresholds[i])
                rung = i;

        if (rung <= RankMaster)
            return rung;

        return rung == thresholds.Length - 1 ? RankTop : RankGrandmaster;
    }

    /// <summary>
    /// This character's rank index in a guild: by standing, raised to Apprentice once the guild's Apprentice task is
    /// done (the same bump GetRankName(key, data) shows). Membership is not checked here; callers that need a member
    /// check IsJoined.
    /// </summary>
    public static int GuildRankIndex(string guildKey, CharacterGuildData data)
    {
        var index = GuildRankIndex(guildKey, data.GetReputation(guildKey));
        return IsApprentice(data, guildKey) ? Math.Max(index, RankApprentice) : index;
    }

    /// <summary>The generic name of a rank index, as League requirements are written ("Journeyman").</summary>
    public static string RankIndexName(int index) => index switch
    {
        <= RankInitiate  => "Initiate",
        RankApprentice   => "Apprentice",
        RankJourneyman   => "Journeyman",
        RankMaster       => "Master",
        RankGrandmaster  => "Grandmaster",
        _                => "top rank",
    };

    /// <summary>
    /// The rank to show: reputation's rank, raised to the ladder's second rank once the Apprentice
    /// task is done (Apprentice on the generic ladder; each custom ladder keeps its own names).
    /// </summary>
    public static string GetRankName(string guildKey, CharacterGuildData data)
    {
        var standing = data.GetReputation(guildKey);
        if (IsApprentice(data, guildKey))
            standing = Math.Max(standing, SecondRankStanding(guildKey));
        return GetRankName(guildKey, standing);
    }

    // The least standing at which a ladder shows its second rank, found by asking the ladder.
    private static int SecondRankStanding(string guildKey)
    {
        var first = GetRankName(guildKey, 0);
        foreach (var candidate in new[] { 1, 10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000 })
            if (GetRankName(guildKey, candidate) != first)
                return candidate;
        return 0;
    }

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

    // -- [guild command -------------------------------------------------------------------------

    [Usage("guild")]
    [Description("Opens the Guild Directory: every guild, what it teaches, where its guildmaster stands.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnGuildCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;
        pm.CloseGump<GuildProgressGump>();
        e.Mobile.SendGump(new GuildProgressGump(pm, acct));
    }
}

// -- Guild context menu entry ----------------------------------------------------------------------

public class GuildMembershipEntry : ContextMenuEntry
{
    private readonly PlayerMobile _from;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;

    // Cliloc 6146 = "Talk" - confirmed from BaseQuester.TalkNumber default
    public GuildMembershipEntry(PlayerMobile from, GuildDef def, IAccount acct)
        : base(6146, 4)
    {
        _from = from;
        _def  = def;
        _acct = acct;
    }

    public override void OnClick(Mobile from, IEntity target)
    {
        ClusterFGuildSystem.OpenGuildHall(_from, _def, target as Mobile);
    }
}

// -- The Guild Directory -------------------------------------------------------------------------

/// <summary>
/// The Guild Directory (F-9 Decision 2): one row per guild, the skills it teaches, its hall (or its
/// towns, for a guild with posts in several), Show me the way, and the player's rank where joined. The same rows open from the
/// New Haven board (its first page; the 26-quest picker is its "Training quests" tab), from [guild and
/// from the League Registrar's Guild Referrals. The class keeps its old name so every caller of the
/// old guild overview now opens the directory.
/// </summary>
public class GuildProgressGump : Gump
{
    private readonly PlayerMobile        _pm;
    private readonly IAccount?           _acct;
    private readonly NewHavenQuestBoard? _board;

    private const int W        = 760;
    private const int HeaderH  = 140;
    private const int RowH     = 44;
    private const int FooterH  = 48;
    public  const int RowsPerPage = 8;

    // The League row (cc-P48, the 2026-10-01 signpost): above the guild rows on every page, set apart as the
    // umbrella over them. Its name label is at x 20 like a guild row's, above the first guild row (y 140).
    public const int LeagueRowY = 86;
    public const string LeagueRowName = "League of Extraordinary Citizens";

    // Button IDs:
    //   0          = close
    //   50         = Training quests (the board's second tab; only when opened from the board)
    //   60         = Show me the way to the League Registrar (the League row)
    //   100 + i    = Show me the way for guild row i
    //   200 + i    = Services for guild row i (members only)
    //   300 + i    = Contracts for guild row i (members only)
    public const int BtnTrainingQuests = 50;
    public const int BtnLeagueWay      = 60;
    public const int BtnWayBase        = 100;
    public const int BtnServicesBase   = 200;
    public const int BtnContractsBase  = 300;

    public static int PageCount => (Guilds().Count + RowsPerPage - 1) / RowsPerPage;

    /// <summary>
    /// The directory's rows (D41, cc-P23): one per registered guild (ClusterFGuildSystem.AllGuilds),
    /// never one per guildmaster or per post, so a guild with guildmasters in two towns is one row.
    /// In F-9's order, which is the order of each guild's first post in GuildLocations.All; a guild
    /// with no post at all still gets a row, after the rest, by key.
    /// </summary>
    public static List<GuildDef> Guilds()
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < GuildLocations.All.Length; i++)
            order.TryAdd(GuildLocations.All[i].GuildKey, i);

        var guilds = new List<GuildDef>(ClusterFGuildSystem.AllGuilds.Values);
        guilds.Sort((a, b) =>
        {
            var ia = order.TryGetValue(a.Key, out var x) ? x : int.MaxValue;
            var ib = order.TryGetValue(b.Key, out var y) ? y : int.MaxValue;
            return ia != ib ? ia.CompareTo(ib) : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        });
        return guilds;
    }

    // The hall column: the hall for a guild with one post, its towns for a guild with several.
    private static string HallColumn(List<GuildLocation> posts)
    {
        if (posts.Count == 0)
            return "No post";
        if (posts.Count == 1)
            return posts[0].Hall;

        var towns = new List<string>();
        foreach (var post in posts)
            if (!towns.Contains(post.Town))
                towns.Add(post.Town);
        return string.Join(", ", towns);
    }

    public GuildProgressGump(PlayerMobile pm, IAccount acct) : this(pm, acct, null) { }

    public GuildProgressGump(PlayerMobile pm, IAccount? acct, NewHavenQuestBoard? board) : base(40, 40)
    {
        _pm    = pm;
        _acct  = acct;
        _board = board;

        Closable   = true;
        Disposable = true;

        // No account (a test host player) reads as a character that has joined nothing.
        var data        = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial)
            : new CharacterGuildData();
        var guilds      = Guilds();
        var totalH      = HeaderH + RowsPerPage * RowH + FooterH;

        AddPage(0);
        AddBackground(0, 0, W, totalH, 9270);
        AddAlphaRegion(10, 10, W - 20, totalH - 20);

        AddLabel(20, 16, 1154, "Guild Directory");
        AddLabel(20, 38, 999, "Every skill has a guild. Joining is free: talk to its guildmaster, and it hands you your tools.");
        AddLabel(20, 58, 999, $"Member of {data.JoinedGuilds.Count} of {ClusterFGuildSystem.AllGuilds.Count} guilds.");

        if (board != null)
        {
            AddButton(W - 200, 14, 4005, 4007, BtnTrainingQuests);
            AddLabel(W - 165, 16, 1154, "Training quests (26)");
        }

        // The League row: the guild for the guilds, above them and fenced off from them.
        AddImageTiled(10, LeagueRowY - 6, W - 20, 2, 9304);
        var leagueRank = ClusterFLeagueRanks.GetRank(pm);
        AddLabel(20, LeagueRowY, 53, LeagueRowName);
        AddLabel(230, LeagueRowY, 999, "Field Office, New Haven");
        AddLabel(450, LeagueRowY, leagueRank > 0 ? ClusterFLeagueRanks.LabelHue(leagueRank) : 999,
            leagueRank > 0 ? $"Rank: {ClusterFLeagueRanks.RankName(leagueRank)}" : "Not registered");
        AddHtml(20, LeagueRowY + 20, 420, 20,
            "<BASEFONT COLOR=#AAAAAA>Ties every guild together. Its Registrar keeps your League rank.</BASEFONT>", false, false);
        AddButton(450, LeagueRowY + 18, 4005, 4007, BtnLeagueWay);
        AddLabel(485, LeagueRowY + 20, 999, "Show me the way");

        AddImageTiled(10, HeaderH - 12, W - 20, 2, 9304);

        for (var page = 1; page <= PageCount; page++)
        {
            AddPage(page);

            for (var row = 0; row < RowsPerPage; row++)
            {
                var i = (page - 1) * RowsPerPage + row;
                if (i >= guilds.Count)
                    break;

                var def      = guilds[i];
                var posts    = GuildLocations.For(def.Key);
                var y        = HeaderH + row * RowH;
                var isMember = data.JoinedGuilds.Contains(def.Key);
                var found    = posts.Exists(p => p.Find() != null);

                AddLabel(20, y, isMember ? 1154 : 999, def.Name);
                AddLabel(230, y, 999, HallColumn(posts));

                if (!found)
                    AddLabel(450, y, 37, "Guildmaster missing");
                else if (isMember)
                    AddLabel(450, y, 68, $"Rank: {ClusterFGuildSystem.GetRankName(def.Key, data)}");
                else
                    AddLabel(450, y, 999, "Not joined");

                AddHtml(20, y + 20, 420, 20,
                    $"<BASEFONT COLOR=#AAAAAA>{GuildSkillTable.Describe(def.Key)}</BASEFONT>", false, false);

                AddButton(450, y + 18, 4005, 4007, BtnWayBase + i);
                AddLabel(485, y + 20, 999, "Show me the way");

                if (isMember)
                {
                    AddButton(600, y + 18, 4011, 4012, BtnServicesBase + i);
                    AddLabel(625, y + 20, 1154, "Services");
                    AddButton(600, y, 4011, 4012, BtnContractsBase + i);
                    AddLabel(625, y + 2, 999, "Contracts");
                }

                AddImageTiled(10, y + RowH - 4, W - 20, 1, 9304);
            }

            var navY = HeaderH + RowsPerPage * RowH + 8;
            if (page < PageCount)
            {
                AddButton(W - 60, navY, 4005, 4007, 0, GumpButtonType.Page, page + 1);
                AddLabel(W - 130, navY + 2, 999, "More guilds");
            }

            if (page > 1)
            {
                AddButton(20, navY, 4014, 4016, 0, GumpButtonType.Page, page - 1);
                AddLabel(55, navY + 2, 999, "Back");
            }
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm || info.ButtonID == 0) return;

        var guilds = Guilds();

        if (info.ButtonID == BtnTrainingQuests)
        {
            _board?.OpenTrainingQuests(_pm);
            return;
        }

        if (info.ButtonID == BtnLeagueWay)
        {
            _pm.SendMessage(0x44, ClusterFLeagueSystem.ShowTheWayToRegistrar(_pm));
            return;
        }

        if (info.ButtonID >= BtnWayBase && info.ButtonID < BtnWayBase + guilds.Count)
        {
            _pm.SendMessage(0x44, ClusterFGuildStarter.ShowTheWay(_pm, guilds[info.ButtonID - BtnWayBase].Key));
            return;
        }

        if (info.ButtonID >= BtnServicesBase && info.ButtonID < BtnServicesBase + guilds.Count)
        {
            var def = guilds[info.ButtonID - BtnServicesBase];
            if (_acct != null && ClusterFGuildSystem.IsJoined(_pm, def.Key))
                ClusterFGuildSystem.OpenMemberServices(_pm, def, _acct);
            return;
        }

        if (info.ButtonID >= BtnContractsBase && info.ButtonID < BtnContractsBase + guilds.Count)
        {
            var def = guilds[info.ButtonID - BtnContractsBase];
            if (_acct != null && ClusterFGuildSystem.IsJoined(_pm, def.Key))
                _pm.SendGump(new GuildContractLedgerGump(_pm, def.Key));
        }
    }
}

// -- The guild hall page ---------------------------------------------------------------------------

/// <summary>
/// A guild's page at its guildmaster (F-9 Decisions 3 and 4): join (free, one click), take this
/// character's tools ("Welcome back" on a member account), starter items, the Apprentice task, and a
/// way on to the guild's own services. Anything that hands something over needs a guildmaster of the
/// guild within reach, however the page was opened; every button is judged again at the click.
/// </summary>
public class GuildTaskDetailGump : Gump
{
    private readonly PlayerMobile _pm;
    private readonly GuildDef     _def;
    private readonly IAccount     _acct;
    private readonly Mobile?      _guildmaster;

    private const int W = 520;

    public const int BtnClose      = 0;
    public const int BtnJoin       = 1;
    public const int BtnTools      = 2;
    public const int BtnStarter    = 3;
    public const int BtnApprentice = 4;
    public const int BtnServices   = 5;
    public const int BtnDirectory  = 6;

    public GuildTaskDetailGump(PlayerMobile pm, GuildDef def, IAccount acct) : this(pm, def, acct, null) { }

    public GuildTaskDetailGump(PlayerMobile pm, GuildDef def, IAccount acct, Mobile? guildmaster) : base(80, 80)
    {
        _pm          = pm;
        _def         = def;
        _acct        = acct;
        _guildmaster = ClusterFGuildSystem.IsNearGuildmaster(pm, guildmaster)
            ? guildmaster
            : ClusterFGuildSystem.FindGuildmasterNear(pm, def);

        Closable   = true;
        Disposable = true;

        var data      = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial);
        var isMember  = data.JoinedGuilds.Contains(def.Key);
        var hasTools  = ClusterFGuildStarter.HasTools(pm, def.Key);
        var atMaster  = _guildmaster != null;
        var starters  = GuildStarterItems.For(def.Key).Length;
        var isApprentice = ClusterFGuildSystem.IsApprentice(data, def.Key);

        const int H = 400;
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);

        AddLabel(20, 14, 1154, def.Name);
        AddImageTiled(10, 36, W - 20, 2, 9304);
        AddHtml(20, 44, W - 40, 36, $"<BASEFONT COLOR=#DDDDDD><I>{def.Pitch}</I></BASEFONT>", false, false);
        AddHtml(20, 82, W - 40, 36, $"<BASEFONT COLOR=#AAAAAA>Teaches: {GuildSkillTable.Describe(def.Key)}</BASEFONT>", false, false);

        if (_guildmaster is BaseGuildmaster)
            AddLabel(20, 118, 999, "Separate from the stock trade guild (say \"join\", 500 gold). One does not join the other.");

        AddImageTiled(10, 140, W - 20, 2, 9304);

        var y = 150;
        if (!isMember)
        {
            AddLabel(20, y, 999, "Joining is free and takes one click. You start as an Initiate.");
            y += 22;
            if (atMaster)
            {
                AddButton(20, y, 4023, 4025, BtnJoin);
                AddLabel(55, y + 2, 1154, "Join the guild");
            }
            else
            {
                AddLabel(20, y + 2, 37, "Talk to this guild's guildmaster to join.");
            }
            y += 30;
        }
        else
        {
            AddLabel(20, y, 68, $"Member. Rank: {ClusterFGuildSystem.GetRankName(def.Key, data)}.");
            y += 22;

            if (!hasTools)
            {
                AddLabel(20, y, 1154, "Welcome back. This character has not taken its tools yet.");
                y += 22;
                if (atMaster)
                {
                    AddButton(20, y, 4023, 4025, BtnTools);
                    AddLabel(55, y + 2, 1154, "Take your tools");
                    y += 30;
                }
            }
        }

        if (starters > 0 && atMaster)
        {
            AddButton(20, y, 4005, 4007, BtnStarter);
            AddLabel(55, y + 2, 999, $"Starter items ({starters})");
            y += 28;
        }

        AddImageTiled(10, y + 4, W - 20, 2, 9304);
        y += 12;

        AddLabel(20, y, 999, "Apprentice task:");
        AddHtml(20, y + 20, W - 40, 36, $"<BASEFONT COLOR=#DDDDDD>{def.TaskDescription}</BASEFONT>", false, false);
        y += 58;

        if (!isMember)
        {
            AddLabel(20, y, 999, "Open to members.");
        }
        else if (isApprentice)
        {
            AddLabel(20, y, 68, "Done.");
        }
        else if (ClusterFGuildSystem.MeetsApprenticeTask(pm, def, out var reason))
        {
            if (atMaster)
            {
                AddButton(20, y, 4023, 4025, BtnApprentice);
                AddLabel(55, y + 2, 1154, $"Complete it: +{def.JoinReputation} reputation, +{def.JoinScrip} scrip");
            }
            else
            {
                AddLabel(20, y, 68, "Met. Complete it at the guildmaster.");
            }
        }
        else
        {
            AddLabel(20, y, 37, reason);
        }

        AddImageTiled(10, H - 50, W - 20, 2, 9304);
        if (isMember)
        {
            AddButton(20, H - 38, 4011, 4012, BtnServices);
            AddLabel(45, H - 36, 999, "Guild services");
        }

        AddButton(180, H - 38, 4011, 4012, BtnDirectory);
        AddLabel(205, H - 36, 999, "Guild Directory");

        AddButton(W - 100, H - 38, 4017, 4019, BtnClose);
        AddLabel(W - 65, H - 36, 999, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm)
            return;

        var atMaster = ClusterFGuildSystem.IsNearGuildmaster(_pm, _guildmaster);

        switch (info.ButtonID)
        {
            case BtnJoin:
                if (atMaster && ClusterFGuildSystem.CanJoin(_pm, _def, out _))
                    ClusterFGuildSystem.Join(_pm, _def);
                break;

            case BtnTools:
                if (atMaster && ClusterFGuildSystem.IsJoined(_pm, _def.Key))
                    ClusterFGuildSystem.Join(_pm, _def); // a member: "Welcome back" and this character's tools
                break;

            case BtnStarter:
                if (atMaster)
                    _pm.SendGump(new GuildStarterItemsGump(_pm, _def, _guildmaster));
                return;

            case BtnApprentice:
                if (atMaster && !ClusterFGuildSystem.CompleteApprenticeTask(_pm, _def, out var reason))
                    _pm.SendMessage(0x22, reason);
                break;

            case BtnServices:
                if (ClusterFGuildSystem.IsJoined(_pm, _def.Key))
                    ClusterFGuildSystem.OpenMemberServices(_pm, _def, _acct);
                return;

            case BtnDirectory:
                _pm.SendGump(new GuildProgressGump(_pm, _acct));
                return;

            default:
                return;
        }

        ClusterFGuildSystem.OpenGuildHall(_pm, _def, _guildmaster);
    }
}

// -- GM admin commands ---------------------------------------------------------------------------

public static class ClusterFGuildAdminCommands
{
    public static void Configure()
    {
        CommandSystem.Register("ResetGuild", AccessLevel.GameMaster, ResetGuild_OnCommand);
    }

    [Usage("ResetGuild <key|all>")]
    [Description("Resets this character's guild membership, reputation, and currency. " +
                 "Use a guild key (e.g. 'smithing') or 'all' to reset every guild. " +
                 "Targets yourself; use [Admin to target another player first.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void ResetGuild_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || pm.Account is not IAccount acct)
            return;

        var key  = e.Length > 0 ? e.GetString(0).Trim() : "all";
        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);

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
            pm.SendMessage(0x44, $"Guild '{key}' reset - membership, reputation, currency cleared.");
        }
    }

    private static void DoReset(PlayerMobile pm, CharacterGuildData data, string key)
    {
        data.JoinedGuilds.Remove(key);
        data.GuildReputation.Remove(key);
        data.GuildCurrency.Remove(key);
        data.ApprenticeGuilds.Remove(key);
        ClusterFAccountPersistence.GetGuildStarter(pm)?.ToolsTaken.Remove(key);

        // Guild-specific data cleanup
        if (key.Equals("smithing", StringComparison.OrdinalIgnoreCase))
            data.SmithCommissions.Clear();

        // cc-P33 (F-3): a Custodian's rank is its lifetime Clean Up points, so resetting the guild resets those too.
        if (key.Equals("custodians", StringComparison.OrdinalIgnoreCase))
            Server.Engines.Points.CleanUpBritanniaData.Instance.RemoveEntry(pm);
    }
}
