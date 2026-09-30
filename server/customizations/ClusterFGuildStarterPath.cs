// The guild starter path (F-9, cc-P15): guilds replace the New Haven training quests as the way a new
// character gets started. Notes in shard-migration notes/cc-P15-guild-starter-path.md.
//
// What lives here, each in one place:
//   GuildSkillTable      F-9's "Skills per guild": every SkillName in exactly one guild.
//   GuildStarterItems    which of the 26 New Haven quest rewards each guild hands out.
//   GuildLocations       where each guild's guildmaster stands (a guild may have several).
//   ClusterFGuildStarter tools on joining, starter items on request, replacements, the trainers' line.
//   GuildDirectionArrow  the "Show me the way" arrow (generalizes MinersCompactDirectionArrow).
//   GuildWelcome         the once-per-character first-login page.
//
// Per character, not per account: ClusterFAccountData v14 keeps a GuildStarterRecord per character
// serial. Membership is per character too since cc-P18 (CharacterGuildData, v15). "Once per loop" is once per
// character until loops exist.
//
// One ledger with the quests: taking an item here marks its MLQuest done for the character
// (MLQuestContext.SetDoneQuest, which only records, MLQuestContext.cs:156-168). A quest already
// turned in shows its item as received. The items are made by each quest's own ItemReward
// (AddRewardItems, ItemReward.cs:68), so the complete Necromancer spellbook comes from the quest's
// private InternalReward without copying it, and Jacob's Pickaxe gets its OnAdded.

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Engines.CharacterCreation;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Items;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// -- Skills per guild -------------------------------------------------------------------------

public static class GuildSkillTable
{
    // F-9, "Skills per guild". One line per guild; Chase adjusts here and nowhere else. Every value
    // of SkillName appears exactly once (GuildStarterPathVerification.EverySkillIsInExactlyOneGuild).
    public static readonly IReadOnlyDictionary<string, SkillName[]> Skills =
        new Dictionary<string, SkillName[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["warriors"]   = [SkillName.Swords, SkillName.Macing, SkillName.Fencing, SkillName.Wrestling, SkillName.Tactics, SkillName.Parry, SkillName.Chivalry, SkillName.Focus, SkillName.Throwing],
            ["arcane"]     = [SkillName.Magery, SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist, SkillName.Inscribe, SkillName.Mysticism, SkillName.Spellweaving],
            ["healers"]    = [SkillName.Healing, SkillName.Anatomy, SkillName.Alchemy, SkillName.TasteID],
            ["rangers"]    = [SkillName.Archery, SkillName.Tracking, SkillName.AnimalTaming, SkillName.AnimalLore, SkillName.Herding, SkillName.Veterinary, SkillName.Camping],
            ["mining"]     = [SkillName.Mining],
            ["smithing"]   = [SkillName.Blacksmith, SkillName.ArmsLore],
            ["tinkers"]    = [SkillName.Tinkering],
            ["thieves"]    = [SkillName.Hiding, SkillName.Stealth, SkillName.Stealing, SkillName.Snooping, SkillName.Lockpicking, SkillName.Poisoning, SkillName.DetectHidden, SkillName.RemoveTrap],
            ["dojo"]       = [SkillName.Bushido, SkillName.Ninjitsu],
            ["keepers"]    = [SkillName.Necromancy, SkillName.SpiritSpeak, SkillName.Forensics],
            ["foresters"]  = [SkillName.Lumberjacking, SkillName.Carpentry, SkillName.Fletching],
            ["tailoring"]  = [SkillName.Tailoring],
            ["maritime"]   = [SkillName.Fishing, SkillName.Cartography, SkillName.Cooking],
            ["merchants"]  = [SkillName.ItemID, SkillName.Begging],
            ["bards"]      = [SkillName.Musicianship, SkillName.Peacemaking, SkillName.Provocation, SkillName.Discordance],
            ["artificers"] = [SkillName.Imbuing],
            ["custodians"] = [],
        };

    public static SkillName[] For(string guildKey) =>
        Skills.TryGetValue(guildKey, out var skills) ? skills : [];

    public static string Describe(string guildKey)
    {
        var skills = For(guildKey);
        if (skills.Length == 0)
            return "no skill of its own";

        var names = new string[skills.Length];
        for (var i = 0; i < skills.Length; i++)
        {
            var index = (int)skills[i];
            names[i] = index < SkillInfo.Table.Length ? SkillInfo.Table[index].Name : skills[i].ToString();
        }

        return string.Join(", ", names);
    }
}

