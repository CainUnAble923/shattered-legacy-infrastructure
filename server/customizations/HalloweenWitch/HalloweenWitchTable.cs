// cc-P786: every number of the Halloween witch, in one table Chase tunes: Configuration/halloween-witch.json, written
// with the defaults on first start if missing, read at start and by [HalloweenWitch reload. A bad file is refused whole,
// every error is printed, and the table in force is kept (the GoldPanningTable pattern). Notes in shard-migration
// notes/cc-P786-halloween-witch.md.
//
//   enabled             false: no witch is placed at the wild spots (those out are taken away at the next check) and
//                       the encounter is not added
//   name, title, hue    what she is called and her skin hue
//   hitsMin/Max, strMin/Max, dexMin/Max, intMin/Max, damageMin/Max, virtualArmor
//   resistMin, resistMax  every resist
//   magery, evalInt, meditation, magicResist, tactics, wrestling   her skills (each exactly)
//   fame, karma
//   goldMin, goldMax    the gold in her pack
//   dropChancePercent   the chance her corpse holds one prize (100: always)
//   drops               the prizes, by weight: { type, weight }. Decor and toys only: a type that is a scroll of power,
//                       stat or skill, a weapon, armour, jewellery, clothing, a talisman, a container, or a deed that is
//                       not a house addon deed is refused
//   tokens              Festival Tokens to her top damager (in a festival season only, FestivalTokens' rule)
//   spots               the wild spots she stands at in the Halloween window: { name, map, x, y, z }
//   wanderRange         how far she strays from her spot
//   respawnMinutes      how long after her death a spot gets a new witch
//   checkMinutes        how often the spots are checked (also on every world load)
//   encounter           the random encounter (cc-P776) that brings one to a traveling player at night in the window:
//                       { enabled, weight, minLevel, facets, systemLine, openingLine }. It joins whatever
//                       encounters.json says (an existing world's file never had it), unless that file already has
//                       an encounter with the id "witch".

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server.Mobiles;

public sealed class WitchDropRow
{
    public string Type { get; set; } = "";
    public int Weight { get; set; } = 1;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class WitchSpot
{
    public string Name { get; set; } = "";
    public string Map { get; set; } = "Trammel";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    [JsonIgnore]
    public Map? Facet => Server.Encounters.EncounterTable.FacetNamed(Map);

    [JsonIgnore]
    public Point3D Location => new(X, Y, Z);
}

public sealed class WitchEncounterRules
{
    public bool Enabled { get; set; } = true;
    public int Weight { get; set; } = 2;
    public int MinLevel { get; set; } = 40;
    public List<string> Facets { get; set; } = [];
    public string SystemLine { get; set; } = "";
    public string OpeningLine { get; set; } = "";
}

public sealed class HalloweenWitchTable
{
    public const string FileName = "halloween-witch.json";
    public const string EncounterId = "witch";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string? About { get; set; }
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public int Hue { get; set; }
    public int HitsMin { get; set; }
    public int HitsMax { get; set; }
    public int StrMin { get; set; }
    public int StrMax { get; set; }
    public int DexMin { get; set; }
    public int DexMax { get; set; }
    public int IntMin { get; set; }
    public int IntMax { get; set; }
    public int DamageMin { get; set; }
    public int DamageMax { get; set; }
    public int VirtualArmor { get; set; }
    public int ResistMin { get; set; }
    public int ResistMax { get; set; }
    public double Magery { get; set; }
    public double EvalInt { get; set; }
    public double Meditation { get; set; }
    public double MagicResist { get; set; }
    public double Tactics { get; set; }
    public double Wrestling { get; set; }
    public int Fame { get; set; }
    public int Karma { get; set; }
    public int GoldMin { get; set; }
    public int GoldMax { get; set; }
    public int DropChancePercent { get; set; } = 100;
    public List<WitchDropRow> Drops { get; set; } = [];
    public int Tokens { get; set; }
    public List<WitchSpot> Spots { get; set; } = [];
    public int WanderRange { get; set; } = 8;
    public int RespawnMinutes { get; set; } = 30;
    public int CheckMinutes { get; set; } = 5;
    public WitchEncounterRules Encounter { get; set; } = new();

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    [JsonIgnore]
    public int TotalWeight => Drops.Sum(d => Math.Max(0, d.Weight));

    private static HalloweenWitchTable? _defaults;
    private static HalloweenWitchTable? _current;

