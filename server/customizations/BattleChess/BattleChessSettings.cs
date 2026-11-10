// BattleChessSettings.cs
//
// cc-P862: where the Battle Chess board stands and how long a game may wait. Configuration/battle-chess.json, written with
// these defaults on first start and read at start; a bad file is refused whole, every error is printed, and the settings in
// force are kept. [BattleChess reload reads it again. Notes: shard-migration notes/cc-P862-battle-chess.md.
//
// The board is 10 x 10 tiles: a rim of stairs at the ground's Z, and the 8 x 8 squares on a dais 5 above it. X, Y is the
// rim's north-west corner. The default spot is the Haven inn's front yard on main's New Haven (SoS Trammel), south of the
// inn's front doors and its casino: the inn's rooms are 5 x 3 tiles, too small for a living board.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Server.BattleChess;

public sealed class BattleChessSettings
{
    public const string FileName = "battle-chess.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static BattleChessSettings Current { get; set; } = new();

    public bool Enabled { get; set; } = true;

    // ---- the board: the rim's north-west corner, at the ground's Z
    public string Map { get; set; } = "Trammel";
    public int X { get; set; } = 3673;
    public int Y { get; set; } = 2628;
    public int Z { get; set; }
    public string Where { get; set; } = "the Haven inn's yard";

    // ---- the pieces
    public int WhiteHue { get; set; } = 1151;
    public int BlackHue { get; set; } = 1175;
    public int StepMilliseconds { get; set; } = 450;   // a piece walks one square in this time

    // ---- waiting
    public int InviteMinutes { get; set; } = 2;        // an invitation nobody answers lapses
    public int MoveMinutes { get; set; } = 10;         // a player who does not move in this time loses on time
    public int AwayMinutes { get; set; } = 5;          // a player logged out this long forfeits
    public int StartRange { get; set; } = 12;          // how close to the board both players must be to start
    public int ClearSeconds { get; set; } = 20;        // the pieces stay this long after the game ends

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The named facet, or null for a name that is none (or the internal map).</summary>
    public static Map FacetOf(string name) =>
        Server.Map.TryParse(name ?? "", null, out var map) && map != null && map != Server.Map.Internal ? map : null;

    public static string ToJson(BattleChessSettings s) => JsonSerializer.Serialize(s, _json) + "\n";

    private static void Range(int value, int min, int max, string name, List<string> errors)
    {
        if (value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }

    public static BattleChessSettings Parse(string json, List<string> errors)
    {
        BattleChessSettings s;

        try
        {
            s = JsonSerializer.Deserialize<BattleChessSettings>(json, _json);
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

        if (FacetOf(s.Map) == null)
        {
            errors.Add($"Map: no facet \"{s.Map}\".");
        }

        Range(s.X, 0, 7000, nameof(X), errors);
        Range(s.Y, 0, 5000, nameof(Y), errors);
        Range(s.Z, -120, 110, nameof(Z), errors);
        Range(s.WhiteHue, 0, 3000, nameof(WhiteHue), errors);
        Range(s.BlackHue, 0, 3000, nameof(BlackHue), errors);
        Range(s.StepMilliseconds, 100, 3000, nameof(StepMilliseconds), errors);
        Range(s.InviteMinutes, 1, 60, nameof(InviteMinutes), errors);
        Range(s.MoveMinutes, 1, 120, nameof(MoveMinutes), errors);
        Range(s.AwayMinutes, 1, 120, nameof(AwayMinutes), errors);
        Range(s.StartRange, 2, 40, nameof(StartRange), errors);
        Range(s.ClearSeconds, 0, 600, nameof(ClearSeconds), errors);

        return errors.Count == 0 ? s : null;
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (File.Exists(path))
        {
            return Reload(path);
        }

        Current = new BattleChessSettings();

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
        BattleChessSettings s = null;

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
        return [$"{FileName} loaded: the board stands at {s.X},{s.Y},{s.Z} on {s.Map}{(s.Enabled ? "" : " (off)")}."];
    }
}
