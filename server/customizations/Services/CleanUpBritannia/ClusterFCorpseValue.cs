// cc-P175 Part B (Chase 2026-10-18: "monster corpse values"): a corpse a Custodian clears ([cleanup, [cleanupall) pays by
// the creature that died, on top of every item inside at its Clean Up value (ClusterFCustodianSystem.ComputeCorpsePoints).
// Until cc-P175 the base was a flat 0.10 for any corpse.
//
// The scale is the creature's Fame, the one number pinned gives every creature for how hard it is (it is what
// Titles.AwardFame pays for the kill, BaseCreature.cs OnDeath). The shard has no other difficulty number: the only one in
// pinned is bard difficulty (BaseInstrument.GetDifficultyFor, BaseInstrument.cs:307), which depends on the instrument.
// Base = Fame x PointsPerFame (0.001): a rabbit 0.15, an orc 1.5, a dragon 15, an ancient wyrm 22.5.
//
// What pays nothing (base 0; the items inside still pay, as litter does):
//   * a summoned creature, a tamed, bonded or animated one, anything with a master (pinned BaseCreature.GetMaster), and
//     anything ever tamed (its Owners list; an unbonded pet's control is already released when the death event fires,
//     so Owners, which taming fills and the death leaves, is what still says it was a pet);
//   * a creature with no Fame;
//   * a corpse the Custodian did not earn: earned means the Custodian, or a member of the Custodian's party, held looting
//     rights at the death (pinned BaseCreature.GetLootingRights, the list Corpse.cs:303-311 gives the corpse; damage by
//     a pet counts for its master). Anyone may loot a creature's corpse (Corpse.IsCriminalAction is false for it), so the
//     rights list, not the loot rule, is what says who earned it;
//   * a corpse with nothing on record: one from before a restart (the record below is not saved; a corpse decays in
//     minutes), or one no creature death made.
// Player corpses are not litter at all (ClusterFCustodianSystem.IsEligible). Creatures staff add with [add are not marked
// by pinned, so they pay like any other; staff choose what they spawn.
//
// Recorded on pinned's CreatureDeathEvent (BaseCreature.OnDeath, after the corpse is made), held beside the corpse in a
// ConditionalWeakTable, like Clean Up's barrel lists (CleanUpTrash.cs): nothing new is saved, so no save format changes.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.PartySystem;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.CleanUpBritannia;

public static class ClusterFCorpseValue
{
    /// <summary>Clean Up points per point of the creature's Fame.</summary>
    public const double PointsPerFame = 0.001;

    private sealed class Record
    {
        public double Base;
        public readonly HashSet<Mobile> Earned = [];
    }

    private static readonly ConditionalWeakTable<Corpse, Record> _records = new();

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (bc?.Corpse is Corpse corpse && !corpse.Deleted)
        {
            Remember(bc, corpse);
        }
    }

    /// <summary>Records what clearing this creature's corpse is worth and who earned it.</summary>
    public static void Remember(BaseCreature bc, Corpse corpse)
    {
        var record = new Record { Base = BasePoints(bc) };

        foreach (var ds in BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax))
        {
            if (ds.m_HasRight && ds.m_Mobile != null)
            {
                record.Earned.Add(ds.m_Mobile);
            }
        }

        _records.AddOrUpdate(corpse, record);
    }

    /// <summary>A creature that pays when it dies: wild, unsummoned, with Fame.</summary>
    public static bool Pays(BaseCreature bc) =>
        bc != null && !bc.Summoned && !bc.Controlled && !bc.IsBonded && !bc.IsAnimatedDead && bc.GetMaster() == null &&
        bc.Owners.Count == 0 && bc.Fame > 0;

    /// <summary>The base points for clearing this creature's corpse: Fame x PointsPerFame, or 0.</summary>
    public static double BasePoints(BaseCreature bc) => Pays(bc) ? bc.Fame * PointsPerFame : 0.0;

    /// <summary>The base this Custodian earns for clearing the corpse: 0 unless the record says the Custodian earned it.</summary>
    public static double BaseFor(Corpse corpse, Mobile custodian)
    {
        if (corpse == null || custodian == null || !_records.TryGetValue(corpse, out var record) || record.Base <= 0)
        {
            return 0.0;
        }

        if (record.Earned.Contains(custodian))
        {
            return record.Base;
        }

        var party = Party.Get(custodian);
        if (party != null)
        {
            foreach (var m in record.Earned)
            {
                if (party.Contains(m))
                {
                    return record.Base;
                }
            }
        }

        return 0.0;
    }

    /// <summary>The base recorded at the death, whoever clears it (the appraisal and the tests); 0 with no record.</summary>
    public static double RecordedBase(Corpse corpse) =>
        corpse != null && _records.TryGetValue(corpse, out var record) ? record.Base : 0.0;
}
