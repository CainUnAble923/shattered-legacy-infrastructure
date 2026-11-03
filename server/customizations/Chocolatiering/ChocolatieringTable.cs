// cc-P785: every number of chocolatiering, in one table Chase tunes: Configuration/chocolatiering.json, written with the
// defaults on first start if missing, read at start. A bad file is refused whole, every error is printed, and the
// built-in table is used (the GoldPanningTable pattern). The recipes are added to Cooking once, at start, so a changed
// file takes effect at the next restart; [Chocolatiering status says what is in force and checks the file.
//
//   enabled      false: none of these recipes is added (pinned's cocoa butter, cocoa liquor and three chocolates stay)
//   recipes      one row per craft entry, all in Cooking's Chocolatiering group:
//                  key        a unique name ("nutcracker.dark", "figure.orc.milk")
//                  item       what it makes: SweetCocoaButter, ChocolateNutcracker or ChocolateFigure (nothing else)
//                  chocolate  dark, milk or white (the nutcracker's and figure's kind; empty for sweet cocoa butter)
//                  figure     orc, beetle, bunny, solen or unicorn (figures only)
//                  minSkill, maxSkill   Cooking, as pinned's craft engine reads them
//                  needOven   true: at an oven
//                  resources  [{ type, amount }], the first is the main one
//   provisioner  what every provisioner also sells, for gold: { type, price, amount (stock) }. The five figure molds,
//                sugar, vanilla and foil sheets.
//
// Rules (refused at load): a recipe can make only the three chocolatiering items; a resource or a vendor row must be an
// item type and none of a deed, a special scroll (power, stat, skill), a weapon, armour, jewellery, clothing or a
// talisman. So nothing here grants points or item properties.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server.Engines.Craft;

public sealed class ChocoResource
{
    public string Type { get; set; } = "";
    public int Amount { get; set; } = 1;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class ChocoRecipe
{
    public string Key { get; set; } = "";
    public string Item { get; set; } = "";
    public string? Chocolate { get; set; }
    public string? Figure { get; set; }
    public double MinSkill { get; set; }
    public double MaxSkill { get; set; }
    public bool NeedOven { get; set; }
    public List<ChocoResource> Resources { get; set; } = [];

    [JsonIgnore]
    public Type? ItemType { get; set; }

    [JsonIgnore]
    public ChocolateNutcracker.ChocolateType ChocolateKind { get; set; }

    [JsonIgnore]
    public ChocolateFigureKind FigureKind { get; set; }
}

public sealed class ChocoVendorRow
{
    public string Type { get; set; } = "";
    public int Price { get; set; }
    public int Amount { get; set; } = 20;

    [JsonIgnore]
    public Type? ItemType { get; set; }
}

public sealed class ChocolatieringTable
{
    public const string FileName = "chocolatiering.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public static readonly Type[] Makes = [typeof(SweetCocoaButter), typeof(ChocolateNutcracker), typeof(ChocolateFigure)];

    public static readonly Type[] ForbiddenBases =
    [
        typeof(SpecialScroll), typeof(BaseWeapon), typeof(BaseArmor), typeof(BaseJewel), typeof(BaseClothing),
        typeof(BaseTalisman)
    ];

