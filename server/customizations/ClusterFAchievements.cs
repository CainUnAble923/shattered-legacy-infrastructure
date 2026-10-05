using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

using T = Server.AchievementTrigger;

namespace Server;

// -- Achievement trigger (cc-P55 Part F, bug-list D73) -------------------------
//
// What awards an achievement, as data: the grant code below reads each achievement's kind and threshold from here
// (GrantCounted, GrantSkills, GrantEvent), and the "(Earned: ...)" line is written from the same object, so the number a
// player is shown is the number that awarded it. Before cc-P55 every threshold was a literal in the grant code.

public enum TriggerKind
{
    FirstLogin,
    Kills,
    OldHavenMageKills,
    KillDrelgor,
    PlayerKill,
    UndeadKills,
    RatmanKills,
    OrcKills,
    OldHavenKills,
    FirstDeath,
    AnySkill,
    SkillsAtLevel,
    SpecificSkill,
    VisitOldHaven,
    VisitFacet,
    VisitAllFacets,
    JoinLeague,
    ReadDispatch,
    GuildReferral,
    JoinGuild,
    MineColoredOre,
    ColoredOreMined,
    MineInFelucca,
    OreTypesDiscovered,
    JacobsPickaxe,
    SurveyReport,
    TreasureMap,
    CraftAny,
    CraftExceptional,
    CraftSmithed,
    CraftTailored,
    IngotsSmelted,
    BankGold,
    NewHavenQuests,
    AchievementPoints,
    Renown,
    Logins,
}

public sealed class AchievementTrigger
{
    public TriggerKind Kind      { get; }
    /// <summary>The count that awards it (1 for a one-time event; for SkillsAtLevel, the number of skills).</summary>
    public int         Threshold { get; }
    /// <summary>The skill level for AnySkill, SkillsAtLevel and SpecificSkill; 0 otherwise.</summary>
    public int         Level     { get; }
    public SkillName?  OnSkill   { get; }
    public string?     FacetName { get; }

    private AchievementTrigger(TriggerKind kind, int threshold, int level, SkillName? skill, string? facet)
    {
        Kind      = kind;
        Threshold = threshold;
        Level     = level;
        OnSkill   = skill;
        FacetName = facet;
    }

    public static AchievementTrigger Event(TriggerKind kind)           => new(kind, 1, 0, null, null);
    public static AchievementTrigger Count(TriggerKind kind, int n)    => new(kind, n, 0, null, null);
    public static AchievementTrigger AnySkill(int level)               => new(TriggerKind.AnySkill, 1, level, null, null);
    public static AchievementTrigger SkillsAt(int count, int level)    => new(TriggerKind.SkillsAtLevel, count, level, null, null);
    public static AchievementTrigger Skill(SkillName skill, int level) => new(TriggerKind.SpecificSkill, 1, level, skill, null);
    public static AchievementTrigger Facet(string facet)               => new(TriggerKind.VisitFacet, 1, 0, null, facet);

    /// <summary>True for the kinds that test a skill level (the ones Part G checks against the 200 cap).</summary>
    public bool IsSkillLevel => Kind is TriggerKind.AnySkill or TriggerKind.SkillsAtLevel or TriggerKind.SpecificSkill;

    /// <summary>What was done, in plain words, with the trigger's own numbers.</summary>
    public string Describe()
    {
        var n = Threshold;
        return Kind switch
        {
            TriggerKind.FirstLogin         => "logged in for the first time",
            TriggerKind.Kills              => n == 1 ? "slew your first creature" : $"slew {n:N0} creatures",
            TriggerKind.OldHavenMageKills  => $"slew {n:N0} Old Haven Mages",
            TriggerKind.KillDrelgor        => "slew Drelgor the Impaler",
            TriggerKind.PlayerKill         => "killed another player",
            TriggerKind.UndeadKills        => $"slew {n:N0} undead",
            TriggerKind.RatmanKills        => $"slew {n:N0} ratmen",
            TriggerKind.OrcKills           => $"slew {n:N0} orcs",
            TriggerKind.OldHavenKills      => $"slew {n:N0} creatures in Old Haven",
            TriggerKind.FirstDeath         => "died for the first time",
            TriggerKind.AnySkill           => $"reached {Level:N0} in a skill",
            TriggerKind.SkillsAtLevel      => $"reached {Level:N0} in {n:N0} skills",
            TriggerKind.SpecificSkill      => $"reached {Level:N0} in {SkillInfo.Table[(int)OnSkill!.Value].Name}",
            TriggerKind.VisitOldHaven      => "found the ruins of Old Haven",
            TriggerKind.VisitFacet         => $"set foot in {FacetName}",
            TriggerKind.VisitAllFacets     => "visited all five facets",
            TriggerKind.JoinLeague         => "registered with the League",
            TriggerKind.ReadDispatch       => "read the League Dispatch",
            TriggerKind.GuildReferral      => "received a guild referral from the League",
            TriggerKind.JoinGuild          => "joined a professional guild",
            TriggerKind.MineColoredOre     => "mined colored ore",
            TriggerKind.ColoredOreMined    => $"mined {n:N0} colored ore",
            TriggerKind.MineInFelucca      => "mined colored ore in Felucca",
            TriggerKind.OreTypesDiscovered => $"discovered {n:N0} ore types",
            TriggerKind.JacobsPickaxe      => "carried one of Jacob's Pickaxes",
            TriggerKind.SurveyReport       => "reported an ore discovery to the Survey Archivist",
            TriggerKind.TreasureMap        => "dug up a treasure map chest",
            TriggerKind.CraftAny           => "crafted an item",
            TriggerKind.CraftExceptional   => "crafted an exceptional item",
            TriggerKind.CraftSmithed       => "smithed an item",
            TriggerKind.CraftTailored      => "tailored an item",
            TriggerKind.IngotsSmelted      => $"smelted {n:N0} ingots",
            TriggerKind.BankGold           => $"held {n:N0} gold in your bank",
            TriggerKind.NewHavenQuests     => n == 1 ? "finished a New Haven trainer quest" : $"finished {n:N0} New Haven trainer quests",
            TriggerKind.AchievementPoints  => $"reached {n:N0} Achievement Points",
            TriggerKind.Renown             => $"reached {n:N0} Renown",
            TriggerKind.Logins             => $"logged in {n:N0} times",
            _                              => Kind.ToString()
        };
    }
}

// ── Achievement category ──────────────────────────────────────────────────────

public enum AchievementCategory
{
    Combat      = 0,
    Skills      = 1,
    Exploration = 2,
    Mining      = 3,
    Crafting    = 4,
    Legacy      = 5,
    Discovery   = 6,
    Collection  = 7,
    League      = 8,
}

// ── Achievement definition ────────────────────────────────────────────────────

public class AchievementDef
{
    public string              Key               { get; }
    public string              Title             { get; }
    public string              Description       { get; }
    /// <summary>
    /// Short snarky one-liner from the System. Shown prominently in the earn popup.
    /// If empty, the popup falls back to Description.
    /// </summary>
    public string              FlavorText        { get; }
    public AchievementCategory Category          { get; }
    public int                 AP                { get; }
    public int                 Renown            { get; }

    /// <summary>
    /// UO item graphic ID shown as an icon in the earn popup and achievement list.
    /// 0 = no icon.
    /// </summary>
    public int                 ItemGumpId        { get; }

    /// <summary>
    /// When true the achievement is invisible in the locked list — players
    /// won't know it exists until they earn it. Earned hidden achievements
    /// display normally with a [Secret] tag.
    /// </summary>
    public bool                Hidden            { get; }

    /// <summary>
    /// Key of an achievement that must be earned before this one can be granted.
    /// null = no prerequisite.
    /// </summary>
    public string?             PrerequisiteKey   { get; }

    /// <summary>
    /// Item types to instantiate and drop into the player's backpack on earn.
    /// Empty array = no physical reward (AP + Renown only).
    /// </summary>
    public Type[]              RewardItems       { get; }

    public string?             ProgressCounter   { get; } // null = no progress bar

    /// <summary>Target count for the progress bar: the trigger's own threshold (cc-P55; it was a second copy).</summary>
    public int                 ProgressThreshold => ProgressCounter != null ? Trigger.Threshold : 0;

    /// <summary>cc-P55 Part F: what awards it; the grant code reads it, and EarnedLine is written from it.</summary>
    public AchievementTrigger  Trigger           { get; }

    /// <summary>
    /// cc-P55 Part G (bug-list D75, Chase 2026-10-05): never awarded again, left out of the lists and the progress count.
    /// Characters who already hold it keep it (and its AP and Renown); their list shows it under "Retired".
    /// </summary>
    public bool                Retired           { get; }

    /// <summary>The plain line shown with the flavor text, e.g. "(Earned: slew 100 creatures)".</summary>
    public string              EarnedLine        => $"(Earned: {Trigger.Describe()})";

    public AchievementDef(string key, string title, string desc,
                          AchievementCategory cat, int ap, int renown,
                          AchievementTrigger trigger,
                          string flavorText        = "",
                          int    itemGumpId        = 0,
                          bool   hidden            = false,
                          string? prerequisiteKey  = null,
                          Type[]? rewardItems      = null,
                          string? progressCounter  = null,
                          bool    retired          = false)
    {
        Key               = key;
        Title             = title;
        Description       = desc;
        FlavorText        = flavorText;
        Category          = cat;
        AP                = ap;
        Renown            = renown;
        Trigger           = trigger;
        ItemGumpId        = itemGumpId;
        Hidden            = hidden;
        PrerequisiteKey   = prerequisiteKey;
        RewardItems       = rewardItems ?? Array.Empty<Type>();
        ProgressCounter   = progressCounter;
        Retired           = retired;
    }
}

// ── Achievement system ────────────────────────────────────────────────────────

/// <summary>
/// Phase 1 achievement framework for Shattered Legacy.
///
/// Achievements are defined in code via RegisterAll(). Earned state and
/// kill/event counters are serialized by ClusterFAchievementPersistence.
///
/// Player command:  [achievements
/// Admin commands:  [ClusterFAchievement grant|revoke|info|list [args]
///
/// External systems can call notification APIs directly:
///   ClusterFAchievementSystem.NotifyKill(pm, victim)
///   ClusterFAchievementSystem.NotifySkillValue(pm, skillValue)
///   ClusterFAchievementSystem.NotifyOldHavenVisit(pm)
///
/// Skill achievements are also scanned on every login so they cannot be
/// missed even if the inline notification path was not yet wired.
/// </summary>
public static class ClusterFAchievementSystem
{
    private static readonly Dictionary<string, AchievementDef> _defs =
        new(StringComparer.OrdinalIgnoreCase);

    // Per-account earned sets: username -> keys of earned achievements.
    private static readonly Dictionary<string, HashSet<string>> _earned =
        new(StringComparer.OrdinalIgnoreCase);

    // Per-account named counters: username -> { "kills" -> 1234, ... }
    private static readonly Dictionary<string, Dictionary<string, int>> _counters =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, AchievementDef> Definitions => _defs;

    // ── Configure ─────────────────────────────────────────────────────────

    public static void Configure()
    {
        RegisterAll();
        CommandSystem.Register("achievements",        AccessLevel.Player,        OnAchievementsCommand);
        CommandSystem.Register("ClusterFAchievement", AccessLevel.Administrator, OnAdminCommand);
    }

