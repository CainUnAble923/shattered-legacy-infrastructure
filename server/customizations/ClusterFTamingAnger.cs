// cc-P171 (Chase 2026-10-18, test shard: 30 tries in a row at 200 Animal Taming on a Dread Warhorse, every one "You seem to
// anger the beast!"). Pinned AnimalTaming.cs:208 (d4531cd9) angers a CanAngerOnTame creature 95% of the time whatever the
// tamer's skill; on a shard whose skills run to 200 the skill must count.
//
// The rule: chance = max(floor, base - perPoint x (tamer's Animal Taming - creature's MinTameSkill)), never above base, so
// at the minimum (or below it, which only a Dark Wolf familiar allows) it is pinned's 95%. Defaults: 120 vs 108 is 77%,
// 150 is 32%, 160 and up is the 15% floor. The skill is Skills.AnimalTaming.Value, the value pinned's own checks read
// (the "no chance" gate, AnimalTaming.cs:184, and the taming roll's CheckTargetSkill, :406), so worn bonuses count.
//
// The one call site is AnimalTaming-anger-scales.patch (the anger test at :208); the rest of the anger branch (the pacify
// keep, Honor and Ethereal Voyage, the attack) is pinned's, untouched. Notes:
// shard-migration notes/cc-P171-taming-anger-scales.md.

using System;
using Server.Mobiles;

namespace Server;

public static class ClusterFTamingAnger
{
    public const double DefaultBase = 0.95;
    public const double DefaultPerPoint = 0.015;
    public const double DefaultFloor = 0.15;

    public static double Base { get; private set; } = DefaultBase;
    public static double PerPoint { get; private set; } = DefaultPerPoint;
    public static double Floor { get; private set; } = DefaultFloor;

    public static void Configure()
    {
        Base = ServerConfiguration.GetOrUpdateSetting("clusterf.taming.angerBase", DefaultBase);
        PerPoint = ServerConfiguration.GetOrUpdateSetting("clusterf.taming.angerPerPoint", DefaultPerPoint);
        Floor = ServerConfiguration.GetOrUpdateSetting("clusterf.taming.angerFloor", DefaultFloor);
    }

    /// <summary>The chance at <paramref name="tamingValue"/> against a creature needing <paramref name="minTameSkill"/>.</summary>
    public static double Chance(double tamingValue, double minTameSkill) =>
        Math.Clamp(Math.Max(Floor, Base - PerPoint * (tamingValue - minTameSkill)), 0.0, Math.Max(Base, 0.0));

    /// <summary>The chance that <paramref name="tamer"/> angers <paramref name="creature"/>; 0 for one that never angers.</summary>
    public static double Chance(Mobile tamer, BaseCreature creature) =>
        creature.CanAngerOnTame ? Chance(tamer.Skills.AnimalTaming.Value, creature.MinTameSkill) : 0.0;

    /// <summary>Pinned's anger test (AnimalTaming.cs:208) with the chance above.</summary>
    public static bool Angers(Mobile tamer, BaseCreature creature) =>
        creature.CanAngerOnTame && Utility.RandomDouble() < Chance(tamer, creature);
}
