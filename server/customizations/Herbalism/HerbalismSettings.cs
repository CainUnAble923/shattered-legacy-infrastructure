// HerbalismSettings.cs
//
// cc-P804 (F-51, phase 1): every Herbalism number and the wild patch list. Configuration/herbalism.json, written with these
// defaults on first start, read at start and by [Herbalism reload. A bad file is refused whole, every error is printed, and the
// table in force is kept. All numbers are placeholders for playtest. Notes: shard-migration notes/cc-P804-herbalism-phase-1.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Server.Engines.Plants;

/// <summary>One herb: its key (a PlantType name when it can be grown), words, tier (1 is 0-50 skill) and what it makes.</summary>
public sealed class HerbRow
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
    public string Reagent { get; set; } = "";
    public int PatchArt { get; set; }
    public int RawArt { get; set; }
    public int RawHue { get; set; }
    public int AlembicRank { get; set; }
    public bool Grown { get; set; }
    public bool Stub { get; set; }
}

/// <summary>One wild patch: its herb, facet and tile. The tile's height comes from the map.</summary>
public sealed class HerbSpot
{
    public string Herb { get; set; } = "";
    public string Map { get; set; } = "Trammel";
    public int X { get; set; }
    public int Y { get; set; }

    public string Key => $"{Map}:{X}:{Y}";
}

public sealed class HerbalismSettings
{
    public const string FileName = "herbalism.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static HerbalismSettings Current { get; set; } = new();

    // Neglect
    public double GraceBaseDays { get; set; } = 2;
    public double GraceExtraDaysAt200 { get; set; } = 5;
    public int NeglectHitsPerCheck { get; set; } = 6;

    // Harvest (herbs a mature plant makes in a day)
    public int BowlHerbsPerDay { get; set; } = 6;
    public int BedHerbsPerDay { get; set; } = 4;
    public int HerbsPerStrainTier { get; set; } = 1;
    public int YieldBonusPercentAt200 { get; set; } = 100;
    public int MaladyYieldLossPercent { get; set; } = 15;
    public int MaxStoredDays { get; set; } = 7;

    // Processing
    public int HerbsPerBundle { get; set; } = 5;
    public int ReagentsPerBundle { get; set; } = 10;
    public int BundleBonusPercentAt200 { get; set; } = 50;

    // Breeding
    public double MutationPercentBase { get; set; } = 5;
    public double MutationPercentAt200 { get; set; } = 20;
    public int TopStrainAt200 { get; set; } = 16;
    public int StrainTiersPerAlembicRank { get; set; } = 3;

    // Beds
    public int BedMaladyPercent { get; set; } = 33;

    // Gains
    public int GainWindow { get; set; } = 50;
    public int GainPerStrainTier { get; set; } = 5;
    public int TierJumpGainRolls { get; set; } = 3;

    // Foraging
    public int RespawnMinutes { get; set; } = 60;
    public int ForageMin { get; set; } = 2;
    public int ForageMax { get; set; } = 5;
    public int SeedChancePercent { get; set; } = 15;

    // The Alembic
    public int BedDeedGold { get; set; } = 5000;
    public int StarterSeeds { get; set; } = 2;
    public int TableX { get; set; } = 3458; // the Order's hall, New Haven
    public int TableY { get; set; } = 2566;
    public int TableZ { get; set; } = 35;

    public List<HerbRow> Herbs { get; set; } = DefaultHerbs();

    public List<HerbSpot> Patches { get; set; } = DefaultPatches();