    // ── Achievement definitions ────────────────────────────────────────────

    private static void RegisterAll()
    {
        // ── Item graphic IDs used below ───────────────────────────────────
        // 0x14EC = Treasure Map      0x0F5E = Broadsword    0x0F49 = Axe
        // 0x0E3B = Spellbook         0x0E34 = Scroll        0x0F26 = Diamond
        // 0x0FF4 = Brown Book        0x1B76 = Metal Shield  0x0E86 = Pickaxe
        // (Add more as new achievements are defined.)

        // ── Exploration ──────────────────────────────────────────────────
        Reg("exploration.citizen",
            "Still Breathing",
            "Log in to Shattered Legacy for the first time. The System has formally acknowledged your continued existence.",
            AchievementCategory.Exploration, ap: 10, renown: 20,
            trigger:          T.Event(TriggerKind.FirstLogin),
            flavor:      "The bar was on the floor. You cleared it.",
            itemGumpId:  0x14EC);   // Treasure Map — a world to explore

        // ── Combat ───────────────────────────────────────────────────────
        Reg("combat.first_blood",
            "Oh Look, It's Dead",
            "Slay your first creature. Something has died because of you. The System has logged this.",
            AchievementCategory.Combat, ap: 5, renown: 5,
            trigger:          T.Count(TriggerKind.Kills, 1),
            flavor:           "You are, by definition, a killer now. Congratulations.",
            itemGumpId:       0x0F5E,   // Broadsword
            progressCounter:  "kills");

        Reg("combat.century",
            "Getting Into It",
            "Slay 100 creatures. You've developed what the System charitably calls a process.",
            AchievementCategory.Combat, ap: 15, renown: 25,
            trigger:          T.Count(TriggerKind.Kills, 100),
            flavor:           "They had families. Probably.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.first_blood",
            progressCounter:  "kills");

        Reg("combat.thousand",
            "This Is Fine",
            "Slay 1,000 creatures. You have personally ended more lives than most Britannians will ever encounter.",
            AchievementCategory.Combat, ap: 35, renown: 75,
            trigger:          T.Count(TriggerKind.Kills, 1_000),
            flavor:           "The local monster population has filed a formal grievance.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.century",
            progressCounter:  "kills");

        Reg("combat.ten_thousand",
            "Extremely Normal Behavior",
            "Slay 10,000 creatures. At this point you are less an adventurer and more a geological event with opinions.",
            AchievementCategory.Combat, ap: 100, renown: 250,
            trigger:          T.Count(TriggerKind.Kills, 10_000),
            flavor:           "The System is choosing to look the other way.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.thousand",
            hidden:           true,     // secret — players don't see this until earned
            progressCounter:  "kills");

        Reg("combat.oldhaven_mage",
            "Specific Grudge",
            "Slay 25 Old Haven Mages. Whatever they did to earn this level of focused personal attention, the System declines to investigate.",
            AchievementCategory.Combat, ap: 20, renown: 50,
            trigger:          T.Count(TriggerKind.OldHavenMageKills, 25),
            flavor:          "You really don't like these guys.",
            itemGumpId:      0x0E3B,    // Spellbook
            progressCounter: "oldhaven_mage_kills");

        Reg("combat.drelgor",
            "You Fought a Man Called 'The Impaler' and Won",
            "Slay Drelgor the Impaler. His title was a warning. You did not take the hint. You were correct not to.",
            AchievementCategory.Combat, ap: 25, renown: 100,
            trigger:          T.Event(TriggerKind.KillDrelgor),
            flavor:     "Bold strategy. Unambiguously effective.",
            itemGumpId: 0x0F49,         // Axe — heavy weapon energy
            hidden:     true);          // secret — finding Drelgor is the discovery

        // ── Skills ───────────────────────────────────────────────────────
        Reg("skills.apprentice",
            "You've Read the Introduction",
            "Reach 50 in any skill. You now understand approximately one-sixth of something. The journey has, technically, begun.",
            AchievementCategory.Skills, ap: 5, renown: 10,
            trigger:          T.AnySkill(50),
            flavor:     "A promising start. The bar is currently underground.",
            itemGumpId: 0x0E34);        // Scroll

        Reg("skills.journeyman",
            "Dangerously Competent",
            "Reach 100 in any skill. Your incompetence is no longer immediately life-threatening. Progress.",
            AchievementCategory.Skills, ap: 15, renown: 35,
            trigger:          T.AnySkill(100),
            flavor:          "Most people stop here. You don't seem like most people.",
            itemGumpId:      0x0E34,    // Scroll
            prerequisiteKey: "skills.apprentice");

        Reg("skills.master",
            "Unsettling Dedication",
            "Reach 200 in any skill. You've gone well past the point where normal people stop and develop hobbies. The System respects this, cautiously.",
            AchievementCategory.Skills, ap: 40, renown: 100,
            trigger:          T.AnySkill(200),
            flavor:          "Whatever you used to do with your free time, this replaced it.",
            itemGumpId:      0x0E34,    // Scroll
            prerequisiteKey: "skills.journeyman");

        Reg("skills.grandmaster",
            "What Have You Done With Your Life (Respect)",
            "Reach 300 in any skill. The absolute ceiling has been touched. The System is both deeply impressed and quietly concerned.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            trigger:          T.AnySkill(300),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "The ceiling has a handprint on it now. That's yours.",
            itemGumpId:      0x0F26,    // Diamond — only fitting
            prerequisiteKey: "skills.master");

        // ── Discovery ────────────────────────────────────────────────────
        Reg("discovery.old_haven",
            "A Bad Neighborhood",
            "Discover the ruins of Old Haven. Nobody lives there anymore. That is, objectively, information you now possess.",
            AchievementCategory.Discovery, ap: 15, renown: 35,
            trigger:          T.Event(TriggerKind.VisitOldHaven),
            flavor:     "The real treasure was the foreboding atmosphere.",
            itemGumpId: 0x14EC);        // Treasure Map

        // ── League ───────────────────────────────────────────────────────
        Reg("league.registered_citizen",
            "Officially a Problem",
            "Register with the League of Extraordinary Citizens. You are now in the database. This cannot be undone.",
            AchievementCategory.League, ap: 10, renown: 25,
            trigger:          T.Event(TriggerKind.JoinLeague),
            flavor:     "They can't un-know this. Neither can you.",
            itemGumpId: 0x0FF4);        // Brown Book — your file

        Reg("league.first_dispatch",
            "Minimally Informed",
            "Read the League Dispatch for the first time. You are now among the people who technically know what's going on.",
            AchievementCategory.League, ap: 5, renown: 10,
            trigger:          T.Event(TriggerKind.ReadDispatch),
            flavor:          "Information received. Presumably retained.",
            itemGumpId:      0x0E34,    // Scroll — the dispatch
            prerequisiteKey: "league.registered_citizen");

        Reg("league.first_referral",
            "Someone Vouched For You",
            "Receive a guild referral from the League. A professional organization wants to meet you. Please don't make it weird.",
            AchievementCategory.League, ap: 5, renown: 10,
            trigger:          T.Event(TriggerKind.GuildReferral),
            flavor:          "They seem excited. It would be a shame to disappoint them.",
            itemGumpId:      0x0FF4,    // Brown Book
            prerequisiteKey: "league.registered_citizen");

        Reg("league.guildbound",
            "You're Someone's Problem Now",
            "Join your first professional guild. They have, in some capacity, accepted responsibility for you.",
            AchievementCategory.League, ap: 15, renown: 50,
            trigger:          T.Event(TriggerKind.JoinGuild),
            flavor:          "Welcome to the family. There's a dues structure.",
            itemGumpId:      0x1B76,    // Metal Shield — belonging to something
            prerequisiteKey: "league.first_referral");

        // ── Combat additions ──────────────────────────────────────────────
        // 0x0F5E = Broadsword   0x0F49 = Battle Axe   0x0E34 = Scroll

        Reg("combat.five_hundred",
            "Hitting Your Stride",
            "Slay 500 creatures. The monsters have noticed there is a pattern here.",
            AchievementCategory.Combat, ap: 25, renown: 60,
            trigger:          T.Count(TriggerKind.Kills, 500),
            flavor:          "Consistent. That's one word for it.",
            itemGumpId:      0x0F5E,
            prerequisiteKey: "combat.century",
            progressCounter: "kills");

        Reg("combat.five_thousand",
            "You Have a Type (It's Dead)",
            "Slay 5,000 creatures. You have ended more lives than most natural disasters.",
            AchievementCategory.Combat, ap: 70, renown: 175,
            trigger:          T.Count(TriggerKind.Kills, 5_000),
            flavor:          "The System has no further commentary. Carry on.",
            itemGumpId:      0x0F5E,
            hidden:          true,
            prerequisiteKey: "combat.thousand",
            progressCounter: "kills");

        Reg("combat.undead_hunter",
            "They Were Already Dead Once",
            "Slay 50 undead. You've dispatched the dispatched. Twice. The System has moved on from this word.",
            AchievementCategory.Combat, ap: 15, renown: 35,
            trigger:          T.Count(TriggerKind.UndeadKills, 50),
            flavor:          "It barely counts. The System counted it anyway.",
            itemGumpId:      0x0F5E,
            progressCounter: "undead_kills");

        Reg("combat.rat_problem",
            "Pest Control at Scale",
            "Slay 25 ratmen. Whatever they did, they seem to have done it in large numbers.",
            AchievementCategory.Combat, ap: 10, renown: 20,
            trigger:          T.Count(TriggerKind.RatmanKills, 25),
            flavor:          "The problem has been addressed. Aggressively.",
            itemGumpId:      0x0F49,
            progressCounter: "ratman_kills");

        Reg("combat.orc_grudge",
            "Made Your Position Clear to the Orcs",
            "Slay 50 orcs. You've formed and acted on very strong opinions about the orcish community.",
            AchievementCategory.Combat, ap: 15, renown: 35,
            trigger:          T.Count(TriggerKind.OrcKills, 50),
            flavor:          "The orcish community has received your feedback.",
            itemGumpId:      0x0F49,
            progressCounter: "orc_kills");

        Reg("combat.first_death",
            "Oh. So THAT'S What Resurrection Feels Like.",
            "Die for the first time. You have now experienced mortality from the inside. You got better.",
            AchievementCategory.Combat, ap: 5, renown: 0,
            trigger:          T.Event(TriggerKind.FirstDeath),
            flavor:          "The System notes you got better. Barely.",
            itemGumpId:      0x0E34);   // Scroll — resurrection scroll vibe

        Reg("combat.pvp_first",
            "Morally Complicated",
            "Kill another player. The System is not here to judge. The System has logged it.",
            AchievementCategory.Combat, ap: 15, renown: 50,
            trigger:          T.Event(TriggerKind.PlayerKill),
            flavor:          "Technically legal in at least one facet.",
            itemGumpId:      0x0F5E,
            hidden:          true);     // Surprise when you do it

        // ── Exploration additions ─────────────────────────────────────────
        // 0x14EC = Treasure Map   0x0F26 = Diamond

        Reg("exploration.trammel",
            "Legal Jurisdiction Acquired",
            "Set foot in Trammel. The safe side. Statistically the permanent address of most Britannians.",
            AchievementCategory.Exploration, ap: 5, renown: 10,
            trigger:          T.Facet("Trammel"),
            flavor:          "The one where monsters are less likely to specifically seek you out.",
            itemGumpId:      0x14EC);

        Reg("exploration.felucca",
            "Bold Move. Noted.",
            "Set foot in Felucca. Some things don't want you here. Others are actively pursuing you.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            trigger:          T.Facet("Felucca"),
            flavor:          "The System notes your continued survival. With mild surprise.",
            itemGumpId:      0x14EC,
            prerequisiteKey: "exploration.trammel");

        Reg("exploration.ilshenar",
            "You Found the Third One",
            "Set foot in Ilshenar. The lost lands that weren't entirely lost, just inconveniently located.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            trigger:          T.Facet("Ilshenar"),
            flavor:          "Not everyone finds this place. You did. That's something.",
            itemGumpId:      0x14EC);

        Reg("exploration.malas",
            "The City That Shouldn't Float, Does",
            "Set foot in Malas. A shadow realm that defies physics, zoning laws, and reasonable expectations.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            trigger:          T.Facet("Malas"),
            flavor:          "You went to the dark floating city. Voluntarily. Okay.",
            itemGumpId:      0x14EC);

        Reg("exploration.tokuno",
            "Islands That Time Forgot, Then Found Again",
            "Set foot in Tokuno. The Empire expects composure. Try to provide it.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            trigger:          T.Facet("Tokuno"),
            flavor:          "Diplomatic incident: narrowly averted.",
            itemGumpId:      0x14EC);

        Reg("exploration.all_facets",
            "Frequent Flyer: Unlimited Edition",
            "Visit all five facets of Britannia. You have been everywhere. Possibly too many places.",
            AchievementCategory.Exploration, ap: 50, renown: 100,
            trigger:          T.Event(TriggerKind.VisitAllFacets),
            flavor:          "The passport is full. The System is out of stamps.",
            itemGumpId:      0x0F26,
            hidden:          true);     // Checked manually — no single prereq

        // ── Skill additions ───────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0x13E3 = Smith's Hammer   0x0E3B = Spellbook

        // -- Expanded skill cap milestones (written for a 300 cap) ---------
        // Tiers: 50 Apprentice | 100 Journeyman | 150 Advanced | 200 Master
        //        250 Paragon   | 300 Grandmaster
        // cc-P55 Part G (D75, Chase 2026-10-05): the cap is 200 (cc-P53), so every skill achievement keyed above it is
        // retired: Paragon, Grandmaster, Triple Grandmaster and the three Legends. Holders keep them.

        Reg("skills.advanced",
            "Exceeding the Old Normal",
            "Reach 150 in any skill. You are now above what was previously considered the ceiling. Someone else's ceiling.",
            AchievementCategory.Skills, ap: 20, renown: 50,
            trigger:          T.AnySkill(150),
            flavor:          "150. The old standard GM cap was 100. The System is aware you noticed.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.journeyman");

        Reg("skills.paragon",
            "This Is No Longer a Normal Amount",
            "Reach 250 in any skill. Three-quarters of the way to the actual ceiling. The System is watching.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            trigger:          T.AnySkill(250),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "250. The System has revised its projections upward.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.master");

        // ── Multi-skill breadth (expanded tiers) ──────────────────────────

        Reg("skills.triple_journeyman",
            "Competent in Three Directions",
            "Reach 100 in three different skills. You are no longer a one-trick pony. You are a three-trick pony.",
            AchievementCategory.Skills, ap: 25, renown: 60,
            trigger:          T.SkillsAt(3, 100),
            flavor:          "Three skills at cap. The System approves of diversification.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.journeyman");

        Reg("skills.five_journeyman",
            "Suspiciously Well-Rounded",
            "Reach 100 in five different skills. At this point it's a lifestyle choice.",
            AchievementCategory.Skills, ap: 50, renown: 125,
            trigger:          T.SkillsAt(5, 100),
            flavor:          "Five skills at 100. The System is mildly concerned about your free time.",
            itemGumpId:      0x0F26,
            prerequisiteKey: "skills.triple_journeyman");

        Reg("skills.triple_master",
            "Elite in Three Disciplines",
            "Reach 200 in three different skills. You have pushed three separate ceilings significantly upward.",
            AchievementCategory.Skills, ap: 75, renown: 200,
            trigger:          T.SkillsAt(3, 200),
            flavor:          "Three skills at 200. This was clearly done on purpose.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.master");

        Reg("skills.triple_grandmaster",
            "The Ceiling Has Three Handprints Now",
            "Reach 300 in three different skills. The System does not have adequate superlatives for this.",
            AchievementCategory.Skills, ap: 150, renown: 500,
            trigger:          T.SkillsAt(3, 300),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "Three skills at 300. The System is speechless. That's a first.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.grandmaster");

        // ── Mining — extended tiers ───────────────────────────────────────

        Reg("skills.mining_elite",
            "The Mountain Yields to You",
            "Reach 200 in Mining. You have gone significantly past what anyone thought was possible with a pickaxe.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            trigger:          T.Skill(SkillName.Mining, 200),
            flavor:          "200 Mining. The rocks have filed for relocation.",
            itemGumpId:      0x0E86,
            prerequisiteKey: "skills.mining_gm");

        Reg("skills.mining_legend",
            "The Earth Has Given Up Entirely",
            "Reach 300 in Mining. Full cap. The ore practically introduces itself.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            trigger:          T.Skill(SkillName.Mining, 300),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "300 Mining. You are the mountain now.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.mining_elite");

        // ── Blacksmithy — extended tiers ──────────────────────────────────

        Reg("skills.smith_elite",
            "The Forge Has No More Objections",
            "Reach 200 in Blacksmithy. Every alloy cooperates. The hammer is an extension of your intent.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            trigger:          T.Skill(SkillName.Blacksmith, 200),
            flavor:          "200 Blacksmithy. The metal stopped arguing.",
            itemGumpId:      0x13E3,
            prerequisiteKey: "skills.smith_gm");

        Reg("skills.smith_legend",
            "The Anvil's Opinions of You Are Reverent",
            "Reach 300 in Blacksmithy. Full cap. The craft has nothing left to teach. You are the craft.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            trigger:          T.Skill(SkillName.Blacksmith, 300),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "300 Blacksmithy. The System suggests naming a technique after yourself.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.smith_elite");

        // ── Magery — extended tiers ───────────────────────────────────────

        Reg("skills.magery_elite",
            "Arcane Comprehension: Unsettling",
            "Reach 200 in Magery. The spellbook is essentially a formality at this point.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            trigger:          T.Skill(SkillName.Magery, 200),
            flavor:          "200 Magery. Reality is taking your calls now.",
            itemGumpId:      0x0E3B,
            prerequisiteKey: "skills.magery_gm");

        Reg("skills.magery_legend",
            "Reality Is Mostly a Suggestion Now",
            "Reach 300 in Magery. Full cap. The distinction between intent and outcome has become very narrow.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            trigger:          T.Skill(SkillName.Magery, 300),
            retired:          true,   // cc-P55 Part G (D75): keyed above the 200 cap
            flavor:          "300 Magery. The System recommends not thinking too hard about what this means.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.magery_elite");

        Reg("skills.renaissance_man",
            "Comfortably Mediocre in Several Things",
            "Reach 50 in five different skills. You've committed to nothing while dabbling in everything. Classic.",
            AchievementCategory.Skills, ap: 15, renown: 25,
            trigger:          T.SkillsAt(5, 50),
            flavor:          "Five half-skills. That's approximately two and a half whole skills.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.apprentice");

        Reg("skills.polymath",
            "Embarrassingly Well-Rounded",
            "Reach 50 in ten different skills. At some point this became a character trait.",
            AchievementCategory.Skills, ap: 35, renown: 75,
            trigger:          T.SkillsAt(10, 50),
            flavor:          "The System has stopped trying to categorize you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.renaissance_man");

        Reg("skills.mining_gm",
            "The Earth Has No Secrets From You",
            "Reach Grandmaster in Mining. Every rock type, every vein depth, catalogued.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            trigger:          T.Skill(SkillName.Mining, 100),
            flavor:          "GM Miner. The ore knows.",
            itemGumpId:      0x0E86);

        Reg("skills.smith_gm",
            "Hammer Time (Permanent)",
            "Reach Grandmaster in Blacksmithy. Metal bends to your will. Almost entirely.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            trigger:          T.Skill(SkillName.Blacksmith, 100),
            flavor:          "The anvil has opinions about you. They are positive.",
            itemGumpId:      0x13E3);

        Reg("skills.magery_gm",
            "Memorized Every Word in Every Spellbook",
            "Reach Grandmaster in Magery. The arcane syllables flow freely now. Perhaps uncomfortably so.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            trigger:          T.Skill(SkillName.Magery, 100),
            flavor:          "The System recommends care around residential areas during practice.",
            itemGumpId:      0x0E3B);

        // ── Mining ────────────────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0x0F26 = Diamond

        Reg("mining.first_vein",
            "The Ground Didn't Want That",
            "Mine your first non-iron colored ore. The earth has relinquished something and you took it.",
            AchievementCategory.Mining, ap: 10, renown: 20,
            trigger:          T.Event(TriggerKind.MineColoredOre),
            flavor:          "Step one of a very long relationship with rocks.",
            itemGumpId:      0x0E86);

        Reg("mining.prospector",
            "Certified Rock Enthusiast",
            "Discover 5 different ore types in your Prospector's Logbook. The rocks respect you now.",
            AchievementCategory.Mining, ap: 20, renown: 50,
            trigger:          T.Count(TriggerKind.OreTypesDiscovered, 5),
            flavor:          "The logbook is filling up. The rocks are taking notes.",
            itemGumpId:      0x0E86,
            progressCounter: "ore_types_discovered");

        Reg("mining.surveyor",
            "The Earth Has a Lot Going On",
            "Discover 10 different ore types. Half of Britannia's underground is now in your notes.",
            AchievementCategory.Mining, ap: 40, renown: 100,
            trigger:          T.Count(TriggerKind.OreTypesDiscovered, 10),
            flavor:          "Your logbook is getting heavy. This is good.",
            itemGumpId:      0x0E86,
            hidden:          true,
            prerequisiteKey: "mining.prospector",
            progressCounter: "ore_types_discovered");

        Reg("mining.master_prospector",
            "Nothing Left to Find. Sort Of.",
            "Discover all 16 ore types. Complete mineral survey of Britannia. The System is genuinely impressed.",
            AchievementCategory.Mining, ap: 100, renown: 300,
            trigger:          T.Count(TriggerKind.OreTypesDiscovered, 16),
            flavor:          "The survey is complete. The earth has no more surprises for you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "mining.surveyor",
            progressCounter: "ore_types_discovered");

        Reg("mining.ore_1k",
            "Industrial Scale Geology",
            "Mine 1,000 total ore. You have moved a quantifiable portion of Britannia's crust.",
            AchievementCategory.Mining, ap: 25, renown: 50,
            trigger:          T.Count(TriggerKind.ColoredOreMined, 1_000),
            flavor:          "Structural geologists have filed a complaint.",
            itemGumpId:      0x0E86,
            progressCounter: "total_ore_mined");

        Reg("mining.ore_10k",
            "You Are Technically a Geological Event",
            "Mine 10,000 total ore. Mountains have opinions. They are not sharing them.",
            AchievementCategory.Mining, ap: 75, renown: 200,
            trigger:          T.Count(TriggerKind.ColoredOreMined, 10_000),
            flavor:          "The crust has moved. This is your doing.",
            itemGumpId:      0x0E86,
            hidden:          true,
            prerequisiteKey: "mining.ore_1k",
            progressCounter: "total_ore_mined");

        Reg("mining.jacobs_legacy",
            "Jacob's Legacy: Accepted",
            "Equip any of Jacob's Pickaxes. Some tools come with history. This one comes with expectations.",
            AchievementCategory.Mining, ap: 15, renown: 35,
            trigger:          T.Event(TriggerKind.JacobsPickaxe),
            flavor:          "The pickaxe has a story. You are now part of it.",
            itemGumpId:      0x0E86);

        Reg("mining.felucca_vein",
            "Unsafe Mining Practices (Felucca Division)",
            "Mine ore in Felucca. The resources are better here. So are the consequences.",
            AchievementCategory.Mining, ap: 20, renown: 50,
            trigger:          T.Event(TriggerKind.MineInFelucca),
            flavor:          "Danger premium: earned.",
            itemGumpId:      0x0E86,
            prerequisiteKey: "exploration.felucca");

        // ── Crafting ──────────────────────────────────────────────────────
        // 0x13E3 = Smith's Hammer   0x0F9D = Sewing Kit   0x1BF2 = Iron Ingot

        Reg("crafting.first_item",
            "You Made a Thing. It Exists Now.",
            "Craft your first item. Raw material has become an object. Your fingerprints are on it.",
            AchievementCategory.Crafting, ap: 5, renown: 10,
            trigger:          T.Event(TriggerKind.CraftAny),
            flavor:          "Crafting: begun. It only gets more expensive from here.",
            itemGumpId:      0x13E3);

        Reg("crafting.exceptional",
            "The System Acknowledges Quality",
            "Craft an exceptional item. Something you made is measurably better than it had to be.",
            AchievementCategory.Crafting, ap: 20, renown: 50,
            trigger:          T.Event(TriggerKind.CraftExceptional),
            flavor:          "Exceptional. Not aspirationally. Actually.",
            itemGumpId:      0x0F26,
            prerequisiteKey: "crafting.first_item");

        Reg("crafting.ingots_100",
            "Halfway to a Real Inventory",
            "Smelt 100 ingots. You have converted rock into slightly more useful rock.",
            AchievementCategory.Crafting, ap: 15, renown: 30,
            trigger:          T.Count(TriggerKind.IngotsSmelted, 100),
            flavor:          "The ore situation has been processed.",
            itemGumpId:      0x1BF2,
            progressCounter: "ingots_smelted");

        Reg("crafting.blacksmith_first",
            "Hammer Applied to Metal: Successfully",
            "Craft your first smithed item. You are a blacksmith now, informally.",
            AchievementCategory.Crafting, ap: 10, renown: 20,
            trigger:          T.Event(TriggerKind.CraftSmithed),
            flavor:          "The anvil has been consulted. It has noted your contribution.",
            itemGumpId:      0x13E3,
            prerequisiteKey: "crafting.first_item");

        Reg("crafting.tailor_first",
            "Sewing: Harder Than It Looks",
            "Craft your first tailored item. Needle, thread, and concentrated effort. Something wearable has resulted.",
            AchievementCategory.Crafting, ap: 10, renown: 20,
            trigger:          T.Event(TriggerKind.CraftTailored),
            flavor:          "The garment exists. That is the whole achievement.",
            itemGumpId:      0x0F9D,
            prerequisiteKey: "crafting.first_item");

        // ── Collection ────────────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0xEED = Gold coins   0x0F26 = Diamond

        Reg("collection.ore_standard",
            "Ore Bingo: Standard Edition",
            "Mine all 8 standard colored ore types (Dull Copper through Valorite). The classics.",
            AchievementCategory.Collection, ap: 25, renown: 75,
            trigger:          T.Count(TriggerKind.OreTypesDiscovered, 8),
            flavor:          "Every serious miner has done this. You are now a serious miner.",
            itemGumpId:      0x0E86,
            progressCounter: "ore_types_discovered");

        Reg("collection.ore_full",
            "Ore Bingo: Expert Edition",
            "Mine all 16 ore types including rare veins. The complete mineral vocabulary of Shattered Legacy.",
            AchievementCategory.Collection, ap: 75, renown: 200,
            trigger:          T.Count(TriggerKind.OreTypesDiscovered, 16),
            flavor:          "The logbook is satisfied. The earth has no surprises left.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "collection.ore_standard",
            progressCounter: "ore_types_discovered");

        Reg("collection.gold_10k",
            "Double-Digit Thousands Is a Personality",
            "Accumulate 10,000 gold in your bank. Enough to be inconvenient to lose.",
            AchievementCategory.Collection, ap: 10, renown: 20,
            trigger:          T.Count(TriggerKind.BankGold, 10_000),
            flavor:          "The wealth is real. For now.",
            itemGumpId:      0xEED);

        Reg("collection.gold_100k",
            "The System Calculates: Comfortable",
            "Accumulate 100,000 gold in your bank. Six figures in Britannian currency. Genuinely impressive.",
            AchievementCategory.Collection, ap: 35, renown: 75,
            trigger:          T.Count(TriggerKind.BankGold, 100_000),
            flavor:          "Don't lose it.",
            itemGumpId:      0xEED,
            prerequisiteKey: "collection.gold_10k");

        // ── Discovery additions ───────────────────────────────────────────

        Reg("discovery.survey_report",
            "Contributing to Science. Technically.",
            "Report an ore discovery to the Survey Archivist. Your data is now in the official record.",
            AchievementCategory.Discovery, ap: 15, renown: 35,
            trigger:          T.Event(TriggerKind.SurveyReport),
            flavor:          "The Archivist has filed it. Probably in the right folder.",
            itemGumpId:      0x14EC);

        Reg("discovery.treasure_map",
            "X Marked the Spot. You Found the X.",
            "Decode and excavate a treasure map chest. The treasure was buried. You unburied it.",
            AchievementCategory.Discovery, ap: 20, renown: 50,
            trigger:          T.Event(TriggerKind.TreasureMap),
            flavor:          "The treasure hunters of old would approve. Briefly, before wanting a cut.",
            itemGumpId:      0x14EC);

        // ── Legacy additions ──────────────────────────────────────────────

        Reg("legacy.new_haven_1",
            "The Tutorial Has a Checkbox",
            "Complete your first New Haven trainer quest. The questgivers are broadly satisfied.",
            AchievementCategory.Legacy, ap: 5, renown: 10,
            trigger:          T.Count(TriggerKind.NewHavenQuests, 1),
            flavor:          "One down. The others are aware you exist now.",
            itemGumpId:      0x0E34);

        Reg("legacy.new_haven_10",
            "New Haven: Mostly Done With You",
            "Complete 10 New Haven trainer quests. A double-digit investment in local civic responsibility.",
            AchievementCategory.Legacy, ap: 20, renown: 50,
            trigger:          T.Count(TriggerKind.NewHavenQuests, 10),
            flavor:          "The trainers are surprised you came back. Ten times.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "legacy.new_haven_1",
            progressCounter: "new_haven_quests");

        Reg("legacy.new_haven_all",
            "The System Is Running Out of Superlatives",
            "Complete all 38 New Haven trainer quests. Every quest. Every trainer. Completely done.",
            AchievementCategory.Legacy, ap: 75, renown: 200,
            trigger:          T.Count(TriggerKind.NewHavenQuests, 38),
            flavor:          "Done. Actually completely done. The System chooses to believe you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "legacy.new_haven_10",
            progressCounter: "new_haven_quests");

        Reg("legacy.old_haven_local",
            "Old Haven's Least Welcome Regular",
            "Slay 50 creatures in Old Haven's ruins. You are the reason tourism has not recovered.",
            AchievementCategory.Legacy, ap: 20, renown: 50,
            trigger:          T.Count(TriggerKind.OldHavenKills, 50),
            flavor:          "They know your name there. It is not used fondly.",
            itemGumpId:      0x0F49,
            progressCounter: "oldhaven_kills");

        // ── League additions ──────────────────────────────────────────────

        Reg("league.ap_100",
            "The System Is Taking Notes",
            "Accumulate 100 Achievement Points. You are officially on the board.",
            AchievementCategory.League, ap: 10, renown: 25,
            trigger:          T.Count(TriggerKind.AchievementPoints, 100),
            flavor:          "100 AP. A beginning. Or a warning sign. Possibly both.",
            itemGumpId:      0x0FF4,
            prerequisiteKey: "league.registered_citizen");

        Reg("league.ap_500",
            "Deeply, Embarrassingly Invested",
            "Accumulate 500 Achievement Points. Half a thousand. The System has taken formal notice.",
            AchievementCategory.League, ap: 25, renown: 75,
            trigger:          T.Count(TriggerKind.AchievementPoints, 500),
            flavor:          "At this point this is a hobby. An aggressive one.",
            itemGumpId:      0x0FF4,
            hidden:          true,
            prerequisiteKey: "league.ap_100");

        Reg("league.renown_500",
            "People Know Your Name Now",
            "Accumulate 500 Renown. Word travels. About you. The System confirms: this is about you.",
            AchievementCategory.League, ap: 20, renown: 50,
            trigger:          T.Count(TriggerKind.Renown, 500),
            flavor:          "The reputation precedes you. It arrives walking fast.",
            itemGumpId:      0x1B76,
            prerequisiteKey: "league.guildbound");

        Reg("league.veteran",
            "Still Here. Somehow.",
            "Log in 10 times. You have returned. Multiple times. The System acknowledges your persistence.",
            AchievementCategory.League, ap: 15, renown: 35,
            trigger:          T.Count(TriggerKind.Logins, 10),
            flavor:          "Ten sessions. The System marks this: 'committed.'",
            itemGumpId:      0x0FF4,
            prerequisiteKey: "league.registered_citizen",
            progressCounter: "login_count");

        Reg("league.known",
            "You Live Here Now. It's Fine.",
            "Log in 50 times. Shattered Legacy is where you live now. The rent is your time.",
            AchievementCategory.League, ap: 40, renown: 100,
            trigger:          T.Count(TriggerKind.Logins, 50),
            flavor:          "50 logins. You're not a visitor anymore.",
            itemGumpId:      0x0FF4,
            hidden:          true,
            prerequisiteKey: "league.veteran",
            progressCounter: "login_count");
    }

    private static void Reg(string key, string title, string desc,
                             AchievementCategory cat, int ap, int renown,
                             AchievementTrigger trigger,
                             string  flavor           = "",
                             int     itemGumpId       = 0,
                             bool    hidden           = false,
                             string? prerequisiteKey  = null,
                             Type[]? rewardItems      = null,
                             string? progressCounter  = null,
                             bool    retired          = false) =>
        _defs[key] = new AchievementDef(key, title, desc, cat, ap, renown, trigger,
                                        flavor, itemGumpId, hidden,
                                        prerequisiteKey, rewardItems,
                                        progressCounter, retired);

    // -- Granting by trigger (cc-P55 Part F) -------------------------------

    /// <summary>The live achievements of one kind, lowest threshold first (so a prerequisite is granted before its next step).</summary>
    private static IEnumerable<AchievementDef> OfKind(TriggerKind kind) =>
        _defs.Values.Where(d => d.Trigger.Kind == kind).OrderBy(d => d.Trigger.Threshold).ThenBy(d => d.Trigger.Level);

    /// <summary>Grants every achievement of <paramref name="kind"/> whose threshold <paramref name="value"/> has reached.</summary>
    private static void GrantCounted(IAccount acct, TriggerKind kind, long value)
    {
        foreach (var def in OfKind(kind))
            if (value >= def.Trigger.Threshold)
                TryGrant(acct, def.Key);
    }

    /// <summary>Grants every achievement of a one-time event kind (and, for a facet, only that facet's).</summary>
    private static void GrantEvent(IAccount acct, TriggerKind kind, string? facet = null)
    {
        foreach (var def in OfKind(kind))
            if (facet == null || string.Equals(def.Trigger.FacetName, facet, StringComparison.OrdinalIgnoreCase))
                TryGrant(acct, def.Key);
    }

    /// <summary>The skill achievements, from the character's Base skill (never Value, so item bonuses do not count).</summary>
    private static void GrantSkills(PlayerMobile pm, IAccount acct)
    {
        foreach (var def in _defs.Values.Where(d => d.Trigger.IsSkillLevel).OrderBy(d => d.Trigger.Level).ThenBy(d => d.Trigger.Threshold))
        {
            var t = def.Trigger;
            var earned = t.Kind switch
            {
                TriggerKind.AnySkill      => CountSkillsAt(pm, t.Level) >= 1,
                TriggerKind.SkillsAtLevel => CountSkillsAt(pm, t.Level) >= t.Threshold,
                TriggerKind.SpecificSkill => pm.Skills[t.OnSkill!.Value].Base >= t.Level,
                _                         => false
            };

            if (earned)
                TryGrant(acct, def.Key);
        }
    }

    private static int CountSkillsAt(PlayerMobile pm, int level)
    {
        var count = 0;
        for (var i = 0; i < pm.Skills.Length; i++)
            if (pm.Skills[i].Base >= level) count++;
        return count;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public static bool HasEarned(IAccount acct, string key) =>
        _earned.TryGetValue(acct.Username, out var set) && set.Contains(key);

    public static IReadOnlyCollection<string> GetEarnedKeys(string username) =>
        _earned.TryGetValue(username, out var set)
            ? set
            : (IReadOnlyCollection<string>)Array.Empty<string>();

    /// <summary>
    /// The "earned / total" the achievements gump shows. cc-P55 Part G: retired achievements count in neither, so a
    /// character who holds one is not shown as further along than the live list allows.
    /// </summary>
    public static (int earned, int total) ProgressCount(string username)
    {
        var earned = GetEarnedKeys(username);
        var live   = _defs.Values.Where(d => !d.Retired).ToList();
        return (live.Count(d => earned.Contains(d.Key)), live.Count);
    }

    /// <summary>
    /// Grant an achievement if not already earned. Returns true on new earn.
    /// Enforces prerequisite chain, updates AP and Renown, spawns any reward
    /// items into the player's backpack, and shows the earn popup.
    /// </summary>
    public static bool TryGrant(IAccount acct, string key)
    {
        if (!_defs.TryGetValue(key, out var def)) return false;

        // cc-P55 Part G: a retired achievement is never awarded again, by any path (staff grants included).
        if (def.Retired) return false;

        // Prerequisite must be earned first.
        if (def.PrerequisiteKey != null && !HasEarned(acct, def.PrerequisiteKey))
            return false;

        if (!GetEarnedMutable(acct.Username).Add(key)) return false; // already earned

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        data.AchievementPoints += def.AP;
        data.GrantRenown(def.Renown, RenownSource.Achievement); // and LifetimeRenown (cc-P48)

        if (def.RewardItems.Length > 0)
            SpawnRewardItems(acct, def);

        NotifyOnlinePlayer(acct, def);
        return true;
    }

    /// <summary>
    /// Instantiate each reward item type and place it in the online player's
    /// backpack (or at their feet if the pack is unavailable).
    /// </summary>
    private static void SpawnRewardItems(IAccount acct, AchievementDef def)
    {
        PlayerMobile? pm = null;
        for (var i = 0; i < acct.Count; i++)
        {
            if (acct[i] is PlayerMobile p && p.NetState != null) { pm = p; break; }
        }
        if (pm == null) return;

        foreach (var type in def.RewardItems)
        {
            if (Activator.CreateInstance(type) is not Item item) continue;

            if (pm.Backpack != null)
                pm.Backpack.DropItem(item);
            else
                item.MoveToWorld(pm.Location, pm.Map);
        }
    }

    /// <summary>
    /// Notify that a player killed a creature. Increments kill counters and
    /// checks all kill-milestone achievements.
    /// </summary>
    public static void NotifyKill(PlayerMobile pm, Mobile victim)
    {
        if (pm.Account is not IAccount acct) return;

        // ── Kill milestones ────────────────────────────────────────────────
        GrantCounted(acct, TriggerKind.Kills, Increment(acct.Username, "kills"));

        // ── Named bosses ───────────────────────────────────────────────────
        if (victim is OldHavenMage)
            GrantCounted(acct, TriggerKind.OldHavenMageKills, Increment(acct.Username, "oldhaven_mage_kills"));

        if (victim is DrelgorTheImpaler)
            GrantEvent(acct, TriggerKind.KillDrelgor);

        // ── PvP ────────────────────────────────────────────────────────────
        if (victim is PlayerMobile)
            GrantEvent(acct, TriggerKind.PlayerKill);

        // ── Undead ────────────────────────────────────────────────────────
        if (victim is Zombie    || victim is Skeleton      || victim is Ghoul
                    || victim is Shade     || victim is Spectre      || victim is Wraith
                    || victim is Lich      || victim is LichLord      || victim is BoneKnight
                    || victim is SkeletalKnight || victim is SkeletalDragon)
        {
            GrantCounted(acct, TriggerKind.UndeadKills, Increment(acct.Username, "undead_kills"));
        }

        // ── Ratmen ────────────────────────────────────────────────────────
        if (victim is Ratman || victim is RatmanArcher || victim is RatmanMage)
            GrantCounted(acct, TriggerKind.RatmanKills, Increment(acct.Username, "ratman_kills"));

        // ── Orcs ──────────────────────────────────────────────────────────
        if (victim is Orc     || victim is OrcBrute  || victim is OrcBomber
                   || victim is OrcCaptain || victim is OrcishMage || victim is OrcishLord)
        {
            GrantCounted(acct, TriggerKind.OrcKills, Increment(acct.Username, "orc_kills"));
        }

        // ── Old Haven region (any mob) ─────────────────────────────────────
        if (IsInOldHaven(pm))
            GrantCounted(acct, TriggerKind.OldHavenKills, Increment(acct.Username, "oldhaven_kills"));
    }

    private static bool IsInOldHaven(Mobile m) =>
        m.Region?.Name?.IndexOf("Old Haven", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Check a skill value against skill achievement thresholds.
    /// Called on login (full skill scan) and optionally on each skill gain.
    /// </summary>
    public static void NotifySkillValue(PlayerMobile pm, double value)
    {
        if (pm.Account is not IAccount acct) return;
        foreach (var def in OfKind(TriggerKind.AnySkill))
            if (value >= def.Trigger.Level)
                TryGrant(acct, def.Key);
    }

    /// <summary>
    /// Notify that a player has entered Old Haven.
    /// Wire to a movement/zone hook for the Old Haven bounds.
    /// </summary>
    public static void NotifyOldHavenVisit(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.VisitOldHaven);
    }

    // ── Event hooks ───────────────────────────────────────────────────────

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;

        GrantEvent(acct, TriggerKind.FirstLogin);

        // -- Skill achievements (any skill, several skills, one skill), from Base --
        GrantSkills(pm, acct);

        // ── Facet visits ──────────────────────────────────────────────────
        NotifyMapVisit(pm);

        // ── New Haven quest progress ───────────────────────────────────────
        var ctx = MLQuestSystem.GetContext(pm);
        if (ctx != null)
        {
            var doneCount = 0;
            foreach (var (questType, _) in AchievementsGump.NewHavenQuests)
                if (ctx.HasDoneQuest(questType)) doneCount++;
            SetCounter(acct.Username, "new_haven_quests", doneCount);
            GrantCounted(acct, TriggerKind.NewHavenQuests, doneCount);
        }

        // ── Login count ───────────────────────────────────────────────────
        GrantCounted(acct, TriggerKind.Logins, Increment(acct.Username, "login_count"));

        // ── AP / Renown league thresholds ─────────────────────────────────
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        GrantCounted(acct, TriggerKind.AchievementPoints, data.AchievementPoints);
        GrantCounted(acct, TriggerKind.Renown, data.Renown);

        // ── Bank gold ─────────────────────────────────────────────────────
        GrantCounted(acct, TriggerKind.BankGold, pm.BankBox?.GetAmount(typeof(Gold)) ?? 0);

        // ── Jacob's Pickaxe ───────────────────────────────────────────────
        CheckJacobsPickaxe(pm);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.FirstDeath);
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (bc.LastKiller is PlayerMobile pm)
            NotifyKill(pm, bc);
    }

    // ── Counters ──────────────────────────────────────────────────────────

    private static int Increment(string username, string counter)
    {
        if (!_counters.TryGetValue(username, out var dict))
            _counters[username] = dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        dict.TryGetValue(counter, out var cur);
        dict[counter] = cur + 1;
        return cur + 1;
    }

    private static int IncrementBy(string username, string counter, int amount)
    {
        if (!_counters.TryGetValue(username, out var dict))
            _counters[username] = dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        dict.TryGetValue(counter, out var cur);
        dict[counter] = cur + amount;
        return cur + amount;
    }

    private static void SetCounter(string username, string counter, int value)
    {
        if (!_counters.TryGetValue(username, out var dict))
            _counters[username] = dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        dict[counter] = value;
    }

    public static int GetCounter(string username, string counter)
    {
        if (!_counters.TryGetValue(username, out var dict)) return 0;
        dict.TryGetValue(counter, out var v);
        return v;
    }

    // ── Additional notification APIs ──────────────────────────────────────

    /// <summary>Called when a player enters a new map. Grants facet achievements.</summary>
    public static void NotifyMapVisit(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        var map = pm.Map;
        if (map == null || map == Map.Internal) return;

        GrantEvent(acct, TriggerKind.VisitFacet, map.Name);

        // All-facets check: only after every facet achievement is earned
        if (OfKind(TriggerKind.VisitFacet).All(d => HasEarned(acct, d.Key)))
            GrantEvent(acct, TriggerKind.VisitAllFacets);
    }

    /// <summary>
    /// Called each time colored ore is yielded. Wire from CompactOreSatchelRoutingHook.
    /// </summary>
    public static void NotifyMining(PlayerMobile pm, int amount, bool inFelucca)
    {
        if (pm.Account is not IAccount acct) return;

        GrantEvent(acct, TriggerKind.MineColoredOre);
        GrantCounted(acct, TriggerKind.ColoredOreMined, IncrementBy(acct.Username, "total_ore_mined", amount));

        if (inFelucca) GrantEvent(acct, TriggerKind.MineInFelucca);
    }

    /// <summary>
    /// Called when the Prospector's Logbook discovery count changes. Wire from
    /// CompactOreSatchelRoutingHook whenever a new ore type or vein is logged.
    /// </summary>
    public static void NotifyOreDiscoveryCount(PlayerMobile pm, int discoveredCount)
    {
        if (pm.Account is not IAccount acct) return;

        SetCounter(acct.Username, "ore_types_discovered", discoveredCount);
        GrantCounted(acct, TriggerKind.OreTypesDiscovered, discoveredCount);
    }

    /// <summary>Called when a survey report is submitted to the Survey Archivist.</summary>
    public static void NotifySurveyReport(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.SurveyReport);
    }

    /// <summary>Called when a treasure map chest is excavated and opened.</summary>
    public static void NotifyTreasureMap(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.TreasureMap);
    }

