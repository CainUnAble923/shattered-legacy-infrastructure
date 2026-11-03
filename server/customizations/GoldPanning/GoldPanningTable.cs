// cc-P781: every number of gold panning, in one table Chase tunes: Configuration/gold-panning.json, written with the
// defaults on first start if missing, read at start and by [GoldPanningStaff reload. A bad file is refused whole, every
// error is printed, and the table in force is kept (the RequisitionTable pattern). Notes in shard-migration
// notes/cc-P781-gold-panning.md.
//
//   enabled            false: the pan does nothing (it says so) and the assayer buys nothing new
//   minSkill           Mining base needed to pan at all
//   successMinSkill,   the Mining check each attempt makes (pinned's CheckSkill: no chance at the first, certain at the
//   successMaxSkill    second); a failed check finds only silt
//   sizes              small, medium and large nuggets: the Mining base each needs, its weight among the sizes the
//                      panner may find, the assayer's price in gold, and the Compact standing it is worth instead
//   gems               the ML gems found now and then: the chance in percent per successful pan (shared evenly), the
//                      Mining base needed, and the type names
//   bank               the water's yield, as stock Mining's veins: one bank per bankWidth x bankHeight tiles, holding
//                      minTotal to maxTotal finds, refilled minRespawnMinutes to maxRespawnMinutes after it runs dry
//   maxRange           tiles from the panner to the water
//   allowMounted       false: off the horse first
//   landWaterTiles,    the tiles that are water (ranges "0xA8-0xAB" or single ids): land tiles and statics
//   staticWaterTiles
//   enclosureRange,    "shallow river and lake water only, no ocean": the target water must have dry land within
//   enclosureDirections enclosureRange tiles in at least enclosureDirections (6) of the 8 compass directions. A lake
//                      reaches land all round, a river on both banks and the diagonals; a coast only on its land
//                      side, with open sea beyond, so it does not pass.
//   pan                the prospector's pan: the assayer's price and its uses
//   assayer            the Compact assayer: placed beside each Miners' Compact Liaison in the world, if none stands
//                      within seedRange of it

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Engines.GoldPanning;

public sealed class NuggetSizeRow
{
    public string Size { get; set; } = "";
    public double ReqSkill { get; set; }
    public int Weight { get; set; }
    public int Gold { get; set; }
    public int Standing { get; set; }
}

public sealed class PanGemRules
{
    public double ChancePercent { get; set; } = 2.0;
    public double ReqSkill { get; set; } = 60.0;
    public List<string> Types { get; set; } = [];
}

public sealed class PanBankRules
{
    public int BankWidth { get; set; } = 8;
    public int BankHeight { get; set; } = 8;
    public int MinTotal { get; set; } = 5;
    public int MaxTotal { get; set; } = 10;
    public double MinRespawnMinutes { get; set; } = 10;
    public double MaxRespawnMinutes { get; set; } = 20;
}

public sealed class PanToolRules
{
    public int Price { get; set; } = 300;
    public int Uses { get; set; } = 50;
}

public sealed class AssayerRules
{
    public int SeedRange { get; set; } = 8;
}

public sealed class GoldPanningTable
{
    public const string FileName = "gold-panning.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static readonly string[] SizeNames = ["small", "medium", "large"];

    public string About { get; set; }
    public bool Enabled { get; set; } = true;
    public double MinSkill { get; set; } = 60.0;
    public double SuccessMinSkill { get; set; } = 40.0;
    public double SuccessMaxSkill { get; set; } = 110.0;
    public List<NuggetSizeRow> Sizes { get; set; } = [];
    public PanGemRules Gems { get; set; } = new();
    public PanBankRules Bank { get; set; } = new();
    public int MaxRange { get; set; } = 2;
    public bool AllowMounted { get; set; }
    public List<string> LandWaterTiles { get; set; } = [];
    public List<string> StaticWaterTiles { get; set; } = [];
    public int EnclosureRange { get; set; } = 24;
    public int EnclosureDirections { get; set; } = 6;
    public PanToolRules Pan { get; set; } = new();
    public AssayerRules Assayer { get; set; } = new();

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    /// <summary>The parsed tile ranges (filled by validation), as pinned's HarvestDefinition wants them: pairs, low then high.</summary>
    [JsonIgnore]
    public int[] LandRanges { get; private set; } = [];

    [JsonIgnore]
    public int[] StaticRanges { get; private set; } = [];