// -- The 26 New Haven starter items, by guild ---------------------------------------------------

public static class GuildStarterItems
{
    // F-9 Decision 1. Each entry is the New Haven training quest whose reward the guild hands out
    // (pinned NewHavenSkillTraining.cs). 26 in all, each quest in exactly one guild.
    public static readonly IReadOnlyDictionary<string, Type[]> Quests =
        new Dictionary<string, Type[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["warriors"] = [typeof(CleansingOldHaven), typeof(TheRudimentsOfSelfDefense), typeof(CrushingBonesAndTakingNames), typeof(EnGuarde), typeof(TheArtOfWar), typeof(TheWayOfTheBlade), typeof(ThouAndThineShield), typeof(TheInnerWarrior)],
            ["arcane"]   = [typeof(DefyingTheArcane), typeof(StoppingTheWorld), typeof(ScribingArcaneKnowledge), typeof(TheMagesApprentice), typeof(ScholarlyTask)],
            ["healers"]  = [typeof(KnowThineEnemy), typeof(BruisesBandagesAndBlood)],
            ["rangers"]  = [typeof(SwiftAsAnArrow), typeof(EyesOfARanger)],
            ["mining"]   = [typeof(TheDeluciansLostMine)],
            ["smithing"] = [typeof(ItsHammerTime)],
            ["tinkers"]  = [typeof(TheRightToolForTheJob)],
            ["thieves"]  = [typeof(BecomingOneWithTheShadows), typeof(WalkingSilently)],
            ["dojo"]     = [typeof(TheArtOfStealth), typeof(TheWayOfTheSamurai)],
            ["keepers"]  = [typeof(TheAllureOfDarkMagic), typeof(ChannelingTheSupernatural)],
        };

    public static Type[] For(string guildKey) =>
        Quests.TryGetValue(guildKey, out var quests) ? quests : [];

    public static string GuildOf(Type questType)
    {
        foreach (var (key, quests) in Quests)
            if (Array.IndexOf(quests, questType) >= 0)
                return key;
        return null;
    }

    // Jacob's Pickaxe and the Hammer of Hephaestus are ours and keep their own reissue and recharge
    // rules (F-9): the guild gives the first copy only. Every other item is replaced for scrip.
    public static string OwnRuleFor(Type questType)
    {
        if (questType == typeof(TheDeluciansLostMine))
            return "Lost? The Miners' Compact Liaison issues a new one.";
        if (questType == typeof(ItsHammerTime))
            return "The Smiths' Fellowship recharges it.";
        return null;
    }
}

// -- Where the guildmasters stand ------------------------------------------------------------

public sealed class GuildLocation
{
    public const int TrammelIndex = 1;
    public const int TerMurIndex = 5;

    // How far from its tile a guildmaster may be and still be "there". Seeded ones do not walk;
    // stock spawned ones wander inside their spawner's home range.
    public const int SearchRange = 15;

    public GuildLocation(
        string guildKey, string hall, Type npcType, int mapIndex, int x, int y, int z,
        bool seeded, Direction facing = Direction.South, params Point3D[] oldTiles)
    {
        GuildKey = guildKey;
        Hall = hall;
        NpcType = npcType;
        MapIndex = mapIndex;
        Point = new Point3D(x, y, z);
        Seeded = seeded;
        Facing = facing;
        OldTiles = oldTiles;
    }

    public string GuildKey { get; }
    public string Hall { get; }
    public Type NpcType { get; }
    public int MapIndex { get; }
    public Point3D Point { get; }

    // True: our ClusterFGuildHallSeeder places this one. False: someone else does (the institution
    // seeder, or pinned's post-UOML vendor spawns, PT-01), and the directory only looks for it.
    public bool Seeded { get; }
    public Direction Facing { get; }

    // Where an earlier seeder put this guildmaster. The hall seeder's repair mode moves one found
    // there instead of placing a second (the New Haven Artificers' Guildmaster, in the water).
    public Point3D[] OldTiles { get; }

    public Map Map => MapIndex >= 0 && MapIndex < Map.Maps.Length ? Map.Maps[MapIndex] : null;

    public Mobile Find() => FindNear(Point);

