// cc-P784: every number of the holiday stocking, in one table Chase tunes: Configuration/holiday-stocking.json, written
// with the defaults on first start if missing, read at start and by [HolidayStocking reload. A bad file is refused whole,
// every error is printed, and the table in force is kept (the GoldPanningTable pattern). Notes in shard-migration
// notes/cc-P784-holiday-stocking.md.
//
//   enabled             false: the stocking says it is empty and gives nothing (it still hangs at the stall)
//   season              the Festival season it hangs in (FestivalSeasons: halloween, winter or spring)
//   holdSeconds         how long the player holds still at the stocking before the grab
//   range               tiles from the player to the stocking, to start and to finish
//   grabsPerDay         grabs a day per account (the shard's local day)
//   tokensOnFirstGrab   Festival Tokens on the account's first grab of the day; later grabs that day give none
//   offsetX, offsetY,   where it hangs, from the Festival Peddler's tile: on the east half of her table (0x0B7D, height 6)
//   offsetZ
//   itemID              the stocking's art (0x2BDC, a red stocking)
//   prizes              the prize table: an item type, how many, and its weight. Treats and small decor only: a type
//                       that is a deed, a scroll of power, stat or skill, a weapon, armour, jewellery, clothing, a
//                       talisman or a container is refused, so nothing in the table grants points or properties.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server.Engines.RewardStores;