    /// <summary>Called when any item is crafted. Pass exceptional=true for exceptional quality.</summary>
    public static void NotifyCraftedItem(PlayerMobile pm, bool exceptional)
    {
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.CraftAny);
        if (exceptional) GrantEvent(acct, TriggerKind.CraftExceptional);
    }

    /// <summary>Called when a smithed item is crafted.</summary>
    public static void NotifySmithedItem(PlayerMobile pm, bool exceptional)
    {
        NotifyCraftedItem(pm, exceptional);
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.CraftSmithed);
    }

    /// <summary>Called when a tailored item is crafted.</summary>
    public static void NotifyTailoredItem(PlayerMobile pm, bool exceptional)
    {
        NotifyCraftedItem(pm, exceptional);
        if (pm.Account is not IAccount acct) return;
        GrantEvent(acct, TriggerKind.CraftTailored);
    }

    /// <summary>Called when ingots are smelted. Pass the number of ingots produced.</summary>
    public static void NotifyIngotsSmelted(PlayerMobile pm, int amount)
    {
        if (pm.Account is not IAccount acct) return;
        GrantCounted(acct, TriggerKind.IngotsSmelted, IncrementBy(acct.Username, "ingots_smelted", amount));
    }

    /// <summary>
    /// Checks whether the player has any Jacob's Pickaxe tier in hand or pack
    /// and grants mining.jacobs_legacy if so.
    /// </summary>
    private static void CheckJacobsPickaxe(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        if (HasEarned(acct, "mining.jacobs_legacy")) return; // already earned

        bool HasJacobs(Item item) =>
            item is JacobsPickaxe           ||
            item is JacobsReinforcedPickaxe ||
            item is JacobsProspectorPickaxe ||
            item is JacobsDeepdelverPickaxe ||
            item is JacobsWorldbreakerPickaxe;

        if (HasJacobs(pm.FindItemOnLayer(Layer.TwoHanded)!)) { GrantEvent(acct, TriggerKind.JacobsPickaxe); return; }
        if (pm.Backpack == null) return;
        foreach (var item in pm.Backpack.Items)
            if (HasJacobs(item)) { GrantEvent(acct, TriggerKind.JacobsPickaxe); return; }
    }

    // ── Notification (earn popup gump) ────────────────────────────────────

    private static void NotifyOnlinePlayer(IAccount acct, AchievementDef def)
    {
        for (var i = 0; i < acct.Count; i++)
        {
            if (acct[i] is PlayerMobile pm && pm.NetState != null)
            {
                pm.SendGump(new AchievementEarnedGump(pm, def));
                break;
            }
        }
    }

    // ── Earned set helpers ────────────────────────────────────────────────

    private static HashSet<string> GetEarnedMutable(string username)
    {
        if (!_earned.TryGetValue(username, out var set))
            _earned[username] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return set;
    }

    // ── Internal persistence API ──────────────────────────────────────────

    internal static void ResetForAccount(string username)
    {
        _earned.Remove(username);
        _counters.Remove(username);
    }

    internal static void LoadFromSave(
        Dictionary<string, HashSet<string>>         earned,
        Dictionary<string, Dictionary<string, int>> counters)
    {
        _earned.Clear();
        foreach (var (k, v) in earned)   _earned[k]   = v;
        _counters.Clear();
        foreach (var (k, v) in counters) _counters[k] = v;
    }

    internal static (Dictionary<string, HashSet<string>>         earned,
                     Dictionary<string, Dictionary<string, int>> counters) GetForSave() =>
        (_earned, _counters);

    // ── Command handlers ──────────────────────────────────────────────────

    [Usage("achievements")]
    [Description("Opens your achievement records from the League of Extraordinary Citizens.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnAchievementsCommand(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
            pm.SendGump(new AchievementsGump(pm));
    }

    [Usage("ClusterFAchievement <grant|revoke|info|list> [args]")]
    [Description("Admin achievement management.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void OnAdminCommand(CommandEventArgs e)
    {
        if (e.Length == 0)
        {
            e.Mobile.SendMessage("Usage: ClusterFAchievement <grant|revoke|info|list> [args]");
            return;
        }

        switch (e.GetString(0).ToLowerInvariant())
        {
            case "grant":  AdminGrant(e);  break;
            case "revoke": AdminRevoke(e); break;
            case "info":   AdminInfo(e);   break;
            case "list":   AdminList(e);   break;
            default:
                e.Mobile.SendMessage("Unknown subcommand. Use: grant, revoke, info, list");
                break;
        }
    }

    private static void AdminGrant(CommandEventArgs e)
    {
        if (e.Length < 2) { e.Mobile.SendMessage("Usage: ClusterFAchievement grant <key> [username]"); return; }
        var key  = e.GetString(1);
        var acct = ResolveAccount(e, 2);
        if (acct == null) return;
        var ok = TryGrant(acct, key);
        e.Mobile.SendMessage(ok
            ? $"Achievement '{key}' granted to {acct.Username}."
            : _defs.TryGetValue(key, out var def) && def.Retired
                ? $"Achievement '{key}' is retired and is never awarded (cc-P55)."
                : $"Achievement '{key}' was not granted (already earned or unknown key).");
    }

    private static void AdminRevoke(CommandEventArgs e)
    {
        if (e.Length < 2) { e.Mobile.SendMessage("Usage: ClusterFAchievement revoke <key> [username]"); return; }
        var key  = e.GetString(1);
        var acct = ResolveAccount(e, 2);
        if (acct == null) return;
        var removed = GetEarnedMutable(acct.Username).Remove(key);
        e.Mobile.SendMessage(removed
            ? $"Achievement '{key}' revoked from {acct.Username}."
            : $"'{key}' was not earned by {acct.Username}.");
    }

    private static void AdminInfo(CommandEventArgs e)
    {
        if (e.Length < 2) { e.Mobile.SendMessage("Usage: ClusterFAchievement info <key>"); return; }
        var key = e.GetString(1);
        if (!_defs.TryGetValue(key, out var def)) { e.Mobile.SendMessage($"No achievement with key '{key}'."); return; }
        e.Mobile.SendMessage($"[{def.Category}] {def.Title} — {def.Description}");
        e.Mobile.SendMessage($"  {def.EarnedLine}{(def.Retired ? " [Retired]" : "")}");
        e.Mobile.SendMessage($"  Rewards: {def.AP} AP, {def.Renown} Renown");
    }

    private static void AdminList(CommandEventArgs e)
    {
        foreach (var def in _defs.Values.OrderBy(d => d.Category).ThenBy(d => d.Key))
            e.Mobile.SendMessage($"  [{def.Category}] {def.Key}: {def.Title} ({def.AP} AP, {def.Renown} R)");
        e.Mobile.SendMessage($"Total: {_defs.Count} achievements defined.");
    }

    private static IAccount? ResolveAccount(CommandEventArgs e, int argIndex)
    {
        if (e.Length > argIndex)
        {
            var username = e.GetString(argIndex);
            var found    = Accounts.GetAccount(username);
            if (found == null) { e.Mobile.SendMessage($"Account '{username}' not found."); return null; }
            return found;
        }
        var self = e.Mobile.Account as IAccount;
        if (self == null) e.Mobile.SendMessage("No account.");
        return self;
    }
}

// ── Achievement persistence ───────────────────────────────────────────────────

public class ClusterFAchievementPersistence : Item
{
    private static ClusterFAchievementPersistence? _instance;

    public static void Configure()
    {
        EventSink.WorldLoad += EnsureExistence;
    }

    private static void EnsureExistence()
    {
        _instance ??= new ClusterFAchievementPersistence();
    }

    private ClusterFAchievementPersistence() : base(1) => Movable = false;

    public ClusterFAchievementPersistence(Serial serial) : base(serial) => _instance = this;

    public override string DefaultName => "ClusterF Achievement Persistence — Internal";

    public override void Serialize(IGenericWriter w)
    {
        base.Serialize(w);
        w.Write(0); // version

        var (earned, counters) = ClusterFAchievementSystem.GetForSave();

        w.Write(earned.Count);
        foreach (var (username, keys) in earned)
        {
            w.Write(username);
            w.Write(keys.Count);
            foreach (var key in keys)
                w.Write(key);
        }

        w.Write(counters.Count);
        foreach (var (username, dict) in counters)
        {
            w.Write(username);
            w.Write(dict.Count);
            foreach (var (name, value) in dict)
            {
                w.Write(name);
                w.Write(value);
            }
        }
    }

    public override void Deserialize(IGenericReader r)
    {
        base.Deserialize(r);
        var version = r.ReadInt();

        var earned      = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var earnedCount = r.ReadInt();
        for (var i = 0; i < earnedCount; i++)
        {
            var username = r.ReadString();
            var keyCount = r.ReadInt();
            var keys     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < keyCount; j++)
                keys.Add(r.ReadString());
            earned[username] = keys;
        }

        var counters     = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var counterCount = r.ReadInt();
        for (var i = 0; i < counterCount; i++)
        {
            var username   = r.ReadString();
            var entryCount = r.ReadInt();
            var dict       = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < entryCount; j++)
                dict[r.ReadString()] = r.ReadInt();
            counters[username] = dict;
        }

        ClusterFAchievementSystem.LoadFromSave(earned, counters);
    }
}

