// cc-P787: every number of beekeeping, in one table Chase tunes: Configuration/beekeeping.json, written with the defaults
// on first start if missing, read at start and on [Beekeeping reload. A bad file is refused whole, every error is
// printed, and the table in force stays (the ChocolatieringTable pattern). The candle and wax pot recipes and the
// beekeeper's rows are read once, at start; everything else (care, harvest, wild hives) is read live.
//
//   enabled            false: hives stop their daily checks and wild hives are not seeded (nothing is deleted)
//   checkHours         hours between a hive's daily checks (the plants' 23)
//   sweepMinutes       how often the one shared sweep looks for hives that are due
//   range              tiles around a hive read for flowers and water
//   maxHivesPerHouse   beehives one house may hold
//   maxPopulation      bees (in tens of thousands) a colony grows to
//   flowersPerBee, waterPerBee   flowers and water a colony needs per population step (rounded up)
//   tooMuchWaterFactor water above need times this is "too much" (soggy combs: one problem, as low water)
//   wetTileWater       water a wet tile (pond, river, sea) in range counts as
//   flowerItemIds, waterItemIds   item art read as flowers and as water (plant bowls and filled water drinks are
//                      counted by type besides)
//   colonizeDays, broodDays     days in the first two stages before a colony produces
//   healthGain, healthLossPerProblem, growHealth, shrinkHealth   daily health and population rules
//   parasiteChance, diseaseChance  daily chance (percent) of each ailment
//   healAmount, greaterHealAmount  health a Heal and a Greater Heal potion mend
//   strengthHours      a Strength potion halves the day's health loss and the ailment chances this long
//   agilityHours, agilityRange  an Agility potion widens the range by agilityRange this long
//   honeyPerBee, waxPerBee      jars of honey and raw wax a producing colony makes per population step per day, at
//                      full health (scaled by health), up to maxHoney and maxWax
//   hiveToolUses       harvests a hive tool lasts
//   waxPotBatch, waxPotWastePercent   raw beeswax a wax pot renders at once, and the share lost as slumgum
//   rawWaxHue, honeycombHue     the hues of raw beeswax and of a wild honeycomb
//   swarmPopulation, swarmHealth   what a bee swarm starts a colony with
//   candles            Alchemy's Candles group: { key, item (a stock candle), minSkill, maxSkill, wax, thread }
//   waxPotRecipe       Tinkering: { minSkill, maxSkill, ingots }
//   beekeeper          what every beekeeper also sells: { type, price, amount }
//   wild               wild hives: { enabled, combsPerHive, stingMin, stingMax, regrowHours, swarmChance, spots:[{name,x,y,z}] }
//
// Rules (refused at load): a candle row makes a stock candle (a light, not a weapon, armour, jewel, cloth or
// talisman); a beekeeper row is one of the beekeeping items or stock honey or beeswax. So nothing here grants points,
// scrolls, deeds other than the hive's own, or item properties.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server.Engines.Beekeeping;

public sealed class CandleRecipe
{
    public string Key { get; set; } = "";
    public string Item { get; set; } = "";
    public double MinSkill { get; set; }
    public double MaxSkill { get; set; }
    public int Wax { get; set; } = 1;
    public int Thread { get; set; } = 1;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class WaxPotRecipe
{
    public double MinSkill { get; set; } = 30.0;
    public double MaxSkill { get; set; } = 80.0;
    public int Ingots { get; set; } = 6;
}

public sealed class BeekeeperRow
{
    public string Type { get; set; } = "";
    public int Price { get; set; }
    public int Amount { get; set; } = 20;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class WildSpot
{
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    [JsonIgnore]
    public Point3D Location => new(X, Y, Z);
}

public sealed class WildHives
{
    public bool Enabled { get; set; } = true;
    public int CombsPerHive { get; set; } = 3;
    public int StingMin { get; set; } = 2;
    public int StingMax { get; set; } = 6;
    public double RegrowHours { get; set; } = 4.0;
    public int SwarmChance { get; set; } = 50;
    public List<WildSpot> Spots { get; set; } = [];
}

public sealed class BeekeepingTable
{
    public const string FileName = "beekeeping.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    /// <summary>What a beekeeper may sell: ours, and stock honey and beeswax.</summary>
    public static readonly Type[] Sellable =
    [
        typeof(BeehiveDeed), typeof(BeeSwarm), typeof(HiveTool), typeof(WaxPot), typeof(JarHoney), typeof(Beeswax)
    ];