    public string? About { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ChocoRecipe> Recipes { get; set; } = [];
    public List<ChocoVendorRow> Provisioner { get; set; } = [];

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    private static ChocolatieringTable? _defaults;
    private static ChocolatieringTable? _current;

    public static ChocolatieringTable Defaults
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

    public static ChocolatieringTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    public ChocoRecipe? Recipe(string key) => Recipes.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public ChocoVendorRow? Sold(Type type) => Provisioner.FirstOrDefault(v => v.ItemType == type);

    // -- Defaults -----------------------------------------------------------------------------------

    private static ChocoResource R(string type, int amount = 1) => new() { Type = type, Amount = amount };

    // The chocolates' skill ranges are pinned's (and OSI's): dark 15-100, milk 32.5-107.5, white 52.5-127.5.
    private static readonly (string Name, double Min, double Max)[] Chocolates =
        [("dark", 15.0, 100.0), ("milk", 32.5, 107.5), ("white", 52.5, 127.5)];

    public static readonly string[] FigureNames = ["orc", "beetle", "bunny", "solen", "unicorn"];

    public static ChocolatieringTable BuildDefaults()
    {
        var t = new ChocolatieringTable
        {
            About = "Chocolatiering in Cooking (cc-P785). Read at start; a changed file takes effect at the next restart, " +
                    "and a bad file is refused whole (the built-in table is used). Pinned already makes cocoa butter, " +
                    "cocoa liquor and the three chocolates from cocoa pulp; this adds sweet cocoa butter, the three " +
                    "chocolate nutcrackers (ServUO's Time of Legends recipes) and foil-wrapped figures from molds, and " +
                    "what the provisioner sells for them. The figures and molds are after Carlos_ar's Chocolatiering " +
                    "Adventure; the code and numbers are ours.",
            Enabled = true
        };

        // ServUO DefCooking.cs:419-422: sugar and cocoa butter, at an oven.
        t.Recipes.Add(new ChocoRecipe
        {
            Key = "sweetcocoabutter", Item = "SweetCocoaButter", MinSkill = 15.0, MaxSkill = 100.0, NeedOven = true,
            Resources = [R("SackOfSugar"), R("CocoaButter")]
        });

        // ServUO DefCooking.cs:447-463: sweet cocoa butter, a foil sheet and cocoa liquor. ServUO's second line names
        // SweetCocoaButter under the foil sheet's label (1124032); the foil is what is meant (GoldFoil is the wrapper).
        foreach (var (name, min, max) in Chocolates)
        {
            t.Recipes.Add(new ChocoRecipe
            {
                Key = $"nutcracker.{name}", Item = "ChocolateNutcracker", Chocolate = name, MinSkill = min, MaxSkill = max,
                Resources = [R("SweetCocoaButter"), R("GoldFoil"), R("CocoaLiquor")]
            });
        }

        // Ours: a figure is one bar of its chocolate, poured into a mold and wrapped in foil, at the chocolate's own skill.
        foreach (var fig in FigureNames)
        {
            foreach (var (name, min, max) in Chocolates)
            {
                var bar = name switch { "dark" => "DarkChocolate", "milk" => "MilkChocolate", _ => "WhiteChocolate" };
                t.Recipes.Add(new ChocoRecipe
                {
                    Key = $"figure.{fig}.{name}", Item = "ChocolateFigure", Chocolate = name, Figure = fig,
                    MinSkill = min, MaxSkill = max,
                    Resources = [R(bar), R(MoldTypeName(fig)), R("GoldFoil")]
                });
            }
        }

        foreach (var fig in FigureNames)
        {
            t.Provisioner.Add(new ChocoVendorRow { Type = MoldTypeName(fig), Price = 25, Amount = 20 });
        }

        t.Provisioner.Add(new ChocoVendorRow { Type = "SackOfSugar", Price = 6, Amount = 40 });
        t.Provisioner.Add(new ChocoVendorRow { Type = "Vanilla", Price = 8, Amount = 40 });
        t.Provisioner.Add(new ChocoVendorRow { Type = "GoldFoil", Price = 5, Amount = 40 });
        return t;
    }

    public static string MoldTypeName(string figure) =>
        figure.ToLowerInvariant() switch
        {
            "orc"     => "OrcChocolateMold",
            "beetle"  => "BeetleChocolateMold",
            "bunny"   => "BunnyChocolateMold",
            "solen"   => "SolenChocolateMold",
            _         => "UnicornChocolateMold"
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

    public static string ToJson(ChocolatieringTable table) => JsonSerializer.Serialize(table, Json);

    public static ChocolatieringTable? Parse(string json, string source, List<string> errors)
    {
        ChocolatieringTable? table;
        try
        {
            table = JsonSerializer.Deserialize<ChocolatieringTable>(json, Json);
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
        if (type.Name.Contains("Deed", StringComparison.OrdinalIgnoreCase))
        {
            return "a deed";
        }

        var bad = ForbiddenBases.FirstOrDefault(b => b.IsAssignableFrom(type));
        return bad == null ? null : $"a {bad.Name}";
    }

    private static Type? ItemTypeOf(string? name, string where, List<string> errors)
    {
        var type = AssemblyHandler.FindTypeByName(name ?? "");
        if (type == null || !typeof(Item).IsAssignableFrom(type) || type.IsAbstract)
        {
            errors.Add($"{where}: \"{name}\" is not an item type");
            return null;
        }

        if (Forbidden(type) is { } why)
        {
            errors.Add($"{where}: \"{name}\" is {why}; chocolatiering uses and sells kitchen goods only");
            return null;
        }

        return type;
    }

    public static void Validate(ChocolatieringTable t, List<string> errors)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in t.Recipes ?? [])
        {
            var where = $"recipes: {r.Key}";
            if (string.IsNullOrWhiteSpace(r.Key) || !keys.Add(r.Key))
            {
                errors.Add($"recipes: key \"{r.Key}\" is empty or used twice");
            }

            r.ItemType = Makes.FirstOrDefault(m => m.Name.Equals(r.Item, StringComparison.OrdinalIgnoreCase));
            if (r.ItemType == null)
            {
                errors.Add($"{where}: item \"{r.Item}\" is not one of {string.Join(", ", Makes.Select(m => m.Name))}");
            }

            if (r.ItemType != typeof(SweetCocoaButter))
            {
                if (!Enum.TryParse<ChocolateNutcracker.ChocolateType>(r.Chocolate ?? "", true, out var kind) ||
                    !Enum.IsDefined(kind) || int.TryParse(r.Chocolate, out _))
                {
                    errors.Add($"{where}: chocolate \"{r.Chocolate}\" is not dark, milk or white");
                }
                else
                {
                    r.ChocolateKind = kind;
                }
            }

            if (r.ItemType == typeof(ChocolateFigure))
            {
                if (!Enum.TryParse<ChocolateFigureKind>(r.Figure ?? "", true, out var fig) || !Enum.IsDefined(fig) ||
                    int.TryParse(r.Figure, out _))
                {
                    errors.Add($"{where}: figure \"{r.Figure}\" is not one of {string.Join(", ", FigureNames)}");
                }
                else
                {
                    r.FigureKind = fig;
                }
            }

            if (r.MinSkill is < 0 or > 120 || r.MaxSkill <= r.MinSkill || r.MaxSkill > 150)
            {
                errors.Add($"{where}: minSkill {r.MinSkill} and maxSkill {r.MaxSkill} must be 0-120 and min below max (max at most 150)");
            }

            if (r.Resources == null || r.Resources.Count == 0)
            {
                errors.Add($"{where}: no resources");
                continue;
            }

            foreach (var res in r.Resources)
            {
                res.ItemType = ItemTypeOf(res.Type, $"{where} resource", errors);
                if (res.Amount is < 1 or > 100)
                {
                    errors.Add($"{where}: {res.Type} amount {res.Amount} is not 1 to 100");
                }
            }
        }

        foreach (var v in t.Provisioner ?? [])
        {
            v.ItemType = ItemTypeOf(v.Type, "provisioner", errors);
            if (v.Price is < 1 or > 100_000 || v.Amount is < 1 or > 999)
            {
                errors.Add($"provisioner: {v.Type} price {v.Price} must be 1-100000 and amount {v.Amount} 1-999");
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

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            lines.Add($"could not read {path}: {ex.Message}; the built-in defaults are in force");
            Current = Defaults;
            return lines;
        }

        var errors = new List<string>();
        var table = Parse(json, path, errors);
        if (table == null)
        {
            lines.Add($"{path} refused; the built-in defaults are in force:");
            lines.AddRange(errors.Select(e => "  " + e));
            Current = Defaults;
            return lines;
        }

        Current = table;
        lines.Add($"chocolatiering table in force: {path} ({table.Recipes.Count} recipes, {table.Provisioner.Count} provisioner rows)");
        return lines;
    }
}