// ── Achievement earned popup ──────────────────────────────────────────────────

/// <summary>
/// Floating popup shown when the player earns a new achievement.
/// Positioned top-right. Shows the achievement title, the System's flavor
/// commentary, and the AP/Renown reward. Dismiss by clicking X or closing.
/// </summary>
public class AchievementEarnedGump : Gump
{
    public AchievementEarnedGump(Mobile m, AchievementDef def) : base(320, 185)
    {
        Closable   = true;
        Disposable = true;

        const int W = 420;
        const int H = 156; // cc-P55 Part F: 20 taller for the Earned line

        AddBackground(0, 0, W, H, 9270);
        // cc-P57 Part F (D80): opaque, like the achievements list and the Staff Hub (D67).
        AddImageTiled(4, 4, W - 8, H - 8, AchievementsGump.PanelTile);

        // ── Header ────────────────────────────────────────────────────────
        AddImageTiled(4, 4, W - 8, 2, 9304);
        AddLabel(W / 2 - 95, 9, 1154, "— ACHIEVEMENT UNLOCKED —");
        AddImageTiled(4, 26, W - 8, 1, 9304);

        // ── Item icon + content ───────────────────────────────────────────
        // If the achievement has an item graphic, show it on the left.
        // Content (title + flavor) shifts right to make room.
        var hasIcon   = def.ItemGumpId > 0;
        var contentX  = hasIcon ? 54 : 12;
        var contentW  = W - contentX - 16;

        if (hasIcon)
            AddItem(8, 34, def.ItemGumpId);

        var bodyText = string.IsNullOrWhiteSpace(def.FlavorText) ? def.Description : def.FlavorText;
        var html =
            $"<BASEFONT COLOR=#FFD700>{def.Title}</BASEFONT><BR>" +
            $"<BASEFONT COLOR=#999999>{bodyText}</BASEFONT> " +
            $"<BASEFONT COLOR=#88AA88>{def.EarnedLine}</BASEFONT>";

        AddHtml(contentX, 30, contentW, 90, html, false, false);

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(4, H - 36, W - 8, 1, 9304);

        // Category + secret tag
        var catLabel = def.Hidden ? $"[{def.Category}] [Secret]" : $"[{def.Category}]";
        AddLabel(12, H - 27, 999, catLabel);

        // Rewards — AP in gold-ish, Renown in gray
        AddLabel(W - 220, H - 27, 1154, $"+{def.AP} AP");
        AddLabel(W - 168, H - 27, 999,  $"+{def.Renown} Renown");

        // Close button
        AddButton(W - 28, H - 30, 4017, 4018, 1);

        AddImageTiled(4, H - 4, W - 8, 2, 9304);
    }

