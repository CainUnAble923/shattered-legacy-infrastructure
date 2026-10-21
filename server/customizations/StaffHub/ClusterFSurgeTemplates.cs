// ClusterFSurgeTemplates.cs
//
// cc-P230 (F-30 second batch, ours). The Staff Hub Events tab's saved creature groups ("templates"): a name, the creature
// types a wave draws from (each with a weight), and an optional named boss that comes with the last wave.
//
// Kept across restarts in their own save file (Saves/StaffHubSurgeTemplates/StaffHubSurgeTemplates.bin) through pinned's
// GenericPersistence, versioned, shard-wide rather than per account (every GM runs the same surges), and deliberately
// not in ClusterFAccountData, as ClusterFStaffHubSpots is not. An unknown version fails loudly (the D40 rule).
//
// Three starter templates are written the first time the store is empty and has never been seeded; a GM who deletes
// them does not get them back on the next restart (the Seeded flag is saved). Every creature named in them is a pinned
// type (cc-P230 notes, Part 2).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Mobiles;

namespace Server;

public sealed record SurgeEntry(string TypeName, int Weight);

public sealed record SurgeTemplate(string Name, IReadOnlyList<SurgeEntry> Creatures, string BossType, string BossName)
{
    public bool HasBoss => !string.IsNullOrEmpty(BossType);

    /// <summary>"Orc x4, OrcishMage x2; boss OrcishLord 'Name'", the line the Events tab shows.</summary>
    public string Describe() =>
        string.Join(", ", Creatures.Select(c => c.Weight > 1 ? $"{c.TypeName} x{c.Weight}" : c.TypeName)) +
        (HasBoss ? $"; boss {BossType}{(string.IsNullOrEmpty(BossName) ? "" : $" '{BossName}'")}" : "");
}

public class ClusterFSurgeTemplates : GenericPersistence
{
    // 0: cc-P230.
    public const int CurrentVersion = 0;

    public const int MaxTemplates = 20;
    public const int MaxNameLength = 32;
    public const int MaxEntries = 8;
    public const int MaxWeight = 20;

    private static ClusterFSurgeTemplates _instance;

    private static readonly List<SurgeTemplate> _templates = [];
    private static bool _seeded;

    public ClusterFSurgeTemplates() : base("StaffHubSurgeTemplates", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ClusterFSurgeTemplates();
        EventSink.WorldLoad += SeedIfNeverSeeded;
    }

    /// <summary>The three starter templates, all pinned creatures (Mobiles/Monsters/Humanoid).</summary>
    public static IReadOnlyList<SurgeTemplate> Starters { get; } =
    [
        new("Orc raid", [new("Orc", 4), new("OrcishMage", 2), new("OrcBomber", 1), new("OrcCaptain", 1)], null, null),
        new("Undead rising",
            [new("Skeleton", 3), new("Zombie", 3), new("Ghoul", 2), new("SkeletalMage", 2), new("BoneKnight", 1)],
            null, null),
        new("Ogre warband", [new("Ogre", 3), new("Ettin", 1)], "OgreLord", "Grumak the Breaker"),
    ];

    public static IReadOnlyList<SurgeTemplate> All => _templates;

    public static bool Seeded => _seeded;

