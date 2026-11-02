// cc-P760: the reward stores' price table, one file: Configuration/reward-stores.json. Every row of every store (the guild
// stores, the Festival store and the Achievement store) by its key: its price and what opens it. The prices are
// placeholders (Chase's plan, section 6) to be tuned in play.
//
// Loaded the way cc-P78's recipe-gating.json is: a missing file is written with the built-in defaults (each row's own
// DefaultPrice and DefaultOpensAt); a file that fails any check is refused whole and the table in force is kept;
// [RewardStores reload reads it again. A row the file leaves out keeps its default. The test host runs no Configure, so it
// uses the defaults.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Commands;

namespace Server.Engines.RewardStores;

public sealed class RewardStorePrices
{
    public const string FileName = "reward-stores.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public sealed record Entry(int Price, int OpensAt);

    public string Source { get; init; } = "built-in defaults";

    public Dictionary<string, Entry> Rows { get; init; } = new(StringComparer.Ordinal);

    public static RewardStorePrices Defaults { get; } = new();

    public static RewardStorePrices Current { get; set; } = Defaults;

    public int PriceOf(RewardStoreRow row) => Rows.TryGetValue(row.Key, out var e) ? e.Price : row.DefaultPrice;

    public int OpensAtOf(RewardStoreRow row) => Rows.TryGetValue(row.Key, out var e) ? e.OpensAt : row.DefaultOpensAt;

    /// <summary>Every store whose rows the table prices, for the defaults file and the key check.</summary>
    public static IEnumerable<RewardStoreDef> AllStores()
    {
        foreach (var store in GuildStores.All())
        {
            yield return store;
        }

        yield return new FestivalStoreDef(null);
        yield return new AchievementStoreDef();
    }

    public static IEnumerable<RewardStoreRow> AllRows() => AllStores().SelectMany(s => s.AllRows());

    public static void Configure()
    {
        CommandSystem.Register("RewardStores", AccessLevel.GameMaster, OnCommand);

        EventSink.ServerStarted += () =>
        {
            foreach (var line in LoadOrWriteDefaults(FilePath))
            {
                Console.WriteLine($"[RewardStores] {line}");
            }
        };
    }

    [Usage("RewardStores [reload | grant <character name> <tokens|ap|renown|guild:<key>> <amount>]")]
    [Description("The reward stores' price table (cc-P760). With no argument, says where the prices came from. reload reads Configuration/reward-stores.json again (a bad file is refused whole). grant gives a character Festival Tokens, AP, Renown or a guild's points (joining the guild, standing set to the amount), for testing.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestFirst)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        var lines = verb switch
        {
            "reload" => Reload(FilePath),
            "grant"  => RewardStoreCommands.Grant(e.Arguments),
            _ => [$"Reward store prices from {Current.Source}: {Current.Rows.Count} rows in the file, {AllRows().Count()} rows in all. [RewardStores reload reads {FileName} again. [RewardStores grant <character name> <tokens|ap|guild:<key>> <amount> (staff, for testing)."]
        };
        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }

    // -- The file ---------------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private sealed class FileRow
    {
        public string? Store { get; set; }
        public string? Label { get; set; }
        public int? Price { get; set; }
        public int? OpensAt { get; set; }
    }

    private sealed class FileForm
    {
        public string? About { get; set; }
        public Dictionary<string, FileRow>? Rows { get; set; }
    }

    /// <summary>The defaults as the file: every row of every store, its store and label beside it for the reader.</summary>
    public static string DefaultsJson()
    {
        var rows = new Dictionary<string, FileRow>(StringComparer.Ordinal);
        foreach (var store in AllStores())
        {
            foreach (var row in store.AllRows())
            {
                rows[row.Key] = new FileRow
                {
                    Store = store.Title,
                    Label = row.Label,
                    Price = row.DefaultPrice,
                    OpensAt = row.DefaultOpensAt
                };
            }
        }

        return JsonSerializer.Serialize(
            new FileForm
            {
                About = "Reward store prices (cc-P760), placeholders to tune in play. One row per store row, by key: price in " +
                        "the store's currency, and opensAt (a guild row: the rank index 0 Initiate to 5 top rank; an " +
                        "Achievement row: the AP total; a Festival row: 0). store and label are for reading only. Edit and " +
                        "run [RewardStores reload; a bad file is refused whole.",
                Rows = rows
            },
            Json
        );
    }

    /// <summary>Reads a table. Null and errors when anything is wrong: not JSON, an unknown key, a negative number.</summary>
    public static RewardStorePrices? Parse(string json, string source, List<string> errors)
    {
        FileForm? f;
        try
        {
            f = JsonSerializer.Deserialize<FileForm>(json, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return null;
        }

        if (f == null)
        {
            errors.Add("the file is empty.");
            return null;
        }

        var known = AllRows().ToDictionary(r => r.Key, StringComparer.Ordinal);
        var table = new RewardStorePrices { Source = source };

        foreach (var (key, row) in f.Rows ?? new Dictionary<string, FileRow>())
        {
            if (!known.TryGetValue(key, out var def))
            {
                errors.Add($"rows.{key}: no store sells a row with this key.");
                continue;
            }

            var price = row.Price ?? def.DefaultPrice;
            var opens = row.OpensAt ?? def.DefaultOpensAt;
            if (price < 0 || opens < 0)
            {
                errors.Add($"rows.{key}: price and opensAt are 0 or more.");
                continue;
            }

            table.Rows[key] = new Entry(price, opens);
        }

        return errors.Count == 0 ? table : null;
    }

    /// <summary>At start: read the file, or write the defaults to it when it is missing. Returns lines to log.</summary>
    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (!File.Exists(path))
        {
            Current = Defaults;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, DefaultsJson());
                return [$"{FileName} was missing; wrote the built-in defaults to {path}."];
            }
            catch (Exception ex)
            {
                return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in defaults."];
            }
        }

        return Reload(path);
    }

    public static List<string> Reload(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return [$"Could not read {path} ({ex.Message}); kept {Current.Source}."];
        }

        return Apply(json, path);
    }

    /// <summary>Parses and, if it is good, puts the table in force. Returns lines to report.</summary>
    public static List<string> Apply(string json, string source)
    {
        var errors = new List<string>();
        var table = Parse(json, source, errors);

        if (table == null)
        {
            var lines = new List<string> { $"{FileName} REFUSED ({errors.Count} error{(errors.Count == 1 ? "" : "s")}); kept {Current.Source}:" };
            lines.AddRange(errors.Select(e => $"  {e}"));
            return lines;
        }

        Current = table;
        return [$"Loaded the reward store prices from {source} ({table.Rows.Count} rows)."];
    }
}
