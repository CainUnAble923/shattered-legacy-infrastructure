// StaffEventsSettings.cs
//
// cc-P810 (P797 ranks 1 and 3): every number of the two staff events, the egg hunt (StaffEggHunt.cs) and the mine maze
// (StaffMineMaze.cs). Configuration/staff-events.json, written with these defaults on first start and read at start; a bad
// file is refused whole, every error is printed, and the defaults stay in force. [StaffEvents reload reads it again.
// Notes: shard-migration notes/cc-P810-staff-events-egg-hunt-mine-maze.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.LootBoxes;

namespace Server;

public sealed class StaffEventsSettings
{
    public const string FileName = "staff-events.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static StaffEventsSettings Current { get; set; } = new();

    // ---- the egg hunt
    public int EggItemID { get; set; } = 0x47E6;      // the dragon Easter egg: one large egg
    public int EggTokens { get; set; } = 25;          // Festival Tokens for the finder
    public int EggMinutes { get; set; } = 30;         // nobody finds it in this time: it is gone
    public int EggArrowSeconds { get; set; } = 20;    // how often a player on the egg's facet without an arrow gets one
    public int EggClaimRange { get; set; } = 2;

    // ---- the mine maze
    public int MazeMinePercent { get; set; } = 35;    // of the tiles off the safe path
    public int MazeMinSide { get; set; } = 5;
    public int MazeMaxTiles { get; set; } = 1600;     // 40 x 40
    public int MazeWinners { get; set; } = 3;
    public string MazePrize { get; set; } = "Tokens"; // Tokens or LootBox
    public int MazeTokens { get; set; } = 20;
    public string MazeBoxTheme { get; set; } = "Explorer";
    public int MazeBoxTier { get; set; } = 1;
    public int MazeReviveSeconds { get; set; } = 5;   // a player killed in the maze is raised at its start after this

    public bool PrizeIsBox => MazePrize.Equals("LootBox", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(StaffEventsSettings s) => JsonSerializer.Serialize(s, _json) + "\n";

    private static void Range(double value, double min, double max, string name, List<string> errors)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }

    /// <summary>Reads settings, or returns null and fills errors: accepted whole or not at all.</summary>
    public static StaffEventsSettings Parse(string json, List<string> errors)
    {
        StaffEventsSettings s;

        try
        {
            s = JsonSerializer.Deserialize<StaffEventsSettings>(json, _json);
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

        Range(s.EggItemID, 1, 0xFFFF, nameof(EggItemID), errors);
        Range(s.EggTokens, 0, 10000, nameof(EggTokens), errors);
        Range(s.EggMinutes, 1, 24 * 60, nameof(EggMinutes), errors);
        Range(s.EggArrowSeconds, 5, 600, nameof(EggArrowSeconds), errors);
        Range(s.EggClaimRange, 1, 18, nameof(EggClaimRange), errors);
        Range(s.MazeMinePercent, 0, 90, nameof(MazeMinePercent), errors);
        Range(s.MazeMinSide, 3, 100, nameof(MazeMinSide), errors);
        Range(s.MazeMaxTiles, 9, 10000, nameof(MazeMaxTiles), errors);
        Range(s.MazeWinners, 1, 20, nameof(MazeWinners), errors);
        Range(s.MazeTokens, 0, 10000, nameof(MazeTokens), errors);
        Range(s.MazeBoxTier, 1, 6, nameof(MazeBoxTier), errors);
        Range(s.MazeReviveSeconds, 0, 600, nameof(MazeReviveSeconds), errors);

        if (!s.PrizeIsBox && !(s.MazePrize ?? "").Equals("Tokens", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"MazePrize must be Tokens or LootBox (it is \"{s.MazePrize}\").");
        }

        if (!Enum.TryParse<BoxTheme>(s.MazeBoxTheme ?? "", true, out _))
        {
            errors.Add($"MazeBoxTheme \"{s.MazeBoxTheme}\" is not a loot box theme.");
        }

        return errors.Count == 0 ? s : null;
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (File.Exists(path))
        {
            return Reload(path);
        }

        Current = new StaffEventsSettings();

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
        StaffEventsSettings s = null;

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
}
