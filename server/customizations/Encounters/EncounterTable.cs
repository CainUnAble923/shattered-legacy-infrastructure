// cc-P776: random encounters, the config file. Notes in shard-migration notes/cc-P776-random-encounters.md; the idea and
// the never-do list are P765's assessment (notes/cc-P765-random-encounters-assessment.md).
//
// Every number lives in Configuration/encounters.json, written with these defaults on a first start, read into force
// at start and by [Encounters reload. A file that does not parse, or fails a check, is refused whole and the table in
// force is kept (the requisitions table's rule). "enabled": false is the off switch: the roll stops, nothing new spawns;
// encounters already out finish and leave as usual.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Encounters;

/// <summary>What an encounter does once its creatures are out. Each kind is one scripted behaviour in ClusterFEncounters.</summary>
public enum EncounterKind
{
    Toll,
    Surveyor,
    Procession,

    /// <summary>cc-P786: one strong creature that comes for the player (the Halloween witch). It pays its own reward.</summary>
    Boss
}

/// <summary>When an encounter can happen, by the hour at the player's spot (Clock.GetTime).</summary>
public enum EncounterTime
{
    Any,
    Day,
    Night
}

/// <summary>One group of creatures: Min to Max of them, each a random pick from Types (stock type names).</summary>
public sealed class EncounterGroup
{
    public List<string> Types { get; set; } = [];
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 1;
}

/// <summary>One encounter in the list.</summary>
public sealed class EncounterDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EncounterKind Kind { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Relative weight among the encounters a player qualifies for at that moment.</summary>
    public int Weight { get; set; } = 1;

    /// <summary>Facet names this encounter can happen on; empty means every facet in the table's Facets.</summary>
    public List<string> Facets { get; set; } = [];

    public EncounterTime Time { get; set; } = EncounterTime.Any;

    /// <summary>The player's level (ClusterFCreatureLevels.PlayerLevel, the skill-total stand-in until F-12).</summary>
    public int MinLevel { get; set; } = 1;

    public int MaxLevel { get; set; } = 200;

    /// <summary>Only for characters who have joined the League.</summary>
    public bool LeagueOnly { get; set; }

    /// <summary>A season switch's name ("halloween", "winter", "spring"): only while that season is on. Null: any time of year.</summary>
    public string? Season { get; set; }

    /// <summary>The hostile (or, for a procession, wandering) creatures.</summary>
    public List<EncounterGroup> Creatures { get; set; } = [];

    /// <summary>The System's line to the player when it starts.</summary>
    public string SystemLine { get; set; } = "";

    /// <summary>What the leader (or the surveyor) says when it starts.</summary>
    public string OpeningLine { get; set; } = "";

    /// <summary>A name and title the encounter's creatures wear instead of their own, and a hue; empty keeps the stock one.</summary>
    public string CreatureName { get; set; } = "";

    public string LeaderName { get; set; } = "";
    public string LeaderTitle { get; set; } = "";
    public int CreatureHue { get; set; }

    // -- Toll --

    /// <summary>The toll: this percent of the gold in the backpack, at most TollCap.</summary>
    public int TollPercent { get; set; } = 5;

    public int TollCap { get; set; } = 200;

    /// <summary>Seconds the player has to say "pay" before the brigands turn on them.</summary>
    public int TollWaitSeconds { get; set; } = 30;

    // -- The reward a kind gives, from the existing systems --

    /// <summary>Toll: the loot box's source key (once per loop, ClusterFLootBoxes.Award) and its grade. Empty: no box.</summary>
    public string BoxSource { get; set; } = "";

    public int BoxGrade { get; set; } = 2;

    /// <summary>Procession: Festival Tokens the leader's top damager gets (FestivalTokens.Grant).</summary>
    public int LeaderTokens { get; set; }
}

public sealed class EncounterTable
{
    public const string FileName = "encounters.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string About { get; set; } = "";

    /// <summary>The off switch. False: no roll, nothing new spawns.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the roll runs, in seconds.</summary>
    public int TickSeconds { get; set; } = 60;

    /// <summary>The chance, per eligible traveling player per tick, that an encounter starts (0 to 1).</summary>
    public double Chance { get; set; } = 0.05;

    /// <summary>Minutes before the same character, or any character on the same account, can have another.</summary>
    public int CooldownMinutes { get; set; } = 25;

    /// <summary>Tiles a player must have moved since the last tick to count as traveling (and so not idle).</summary>
    public int MinTravelTiles { get; set; } = 12;

    /// <summary>Seconds a move counts as recent travel.</summary>
    public int TravelWindowSeconds { get; set; } = 120;

