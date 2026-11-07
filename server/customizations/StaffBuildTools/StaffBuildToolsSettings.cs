// StaffBuildToolsSettings.cs
//
// cc-P812 (P798 ranks 1 and 2): the numbers of two staff tools, the Visual Builder's decor capture (DecorCapture.cs) and the
// animation locator (AnimLocator.cs). Configuration/staff-build-tools.json, written with these defaults on first start and
// read at start; a bad file is refused whole, every error is printed, and the defaults stay in force.
// Notes: shard-migration notes/cc-P812-decor-capture-animation-locator.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Server;

public sealed class StaffBuildToolsSettings
{
    public const string FileName = "staff-build-tools.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static StaffBuildToolsSettings Current { get; set; } = new();

    // ---- decor capture
    public int CaptureMaxSide { get; set; } = 64;
    public int CaptureMaxPieces { get; set; } = 4096;
    public bool CaptureMapStatics { get; set; }          // the map's own statics are the town, not the decorating
    public string CaptureFolder { get; set; } = "decor-captures"; // beside the world's Saves folder, never in a repo

    // Item types never captured, by name (a type or any of its base types).
    public List<string> CaptureSkipTypes { get; set; } = ["Corpse", "BaseMulti", "Spawner", "Moongate", "AnimLocatorStand"];

    // ---- animation locator
    public double SecondsPerAction { get; set; } = 3;
    public int FrameCount { get; set; } = 7;
    public int StandDistance { get; set; } = 2;

    // How many actions to step through, by the body's type (pinned's BodyType names).
    public Dictionary<string, int> ActionsByBodyType { get; set; } = new()
    {
        ["Monster"] = 22,
        ["Sea"] = 13,
        ["Animal"] = 13,
        ["Human"] = 35,
        ["Equipment"] = 35,
        ["Empty"] = 22,
    };

    public int ActionsFor(BodyType type) => ActionsByBodyType.TryGetValue(type.ToString(), out var n) ? n : 22;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(StaffBuildToolsSettings s) => JsonSerializer.Serialize(s, _json) + "\n";

    private static void Range(double value, double min, double max, string name, List<string> errors)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }

    public static StaffBuildToolsSettings Parse(string json, List<string> errors)
    {
        StaffBuildToolsSettings s;

        try
        {
            s = JsonSerializer.Deserialize<StaffBuildToolsSettings>(json, _json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return null;
        }

        if (s == null)
        {
            errors.Add("the file is empty.");
            return null;
        }

        Range(s.CaptureMaxSide, 1, 256, nameof(CaptureMaxSide), errors);
        Range(s.CaptureMaxPieces, 1, 65536, nameof(CaptureMaxPieces), errors);
        Range(s.SecondsPerAction, 0.5, 30, nameof(SecondsPerAction), errors);
        Range(s.FrameCount, 1, 30, nameof(FrameCount), errors);
        Range(s.StandDistance, 1, 10, nameof(StandDistance), errors);

        if (string.IsNullOrWhiteSpace(s.CaptureFolder) || s.CaptureFolder.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ||
            s.CaptureFolder.Contains(".."))
        {
            errors.Add("CaptureFolder must be a plain folder name.");
        }

        foreach (var (type, count) in s.ActionsByBodyType ?? [])
        {
            if (!Enum.TryParse<BodyType>(type, out _))
            {
                errors.Add($"ActionsByBodyType: \"{type}\" is not a body type.");
            }

            Range(count, 1, 100, $"ActionsByBodyType[{type}]", errors);
        }

        return errors.Count == 0 ? s : null;
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (File.Exists(path))
        {
            return Reload(path);
        }

        Current = new StaffBuildToolsSettings();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToJson(Current));
            return [$"{FileName} was missing; wrote the built-in defaults to {path}."];
        }
        catch (Exception ex)
        {
            return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in defaults."];
        }
    }

    public static List<string> Reload(string path)
    {
        var errors = new List<string>();
        StaffBuildToolsSettings s = null;

        try
        {
            s = Parse(File.ReadAllText(path), errors);
        }
        catch (Exception ex)
        {
            errors.Add($"could not read it: {ex.Message}");
        }

        if (s == null)
        {
            errors.Insert(0, $"{FileName} REFUSED, the settings in force are kept:");
            return errors;
        }

        Current = s;
        return [$"{FileName} loaded."];
    }

    public static void Configure()
    {
        foreach (var line in LoadOrWriteDefaults(FilePath))
        {
            Console.WriteLine($"[StaffBuildTools] {line}");
        }
    }
}
