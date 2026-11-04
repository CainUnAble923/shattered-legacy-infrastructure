// cc-P793: every number of the Peacock, in one table Chase tunes: Configuration/peacock.json, written with the defaults on
// first start if missing, read at start and on [Peacock reload. A bad file is refused whole, every error is printed, and
// the table in force stays (the BeekeepingTable pattern).
//
//   enabled             false: no meadow spawner is seeded (nothing is deleted)
//   minTameSkill, controlSlots     to tame it; a desert ostard's 29.1 and 1
//   strMin..wrestlingMax, fame, meat   a desert ostard's numbers; read when a peacock is made, so saved ones keep theirs
//   spawns              the meadows: [{ name, x, y, z, count, homeRange, minDelayMinutes, maxDelayMinutes }], on Trammel.
//                       A spot with a peacock spawner within a tile is left alone, so moving one here leaves the old one.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Mobiles;

public sealed class PeacockSpawn
{
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Count { get; set; } = 3;
    public int HomeRange { get; set; } = 12;
    public double MinDelayMinutes { get; set; } = 10;
    public double MaxDelayMinutes { get; set; } = 20;

    [JsonIgnore]
    public Point3D Location => new(X, Y, Z);
}

public sealed class PeacockTable
{
    public const string FileName = "peacock.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public bool Enabled { get; set; } = true;
    public double MinTameSkill { get; set; } = 29.1;
    public int ControlSlots { get; set; } = 1;

    public int StrMin { get; set; } = 94;
    public int StrMax { get; set; } = 170;
    public int DexMin { get; set; } = 56;
    public int DexMax { get; set; } = 75;
    public int IntMin { get; set; } = 6;
    public int IntMax { get; set; } = 10;
    public int HitsMin { get; set; } = 71;
    public int HitsMax { get; set; } = 88;
    public int DamageMin { get; set; } = 5;
    public int DamageMax { get; set; } = 11;
    public int PhysicalResistMin { get; set; } = 15;
    public int PhysicalResistMax { get; set; } = 20;
    public int FireResistMin { get; set; } = 5;
    public int FireResistMax { get; set; } = 15;
    public double MagicResistMin { get; set; } = 25.1;
    public double MagicResistMax { get; set; } = 30.0;
    public double TacticsMin { get; set; } = 25.3;
    public double TacticsMax { get; set; } = 40.0;
    public double WrestlingMin { get; set; } = 29.3;
    public double WrestlingMax { get; set; } = 44.0;
    public int Fame { get; set; } = 450;
    public int Meat { get; set; } = 3;

    // Checked on the SoS land (notes cc-P793-tools): open grass, every tile of the home range standable, on foot from Trinsic.
    public List<PeacockSpawn> Spawns { get; set; } = [];

    [JsonIgnore]
    public string Source { get; private set; } = "built-in defaults";

    private static PeacockTable? _current;

    public static PeacockTable Defaults => new()
    {
        Spawns =
        [
            new PeacockSpawn { Name = "Trinsic road meadow", X = 1986, Y = 2244, Z = 0 },
            new PeacockSpawn { Name = "West meadow", X = 1800, Y = 2172, Z = 8 }
        ]
    };

    public static PeacockTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string ToJson(PeacockTable table) => JsonSerializer.Serialize(table, Json);

    public static PeacockTable? Parse(string json, string source, List<string> errors)
    {
        PeacockTable? table;
        try
        {
            table = JsonSerializer.Deserialize<PeacockTable>(json, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"{source}: not valid JSON: {ex.Message}");
            return null;
        }

        if (table == null)
        {
            errors.Add($"{source}: empty");
            return null;
        }

        table.Source = source;
        Validate(table, errors);
        return errors.Count == 0 ? table : null;
    }

    private static void Range(List<string> errors, string name, double min, double max, double floor, double ceiling)
    {
        if (min < floor || max > ceiling || min > max)
        {
            errors.Add($"{name}: {min} to {max} is outside {floor} to {ceiling}, or the wrong way round");
        }
    }

    public static void Validate(PeacockTable t, List<string> errors)
    {
        Range(errors, "minTameSkill", t.MinTameSkill, t.MinTameSkill, 0, 120);
        Range(errors, "controlSlots", t.ControlSlots, t.ControlSlots, 1, 5);
        Range(errors, "str", t.StrMin, t.StrMax, 1, 1000);
        Range(errors, "dex", t.DexMin, t.DexMax, 1, 1000);
        Range(errors, "int", t.IntMin, t.IntMax, 1, 1000);
        Range(errors, "hits", t.HitsMin, t.HitsMax, 1, 5000);
        Range(errors, "damage", t.DamageMin, t.DamageMax, 1, 100);
        Range(errors, "physicalResist", t.PhysicalResistMin, t.PhysicalResistMax, 0, 100);
        Range(errors, "fireResist", t.FireResistMin, t.FireResistMax, 0, 100);
        Range(errors, "magicResist", t.MagicResistMin, t.MagicResistMax, 0, 120);
        Range(errors, "tactics", t.TacticsMin, t.TacticsMax, 0, 120);
        Range(errors, "wrestling", t.WrestlingMin, t.WrestlingMax, 0, 120);
        Range(errors, "fame", t.Fame, t.Fame, 0, 32000);
        Range(errors, "meat", t.Meat, t.Meat, 0, 100);

        var seen = new HashSet<(int, int)>();
        foreach (var s in t.Spawns ??= [])
        {
            var name = string.IsNullOrWhiteSpace(s.Name) ? $"spawn at {s.X},{s.Y}" : s.Name;

            if (string.IsNullOrWhiteSpace(s.Name))
            {
                errors.Add($"{name}: no name");
            }

            if (!seen.Add((s.X, s.Y)))
            {
                errors.Add($"{name}: a second spawn at {s.X},{s.Y}");
            }

            Range(errors, $"{name} x", s.X, s.X, 0, 7167);
            Range(errors, $"{name} y", s.Y, s.Y, 0, 4095);
            Range(errors, $"{name} z", s.Z, s.Z, -128, 127);
            Range(errors, $"{name} count", s.Count, s.Count, 1, 20);
            Range(errors, $"{name} homeRange", s.HomeRange, s.HomeRange, 0, 60);
            Range(errors, $"{name} delay", s.MinDelayMinutes, s.MaxDelayMinutes, 0.5, 1440);
        }
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        var lines = new List<string>();
        if (!File.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson(Defaults));
                lines.Add($"wrote the default table to {path}");
            }
            catch (Exception ex)
            {
                lines.Add($"could not write {path}: {ex.Message}; the built-in defaults are in force");
            }

            Current = Defaults;
            return lines;
        }

        return Reload(path);
    }

    /// <summary>Reads the file; a bad one is refused and the table in force stays.</summary>
    public static List<string> Reload(string path)
    {
        var lines = new List<string>();
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            lines.Add($"could not read {path}: {ex.Message}; the table in force stays ({Current.Source})");
            return lines;
        }

        var errors = new List<string>();
        var table = Parse(json, path, errors);
        if (table == null)
        {
            lines.Add($"{path} refused; the table in force stays ({Current.Source}):");
            lines.AddRange(errors.Select(e => "  " + e));
            return lines;
        }

        Current = table;
        lines.Add($"peacock table in force: {path} ({table.Spawns.Count} meadows)");
        return lines;
    }
}
