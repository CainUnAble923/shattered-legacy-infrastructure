// cc-P782: every number of grave digging, in one table Chase tunes: Configuration/grave-digging.json, written with the
// defaults on first start if missing, read at start and by [GraveDiggingStaff reload. A bad file is refused whole, every
// error is printed, and the table in force is kept (the RequisitionTable pattern). Notes in shard-migration
// notes/cc-P782-grave-digging.md.
//
//   enabled            false: the spade does nothing (it says so)
//   minSkill           Forensics base needed to dig at all (0: anyone with a spade)
//   successMinSkill,   the Forensics check each dig makes (pinned's CheckSkill: no chance at the first, certain at the
//   successMaxSkill    second); a failed check turns up only earth
//   graveTiles         the grave statics a spade digs (ranges "0xEE0-0xEE2" or single ids); headstones are not dug
//   regionKeywords     a grave counts when its region's name holds one of these (Britain Graveyard, Vesper Graveyard)
//   areas              ...or when it lies in one of these cemetery boxes, on the facets named. They are only checked by
//                      the dig, never registered as regions, so no town's guards or rules change. The defaults are every
//                      cluster of 8 or more graves on the shard's Trammel map (the notes list how they were found);
//                      Felucca shares the map file.
//   node               each grave tile is its own node (pinned HarvestBank, 1 x 1): minTotal to maxTotal digs, shared
//                      by everyone, refilled minRespawnMinutes to maxRespawnMinutes after it is first dug
//   maxRange           tiles from the digger to the grave
//   tiers              what a dig turns up, by Forensics base: the highest tier the digger reaches is drawn from, by
//                      weight; each find gives min to max of its item
//   bonus              a small extra roll on a successful dig: chancePercent, from bonus minSkill, drawn by weight from
//                      entries in the loot boxes' own row format (PoolEntry, LootBoxRoller makes them). An entry's
//                      minGrade and maxGrade are Forensics grades here (gradeSkills: grade 1 from 0, 2 from 50, 3 from
//                      75, 4 from 90). Only plain item types and the specials @TreasureMap, @Decor, @Reagents and @Seed
//                      are allowed: no magic gear, no weapons or armour, nothing blessed (a blessed item is given unblessed).
//   spade              the gravedigger's spade: the tinkers' price and its uses

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.LootBoxes;

namespace Server.Engines.GraveDigging;

public sealed class CemeteryArea
{
    public string Name { get; set; } = "";
    public List<string> Facets { get; set; } = [];
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }

    public bool Contains(Map map, int x, int y) =>
        map != null && x >= X1 && x <= X2 && y >= Y1 && y <= Y2 &&
        Facets.Any(f => f.Equals(map.Name, StringComparison.OrdinalIgnoreCase));
}

public sealed class GraveNodeRules
{
    public int MinTotal { get; set; } = 6;
    public int MaxTotal { get; set; } = 12;
    public double MinRespawnMinutes { get; set; } = 30;
    public double MaxRespawnMinutes { get; set; } = 60;
}

public sealed class GraveFind
{
    public string Item { get; set; } = "";
    public int Weight { get; set; } = 1;
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 1;

    [JsonIgnore]
    public Type Type { get; set; }
}

public sealed class GraveTier
{
    public double MinSkill { get; set; }
    public List<GraveFind> Finds { get; set; } = [];
}

public sealed class GraveBonusRules
{
    public double ChancePercent { get; set; } = 4.0;
    public double MinSkill { get; set; } = 50.0;
    public List<PoolEntry> Entries { get; set; } = [];
}

public sealed class SpadeRules
{
    public int Price { get; set; } = 40;
    public int Uses { get; set; } = 50;
}

public sealed class GraveDiggingTable
{
    public const string FileName = "grave-digging.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    /// <summary>The only specials a bonus entry may name: none of them makes magic gear, a weapon or armour.</summary>
    public static readonly string[] AllowedSpecials = ["@TreasureMap", "@Decor", "@Reagents", "@Seed"];

    public string About { get; set; }
    public bool Enabled { get; set; } = true;
    public double MinSkill { get; set; }
    public double SuccessMinSkill { get; set; } = -25.0;
    public double SuccessMaxSkill { get; set; } = 100.0;
    public List<string> GraveTiles { get; set; } = [];
    public List<string> RegionKeywords { get; set; } = [];
    public List<CemeteryArea> Areas { get; set; } = [];
    public GraveNodeRules Node { get; set; } = new();
    public int MaxRange { get; set; } = 2;
    public List<GraveTier> Tiers { get; set; } = [];
    public GraveBonusRules Bonus { get; set; } = new();
    public List<double> GradeSkills { get; set; } = [0, 50, 75, 90];
    public SpadeRules Spade { get; set; } = new();

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    /// <summary>The parsed grave tile ranges (filled by validation): pairs, low then high, as HarvestDefinition wants them.</summary>
    [JsonIgnore]
    public int[] GraveRanges { get; private set; } = [];