    public Mobile FindNear(Point3D point)
    {
        var map = Map;
        if (map == null || map == Map.Internal)
            return null;

        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var m in map.GetMobilesInRange(point, SearchRange))
        {
            if (m.Deleted || m.GetType() != NpcType)
                continue;

            var d = Math.Max(Math.Abs(m.X - point.X), Math.Abs(m.Y - point.Y));
            if (d < bestDistance)
            {
                best = m;
                bestDistance = d;
            }
        }

        return best;
    }
}

public static class GuildLocations
{
    // In F-9's order. Seeded tiles were picked by reading the client map and checked standable with a
    // port of pinned Map.CanFit (notes/cc-P15, "Placements"). Unseeded ones are where the NPC is
    // placed by someone else; spawned stock guildmasters wander, so their tile is their spawn point.
    public static readonly GuildLocation[] All =
    [
        new("warriors",   "Warrior's Guild Hall",             typeof(WarriorGuildmaster),              GuildLocation.TrammelIndex, 3530, 2537, 20, true),
        new("arcane",     "New Haven Magery School",          typeof(MageGuildmaster),                 GuildLocation.TrammelIndex, 3486, 2494, 52, true),
        new("healers",    "Healer's Hall",                    typeof(HealerGuildmaster),               GuildLocation.TrammelIndex, 3463, 2558, 36, false),
        new("rangers",    "New Haven stables",                typeof(OutridersGuildmaster),            GuildLocation.TrammelIndex, 3524, 2574, 7,  false),
        new("mining",     "Mine camp, south mountains",       typeof(MinersCompactLiaison),            GuildLocation.TrammelIndex, 3498, 2744, 4,  false, Direction.East),
        new("smithing",   "Forge and Anvil",                  typeof(BlacksmithGuildmaster),           GuildLocation.TrammelIndex, 3469, 2536, 41, false),
        new("tinkers",    "Springs N Things",                 typeof(TinkerGuildmaster),               GuildLocation.TrammelIndex, 3458, 2524, 53, false),
        new("thieves",    "Bountiful Harvest Inn, back room", typeof(ThiefGuildmaster),                GuildLocation.TrammelIndex, 3495, 2515, 27, true),
        new("dojo",       "Ninja Dojo",                       typeof(TwinPathsDojoGuildmaster),        GuildLocation.TrammelIndex, 3420, 2517, 21, true),
        new("keepers",    "Necromancers Guild Hall",          typeof(KeepersOfTheLastDoorGuildmaster), GuildLocation.TrammelIndex, 3551, 2463, 15, true),
        new("foresters",  "Carpenters of New Haven",          typeof(ForestersGuildmaster),            GuildLocation.TrammelIndex, 3441, 2637, 28, false),
        new("tailoring",  "A Stitch In Time",                 typeof(TailorGuildmaster),               GuildLocation.TrammelIndex, 3494, 2551, 20, false),
        new("maritime",   "New Haven Docks",                  typeof(FisherGuildmaster),               GuildLocation.TrammelIndex, 3507, 2597, 1,  true),
        new("custodians", "Civic hall, upstairs",             typeof(SanitationWarden),                GuildLocation.TrammelIndex, 3506, 2560, 21, false),
        new("merchants",  "New Haven Bank",                   typeof(MerchantGuildmaster),             GuildLocation.TrammelIndex, 3488, 2567, 20, true),
        new("bards",      "Bardic Guild",                     typeof(BardGuildmaster),                 GuildLocation.TrammelIndex, 3410, 2608, 55, true),
        new("artificers", "New Haven Magery School",          typeof(ArtificersGuildmaster),           GuildLocation.TrammelIndex, 3483, 2501, 52, true, Direction.South, new Point3D(3490, 2627, 0)),
        new("artificers", "Royal City, Ter Mur",              typeof(ArtificersGuildmaster),           GuildLocation.TerMurIndex,  797,  3431, -10, false, Direction.West),
    ];

    public static List<GuildLocation> For(string guildKey)
    {
        var list = new List<GuildLocation>();
        foreach (var loc in All)
            if (loc.GuildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase))
                list.Add(loc);
        return list;
    }
}

// -- Tools, starter items, replacements ---------------------------------------------------------

public enum StarterItemState
{
    Available,           // not had in this loop
    Taken,               // taken from a guild in this loop
    ReceivedFromTrainer  // the matching quest was turned in to a trainer or the board
}