    public static readonly Type[] ForbiddenBases =
    [
        typeof(SpecialScroll), typeof(BaseWeapon), typeof(BaseArmor), typeof(BaseJewel), typeof(BaseClothing),
        typeof(BaseTalisman)
    ];

    public string? About { get; set; }
    public bool Enabled { get; set; } = true;
    public double CheckHours { get; set; } = 23.0;
    public double SweepMinutes { get; set; } = 5.0;
    public int Range { get; set; } = 5;
    public int MaxHivesPerHouse { get; set; } = 2;
    public int MaxPopulation { get; set; } = 10;
    public double FlowersPerBee { get; set; } = 0.5;
    public double WaterPerBee { get; set; } = 0.3;
    public int TooMuchWaterFactor { get; set; } = 6;
    public int WetTileWater { get; set; } = 3;
    public List<int> FlowerItemIds { get; set; } = [];
    public List<int> WaterItemIds { get; set; } = [];
    public int ColonizeDays { get; set; } = 1;
    public int BroodDays { get; set; } = 1;
    public int HealthGain { get; set; } = 10;
    public int HealthLossPerProblem { get; set; } = 15;
    public int GrowHealth { get; set; } = 50;
    public int ShrinkHealth { get; set; } = 30;
    public int ParasiteChance { get; set; } = 10;
    public int DiseaseChance { get; set; } = 8;
    public int HealAmount { get; set; } = 15;
    public int GreaterHealAmount { get; set; } = 35;
    public double StrengthHours { get; set; } = 24.0;
    public double AgilityHours { get; set; } = 24.0;
    public int AgilityRange { get; set; } = 2;
    public double HoneyPerBee { get; set; } = 0.3;
    public double WaxPerBee { get; set; } = 0.2;
    public int MaxHoney { get; set; } = 20;
    public int MaxWax { get; set; } = 15;
    public int HiveToolUses { get; set; } = 50;
    public int WaxPotBatch { get; set; } = 5;
    public int WaxPotWastePercent { get; set; } = 20;
    public int RawWaxHue { get; set; } = 1126;
    public int HoneycombHue { get; set; } = 1177;
    public int SwarmPopulation { get; set; } = 3;
    public int SwarmHealth { get; set; } = 80;
    public List<CandleRecipe> Candles { get; set; } = [];
    public WaxPotRecipe WaxPotRecipe { get; set; } = new();
    public List<BeekeeperRow> Beekeeper { get; set; } = [];
    public WildHives Wild { get; set; } = new();

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    private static BeekeepingTable? _defaults;
    private static BeekeepingTable? _current;

    public static BeekeepingTable Defaults
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

    public static BeekeepingTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    public BeekeeperRow? Sold(Type type) => Beekeeper.FirstOrDefault(v => v.ItemType == type);

    // -- Defaults -----------------------------------------------------------------------------------