    private static GraveDiggingTable _defaults;
    private static GraveDiggingTable _current;

    public static GraveDiggingTable Defaults
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

    public static GraveDiggingTable Current
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

    /// <summary>The tier a Forensics base reaches: the highest whose minSkill it meets, or null.</summary>
    public GraveTier TierFor(double forensics)
    {
        GraveTier best = null;
        foreach (var t in Tiers)
            if (forensics >= t.MinSkill && (best == null || t.MinSkill > best.MinSkill))
                best = t;
        return best;
    }

    /// <summary>The Forensics grade, 1 to GradeSkills.Count, that bonus entries' minGrade and maxGrade read.</summary>
    public int GradeFor(double forensics)
    {
        var grade = 1;
        for (var i = 0; i < GradeSkills.Count; i++)
            if (forensics >= GradeSkills[i])
                grade = i + 1;
        return grade;
    }

    // -- Defaults -----------------------------------------------------------------------------------

    private static readonly List<string> Facets = ["Trammel", "Felucca"];

    private static CemeteryArea A(string name, int x1, int y1, int x2, int y2) =>
        new() { Name = name, Facets = Facets.ToList(), X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };

    private static GraveFind F(string item, int weight, int min, int max) => new() { Item = item, Weight = weight, Min = min, Max = max };

    private static PoolEntry P(string item, int weight, int minGrade = 1, int arg = 0, int itemId = 0, string name = null) =>
        new() { Item = item, Weight = weight, MinGrade = minGrade, MaxGrade = 6, Arg = arg, ItemId = itemId, Name = name };

