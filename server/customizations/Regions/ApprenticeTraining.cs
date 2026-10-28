// cc-P189 (Chase, 2026-10-19 14:54: "we should have the young player skill boost for old haven area"): OSI's New Haven
// apprentice training areas, ported from ServUO. Notes: shard-migration notes/cc-P189-cont-old-haven-new-player-safe.md.
//
// What ServUO does (the port's source; pinned's Regions/ApprenticeRegion.cs is an empty stub, "TODO: Implement"):
//   - A New Haven trainer's quest carries an ApprenticeObjective naming a skill, 50.0, and one region
//     (ServUO Mobiles/NPCs/*.cs, e.g. Alefian.cs:88, Magic Resist in "Old Haven Training"; 26 trainers, table below).
//   - The gain: SkillCheck.Gain multiplies the gain by Utility.RandomMinMax(2, 4) when QuestHelper.EnhancedSkill is true
//     (ServUO Scripts/Misc/SkillCheck.cs:404-407), that is when the player holds an incomplete apprentice objective for
//     that skill and stands in (or in a child of) its region (Services/MondainsLegacyQuests/Helpers/QuestHelper.cs:802-827).
//   - Who: whoever holds the quest. Nothing checks Young. The trainers offer the quest only below 50 and it completes at
//     50 (QuestObjectives.cs:785-805), so the boost runs from 0 to 50 and stops there, whatever the cap.
//   - Entering or leaving the region with the quest sends the trainer's own enter or leave line and adds or removes the
//     "Accelerated Skillgain" buff (ApprenticeRegion.cs:24-95, BuffIcon.ArcaneEmpowerment, 1078511, 1078512). Accepting
//     the quest inside the region does the same at once (QuestObjectives.cs:807-818), and completing it removes the buff
//     (QuestHelper.cs:19-33, called from QuestObjectives.cs:820-823).
//   - It never stacks with a Scroll of Alacrity: the scroll refuses while any apprentice quest is held
//     (Items/Consumables/ScrollofAlacrity.cs:71-84), and the scroll's own branch would replace the amount anyway
//     (SkillCheck.cs:410-418).
//
// How it is built here: pinned's New Haven quests are its own ML quest system (Engines/ML Quests/Definitions/
// NewHavenSkillTraining.cs), each a GainSkillObjective(skill, 500, useReal: true, accelerate: true). The region of each is
// ServUO's, matched by skill (one trainer per skill on both sides, so the 26 pair up one to one). The gain hook is
// ClusterFSkillGain.ObserveAttempt, which every player gain passes through (the four pinned handlers and the shard's extra
// attempts): after a gain, OnGain adds (n - 1) times that gain with n = RandomMinMax(2, 4), which is ServUO's toGain *= n.
//
// Deviations (listed in the notes for the register; agents do not edit docs):
//   1. Pinned's own acceleration stays: accepting one of these quests gives pinned's 15-minute x2-5 on the skill
//      everywhere (GainSkillObjective.cs:122-133, SkillCheck.cs:273-276). While that runs this boost does nothing, so the
//      two never stack, as on ServUO. Pinned's alacrity scroll is not made to refuse a quest holder.
//   2. The extra amount is added after pinned's gain, with pinned's own rules for it (a down-locked skill may drop by it,
//      SkillCheck.cs:259-271; nothing is added once the total is at the cap, :278-281), instead of inside one Gain call.
//   3. The buff and the enter line come at the first boosted gain when the quest was accepted inside the region (pinned
//      has no accept hook to reach), and the buff goes at the gain that completes the quest; ServUO does both at once.
//   4. The buff names the skill by its display name ("Resisting Spells"), not ServUO's enum name ("MagicResist").
//   5. On SoS (cc-P678) Old Haven is the author's Haven town, which is New Haven now, so Old Haven Training is the north
//      woods between the town and the shore (x 3590-3710, y 2378-2459), where Old Haven's undead field was moved
//      (ClusterFHavenTownSpawns.TrainingField). It was ServUO's rectangle plus the walk to the Rootdeep hole (cc-P189).
// Only Old Haven Training is registered. ServUO's other Haven apprentice regions (Haven Mountains, Springs And Things
// Workshop, Haven Dojo, Gorge's Shop, Haven Library, Enhanced Tracking Skill) are in the table but not registered: Chase
// decides (notes). A quest whose region is not registered gets no boost, as ServUO logs an invalid region and gives none.
//
// Drelgor (Mobiles/Named/Drelgor.cs) speaks to players in "Old Haven Training"; with this region he does (cc-P129's
// recorded deviation reverses).

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.BuffIcons;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Objectives;
using Server.Mobiles;

