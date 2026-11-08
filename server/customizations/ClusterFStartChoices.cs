// cc-P714: the shorter start (P709's creation proposal, Chase's decisions of 2026-10-27; shard-migration
// notes/cc-P714-gated-skills.md). While ClusterFSchoolGate is in force, a new player character starts from one of four
// 120-point templates (four skills at 30) or from a shorter custom list:
//
//   Fighter (profession 1, the client's Warrior): Swordsmanship, Tactics, Anatomy, Healing; 45/35/10.
//   Archer (profession 8, a new client entry; for a gargoyle it is Thrower, Throwing for Archery): Archery, Tactics,
//     Anatomy, Healing; 45/35/10.
//   Mage (profession 2): Magery, Evaluating Intelligence, Meditation, Wrestling; 25/20/45.
//   (Maker, profession 3, the client's Blacksmith: Mining, Blacksmithy, Tinkering, Tailoring; 60/15/15. cc-P817, Chase
//   2026-11-07: "I also want to remove the trade choice": no longer offered; the crafts are learned in the world, and
//   the custom list below still has them. The player package's Prof.txt copy drops it from the screen.)
//   Choose my own skills (profession 0): only the skills in CustomSkills, at most 50 in one, no repeats, totalling 120
//     on a four-slot client (stats 90) or 100 on a legacy three-slot client (stats 80), each stat 10-60; Archery is not
//     a gargoyle's and Throwing is only a gargoyle's.
//
// Paladin (5), Necromancer (4), Samurai (6) and Ninja (7) are not offered: their schools are learned in the world. A
// request the rules refuse (an old profession, a skill off the list, a wrong total, a repeat, a race's wrong weapon) makes
// no character, so no slot is used and nothing is half made; the client is told "Character creation blocked." and the
// log says why. The stock client still lists the old professions until the client package ships its own profession
// file (the follow-up in the notes). Skipping the tutorial or finishing it grants neither gated school.

using System;
using System.Collections.Generic;


namespace Server;

public sealed record StartChoice(string Name, (SkillName Skill, byte Value)[] Skills, byte[] Stats, string Kit, bool Template);

public static class ClusterFStartChoices
{
    public const int FighterId = 1;
    public const int MageId = 2;
    public const int MakerId = 3;
    public const int NecromancerId = 4;
    public const int PaladinId = 5;
    public const int SamuraiId = 6;
    public const int NinjaId = 7;
    public const int ArcherId = 8;

    public const string CreationText =
        "These choices give you starting skills and supplies. They do not choose a permanent class. You can learn other skills in the world. " +
        "Chivalry and Necromancy each begin with a teacher's quest.";

    public static bool Enabled => ClusterFSchoolGate.Enabled;

    public static readonly HashSet<int> RemovedProfessions = [NecromancerId, PaladinId, SamuraiId, NinjaId, MakerId]; // cc-P817: the Maker

    public static readonly HashSet<SkillName> CustomSkills =
    [
        SkillName.Swords, SkillName.Macing, SkillName.Fencing, SkillName.Archery, SkillName.Throwing, SkillName.Wrestling,
        SkillName.Tactics, SkillName.Anatomy, SkillName.Parry, SkillName.Healing,
        SkillName.Magery, SkillName.EvalInt, SkillName.Meditation,
        SkillName.Mining, SkillName.Lumberjacking, SkillName.Fishing, SkillName.Blacksmith, SkillName.Tailoring,
        SkillName.Tinkering, SkillName.Carpentry,
        SkillName.Hiding, SkillName.Tracking
    ];

    public static StartChoice Fighter =>
        new("Fighter", [(SkillName.Swords, 30), (SkillName.Tactics, 30), (SkillName.Anatomy, 30), (SkillName.Healing, 30)], [45, 35, 10], "warrior", true);

    public static StartChoice Archer =>
        new("Archer", [(SkillName.Archery, 30), (SkillName.Tactics, 30), (SkillName.Anatomy, 30), (SkillName.Healing, 30)], [45, 35, 10], "archer", true);

    public static StartChoice Thrower =>
        new("Thrower", [(SkillName.Throwing, 30), (SkillName.Tactics, 30), (SkillName.Anatomy, 30), (SkillName.Healing, 30)], [45, 35, 10], "thrower", true);

    public static StartChoice Mage =>
        new("Mage", [(SkillName.Magery, 30), (SkillName.EvalInt, 30), (SkillName.Meditation, 30), (SkillName.Wrestling, 30)], [25, 20, 45], "mage", true);

    /// <summary>The templates a race is offered, by profession id.</summary>
    public static StartChoice Template(int profession, Race race) => profession switch
    {
        FighterId => Fighter,
        ArcherId => race == Race.Gargoyle ? Thrower : Archer,
        MageId => Mage,
        _ => null
    };

    /// <summary>
    /// The start a request resolves to, or null with the reason it is refused. newCreation: the client sent four skill slots
    /// (120 points, 90 stats); a legacy client sends three (100 points, 80 stats).
    /// </summary>
    public static StartChoice Resolve(int profession, (SkillName, byte)[] skills, byte[] stats, Race race, bool newCreation, out string why)
    {
        why = null;
        if (RemovedProfessions.Contains(profession))
        {
            why = profession == MakerId
                ? $"profession {profession} (the Maker, the client's Blacksmith) is no longer offered at creation (the crafts are learned in the world)"
                : $"profession {profession} is no longer offered at creation (Paladin, Necromancer, Samurai and Ninja are learned in the world)";
            return null;
        }

        if (profession != 0)
        {
            var template = Template(profession, race);
            if (template == null)
            {
                why = $"profession {profession} is not one of the start templates";
            }

            return template;
        }

        var budget = newCreation ? 120 : 100;
        var statBudget = newCreation ? 90 : 80;
        var total = 0;
        var seen = new HashSet<SkillName>();
        var chosen = new List<(SkillName, byte)>();
        foreach (var (name, value) in skills ?? [])
        {
            if (value == 0)
            {
                continue;
            }

            if (!CustomSkills.Contains(name))
            {
                why = $"{name} is not on the starting list";
                return null;
            }

            if (value > 50)
            {
                why = $"{name} at {value} is over 50";
                return null;
            }

            if (!seen.Add(name))
            {
                why = $"{name} is chosen twice";
                return null;
            }

            if (name == SkillName.Archery && race == Race.Gargoyle || name == SkillName.Throwing && race != Race.Gargoyle)
            {
                why = $"{name} is not a {race?.Name ?? "character"}'s weapon skill";
                return null;
            }

            total += value;
            chosen.Add((name, value));
        }

        if (total != budget)
        {
            why = $"the skills total {total}, not {budget}";
            return null;
        }

        if (stats is not { Length: 3 } || Array.Exists(stats, s => s is < 10 or > 60) || stats[0] + stats[1] + stats[2] != statBudget)
        {
            why = $"the stats are not three values of 10-60 totalling {statBudget}";
            return null;
        }

        return new StartChoice("Custom", chosen.ToArray(), [stats[0], stats[1], stats[2]], "custom", false);
    }
}