    public override void OnResponse(NetState sender, in RelayInfo info) { }
}

// ── Achievements gump ─────────────────────────────────────────────────────────

/// <summary>
/// Player-facing achievement records gump. Opened via [achievements.
///
/// Tabs (two even rows, cc-P57 Part F): All | Combat | Skills | Exploration | Mining / Crafting | Legacy | Discovery | Quests
///
/// Achievements tab:
///   - Earned achievements listed first (gold, with AP/Renown), then locked (gray).
///   - Achievements with progress counters show a "Progress: n/m" line until earned (a block bar until cc-P57).
///
/// Quests tab:
///   - All 38 New Haven ML trainer quests with done/pending indicators.
/// </summary>
public class AchievementsGump : Gump
{
    private readonly PlayerMobile         _pm;
    private readonly AchievementCategory? _filter;
    private readonly bool                 _questsTab;
    private readonly int                  _page;

    public const int GumpWidth  = 580;
    public const int GumpHeight = 510;
    private const int BgGumpId   = 9270;

    // Tab button base ID — 100..107 for categories, 108 for quests
    private const int TabBase     = 100;
    private const int TabQuests   = 108;

    // Pagination button IDs
    private const int PagePrev = 300;
    private const int PageNext = 301;

    // cc-P57 Part F (bug-list D80, Chase screenshot 2026-10-05): rows were a fixed 64 high, five to a page, and a row
    // with a wrapped flavor, an Earned line and a progress line ran under the next row's title. Each row now gets the
    // height its own lines need (ClusterFGumpText, the client's font widths) and a page holds as many rows as fit.
    public const int PanelTile     = 2624; // the solid tile under the text (cc-P52 Part F, D67), not an alpha region
    public const int TabY          = 60;
    public const int TabRowH       = 22;
    public const int TabColW       = 108; // two even rows of tabs, full words, in fixed columns
    public const int TabLabelDx    = 34;  // the tab button (4011) is 30 wide
    public const int ListY         = TabY + 2 * TabRowH + 6;
    public const int ContentBottom = GumpHeight - 74; // rows end here; the pager sits below, the footer line at H - 40
    public const int HeaderRowH    = 26;
    public const int RowGap        = 6;
    public const int IconH         = 48;