public static class ClusterFGuildStarter
{
    // Guild scrip for a lost starter item, paid in the scrip of the guild that gives it. 10 is what
    // the Apprentice task pays, so one replacement is always within reach of a member who has done
    // it; the generic guilds have no other scrip source yet.
    public const int ReplacementScrip = 10;

    public static GuildStarterRecord GetRecord(PlayerMobile pm, bool create = true)
    {
        if (pm?.Account is not IAccount acct)
            return null;

        return create
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildStarter(pm.Serial)
            : ClusterFAccountPersistence.Get(acct)?.GetGuildStarter(pm.Serial);
    }

    public static bool HasTools(PlayerMobile pm, string guildKey) =>
        GetRecord(pm, false)?.ToolsTaken.Contains(guildKey) == true;

    /// <summary>
    /// Once per character per guild: the character-creation items for each of the guild's skills
    /// (CharacterCreation.GiveSkillItems, so gargoyles get gargoyle items), no item type twice in the
    /// kit, then the guild's own join bonus. Returns every item handed over, or null if this
    /// character already had them.
    /// </summary>
    public static List<Item> GiveTools(PlayerMobile pm, GuildDef def)
    {
        var record = GetRecord(pm);
        if (record == null || record.ToolsTaken.Contains(def.Key))
            return null;

        var handed = new List<Item>();
        var kitTypes = new HashSet<Type>();

        foreach (var skill in GuildSkillTable.For(def.Key))
        {
            // Within one skill the creation list is kept as it is (Cartography's four maps); across
            // skills a type already in the kit is not given twice (Tactics and Swords, one sword).
            var fromThisSkill = new HashSet<Type>();

            foreach (var item in CharacterCreation.GiveSkillItems(pm, skill))
            {
                if (item.Deleted)
                    continue;

                var type = item.GetType();
                if (kitTypes.Contains(type))
                {
                    item.Delete();
                    continue;
                }

                fromThisSkill.Add(type);
                handed.Add(item);
            }

            kitTypes.UnionWith(fromThisSkill);
        }

        ClusterFGuildSystem.GiveJoinBonus(pm, def);
        record.ToolsTaken.Add(def.Key);

        pm.SendMessage(0x44, handed.Count > 0
            ? $"The {def.Name} hands you the tools of its trade: {handed.Count} item{(handed.Count == 1 ? "" : "s")}."
            : $"The {def.Name} has no trade tools to hand you, but you are welcome all the same.");
        return handed;
    }

    public static MLQuest QuestFor(Type questType) => MLQuestSystem.FindQuest(questType);

    public static StarterItemState GetState(PlayerMobile pm, Type questType)
    {
        if (GetRecord(pm, false)?.ItemsTaken.Contains(questType.Name) == true)
            return StarterItemState.Taken;

        var quest = QuestFor(questType);
        if (quest != null && MLQuestSystem.GetContext(pm)?.HasDoneQuest(quest) == true)
            return StarterItemState.ReceivedFromTrainer;

        return StarterItemState.Available;
    }

    // The quest's own reward items, made the way ClaimRewards makes them (MLQuestEntry.cs:361-393):
    // every BaseReward's AddRewardItems, all into the pack or none.
    private static bool GiveRewardItems(PlayerMobile pm, MLQuest quest, out List<Item> made)
    {
        made = new List<Item>();
        foreach (var reward in quest.Rewards)
            reward.AddRewardItems(pm, made);

        if (made.Count == 0)
            return false;

        foreach (var item in made)
        {
            if (!pm.AddToBackpack(item))
            {
                foreach (var undo in made)
                    undo.Delete();

                made.Clear();
                pm.SendLocalizedMessage(1078524); // Your backpack is full. You cannot complete the quest and receive your reward.
                return false;
            }
        }

        foreach (var item in made)
            pm.SendLocalizedMessage(1074360, item.Name ?? $"#{item.LabelNumber}"); // You receive a reward: ~1_REWARD~

        return true;
    }

