// cc-P610 (P491 #2): the mini champion controllers' pending restarts, saved. A controller that finished its levels
// waits RestartDelay and starts again; the wait was a timer only, and every inactive controller was given a fresh one at
// load, so one switched off on purpose came back. This store holds when each pending restart is due (by controller
// serial); a load resumes exactly those. A save made before the store existed has none to read (Loaded false): then the
// controllers keep ServUO's rule. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.

using System;
using System.Collections.Generic;

namespace Server.Engines.MiniChamps;

public static class MiniChampRestarts
{
    private static readonly Dictionary<Serial, DateTime> _pending = new();
    private static GenericPersistence _persistence;

    /// <summary>Whether this store was read from the save (it was not in a save made before it existed).</summary>
    public static bool Loaded { get; private set; }

    public static void Configure() => _persistence ??= new MiniChampRestartsPersistence();

    public static void Note(MiniChamp c, DateTime at) => _pending[c.Serial] = at;

    public static void Clear(MiniChamp c) => _pending.Remove(c.Serial);

    public static DateTime? PendingAt(MiniChamp c) => _pending.TryGetValue(c.Serial, out var at) ? at : null;

    /// <summary>Forgets every pending restart (tests: as a fresh process, before the store is read).</summary>
    public static void ClearAll()
    {
        _pending.Clear();
        Loaded = false;
    }

    public static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt(_pending.Count);
        foreach (var (serial, at) in _pending)
        {
            writer.Write(serial);
            writer.Write(at);
        }
    }

    public static void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            _pending[serial] = reader.ReadDateTime();
        }

        Loaded = true;
    }
}

public sealed class MiniChampRestartsPersistence : GenericPersistence
{
    public MiniChampRestartsPersistence() : base("MiniChampRestarts", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => MiniChampRestarts.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => MiniChampRestarts.Deserialize(reader);
}