    /// <summary>cc-P55 Part G: the heading over the retired achievements a character already holds.</summary>
    public const string RetiredHeader = "-- Retired (No Longer Awarded) --------------------";

    // cc-P57 Part F: full words ("Explore", "Craft" and "Discov" were cut to fit one row, and "Discov" was still cut).
    internal static readonly (string Label, AchievementCategory? Cat)[] CategoryTabs =
    [
        ("All",         null),
        ("Combat",      AchievementCategory.Combat),
        ("Skills",      AchievementCategory.Skills),
        ("Exploration", AchievementCategory.Exploration),
        ("Mining",      AchievementCategory.Mining),
        ("Crafting",    AchievementCategory.Crafting),
        ("Legacy",      AchievementCategory.Legacy),
        ("Discovery",   AchievementCategory.Discovery),
    ];

    /// <summary>Where tab <paramref name="i"/> (0..7 the categories, 8 Quests) sits: five on the first row, four on the second.</summary>
    internal static (int X, int Y) TabAt(int i) => (14 + i % 5 * TabColW, TabY + i / 5 * TabRowH);

    // ── New Haven quest list ──────────────────────────────────────────────

    internal static readonly (Type QuestType, string Title)[] NewHavenQuests =
    [
        // NewHavenTraining.cs (12)
        (typeof(SplitEnds),                "Split Ends"),
        (typeof(IShotAnArrowIntoTheAir),   "I Shot an Arrow Into the Air..."),
        (typeof(BakersDozen),              "Baker's Dozen"),
        (typeof(AStitchInTime),            "A Stitch in Time"),
        (typeof(BatteredBucklers),         "Battered Bucklers"),
        (typeof(MoreOrePlease),            "More Ore Please"),
        (typeof(ComfortableSeating),       "Comfortable Seating"),
        (typeof(ThePenIsMightier),         "The Pen is Mightier"),
        (typeof(AClockworkPuzzle),         "A Clockwork Puzzle"),
        (typeof(DeliciousFishes),          "Delicious Fishes"),
        (typeof(FleeAndFatigue),           "Flee and Fatigue"),
        (typeof(ChopChopOnTheDouble),      "Chop Chop, On the Double!"),
        // NewHavenSkillTraining.cs (26)
        (typeof(CleansingOldHaven),        "Cleansing Old Haven"),
        (typeof(TheRudimentsOfSelfDefense),"The Rudiments of Self Defense"),
        (typeof(CrushingBonesAndTakingNames),"Crushing Bones and Taking Names"),
        (typeof(SwiftAsAnArrow),           "Swift as an Arrow"),
        (typeof(EnGuarde),                 "En Guarde!"),
        (typeof(TheArtOfWar),              "The Art of War"),
        (typeof(TheWayOfTheBlade),         "The Way of the Blade"),
        (typeof(ThouAndThineShield),       "Thou and Thine Shield"),
        (typeof(DefyingTheArcane),         "Defying the Arcane"),
        (typeof(StoppingTheWorld),         "Stopping the World"),
        (typeof(ScribingArcaneKnowledge),  "Scribing Arcane Knowledge"),
        (typeof(TheMagesApprentice),       "The Mage's Apprentice"),
        (typeof(ScholarlyTask),            "A Scholarly Task"),
        (typeof(TheRightToolForTheJob),    "The Right Tool for the Job"),
        (typeof(KnowThineEnemy),           "Know Thine Enemy"),
        (typeof(BruisesBandagesAndBlood),  "Bruises, Bandages and Blood"),
        (typeof(TheInnerWarrior),          "The Inner Warrior"),
        (typeof(TheArtOfStealth),          "The Art of Stealth"),
        (typeof(BecomingOneWithTheShadows),"Becoming One with the Shadows"),
        (typeof(WalkingSilently),          "Walking Silently"),
        (typeof(EyesOfARanger),            "Eyes of a Ranger"),
        (typeof(TheWayOfTheSamurai),       "The Way of the Samurai"),
        (typeof(TheAllureOfDarkMagic),     "The Allure of Dark Magic"),
        (typeof(ChannelingTheSupernatural),"Channeling the Supernatural"),
        (typeof(TheDeluciansLostMine),     "The Delucian's Lost Mine"),
        (typeof(ItsHammerTime),            "It's Hammer Time!"),
    ];

