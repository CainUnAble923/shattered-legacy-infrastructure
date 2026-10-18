// cc-P142 (decision 6): where monster stealing's saved state lives. ServUO keeps two things in the mobiles' own saves:
// BaseCreature.HasBeenStolen (BaseCreature.cs:326-332) and PlayerMobile.NextGemOfSalvationUse. cc-P127 held both in
// memory, so a restart let a creature be stolen from again and a Gem of Salvation be used again. With cc-P142's
// one-success rule the flag has to outlive a restart, so both live here: a side table we own, in its own save file
// (Saves/MonsterStealing, pinned's GenericPersistence), the shape cc-P121's AbilityProfileStore and cc-P130's
// PetTrainingStore chose. BaseCreature's and PlayerMobile's save formats and versions are untouched, so a pinned bump
// cannot collide with it; the prompt's "serialize it, with a version bump" is met by this file's own version instead,
// argued in shard-migration notes/cc-P142-monster-stealing-shard-rules.md (the alternative, a BaseCreature patch, is
// listed there).
//
// What is kept: the creatures a thief has succeeded against (a set; a miss marks nothing, decision 3a) and each
// player's next Gem of Salvation time. A deleted creature or player drops out (the deletion events, and the save skips
// them). The per-creature retry cooldown after a miss is seconds long and stays in memory (StealingHandler).

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Engines.CreatureStealing;

public class MonsterStealingStore : GenericPersistence
{
    // 0: cc-P142.
    public const int CurrentVersion = 0;

    private static MonsterStealingStore _instance;

    private static readonly HashSet<BaseCreature> _stolen = new();
    private static readonly Dictionary<Mobile, DateTime> _gemNextUse = new();

    public MonsterStealingStore() : base("MonsterStealing", 10)
    {
    }

    public static void Configure()
    {
        if (_instance != null)
        {
            return; // the tests call this too; one registration
        }

        _instance = new MonsterStealingStore();
    }

    /// <summary>The registered persistence (after Configure); tests drive its Serialize/Deserialize directly.</summary>
    public static MonsterStealingStore Instance => _instance;

    public static int StolenCount => _stolen.Count;

    public static bool HasBeenStolen(BaseCreature bc) => bc != null && _stolen.Contains(bc);

    public static void SetStolen(BaseCreature bc, bool value)
    {
        if (bc == null)
        {
            return;
        }

        if (value)
        {
            if (!bc.Deleted)
            {
                _stolen.Add(bc);
            }
        }
        else
        {
            _stolen.Remove(bc);
        }
    }

    public static DateTime GemNextUse(Mobile m) =>
        m != null && _gemNextUse.TryGetValue(m, out var next) ? next : DateTime.MinValue;

    public static void SetGemNextUse(Mobile m, DateTime next)
    {
        if (m == null)
        {
            return;
        }

        if (next <= DateTime.MinValue)
        {
            _gemNextUse.Remove(m);
        }
        else
        {
            _gemNextUse[m] = next;
        }
    }

    /// <summary>Tests only: forget everything, as a fresh world would.</summary>
    public static void ClearForTests()
    {
        _stolen.Clear();
        _gemNextUse.Clear();
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc) => _stolen.Remove(bc);

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile pm) => _gemNextUse.Remove(pm);

    public override void Serialize(IGenericWriter writer)
    {
        var stolen = new List<BaseCreature>();

        foreach (var bc in _stolen)
        {
            if (!bc.Deleted)
            {
                stolen.Add(bc);
            }
        }

        var gems = new List<KeyValuePair<Mobile, DateTime>>();

        foreach (var pair in _gemNextUse)
        {
            if (!pair.Key.Deleted && pair.Value > Core.Now)
            {
                gems.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);

        writer.WriteEncodedInt(stolen.Count);
        foreach (var bc in stolen)
        {
            writer.Write(bc);
        }

        writer.WriteEncodedInt(gems.Count);
        foreach (var (m, next) in gems)
        {
            writer.Write(m);
            writer.Write(next);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"MonsterStealing save version {version} is newer than this build reads ({CurrentVersion})."
            );
        }

        var stolenCount = reader.ReadEncodedInt();
        for (var i = 0; i < stolenCount; i++)
        {
            var bc = reader.ReadEntity<BaseCreature>();
            if (bc is { Deleted: false })
            {
                _stolen.Add(bc);
            }
        }

        var gemCount = reader.ReadEncodedInt();
        for (var i = 0; i < gemCount; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var next = reader.ReadDateTime();
            if (m is { Deleted: false } && next > Core.Now)
            {
                _gemNextUse[m] = next;
            }
        }
    }
}