    /// <summary>Where the creatures appear: this many tiles from the player, out of the player's line of sight when a spot allows.</summary>
    public int SpawnDistanceMin { get; set; } = 9;

    public int SpawnDistanceMax { get; set; } = 15;
    public int SpawnTries { get; set; } = 24;

    /// <summary>No new encounter starts within this many tiles of one already out (a group of ten gets one, not ten).</summary>
    public int CrowdRange { get; set; } = 24;

    /// <summary>Minutes after which an encounter's creatures leave, any that are not fighting.</summary>
    public int LeaveAfterMinutes { get; set; } = 8;

    /// <summary>How often the cleanup sweep runs, in seconds.</summary>
    public int SweepSeconds { get; set; } = 10;

    /// <summary>The hour (0-23, local to the spot) day starts and night starts.</summary>
    public int DayStartHour { get; set; } = 6;

    public int NightStartHour { get; set; } = 20;

    /// <summary>Facets encounters can happen on. The Fold (the Wakening Isle) and its copies are refused whatever this says.</summary>
    public List<string> Facets { get; set; } = [];

    /// <summary>Regions (and everything under them) where no encounter starts and no encounter creature may stand.</summary>
    public List<string> DenyRegions { get; set; } = [];

    public List<EncounterDef> Encounters { get; set; } = [];

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    private static EncounterTable? _current;

    public static EncounterTable Defaults => BuildDefaults();

    public static EncounterTable Current
    {
        get => _current ??= Supplemented(Defaults);
        set => _current = Supplemented(value);
    }

    /// <summary>
    /// cc-P786: other systems' encounters, added to every table as it comes into force (a system with its own config adds
    /// its encounter here, so an encounters.json written before that system existed still gets it). A handler adds
    /// nothing when the table already has an encounter with its id.
    /// </summary>
    public static event Action<EncounterTable>? Supplement;

    private static EncounterTable Supplemented(EncounterTable table)
    {
        Supplement?.Invoke(table);
        return table;
    }

