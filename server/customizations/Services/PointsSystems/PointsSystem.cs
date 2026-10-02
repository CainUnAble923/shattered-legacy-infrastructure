// cc-P33 (F-3): the smallest points framework Clean Up Britannia needs, shaped on ServUO pub57's
// Services/PointsSystems/PointsSystem.cs so the next points port (Virtue Artifacts, the same file's
// VirtueArtifactsSystem) registers as one more subclass. Pinned 7c9215d97 has no points framework.
//
// Kept from ServUO: points are a double per character (PlayerMobile), AwardPoints caps at MaxPoints,
// DeductPoints refuses an overdraft, AutoAdd creates an entry on first touch, a system may derive its
// own entry (GetSystemEntry, ServUO :198), and every system lives in one save file.
// Left out: the loyalty gump, titles, kill and quest hooks, ConvertFromOldSystem. Nothing here uses them.
// Storage is pinned's GenericPersistence keyed by PlayerMobile, the pattern of
// Engines/CannedEvil/ChampionTitleSystem.cs, written to Saves/PointsSystem/PointsSystem.bin.

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Engines.Points;

// ServUO's enum lists 26 systems. Only the ones this shard runs are here; the save file writes the
// name, not the number, so adding one later never renumbers a save.
public enum PointsType
{
    CleanUpBritannia
}

public class PointsEntry
{
    public PointsEntry(PlayerMobile pm) => Player = pm;

    [CommandProperty(AccessLevel.GameMaster)]
    public PlayerMobile Player { get; }

    [CommandProperty(AccessLevel.GameMaster)]
    public double Points { get; set; }

    public virtual void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.Write(Points);
    }

    public virtual void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new InvalidDataException($"PointsEntry version {version} is not one this build reads (0).");
        }

        Points = reader.ReadDouble();
    }
}

public abstract class PointsSystem
{
    private static readonly Dictionary<PointsType, PointsSystem> _systems = new();

    private readonly Dictionary<PlayerMobile, PointsEntry> _table = new();

    protected PointsSystem() => _systems[Loyalty] = this;

    public abstract PointsType Loyalty { get; }
    public abstract bool AutoAdd { get; }
    public abstract double MaxPoints { get; }

    public static IEnumerable<PointsSystem> Systems => _systems.Values;

    public IReadOnlyDictionary<PlayerMobile, PointsEntry> PlayerTable => _table;

    public static PointsSystem GetSystemInstance(PointsType type) => _systems.GetValueOrDefault(type);

    // ServUO :120. Message on by default, as there.
    public virtual void AwardPoints(Mobile from, double points, bool quest = false, bool message = true)
    {
        if (from is not PlayerMobile pm || points <= 0)
        {
            return;
        }

        var entry = GetEntry(pm);

        if (entry == null)
        {
            return;
        }

        var old = entry.Points;
        entry.Points = Math.Min(MaxPoints, entry.Points + points);
        OnPointsAwarded(entry, entry.Points - old);

        if (message)
        {
            SendMessage(pm, old, points, quest);
        }
    }

    // What an award actually added, after the cap. Clean Up keeps its lifetime total here.
    protected virtual void OnPointsAwarded(PointsEntry entry, double added)
    {
    }

    public virtual void SendMessage(PlayerMobile from, double old, double points, bool quest)
    {
    }

    // ServUO :149.
    public virtual bool DeductPoints(Mobile from, double points, bool message = false)
    {
        var entry = GetEntry(from);

        if (entry == null || entry.Points < points)
        {
            return false;
        }

        entry.Points -= points;
        return true;
    }

    public double GetPoints(Mobile from) => GetEntry(from)?.Points ?? 0.0;

    public void SetPoints(PlayerMobile pm, double points)
    {
        var entry = GetEntry(pm, true);

        if (entry != null)
        {
            entry.Points = points;
        }
    }

    public PointsEntry GetEntry(Mobile from, bool create = false)
    {
        if (from is not PlayerMobile pm)
        {
            return null;
        }

        if (!_table.TryGetValue(pm, out var entry) && (create || AutoAdd))
        {
            _table[pm] = entry = GetSystemEntry(pm);
        }

        return entry;
    }

    public TEntry GetPlayerEntry<TEntry>(Mobile from, bool create = false) where TEntry : PointsEntry =>
        GetEntry(from, create) as TEntry;

    // ServUO :198: override to keep more per player than the points.
    public virtual PointsEntry GetSystemEntry(PlayerMobile pm) => new(pm);

    public void RemoveEntry(PlayerMobile pm) => _table.Remove(pm);

    public void Clear() => _table.Clear();

    public virtual void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt(_table.Count);

        foreach (var (pm, entry) in _table)
        {
            writer.Write(pm);
            entry.Serialize(writer);
        }
    }

    public virtual void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new InvalidDataException(
                $"{GetType().Name} version {version} is not one this build reads (0).");
        }

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var pm = reader.ReadEntity<PlayerMobile>();

            // A deleted character still has its bytes; read them, keep nothing.
            var entry = GetSystemEntry(pm);
            entry.Deserialize(reader);

            if (pm != null)
            {
                _table[pm] = entry;
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(Mobile m)
    {
        if (m is PlayerMobile pm)
        {
            foreach (var system in _systems.Values)
            {
                system.RemoveEntry(pm);
            }
        }
    }
}

public class PointsSystemPersistence : GenericPersistence
{
    private static PointsSystemPersistence _instance;

    public PointsSystemPersistence() : base("PointsSystem", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new PointsSystemPersistence();
    }

    public override void Serialize(IGenericWriter writer) => WriteAll(writer);

    public override void Deserialize(IGenericReader reader) => ReadAll(reader);

    public static void WriteAll(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        var systems = new List<PointsSystem>(PointsSystem.Systems);
        writer.WriteEncodedInt(systems.Count);

        foreach (var system in systems)
        {
            writer.Write(system.Loyalty.ToString());
            system.Serialize(writer);
        }
    }

    // A system this build does not know cannot be skipped (its length is not written), so it fails
    // loudly rather than misreading every byte after it (the D40 rule).
    public static void ReadAll(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new InvalidDataException($"PointsSystem save version {version} is not one this build reads (0).");
        }

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadString();

            if (!Enum.TryParse<PointsType>(name, out var type) || PointsSystem.GetSystemInstance(type) is not { } system)
            {
                throw new InvalidDataException($"PointsSystem save names a system this build does not run: {name}.");
            }

            system.Clear();
            system.Deserialize(reader);
        }
    }
}