    public static HalloweenWitchTable Defaults
    {
        get
        {
            if (_defaults == null)
            {
                var t = BuildDefaults();
                Validate(t, new List<string>());
                _defaults = t;
            }

            return _defaults;
        }
    }

    public static HalloweenWitchTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    // -- Defaults -----------------------------------------------------------------------------------

    public static HalloweenWitchTable BuildDefaults() => new()
    {
        About = "The Halloween witch (cc-P786): a field boss at a few wild spots and a night encounter, only while the " +
                "Halloween season is on. Edit and run [HalloweenWitch reload; a bad file is refused whole and the " +
                "table in force is kept (the encounter part takes effect at the next [Encounters reload or restart). " +
                "Her corpse holds one of the drops (decor and toys only) and her top damager gets the tokens. The idea " +
                "is from Milva's Halloween Witch; the code and numbers are ours.",
        Enabled = true,
        Name = "a wicked witch",
        Title = "of the hollow hills",
        Hue = 63, // a green skin, as the source had it
        // About 900 hits and Magery 110, as P779's sketch says; the rest of a stock evil mage lord, scaled up.
        HitsMin = 850,
        HitsMax = 950,
        StrMin = 300,
        StrMax = 350,
        DexMin = 110,
        DexMax = 130,
        IntMin = 500,
        IntMax = 600,
        DamageMin = 15,
        DamageMax = 22,
        VirtualArmor = 50,
        ResistMin = 50,
        ResistMax = 65,
        Magery = 110.0,
        EvalInt = 110.0,
        Meditation = 100.0,
        MagicResist = 110.0,
        Tactics = 100.0,
        Wrestling = 100.0,
        Fame = 15000,
        Karma = -15000,
        GoldMin = 400,
        GoldMax = 700,
        DropChancePercent = 100,
        Drops =
        [
            new() { Type = "WitchOuijaBoard", Weight = 2 },
            new() { Type = "TortureBlockDeed", Weight = 1 },
            new() { Type = "GoreRackDeed", Weight = 1 },
        ],
        Tokens = 5,
        // Three of pinned's Trammel graveyard spawner spots (Data/Spawns/shared/trammel/Graveyards.json), outside the
        // towns' guards: Yew, Cove and Britain. A witch stands near each, not on the gravestones.
        Spots =
        [
            new() { Name = "the Yew graveyard", Map = "Trammel", X = 722, Y = 1119, Z = 0 },
            new() { Name = "the Cove graveyard", Map = "Trammel", X = 2438, Y = 1100, Z = 8 },
            new() { Name = "the Britain graveyard", Map = "Trammel", X = 1369, Y = 1475, Z = 10 },
        ],
        WanderRange = 8,
        RespawnMinutes = 30,
        CheckMinutes = 5,
        Encounter = new WitchEncounterRules
        {
            Enabled = true,
            Weight = 2,
            MinLevel = 40,
            Facets = ["Trammel", "Felucca"],
            SystemLine = "System: Someone is brewing something out here. It smells like trouble and toads.",
            OpeningLine = "A traveler! Just the thing the pot was missing."
        }
    };

    /// <summary>The drop a roll in [0, 1) draws, by weight.</summary>
    public WitchDropRow Draw(double roll)
    {
        var at = Math.Clamp(roll, 0.0, 0.999999) * TotalWeight;
        foreach (var row in Drops)
        {
            var w = Math.Max(0, row.Weight);
            if (at < w)
            {
                return row;
            }

            at -= w;
        }

        return Drops.Last(d => d.Weight > 0);
    }

    // -- The rules ----------------------------------------------------------------------------------

    public static readonly Type[] ForbiddenBases =
    [
        typeof(SpecialScroll), typeof(BaseWeapon), typeof(BaseArmor), typeof(BaseJewel), typeof(BaseClothing),
        typeof(BaseTalisman), typeof(Container)
    ];

    /// <summary>Why a drop type is refused, or null. A deed is allowed only as a house addon (decor) deed.</summary>
    public static string? Forbidden(Type type)
    {
        if (type.Name.Contains("Deed", StringComparison.OrdinalIgnoreCase) && !typeof(BaseAddonDeed).IsAssignableFrom(type))
        {
            return "a deed that is not a house decor deed";
        }

        var bad = ForbiddenBases.FirstOrDefault(b => b.IsAssignableFrom(type));
        return bad == null ? null : $"a {bad.Name}";
    }

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