    private static List<HerbRow> DefaultHerbs() =>
    [
        new() { Key = "Garlic", Name = "garlic", Tier = 1, Reagent = "Garlic", PatchArt = 0x18E1, RawArt = 0xF84, RawHue = 0x7D6, Grown = true },
        new() { Key = "Ginseng", Name = "ginseng", Tier = 1, Reagent = "Ginseng", PatchArt = 0x18E9, RawArt = 0xF85, RawHue = 0x7D6, Grown = true },
        new() { Key = "Mandrake", Name = "mandrake", Tier = 2, Reagent = "MandrakeRoot", PatchArt = 0x18DF, RawArt = 0xF86, RawHue = 0x7D6, AlembicRank = 1, Grown = true },
        new() { Key = "Nightshade", Name = "nightshade", Tier = 2, Reagent = "Nightshade", PatchArt = 0x18E5, RawArt = 0xF88, RawHue = 0x7D6, AlembicRank = 1, Grown = true },
        new() { Key = "BloodMoss", Name = "blood moss", Tier = 2, Reagent = "Bloodmoss", PatchArt = 0xC9F, RawArt = 0xF7B, RawHue = 0x7D6, AlembicRank = 1, Grown = true },
        new() { Key = "GraveMoss", Name = "grave moss", Tier = 3, Reagent = "GraveDust", PatchArt = 0xCA7, RawArt = 0xF8F, RawHue = 0x7D6 },
        new() { Key = "NoxBloom", Name = "nox bloom", Tier = 3, Reagent = "NoxCrystal", PatchArt = 0xC86, RawArt = 0xF8E, RawHue = 0x7D6 },
        new() { Key = "DragonsTongue", Name = "dragon's tongue", Tier = 3, Reagent = "DragonsBlood", PatchArt = 0xC97, RawArt = 0xF82, RawHue = 0x7D6 },
        new() { Key = "Exotic", Name = "exotic herb", Tier = 4, Reagent = "", Stub = true },
    ];

    private static HerbSpot S(string herb, int x, int y, string map = "Trammel") => new() { Herb = herb, X = x, Y = y, Map = map };

    private static List<HerbSpot> DefaultPatches() =>
    [
        // New Haven: the first patches a new player meets, and the Camping lesson's.
        S("Garlic", 3432, 2630), S("Ginseng", 3436, 2634), S("Garlic", 3420, 2640), S("Ginseng", 3414, 2646),

        // Forests and fields near the mainland towns: the two starting herbs.
        S("Garlic", 1450, 1680), S("Ginseng", 1460, 1700), S("Garlic", 2510, 500), S("Ginseng", 2530, 480),
        S("Garlic", 620, 2200), S("Ginseng", 640, 2220), S("Garlic", 1860, 2750), S("Ginseng", 1880, 2770),

        // Deep woods and wet ground: the second tier.
        S("Mandrake", 520, 1000), S("Nightshade", 540, 960), S("BloodMoss", 580, 1020),
        S("Mandrake", 2100, 1100), S("Nightshade", 2150, 1150), S("BloodMoss", 2200, 1180),
        S("Nightshade", 3550, 2750), S("BloodMoss", 1880, 2100), S("Mandrake", 780, 1500),

        // Graveyards, the swamp and the volcanic south: the third.
        S("GraveMoss", 780, 1120), S("GraveMoss", 1380, 1500), S("NoxBloom", 2120, 310), S("NoxBloom", 3990, 2860),
        S("DragonsTongue", 4760, 3770), S("DragonsTongue", 5300, 3800),
    ];