    public static BeekeepingTable BuildDefaults()
    {
        var t = new BeekeepingTable
        {
            About = "Beekeeping (cc-P787): a hive in your house, honey and raw beeswax, the wax pot, candles on Alchemy " +
                    "and wild hives in Trammel's woods. Care numbers are read live; the recipes and the beekeeper's " +
                    "rows at start. A bad file is refused whole. After the apiculture of Shafev's Cooking Expansion " +
                    "Set; the code and numbers are ours.",

            // Flowers: the stock flower art (campion, foxglove, orfluer, poppies, snowdrops, white flowers), the
            // flower pots and the reagent plants (mandrake, nightshade, ginseng), each name read from tiledata (notes).
            FlowerItemIds =
            [
                0xC83, 0xC84, 0xC85, 0xC86, 0xC87, 0xC88, 0xC89, 0xC8A, 0xC8B, 0xC8C, 0xC8D, 0xC8E,
                0xCBE, 0xCBF, 0xCC0, 0xCC1,
                0x11C6, 0x11C7, 0x11CA, 0x11CB, 0x11CC,
                0x18DF, 0x18E0, 0x18E5, 0x18E6, 0x18E9, 0x18EA
            ],

            // Water: the troughs, the water tub and the water barrel.
            WaterItemIds = [0xB41, 0xB42, 0xB43, 0xB44, 0xE7B, 0x154D]
        };

        t.Candles.Add(new CandleRecipe { Key = "candle", Item = "Candle", MinSkill = 10.0, MaxSkill = 60.0, Wax = 1, Thread = 1 });
        t.Candles.Add(new CandleRecipe { Key = "candleshort", Item = "CandleShort", MinSkill = 25.0, MaxSkill = 75.0, Wax = 1, Thread = 1 });
        t.Candles.Add(new CandleRecipe { Key = "candlelong", Item = "CandleLong", MinSkill = 35.0, MaxSkill = 85.0, Wax = 2, Thread = 1 });
        t.Candles.Add(new CandleRecipe { Key = "candlelarge", Item = "CandleLarge", MinSkill = 45.0, MaxSkill = 95.0, Wax = 3, Thread = 1 });

        t.Beekeeper.Add(new BeekeeperRow { Type = "BeehiveDeed", Price = 500, Amount = 10 });
        t.Beekeeper.Add(new BeekeeperRow { Type = "BeeSwarm", Price = 250, Amount = 10 });
        t.Beekeeper.Add(new BeekeeperRow { Type = "HiveTool", Price = 60, Amount = 20 });
        t.Beekeeper.Add(new BeekeeperRow { Type = "WaxPot", Price = 150, Amount = 10 });

        // Checked on the client map (Trammel, facet 1): open woodland, passable, no static on the tile (notes).
        t.Wild.Spots =
        [
            new WildSpot { Name = "Yew woods, east", X = 601, Y = 1101, Z = 0 },
            new WildSpot { Name = "Yew woods, south", X = 480, Y = 1150, Z = 0 },
            new WildSpot { Name = "Yew woods, north", X = 640, Y = 880, Z = 0 },
            new WildSpot { Name = "Vesper, east fields", X = 2990, Y = 620, Z = 0 },
            new WildSpot { Name = "Britain woods, west", X = 1300, Y = 1550, Z = 20 },
            new WildSpot { Name = "Britain woods, south", X = 1250, Y = 1650, Z = 0 },
            new WildSpot { Name = "Britain woods, north", X = 1370, Y = 1400, Z = 0 },
            new WildSpot { Name = "Skara mainland, south", X = 880, Y = 2240, Z = 0 },
            new WildSpot { Name = "Skara mainland, north", X = 900, Y = 2000, Z = 0 },
            new WildSpot { Name = "Minoc woods", X = 2450, Y = 500, Z = 15 },
            new WildSpot { Name = "Trinsic woods, north", X = 1880, Y = 2650, Z = 0 },
            new WildSpot { Name = "Trinsic woods, west", X = 1700, Y = 2850, Z = 0 }
        ];

        return t;
    }

    // -- Care helpers -------------------------------------------------------------------------------

    public int FlowersNeeded(int population) => (int)Math.Ceiling(population * FlowersPerBee);

    public int WaterNeeded(int population) => (int)Math.Ceiling(population * WaterPerBee);

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

    public static string ToJson(BeekeepingTable table) => JsonSerializer.Serialize(table, Json);