namespace Server.Regions;

public static class ApprenticeTraining
{
    public const string OldHavenTraining = "Old Haven Training";

    /// <summary>One New Haven trainer's quest: the region it trains in and the trainer's enter and leave lines.</summary>
    public sealed record Area(Type Quest, SkillName Skill, string Region, int Enter, int Leave);

    /// <summary>ServUO's 26 apprentice objectives (Mobiles/NPCs/&lt;trainer&gt;.cs), on pinned's quest of the same skill.</summary>
    public static readonly Area[] Areas =
    [
        new(typeof(CleansingOldHaven), SkillName.Chivalry, OldHavenTraining, 1077720, 1077721),           // Aelorn.cs:73
        new(typeof(TheArtOfWar), SkillName.Tactics, OldHavenTraining, 1077668, 1077669),                  // AldenArmstrong.cs:80
        new(typeof(DefyingTheArcane), SkillName.MagicResist, OldHavenTraining, 1077494, 1077588),         // Alefian.cs:88
        new(typeof(KnowThineEnemy), SkillName.Anatomy, OldHavenTraining, 1077686, 1077687),               // AndreasVesalius.cs:79
        new(typeof(BruisesBandagesAndBlood), SkillName.Healing, OldHavenTraining, 1077677, 1077678),      // Avicenna.cs:81
        new(typeof(CrushingBonesAndTakingNames), SkillName.Macing, OldHavenTraining, 1078063, 1078064),   // Churchill.cs:77
        new(typeof(TheRudimentsOfSelfDefense), SkillName.Wrestling, OldHavenTraining, 1077492, 1077586),  // Dimethro.cs:75
        new(typeof(StoppingTheWorld), SkillName.Meditation, OldHavenTraining, 1077490, 1077584),          // Gustar.cs:80
        new(typeof(TheWayOfTheSamurai), SkillName.Bushido, OldHavenTraining, 1078008, 1078009),           // Hamato.cs:80
        new(typeof(TheWayOfTheBlade), SkillName.Swords, OldHavenTraining, 1077659, 1077660),              // Jockles.cs:81
        new(typeof(WalkingSilently), SkillName.Stealth, OldHavenTraining, 1078176, 1078177),              // Jun.cs:75
        new(typeof(TheMagesApprentice), SkillName.Magery, OldHavenTraining, 1077489, 1077583),            // Kaelynna.cs:79
        new(typeof(ScholarlyTask), SkillName.EvalInt, OldHavenTraining, 1077491, 1077585),                // Mithneral.cs:75
        new(typeof(ChannelingTheSupernatural), SkillName.SpiritSpeak, OldHavenTraining, 1078045, 1078046), // Morganna.cs:75
        new(typeof(TheAllureOfDarkMagic), SkillName.Necromancy, OldHavenTraining, 1078037, 1078038),      // Mulcivikh.cs:73
        new(typeof(EnGuarde), SkillName.Fencing, OldHavenTraining, 1078188, 1078189),                     // Recaro.cs:77
        new(typeof(SwiftAsAnArrow), SkillName.Archery, OldHavenTraining, 1078203, 1078204),               // Robyn.cs:76
        new(typeof(TheArtOfStealth), SkillName.Ninjitsu, OldHavenTraining, 1078156, 1078157),             // Ryuichi.cs:75
        new(typeof(TheInnerWarrior), SkillName.Focus, OldHavenTraining, 1077697, 1077698),                // SarsmeaSmythe.cs:80
        new(typeof(ThouAndThineShield), SkillName.Parry, OldHavenTraining, 1077705, 1077706),             // TylAriadne.cs:78
        new(typeof(TheRightToolForTheJob), SkillName.Tinkering, "Springs And Things Workshop", 1077742, 1077743), // Amelia.cs:83
        new(typeof(BecomingOneWithTheShadows), SkillName.Hiding, "Haven Dojo", 1078166, 1078167),         // Chiyo.cs:73
        new(typeof(ItsHammerTime), SkillName.Blacksmith, "Gorge's Shop", 1077733, 1077734),               // GeorgeHephaestus.cs:77
        new(typeof(TheDeluciansLostMine), SkillName.Mining, "Haven Mountains", 1077751, 1077752),         // Jacob.cs:84
        new(typeof(ScribingArcaneKnowledge), SkillName.Inscribe, "Haven Library", 1077493, 1077587),      // Jillian.cs:86
        new(typeof(EyesOfARanger), SkillName.Tracking, "Enhanced Tracking Skill", 1078215, 1078216)       // Walker.cs:75
    ];

