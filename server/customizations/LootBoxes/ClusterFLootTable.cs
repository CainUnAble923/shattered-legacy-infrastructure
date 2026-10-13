// cc-P79 (F-40): the loot box numbers, in one table Chase can tune. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
//
// Loaded from Configuration/loot-boxes.json the way cc-P48's League ladder is: a missing file is written with the
// built-in defaults below; a file that fails a check is refused whole and the table in force is kept; [LootBox reload
// reads it again. The test host runs no Configure and uses the defaults. Every number here is a placeholder for
// playtesting (F-40, "numbers unchanged").

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Items;

namespace Server.LootBoxes;

public enum BoxTheme
{
    Miner,
    Woodsman,
    Artisan,
    Hunter,
    Explorer,
    Tamer,
    Scholar,
    Boss,
    Loop,
    League
}

public enum BoxPool
{
    Consumables,
    Resources,
    MagicGear,
    Recipes,
    Cosmetics,
    Rare
}

/// <summary>A named step of a ladder: its display name and its hue (a CraftResource name, or a hue number).</summary>
public sealed record LadderStep(string Name, string Resource, int Hue);

public sealed class LootTable
{
    public const string FileName = "loot-boxes.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string Source { get; init; } = "built-in defaults";

    // Slots per tier 1 to 6.
    public int[] Slots { get; init; } = [2, 3, 3, 4, 5, 6];

    // The currency slot: gold at every tier, Renown from tier 2.
    public int[] Gold { get; init; } = [250, 750, 2_000, 5_000, 12_000, 30_000];
    public int[] Renown { get; init; } = [0, 25, 50, 100, 200, 400];

    // General pool weights by tier 1 to 6.
    public Dictionary<string, int[]> PoolWeights { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Consumables"] = [50, 40, 30, 20, 15, 10],
        ["Resources"] = [30, 30, 25, 20, 15, 10],
        ["MagicGear"] = [15, 20, 25, 25, 25, 20],
        ["Recipes"] = [5, 8, 12, 15, 15, 15],
        ["Cosmetics"] = [0, 2, 6, 12, 15, 20],
        ["Rare"] = [0, 0, 2, 8, 15, 25],
    };

    // Box names and hues. Resource themes use their ladder; the rest use rarity.
    public LadderStep[] Rarity { get; init; } =
    [
        new("Common", "", 0x0386),
        new("Uncommon", "", 0x0044),
        new("Rare", "", 0x0059),
        new("Epic", "", 0x0013),
        new("Legendary", "", 0x002B),
        new("Celestial", "Celestial", 0),
    ];

    public LadderStep[] MetalLadder { get; init; } =
    [
        new("Dull Copper", "DullCopper", 0), new("Bronze", "Bronze", 0), new("Verite", "Verite", 0),
        new("Valorite", "Valorite", 0), new("Mythril", "Mythril", 0), new("Celestial", "Celestial", 0),
    ];

    // There is no leather past Barbed; the top step borrows the Celestial metal hue.
    public LadderStep[] LeatherLadder { get; init; } =
    [
        new("Leather", "RegularLeather", 0), new("Spined", "SpinedLeather", 0), new("Horned", "HornedLeather", 0),
        new("Barbed", "BarbedLeather", 0), new("Barbed", "BarbedLeather", 0), new("Celestial", "Celestial", 0),
    ];

    public LadderStep[] WoodLadder { get; init; } =
    [
        new("Oak", "OakWood", 0), new("Yew", "YewWood", 0), new("Heartwood", "Heartwood", 0),
        new("Frostwood", "Frostwood", 0), new("Runewood", "Runewood", 0), new("Celestial", "Celestial", 0),
    ];