    public static BeekeepingTable? Parse(string json, string source, List<string> errors)
    {
        BeekeepingTable? table;
        try
        {
            table = JsonSerializer.Deserialize<BeekeepingTable>(json, Json);
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

    public static string? Forbidden(Type type)
    {
        if (type.Name.Contains("Deed", StringComparison.OrdinalIgnoreCase) && type != typeof(BeehiveDeed))
        {
            return "a deed";
        }

        var bad = ForbiddenBases.FirstOrDefault(b => b.IsAssignableFrom(type));
        return bad == null ? null : $"a {bad.Name}";
    }

    private static void InRange(List<string> errors, string name, double value, double min, double max)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} {value} is not {min} to {max}");
        }
    }

    public static void Validate(BeekeepingTable t, List<string> errors)
    {
        InRange(errors, "checkHours", t.CheckHours, 0.1, 24 * 7);
        InRange(errors, "sweepMinutes", t.SweepMinutes, 0.5, 120);
        InRange(errors, "range", t.Range, 1, 12);
        InRange(errors, "maxHivesPerHouse", t.MaxHivesPerHouse, 0, 20);
        InRange(errors, "maxPopulation", t.MaxPopulation, 1, 100);
        InRange(errors, "flowersPerBee", t.FlowersPerBee, 0, 10);
        InRange(errors, "waterPerBee", t.WaterPerBee, 0, 10);
        InRange(errors, "tooMuchWaterFactor", t.TooMuchWaterFactor, 1, 100);
        InRange(errors, "wetTileWater", t.WetTileWater, 0, 100);
        InRange(errors, "colonizeDays", t.ColonizeDays, 0, 30);
        InRange(errors, "broodDays", t.BroodDays, 0, 30);
        InRange(errors, "healthGain", t.HealthGain, 0, 100);
        InRange(errors, "healthLossPerProblem", t.HealthLossPerProblem, 0, 100);
        InRange(errors, "growHealth", t.GrowHealth, 0, 100);
        InRange(errors, "shrinkHealth", t.ShrinkHealth, 0, 100);
        InRange(errors, "parasiteChance", t.ParasiteChance, 0, 100);
        InRange(errors, "diseaseChance", t.DiseaseChance, 0, 100);
        InRange(errors, "healAmount", t.HealAmount, 0, 100);
        InRange(errors, "greaterHealAmount", t.GreaterHealAmount, 0, 100);
        InRange(errors, "strengthHours", t.StrengthHours, 0, 24 * 7);
        InRange(errors, "agilityHours", t.AgilityHours, 0, 24 * 7);
        InRange(errors, "agilityRange", t.AgilityRange, 0, 6);
        InRange(errors, "honeyPerBee", t.HoneyPerBee, 0, 10);
        InRange(errors, "waxPerBee", t.WaxPerBee, 0, 10);
        InRange(errors, "maxHoney", t.MaxHoney, 0, 1000);
        InRange(errors, "maxWax", t.MaxWax, 0, 1000);
        InRange(errors, "hiveToolUses", t.HiveToolUses, 1, 1000);
        InRange(errors, "waxPotBatch", t.WaxPotBatch, 1, 100);
        InRange(errors, "waxPotWastePercent", t.WaxPotWastePercent, 0, 90);
        InRange(errors, "rawWaxHue", t.RawWaxHue, 0, 0xBB6);
        InRange(errors, "honeycombHue", t.HoneycombHue, 0, 0xBB6);
        InRange(errors, "swarmPopulation", t.SwarmPopulation, 1, t.MaxPopulation);
        InRange(errors, "swarmHealth", t.SwarmHealth, 1, 100);

        if (t.ShrinkHealth > t.GrowHealth)
        {
            errors.Add($"shrinkHealth {t.ShrinkHealth} is above growHealth {t.GrowHealth}");
        }

        t.FlowerItemIds ??= [];
        t.WaterItemIds ??= [];
        foreach (var id in t.FlowerItemIds.Concat(t.WaterItemIds))
        {
            if (id is < 1 or > 0xFFFF)
            {
                errors.Add($"item id {id} is not an art id");
            }
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in t.Candles ?? [])
        {
            var where = $"candles: {c.Key}";
            if (string.IsNullOrWhiteSpace(c.Key) || !keys.Add(c.Key))
            {
                errors.Add($"candles: key \"{c.Key}\" is empty or used twice");
            }

            var type = AssemblyHandler.FindTypeByName(c.Item ?? "");
            if (type == null || type.IsAbstract || !typeof(BaseLight).IsAssignableFrom(type))
            {
                errors.Add($"{where}: \"{c.Item}\" is not a light (a stock candle)");
            }
            else if (Forbidden(type) is { } why)
            {
                errors.Add($"{where}: \"{c.Item}\" is {why}; candles only");
            }
            else
            {
                c.ItemType = type;
            }

            if (c.MinSkill is < 0 or > 120 || c.MaxSkill <= c.MinSkill || c.MaxSkill > 150)
            {
                errors.Add($"{where}: minSkill {c.MinSkill} and maxSkill {c.MaxSkill} must be 0-120 and min below max (max at most 150)");
            }

            if (c.Wax is < 1 or > 50 || c.Thread is < 0 or > 50)
            {
                errors.Add($"{where}: wax {c.Wax} must be 1-50 and thread {c.Thread} 0-50");
            }
        }

        var p = t.WaxPotRecipe ??= new WaxPotRecipe();
        if (p.MinSkill is < 0 or > 120 || p.MaxSkill <= p.MinSkill || p.MaxSkill > 150 || p.Ingots is < 1 or > 50)
        {
            errors.Add($"waxPotRecipe: minSkill {p.MinSkill}, maxSkill {p.MaxSkill} (0-120, min below max) and ingots {p.Ingots} (1-50)");
        }

        foreach (var v in t.Beekeeper ?? [])
        {
            var type = AssemblyHandler.FindTypeByName(v.Type ?? "");
            if (type == null || !Sellable.Contains(type))
            {
                errors.Add($"beekeeper: \"{v.Type}\" is not one of {string.Join(", ", Sellable.Select(s => s.Name))}");
            }
            else
            {
                v.ItemType = type;
            }

            if (v.Price is < 1 or > 100_000 || v.Amount is < 1 or > 999)
            {
                errors.Add($"beekeeper: {v.Type} price {v.Price} must be 1-100000 and amount {v.Amount} 1-999");
            }
        }

        var w = t.Wild ??= new WildHives();
        InRange(errors, "wild.combsPerHive", w.CombsPerHive, 0, 20);
        InRange(errors, "wild.stingMin", w.StingMin, 0, 50);
        InRange(errors, "wild.stingMax", w.StingMax, w.StingMin, 50);
        InRange(errors, "wild.regrowHours", w.RegrowHours, 0.05, 24 * 7);
        InRange(errors, "wild.swarmChance", w.SwarmChance, 0, 100);
        var spots = new HashSet<(int, int)>();
        foreach (var s in w.Spots ??= [])
        {
            if (s.X is < 0 or >= 7168 || s.Y is < 0 or >= 4096 || s.Z is < -128 or > 127)
            {
                errors.Add($"wild spot \"{s.Name}\" ({s.X}, {s.Y}, {s.Z}) is off Trammel");
            }

            if (!spots.Add((s.X, s.Y)))
            {
                errors.Add($"wild spot \"{s.Name}\" ({s.X}, {s.Y}) is listed twice");
            }
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

        return Reload(path);
    }

    /// <summary>Reads the file; a bad one is refused and the table in force stays.</summary>
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
            lines.Add($"could not read {path}: {ex.Message}; the table in force stays ({Current.Source})");
            return lines;
        }

        var errors = new List<string>();
        var table = Parse(json, path, errors);
        if (table == null)
        {
            lines.Add($"{path} refused; the table in force stays ({Current.Source}):");
            lines.AddRange(errors.Select(e => "  " + e));
            return lines;
        }

        Current = table;
        lines.Add($"beekeeping table in force: {path} ({table.Candles.Count} candles, {table.Beekeeper.Count} beekeeper rows, {table.Wild.Spots.Count} wild spots)");
        return lines;
    }
}