    private static readonly Dictionary<Type, Area> _byQuest = BuildIndex();

    private static Dictionary<Type, Area> BuildIndex()
    {
        var index = new Dictionary<Type, Area>();
        foreach (var area in Areas)
        {
            index[area.Quest] = area;
        }

        return index;
    }

    /// <summary>The players showing the buff, so leaving removes only a buff this added.</summary>
    private static readonly HashSet<PlayerMobile> _buffed = [];

    /// <summary>
    /// The apprentice areas <paramref name="pm"/> trains in: an incomplete quest of the table, still running, for
    /// <paramref name="skill"/> (or any skill when null). <paramref name="gained"/> is taken off the skill first, so a gain
    /// that reaches 50 is judged as ServUO judges it, before the objective completes.
    /// </summary>
    public static IEnumerable<Area> Training(PlayerMobile pm, SkillName? skill = null, int gained = 0)
    {
        var context = MLQuestSystem.GetContext(pm);
        if (context == null)
        {
            yield break;
        }

        foreach (var instance in context.QuestInstances)
        {
            if (instance.Removed || instance.ClaimReward || !_byQuest.TryGetValue(instance.Quest.GetType(), out var area))
            {
                continue;
            }

            if (skill != null && area.Skill != skill)
            {
                continue;
            }

            foreach (var objective in instance.Objectives)
            {
                if (objective is GainSkillObjectiveInstance gso && gso.Handles(area.Skill))
                {
                    var s = pm.Skills[area.Skill];
                    var value = (gso.Objective.UseReal ? s.Fixed : s.BaseFixedPoint) - gained;

                    if (value < gso.Objective.ThresholdFixed)
                    {
                        yield return area;
                    }

                    break;
                }
            }
        }
    }

    /// <summary>The registered apprentice region of that name <paramref name="pm"/> stands in, or null.</summary>
    public static ApprenticeRegion? InRegion(PlayerMobile pm, string name) =>
        pm.Region?.GetRegion(name) as ApprenticeRegion;

    /// <summary>
    /// After a gain on <paramref name="skill"/> from <paramref name="before"/> (fixed point): ServUO's x2-4 when the player
    /// trains it here. Returns the amount added (fixed point).
    /// </summary>
    public static int OnGain(Mobile from, Skill skill, int before)
    {
        if (from is not PlayerMobile pm || skill == null)
        {
            return 0;
        }

        var gained = skill.BaseFixedPoint - before;
        if (gained <= 0)
        {
            return 0;
        }

        Area? training = null;
        foreach (var area in Training(pm, skill.SkillName, gained))
        {
            if (InRegion(pm, area.Region) != null)
            {
                training = area;
                break;
            }
        }

        if (training == null)
        {
            RefreshBuff(pm);
            return 0;
        }

        // Pinned's own acceleration already multiplied this gain (deviation 1): never both.
        if (pm.AcceleratedSkill == skill.SkillName && pm.AcceleratedStart > Core.Now)
        {
            return 0;
        }

        var extra = gained * (Utility.RandomMinMax(2, 4) - 1);
        var added = AddAsPinnedGains(pm, skill, extra);

        if (!_buffed.Contains(pm))
        {
            Announce(pm, training, true); // accepted inside the region (deviation 3)
        }

        RefreshBuff(pm); // the gain may have completed the quest
        return added;
    }

    // Pinned SkillCheck.Gain's rules for an amount (SkillCheck.cs:259-281): a down-locked skill may drop by it, and nothing is
    // added once the total is at the cap.
    private static int AddAsPinnedGains(PlayerMobile pm, Skill skill, int amount)
    {
        var skills = pm.Skills;

        if (skills.Total / (double)skills.Cap >= Utility.RandomDouble())
        {
            for (var i = 0; i < skills.Length; ++i)
            {
                var toLower = skills[i];

                if (toLower != skill && toLower.Lock == SkillLock.Down && toLower.BaseFixedPoint >= amount)
                {
                    toLower.BaseFixedPoint = Math.Max(toLower.BaseFixedPoint - amount, 0);
                    break;
                }
            }
        }

        if (skills.Total >= skills.Cap)
        {
            return 0;
        }

        var before = skill.BaseFixedPoint;
        skill.BaseFixedPoint = Math.Min(skill.BaseFixedPoint + amount, skill.CapFixedPoint);
        return skill.BaseFixedPoint - before;
    }

