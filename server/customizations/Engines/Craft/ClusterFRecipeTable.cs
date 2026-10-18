// cc-P78 (F-43): recipe-gated crafting, the data. Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
// Every number Chase may tune is in one table, loaded from Configuration/recipe-gating.json the way cc-P48's League
// ladder is: a missing file is written with the built-in defaults below; a file that fails any check is refused whole
// and the table in force is kept; [RecipeGating reload reads it again. The test host runs no Configure, so it uses the
// defaults.
//
// Bands are keyed off each item's minimum skill in its craft system's main skill (CraftItem.Skills, the entry whose
// SkillToMake is the system's MainSkill; pinned CraftItem.cs:884-889 finds it the same way). No stock item is tagged.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Server.Engines.Craft;

public sealed record RecipeBand(int Number, string Name, double Floor, double Ceiling, bool Book);

/// <summary>A named table of recipe keys (or "bandV:Skill" pools) other systems draw scrolls from (Part F).</summary>
public sealed record RecipeRewardTable(string Name, bool Epic, string[] Keys);

public sealed class RecipeGatingTable
{
    public const string FileName = "recipe-gating.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    /// <summary>Where the table in force came from: "built-in defaults" or the file's path.</summary>
    public string Source { get; init; } = "built-in defaults";

    // Items at or below this minimum skill are always known.
    public double FreeUpTo { get; init; } = 30.0;

    // Band I to IV are pattern books (one per band per craft skill); V and up are single scrolls per item.
    public RecipeBand[] Bands { get; init; } =
    [
        new(1, "I", 30.0, 50.0, true),
        new(2, "II", 50.0, 70.0, true),
        new(3, "III", 70.0, 90.0, true),
        new(4, "IV", 90.0, 100.0, true),
        new(5, "V", 100.0, 120.0, false),
        new(6, "VI", 120.0, 140.0, false),
        new(7, "VII", 140.0, 160.0, false),
        new(8, "VIII", 160.0, 180.0, false),
        new(9, "IX", 180.0, 200.0, false),
    ];

    // The merge rule: a book band with fewer items than this, for one skill, folds into the next band's book.
    public int MergeBelow { get; init; } = 5;

    // Guild shop prices, guild currency only. Book price by band I to IV (roughly doubling).
    public int[] BookPrice { get; init; } = [100, 200, 400, 800];

    // A Band V rotation scroll: guild currency plus one material of the next tier, by craft skill (empty: none).
    public int BandVScrollPrice { get; init; } = 1_500;
    public int BandVMaterialAmount { get; init; } = 10;

    public Dictionary<string, string> BandVMaterial { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Blacksmith"] = "PlatinumIngot",
        ["Tinkering"] = "PlatinumIngot",
        ["Carpentry"] = "IronwoodBoard",
        ["Fletching"] = "IronwoodBoard",
        ["Tailoring"] = "BarbedLeather",
        ["Alchemy"] = "",
        ["Inscribe"] = "",
        ["Cooking"] = "",
    };

    // Guild promotion (Part E): the craft skill needed for rank index 0 to 5.
    public double[] PromotionSkill { get; init; } = [0.0, 30.0, 50.0, 70.0, 90.0, 100.0];

    // Rank 5's Band V starter set: this many scrolls per craft skill the guild owns.
    public int Rank5StarterScrolls { get; init; } = 2;

    // Band V rotation size in the shop at rank index 4 and 5. The rotation changes once a day.
    public int[] RotationSize { get; init; } = [3, 5];

    // Guilds whose rank 5 trial is waived even when guilds.trialsWaivedUntilBuilt is false.
    public string[] TrialWaivedGuilds { get; init; } = [];

    // Part F. A large BOD's chance at a Band V scroll for its craft; a recurring client's chance at a custom recipe.
    public double LargeBodScrollChance { get; init; } = 0.02;
    public double CommissionScrollChance { get; init; } = 0.05;
    public string[] CommissionScrolls { get; init; } = [];

    // Part F: tables loot boxes, bosses and quests draw from. Epic tables (F-42) pay only from bosses and boxes.
    public RecipeRewardTable[] RewardTables { get; init; } =
    [
        new("bandV", false, ["bandV:any"]),
        new("epic", true, []),
        // cc-P142: what a monster steal's recipe draw pays three times in four (StealLootTables.DenRecipeKeys mirrors
        // this list for a file whose rewardTables lack it): the three Den recipes and the Shadow Cloak schematics.
        new("thieves", false, ["recipe:9400", "recipe:9401", "recipe:9402", "recipe:9121", "recipe:9122", "recipe:9123"]),
    ];