    public static GraveDiggingTable BuildDefaults() => new()
    {
        About = "Grave digging (cc-P782). Edit and run [GraveDiggingStaff reload; a bad file is refused whole and the table in " +
                "force is kept. A spade digs graveTiles inside a region whose name holds a regionKeyword or inside an area; " +
                "each grave tile is a node of node.minTotal to maxTotal digs. Tiers: what a dig finds by Forensics base. " +
                "Bonus: entries in the loot boxes' row format; minGrade is the Forensics grade (gradeSkills). The idea is " +
                "from Massapequa's Grave Digging; the code and numbers are ours.",
        Enabled = true,
        MinSkill = 0,
        SuccessMinSkill = -25.0,
        SuccessMaxSkill = 100.0,
        GraveTiles = ["0xED3", "0xEDF", "0xEE0-0xEE2", "0xEE8"],
        RegionKeywords = ["Graveyard", "Cemetery"],
        // Every cluster of 8 or more graves (graveTiles within 8 tiles of each other) on the shard's Trammel map, the box
        // around it padded by 3 (notes/cc-P782-grave-digging.md). Named after the nearest place in Locations/trammel.json.
        Areas =
        [
            A("the Chapel of Saint Lucia, Trinsic jungle", 1651, 3144, 1674, 3179),
            A("Jhelom cemetery", 1278, 3721, 1301, 3753),
            A("Cove graveyard", 2423, 1098, 2450, 1112),
            A("Moonglow cemetery", 4527, 1314, 4554, 1328),
            A("Jhelom cemetery, north", 1271, 3710, 1289, 3728),
            A("Britain forest, south-east", 1820, 2410, 1838, 2428),
            A("graves south of Skara Brae", 833, 2453, 861, 2479),
            A("graveyard at 2572,3094", 2572, 3094, 2603, 3114),
            A("Vesper graveyard", 2756, 870, 2784, 896),
            A("graveyard at 2600,3116", 2600, 3116, 2618, 3134),
            A("graveyard at 2625,3101", 2625, 3101, 2633, 3128),
            A("Yew cemetery", 716, 1102, 730, 1114),
            A("the Dhampire ruins, Britain forest", 1466, 2501, 1479, 2515),
            A("graveyard at 2599,3148", 2599, 3148, 2622, 3160),
            A("graves at 375,197", 375, 197, 409, 231),
            A("graveyard at 2583,3106", 2583, 3106, 2596, 3117),
            A("graveyard at 2573,3146", 2573, 3146, 2580, 3164),
            A("graveyard at 2625,3131", 2625, 3131, 2633, 3150),
            A("graves east of Cove", 2512, 1145, 2522, 1157),
            A("Yew cemetery, south", 710, 1124, 731, 1136),
            A("graves south of Skara Brae, north", 834, 2438, 846, 2449),
            A("Vesper graveyard, north", 2757, 855, 2769, 866),
            A("graves at 3643,2853", 3643, 2853, 3655, 2863),
            A("graves at 1672,1361", 1672, 1361, 1679, 1373),
        ],
        Node = new GraveNodeRules { MinTotal = 6, MaxTotal = 12, MinRespawnMinutes = 30, MaxRespawnMinutes = 60 },
        MaxRange = 2,
        // The source's tiers: bones under 50; bones and fertile dirt from 50; and the necromancy reagents from 75.
        Tiers =
        [
            new GraveTier { MinSkill = 0, Finds = [F("Bone", 1, 1, 3)] },
            new GraveTier { MinSkill = 50, Finds = [F("Bone", 6, 2, 4), F("FertileDirt", 4, 1, 2)] },
            new GraveTier
            {
                MinSkill = 75,
                Finds =
                [
                    F("Bone", 4, 2, 5), F("FertileDirt", 3, 1, 3), F("GraveDust", 3, 2, 5), F("NoxCrystal", 2, 1, 3),
                    F("BatWing", 2, 1, 3), F("DaemonBlood", 2, 1, 3), F("PigIron", 2, 1, 3),
                ]
            },
        ],
        Bonus = new GraveBonusRules
        {
            ChancePercent = 4.0,
            MinSkill = 50.0,
            Entries =
            [
                P("RibCage", 30),                                    // human remains: cut with scissors for bones
                P("BonePile", 30),
                P("@Decor", 12, 2, itemId: 0x1165, name: "a gravestone"),
                P("@Decor", 8, 2, itemId: 0xED7, name: "a weathered headstone"),
                P("@TreasureMap", 10, 3, arg: 1),                    // a level 1 treasure map, from Forensics 75
                P("WoodenCoffinDeed", 4, 4),                         // grave decor, from Forensics 90
            ]
        },
        GradeSkills = [0, 50, 75, 90],
        Spade = new SpadeRules { Price = 40, Uses = 50 },
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

    public static string ToJson(GraveDiggingTable table) => JsonSerializer.Serialize(table, Json);

    public static GraveDiggingTable Parse(string json, string source, List<string> errors)
    {
        GraveDiggingTable table;
        try
        {
            table = JsonSerializer.Deserialize<GraveDiggingTable>(json, Json);
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

    private static bool TryParseId(string s, out int id) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

    public static bool TryParseRange(string text, out int low, out int high)
    {
        low = high = 0;
        var parts = (text ?? "").Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 || !TryParseId(parts[0], out low))
            return false;
        high = low;
        if (parts.Length == 2 && !TryParseId(parts[1], out high))
            return false;
        return low >= 0 && high >= low && high <= 0xFFFF;
    }

    private static Type ItemType(string name)
    {
        var type = AssemblyHandler.FindTypeByName(name ?? "");
        return type != null && typeof(Item).IsAssignableFrom(type) && !type.IsAbstract ? type : null;
    }

    /// <summary>Why a bonus entry is not allowed here, or null.</summary>
    public static string WhyNotBonus(PoolEntry e)
    {
        if (LootBoxRoller.CheckEntry(e) is { } why)
            return why;

        if (e.Item.StartsWith('@'))
            return AllowedSpecials.Contains(e.Item, StringComparer.OrdinalIgnoreCase)
                ? null
                : $"'{e.Item}' is not allowed here (only {string.Join(", ", AllowedSpecials)}).";

        var type = ItemType(e.Item);
        if (type == null)
            return $"'{e.Item}' is not an item type.";
        if (typeof(Items.BaseWeapon).IsAssignableFrom(type) || typeof(Items.BaseArmor).IsAssignableFrom(type) ||
            typeof(Items.BaseJewel).IsAssignableFrom(type) || typeof(Items.BaseClothing).IsAssignableFrom(type) ||
            typeof(Items.BaseTalisman).IsAssignableFrom(type))
            return $"'{e.Item}' is gear; grave digging gives no gear.";
        if (typeof(Items.PowerScroll).IsAssignableFrom(type) || type.Name.Contains("BlessDeed", StringComparison.OrdinalIgnoreCase))
            return $"'{e.Item}' is not allowed by the shard's reward rules.";
        return null;
    }

    public static void Validate(GraveDiggingTable t, List<string> errors)
    {
        if (t.MinSkill is < 0 or > 120) errors.Add($"minSkill {t.MinSkill} is not 0 to 120");
        if (t.SuccessMaxSkill <= t.SuccessMinSkill) errors.Add("successMaxSkill must be above successMinSkill");

        var ranges = new List<int>();
        foreach (var row in t.GraveTiles ?? [])
        {
            if (TryParseRange(row, out var lo, out var hi))
            {
                ranges.Add(lo);
                ranges.Add(hi);
            }
            else
            {
                errors.Add($"graveTiles: \"{row}\" is not a tile id or a range like 0xEE0-0xEE2");
            }
        }

        t.GraveRanges = ranges.ToArray();
        if (t.GraveRanges.Length == 0) errors.Add("graveTiles: none");

        if ((t.RegionKeywords?.Count ?? 0) + (t.Areas?.Count ?? 0) == 0) errors.Add("no cemetery: regionKeywords and areas are both empty");
        foreach (var a in t.Areas ?? [])
        {
            if (a.X2 < a.X1 || a.Y2 < a.Y1) errors.Add($"areas: \"{a.Name}\" has its corners the wrong way round");
            if (a.Facets == null || a.Facets.Count == 0) errors.Add($"areas: \"{a.Name}\" names no facet");
            foreach (var f in a.Facets ?? [])
                if (!Map.TryParse(f ?? "", null, out var fm) || fm == null || fm == Map.Internal) errors.Add($"areas: \"{a.Name}\": \"{f}\" is not a facet");
        }

        if (t.Node == null) errors.Add("node: missing");
        else
        {
            if (t.Node.MinTotal < 1 || t.Node.MaxTotal < t.Node.MinTotal) errors.Add("node: minTotal must be 1 or more and maxTotal at least minTotal");
            if (t.Node.MinRespawnMinutes < 0 || t.Node.MaxRespawnMinutes < t.Node.MinRespawnMinutes) errors.Add("node: the respawn minutes are out of order");
        }

        if (t.MaxRange is < 1 or > 6) errors.Add($"maxRange {t.MaxRange} is not 1 to 6");

        if (t.Tiers == null || t.Tiers.Count == 0) errors.Add("tiers: none");
        else if (t.Tiers.All(x => x.MinSkill > t.MinSkill)) errors.Add("tiers: none open at minSkill");
        foreach (var tier in t.Tiers ?? [])
        {
            if (tier.Finds == null || tier.Finds.Count == 0 || tier.Finds.Sum(f => Math.Max(0, f.Weight)) <= 0)
                errors.Add($"tiers: the {tier.MinSkill} tier has nothing with weight");
            foreach (var f in tier.Finds ?? [])
            {
                f.Type = ItemType(f.Item);
                if (f.Type == null) errors.Add($"tiers: \"{f.Item}\" is not an item type");
                if (f.Weight < 0 || f.Min < 1 || f.Max < f.Min || f.Max > 100) errors.Add($"tiers: \"{f.Item}\" needs weight 0 or more and 1 <= min <= max <= 100");
            }
        }

        if (t.Bonus == null) errors.Add("bonus: missing");
        else
        {
            if (t.Bonus.ChancePercent is < 0 or > 100) errors.Add($"bonus.chancePercent {t.Bonus.ChancePercent} is not 0 to 100");
            if (t.Bonus.ChancePercent > 0 && (t.Bonus.Entries == null || t.Bonus.Entries.Count == 0)) errors.Add("bonus: a chance with no entries");
            foreach (var e in t.Bonus.Entries ?? [])
                if (WhyNotBonus(e) is { } why)
                    errors.Add($"bonus: {why}");
        }

        if (t.GradeSkills == null || t.GradeSkills.Count is < 1 or > 6) errors.Add("gradeSkills: 1 to 6 thresholds");
        else
            for (var i = 1; i < t.GradeSkills.Count; i++)
                if (t.GradeSkills[i] <= t.GradeSkills[i - 1]) errors.Add("gradeSkills: must rise");

        if (t.Spade == null) errors.Add("spade: missing");
        else if (t.Spade.Price is < 1 or > 60000 || t.Spade.Uses < 1) errors.Add("spade: price must be 1 to 60000 and uses 1 or more");
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
        lines.Add($"grave digging table in force: {path} ({table.Areas.Count} cemetery areas, {table.Tiers.Count} tiers, bonus {table.Bonus.ChancePercent}%)");
        return lines;
    }
}
