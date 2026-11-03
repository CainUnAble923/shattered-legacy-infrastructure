// cc-P780 (F-24): every number of the League hunts, in one table Chase tunes: Configuration/league-hunts.json, written
// with the defaults on first start if missing, read at start and by [LeagueHuntsStaff reload. A bad file is refused
// whole, every error is printed, and the table in force is kept (the RequisitionTable pattern). Notes in shard-migration
// notes/cc-P780-daily-league-hunts.md.
//
//   enabled          false: no hunts are rolled or counted, and the board shows none
//   dailyHunts       hunts rolled per character per refresh day (the requisitions board's day, refreshHourUtc there)
//   weeklyResetDay   the weekday the great hunt rolls over, at the requisitions refresh hour
//   shareRange       tiles from the kill within which a party member of the credited player also counts it
//   onlyWithSpawner  true: a family is offered only while one of its bestiary spawners stands in the world
//                    (ClusterFLeagueBestiary.Spawners), so no hunt names a creature that cannot be found
//   counts           kills needed by the family's use: cull (a culling pack), hunt (a hunt target), boss (a mini-boss)
//   tiers            per tier 0-5 (the requisitions tier: rank in the guild, capped by the League gate): the highest
//                    bestiary level a family may start at (BestiaryEntry.LevelFrom), and the Renown, guild standing and
//                    gold paid
//   great            the weekly great hunt: counts times countMultiplier, pay times payMultiplier, families up to
//                    levelBonus levels above the tier's cap, and a League shop loot box (Bronze, Silver, Gold or none)
//   families         the League Bestiary families a hunt can name: a key, the plural name shown, the bestiary type names
//                    counted, the use, and an optional slayer (a SlayerName) that a requisition slayer row marked "any"
//                    asks for while the family is hunted (empty: whatever pinned's slayer groups say slays it)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Engines.LeagueRewards;
using Server.Items;

namespace Server;

public sealed class LeagueHuntTierRow
{
    public int Tier { get; set; }
    public int MaxLevel { get; set; }
    public int Renown { get; set; }
    public int Reputation { get; set; }
    public int Gold { get; set; }
}

public sealed class LeagueHuntCounts
{
    public int Cull { get; set; } = 12;
    public int Hunt { get; set; } = 4;
    public int Boss { get; set; } = 1;
}

public sealed class LeagueGreatHuntRules
{
    public int CountMultiplier { get; set; } = 3;
    public int PayMultiplier { get; set; } = 4;
    public int LevelBonus { get; set; } = 30;
    public string LootBox { get; set; } = "Silver";
}

public sealed class LeagueHuntFamily
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Types { get; set; } = [];
    public string Use { get; set; } = "cull";
    public string Slayer { get; set; } = "";

    /// <summary>The bestiary entries this family counts (filled by validation).</summary>
    [JsonIgnore]
    public List<BestiaryEntry> Entries { get; } = [];

    /// <summary>The lowest bestiary level among the family's creatures.</summary>
    [JsonIgnore]
    public int LevelFrom => Entries.Count == 0 ? int.MaxValue : Entries.Min(e => e.LevelFrom);

    public bool Counts(Type type) => type != null && Entries.Any(e => e.Type.IsAssignableFrom(type));
}

public sealed class LeagueHuntTable
{
    public const string FileName = "league-hunts.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static readonly string[] Uses = ["cull", "hunt", "boss"];

    public string About { get; set; }
    public bool Enabled { get; set; } = true;
    public int DailyHunts { get; set; } = 2;
    public string WeeklyResetDay { get; set; } = "Monday";
    public int ShareRange { get; set; } = 18;
    public bool OnlyWithSpawner { get; set; } = true;
    public LeagueHuntCounts Counts { get; set; } = new();
    public List<LeagueHuntTierRow> Tiers { get; set; } = [];
    public LeagueGreatHuntRules Great { get; set; } = new();
    public List<LeagueHuntFamily> Families { get; set; } = [];

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    [JsonIgnore]
    public DayOfWeek ResetDay => Enum.TryParse<DayOfWeek>(WeeklyResetDay, true, out var d) ? d : DayOfWeek.Monday;

    private static LeagueHuntTable _defaults;
    private static LeagueHuntTable _current;

    public static LeagueHuntTable Defaults
    {
        get
        {
            if (_defaults == null)
            {
                var t = BuildDefaults();
                Validate(t, new List<string>()); // fills each family's entries
                _defaults = t;
            }

            return _defaults;
        }
    }

    public static LeagueHuntTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    // -- Lookups ------------------------------------------------------------------------------------