    // ── Constructor ───────────────────────────────────────────────────────

    public AchievementsGump(PlayerMobile pm, AchievementCategory? filter = null, bool questsTab = false, int page = 0)
        : base(50, 40)
    {
        _pm        = pm;
        _filter    = filter;
        _questsTab = questsTab;
        _page      = page;

        Closable   = true;
        Disposable = true;

        var acct       = pm.Account as IAccount;
        var username   = acct?.Username ?? "";
        var earnedKeys = ClusterFAchievementSystem.GetEarnedKeys(username);
        var data       = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        AddBackground(0, 0, GumpWidth, GumpHeight, BgGumpId);
        // cc-P57 Part F: opaque; the alpha region let the world show through the rows.
        AddImageTiled(10, 10, GumpWidth - 20, GumpHeight - 20, PanelTile);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(GumpWidth / 2 - 125, 13, 1154, "League of Extraordinary Citizens");
        AddLabel(GumpWidth / 2 - 80,  31, 999,  "Achievement Records");
        AddLabel(18, 13, 999, pm.Name);

        var ap     = data?.AchievementPoints ?? 0;
        var renown = data?.Renown ?? 0;
        var (earned, total) = ClusterFAchievementSystem.ProgressCount(username);
        AddLabel(GumpWidth - 200, 13, 1154, $"AP: {ap}");
        AddLabel(GumpWidth - 200, 31, 999,  $"Renown: {renown}   {earned}/{total}");

        AddImageTiled(10, 54, GumpWidth - 20, 2, 9304);

        // ── Tabs: two even rows in fixed columns, full words (cc-P57 Part F) ──
        // The labels sat at button + 18 over a 30-wide button and were cut to fit one row.
        for (var i = 0; i <= CategoryTabs.Length; i++)
        {
            var (x, y) = TabAt(i);
            var isQuests = i == CategoryTabs.Length;
            var label = isQuests ? "Quests" : CategoryTabs[i].Label;
            var isActive = isQuests ? questsTab : !questsTab && CategoryTabs[i].Cat == filter;
            AddButton(x, y, 4011, 4012, isQuests ? TabQuests : TabBase + i);
            AddLabel(x + TabLabelDx, y + 2, isActive ? 1154 : 999, label);
        }

        AddImageTiled(10, ListY - 6, GumpWidth - 20, 2, 9304);

        // ── Content area ─────────────────────────────────────────────────
        const int listY = ListY;
        const int listH = GumpHeight - listY - 46;

        if (questsTab)
            AddQuestContent(listY, listH);
        else
            AddAchievementContent(listY, listH, filter, username, earnedKeys);

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(10, GumpHeight - 40, GumpWidth - 20, 2, 9304);
        AddButton(GumpWidth / 2 - 150, GumpHeight - 30, 4011, 4012, 200);
        AddLabel(GumpWidth / 2 - 132,  GumpHeight - 28, 999, "Guilds");
        AddButton(GumpWidth / 2 - 40, GumpHeight - 30, 4023, 4025, 0);
        AddLabel(GumpWidth / 2 - 16,  GumpHeight - 28, 1154, "Close");
    }