    // Tier by source. Achievements: by AP (the ceilings for tiers 1 to 5; above the last, tier 6), with overrides by key.
    public int[] AchievementApCeilings { get; init; } = [10, 25, 50, 100, 250];
    public Dictionary<string, int> AchievementTier { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // Achievement category to box theme.
    public Dictionary<string, string> CategoryTheme { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Combat"] = "Hunter",
        ["Skills"] = "Scholar",
        ["Exploration"] = "Explorer",
        ["Mining"] = "Miner",
        ["Crafting"] = "Artisan",
        ["Legacy"] = "Artisan",
        ["Discovery"] = "Explorer",
        ["Collection"] = "Scholar",
        ["League"] = "League",
    };

    // Bosses: tier by type name (or a base class name), default for the rest.
    public int BossDefaultTier { get; init; } = 4;
    public Dictionary<string, int> BossTier { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseChampion"] = 5,
        ["BasePeerless"] = 5,
        ["Harrower"] = 6,
    };

    // League rank index 1 to 17 (Iron to Celestial) to tier.
    public int[] LeagueRankTier { get; init; } = [1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6];

    public int DungeonClearTier { get; init; } = 3;
    public int LoopTier { get; init; } = 6;

    // A box opened but not emptied (a full bank) is cleaned up after this many days.
    public int AbandonedBoxDays { get; init; } = 7;

    public static LootTable Defaults { get; } = new();

    public static LootTable Current { get; set; } = Defaults;

    // -- Lookups --

    public int SlotsFor(int tier) => Slots[Math.Clamp(tier, 1, 6) - 1];

    public int TierForAchievement(string key, int ap)
    {
        if (AchievementTier.TryGetValue(key, out var tier))
        {
            return Math.Clamp(tier, 1, 6);
        }

        for (var i = 0; i < AchievementApCeilings.Length; i++)
        {
            if (ap <= AchievementApCeilings[i])
            {
                return i + 1;
            }
        }

        return 6;
    }

    public BoxTheme ThemeForCategory(string category) =>
        CategoryTheme.TryGetValue(category, out var name) && Enum.TryParse<BoxTheme>(name, true, out var theme)
            ? theme
            : BoxTheme.Explorer;

    public int TierForBoss(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            if (BossTier.TryGetValue(t.Name, out var tier))
            {
                return Math.Clamp(tier, 1, 6);
            }
        }

        return Math.Clamp(BossDefaultTier, 1, 6);
    }

    public int TierForLeagueRank(int rank) => LeagueRankTier[Math.Clamp(rank, 1, LeagueRankTier.Length) - 1];

    public int Weight(BoxPool pool, int tier) =>
        PoolWeights.TryGetValue(pool.ToString(), out var w) && w.Length >= 6 ? w[Math.Clamp(tier, 1, 6) - 1] : 0;

    public static int HueOf(LadderStep step)
    {
        if (!string.IsNullOrEmpty(step.Resource) && Enum.TryParse<CraftResource>(step.Resource, true, out var res))
        {
            return CraftResources.GetHue(res);
        }

        return step.Hue;
    }