    private static bool CanUseGuild(PlayerMobile pm, GuildDef def, Type questType, out string message)
    {
        message = null;
        if (pm?.Account is not IAccount acct)
        {
            message = "Could not resolve your account.";
            return false;
        }

        if (!ClusterFGuildSystem.IsJoined(pm, def.Key))
        {
            message = $"Join the {def.Name} first. Joining is free.";
            return false;
        }

        if (Array.IndexOf(GuildStarterItems.For(def.Key), questType) < 0 || QuestFor(questType) == null)
        {
            message = "That is not one of this guild's starter items.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Hands the starter item once in this character's loop and marks its quest done, so the quest
    /// then pays nothing. A copy of the quest the character is carrying is closed first.
    /// </summary>
    public static bool TakeStarterItem(PlayerMobile pm, GuildDef def, Type questType, out string message)
    {
        if (!CanUseGuild(pm, def, questType, out message))
            return false;

        switch (GetState(pm, questType))
        {
            case StarterItemState.Taken:
                message = "You have already taken that one in this life.";
                return false;
            case StarterItemState.ReceivedFromTrainer:
                message = "You already received that from the trainer.";
                return false;
        }

        var quest = QuestFor(questType);
        if (!GiveRewardItems(pm, quest, out _))
        {
            message = "Make room in your backpack first.";
            return false;
        }

        var context = MLQuestSystem.GetOrCreateContext(pm);
        var held = context.FindInstance(quest);
        if (held != null)
        {
            held.Cancel();
            pm.SendMessage(0x3B2, "The guild has given you that quest's reward, so the quest is closed.");
        }

        context.SetDoneQuest(quest);
        GetRecord(pm).ItemsTaken.Add(questType.Name);

        message = "Taken. The matching training quest now counts as done.";
        return true;
    }

    public static bool OwnsCopy(PlayerMobile pm, Type itemType)
    {
        foreach (var worn in pm.Items)
            if (itemType.IsInstanceOfType(worn))
                return true;

        return pm.Backpack?.FindItemByType(itemType) != null ||
               pm.FindBankNoCreate()?.FindItemByType(itemType) != null;
    }

    /// <summary>
    /// A lost starter item again, within the same loop, for ReplacementScrip of the guild's scrip.
    /// Not for Jacob's Pickaxe or the Hammer of Hephaestus, which keep their own rules.
    /// </summary>
    public static bool ReplaceStarterItem(PlayerMobile pm, GuildDef def, Type questType, out string message)
    {
        if (!CanUseGuild(pm, def, questType, out message))
            return false;

        if (GetState(pm, questType) == StarterItemState.Available)
        {
            message = "You have not had that one yet. Take it instead.";
            return false;
        }

        var ownRule = GuildStarterItems.OwnRuleFor(questType);
        if (ownRule != null)
        {
            message = ownRule;
            return false;
        }

        var guild = ClusterFAccountPersistence.GetOrCreate((IAccount)pm.Account).GetOrCreateGuildData(pm.Serial);
        if (guild.GetCurrency(def.Key) < ReplacementScrip)
        {
            message = $"A replacement costs {ReplacementScrip} {def.Name} scrip. You have {guild.GetCurrency(def.Key)}.";
            return false;
        }

        // Made first so its type can be checked; a player who still has one gets nothing and pays nothing.
        var quest = QuestFor(questType);
        var probe = new List<Item>();
        foreach (var reward in quest.Rewards)
            reward.AddRewardItems(pm, probe);

        var stillOwned = false;
        foreach (var item in probe)
        {
            stillOwned |= OwnsCopy(pm, item.GetType());
            item.Delete();
        }

        if (stillOwned)
        {
            message = "You still have one. Only a lost item is replaced.";
            return false;
        }

        if (!GiveRewardItems(pm, quest, out _))
        {
            message = "Make room in your backpack first.";
            return false;
        }

        guild.SpendCurrency(def.Key, ReplacementScrip);
        message = $"Replaced for {ReplacementScrip} scrip.";
        return true;
    }

    // -- The trainers' line (F-9 Decision 2.4) --

    // Each New Haven skill trainer and the hall it stands in (ClusterFNewHavenSeeder's areas), as the
    // guild whose hall that is. Sarsmea Smythe stands in the bank, the Merchants' Exchange's hall.
    private static readonly Dictionary<Type, (string GuildKey, string Hall)> TrainerHalls = new()
    {
        [typeof(Aelorn)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Dimethro)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Churchill)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Robyn)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Recaro)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(AldenArmstrong)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Jockles)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(TylAriadne)] = ("warriors", "Warrior's Guild Hall"),
        [typeof(Alefian)] = ("arcane", "Magery School"),
        [typeof(Gustar)] = ("arcane", "Magery School"),
        [typeof(Jillian)] = ("arcane", "Magery School"),
        [typeof(Kaelynna)] = ("arcane", "Magery School"),
        [typeof(Mithneral)] = ("arcane", "Magery School"),
        [typeof(AmeliaYoungstone)] = ("tinkers", "Springs N Things"),
        [typeof(AndreasVesalius)] = ("healers", "Healer's Hall"),
        [typeof(Avicenna)] = ("healers", "Healer's Hall"),
        [typeof(SarsmeaSmythe)] = ("merchants", "New Haven Bank"),
        [typeof(Ryuichi)] = ("dojo", "Ninja Dojo"),
        [typeof(Chiyo)] = ("dojo", "Ninja Dojo"),
        [typeof(Jun)] = ("dojo", "Ninja Dojo"),
        [typeof(Walker)] = ("dojo", "Ninja Dojo"),
        [typeof(Hamato)] = ("dojo", "Hamato Dojo"),
        [typeof(Mulcivikh)] = ("keepers", "Necromancers Guild Hall"),
        [typeof(Morganna)] = ("keepers", "Necromancers Guild Hall"),
        [typeof(JacobWaltz)] = ("mining", "mine camp"),
        [typeof(GeorgeHephaestus)] = ("smithing", "Forge and Anvil"),
    };

    public static IReadOnlyDictionary<Type, (string GuildKey, string Hall)> TrainerHallTable => TrainerHalls;

    /// <summary>
    /// The one line a trainer adds: the guild whose hall it stands in, and, where it differs, the
    /// guild that also hands out this trainer's reward.
    /// </summary>
    public static string TrainerLine(BaseCreature trainer)
    {
        if (!TrainerHalls.TryGetValue(trainer.GetType(), out var hall))
            return null;

        var hallGuild = ClusterFGuildSystem.GetDef(hall.GuildKey);
        if (hallGuild == null)
            return null;

        string itemGuildKey = null;
        foreach (var quest in trainer.MLQuests)
        {
            itemGuildKey = GuildStarterItems.GuildOf(quest.GetType());
            if (itemGuildKey != null)
                break;
        }

        var itemGuild = itemGuildKey != null ? ClusterFGuildSystem.GetDef(itemGuildKey) : null;

        if (itemGuild == null || itemGuild == hallGuild)
            return $"This {hall.Hall} belongs to the {hallGuild.Name}. Its guildmaster can give you what I give.";

        return $"This {hall.Hall} belongs to the {hallGuild.Name}. What I give, the {itemGuild.Name} gives too.";
    }

    public static void TellTrainerLine(BaseCreature trainer, Mobile from)
    {
        if (from is PlayerMobile pm && TrainerLine(trainer) is { } line)
            MLQuestSystem.Tell(trainer, pm, line);
    }

    // -- Show me the way --

    /// <summary>
    /// Points the player at a guildmaster with a quest arrow and says what it did. A guildmaster that
    /// is not where the directory says is reported, never silently skipped.
    /// </summary>
    public static string ShowTheWay(PlayerMobile pm, GuildLocation loc)
    {
        var def = ClusterFGuildSystem.GetDef(loc.GuildKey);
        var name = def?.Name ?? loc.GuildKey;
        var map = loc.Map;

        if (map == null)
            return $"The {name}'s post at {loc.Hall} is on a facet this shard does not have loaded.";

        var npc = loc.Find();
        if (npc == null)
            return $"The {name}'s guildmaster is not at {loc.Hall} ({loc.Point.X}, {loc.Point.Y}, {map.Name}). Please tell a Game Master.";

        if (pm.Map != map)
            return $"The {name}'s guildmaster is at {loc.Hall}, on {map.Name}. The arrow cannot cross facets.";

        pm.QuestArrow = new GuildDirectionArrow(pm, npc, npc.Location, map, $"the {name}'s guildmaster");
        return $"Follow the arrow to the {name}'s guildmaster at {loc.Hall}. Right-click the arrow to put it away.";
    }
}