    /// <summary>The enter or leave line, and the buff, for one area (ServUO ApprenticeRegion.OnEnter and OnExit).</summary>
    public static void Announce(PlayerMobile pm, Area area, bool entering)
    {
        pm.SendLocalizedMessage(entering ? area.Enter : area.Leave);

        if (entering)
        {
            var name = pm.Skills[area.Skill]?.Name ?? area.Skill.ToString();
            pm.AddBuff(new BuffInfo(BuffIcon.ArcaneEmpowerment, 1078511, 1078512, args: name)); // Accelerated Skillgain Skill: ~1_val~
            _buffed.Add(pm);
        }
    }

    /// <summary>Removes this system's buff from a player who no longer trains where they stand.</summary>
    public static void RefreshBuff(PlayerMobile pm)
    {
        if (!_buffed.Contains(pm))
        {
            return;
        }

        foreach (var area in Training(pm))
        {
            if (InRegion(pm, area.Region) != null)
            {
                return;
            }
        }

        RemoveBuff(pm);
    }

    /// <summary>Removes this system's buff, if it shows (ServUO QuestHelper.RemoveAcceleratedSkillgain).</summary>
    public static void RemoveBuff(PlayerMobile pm)
    {
        if (_buffed.Remove(pm))
        {
            pm.RemoveBuff(BuffIcon.ArcaneEmpowerment);
        }
    }

    /// <summary>True while this system's buff shows on <paramref name="pm"/>.</summary>
    public static bool HasBuff(PlayerMobile pm) => _buffed.Contains(pm);
}

/// <summary>
/// ServUO's ApprenticeRegion (Services/MondainsLegacyQuests/ApprenticeRegion.cs), on pinned's stub. The rules a player meets
/// (no housing, Young protection) are the parent's: every Region virtual hands on to Parent (Region.cs:745-880), and
/// BaseRegion.YoungProtected is true (BaseRegion.cs:43).
/// </summary>
public class ApprenticeTrainingRegion : ApprenticeRegion
{
    /// <summary>
    /// cc-P678: the north woods, x 3590-3710, y 2378-2459, around the undead field's new spot (ClusterFHavenTownSpawns.
    /// TrainingField, 3640,2418, home and walking range 40). On SoS ServUO's "Old Haven Training" (Data/Regions.xml:
    /// 1442-1444, x 3589, y 2443, 115 x 100) and P189's walk to the Rootdeep hole are the author's Haven town, New Haven
    /// now (deviation 5). The region stops at the town's north edge (y 2460). Rectangle3D's end is exclusive.
    /// </summary>
    public static readonly Rectangle3D[] OldHavenArea =
    [
        new(new Point3D(3590, 2378, MinZ), new Point3D(3711, 2460, MaxZ))
    ];

    public static ApprenticeTrainingRegion? OldHaven { get; private set; }

    public ApprenticeTrainingRegion(string name, Map map, Region? parent, params Rectangle3D[] area)
        : base(name, map, parent, DefaultPriority, area)
    {
    }

    /// <summary>
    /// Registered every start, after RegionJsonSerializer.LoadRegions and World.Load (Main.cs:671-675), as the Burrows are.
    /// The parent is pinned's "Haven Island" (regions.json:1499-1506, a NoHousingRegion), which ServUO nests it under too;
    /// on Endless Journey nothing else is loaded over these rectangles (pinned's "Haven" ends at SE, regions.json:1670-1684).
    /// </summary>
    public static void Initialize()
    {
        if (OldHaven != null || Map.Trammel == null)
        {
            return;
        }

        var parent = Find("Haven Island", Map.Trammel);
        OldHaven = new ApprenticeTrainingRegion(ApprenticeTraining.OldHavenTraining, Map.Trammel, parent, OldHavenArea);
        OldHaven.Register();
    }

    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (m is PlayerMobile pm)
        {
            foreach (var area in ApprenticeTraining.Training(pm))
            {
                if (IsPartOf(area.Region))
                {
                    ApprenticeTraining.Announce(pm, area, true);
                }
            }
        }
    }

    public override void OnExit(Mobile m)
    {
        base.OnExit(m);

        // m.Region is still this region here (Mobile.UpdateRegion sets it after OnExit, Mobile.cs:7045-7048).
        if (m is PlayerMobile pm)
        {
            foreach (var area in ApprenticeTraining.Training(pm))
            {
                if (IsPartOf(area.Region))
                {
                    ApprenticeTraining.Announce(pm, area, false);
                }
            }

            ApprenticeTraining.RemoveBuff(pm);
        }
    }
}
