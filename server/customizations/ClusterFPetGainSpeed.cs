// cc-P172 (Chase 2026-10-18, test shard, animal training on a pet Giant Spider: "pet skills do not increase rate... that
// should be something that increases in speed after loops/skill/outriders rank"). Pinned SkillCheck.cs:140-143 (d4531cd9)
// doubles a controlled creature's gain chance and that is all; nothing the owner does makes a pet learn faster.
//
// The rule: a controlled pet whose master is a player rolls its gain chance x Multiplier (on top of pinned's x2), where
//   Multiplier = 1 + loops x perLoop + perSkillPoint x (Animal Taming above 100 + Animal Lore above 100) + rank x perRank,
// at least 1 and capped at maxMultiplier. Each skill counts from 100 to 200 (Value, so worn bonuses count, as pinned's own
// taming checks read it). The rank is the owner's rung on the Rangers' League (Outriders) ladder, 0 Wanderer to 6 Legendary
// Outrider, while a member; 0 otherwise. Loops: F-12 is not built, so they come from EvoPet.OwnerLoopsSource (0 until F-12
// sets it), and an evo pet reads its own OwnerLoops (the same source, with staff's TestOwnerLoops stand-in).
//
// It makes the roll likelier, never a free gain: the one call site is SkillCheck-pet-gain-speed.patch, inside pinned's
// controlled-creature branch of CheckSkill, so every pet skill (Focus while training too, cc-P130) goes through it and an
// uncontrolled creature never does. Shown on the Animal Lore gump's skill pages (NewAnimalLoreGump). Notes:
// shard-migration notes/cc-P172-pet-gain-speed.md.

using System;
using Server.Mobiles;

namespace Server;

public static class ClusterFPetGainSpeed
{
    public const string RangersKey = "rangers";

    public const double DefaultPerLoop = 0.1;
    public const double DefaultPerSkillPoint = 0.004;
    public const double DefaultPerRank = 0.1;
    public const double DefaultMaxMultiplier = 3.0;

    public static double PerLoop { get; private set; } = DefaultPerLoop;
    public static double PerSkillPoint { get; private set; } = DefaultPerSkillPoint;
    public static double PerRank { get; private set; } = DefaultPerRank;
    public static double MaxMultiplier { get; private set; } = DefaultMaxMultiplier;

    public static void Configure()
    {
        PerLoop = ServerConfiguration.GetOrUpdateSetting("clusterf.petGain.perLoop", DefaultPerLoop);
        PerSkillPoint = ServerConfiguration.GetOrUpdateSetting("clusterf.petGain.perSkillPoint", DefaultPerSkillPoint);
        PerRank = ServerConfiguration.GetOrUpdateSetting("clusterf.petGain.perRank", DefaultPerRank);
        MaxMultiplier = ServerConfiguration.GetOrUpdateSetting("clusterf.petGain.maxMultiplier", DefaultMaxMultiplier);
    }

    /// <summary>The owner's Animal Taming and Animal Lore above 100, each to 200, x perSkillPoint.</summary>
    public static double OwnerSkillBonus(double taming, double lore) =>
        PerSkillPoint * (Math.Clamp(taming - 100.0, 0.0, 100.0) + Math.Clamp(lore - 100.0, 0.0, 100.0));

    /// <summary>The multiplier for these inputs: at least 1, at most maxMultiplier.</summary>
    public static double Multiplier(int loops, double taming, double lore, int rank) =>
        Math.Clamp(
            1.0 + Math.Max(loops, 0) * PerLoop + OwnerSkillBonus(taming, lore) + Math.Max(rank, 0) * PerRank,
            1.0,
            Math.Max(MaxMultiplier, 1.0)
        );

    /// <summary>The multiplier on <paramref name="creature"/>'s gain chance: 1 unless a player controls it.</summary>
    public static double Multiplier(Mobile creature) =>
        creature is BaseCreature { Controlled: true, ControlMaster: PlayerMobile owner } pet
            ? Multiplier(
                OwnerLoops(pet, owner),
                owner.Skills.AnimalTaming.Value,
                owner.Skills.AnimalLore.Value,
                OutridersRank(owner)
            )
            : 1.0;

    /// <summary>The owner's loops (F-12 stub: EvoPet.OwnerLoopsSource); an evo pet's own OwnerLoops for an evo pet.</summary>
    public static int OwnerLoops(BaseCreature pet, Mobile owner) =>
        Math.Max(pet is EvoPet evo ? evo.OwnerLoops : EvoPet.OwnerLoopsSource(owner), 0);

    /// <summary>
    /// The owner's rung on the Rangers' League ladder (ClusterFGuildSystem.RankThresholds("rangers")): 0 Wanderer to 6
    /// Legendary Outrider, raised to Scout once the Apprentice task is done (as GetRankName shows it); 0 for a non-member.
    /// </summary>
    public static int OutridersRank(Mobile owner)
    {
        if (!ClusterFGuildSystem.IsJoined(owner, RangersKey) || ClusterFAccountPersistence.GetGuild(owner) is not { } data)
        {
            return 0;
        }

        var thresholds = ClusterFGuildSystem.RankThresholds(RangersKey);
        var standing = data.GetReputation(RangersKey);

        if (ClusterFGuildSystem.IsApprentice(data, RangersKey))
        {
            standing = Math.Max(standing, thresholds[1]);
        }

        var rung = 0;
        for (var i = 1; i < thresholds.Length; i++)
        {
            if (standing >= thresholds[i])
            {
                rung = i;
            }
        }

        return rung;
    }

    /// <summary>The gain speed as the lore gump shows it ("x2.4").</summary>
    public static string Describe(Mobile creature) => $"x{Multiplier(creature):0.0#}";
}