// -- The arrow ---------------------------------------------------------------------------------

/// <summary>
/// A quest arrow to a place, with a mobile as its packet target (QuestArrow needs one). It follows a
/// target that moves, puts itself away on arrival or after ten minutes, and right-click dismisses it.
/// Generalizes MinersCompactDirectionArrow, which never showed: it was built with the two-argument
/// QuestArrow constructor, which sends nothing (QuestArrow.cs:8-13), and was never assigned to the
/// player's QuestArrow.
/// </summary>
public class GuildDirectionArrow : QuestArrow
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const int ArrivedRange = 3;

    private readonly Map _map;
    private readonly string _label;
    private readonly DateTime _expires;
    private Point3D _point;
    private TimerExecutionToken _timer;

    public GuildDirectionArrow(PlayerMobile pm, Mobile target, Point3D point, Map map, string label)
        : base(pm, target, point.X, point.Y)
    {
        _point = point;
        _map = map;
        _label = label;
        _expires = Core.Now + Lifetime;
        Timer.StartTimer(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), Tick, out _timer);
    }

    public Point3D Point => _point;

    private void Tick()
    {
        if (!Running)
        {
            _timer.Cancel();
            return;
        }

        if (Mobile.Deleted || Core.Now >= _expires)
        {
            Stop(_point.X, _point.Y);
            return;
        }

        if (Mobile.Map == _map && Mobile.InRange(_point, ArrivedRange))
        {
            Mobile.SendMessage(0x44, $"You have found {_label}.");
            Stop(_point.X, _point.Y);
            return;
        }

        // A spawned guildmaster wanders; the arrow follows it.
        if (Target != Mobile && !Target.Deleted && Target.Map == _map && Target.Location != _point)
        {
            _point = Target.Location;
            Update(_point.X, _point.Y);
        }
    }

    public override void OnStop() => _timer.Cancel();

    public override void OnClick(bool rightClick)
    {
        if (rightClick)
            Stop(_point.X, _point.Y);
    }
}

