// cc-P130: where a creature's training state lives (Chase's decision 2a, 2026-10-15, as cc-P121's AbilityProfileStore).
// ServUO adds four things to BaseCreature's own save for training (BaseCreature.cs:2588-2614, read at :2942-2984): the
// TrainingProfile, ControlSlotsMin, ControlSlotsMax and CurrentTameSkill. Here they are a side table we own, keyed by
// the creature, in its own save file (Saves/PetTraining, pinned's GenericPersistence), so BaseCreature's save format
// and version are untouched and a pinned bump cannot collide with it.
//
// What a record holds, and what is derived instead of stored:
//   - Training: the TrainingProfile (with its plan), once a tamer has begun training (or opened the lore gump, which
//     ServUO creates it for).
//   - SlotsLowered: ServUO's CalculateSlots lowers a creature's minimum slots by one when its stats rolled low at
//     construction (the sixteen SlotLowerables). The roll is gone after construction, so the result is kept.
//   - CurrentTameSkill: set once training raises it (OnTrain) or AdjustTameRequirements runs; unset, it reads
//     MinTameSkill, which is what ServUO's MinTameSkill setter gives it at construction.
//   - ControlSlotsMin and ControlSlotsMax are NOT stored: ServUO's are the definition's (lowered when SlotsLowered),
//     and its load rule lowers the minimum to the saved slots when those are below it; BaseCreature.PetTraining.cs
//     derives exactly that from the definition, the flag and ControlSlots.
// A record is written only when it says something (training, or a lowered minimum, or a set tame skill), and goes
// when the creature is deleted. A world saved before this file existed loads empty (GenericPersistence returns without
// reading when the file is absent).
//
// Two one-time jobs ride with the store: UpgradeSlots (ServUO's load rule for a world saved before training: every
// tamable's slots become its minimum, once, with its master's follower count kept right), run from the world-load
// event and remembered in the file; and the "settled" set, which is how the ControlSlots hook tells a constructor's
// assignment (ServUO: the setter's first call, when min and max are still 0) from a later one.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using ModernUO.CodeGeneratedEvents;

namespace Server.Mobiles;

public class PetTrainingRecord
{
    public TrainingProfile Training { get; set; }
    public bool SlotsLowered { get; set; }
    public double? CurrentTameSkill { get; set; }

    public bool IsEmpty => Training == null && !SlotsLowered && CurrentTameSkill == null;
}

public class PetTrainingStore : GenericPersistence
{
    // 0: cc-P130.
    public const int CurrentVersion = 0;

    private static PetTrainingStore _instance;

    private static readonly Dictionary<BaseCreature, PetTrainingRecord> _records = new();
    private static readonly ConditionalWeakTable<BaseCreature, object> _settled = new();
    private static readonly object _settledMark = new();

    /// <summary>True once ServUO's upgrade pass for a pre-training world has run on this world.</summary>
    public static bool SlotsUpgraded { get; private set; }

    public PetTrainingStore() : base("PetTraining", 10)
    {
    }

    public static void Configure()
    {
        if (_instance != null)
        {
            return; // the tests call this too; one registration
        }

        _instance = new PetTrainingStore();
        EventSink.WorldLoad += OnWorldLoad;
    }

    /// <summary>The registered persistence (after Configure); tests drive its Serialize/Deserialize directly.</summary>
    public static PetTrainingStore Instance => _instance;

    public static int Count => _records.Count;

    public static PetTrainingRecord Get(BaseCreature bc, bool create)
    {
        if (bc == null)
        {
            return null;
        }

        if (!_records.TryGetValue(bc, out var record) && create && !bc.Deleted)
        {
            _records[bc] = record = new PetTrainingRecord();
        }

        return record;
    }

    public static TrainingProfile GetTrainingProfile(BaseCreature bc, bool create)
    {
        var record = Get(bc, create);

        if (record == null)
        {
            return null;
        }

        if (record.Training == null && create)
        {
            record.Training = new TrainingProfile(bc);
        }

        return record.Training;
    }

    public static bool IsSettled(BaseCreature bc) => _settled.TryGetValue(bc, out _);

    public static void MarkSettled(BaseCreature bc) => _settled.AddOrUpdate(bc, _settledMark);

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc) => _records.Remove(bc);

    private static void OnWorldLoad()
    {
        // Every creature in the save was built before this call: its ControlSlots assignment, if any, is a later one.
        foreach (var m in World.Mobiles.Values)
        {
            if (m is BaseCreature bc)
            {
                MarkSettled(bc);
            }
        }

        if (!SlotsUpgraded)
        {
            var changed = UpgradeSlots();
            Console.WriteLine($"[PetTraining] Pet Training slots upgraded on {changed} tamable creature(s) (one-time pass).");
        }
    }

    /// <summary>
    /// ServUO's load rule for a world saved before training (BaseCreature.cs:2957-2969): a tamable's slots become its
    /// minimum (the definition's, or its own slots when they are below it). A pet's master keeps the right follower
    /// count. Runs once per world; returns how many creatures changed.
    /// </summary>
    public static int UpgradeSlots()
    {
        var changed = 0;

        if (PetTrainingHelper.Enabled)
        {
            foreach (var m in World.Mobiles.Values)
            {
                if (m is not BaseCreature { Tamable: true, Deleted: false } bc)
                {
                    continue;
                }

                MarkSettled(bc);
                var min = bc.ControlSlotsMin;

                if (bc.ControlSlots != min)
                {
                    bc.RemoveFollowers();
                    bc.ControlSlots = min;
                    bc.AddFollowers();
                    changed++;
                }
            }
        }

        SlotsUpgraded = true;
        return changed;
    }

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<BaseCreature, PetTrainingRecord>>();

        foreach (var pair in _records)
        {
            if (!pair.Key.Deleted && !pair.Value.IsEmpty)
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.Write(SlotsUpgraded);
        writer.WriteEncodedInt(live.Count);

        foreach (var (creature, record) in live)
        {
            writer.Write(creature);
            writer.Write(record.SlotsLowered);
            writer.Write(record.CurrentTameSkill != null);

            if (record.CurrentTameSkill != null)
            {
                writer.Write(record.CurrentTameSkill.Value);
            }

            writer.Write(record.Training != null);
            record.Training?.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"PetTrainingStore version {version} is not one this build reads (0 to {CurrentVersion})."
            );
        }

        _records.Clear();
        SlotsUpgraded = reader.ReadBool();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var creature = reader.ReadEntity<BaseCreature>();
            var record = new PetTrainingRecord { SlotsLowered = reader.ReadBool() };

            if (reader.ReadBool())
            {
                record.CurrentTameSkill = reader.ReadDouble();
            }

            if (reader.ReadBool())
            {
                // Read in full either way, so the next entry starts in the right place.
                var training = new TrainingProfile(creature);
                training.Deserialize(reader);
                record.Training = training;
            }

            if (creature is { Deleted: false })
            {
                _records[creature] = record;
            }
        }
    }
}