    // -- The file --

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
        public int[]? Slots { get; set; }
        public int[]? Gold { get; set; }
        public int[]? Renown { get; set; }
        public Dictionary<string, int[]>? PoolWeights { get; set; }
        public LadderStep[]? Rarity { get; set; }
        public LadderStep[]? MetalLadder { get; set; }
        public LadderStep[]? LeatherLadder { get; set; }
        public LadderStep[]? WoodLadder { get; set; }
        public int[]? AchievementApCeilings { get; set; }
        public Dictionary<string, int>? AchievementTier { get; set; }
        public Dictionary<string, string>? CategoryTheme { get; set; }
        public int? BossDefaultTier { get; set; }
        public Dictionary<string, int>? BossTier { get; set; }
        public int[]? LeagueRankTier { get; set; }
        public int? DungeonClearTier { get; set; }
        public int? LoopTier { get; set; }
        public int? AbandonedBoxDays { get; set; }
    }

    public static string ToJson(LootTable t) =>
        JsonSerializer.Serialize(
            new FileForm
            {
                About = "Loot boxes (cc-P79, F-40). Six tiers; arrays are tier 1 to 6. Pool weights are placeholders for " +
                        "playtesting. Edit and run [LootBox reload; a bad file is refused whole.",
                Slots = t.Slots, Gold = t.Gold, Renown = t.Renown, PoolWeights = t.PoolWeights, Rarity = t.Rarity,
                MetalLadder = t.MetalLadder, LeatherLadder = t.LeatherLadder, WoodLadder = t.WoodLadder,
                AchievementApCeilings = t.AchievementApCeilings, AchievementTier = t.AchievementTier,
                CategoryTheme = t.CategoryTheme, BossDefaultTier = t.BossDefaultTier, BossTier = t.BossTier,
                LeagueRankTier = t.LeagueRankTier, DungeonClearTier = t.DungeonClearTier, LoopTier = t.LoopTier,
                AbandonedBoxDays = t.AbandonedBoxDays
            },
            Json
        );

    /// <summary>Reads a table; null and errors when anything is wrong. A field left out keeps its default.</summary>
    public static LootTable? Parse(string json, string source, List<string> errors)
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
        var t = new LootTable
        {
            Source = source,
            Slots = f.Slots ?? d.Slots,
            Gold = f.Gold ?? d.Gold,
            Renown = f.Renown ?? d.Renown,
            PoolWeights = f.PoolWeights != null ? new Dictionary<string, int[]>(f.PoolWeights, StringComparer.OrdinalIgnoreCase) : d.PoolWeights,
            Rarity = f.Rarity ?? d.Rarity,
            MetalLadder = f.MetalLadder ?? d.MetalLadder,
            LeatherLadder = f.LeatherLadder ?? d.LeatherLadder,
            WoodLadder = f.WoodLadder ?? d.WoodLadder,
            AchievementApCeilings = f.AchievementApCeilings ?? d.AchievementApCeilings,
            AchievementTier = f.AchievementTier != null ? new Dictionary<string, int>(f.AchievementTier, StringComparer.OrdinalIgnoreCase) : d.AchievementTier,
            CategoryTheme = f.CategoryTheme != null ? new Dictionary<string, string>(f.CategoryTheme, StringComparer.OrdinalIgnoreCase) : d.CategoryTheme,
            BossDefaultTier = f.BossDefaultTier ?? d.BossDefaultTier,
            BossTier = f.BossTier != null ? new Dictionary<string, int>(f.BossTier, StringComparer.OrdinalIgnoreCase) : d.BossTier,
            LeagueRankTier = f.LeagueRankTier ?? d.LeagueRankTier,
            DungeonClearTier = f.DungeonClearTier ?? d.DungeonClearTier,
            LoopTier = f.LoopTier ?? d.LoopTier,
            AbandonedBoxDays = f.AbandonedBoxDays ?? d.AbandonedBoxDays
        };

        void SixOf(string name, Array a)
        {
            if (a.Length != 6)
            {
                errors.Add($"{name}: {a.Length} values, expected 6 (tier 1 to 6).");
            }
        }

        SixOf("slots", t.Slots);
        SixOf("gold", t.Gold);
        SixOf("renown", t.Renown);
        SixOf("rarity", t.Rarity);
        SixOf("metalLadder", t.MetalLadder);
        SixOf("leatherLadder", t.LeatherLadder);
        SixOf("woodLadder", t.WoodLadder);

        foreach (var pool in Enum.GetNames<BoxPool>())
        {
            if (!t.PoolWeights.TryGetValue(pool, out var w))
            {
                errors.Add($"poolWeights: no row for {pool}.");
            }
            else if (w.Length != 6 || Array.Exists(w, x => x < 0))
            {
                errors.Add($"poolWeights.{pool}: needs 6 weights, none negative.");
            }
        }

        if (Array.Exists(t.Slots, s => s < 2))
        {
            errors.Add("slots: every tier needs at least 2 (the theme and currency slots).");
        }

        if (Array.Exists(t.Gold, g => g < 0) || Array.Exists(t.Renown, r => r < 0))
        {
            errors.Add("gold or renown: negative.");
        }

        if (t.AchievementApCeilings.Length != 5)
        {
            errors.Add($"achievementApCeilings: {t.AchievementApCeilings.Length} values, expected 5 (tiers 1 to 5).");
        }

        foreach (var (key, theme) in t.CategoryTheme)
        {
            if (!Enum.TryParse<BoxTheme>(theme, true, out _))
            {
                errors.Add($"categoryTheme.{key}: '{theme}' is not a box theme.");
            }
        }

        if (t.LeagueRankTier.Length == 0 || Array.Exists(t.LeagueRankTier, x => x is < 1 or > 6))
        {
            errors.Add("leagueRankTier: needs tiers 1 to 6.");
        }

        if (t.AbandonedBoxDays < 1)
        {
            errors.Add("abandonedBoxDays: at least 1.");
        }

        return errors.Count == 0 ? t : null;
    }

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

        try
        {
            return Apply(File.ReadAllText(path), path);
        }
        catch (Exception ex)
        {
            return [$"Could not read {path} ({ex.Message}); kept {Current.Source}."];
        }
    }

    public static List<string> Apply(string json, string source)
    {
        var errors = new List<string>();
        var table = Parse(json, source, errors);
        if (table == null)
        {
            var lines = new List<string> { $"{FileName} REFUSED ({errors.Count} error{(errors.Count == 1 ? "" : "s")}); kept {Current.Source}:" };
            errors.ForEach(e => lines.Add($"  {e}"));
            return lines;
        }

        Current = table;
        return [$"Loaded the loot box table from {source}."];
    }
}