    public LeagueHuntTierRow TierFor(int tier)
    {
        LeagueHuntTierRow best = null;
        foreach (var row in Tiers)
            if (row.Tier <= tier && (best == null || row.Tier > best.Tier))
                best = row;
        return best ?? Tiers.FirstOrDefault() ?? new LeagueHuntTierRow { MaxLevel = 15, Renown = 10, Reputation = 40, Gold = 250 };
    }

    public LeagueHuntFamily Family(string key) =>
        Families.FirstOrDefault(f => f.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase));

    public int CountFor(LeagueHuntFamily family) => family.Use.ToLowerInvariant() switch
    {
        "boss" => Counts.Boss,
        "hunt" => Counts.Hunt,
        _      => Counts.Cull,
    };

    /// <summary>The box a great hunt pays, or null for none.</summary>
    public LeagueShop.BoxTier? GreatBox =>
        Enum.TryParse<LeagueShop.BoxTier>(Great?.LootBox ?? "", true, out var tier) ? tier : null;

    // -- Defaults -----------------------------------------------------------------------------------

    private static LeagueHuntFamily F(string key, string name, string use, params string[] types) =>
        new() { Key = key, Name = name, Use = use, Types = types.ToList() };

    public static LeagueHuntTable BuildDefaults() => new()
    {
        About = "League hunts (cc-P780, F-24): daily kill goals on the requisitions board and one weekly great hunt. Edit and " +
                "run [LeagueHuntsStaff reload; a bad file is refused whole and the table in force is kept. counts: kills " +
                "needed by use (cull, hunt, boss). tiers: by the requisitions tier 0-5, the highest bestiary level a family " +
                "may start at and the Renown, guild standing and gold paid. great: the weekly hunt's multipliers, its extra " +
                "level reach and its League shop box (Bronze, Silver, Gold or empty). families: League Bestiary creatures " +
                "by type name; slayer is optional (a SlayerName a requisition slayer row marked any then asks for).",
        Enabled = true,
        DailyHunts = 2,
        WeeklyResetDay = "Monday",
        ShareRange = 18,
        OnlyWithSpawner = true,
        Counts = new LeagueHuntCounts { Cull = 12, Hunt = 4, Boss = 1 },
        // Pay: the requisitions tier's Renown and standing (a hunt is a day's work like a requisition), plus gold near
        // what a requisition item's Appraiser price pays at that tier.
        Tiers =
        [
            new() { Tier = 0, MaxLevel = 15, Renown = 10, Reputation = 40, Gold = 250 },
            new() { Tier = 1, MaxLevel = 30, Renown = 20, Reputation = 60, Gold = 400 },
            new() { Tier = 2, MaxLevel = 40, Renown = 35, Reputation = 90, Gold = 600 },
            new() { Tier = 3, MaxLevel = 50, Renown = 55, Reputation = 130, Gold = 900 },
            new() { Tier = 4, MaxLevel = 75, Renown = 80, Reputation = 180, Gold = 1300 },
            new() { Tier = 5, MaxLevel = 200, Renown = 110, Reputation = 250, Gold = 1800 },
        ],
        Great = new LeagueGreatHuntRules { CountMultiplier = 3, PayMultiplier = 4, LevelBonus = 30, LootBox = "Silver" },
        Families =
        [
            F("hedge-snatchers", "hedge snatchers", "cull", "HedgeSnatcher"),
            F("snatcher-outriders", "snatcher outriders", "hunt", "SnatcherOutrider"),
            F("hornback-raiders", "hornback raiders", "hunt", "HornbackRaider"),
            F("ditch-crawlers", "ditch crawlers", "cull", "DitchCrawler"),
            F("mire-croakers", "mire croakers", "hunt", "MireCroaker"),
            F("brine-lurkers", "brine lurkers", "cull", "BrineLurker"),
            F("moonmaws", "moonmaws", "cull", "Moonmaw"),
            F("thornwisps", "thornwisps", "cull", "Thornwisp"),
            F("pale-drifters", "pale drifters", "cull", "PaleDrifter"),
            F("slaghearts", "slaghearts", "cull", "Slagheart"),
            F("glasswardens", "glasswardens", "cull", "Glasswarden"),
            F("tunnelmaws", "tunnelmaws", "hunt", "Tunnelmaw"),
            F("the-siegebreaker", "the Siegebreaker", "boss", "TheSiegebreaker"),
            F("the-hollow-oak", "the Hollow Oak", "boss", "TheHollowOak"),
        ],
    };

    // -- The file -----------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string ToJson(LeagueHuntTable table) => JsonSerializer.Serialize(table, Json);

    /// <summary>Reads a table from JSON. Returns null and fills errors when anything is wrong: accepted whole or not at all.</summary>
    public static LeagueHuntTable Parse(string json, string source, List<string> errors)
    {
        LeagueHuntTable table;
        try
        {
            table = JsonSerializer.Deserialize<LeagueHuntTable>(json, Json);
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

    public static void Validate(LeagueHuntTable t, List<string> errors)
    {
        if (t.DailyHunts is < 0 or > 5) errors.Add($"dailyHunts {t.DailyHunts} is not 0 to 5");
        if (!Enum.TryParse<DayOfWeek>(t.WeeklyResetDay ?? "", true, out _)) errors.Add($"weeklyResetDay \"{t.WeeklyResetDay}\" is not a weekday");
        if (t.ShareRange is < 0 or > 30) errors.Add($"shareRange {t.ShareRange} is not 0 to 30");

        if (t.Counts == null) errors.Add("counts: missing");
        else if (t.Counts.Cull < 1 || t.Counts.Hunt < 1 || t.Counts.Boss < 1) errors.Add("counts: every count must be 1 or more");

        if (t.Tiers == null || t.Tiers.All(r => r.Tier != 0)) errors.Add("tiers: no row for tier 0");
        foreach (var row in t.Tiers ?? new List<LeagueHuntTierRow>())
        {
            if (row.Tier is < 0 or > 5) errors.Add($"tiers: tier {row.Tier} is not 0 to 5");
            if (row.MaxLevel < 1) errors.Add($"tiers: tier {row.Tier} maxLevel {row.MaxLevel} is below 1");
            if (row.Renown < 0 || row.Reputation < 0 || row.Gold < 0) errors.Add($"tiers: tier {row.Tier} pays a negative amount");
            if (row.Gold > 60000) errors.Add($"tiers: tier {row.Tier} gold {row.Gold} is over 60000");
        }

        if (t.Great == null) errors.Add("great: missing");
        else
        {
            if (t.Great.CountMultiplier is < 1 or > 20) errors.Add($"great.countMultiplier {t.Great.CountMultiplier} is not 1 to 20");
            if (t.Great.PayMultiplier is < 1 or > 20) errors.Add($"great.payMultiplier {t.Great.PayMultiplier} is not 1 to 20");
            if (t.Great.LevelBonus < 0) errors.Add("great.levelBonus is negative");
            if (!string.IsNullOrEmpty(t.Great.LootBox) && !Enum.TryParse<LeagueShop.BoxTier>(t.Great.LootBox, true, out _))
                errors.Add($"great.lootBox \"{t.Great.LootBox}\" is not Bronze, Silver, Gold, Platinum or empty");
        }

        var i = 0;
        foreach (var f in t.Families ?? new List<LeagueHuntFamily>())
        {
            i++;
            f.Entries.Clear();
            if (string.IsNullOrWhiteSpace(f.Key) || string.IsNullOrWhiteSpace(f.Name)) errors.Add($"families[{i}]: a key and a name are needed");
            if (!Uses.Contains((f.Use ?? "").ToLowerInvariant())) errors.Add($"families[{i}] {f.Key}: use \"{f.Use}\" is not cull, hunt or boss");
            if (f.Types == null || f.Types.Count == 0) errors.Add($"families[{i}] {f.Key}: no types");
            foreach (var name in f.Types ?? new List<string>())
            {
                var entry = ClusterFLeagueBestiary.Entries.FirstOrDefault(e => e.Type.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (entry == null) errors.Add($"families[{i}] {f.Key}: \"{name}\" is not a League Bestiary creature");
                else f.Entries.Add(entry);
            }

            if (!string.IsNullOrEmpty(f.Slayer) && !(Enum.TryParse<SlayerName>(f.Slayer, true, out var s) && s != SlayerName.None))
                errors.Add($"families[{i}] {f.Key}: slayer \"{f.Slayer}\" is not a slayer family");
        }

        if (t.Families != null && t.Families.Select(f => f.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != t.Families.Count)
            errors.Add("families: a key appears twice");
    }

    /// <summary>At start: read the file into force, or write the defaults when there is none. Returns console lines.</summary>
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

        lines.AddRange(Reload(path));
        return lines;
    }

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
            lines.Add($"could not read {path}: {ex.Message}; the table in force is kept ({Current.Source})");
            return lines;
        }

        var errors = new List<string>();
        var table = Parse(json, path, errors);
        if (table == null)
        {
            lines.Add($"{path} refused; the table in force is kept ({Current.Source}):");
            lines.AddRange(errors.Select(e => "  " + e));
            return lines;
        }

        Current = table;
        lines.Add($"league hunts table in force: {path} ({table.Families.Count} families, {table.DailyHunts} a day, great hunt resets {table.ResetDay})");
        return lines;
    }
}