    /// <summary>A facet by its name (Trammel, Felucca, ...), or null; never Internal.</summary>
    public static Map? FacetNamed(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Map.AllMaps.FirstOrDefault(m => m != null && m != Map.Internal && string.Equals(m.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public EncounterDef? Find(string id) =>
        Encounters.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    // -- The defaults: the three starters from P765's sketch --

    public static EncounterTable BuildDefaults() => new()
    {
        About = "cc-P776 random encounters. enabled false turns them off ([Encounters reload after an edit). Chance is per " +
                "eligible traveling player per tick. Creature types are stock types from the spawn data; loot is their own, " +
                "plus the loot box or tokens named here.",
        Facets = ["Trammel", "Felucca"],
        DenyRegions = ["Haven Island", "New Haven", "Haven", "the Rootdeep Burrows", "the Vault", "Jail", "Green Acres"],
        Encounters =
        [
            new EncounterDef
            {
                Id = "toll",
                Name = "Toll Road",
                Kind = EncounterKind.Toll,
                Weight = 3,
                Time = EncounterTime.Day,
                MinLevel = 15,
                Creatures = [new EncounterGroup { Types = ["Brigand"], Min = 2, Max = 4 }],
                LeaderTitle = "the toll keeper",
                SystemLine = "System: An unlicensed toll booth has opened on this road. Your options are compliance or a sharply worded reply.",
                OpeningLine = "Road tax, traveler. Say 'pay' and walk on lighter. Or don't.",
                BoxSource = "encounter:toll",
                BoxGrade = 2
            },
            new EncounterDef
            {
                Id = "surveyor",
                Name = "Lost Surveyor",
                Kind = EncounterKind.Surveyor,
                Weight = 2,
                LeagueOnly = true,
                MinLevel = 10,
                Creatures = [new EncounterGroup { Types = ["Orc", "Ratman", "GreyWolf", "TimberWolf"], Min = 2, Max = 3 }],
                LeaderName = "Odile Marchant",
                LeaderTitle = "the League surveyor",
                SystemLine = "System: A League surveyor has located a hostile ecosystem by standing in it.",
                OpeningLine = "Over here! Anyone with a League seal and a weapon!"
            },
            new EncounterDef
            {
                Id = "procession",
                Name = "The Lantern Procession",
                Kind = EncounterKind.Procession,
                Weight = 2,
                Facets = ["Trammel"],
                Time = EncounterTime.Night,
                Season = "halloween",
                Creatures = [new EncounterGroup { Types = ["Zombie"], Min = 3, Max = 5 }],
                CreatureName = "a lantern walker",
                LeaderName = "the lantern bearer",
                CreatureHue = 0x2B,
                SystemLine = "System: A procession is passing through. Attendance is optional; interference is noted.",
                LeaderTokens = 1
            }
        ]
    };

    // -- The file --

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string ToJson(EncounterTable table) => JsonSerializer.Serialize(table, Json);

    /// <summary>Reads a table from JSON, or null with the errors: a table is accepted whole or not at all.</summary>
    public static EncounterTable? Parse(string json, string source, List<string> errors)
    {
        EncounterTable? table;
        try
        {
            table = JsonSerializer.Deserialize<EncounterTable>(json, Json);
        }
        catch (Exception ex)
        {
            errors.Add($"not valid: {ex.Message}");
            return null;
        }

        if (table == null)
        {
            errors.Add("empty");
            return null;
        }

        errors.AddRange(table.Validate());
        if (errors.Count > 0)
        {
            return null;
        }

        table.Source = source;
        return table;
    }

    /// <summary>The checks: sane numbers, unique ids, known facets, creature types that exist and are creatures.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (TickSeconds < 5)
        {
            errors.Add($"tickSeconds {TickSeconds} is under 5");
        }

        if (Chance is < 0 or > 1)
        {
            errors.Add($"chance {Chance} is not 0 to 1");
        }

        if (CooldownMinutes < 1)
        {
            errors.Add($"cooldownMinutes {CooldownMinutes} is under 1");
        }

        if (SpawnDistanceMin < 2 || SpawnDistanceMax < SpawnDistanceMin || SpawnDistanceMax > 30)
        {
            errors.Add($"spawnDistanceMin/Max {SpawnDistanceMin}/{SpawnDistanceMax} must be 2 <= min <= max <= 30");
        }

        if (SpawnTries is < 1 or > 200)
        {
            errors.Add($"spawnTries {SpawnTries} is not 1 to 200");
        }

        if (LeaveAfterMinutes < 1 || SweepSeconds < 1)
        {
            errors.Add("leaveAfterMinutes and sweepSeconds must be at least 1");
        }

        if (DayStartHour is < 0 or > 23 || NightStartHour is < 0 or > 23 || DayStartHour == NightStartHour)
        {
            errors.Add($"dayStartHour/nightStartHour {DayStartHour}/{NightStartHour} must be two different hours 0-23");
        }

        foreach (var facet in Facets)
        {
            if (FacetNamed(facet) == null)
            {
                errors.Add($"facets: '{facet}' is not a facet");
            }
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in Encounters)
        {
            var at = $"encounter '{e.Id}'";
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id))
            {
                errors.Add($"{at}: id is empty or repeated");
            }

            if (e.Weight < 0 || e.MinLevel < 1 || e.MaxLevel < e.MinLevel)
            {
                errors.Add($"{at}: weight, minLevel or maxLevel out of range");
            }

            if (e.Creatures.Count == 0)
            {
                errors.Add($"{at}: no creatures");
            }

            foreach (var g in e.Creatures)
            {
                if (g.Min < 1 || g.Max < g.Min || g.Max > 8)
                {
                    errors.Add($"{at}: a group's min/max {g.Min}/{g.Max} must be 1 <= min <= max <= 8");
                }

                if (g.Types.Count == 0)
                {
                    errors.Add($"{at}: a group has no types");
                }

                foreach (var name in g.Types)
                {
                    var type = AssemblyHandler.FindTypeByName(name);
                    if (type == null || !typeof(Mobiles.BaseCreature).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        errors.Add($"{at}: '{name}' is not a creature type");
                    }
                }
            }

            foreach (var facet in e.Facets)
            {
                if (FacetNamed(facet) == null)
                {
                    errors.Add($"{at}: facet '{facet}' is not a facet");
                }
            }

            if (e.TollPercent is < 0 or > 100 || e.TollCap < 0 || e.TollWaitSeconds < 1)
            {
                errors.Add($"{at}: tollPercent, tollCap or tollWaitSeconds out of range");
            }

            if (e.BoxGrade is < 1 or > 5 || e.LeaderTokens < 0)
            {
                errors.Add($"{at}: boxGrade must be 1 to 5 and leaderTokens at least 0");
            }
        }

        return errors;
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
        lines.Add($"encounters table in force: {path} ({(table.Enabled ? "on" : "OFF")}, {table.Encounters.Count} encounters, " +
                  $"chance {table.Chance:0.###} per {table.TickSeconds}s, cooldown {table.CooldownMinutes} min)");
        return lines;
    }
}