public sealed class StockingPrizeRow
{
    public string Type { get; set; } = "";
    public int Amount { get; set; } = 1;
    public int Weight { get; set; } = 1;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class HolidayStockingTable
{
    public const string FileName = "holiday-stocking.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string? About { get; set; }
    public bool Enabled { get; set; } = true;
    public string Season { get; set; } = FestivalSeasons.Winter;
    public double HoldSeconds { get; set; } = 6.0;
    public int Range { get; set; } = 2;
    public int GrabsPerDay { get; set; } = 1;
    public int TokensOnFirstGrab { get; set; } = 1;
    public int OffsetX { get; set; } = 1;
    public int OffsetY { get; set; } = 1;
    public int OffsetZ { get; set; } = 6;
    public int ItemID { get; set; } = 0x2BDC;
    public List<StockingPrizeRow> Prizes { get; set; } = [];

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    [JsonIgnore]
    public int TotalWeight => Prizes.Sum(p => Math.Max(0, p.Weight));

    /// <summary>
    /// The kinds of item the prize table refuses: anything that grants skill or stat points (the special scrolls: power,
    /// stat cap, alacrity, transcendence), anything with item properties (weapons, armour, jewellery, clothing,
    /// talismans), deeds, and containers (which could carry any of those).
    /// </summary>
    public static readonly Type[] ForbiddenBases =
    [
        typeof(SpecialScroll), typeof(BaseWeapon), typeof(BaseArmor), typeof(BaseJewel), typeof(BaseClothing),
        typeof(BaseTalisman), typeof(Container)
    ];

    public static string? Forbidden(Type type)
    {
        if (type.Name.Contains("Deed", StringComparison.OrdinalIgnoreCase))
        {
            return "a deed";
        }

        var bad = ForbiddenBases.FirstOrDefault(b => b.IsAssignableFrom(type));
        return bad == null ? null : $"a {bad.Name}";
    }

    private static HolidayStockingTable? _defaults;
    private static HolidayStockingTable? _current;

    public static HolidayStockingTable Defaults
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

    public static HolidayStockingTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    // -- Defaults -----------------------------------------------------------------------------------

    // Our winter treats and small decor, all pinned items: food that fills (cookies, cake, candy canes, jelly beans) is
    // most of the weight, so most grabs are a snack you can eat now; the rest is small winter decor for a house.
    public static HolidayStockingTable BuildDefaults() => new()
    {
        About = "The holiday stocking at the Festival stall (cc-P784). Edit and run [HolidayStocking reload; a bad file is " +
                "refused whole and the table in force is kept. Once a day per account (grabsPerDay) while its season is " +
                "on, a player who holds still holdSeconds within range of it gets one prize, drawn by weight, and " +
                "tokensOnFirstGrab Festival Tokens on the day's first grab. Prizes are treats and small decor only: " +
                "deeds, special scrolls, weapons, armour, jewellery, clothing, talismans and containers are refused. " +
                "The idea is from Candy Apple's Stocking full of goodies; the code and numbers are ours.",
        Enabled = true,
        Season = FestivalSeasons.Winter,
        HoldSeconds = 6.0,
        Range = 2,
        GrabsPerDay = 1,
        TokensOnFirstGrab = 1,
        OffsetX = 1,
        OffsetY = 1,
        OffsetZ = 6,
        ItemID = 0x2BDC,
        Prizes =
        [
            new() { Type = "GingerBreadCookie", Amount = 3, Weight = 20 },
            new() { Type = "CandyCane", Amount = 3, Weight = 20 },
            new() { Type = "JellyBeans", Amount = 10, Weight = 15 },
            new() { Type = "Cake", Amount = 1, Weight = 15 },
            new() { Type = "HolidayBell", Amount = 1, Weight = 8 },
            new() { Type = "RedPoinsettia", Amount = 1, Weight = 6 },
            new() { Type = "WhitePoinsettia", Amount = 1, Weight = 6 },
            new() { Type = "BlueSnowflake", Amount = 1, Weight = 5 },
            new() { Type = "WhiteSnowflake", Amount = 1, Weight = 5 },
        ],
    };

    /// <summary>The prize a roll in [0, 1) draws, by weight.</summary>
    public StockingPrizeRow Draw(double roll)
    {
        var total = TotalWeight;
        var at = Math.Clamp(roll, 0.0, 0.999999) * total;
        foreach (var row in Prizes)
        {
            var w = Math.Max(0, row.Weight);
            if (at < w)
            {
                return row;
            }

            at -= w;
        }

        return Prizes.Last(p => p.Weight > 0);
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

    public static string ToJson(HolidayStockingTable table) => JsonSerializer.Serialize(table, Json);

    public static HolidayStockingTable? Parse(string json, string source, List<string> errors)
    {
        HolidayStockingTable? table;
        try
        {
            table = JsonSerializer.Deserialize<HolidayStockingTable>(json, Json);
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

    public static void Validate(HolidayStockingTable t, List<string> errors)
    {
        if (!FestivalSeasons.All.Contains(t.Season))
        {
            errors.Add($"season \"{t.Season}\" is not one of {string.Join(", ", FestivalSeasons.All)}");
        }

        if (t.HoldSeconds is < 0 or > 60) errors.Add($"holdSeconds {t.HoldSeconds} is not 0 to 60");
        if (t.Range is < 1 or > 6) errors.Add($"range {t.Range} is not 1 to 6");
        if (t.GrabsPerDay is < 1 or > 10) errors.Add($"grabsPerDay {t.GrabsPerDay} is not 1 to 10");
        if (t.TokensOnFirstGrab is < 0 or > 10) errors.Add($"tokensOnFirstGrab {t.TokensOnFirstGrab} is not 0 to 10");
        if (Math.Abs(t.OffsetX) > 5 || Math.Abs(t.OffsetY) > 5 || t.OffsetZ is < -20 or > 40)
            errors.Add("offsetX and offsetY must be -5 to 5 and offsetZ -20 to 40");
        if (t.ItemID is < 1 or > 0xFFFF) errors.Add($"itemID {t.ItemID} is not an item id");

        if (t.Prizes == null || t.Prizes.Count == 0)
        {
            errors.Add("prizes: none");
            return;
        }

        foreach (var row in t.Prizes)
        {
            row.ItemType = null;
            var type = AssemblyHandler.FindTypeByName(row.Type ?? "");
            if (type == null || !typeof(Item).IsAssignableFrom(type) || type.IsAbstract)
            {
                errors.Add($"prizes: \"{row.Type}\" is not an item type");
                continue;
            }

            if (Forbidden(type) is { } why)
            {
                errors.Add($"prizes: \"{row.Type}\" is {why}; the stocking gives treats and small decor only");
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null && type.GetConstructor([typeof(int)]) == null)
            {
                errors.Add($"prizes: \"{row.Type}\" cannot be made without arguments");
                continue;
            }

            if (row.Amount is < 1 or > 20) errors.Add($"prizes: {row.Type} amount {row.Amount} is not 1 to 20");
            if (row.Weight < 0) errors.Add($"prizes: {row.Type} weight is negative");
            row.ItemType = type;
        }

        if (t.Prizes.Sum(p => Math.Max(0, p.Weight)) <= 0) errors.Add("prizes: no row has weight");
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
        lines.Add($"holiday stocking table in force: {path} ({table.Season}, {table.Prizes.Count} prizes, " +
                  $"{table.GrabsPerDay} a day, {table.HoldSeconds}s)");
        return lines;
    }
}