// -- First login --------------------------------------------------------------------------------

public static class GuildWelcome
{
    // The board each starting town has. All characters start in New Haven today
    // (CharacterCreation.ConstructAvailableStartingCities); F-10 adds a row here, not code.
    public static (string Town, int MapIndex, Point3D Board) StartingBoardFor(PlayerMobile pm) =>
        ("New Haven", GuildLocation.TrammelIndex, NewHavenQuestBoard.HomeLocation);

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        var record = ClusterFGuildStarter.GetRecord(pm);
        if (record == null || record.WelcomeShown)
            return;

        record.WelcomeShown = true;
        pm.SendGump(new GuildWelcomeGump(pm));
    }

    public static string PointToBoard(PlayerMobile pm)
    {
        var (town, mapIndex, spot) = StartingBoardFor(pm);
        var map = Map.Maps[mapIndex];

        if (pm.Map != map)
            return $"The Guild Board stands in {town}'s square, on {map?.Name}.";

        // QuestArrow targets a mobile; the arrow points at the board's tile and names the player as
        // its packet target, which the client only uses as an id.
        pm.QuestArrow = new GuildDirectionArrow(pm, pm, spot, map, "the Guild Board");
        return $"Follow the arrow to the Guild Board in {town}'s square.";
    }
}

public class GuildWelcomeGump : Gump
{
    private const int W = 420;
    private const int H = 230;
    private const int BtnArrow = 1;

    private readonly PlayerMobile _pm;

    public GuildWelcomeGump(PlayerMobile pm) : base(120, 100)
    {
        _pm = pm;
        var (town, _, _) = GuildWelcome.StartingBoardFor(pm);

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);
        AddLabel(20, 16, 1154, $"Welcome to {town}");
        AddImageTiled(10, 38, W - 20, 2, 9304);

        AddHtml(20, 48, W - 40, 110,
            "<BASEFONT COLOR=#DDDDDD>Every skill has a guild in " + town + ". Joining one is free, " +
            "and it hands you the tools of its trade and the starter items the trainers give.<BR><BR>" +
            "The Guild Board in the town square lists every guild, what it teaches, and where its " +
            "guildmaster stands.</BASEFONT>", false, false);

        AddImageTiled(10, H - 62, W - 20, 2, 9304);
        AddButton(20, H - 48, 4005, 4007, BtnArrow);
        AddLabel(55, H - 46, 1154, "Show me the Guild Board");
        AddButton(W - 110, H - 48, 4017, 4019, 0);
        AddLabel(W - 75, H - 46, 999, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnArrow)
            _pm.SendMessage(0x44, GuildWelcome.PointToBoard(_pm));
    }
}