    public HerbRow Herb(string key)
    {
        foreach (var h in Herbs)
        {
            if (h.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return h;
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(HerbalismSettings settings) => JsonSerializer.Serialize(settings, _json) + "\n";

    private static void Range(double value, double min, double max, string name, List<string> errors)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }

    /// <summary>Reads settings, or returns null and fills errors: accepted whole or not at all.</summary>
    public static HerbalismSettings Parse(string json, List<string> errors)
    {
        HerbalismSettings s;

        try
        {
            s = JsonSerializer.Deserialize<HerbalismSettings>(json, _json);
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

        Range(s.GraceBaseDays, 0, 60, nameof(GraceBaseDays), errors);
        Range(s.GraceExtraDaysAt200, 0, 60, nameof(GraceExtraDaysAt200), errors);
        Range(s.NeglectHitsPerCheck, 0, 100, nameof(NeglectHitsPerCheck), errors);
        Range(s.BowlHerbsPerDay, 0, 1000, nameof(BowlHerbsPerDay), errors);
        Range(s.BedHerbsPerDay, 0, 1000, nameof(BedHerbsPerDay), errors);
        Range(s.HerbsPerStrainTier, 0, 100, nameof(HerbsPerStrainTier), errors);
        Range(s.YieldBonusPercentAt200, 0, 1000, nameof(YieldBonusPercentAt200), errors);
        Range(s.MaladyYieldLossPercent, 0, 100, nameof(MaladyYieldLossPercent), errors);
        Range(s.MaxStoredDays, 1, 365, nameof(MaxStoredDays), errors);
        Range(s.HerbsPerBundle, 1, 1000, nameof(HerbsPerBundle), errors);
        Range(s.ReagentsPerBundle, 1, 1000, nameof(ReagentsPerBundle), errors);
        Range(s.BundleBonusPercentAt200, 0, 1000, nameof(BundleBonusPercentAt200), errors);
        Range(s.MutationPercentBase, 0, 100, nameof(MutationPercentBase), errors);
        Range(s.MutationPercentAt200, 0, 100, nameof(MutationPercentAt200), errors);
        Range(s.TopStrainAt200, 0, HerbalLane.TopStrain, nameof(TopStrainAt200), errors);
        Range(s.StrainTiersPerAlembicRank, 1, 100, nameof(StrainTiersPerAlembicRank), errors);
        Range(s.BedMaladyPercent, 0, 100, nameof(BedMaladyPercent), errors);
        Range(s.GainWindow, 1, 200, nameof(GainWindow), errors);
        Range(s.GainPerStrainTier, 0, 50, nameof(GainPerStrainTier), errors);
        Range(s.TierJumpGainRolls, 0, 20, nameof(TierJumpGainRolls), errors);
        Range(s.RespawnMinutes, 1, 100000, nameof(RespawnMinutes), errors);
        Range(s.ForageMin, 1, 100, nameof(ForageMin), errors);
        Range(s.ForageMax, s.ForageMin, 100, nameof(ForageMax), errors);
        Range(s.SeedChancePercent, 0, 100, nameof(SeedChancePercent), errors);
        Range(s.BedDeedGold, 0, 100000000, nameof(BedDeedGold), errors);
        Range(s.StarterSeeds, 0, 100, nameof(StarterSeeds), errors);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var h in s.Herbs ?? [])
        {
            if (!keys.Add(h.Key ?? ""))
            {
                errors.Add($"herb {h.Key}: a second row.");
            }

            if (h.Tier is < 1 or > 4)
            {
                errors.Add($"herb {h.Key}: tier must be 1 to 4.");
            }

            if (h.Grown && !Enum.TryParse<PlantType>(h.Key, out _))
            {
                errors.Add($"herb {h.Key}: a grown herb's key must be a plant type.");
            }

            if (!h.Stub && AssemblyHandler.FindTypeByName(h.Reagent ?? "")?.IsSubclassOf(typeof(Item)) != true)
            {
                errors.Add($"herb {h.Key}: \"{h.Reagent}\" is not an item.");
            }
        }

        var spots = new HashSet<string>();

        foreach (var spot in s.Patches ?? [])
        {
            if (s.Herb(spot.Herb) is null or { Stub: true })
            {
                errors.Add($"patch {spot.Key}: \"{spot.Herb}\" is not a herb that grows wild.");
            }

            if (Server.Map.Parse(spot.Map ?? "") == null)
            {
                errors.Add($"patch {spot.Key}: no such facet.");
            }

            if (!spots.Add(spot.Key))
            {
                errors.Add($"patch {spot.Key}: a second patch on that tile.");
            }
        }

        return errors.Count == 0 ? s : null;
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (File.Exists(path))
        {
            return Reload(path);
        }

        Current = new HerbalismSettings();

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
        HerbalismSettings s = null;

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
        return [$"{FileName} loaded: {s.Herbs.Count} herbs, {s.Patches.Count} wild patches."];
    }
}