    /// <summary>The gem types (filled by validation).</summary>
    [JsonIgnore]
    public List<Type> GemTypes { get; } = [];

    private static GoldPanningTable _defaults;
    private static GoldPanningTable _current;

    public static GoldPanningTable Defaults
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

    public static GoldPanningTable Current
    {
        get => _current ??= Defaults;
        set
        {
            _current = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when a new table comes into force (the harvest definition is rebuilt from it).</summary>
    public static event Action Changed;

    public NuggetSizeRow Size(string name) =>
        Sizes.FirstOrDefault(s => s.Size.Equals(name ?? "", StringComparison.OrdinalIgnoreCase));

    public static bool InRanges(int[] ranges, int id)
    {
        for (var i = 0; i + 1 < ranges.Length; i += 2)
            if (id >= ranges[i] && id <= ranges[i + 1])
                return true;
        return false;
    }

    // -- Defaults -----------------------------------------------------------------------------------

    public static GoldPanningTable BuildDefaults() => new()
    {
        About = "Gold panning (cc-P781). Edit and run [GoldPanningStaff reload; a bad file is refused whole and the table in " +
                "force is kept. Sizes: the Mining base each nugget needs, its weight among the sizes open to the panner, " +
                "the assayer's gold price and the Compact standing it is worth instead. Water: the tiles that are water, " +
                "and the enclosure rule that keeps panning to rivers and lakes (dry land within enclosureRange tiles in " +
                "enclosureDirections of the 8 directions). The idea is from manwitch's Gold Panning (by Hammerhand and " +
                "Milva); the code and numbers are ours.",
        Enabled = true,
        MinSkill = 60.0,
        SuccessMinSkill = 40.0,
        SuccessMaxSkill = 110.0,
        // A small nugget is about a stack of iron ingots' worth at a smith; the source's trader paid about 33 gold.
        Sizes =
        [
            new() { Size = "small", ReqSkill = 60.0, Weight = 80, Gold = 25, Standing = 2 },
            new() { Size = "medium", ReqSkill = 75.0, Weight = 17, Gold = 100, Standing = 8 },
            new() { Size = "large", ReqSkill = 90.0, Weight = 3, Gold = 350, Standing = 25 },
        ],
        Gems = new PanGemRules
        {
            ChancePercent = 2.0,
            ReqSkill = 60.0,
            Types = ["BlueDiamond", "BrilliantAmber", "DarkSapphire", "EcruCitrine", "FireRuby", "PerfectEmerald", "Turquoise", "WhitePearl"],
        },
        Bank = new PanBankRules { BankWidth = 8, BankHeight = 8, MinTotal = 5, MaxTotal = 10, MinRespawnMinutes = 10, MaxRespawnMinutes = 20 },
        MaxRange = 2,
        AllowMounted = false,
        // Pinned Fishing's water: the land water tiles, and the static water and its animated runs (Fishing.cs).
        LandWaterTiles = ["0xA8-0xAB", "0x136-0x137"],
        StaticWaterTiles = ["0x1797-0x179C", "0x346E-0x3485", "0x3490-0x34AB", "0x34B5-0x34D5"],
        EnclosureRange = 24,
        // A lake reaches land in all 8 directions, a river in about 6 (open water up and down stream), a coast in
        // about 3 (open sea beyond it): 6 takes rivers and lakes and leaves the coast.
        EnclosureDirections = 6,
        Pan = new PanToolRules { Price = 300, Uses = 50 },
        Assayer = new AssayerRules { SeedRange = 8 },
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

    public static string ToJson(GoldPanningTable table) => JsonSerializer.Serialize(table, Json);

    public static GoldPanningTable Parse(string json, string source, List<string> errors)
    {
        GoldPanningTable table;
        try
        {
            table = JsonSerializer.Deserialize<GoldPanningTable>(json, Json);
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

    public static bool TryParseRange(string text, out int low, out int high)
    {
        low = high = 0;
        var parts = (text ?? "").Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 || !TryParseId(parts[0], out low))
            return false;
        high = low;
        return parts.Length == 1 || TryParseId(parts[1], out high) && high >= low;
    }

    private static bool TryParseId(string s, out int id) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

    private static int[] Ranges(List<string> rows, string name, List<string> errors)
    {
        var list = new List<int>();
        foreach (var row in rows ?? [])
        {
            if (TryParseRange(row, out var lo, out var hi) && lo >= 0 && hi <= 0xFFFF)
            {
                list.Add(lo);
                list.Add(hi);
            }
            else
            {
                errors.Add($"{name}: \"{row}\" is not a tile id or a range like 0x1797-0x179C");
            }
        }

        return list.ToArray();
    }

    public static void Validate(GoldPanningTable t, List<string> errors)
    {
        if (t.MinSkill is < 0 or > 120) errors.Add($"minSkill {t.MinSkill} is not 0 to 120");
        if (t.SuccessMinSkill < 0 || t.SuccessMaxSkill <= t.SuccessMinSkill) errors.Add("successMinSkill must be 0 or more and below successMaxSkill");

        if (t.Sizes == null || t.Sizes.Count == 0) errors.Add("sizes: none");
        foreach (var name in SizeNames)
            if (t.Sizes?.Count(s => s.Size.Equals(name, StringComparison.OrdinalIgnoreCase)) != 1)
                errors.Add($"sizes: needs exactly one \"{name}\" row");
        foreach (var s in t.Sizes ?? [])
        {
            if (!SizeNames.Contains(s.Size?.ToLowerInvariant())) errors.Add($"sizes: \"{s.Size}\" is not small, medium or large");
            if (s.ReqSkill < t.MinSkill || s.ReqSkill > 120) errors.Add($"sizes: {s.Size} reqSkill {s.ReqSkill} is not minSkill to 120");
            if (s.Weight < 0) errors.Add($"sizes: {s.Size} weight is negative");
            if (s.Gold is < 0 or > 60000 || s.Standing is < 0 or > 10000) errors.Add($"sizes: {s.Size} gold is not 0 to 60000 or standing not 0 to 10000");
        }

        if (t.Sizes != null && t.Sizes.Where(s => s.ReqSkill <= t.MinSkill).Sum(s => s.Weight) <= 0)
            errors.Add("sizes: no size with weight is open at minSkill");

        t.GemTypes.Clear();
        if (t.Gems == null) errors.Add("gems: missing");
        else
        {
            if (t.Gems.ChancePercent is < 0 or > 100) errors.Add($"gems.chancePercent {t.Gems.ChancePercent} is not 0 to 100");
            foreach (var name in t.Gems.Types ?? [])
            {
                var type = AssemblyHandler.FindTypeByName(name);
                if (type == null || !typeof(Item).IsAssignableFrom(type)) errors.Add($"gems.types: \"{name}\" is not an item type");
                else t.GemTypes.Add(type);
            }

            if (t.Gems.ChancePercent > 0 && t.GemTypes.Count == 0) errors.Add("gems: a chance with no types");
        }

        if (t.Bank == null) errors.Add("bank: missing");
        else
        {
            if (t.Bank.BankWidth < 1 || t.Bank.BankHeight < 1) errors.Add("bank: width and height must be 1 or more");
            if (t.Bank.MinTotal < 1 || t.Bank.MaxTotal < t.Bank.MinTotal) errors.Add("bank: minTotal must be 1 or more and maxTotal at least minTotal");
            if (t.Bank.MinRespawnMinutes < 0 || t.Bank.MaxRespawnMinutes < t.Bank.MinRespawnMinutes) errors.Add("bank: the respawn minutes are out of order");
        }

        if (t.MaxRange is < 1 or > 6) errors.Add($"maxRange {t.MaxRange} is not 1 to 6");
        t.LandRanges = Ranges(t.LandWaterTiles, "landWaterTiles", errors);
        t.StaticRanges = Ranges(t.StaticWaterTiles, "staticWaterTiles", errors);
        if (t.LandRanges.Length + t.StaticRanges.Length == 0) errors.Add("no water tiles");
        if (t.EnclosureRange is < 1 or > 64) errors.Add($"enclosureRange {t.EnclosureRange} is not 1 to 64");
        if (t.EnclosureDirections is < 0 or > 8) errors.Add($"enclosureDirections {t.EnclosureDirections} is not 0 to 8");

        if (t.Pan == null) errors.Add("pan: missing");
        else if (t.Pan.Price is < 1 or > 60000 || t.Pan.Uses < 1) errors.Add("pan: price must be 1 to 60000 and uses 1 or more");

        if (t.Assayer == null) errors.Add("assayer: missing");
        else if (t.Assayer.SeedRange is < 1 or > 30) errors.Add($"assayer.seedRange {t.Assayer.SeedRange} is not 1 to 30");
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
        lines.Add($"gold panning table in force: {path} (Mining {table.MinSkill}+, {table.GemTypes.Count} gems at {table.Gems.ChancePercent}%)");
        return lines;
    }
}