// -- Starter items page -------------------------------------------------------------------------

/// <summary>
/// At the guildmaster: that guild's New Haven starter items, one click to take one, once per loop.
/// Every button is judged against the world at the click.
/// </summary>
public class GuildStarterItemsGump : Gump
{
    private const int W = 560;
    private const int RowH = 26;
    private const int Top = 96;

    private const int BtnBack = 1;
    private const int TakeBase = 100;
    private const int ReplaceBase = 200;

    private readonly PlayerMobile _pm;
    private readonly GuildDef _def;
    private readonly Mobile _guildmaster;

    public GuildStarterItemsGump(PlayerMobile pm, GuildDef def, Mobile guildmaster, string notice = null) : base(80, 80)
    {
        _pm = pm;
        _def = def;
        _guildmaster = guildmaster;

        var quests = GuildStarterItems.For(def.Key);
        var h = Top + Math.Max(quests.Length, 1) * RowH + 70;

        AddBackground(0, 0, W, h, 9270);
        AddAlphaRegion(10, 10, W - 20, h - 20);
        AddLabel(20, 16, 1154, $"{def.Name}: starter items");
        AddLabel(20, 38, 999, "Free, one of each, once in this life. Taking one closes its training quest.");
        AddLabel(20, 58, 999, $"Lost one? A replacement costs {ClusterFGuildStarter.ReplacementScrip} guild scrip.");
        if (notice != null)
            AddLabel(20, 76, 68, notice);

        var y = Top;
        if (quests.Length == 0)
        {
            AddLabel(20, y, 999, "This guild hands out no New Haven starter items.");
            y += RowH;
        }

        for (var i = 0; i < quests.Length; i++)
        {
            var quest = ClusterFGuildStarter.QuestFor(quests[i]);
            if (quest == null || quest.Rewards.Count == 0)
                continue;

            quest.Rewards[0].Name.AddHtmlText(this, 20, y + 2, 250, 20, numberColor: 0x7FFF, stringColor: 0xFFFFFF);

            switch (ClusterFGuildStarter.GetState(pm, quests[i]))
            {
                case StarterItemState.Available:
                    AddButton(290, y, 4005, 4007, TakeBase + i);
                    AddLabel(325, y + 2, 68, "Take it");
                    break;
                default:
                {
                    var how = ClusterFGuildStarter.GetState(pm, quests[i]) == StarterItemState.Taken
                        ? "Taken"
                        : "Received from the trainer";
                    var rule = GuildStarterItems.OwnRuleFor(quests[i]);

                    if (rule != null)
                    {
                        AddHtml(290, y + 2, W - 310, 20, $"<BASEFONT COLOR=#AAAAAA>{how}. {rule}</BASEFONT>", false, false);
                    }
                    else
                    {
                        AddLabel(290, y + 2, 999, how);
                        AddButton(440, y, 4005, 4007, ReplaceBase + i);
                        AddLabel(475, y + 2, 999, "Replace");
                    }

                    break;
                }
            }

            y += RowH;
        }

        AddImageTiled(10, y + 8, W - 20, 2, 9304);
        AddButton(20, y + 20, 4014, 4016, BtnBack);
        AddLabel(55, y + 22, 999, "Back");
        AddButton(W - 110, y + 20, 4017, 4019, 0);
        AddLabel(W - 75, y + 22, 999, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm || info.ButtonID == 0)
            return;

        if (info.ButtonID == BtnBack)
        {
            ClusterFGuildSystem.OpenGuildHall(_pm, _def, _guildmaster);
            return;
        }

        if (!ClusterFGuildSystem.IsNearGuildmaster(_pm, _guildmaster))
            return;

        var quests = GuildStarterItems.For(_def.Key);
        string message;

        if (info.ButtonID >= TakeBase && info.ButtonID < TakeBase + quests.Length)
            ClusterFGuildStarter.TakeStarterItem(_pm, _def, quests[info.ButtonID - TakeBase], out message);
        else if (info.ButtonID >= ReplaceBase && info.ButtonID < ReplaceBase + quests.Length)
            ClusterFGuildStarter.ReplaceStarterItem(_pm, _def, quests[info.ButtonID - ReplaceBase], out message);
        else
            return;

        _pm.SendGump(new GuildStarterItemsGump(_pm, _def, _guildmaster, message));
    }
}