    // ── Achievement list (paginated, icon-capable) ────────────────────────

    // Each display entry is either a section-header divider or an achievement row.
    private readonly record struct PageRow(string? Header, AchievementDef? Def, bool Earned);

    private void AddAchievementContent(int listY, int listH,
        AchievementCategory? filter, string username, IReadOnlyCollection<string> earnedKeys)
    {
        var allDefs  = ClusterFAchievementSystem.Definitions.Values;
        var filtered = (filter.HasValue
            ? allDefs.Where(d => d.Category == filter.Value)
            : allDefs)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Title)
            .ToList();

        var earnedList = filtered.Where(d =>  earnedKeys.Contains(d.Key) && !d.Retired).ToList();
        // Hidden achievements that haven't been earned are completely invisible; so are retired ones (cc-P55 Part G).
        var lockedList = filtered.Where(d => !earnedKeys.Contains(d.Key) && !d.Hidden && !d.Retired).ToList();
        // A retired achievement this account already holds is kept, listed last under its own heading.
        var retiredList = filtered.Where(d => earnedKeys.Contains(d.Key) && d.Retired).ToList();

        // Build a flat list of rows; section-header rows + achievement rows interleaved. cc-P57 Part F: ASCII headings
        // (the box-drawing dashes were 16 pixels each and ran the heading past the frame).
        var rows = new List<PageRow>();

        if (filtered.Count > 0)
        {
            if (earnedList.Count > 0)
            {
                rows.Add(new PageRow(EarnedHeader, null, false));
                foreach (var d in earnedList) rows.Add(new PageRow(null, d, true));
            }
            if (lockedList.Count > 0)
            {
                rows.Add(new PageRow(LockedHeader, null, false));
                foreach (var d in lockedList) rows.Add(new PageRow(null, d, false));
            }
            if (retiredList.Count > 0)
            {
                rows.Add(new PageRow(RetiredHeader, null, false));
                foreach (var d in retiredList) rows.Add(new PageRow(null, d, true));
            }
        }

        if (rows.Count == 0)
        {
            AddLabel(18, listY + 10, 0x555, "No achievements in this category yet.");
            return;
        }

        // cc-P57 Part F: each row as tall as its own lines, and a page holds the rows that fit above ContentBottom.
        var heights = rows.Select(r => RowHeight(r, username)).ToList();
        var pages   = Paginate(heights, ContentBottom - listY);
        var page    = Math.Clamp(_page, 0, pages.Count - 1);
        var (pageStart, pageEnd) = pages[page];

        var rowY = listY;
        for (var i = pageStart; i < pageEnd; i++)
        {
            var row = rows[i];

            if (row.Header != null)
            {
                // ── Section divider row ──────────────────────────────────
                AddLabel(16, rowY + 4, 0x777, row.Header);
                AddImageTiled(16, rowY + 22, GumpWidth - 32, 2, 9304);
            }
            else if (row.Def != null)
            {
                // ── Achievement row ──────────────────────────────────────
                var def   = row.Def;
                var lines = RowLines(def, row.Earned, username);
                var textX = TextX(def);

                if (def.ItemGumpId > 0)
                    AddItem(IconX, rowY + 4, def.ItemGumpId);

                AddHtml(textX, rowY + 2, GumpWidth - textX - 16, HtmlHeight(lines, def), RowHtml(lines), false, false);
            }

            rowY += heights[i];
        }

        // ── Pagination controls ──────────────────────────────────────────
        if (pages.Count > 1)
        {
            var btnY = ContentBottom + 4;

            if (page > 0)
            {
                AddButton(GumpWidth / 2 - 90, btnY, 4011, 4012, PagePrev);
                AddLabel(GumpWidth / 2 - 56, btnY + 2, 999, "< Prev");
            }

            AddLabel(GumpWidth / 2 - 18, btnY + 2, 999, $"{page + 1}/{pages.Count}");

            if (page < pages.Count - 1)
            {
                AddButton(GumpWidth / 2 + 30, btnY, 4011, 4012, PageNext);
                AddLabel(GumpWidth / 2 + 64, btnY + 2, 999, "Next >");
            }
        }
    }

    public const string EarnedHeader = "-- The System Has Recorded These --";
    public const string LockedHeader = "-- Not Yet (The System Is Watching) --";

    private const int IconX       = 14;
    // cc-P57 Part F: the icons are at most 50 wide and 44 tall (EA art, read 2026-10-05: 0x1B76 is 50 x 44), so text
    // starts clear of them at 68 (it started at 56) and a row is at least IconH tall.
    private const int TextXIcon   = 68; // text X when icon is present
    private const int TextXNoIcon = 14; // text X when no icon

    private static int TextX(AchievementDef def) => def.ItemGumpId > 0 ? TextXIcon : TextXNoIcon;

    // A row's lines, each a list of (color, text) runs. The HTML and the measure are both made from this, so they agree.
    private static List<List<(string Color, string Text)>> RowLines(AchievementDef def, bool earned, string username)
    {
        var lines = new List<List<(string, string)>>();

        if (earned)
        {
            var title = new List<(string, string)> { ("#FFD700", def.Title) };
            if (def.Hidden)
                title.Add(("#886633", " [Secret]"));
            title.Add(("#6699BB", $" [{def.AP} AP / {def.Renown} R]"));
            lines.Add(title);

            if (!string.IsNullOrWhiteSpace(def.FlavorText))
                lines.Add([("#4A7070", $"\"{def.FlavorText}\"")]);
            else
                lines.Add([("#888888", def.Description)]);

            // cc-P55 Part F: what earned it, in plain words, from the achievement's own trigger; its own line (cc-P57).
            var earnedLine = new List<(string, string)> { ("#6A8A6A", def.EarnedLine) };
            if (def.RewardItems.Length > 0)
                earnedLine.Add(("#558855", $" (+{def.RewardItems.Length} item)"));
            lines.Add(earnedLine);
        }
        else
        {
            lines.Add([("#777777", def.Title), ("#4A6A80", $" [{def.AP} AP / {def.Renown} R]")]);
            lines.Add([("#8A8A8A", def.Description)]);

            // cc-P57 Part F: the prerequisite on its own line, as a plain sentence, under the description.
            var playerEarned = ClusterFAchievementSystem.GetEarnedKeys(username);
            if (def.PrerequisiteKey != null
                && !playerEarned.Contains(def.PrerequisiteKey)
                && ClusterFAchievementSystem.Definitions.TryGetValue(def.PrerequisiteKey, out var prereqDef))
            {
                lines.Add([("#AA6666", $"Requires: {prereqDef.Title}")]);
            }
        }

        // Progress, as words (cc-P57 Part F: the block bar's empty cells had no glyph in the client's font). Only while
        // not yet earned (cc-P61 Part B, D84): the counter keeps counting past the goal ("Progress: 9/1" under an
        // earned "Oh Look, It's Dead"), and the Earned line already says what was done.
        if (!earned && def.ProgressCounter != null && def.ProgressThreshold > 0)
        {
            var count = ClusterFAchievementSystem.GetCounter(username, def.ProgressCounter);
            lines.Add([("#7A7A7A", $"Progress: {count:N0}/{def.ProgressThreshold:N0}")]);
        }

        return lines;
    }

    private static string RowHtml(List<List<(string Color, string Text)>> lines) =>
        string.Join("<BR>", lines.Select(l => string.Concat(l.Select(r => $"<BASEFONT COLOR={r.Color}>{r.Text}</BASEFONT>"))));

    /// <summary>The row's text as the client lays it out: runs joined, lines split by '\n'.</summary>
    internal static string RowPlainText(AchievementDef def, bool earned, string username) =>
        string.Join("\n", RowLines(def, earned, username).Select(l => string.Concat(l.Select(r => r.Text))));

    // The measure wraps 8 pixels short of the block's width, so a rounding difference in the client never adds a line.
    private static int HtmlHeight(List<List<(string Color, string Text)>> lines, AchievementDef def) =>
        ClusterFGumpText.Lines(string.Join("\n", lines.Select(l => string.Concat(l.Select(r => r.Text)))),
            GumpWidth - TextX(def) - 16 - 8) * ClusterFGumpText.LineHeight + 4;

    private static int RowHeight(PageRow row, string username)
    {
        if (row.Def == null)
            return HeaderRowH;

        var html = HtmlHeight(RowLines(row.Def, row.Earned, username), row.Def);
        return Math.Max(html + 2, row.Def.ItemGumpId > 0 ? IconH : 0) + RowGap;
    }

    /// <summary>Pages of whole rows, each page's rows fitting in <paramref name="room"/>; a page always takes one row.</summary>
    internal static List<(int Start, int End)> Paginate(IReadOnlyList<int> heights, int room)
    {
        var pages = new List<(int, int)>();
        var start = 0;
        while (start < heights.Count)
        {
            var end = start;
            var used = 0;
            while (end < heights.Count && (end == start || used + heights[end] <= room))
            {
                used += heights[end];
                end++;
            }

            pages.Add((start, end));
            start = end;
        }

        return pages;
    }

    // ── Quest list ────────────────────────────────────────────────────────

    private void AddQuestContent(int listY, int listH)
    {
        var ctx = MLQuestSystem.GetContext(_pm);
        var sb  = new StringBuilder();

        var doneCount  = 0;
        var totalCount = NewHavenQuests.Length;

        sb.Append("<BASEFONT COLOR=#888888>-- New Haven Trainer Quests --</BASEFONT><BR>");

        foreach (var (questType, title) in NewHavenQuests)
        {
            var done = ctx?.HasDoneQuest(questType) ?? false;
            if (done) doneCount++;

            if (done)
            {
                sb.Append($"<BASEFONT COLOR=#FFD700>[x] {title}</BASEFONT><BR>"); // cc-P57 Part F: ASCII (no check-mark glyph in the font)
            }
            else
            {
                sb.Append($"<BASEFONT COLOR=#555555>[ ] {title}</BASEFONT><BR>");
            }
        }

        // Inline summary header — can't easily insert at top after building, so add at bottom
        sb.Append($"<BR><BASEFONT COLOR=#888888>Completed: {doneCount} / {totalCount}</BASEFONT>");

        AddHtml(16, listY, GumpWidth - 32, listH, sb.ToString(), false, true);
    }

    // ── Response ──────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return; // Close

        if (info.ButtonID == 200)
        {
            if (_pm.Account is Accounting.IAccount acct)
                _pm.SendGump(new GuildProgressGump(_pm, acct));
            return;
        }

        if (info.ButtonID == TabQuests)
        {
            _pm.SendGump(new AchievementsGump(_pm, null, true));
            return;
        }

        // ── Pagination ────────────────────────────────────────────────────
        if (info.ButtonID == PagePrev)
        {
            _pm.SendGump(new AchievementsGump(_pm, _filter, false, _page - 1));
            return;
        }
        if (info.ButtonID == PageNext)
        {
            _pm.SendGump(new AchievementsGump(_pm, _filter, false, _page + 1));
            return;
        }

        var tabIdx = info.ButtonID - TabBase;
        if (tabIdx >= 0 && tabIdx < CategoryTabs.Length)
            _pm.SendGump(new AchievementsGump(_pm, CategoryTabs[tabIdx].Cat, false));
    }
}