    public static RecipeGatingTable Defaults { get; } = new();

    public static RecipeGatingTable Current { get; set; } = Defaults;

    // -- Lookups --------------------------------------------------------------------------------------------

    /// <summary>0 for a free item, else the band number the minimum skill falls in (the last band above 200).</summary>
    public int BandOf(double minSkill)
    {
        if (minSkill <= FreeUpTo)
        {
            return 0;
        }

        foreach (var band in Bands)
        {
            if (minSkill <= band.Ceiling)
            {
                return band.Number;
            }
        }

        return Bands[^1].Number;
    }

    public RecipeBand? Band(int number) => Array.Find(Bands, b => b.Number == number);

    public bool IsBookBand(int number) => Band(number)?.Book == true;

    public string BandName(int number) => Band(number)?.Name ?? number.ToString();

    public double FloorOf(int number) => Band(number)?.Floor ?? FreeUpTo;

    public int LastBookBand
    {
        get
        {
            var last = 0;
            foreach (var band in Bands)
            {
                if (band.Book)
                {
                    last = Math.Max(last, band.Number);
                }
            }

            return last;
        }
    }

    public int PriceOfBook(int band) => band >= 1 && band <= BookPrice.Length ? BookPrice[band - 1] : BookPrice[^1];

    public RecipeRewardTable? RewardTable(string name) =>
        Array.Find(RewardTables, t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // -- The file -------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private sealed class FileForm
    {
        public string? About { get; set; }
        public double? FreeUpTo { get; set; }
        public RecipeBand[]? Bands { get; set; }
        public int? MergeBelow { get; set; }
        public int[]? BookPrice { get; set; }
        public int? BandVScrollPrice { get; set; }
        public int? BandVMaterialAmount { get; set; }
        public Dictionary<string, string>? BandVMaterial { get; set; }
        public double[]? PromotionSkill { get; set; }
        public int? Rank5StarterScrolls { get; set; }
        public int[]? RotationSize { get; set; }
        public string[]? TrialWaivedGuilds { get; set; }
        public double? LargeBodScrollChance { get; set; }
        public double? CommissionScrollChance { get; set; }
        public string[]? CommissionScrolls { get; set; }
        public RecipeRewardTable[]? RewardTables { get; set; }
    }

    public static string ToJson(RecipeGatingTable t) =>
        JsonSerializer.Serialize(
            new FileForm
            {
                About = "Recipe-gated crafting (cc-P78, F-43). Bands by an item's minimum skill (floor exclusive, ceiling " +
                        "inclusive); book bands are taught by one pattern book per craft skill, the rest by single scrolls. " +
                        "Prices are guild currency. Edit and run [RecipeGating reload; a bad file is refused whole.",
                FreeUpTo = t.FreeUpTo,
                Bands = t.Bands,
                MergeBelow = t.MergeBelow,
                BookPrice = t.BookPrice,
                BandVScrollPrice = t.BandVScrollPrice,
                BandVMaterialAmount = t.BandVMaterialAmount,
                BandVMaterial = t.BandVMaterial,
                PromotionSkill = t.PromotionSkill,
                Rank5StarterScrolls = t.Rank5StarterScrolls,
                RotationSize = t.RotationSize,
                TrialWaivedGuilds = t.TrialWaivedGuilds,
                LargeBodScrollChance = t.LargeBodScrollChance,
                CommissionScrollChance = t.CommissionScrollChance,
                CommissionScrolls = t.CommissionScrolls,
                RewardTables = t.RewardTables
            },
            Json
        );

    /// <summary>
    /// Reads a table. Returns null and fills errors when anything is wrong; accepted whole or not at all. A field left
    /// out keeps its default. Checked: syntax; bands numbered 1, 2, 3 ... in order, each floor the ceiling before it,
    /// ceilings rising, book bands first; the first floor is FreeUpTo; prices and counts not negative and one price per
    /// book band; six promotion skills, rising; two rotation sizes; chances 0 to 1; reward tables named once each.
    /// </summary>
    public static RecipeGatingTable? Parse(string json, string source, List<string> errors)
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

        var d = Defaults;
        var t = new RecipeGatingTable
        {
            Source = source,
            FreeUpTo = f.FreeUpTo ?? d.FreeUpTo,
            Bands = f.Bands ?? d.Bands,
            MergeBelow = f.MergeBelow ?? d.MergeBelow,
            BookPrice = f.BookPrice ?? d.BookPrice,
            BandVScrollPrice = f.BandVScrollPrice ?? d.BandVScrollPrice,
            BandVMaterialAmount = f.BandVMaterialAmount ?? d.BandVMaterialAmount,
            BandVMaterial = f.BandVMaterial != null
                ? new Dictionary<string, string>(f.BandVMaterial, StringComparer.OrdinalIgnoreCase)
                : d.BandVMaterial,
            PromotionSkill = f.PromotionSkill ?? d.PromotionSkill,
            Rank5StarterScrolls = f.Rank5StarterScrolls ?? d.Rank5StarterScrolls,
            RotationSize = f.RotationSize ?? d.RotationSize,
            TrialWaivedGuilds = f.TrialWaivedGuilds ?? d.TrialWaivedGuilds,
            LargeBodScrollChance = f.LargeBodScrollChance ?? d.LargeBodScrollChance,
            CommissionScrollChance = f.CommissionScrollChance ?? d.CommissionScrollChance,
            CommissionScrolls = f.CommissionScrolls ?? d.CommissionScrolls,
            RewardTables = f.RewardTables ?? d.RewardTables
        };

        if (t.Bands.Length == 0)
        {
            errors.Add("bands: at least one band is needed.");
        }

        var floor = t.FreeUpTo;
        var seenScroll = false;
        var books = 0;
        for (var i = 0; i < t.Bands.Length; i++)
        {
            var b = t.Bands[i];
            if (b == null)
            {
                errors.Add($"bands[{i}]: empty.");
                continue;
            }

            if (b.Number != i + 1)
            {
                errors.Add($"bands[{i}]: number {b.Number}, expected {i + 1}.");
            }

            if (Math.Abs(b.Floor - floor) > 0.0001)
            {
                errors.Add($"band {b.Name}: floor {b.Floor}, expected {floor} (the ceiling before it).");
            }

            if (b.Ceiling <= b.Floor)
            {
                errors.Add($"band {b.Name}: ceiling {b.Ceiling} is not above its floor {b.Floor}.");
            }

            if (string.IsNullOrWhiteSpace(b.Name))
            {
                errors.Add($"bands[{i}]: no name.");
            }

            if (b.Book)
            {
                books++;
                if (seenScroll)
                {
                    errors.Add($"band {b.Name}: a book band after a scroll band.");
                }
            }
            else
            {
                seenScroll = true;
            }

            floor = b.Ceiling;
        }

        if (t.MergeBelow < 0)
        {
            errors.Add("mergeBelow: negative.");
        }

        if (t.BookPrice.Length != books)
        {
            errors.Add($"bookPrice: {t.BookPrice.Length} prices for {books} book bands.");
        }

        if (Array.Exists(t.BookPrice, p => p < 0) || t.BandVScrollPrice < 0 || t.BandVMaterialAmount < 0 ||
            t.Rank5StarterScrolls < 0 || Array.Exists(t.RotationSize, r => r < 0))
        {
            errors.Add("a price, amount or count is negative.");
        }

        if (t.PromotionSkill.Length != 6)
        {
            errors.Add($"promotionSkill: {t.PromotionSkill.Length} values, expected 6 (rank index 0 to 5).");
        }
        else
        {
            for (var i = 1; i < 6; i++)
            {
                if (t.PromotionSkill[i] < t.PromotionSkill[i - 1])
                {
                    errors.Add($"promotionSkill: rank {i} needs less skill than rank {i - 1}.");
                }
            }
        }

        if (t.RotationSize.Length != 2)
        {
            errors.Add($"rotationSize: {t.RotationSize.Length} values, expected 2 (rank index 4 and 5).");
        }

        if (t.LargeBodScrollChance is < 0 or > 1 || t.CommissionScrollChance is < 0 or > 1)
        {
            errors.Add("a chance is outside 0 to 1.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in t.RewardTables)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.Name) || !names.Add(table.Name))
            {
                errors.Add($"rewardTables: a table is unnamed or named twice ({table?.Name}).");
            }
        }

        return errors.Count == 0 ? t : null;
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
                File.WriteAllText(path, ToJson(Defaults));
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
            foreach (var e in errors)
            {
                lines.Add($"  {e}");
            }

            return lines;
        }

        Current = table;
        ClusterFRecipeGate.ClearCache();
        return [$"Loaded the recipe gating table from {source}."];
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"Free up to {FreeUpTo}; ");
        foreach (var b in Bands)
        {
            sb.Append($"{b.Name} {b.Floor}-{b.Ceiling} {(b.Book ? "book" : "scroll")}; ");
        }

        sb.Append($"merge below {MergeBelow}.");
        return sb.ToString();
    }
}
