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

namespace Server;

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
    public int                 ProgressThreshold { get; } // target count for progress bar

    public AchievementDef(string key, string title, string desc,
                          AchievementCategory cat, int ap, int renown,
                          string flavorText        = "",
                          int    itemGumpId        = 0,
                          bool   hidden            = false,
                          string? prerequisiteKey  = null,
                          Type[]? rewardItems      = null,
                          string? progressCounter  = null,
                          int     progressThreshold = 0)
    {
        Key               = key;
        Title             = title;
        Description       = desc;
        FlavorText        = flavorText;
        Category          = cat;
        AP                = ap;
        Renown            = renown;
        ItemGumpId        = itemGumpId;
        Hidden            = hidden;
        PrerequisiteKey   = prerequisiteKey;
        RewardItems       = rewardItems ?? Array.Empty<Type>();
        ProgressCounter   = progressCounter;
        ProgressThreshold = progressThreshold;
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
            flavor:      "The bar was on the floor. You cleared it.",
            itemGumpId:  0x14EC);   // Treasure Map — a world to explore

        // ── Combat ───────────────────────────────────────────────────────
        Reg("combat.first_blood",
            "Oh Look, It's Dead",
            "Slay your first creature. Something has died because of you. The System has logged this.",
            AchievementCategory.Combat, ap: 5, renown: 5,
            flavor:           "You are, by definition, a killer now. Congratulations.",
            itemGumpId:       0x0F5E,   // Broadsword
            progressCounter:  "kills", progressThreshold: 1);

        Reg("combat.century",
            "Getting Into It",
            "Slay 100 creatures. You've developed what the System charitably calls a process.",
            AchievementCategory.Combat, ap: 15, renown: 25,
            flavor:           "They had families. Probably.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.first_blood",
            progressCounter:  "kills", progressThreshold: 100);

        Reg("combat.thousand",
            "This Is Fine",
            "Slay 1,000 creatures. You have personally ended more lives than most Britannians will ever encounter.",
            AchievementCategory.Combat, ap: 35, renown: 75,
            flavor:           "The local monster population has filed a formal grievance.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.century",
            progressCounter:  "kills", progressThreshold: 1000);

        Reg("combat.ten_thousand",
            "Extremely Normal Behavior",
            "Slay 10,000 creatures. At this point you are less an adventurer and more a geological event with opinions.",
            AchievementCategory.Combat, ap: 100, renown: 250,
            flavor:           "The System is choosing to look the other way.",
            itemGumpId:       0x0F5E,   // Broadsword
            prerequisiteKey:  "combat.thousand",
            hidden:           true,     // secret — players don't see this until earned
            progressCounter:  "kills", progressThreshold: 10000);

        Reg("combat.oldhaven_mage",
            "Specific Grudge",
            "Slay 25 Old Haven Mages. Whatever they did to earn this level of focused personal attention, the System declines to investigate.",
            AchievementCategory.Combat, ap: 20, renown: 50,
            flavor:          "You really don't like these guys.",
            itemGumpId:      0x0E3B,    // Spellbook
            progressCounter: "oldhaven_mage_kills", progressThreshold: 25);

        Reg("combat.drelgor",
            "You Fought a Man Called 'The Impaler' and Won",
            "Slay Drelgor the Impaler. His title was a warning. You did not take the hint. You were correct not to.",
            AchievementCategory.Combat, ap: 25, renown: 100,
            flavor:     "Bold strategy. Unambiguously effective.",
            itemGumpId: 0x0F49,         // Axe — heavy weapon energy
            hidden:     true);          // secret — finding Drelgor is the discovery

        // ── Skills ───────────────────────────────────────────────────────
        Reg("skills.apprentice",
            "You've Read the Introduction",
            "Reach 50 in any skill. You now understand approximately one-sixth of something. The journey has, technically, begun.",
            AchievementCategory.Skills, ap: 5, renown: 10,
            flavor:     "A promising start. The bar is currently underground.",
            itemGumpId: 0x0E34);        // Scroll

        Reg("skills.journeyman",
            "Dangerously Competent",
            "Reach 100 in any skill. Your incompetence is no longer immediately life-threatening. Progress.",
            AchievementCategory.Skills, ap: 15, renown: 35,
            flavor:          "Most people stop here. You don't seem like most people.",
            itemGumpId:      0x0E34,    // Scroll
            prerequisiteKey: "skills.apprentice");

        Reg("skills.master",
            "Unsettling Dedication",
            "Reach 200 in any skill. You've gone well past the point where normal people stop and develop hobbies. The System respects this, cautiously.",
            AchievementCategory.Skills, ap: 40, renown: 100,
            flavor:          "Whatever you used to do with your free time, this replaced it.",
            itemGumpId:      0x0E34,    // Scroll
            prerequisiteKey: "skills.journeyman");

        Reg("skills.grandmaster",
            "What Have You Done With Your Life (Respect)",
            "Reach 300 in any skill. The absolute ceiling has been touched. The System is both deeply impressed and quietly concerned.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            flavor:          "The ceiling has a handprint on it now. That's yours.",
            itemGumpId:      0x0F26,    // Diamond — only fitting
            prerequisiteKey: "skills.master");

        // ── Discovery ────────────────────────────────────────────────────
        Reg("discovery.old_haven",
            "A Bad Neighborhood",
            "Discover the ruins of Old Haven. Nobody lives there anymore. That is, objectively, information you now possess.",
            AchievementCategory.Discovery, ap: 15, renown: 35,
            flavor:     "The real treasure was the foreboding atmosphere.",
            itemGumpId: 0x14EC);        // Treasure Map

        // ── League ───────────────────────────────────────────────────────
        Reg("league.registered_citizen",
            "Officially a Problem",
            "Register with the League of Extraordinary Citizens. You are now in the database. This cannot be undone.",
            AchievementCategory.League, ap: 10, renown: 25,
            flavor:     "They can't un-know this. Neither can you.",
            itemGumpId: 0x0FF4);        // Brown Book — your file

        Reg("league.first_dispatch",
            "Minimally Informed",
            "Read the League Dispatch for the first time. You are now among the people who technically know what's going on.",
            AchievementCategory.League, ap: 5, renown: 10,
            flavor:          "Information received. Presumably retained.",
            itemGumpId:      0x0E34,    // Scroll — the dispatch
            prerequisiteKey: "league.registered_citizen");

        Reg("league.first_referral",
            "Someone Vouched For You",
            "Receive a guild referral from the League. A professional organization wants to meet you. Please don't make it weird.",
            AchievementCategory.League, ap: 5, renown: 10,
            flavor:          "They seem excited. It would be a shame to disappoint them.",
            itemGumpId:      0x0FF4,    // Brown Book
            prerequisiteKey: "league.registered_citizen");

        Reg("league.guildbound",
            "You're Someone's Problem Now",
            "Join your first professional guild. They have, in some capacity, accepted responsibility for you.",
            AchievementCategory.League, ap: 15, renown: 50,
            flavor:          "Welcome to the family. There's a dues structure.",
            itemGumpId:      0x1B76,    // Metal Shield — belonging to something
            prerequisiteKey: "league.first_referral");

        // ── Combat additions ──────────────────────────────────────────────
        // 0x0F5E = Broadsword   0x0F49 = Battle Axe   0x0E34 = Scroll

        Reg("combat.five_hundred",
            "Hitting Your Stride",
            "Slay 500 creatures. The monsters have noticed there is a pattern here.",
            AchievementCategory.Combat, ap: 25, renown: 60,
            flavor:          "Consistent. That's one word for it.",
            itemGumpId:      0x0F5E,
            prerequisiteKey: "combat.century",
            progressCounter: "kills", progressThreshold: 500);

        Reg("combat.five_thousand",
            "You Have a Type (It's Dead)",
            "Slay 5,000 creatures. You have ended more lives than most natural disasters.",
            AchievementCategory.Combat, ap: 70, renown: 175,
            flavor:          "The System has no further commentary. Carry on.",
            itemGumpId:      0x0F5E,
            hidden:          true,
            prerequisiteKey: "combat.thousand",
            progressCounter: "kills", progressThreshold: 5000);

        Reg("combat.undead_hunter",
            "They Were Already Dead Once",
            "Slay 50 undead. You've dispatched the dispatched. Twice. The System has moved on from this word.",
            AchievementCategory.Combat, ap: 15, renown: 35,
            flavor:          "It barely counts. The System counted it anyway.",
            itemGumpId:      0x0F5E,
            progressCounter: "undead_kills", progressThreshold: 50);

        Reg("combat.rat_problem",
            "Pest Control at Scale",
            "Slay 25 ratmen. Whatever they did, they seem to have done it in large numbers.",
            AchievementCategory.Combat, ap: 10, renown: 20,
            flavor:          "The problem has been addressed. Aggressively.",
            itemGumpId:      0x0F49,
            progressCounter: "ratman_kills", progressThreshold: 25);

        Reg("combat.orc_grudge",
            "Made Your Position Clear to the Orcs",
            "Slay 50 orcs. You've formed and acted on very strong opinions about the orcish community.",
            AchievementCategory.Combat, ap: 15, renown: 35,
            flavor:          "The orcish community has received your feedback.",
            itemGumpId:      0x0F49,
            progressCounter: "orc_kills", progressThreshold: 50);

        Reg("combat.first_death",
            "Oh. So THAT'S What Resurrection Feels Like.",
            "Die for the first time. You have now experienced mortality from the inside. You got better.",
            AchievementCategory.Combat, ap: 5, renown: 0,
            flavor:          "The System notes you got better. Barely.",
            itemGumpId:      0x0E34);   // Scroll — resurrection scroll vibe

        Reg("combat.pvp_first",
            "Morally Complicated",
            "Kill another player. The System is not here to judge. The System has logged it.",
            AchievementCategory.Combat, ap: 15, renown: 50,
            flavor:          "Technically legal in at least one facet.",
            itemGumpId:      0x0F5E,
            hidden:          true);     // Surprise when you do it

        // ── Exploration additions ─────────────────────────────────────────
        // 0x14EC = Treasure Map   0x0F26 = Diamond

        Reg("exploration.trammel",
            "Legal Jurisdiction Acquired",
            "Set foot in Trammel. The safe side. Statistically the permanent address of most Britannians.",
            AchievementCategory.Exploration, ap: 5, renown: 10,
            flavor:          "The one where monsters are less likely to specifically seek you out.",
            itemGumpId:      0x14EC);

        Reg("exploration.felucca",
            "Bold Move. Noted.",
            "Set foot in Felucca. Some things don't want you here. Others are actively pursuing you.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            flavor:          "The System notes your continued survival. With mild surprise.",
            itemGumpId:      0x14EC,
            prerequisiteKey: "exploration.trammel");

        Reg("exploration.ilshenar",
            "You Found the Third One",
            "Set foot in Ilshenar. The lost lands that weren't entirely lost, just inconveniently located.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            flavor:          "Not everyone finds this place. You did. That's something.",
            itemGumpId:      0x14EC);

        Reg("exploration.malas",
            "The City That Shouldn't Float, Does",
            "Set foot in Malas. A shadow realm that defies physics, zoning laws, and reasonable expectations.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            flavor:          "You went to the dark floating city. Voluntarily. Okay.",
            itemGumpId:      0x14EC);

        Reg("exploration.tokuno",
            "Islands That Time Forgot, Then Found Again",
            "Set foot in Tokuno. The Empire expects composure. Try to provide it.",
            AchievementCategory.Exploration, ap: 10, renown: 25,
            flavor:          "Diplomatic incident: narrowly averted.",
            itemGumpId:      0x14EC);

        Reg("exploration.all_facets",
            "Frequent Flyer: Unlimited Edition",
            "Visit all five facets of Britannia. You have been everywhere. Possibly too many places.",
            AchievementCategory.Exploration, ap: 50, renown: 100,
            flavor:          "The passport is full. The System is out of stamps.",
            itemGumpId:      0x0F26,
            hidden:          true);     // Checked manually — no single prereq

        // ── Skill additions ───────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0x13E3 = Smith's Hammer   0x0E3B = Spellbook

        // ── Expanded skill cap milestones (cap = 300) ─────────────────────
        // Tiers: 50 Apprentice | 100 Journeyman | 150 Advanced | 200 Master
        //        250 Paragon   | 300 Grandmaster

        Reg("skills.advanced",
            "Exceeding the Old Normal",
            "Reach 150 in any skill. You are now above what was previously considered the ceiling. Someone else's ceiling.",
            AchievementCategory.Skills, ap: 20, renown: 50,
            flavor:          "150. The old standard GM cap was 100. The System is aware you noticed.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.journeyman");

        Reg("skills.paragon",
            "This Is No Longer a Normal Amount",
            "Reach 250 in any skill. Three-quarters of the way to the actual ceiling. The System is watching.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            flavor:          "250. The System has revised its projections upward.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.master");

        // ── Multi-skill breadth (expanded tiers) ──────────────────────────

        Reg("skills.triple_journeyman",
            "Competent in Three Directions",
            "Reach 100 in three different skills. You are no longer a one-trick pony. You are a three-trick pony.",
            AchievementCategory.Skills, ap: 25, renown: 60,
            flavor:          "Three skills at cap. The System approves of diversification.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.journeyman");

        Reg("skills.five_journeyman",
            "Suspiciously Well-Rounded",
            "Reach 100 in five different skills. At this point it's a lifestyle choice.",
            AchievementCategory.Skills, ap: 50, renown: 125,
            flavor:          "Five skills at 100. The System is mildly concerned about your free time.",
            itemGumpId:      0x0F26,
            prerequisiteKey: "skills.triple_journeyman");

        Reg("skills.triple_master",
            "Elite in Three Disciplines",
            "Reach 200 in three different skills. You have pushed three separate ceilings significantly upward.",
            AchievementCategory.Skills, ap: 75, renown: 200,
            flavor:          "Three skills at 200. This was clearly done on purpose.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.master");

        Reg("skills.triple_grandmaster",
            "The Ceiling Has Three Handprints Now",
            "Reach 300 in three different skills. The System does not have adequate superlatives for this.",
            AchievementCategory.Skills, ap: 150, renown: 500,
            flavor:          "Three skills at 300. The System is speechless. That's a first.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.grandmaster");

        // ── Mining — extended tiers ───────────────────────────────────────

        Reg("skills.mining_elite",
            "The Mountain Yields to You",
            "Reach 200 in Mining. You have gone significantly past what anyone thought was possible with a pickaxe.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            flavor:          "200 Mining. The rocks have filed for relocation.",
            itemGumpId:      0x0E86,
            prerequisiteKey: "skills.mining_gm");

        Reg("skills.mining_legend",
            "The Earth Has Given Up Entirely",
            "Reach 300 in Mining. Full cap. The ore practically introduces itself.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            flavor:          "300 Mining. You are the mountain now.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.mining_elite");

        // ── Blacksmithy — extended tiers ──────────────────────────────────

        Reg("skills.smith_elite",
            "The Forge Has No More Objections",
            "Reach 200 in Blacksmithy. Every alloy cooperates. The hammer is an extension of your intent.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            flavor:          "200 Blacksmithy. The metal stopped arguing.",
            itemGumpId:      0x13E3,
            prerequisiteKey: "skills.smith_gm");

        Reg("skills.smith_legend",
            "The Anvil's Opinions of You Are Reverent",
            "Reach 300 in Blacksmithy. Full cap. The craft has nothing left to teach. You are the craft.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            flavor:          "300 Blacksmithy. The System suggests naming a technique after yourself.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.smith_elite");

        // ── Magery — extended tiers ───────────────────────────────────────

        Reg("skills.magery_elite",
            "Arcane Comprehension: Unsettling",
            "Reach 200 in Magery. The spellbook is essentially a formality at this point.",
            AchievementCategory.Skills, ap: 60, renown: 150,
            flavor:          "200 Magery. Reality is taking your calls now.",
            itemGumpId:      0x0E3B,
            prerequisiteKey: "skills.magery_gm");

        Reg("skills.magery_legend",
            "Reality Is Mostly a Suggestion Now",
            "Reach 300 in Magery. Full cap. The distinction between intent and outcome has become very narrow.",
            AchievementCategory.Skills, ap: 100, renown: 300,
            flavor:          "300 Magery. The System recommends not thinking too hard about what this means.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.magery_elite");

        Reg("skills.renaissance_man",
            "Comfortably Mediocre in Several Things",
            "Reach 50 in five different skills. You've committed to nothing while dabbling in everything. Classic.",
            AchievementCategory.Skills, ap: 15, renown: 25,
            flavor:          "Five half-skills. That's approximately two and a half whole skills.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "skills.apprentice");

        Reg("skills.polymath",
            "Embarrassingly Well-Rounded",
            "Reach 50 in ten different skills. At some point this became a character trait.",
            AchievementCategory.Skills, ap: 35, renown: 75,
            flavor:          "The System has stopped trying to categorize you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "skills.renaissance_man");

        Reg("skills.mining_gm",
            "The Earth Has No Secrets From You",
            "Reach Grandmaster in Mining. Every rock type, every vein depth, catalogued.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            flavor:          "GM Miner. The ore knows.",
            itemGumpId:      0x0E86);

        Reg("skills.smith_gm",
            "Hammer Time (Permanent)",
            "Reach Grandmaster in Blacksmithy. Metal bends to your will. Almost entirely.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            flavor:          "The anvil has opinions about you. They are positive.",
            itemGumpId:      0x13E3);

        Reg("skills.magery_gm",
            "Memorized Every Word in Every Spellbook",
            "Reach Grandmaster in Magery. The arcane syllables flow freely now. Perhaps uncomfortably so.",
            AchievementCategory.Skills, ap: 50, renown: 100,
            flavor:          "The System recommends care around residential areas during practice.",
            itemGumpId:      0x0E3B);

        // ── Mining ────────────────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0x0F26 = Diamond

        Reg("mining.first_vein",
            "The Ground Didn't Want That",
            "Mine your first non-iron colored ore. The earth has relinquished something and you took it.",
            AchievementCategory.Mining, ap: 10, renown: 20,
            flavor:          "Step one of a very long relationship with rocks.",
            itemGumpId:      0x0E86);

        Reg("mining.prospector",
            "Certified Rock Enthusiast",
            "Discover 5 different ore types in your Prospector's Logbook. The rocks respect you now.",
            AchievementCategory.Mining, ap: 20, renown: 50,
            flavor:          "The logbook is filling up. The rocks are taking notes.",
            itemGumpId:      0x0E86,
            progressCounter: "ore_types_discovered", progressThreshold: 5);

        Reg("mining.surveyor",
            "The Earth Has a Lot Going On",
            "Discover 10 different ore types. Half of Britannia's underground is now in your notes.",
            AchievementCategory.Mining, ap: 40, renown: 100,
            flavor:          "Your logbook is getting heavy. This is good.",
            itemGumpId:      0x0E86,
            hidden:          true,
            prerequisiteKey: "mining.prospector",
            progressCounter: "ore_types_discovered", progressThreshold: 10);

        Reg("mining.master_prospector",
            "Nothing Left to Find. Sort Of.",
            "Discover all 16 ore types. Complete mineral survey of Britannia. The System is genuinely impressed.",
            AchievementCategory.Mining, ap: 100, renown: 300,
            flavor:          "The survey is complete. The earth has no more surprises for you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "mining.surveyor",
            progressCounter: "ore_types_discovered", progressThreshold: 16);

        Reg("mining.ore_1k",
            "Industrial Scale Geology",
            "Mine 1,000 total ore. You have moved a quantifiable portion of Britannia's crust.",
            AchievementCategory.Mining, ap: 25, renown: 50,
            flavor:          "Structural geologists have filed a complaint.",
            itemGumpId:      0x0E86,
            progressCounter: "total_ore_mined", progressThreshold: 1000);

        Reg("mining.ore_10k",
            "You Are Technically a Geological Event",
            "Mine 10,000 total ore. Mountains have opinions. They are not sharing them.",
            AchievementCategory.Mining, ap: 75, renown: 200,
            flavor:          "The crust has moved. This is your doing.",
            itemGumpId:      0x0E86,
            hidden:          true,
            prerequisiteKey: "mining.ore_1k",
            progressCounter: "total_ore_mined", progressThreshold: 10000);

        Reg("mining.jacobs_legacy",
            "Jacob's Legacy: Accepted",
            "Equip any of Jacob's Pickaxes. Some tools come with history. This one comes with expectations.",
            AchievementCategory.Mining, ap: 15, renown: 35,
            flavor:          "The pickaxe has a story. You are now part of it.",
            itemGumpId:      0x0E86);

        Reg("mining.felucca_vein",
            "Unsafe Mining Practices (Felucca Division)",
            "Mine ore in Felucca. The resources are better here. So are the consequences.",
            AchievementCategory.Mining, ap: 20, renown: 50,
            flavor:          "Danger premium: earned.",
            itemGumpId:      0x0E86,
            prerequisiteKey: "exploration.felucca");

        // ── Crafting ──────────────────────────────────────────────────────
        // 0x13E3 = Smith's Hammer   0x0F9D = Sewing Kit   0x1BF2 = Iron Ingot

        Reg("crafting.first_item",
            "You Made a Thing. It Exists Now.",
            "Craft your first item. Raw material has become an object. Your fingerprints are on it.",
            AchievementCategory.Crafting, ap: 5, renown: 10,
            flavor:          "Crafting: begun. It only gets more expensive from here.",
            itemGumpId:      0x13E3);

        Reg("crafting.exceptional",
            "The System Acknowledges Quality",
            "Craft an exceptional item. Something you made is measurably better than it had to be.",
            AchievementCategory.Crafting, ap: 20, renown: 50,
            flavor:          "Exceptional. Not aspirationally. Actually.",
            itemGumpId:      0x0F26,
            prerequisiteKey: "crafting.first_item");

        Reg("crafting.ingots_100",
            "Halfway to a Real Inventory",
            "Smelt 100 ingots. You have converted rock into slightly more useful rock.",
            AchievementCategory.Crafting, ap: 15, renown: 30,
            flavor:          "The ore situation has been processed.",
            itemGumpId:      0x1BF2,
            progressCounter: "ingots_smelted", progressThreshold: 100);

        Reg("crafting.blacksmith_first",
            "Hammer Applied to Metal: Successfully",
            "Craft your first smithed item. You are a blacksmith now, informally.",
            AchievementCategory.Crafting, ap: 10, renown: 20,
            flavor:          "The anvil has been consulted. It has noted your contribution.",
            itemGumpId:      0x13E3,
            prerequisiteKey: "crafting.first_item");

        Reg("crafting.tailor_first",
            "Sewing: Harder Than It Looks",
            "Craft your first tailored item. Needle, thread, and concentrated effort. Something wearable has resulted.",
            AchievementCategory.Crafting, ap: 10, renown: 20,
            flavor:          "The garment exists. That is the whole achievement.",
            itemGumpId:      0x0F9D,
            prerequisiteKey: "crafting.first_item");

        // ── Collection ────────────────────────────────────────────────────
        // 0x0E86 = Pickaxe   0xEED = Gold coins   0x0F26 = Diamond

        Reg("collection.ore_standard",
            "Ore Bingo: Standard Edition",
            "Mine all 8 standard colored ore types (Dull Copper through Valorite). The classics.",
            AchievementCategory.Collection, ap: 25, renown: 75,
            flavor:          "Every serious miner has done this. You are now a serious miner.",
            itemGumpId:      0x0E86,
            progressCounter: "ore_types_discovered", progressThreshold: 8);

        Reg("collection.ore_full",
            "Ore Bingo: Expert Edition",
            "Mine all 16 ore types including rare veins. The complete mineral vocabulary of Shattered Legacy.",
            AchievementCategory.Collection, ap: 75, renown: 200,
            flavor:          "The logbook is satisfied. The earth has no surprises left.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "collection.ore_standard",
            progressCounter: "ore_types_discovered", progressThreshold: 16);

        Reg("collection.gold_10k",
            "Double-Digit Thousands Is a Personality",
            "Accumulate 10,000 gold in your bank. Enough to be inconvenient to lose.",
            AchievementCategory.Collection, ap: 10, renown: 20,
            flavor:          "The wealth is real. For now.",
            itemGumpId:      0xEED);

        Reg("collection.gold_100k",
            "The System Calculates: Comfortable",
            "Accumulate 100,000 gold in your bank. Six figures in Britannian currency. Genuinely impressive.",
            AchievementCategory.Collection, ap: 35, renown: 75,
            flavor:          "Don't lose it.",
            itemGumpId:      0xEED,
            prerequisiteKey: "collection.gold_10k");

        // ── Discovery additions ───────────────────────────────────────────

        Reg("discovery.survey_report",
            "Contributing to Science. Technically.",
            "Report an ore discovery to the Survey Archivist. Your data is now in the official record.",
            AchievementCategory.Discovery, ap: 15, renown: 35,
            flavor:          "The Archivist has filed it. Probably in the right folder.",
            itemGumpId:      0x14EC);

        Reg("discovery.treasure_map",
            "X Marked the Spot. You Found the X.",
            "Decode and excavate a treasure map chest. The treasure was buried. You unburied it.",
            AchievementCategory.Discovery, ap: 20, renown: 50,
            flavor:          "The treasure hunters of old would approve. Briefly, before wanting a cut.",
            itemGumpId:      0x14EC);

        // ── Legacy additions ──────────────────────────────────────────────

        Reg("legacy.new_haven_1",
            "The Tutorial Has a Checkbox",
            "Complete your first New Haven trainer quest. The questgivers are broadly satisfied.",
            AchievementCategory.Legacy, ap: 5, renown: 10,
            flavor:          "One down. The others are aware you exist now.",
            itemGumpId:      0x0E34);

        Reg("legacy.new_haven_10",
            "New Haven: Mostly Done With You",
            "Complete 10 New Haven trainer quests. A double-digit investment in local civic responsibility.",
            AchievementCategory.Legacy, ap: 20, renown: 50,
            flavor:          "The trainers are surprised you came back. Ten times.",
            itemGumpId:      0x0E34,
            prerequisiteKey: "legacy.new_haven_1",
            progressCounter: "new_haven_quests", progressThreshold: 10);

        Reg("legacy.new_haven_all",
            "The System Is Running Out of Superlatives",
            "Complete all 38 New Haven trainer quests. Every quest. Every trainer. Completely done.",
            AchievementCategory.Legacy, ap: 75, renown: 200,
            flavor:          "Done. Actually completely done. The System chooses to believe you.",
            itemGumpId:      0x0F26,
            hidden:          true,
            prerequisiteKey: "legacy.new_haven_10",
            progressCounter: "new_haven_quests", progressThreshold: 38);

        Reg("legacy.old_haven_local",
            "Old Haven's Least Welcome Regular",
            "Slay 50 creatures in Old Haven's ruins. You are the reason tourism has not recovered.",
            AchievementCategory.Legacy, ap: 20, renown: 50,
            flavor:          "They know your name there. It is not used fondly.",
            itemGumpId:      0x0F49,
            progressCounter: "oldhaven_kills", progressThreshold: 50);

        // ── League additions ──────────────────────────────────────────────

        Reg("league.ap_100",
            "The System Is Taking Notes",
            "Accumulate 100 Achievement Points. You are officially on the board.",
            AchievementCategory.League, ap: 10, renown: 25,
            flavor:          "100 AP. A beginning. Or a warning sign. Possibly both.",
            itemGumpId:      0x0FF4,
            prerequisiteKey: "league.registered_citizen");

        Reg("league.ap_500",
            "Deeply, Embarrassingly Invested",
            "Accumulate 500 Achievement Points. Half a thousand. The System has taken formal notice.",
            AchievementCategory.League, ap: 25, renown: 75,
            flavor:          "At this point this is a hobby. An aggressive one.",
            itemGumpId:      0x0FF4,
            hidden:          true,
            prerequisiteKey: "league.ap_100");

        Reg("league.renown_500",
            "People Know Your Name Now",
            "Accumulate 500 Renown. Word travels. About you. The System confirms: this is about you.",
            AchievementCategory.League, ap: 20, renown: 50,
            flavor:          "The reputation precedes you. It arrives walking fast.",
            itemGumpId:      0x1B76,
            prerequisiteKey: "league.guildbound");

        Reg("league.veteran",
            "Still Here. Somehow.",
            "Log in 10 times. You have returned. Multiple times. The System acknowledges your persistence.",
            AchievementCategory.League, ap: 15, renown: 35,
            flavor:          "Ten sessions. The System marks this: 'committed.'",
            itemGumpId:      0x0FF4,
            prerequisiteKey: "league.registered_citizen",
            progressCounter: "login_count", progressThreshold: 10);

        Reg("league.known",
            "You Live Here Now. It's Fine.",
            "Log in 50 times. Shattered Legacy is where you live now. The rent is your time.",
            AchievementCategory.League, ap: 40, renown: 100,
            flavor:          "50 logins. You're not a visitor anymore.",
            itemGumpId:      0x0FF4,
            hidden:          true,
            prerequisiteKey: "league.veteran",
            progressCounter: "login_count", progressThreshold: 50);
    }

    private static void Reg(string key, string title, string desc,
                             AchievementCategory cat, int ap, int renown,
                             string  flavor           = "",
                             int     itemGumpId       = 0,
                             bool    hidden           = false,
                             string? prerequisiteKey  = null,
                             Type[]? rewardItems      = null,
                             string? progressCounter  = null,
                             int     progressThreshold = 0) =>
        _defs[key] = new AchievementDef(key, title, desc, cat, ap, renown,
                                        flavor, itemGumpId, hidden,
                                        prerequisiteKey, rewardItems,
                                        progressCounter, progressThreshold);

    // ── Public API ─────────────────────────────────────────────────────────

    public static bool HasEarned(IAccount acct, string key) =>
        _earned.TryGetValue(acct.Username, out var set) && set.Contains(key);

    public static IReadOnlyCollection<string> GetEarnedKeys(string username) =>
        _earned.TryGetValue(username, out var set)
            ? set
            : (IReadOnlyCollection<string>)Array.Empty<string>();

    /// <summary>
    /// Grant an achievement if not already earned. Returns true on new earn.
    /// Enforces prerequisite chain, updates AP and Renown, spawns any reward
    /// items into the player's backpack, and shows the earn popup.
    /// </summary>
    public static bool TryGrant(IAccount acct, string key)
    {
        if (!_defs.TryGetValue(key, out var def)) return false;

        // Prerequisite must be earned first.
        if (def.PrerequisiteKey != null && !HasEarned(acct, def.PrerequisiteKey))
            return false;

        if (!GetEarnedMutable(acct.Username).Add(key)) return false; // already earned

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        data.AchievementPoints += def.AP;
        data.Renown            += def.Renown;

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
        var kills = Increment(acct.Username, "kills");
        TryGrant(acct, "combat.first_blood");
        if (kills >= 100)    TryGrant(acct, "combat.century");
        if (kills >= 500)    TryGrant(acct, "combat.five_hundred");
        if (kills >= 1_000)  TryGrant(acct, "combat.thousand");
        if (kills >= 5_000)  TryGrant(acct, "combat.five_thousand");
        if (kills >= 10_000) TryGrant(acct, "combat.ten_thousand");

        // ── Named bosses ───────────────────────────────────────────────────
        if (victim is OldHavenMage)
        {
            var mageKills = Increment(acct.Username, "oldhaven_mage_kills");
            if (mageKills >= 25) TryGrant(acct, "combat.oldhaven_mage");
        }

        if (victim is DrelgorTheImpaler)
            TryGrant(acct, "combat.drelgor");

        // ── PvP ────────────────────────────────────────────────────────────
        if (victim is PlayerMobile)
            TryGrant(acct, "combat.pvp_first");

        // ── Undead ────────────────────────────────────────────────────────
        if (victim is Zombie    || victim is Skeleton      || victim is Ghoul
                    || victim is Shade     || victim is Spectre      || victim is Wraith
                    || victim is Lich      || victim is LichLord      || victim is BoneKnight
                    || victim is SkeletalKnight || victim is SkeletalDragon)
        {
            var u = Increment(acct.Username, "undead_kills");
            if (u >= 50) TryGrant(acct, "combat.undead_hunter");
        }

        // ── Ratmen ────────────────────────────────────────────────────────
        if (victim is Ratman || victim is RatmanArcher || victim is RatmanMage)
        {
            var r = Increment(acct.Username, "ratman_kills");
            if (r >= 25) TryGrant(acct, "combat.rat_problem");
        }

        // ── Orcs ──────────────────────────────────────────────────────────
        if (victim is Orc     || victim is OrcBrute  || victim is OrcBomber
                   || victim is OrcCaptain || victim is OrcishMage || victim is OrcishLord)
        {
            var o = Increment(acct.Username, "orc_kills");
            if (o >= 50) TryGrant(acct, "combat.orc_grudge");
        }

        // ── Old Haven region (any mob) ─────────────────────────────────────
        if (IsInOldHaven(pm))
        {
            var oh = Increment(acct.Username, "oldhaven_kills");
            if (oh >= 50) TryGrant(acct, "legacy.old_haven_local");
        }
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
        if (value >= 50)  TryGrant(acct, "skills.apprentice");
        if (value >= 100) TryGrant(acct, "skills.journeyman");
        if (value >= 150) TryGrant(acct, "skills.advanced");
        if (value >= 200) TryGrant(acct, "skills.master");
        if (value >= 250) TryGrant(acct, "skills.paragon");
        if (value >= 300) TryGrant(acct, "skills.grandmaster");
    }

    /// <summary>
    /// Notify that a player has entered Old Haven.
    /// Wire to a movement/zone hook for the Old Haven bounds.
    /// </summary>
    public static void NotifyOldHavenVisit(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "discovery.old_haven");
    }

    // ── Event hooks ───────────────────────────────────────────────────────

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;

        TryGrant(acct, "exploration.citizen");

        // ── Skill achievements ─────────────────────────────────────────────
        var skillsAt50  = 0;
        var skillsAt100 = 0;
        var skillsAt200 = 0;
        var skillsAt300 = 0;
        for (var i = 0; i < pm.Skills.Length; i++)
        {
            var val = pm.Skills[i].Base;
            NotifySkillValue(pm, val);
            if (val >= 50)  skillsAt50++;
            if (val >= 100) skillsAt100++;
            if (val >= 200) skillsAt200++;
            if (val >= 300) skillsAt300++;
        }

        // Breadth — standard tiers
        if (skillsAt50 >= 5)  TryGrant(acct, "skills.renaissance_man");
        if (skillsAt50 >= 10) TryGrant(acct, "skills.polymath");

        // Breadth — expanded cap tiers
        if (skillsAt100 >= 3) TryGrant(acct, "skills.triple_journeyman");
        if (skillsAt100 >= 5) TryGrant(acct, "skills.five_journeyman");
        if (skillsAt200 >= 3) TryGrant(acct, "skills.triple_master");
        if (skillsAt300 >= 3) TryGrant(acct, "skills.triple_grandmaster");

        // Skill-specific tiers — Mining
        var miningBase = pm.Skills[SkillName.Mining].Base;
        if (miningBase >= 100) TryGrant(acct, "skills.mining_gm");
        if (miningBase >= 200) TryGrant(acct, "skills.mining_elite");
        if (miningBase >= 300) TryGrant(acct, "skills.mining_legend");

        // Skill-specific tiers — Blacksmithy
        var smithBase = pm.Skills[SkillName.Blacksmith].Base;
        if (smithBase >= 100) TryGrant(acct, "skills.smith_gm");
        if (smithBase >= 200) TryGrant(acct, "skills.smith_elite");
        if (smithBase >= 300) TryGrant(acct, "skills.smith_legend");

        // Skill-specific tiers — Magery
        var mageryBase = pm.Skills[SkillName.Magery].Base;
        if (mageryBase >= 100) TryGrant(acct, "skills.magery_gm");
        if (mageryBase >= 200) TryGrant(acct, "skills.magery_elite");
        if (mageryBase >= 300) TryGrant(acct, "skills.magery_legend");

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
            if (doneCount >= 1)  TryGrant(acct, "legacy.new_haven_1");
            if (doneCount >= 10) TryGrant(acct, "legacy.new_haven_10");
            if (doneCount >= 38) TryGrant(acct, "legacy.new_haven_all");
        }

        // ── Login count ───────────────────────────────────────────────────
        var logins = Increment(acct.Username, "login_count");
        if (logins >= 10) TryGrant(acct, "league.veteran");
        if (logins >= 50) TryGrant(acct, "league.known");

        // ── AP / Renown league thresholds ─────────────────────────────────
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (data.AchievementPoints >= 100) TryGrant(acct, "league.ap_100");
        if (data.AchievementPoints >= 500) TryGrant(acct, "league.ap_500");
        if (data.Renown >= 500)            TryGrant(acct, "league.renown_500");

        // ── Bank gold ─────────────────────────────────────────────────────
        var bankGold = pm.BankBox?.GetAmount(typeof(Gold)) ?? 0;
        if (bankGold >= 10_000)  TryGrant(acct, "collection.gold_10k");
        if (bankGold >= 100_000) TryGrant(acct, "collection.gold_100k");

        // ── Jacob's Pickaxe ───────────────────────────────────────────────
        CheckJacobsPickaxe(pm);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "combat.first_death");
    }

    [OnEvent(nameof(BaseCreature.CreatureDeathEvent))]
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

        if (map == Map.Trammel)  TryGrant(acct, "exploration.trammel");
        if (map == Map.Felucca)  TryGrant(acct, "exploration.felucca");
        if (map == Map.Ilshenar) TryGrant(acct, "exploration.ilshenar");
        if (map == Map.Malas)    TryGrant(acct, "exploration.malas");
        if (map == Map.Tokuno)   TryGrant(acct, "exploration.tokuno");

        // All-facets check — only after all five individually earned
        if (HasEarned(acct, "exploration.trammel") &&
            HasEarned(acct, "exploration.felucca")  &&
            HasEarned(acct, "exploration.ilshenar") &&
            HasEarned(acct, "exploration.malas")    &&
            HasEarned(acct, "exploration.tokuno"))
        {
            TryGrant(acct, "exploration.all_facets");
        }
    }

    /// <summary>
    /// Called each time colored ore is yielded. Wire from CompactOreSatchelRoutingHook.
    /// </summary>
    public static void NotifyMining(PlayerMobile pm, int amount, bool inFelucca)
    {
        if (pm.Account is not IAccount acct) return;

        TryGrant(acct, "mining.first_vein");

        var total = IncrementBy(acct.Username, "total_ore_mined", amount);
        if (total >= 1_000)  TryGrant(acct, "mining.ore_1k");
        if (total >= 10_000) TryGrant(acct, "mining.ore_10k");

        if (inFelucca) TryGrant(acct, "mining.felucca_vein");
    }

    /// <summary>
    /// Called when the Prospector's Logbook discovery count changes. Wire from
    /// CompactOreSatchelRoutingHook whenever a new ore type or vein is logged.
    /// </summary>
    public static void NotifyOreDiscoveryCount(PlayerMobile pm, int discoveredCount)
    {
        if (pm.Account is not IAccount acct) return;

        SetCounter(acct.Username, "ore_types_discovered", discoveredCount);

        if (discoveredCount >= 5)  TryGrant(acct, "mining.prospector");
        if (discoveredCount >= 8)  TryGrant(acct, "collection.ore_standard");
        if (discoveredCount >= 10) TryGrant(acct, "mining.surveyor");
        if (discoveredCount >= 16)
        {
            TryGrant(acct, "mining.master_prospector");
            TryGrant(acct, "collection.ore_full");
        }
    }

    /// <summary>Called when a survey report is submitted to the Survey Archivist.</summary>
    public static void NotifySurveyReport(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "discovery.survey_report");
    }

    /// <summary>Called when a treasure map chest is excavated and opened.</summary>
    public static void NotifyTreasureMap(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "discovery.treasure_map");
    }

    /// <summary>Called when any item is crafted. Pass exceptional=true for exceptional quality.</summary>
    public static void NotifyCraftedItem(PlayerMobile pm, bool exceptional)
    {
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "crafting.first_item");
        if (exceptional) TryGrant(acct, "crafting.exceptional");
    }

    /// <summary>Called when a smithed item is crafted.</summary>
    public static void NotifySmithedItem(PlayerMobile pm, bool exceptional)
    {
        NotifyCraftedItem(pm, exceptional);
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "crafting.blacksmith_first");
    }

    /// <summary>Called when a tailored item is crafted.</summary>
    public static void NotifyTailoredItem(PlayerMobile pm, bool exceptional)
    {
        NotifyCraftedItem(pm, exceptional);
        if (pm.Account is not IAccount acct) return;
        TryGrant(acct, "crafting.tailor_first");
    }

    /// <summary>Called when ingots are smelted. Pass the number of ingots produced.</summary>
    public static void NotifyIngotsSmelted(PlayerMobile pm, int amount)
    {
        if (pm.Account is not IAccount acct) return;
        var total = IncrementBy(acct.Username, "ingots_smelted", amount);
        if (total >= 100) TryGrant(acct, "crafting.ingots_100");
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

        if (HasJacobs(pm.FindItemOnLayer(Layer.TwoHanded)!)) { TryGrant(acct, "mining.jacobs_legacy"); return; }
        if (pm.Backpack == null) return;
        foreach (var item in pm.Backpack.Items)
            if (HasJacobs(item)) { TryGrant(acct, "mining.jacobs_legacy"); return; }
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
    private static void OnAchievementsCommand(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
            pm.SendGump(new AchievementsGump(pm));
    }

    [Usage("ClusterFAchievement <grant|revoke|info|list> [args]")]
    [Description("Admin achievement management.")]
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
        const int H = 136;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(4, 4, W - 8, H - 8);

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
            $"<BASEFONT COLOR=#999999>{bodyText}</BASEFONT>";

        AddHtml(contentX, 30, contentW, 70, html, false, false);

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
/// Tabs: All | Combat | Skills | Explore | Mining | Craft | Legacy | Quests
///
/// Achievements tab:
///   - Earned achievements listed first (gold, with AP/Renown), then locked (gray).
///   - Achievements with progress counters show a filled/empty block bar.
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

    private const int GumpWidth  = 580;
    private const int GumpHeight = 510;
    private const int BgGumpId   = 9270;

    // Tab button base ID — 100..107 for categories, 108 for quests
    private const int TabBase     = 100;
    private const int TabQuests   = 108;

    // Pagination button IDs
    private const int PagePrev = 300;
    private const int PageNext = 301;

    // Achievement row layout constants
    private const int RowH        = 52;
    private const int RowsPerPage = 6;

    private static readonly (string Label, AchievementCategory? Cat)[] CategoryTabs =
    [
        ("All",    null),
        ("Combat", AchievementCategory.Combat),
        ("Skills", AchievementCategory.Skills),
        ("Explore",AchievementCategory.Exploration),
        ("Mining", AchievementCategory.Mining),
        ("Craft",  AchievementCategory.Crafting),
        ("Legacy", AchievementCategory.Legacy),
        ("Discov", AchievementCategory.Discovery),
    ];

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
        AddAlphaRegion(10, 10, GumpWidth - 20, GumpHeight - 20);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(GumpWidth / 2 - 125, 13, 1154, "League of Extraordinary Citizens");
        AddLabel(GumpWidth / 2 - 80,  31, 999,  "Achievement Records");
        AddLabel(18, 13, 999, pm.Name);

        var ap     = data?.AchievementPoints ?? 0;
        var renown = data?.Renown ?? 0;
        var earned = earnedKeys.Count;
        var total  = ClusterFAchievementSystem.Definitions.Count;
        AddLabel(GumpWidth - 180, 13, 1154, $"AP: {ap}");
        AddLabel(GumpWidth - 180, 31, 999,  $"Renown: {renown}   {earned}/{total}");

        AddImageTiled(10, 54, GumpWidth - 20, 2, 9304);

        // ── Tab row — row 1: category tabs ───────────────────────────────
        const int tabY  = 60;
        const int tabY2 = tabY + 22; // second row for Quests tab
        var tabX = 14;
        for (var i = 0; i < CategoryTabs.Length; i++)
        {
            var (label, cat) = CategoryTabs[i];
            var isActive = !questsTab && cat == filter;
            AddButton(tabX, tabY, 4011, 4012, TabBase + i);
            AddLabel(tabX + 18, tabY + 2, isActive ? 1154 : 999, label);
            tabX += label.Length * 8 + 22;
        }

        // ── Tab row — row 2: Quests tab ───────────────────────────────────
        AddButton(14, tabY2, 4011, 4012, TabQuests);
        AddLabel(14 + 18, tabY2 + 2, questsTab ? 1154 : 999, "Quests");

        AddImageTiled(10, tabY2 + 22, GumpWidth - 20, 2, 9304);

        // ── Content area ─────────────────────────────────────────────────
        const int listY = tabY2 + 28;
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
        const int IconX       = 14;
        const int TextXIcon   = 56; // text X when icon is present
        const int TextXNoIcon = 14; // text X when no icon

        var allDefs  = ClusterFAchievementSystem.Definitions.Values;
        var filtered = (filter.HasValue
            ? allDefs.Where(d => d.Category == filter.Value)
            : allDefs)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Title)
            .ToList();

        var earnedList = filtered.Where(d =>  earnedKeys.Contains(d.Key)).ToList();
        // Hidden achievements that haven't been earned are completely invisible.
        var lockedList = filtered.Where(d => !earnedKeys.Contains(d.Key) && !d.Hidden).ToList();

        // Build a flat list of rows; section-header rows + achievement rows interleaved.
        var rows = new List<PageRow>();

        if (filtered.Count > 0)
        {
            if (earnedList.Count > 0)
            {
                rows.Add(new PageRow("━━ The System Has Recorded These ━━━━━━━━━━━━━━━━━━", null, false));
                foreach (var d in earnedList) rows.Add(new PageRow(null, d, true));
            }
            if (lockedList.Count > 0)
            {
                rows.Add(new PageRow("━━ Not Yet (The System Is Watching) ━━━━━━━━━━━━━━", null, false));
                foreach (var d in lockedList) rows.Add(new PageRow(null, d, false));
            }
        }

        if (rows.Count == 0)
        {
            AddLabel(18, listY + 10, 0x555, "No achievements in this category yet.");
            return;
        }

        var totalPages = (rows.Count + RowsPerPage - 1) / RowsPerPage;
        var page       = Math.Clamp(_page, 0, totalPages - 1);
        var pageStart  = page * RowsPerPage;
        var pageEnd    = Math.Min(pageStart + RowsPerPage, rows.Count);

        for (var i = pageStart; i < pageEnd; i++)
        {
            var rowY = listY + (i - pageStart) * RowH;
            var row  = rows[i];

            if (row.Header != null)
            {
                // ── Section divider row ──────────────────────────────────
                AddLabel(16, rowY + 4, 0x777, row.Header);
                AddImageTiled(16, rowY + 20, GumpWidth - 32, 2, 9304);
            }
            else if (row.Def != null)
            {
                // ── Achievement row ──────────────────────────────────────
                var def   = row.Def;
                var textX = def.ItemGumpId > 0 ? TextXIcon : TextXNoIcon;
                var textW = GumpWidth - textX - 16;

                if (def.ItemGumpId > 0)
                    AddItem(IconX, rowY + 4, def.ItemGumpId);

                AddHtml(textX, rowY + 2, textW, RowH - 4,
                    BuildRowHtml(def, row.Earned, username), false, false);
            }
        }

        // ── Pagination controls ──────────────────────────────────────────
        if (totalPages > 1)
        {
            var btnY = listY + RowsPerPage * RowH + 6;

            if (page > 0)
            {
                AddButton(GumpWidth / 2 - 90, btnY, 4011, 4012, PagePrev);
                AddLabel(GumpWidth / 2 - 72, btnY + 2, 999, "< Prev");
            }

            AddLabel(GumpWidth / 2 - 18, btnY + 2, 999, $"{page + 1}/{totalPages}");

            if (page < totalPages - 1)
            {
                AddButton(GumpWidth / 2 + 20, btnY, 4011, 4012, PageNext);
                AddLabel(GumpWidth / 2 + 38, btnY + 2, 999, "Next >");
            }
        }
    }

    /// <summary>Builds the HTML content for a single achievement row (no scroll, fixed height).</summary>
    private static string BuildRowHtml(AchievementDef def, bool earned, string username)
    {
        var sb = new StringBuilder();

        if (earned)
        {
            var secretTag = def.Hidden ? "<BASEFONT COLOR=#886633> [Secret]</BASEFONT>" : "";
            sb.Append($"<BASEFONT COLOR=#FFD700>{def.Title}</BASEFONT>{secretTag}");
            sb.Append($"<BASEFONT COLOR=#6699BB> [{def.AP} AP / {def.Renown} R]</BASEFONT><BR>");

            if (!string.IsNullOrWhiteSpace(def.FlavorText))
                sb.Append($"<BASEFONT COLOR=#4A7070>\"{def.FlavorText}\"</BASEFONT>");
            else
                sb.Append($"<BASEFONT COLOR=#888888>{def.Description}</BASEFONT>");

            if (def.RewardItems.Length > 0)
                sb.Append($"<BASEFONT COLOR=#558855> (+{def.RewardItems.Length} item)</BASEFONT>");
        }
        else
        {
            sb.Append($"<BASEFONT COLOR=#555555>{def.Title}</BASEFONT>");
            sb.Append($"<BASEFONT COLOR=#2A4455> [{def.AP} AP / {def.Renown} R]</BASEFONT><BR>");

            var playerEarned = ClusterFAchievementSystem.GetEarnedKeys(username);
            var defs         = ClusterFAchievementSystem.Definitions;
            if (def.PrerequisiteKey != null
                && !playerEarned.Contains(def.PrerequisiteKey)
                && defs.TryGetValue(def.PrerequisiteKey, out var prereqDef))
            {
                sb.Append($"<BASEFONT COLOR=#553333>Requires: {prereqDef.Title}</BASEFONT>");
            }
            else
            {
                sb.Append($"<BASEFONT COLOR=#3A3A3A>{def.Description}</BASEFONT>");
            }
        }

        // Progress bar (third line — only renders if row is tall enough)
        if (def.ProgressCounter != null && def.ProgressThreshold > 0)
        {
            var count    = ClusterFAchievementSystem.GetCounter(username, def.ProgressCounter);
            var clamped  = Math.Min(count, def.ProgressThreshold);
            var filled   = (int)(clamped * 12.0 / def.ProgressThreshold);
            var bar      = new string('█', filled) + new string('░', 12 - filled);
            var barColor = earned ? "#FFD700" : "#5A5A5A";
            var txtColor = earned ? "#AAAAAA" : "#444444";
            sb.Append($"<BR><BASEFONT COLOR={barColor}>{bar}</BASEFONT>");
            sb.Append($"<BASEFONT COLOR={txtColor}> {count:N0}/{def.ProgressThreshold:N0}</BASEFONT>");
        }

        return sb.ToString();
    }

    // ── Quest list ────────────────────────────────────────────────────────

    private void AddQuestContent(int listY, int listH)
    {
        var ctx = MLQuestSystem.GetContext(_pm);
        var sb  = new StringBuilder();

        var doneCount  = 0;
        var totalCount = NewHavenQuests.Length;

        sb.Append("<BASEFONT COLOR=#888888>━━ New Haven Trainer Quests ━━━━━━━━━━━━━━</BASEFONT><BR>");

        foreach (var (questType, title) in NewHavenQuests)
        {
            var done = ctx?.HasDoneQuest(questType) ?? false;
            if (done) doneCount++;

            if (done)
            {
                sb.Append($"<BASEFONT COLOR=#FFD700>[✓] {title}</BASEFONT><BR>");
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