    public static string ToJson(HalloweenWitchTable table) => JsonSerializer.Serialize(table, Json);

    public static HalloweenWitchTable? Parse(string json, string source, List<string> errors)
    {
        HalloweenWitchTable? table;
        try
        {
            table = JsonSerializer.Deserialize<HalloweenWitchTable>(json, Json);
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

    private static void Range(List<string> errors, string what, int min, int max, int floor, int ceiling)
    {
        if (min < floor || max < min || max > ceiling)
        {
            errors.Add($"{what}Min/{what}Max {min}/{max} must be {floor} <= min <= max <= {ceiling}");
        }
    }

    public static void Validate(HalloweenWitchTable t, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(t.Name)) errors.Add("name is empty");
        Range(errors, "hits", t.HitsMin, t.HitsMax, 1, 100_000);
        Range(errors, "str", t.StrMin, t.StrMax, 1, 5_000);
        Range(errors, "dex", t.DexMin, t.DexMax, 1, 5_000);
        Range(errors, "int", t.IntMin, t.IntMax, 1, 5_000);
        Range(errors, "damage", t.DamageMin, t.DamageMax, 1, 200);
        Range(errors, "resist", t.ResistMin, t.ResistMax, 0, 90);
        Range(errors, "gold", t.GoldMin, t.GoldMax, 0, 60_000);
        if (t.VirtualArmor is < 0 or > 200) errors.Add($"virtualArmor {t.VirtualArmor} is not 0 to 200");

        foreach (var (what, v) in new[]
                 {
                     ("magery", t.Magery), ("evalInt", t.EvalInt), ("meditation", t.Meditation),
                     ("magicResist", t.MagicResist), ("tactics", t.Tactics), ("wrestling", t.Wrestling)
                 })
        {
            if (v is < 0 or > 200) errors.Add($"{what} {v} is not 0 to 200");
        }

        if (t.DropChancePercent is < 0 or > 100) errors.Add($"dropChancePercent {t.DropChancePercent} is not 0 to 100");
        if (t.Tokens is < 0 or > 100) errors.Add($"tokens {t.Tokens} is not 0 to 100");

        foreach (var d in t.Drops ?? [])
        {
            d.ItemType = null;
            var type = AssemblyHandler.FindTypeByName(d.Type ?? "");
            if (type == null || !typeof(Item).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
            {
                errors.Add($"drops: \"{d.Type}\" is not an item type made without arguments");
                continue;
            }

            if (Forbidden(type) is { } why)
            {
                errors.Add($"drops: \"{d.Type}\" is {why}; the witch drops decor and toys only");
                continue;
            }

            if (d.Weight < 0) errors.Add($"drops: {d.Type} weight is negative");
            d.ItemType = type;
        }

        if (t.DropChancePercent > 0 && (t.Drops == null || t.Drops.Sum(d => Math.Max(0, d.Weight)) <= 0))
        {
            errors.Add("drops: a drop chance with no drop that has weight");
        }

        foreach (var s in t.Spots ?? [])
        {
            if (s.Facet == null) errors.Add($"spots: {s.Name}: \"{s.Map}\" is not a facet");
            if (s.X < 0 || s.Y < 0 || s.Z is < -128 or > 127) errors.Add($"spots: {s.Name}: {s.X},{s.Y},{s.Z} is not a place");
        }

        if (t.WanderRange is < 0 or > 30) errors.Add($"wanderRange {t.WanderRange} is not 0 to 30");
        if (t.RespawnMinutes is < 1 or > 10_080) errors.Add($"respawnMinutes {t.RespawnMinutes} is not 1 to 10080");
        if (t.CheckMinutes is < 1 or > 60) errors.Add($"checkMinutes {t.CheckMinutes} is not 1 to 60");

        if (t.Encounter == null)
        {
            errors.Add("encounter: missing");
        }
        else if (t.Encounter.Weight < 0 || t.Encounter.MinLevel < 1)
        {
            errors.Add("encounter: weight must be 0 or more and minLevel 1 or more");
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
        lines.Add($"halloween witch table in force: {path} ({(table.Enabled ? "on" : "off")}, {table.Spots.Count} spots, " +
                  $"{table.Drops.Count} drops, {table.Tokens} tokens)");
        return lines;
    }
}