    public static SurgeTemplate Find(string name) =>
        string.IsNullOrEmpty(name) ? null : _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Writes the starters once, the first time the shard has none and has never had them.</summary>
    public static void SeedIfNeverSeeded()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;
        if (_templates.Count == 0)
        {
            _templates.AddRange(Starters);
        }
    }

    /// <summary>
    /// A creature type a surge can make: a BaseCreature with a public constructor that needs no arguments (every
    /// parameter optional counts, as pinned's ActivatorExtensions.GetConstructor and CreateInstance treat it).
    /// </summary>
    public static Type CreatureType(string name)
    {
        var t = string.IsNullOrWhiteSpace(name) ? null : AssemblyHandler.FindTypeByName(name.Trim());
        return t != null && typeof(BaseCreature).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(out _) != null
            ? t
            : null;
    }

    /// <summary>
    /// Reads the creatures box: "Orc*4, OrcishMage*2, OrcBomber" (a weight after * or x, 1 when absent). Every name must
    /// be a creature type; on any error nothing is returned and the error says which.
    /// </summary>
    public static List<SurgeEntry> ParseCreatures(string text, out string error)
    {
        error = null;
        var list = new List<SurgeEntry>();

        foreach (var raw in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = raw;
            var weight = 1;
            var star = raw.IndexOfAny(['*', 'x', 'X'], 1);

            // "x" only splits when what follows is a number (OrcBomber has no x, but a name could).
            if (star > 0 && int.TryParse(raw[(star + 1)..].Trim(), out var w))
            {
                name = raw[..star].Trim();
                weight = w;
            }

            var type = CreatureType(name);
            if (type == null)
            {
                error = $"'{name}' is not a creature type.";
                return null;
            }

            if (weight < 1 || weight > MaxWeight)
            {
                error = $"{name}: a weight is 1 to {MaxWeight}.";
                return null;
            }

            list.Add(new SurgeEntry(type.Name, weight));
        }

        if (list.Count == 0)
        {
            error = "Name at least one creature, for example: Orc*4, OrcishMage*2";
            return null;
        }

        if (list.Count > MaxEntries)
        {
            error = $"A template holds at most {MaxEntries} creature types.";
            return null;
        }

        return list;
    }

    /// <summary>Saves a template (a template of the same name is replaced). Returns a line for the GM either way.</summary>
    public static bool Save(string name, string creatures, string bossType, string bossName, out string message)
    {
        name = name?.Trim() ?? "";
        bossType = bossType?.Trim() ?? "";
        bossName = bossName?.Trim() ?? "";

        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            message = $"Name the template (1 to {MaxNameLength} characters).";
            return false;
        }

        var entries = ParseCreatures(creatures, out message);
        if (entries == null)
        {
            return false;
        }

        string boss = null;
        if (bossType.Length > 0)
        {
            var t = CreatureType(bossType);
            if (t == null)
            {
                message = $"Boss: '{bossType}' is not a creature type.";
                return false;
            }

            boss = t.Name;
        }

        if (bossName.Length > MaxNameLength)
        {
            message = $"A boss name is at most {MaxNameLength} characters.";
            return false;
        }

        var replaced = _templates.RemoveAll(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!replaced && _templates.Count >= MaxTemplates)
        {
            message = $"There are already {MaxTemplates} templates; delete one first.";
            return false;
        }

        _templates.Add(new SurgeTemplate(name, entries, boss, boss == null || bossName.Length == 0 ? null : bossName));
        message = $"{(replaced ? "Replaced" : "Saved")} template '{name}'.";
        return true;
    }

    /// <summary>Deletes a template by name.</summary>
    public static bool Delete(string name) =>
        _templates.RemoveAll(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;

    // ---------------------------------------------------------------- persistence

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    public static void WriteTo(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.Write(_seeded);
        writer.WriteEncodedInt(_templates.Count);

        foreach (var t in _templates)
        {
            writer.Write(t.Name);
            writer.Write(t.BossType);
            writer.Write(t.BossName);
            writer.WriteEncodedInt(t.Creatures.Count);

            foreach (var c in t.Creatures)
            {
                writer.Write(c.TypeName);
                writer.WriteEncodedInt(c.Weight);
            }
        }
    }

    /// <summary>Replaces every template with what the reader holds.</summary>
    public static void ReadFrom(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"ClusterFSurgeTemplates version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        _templates.Clear();
        _seeded = reader.ReadBool();

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadString();
            var bossType = reader.ReadString();
            var bossName = reader.ReadString();
            var n = reader.ReadEncodedInt();
            var entries = new List<SurgeEntry>(n);

            for (var j = 0; j < n; j++)
            {
                entries.Add(new SurgeEntry(reader.ReadString(), reader.ReadEncodedInt()));
            }

            _templates.Add(new SurgeTemplate(name, entries, bossType, bossName));
        }
    }

    /// <summary>For the facts: empties the store and forgets it was ever seeded.</summary>
    public static void ResetForTests()
    {
        _templates.Clear();
        _seeded = false;
    }
}
