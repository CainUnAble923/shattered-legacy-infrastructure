// cc-P82: the Artificers' Order Imbuing rework, the data. Design: shard-migration docs/artificers-imbuing-design.md.
// Notes: shard-migration notes/cc-P82-artificers-imbuing.md.
//
// Every number Chase may tune is in one table, loaded from Configuration/imbuing.json the way cc-P78's recipe gating
// table is: a missing file is written with the built-in defaults; a file that fails any check is refused whole and the
// table in force is kept; [ImbueKnowledge reload reads it again. The test host runs no Configure, so it uses the defaults.
//
// What is here: the tier fractions and every Overcharged maximum; the property ladder (the rank each property is
// learned at); the formula books (their contents are data, one item class reads them); the Master Formulary's effect;
// where imbuing may reach which tier (soul forges, the mule socket); and the custom properties' values, ingredients and
// effect numbers.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server;

/// <summary>A formula book: what reading it teaches the account. Rank is the Artificers' rank index (0 to 5) it is sold at.</summary>
public sealed class ImbueBookConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public int Price { get; set; }
    public bool MasterFormulary { get; set; }
    public List<ImbueGrantConfig> Grants { get; set; } = [];
}

public sealed class ImbueGrantConfig
{
    public string Property { get; set; } = "";
    public string Tier { get; set; } = "Basic";
}

/// <summary>A custom property (design table): its values by tier, what it may go on, what it costs.</summary>
public sealed class ImbueCustomConfig
{
    public string Effect { get; set; } = "";
    public int Full { get; set; }

    // 0: this property has no Overcharged tier (Fortune's Favor).
    public int Overcharged { get; set; }

    // Any of Weapon, Armor, Jewel, Clothing, comma separated.
    public string Applies { get; set; } = "Weapon,Armor,Jewel,Clothing";
    public string Ingredient { get; set; } = "";
    public int IngredientAmount { get; set; }
    public int Shards { get; set; }
    public int Gold { get; set; }
}

/// <summary>Effect numbers that are not a property's own value.</summary>
public sealed class ImbueEffectConfig
{
    // Shatter: the target's highest resist drops by this much for this long.
    public int ShatterResistDrop { get; set; } = 10;
    public int ShatterSeconds { get; set; } = 5;

    // Butcher's Eye: a carve's rare part, chance in percent at the property's Full value (scaled by value).
    public int ButchersEyeRareChance { get; set; } = 2;
    public string[] ButchersEyeRareParts { get; set; } =
        ["DaemonClaw", "GoblinBlood", "LavaSerpentCrust", "UndyingFlesh", "VialOfVitriol", "SpiderCarapace", "RaptorTeeth",
         "ReflectiveWolfEye", "SlithTongue", "BottleIchor", "ToxicVenomSac", "SilverSnakeSkin", "FaeryDust"];

    // Prospector: the rare finds, by gathering kind.
    public string[] ProspectorMining { get; set; } =
        ["BlueDiamond", "FireRuby", "Turquoise", "DarkSapphire", "EcruCitrine", "PerfectEmerald", "WhitePearl", "CrystallineBlackrock", "PowderedIron"];
    public string[] ProspectorLumber { get; set; } =
        ["BarkFragment", "LuminescentFungi", "ParasiticPlant", "BrilliantAmber", "SwitchItem", "Seed"];

    // Fortune's Favor: the most a character's worn Fortune's Favor counts for, all items together (percent).
    public int FortuneTotalCap { get; set; } = 10;

    // The achievements' window: a box earned this many seconds or less after the luck gear went on.
    public int FortuneEquipWindowSeconds { get; set; } = 60;
}

public sealed class ImbuingTable
{
    public const string FileName = "imbuing.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    public string About { get; set; } =
        "Artificers' Order Imbuing (cc-P82). Tiers: Basic = OSI max x basicFraction, Refined = x refinedFraction, Full = " +
        "the OSI max (the catalogue's), Overcharged = overchargedMax. ladder: the Artificers' rank index a property is " +
        "learned at (free, 1 to 4, or 140 for the 140+ path). books: what each formula book teaches. sites: the highest " +
        "tier each place allows. Edit and run [ImbueKnowledge reload; a bad file is refused whole.";

    public double BasicFraction { get; set; } = 0.333;
    public double RefinedFraction { get; set; } = 0.667;

    // Overcharged maxima by catalogue property (the 200 skill cap's tier). A property left out has no Overcharged tier.
    public Dictionary<string, int> OverchargedMax { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Ladder { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ImbueBookConfig> Books { get; set; } = [];

    // Property slots on an item before the Master Formulary.
    public int BaseSlots { get; set; } = 5;

    // The Master Formulary: "slot" adds MasterExtraSlots; "budget" keeps the slots and raises the item's total
    // intensity budget (sum of each property's value over its Full value, in percent) from BaseSlots x 100 by
    // MasterBudgetPercent.
    public string MasterMode { get; set; } = "slot";
    public int MasterExtraSlots { get; set; } = 1;
    public int MasterBudgetPercent { get; set; } = 100;

    // The highest tier each place allows: Anywhere (the toolkit, the guildmaster), MuleII and MuleIII (the Imbued Barding
    // Socket's owner near the mule), Forge (a soul forge: the Queen's or an Artificers' hall forge).
    public Dictionary<string, string> SiteCaps { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Anywhere"] = "Full",
        ["MuleII"] = "Refined",
        ["MuleIII"] = "Full",
        ["Forge"] = "Overcharged",
    };

    // Success bonus in percentage points: a soul forge (and the mule at III), and the Queen's own soulforge.
    public int ForgeBonusPercent { get; set; } = 2;
    public int QueensForgeBonusPercent { get; set; } = 6;
    public int ForgeRange { get; set; } = 2;
    public int MuleRange { get; set; } = 3;

    // Crafter gear: what crafting and gathering +skill may go on, besides any item the guilds mark later (type names).
    public string[] CrafterGearTypes { get; set; } = ["HalfApron", "FullApron"];

    public Dictionary<string, ImbueCustomConfig> Custom { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ImbueEffectConfig Effects { get; set; } = new();

    // -- Defaults ------------------------------------------------------------------------------------------------

    public const string Free = "free";
    public const string Path140 = "140";

    public static readonly string[] Tiers = ["Basic", "Refined", "Full", "Overcharged"];

    /// <summary>The design doc's ladder, by knowledge key (a catalogue property, or a family of them).</summary>
    public static Dictionary<string, string> DefaultLadder()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void At(string rank, params string[] keys)
        {
            foreach (var k in keys)
            {
                d[k] = rank;
            }
        }

        At(Free,
            "Physical Resist Bonus", "Fire Resist Bonus", "Cold Resist Bonus", "Poison Resist Bonus", "Energy Resist Bonus",
            "Bonus Strength", "Bonus Dexterity", "Bonus Intelligence", "Hit Point Increase", "Stamina Increase", "Mana Increase",
            "Hit Points Regen", "Stamina Regen", "Mana Regen", "Night Sight", "Luck", "Lower Stat Req (Weapon)",
            "Lower Stat Req (Armor)", "Durability (Weapon)", "Durability (Armor)", "Self Repair", "Self Repair (Armor)",
            "Lower Reagent Cost", "Enhance Potions");
        At("1",
            "Hit Life Leech", "Hit Mana Leech", "Hit Stamina Leech", "Hit Lower Attack", "Hit Lower Defense",
            ImbueFamilies.ElementalDamage, "Balanced", "Velocity", "Spell Channeling", "Mage Armor",
            ImbueFamilies.JewelrySkill, ImbueFamilies.CrafterSkill);
        At("2",
            "Hit Magic Arrow", "Hit Harm", "Hit Fireball", "Hit Lightning", "Hit Dispel", "Damage Increase", "Mage Weapon",
            "Reflect Physical Damage");
        At("3",
            "Hit Cold Area", "Hit Fire Area", "Hit Energy Area", "Hit Poison Area", "Hit Physical Area", ImbueFamilies.Slayer,
            "Hit Chance Increase", "Defense Chance Increase", "Spell Damage Increase", "Lower Mana Cost",
            "Bountiful", "Mastercraft", "Prospector", "Butcher's Eye");
        At("4",
            "Faster Casting", "Faster Cast Recovery", "Swing Speed Increase", ImbueFamilies.SuperSlayer, "Casting Focus");
        At(Path140,
            "Echo of the Loop", "Time Slip", "Shatter", "Bond", "Spell Leech", "Renowned", "Fortune's Favor",
            "Splintering", "Reactive Paralyze", "Soul Charge");
        return d;
    }

    /// <summary>
    /// The design's rule as books: learned at Basic, Refined one rank later, Full two ranks later. So the rank r book
    /// teaches, for every property learned at rank L from r - 2 to r, tier r - L + 1 (at most Full). Rank 5 teaches no new
    /// property: it is the Master Formulary (rank 3's Full, rank 4's Refined, and the item budget).
    /// </summary>
    public static List<ImbueBookConfig> BooksFromLadder(Dictionary<string, string> ladder, int[] prices)
    {
        string[] names = ["Apprentice Formulary", "Journeyman Formulary", "Artificer's Formulary", "Arcane Formulary", "Master Formulary"];
        var books = new List<ImbueBookConfig>();

        for (var r = 1; r <= 5; r++)
        {
            var book = new ImbueBookConfig
            {
                Id = names[r - 1].Replace("'", "").Replace(' ', '-').ToLowerInvariant(),
                Name = names[r - 1],
                Rank = r,
                Price = prices[r - 1],
                MasterFormulary = r == 5,
            };

            foreach (var (key, at) in ladder.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!TryRankOf(at, out var learned))
                {
                    continue; // the 140+ path
                }

                var tier = r - learned + 1;
                if (learned <= r && tier is >= 1 and <= 3)
                {
                    book.Grants.Add(new ImbueGrantConfig { Property = key, Tier = Tiers[tier - 1] });
                }
            }

            books.Add(book);
        }

        return books;
    }

    /// <summary>"free" is 0, "1" to "4" themselves; false for the 140+ path.</summary>
    public static bool TryRankOf(string at, out int rank)
    {
        if (string.Equals(at, Free, StringComparison.OrdinalIgnoreCase))
        {
            rank = 0;
            return true;
        }

        return int.TryParse(at, out rank) && rank is >= 1 and <= 4;
    }

    public static Dictionary<string, ImbueCustomConfig> DefaultCustom() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Echo of the Loop"] = new() { Effect = "% XP gain for XmlLevelItems and player levels", Full = 10, Overcharged = 15, Ingredient = "UndyingFlesh", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Time Slip"] = new() { Effect = "% chance on hit to refund a special move's mana and cooldown", Full = 5, Overcharged = 8, Applies = "Weapon", Ingredient = "VialOfVitriol", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Shatter"] = new() { Effect = "% chance on hit to lower the target's highest resist briefly", Full = 10, Overcharged = 15, Applies = "Weapon", Ingredient = "CrushedGlass", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Bond"] = new() { Effect = "% companion damage and companion XP", Full = 10, Overcharged = 15, Applies = "Armor,Jewel,Clothing", Ingredient = "ReflectiveWolfEye", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Spell Leech"] = new() { Effect = "% of spell damage dealt heals the caster", Full = 5, Overcharged = 8, Applies = "Weapon,Jewel", Ingredient = "DaemonClaw", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Renowned"] = new() { Effect = "% Renown from League commissions and hunts", Full = 10, Overcharged = 15, Ingredient = "GoblinBlood", IngredientAmount = 5, Shards = 25, Gold = 12000 },
        ["Bountiful"] = new() { Effect = "% bonus gathering yield", Full = 15, Overcharged = 22, Applies = "Armor,Jewel,Clothing", Ingredient = "SpiderCarapace", IngredientAmount = 5, Shards = 15, Gold = 8000 },
        ["Mastercraft"] = new() { Effect = "% exceptional chance when crafting", Full = 5, Overcharged = 8, Applies = "Armor,Jewel,Clothing", Ingredient = "LavaSerpentCrust", IngredientAmount = 5, Shards = 15, Gold = 8000 },
        ["Prospector"] = new() { Effect = "% chance at a rare find while gathering", Full = 10, Overcharged = 15, Applies = "Armor,Jewel,Clothing", Ingredient = "SlithTongue", IngredientAmount = 5, Shards = 15, Gold = 8000 },
        ["Butcher's Eye"] = new() { Effect = "% bonus monster parts when carving, small chance at a rare part", Full = 20, Overcharged = 30, Ingredient = "RaptorTeeth", IngredientAmount = 5, Shards = 15, Gold = 8000 },
        ["Fortune's Favor"] = new() { Effect = "% chance a loot box rolls its contents one quality tier higher (never more items)", Full = 5, Overcharged = 0, Ingredient = "FaeryDust", IngredientAmount = 5, Shards = 30, Gold = 15000 },
    };

    private static ImbuingTable? _defaults;

    /// <summary>The built-in table. Built on first use (the Overcharged list reads the catalogue).</summary>
    public static ImbuingTable Defaults => _defaults ??= BuildDefaults();

    private static ImbuingTable? _current;

    public static ImbuingTable Current
    {
        get => _current ?? Defaults;
        set => _current = value;
    }

    public static ImbuingTable BuildDefaults()
    {
        var t = new ImbuingTable { Ladder = DefaultLadder(), Custom = DefaultCustom() };

        foreach (var def in ImbueCatalogue.All)
        {
            if (!def.IsCustom && !def.IsBool)
            {
                t.OverchargedMax[def.Name] = def.MaxGuild;
            }
        }

        t.Books = BooksFromLadder(t.Ladder, [250, 500, 1_000, 2_000, 4_000]);
        return t;
    }

    // -- Lookups ---------------------------------------------------------------------------------------------------

    public ImbueBookConfig? Book(string id) => Books.Find(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public ImbueCustomConfig? CustomOf(string name) => Custom.TryGetValue(name, out var c) ? c : null;

    public ImbueTier SiteCap(string site) =>
        SiteCaps.TryGetValue(site, out var s) && Enum.TryParse<ImbueTier>(s, true, out var t) ? t : ImbueTier.Full;

    public bool BudgetMode => MasterMode.Equals("budget", StringComparison.OrdinalIgnoreCase);

    // -- The file --------------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson(ImbuingTable t) => JsonSerializer.Serialize(t, Json);

    /// <summary>
    /// Reads a table; null and errors when anything is wrong (accepted whole or not at all). A field left out keeps its
    /// default. Checked: syntax; fractions rising inside 0 to 1; every Overcharged maximum at or above its Full value;
    /// every ladder entry a known key at a known rank; every book's id, rank (1 to 5), price and grants (known keys, real
    /// tiers, never Overcharged); one Master Formulary; slots, budget and site caps sane; every custom property known,
    /// Full above 0, Fortune's Favor without Overcharged; every type name real.
    /// </summary>
    public static ImbuingTable? Parse(string json, string source, List<string> errors)
    {
        ImbuingTable? t;
        try
        {
            t = JsonSerializer.Deserialize<ImbuingTable>(json, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return null;
        }

        if (t == null)
        {
            errors.Add("the file is empty.");
            return null;
        }

        t.Source = source;
        var d = Defaults;
        t.OverchargedMax = new Dictionary<string, int>(t.OverchargedMax ?? d.OverchargedMax, StringComparer.OrdinalIgnoreCase);
        t.Ladder = new Dictionary<string, string>(t.Ladder is { Count: > 0 } ? t.Ladder : d.Ladder, StringComparer.OrdinalIgnoreCase);
        t.SiteCaps = new Dictionary<string, string>(t.SiteCaps ?? d.SiteCaps, StringComparer.OrdinalIgnoreCase);
        t.Custom = new Dictionary<string, ImbueCustomConfig>(t.Custom is { Count: > 0 } ? t.Custom : d.Custom, StringComparer.OrdinalIgnoreCase);
        t.Books = t.Books is { Count: > 0 } ? t.Books : d.Books;
        t.Effects ??= new ImbueEffectConfig();
        t.CrafterGearTypes ??= d.CrafterGearTypes;

        if (t.BasicFraction is <= 0 or >= 1 || t.RefinedFraction is <= 0 or >= 1 || t.RefinedFraction <= t.BasicFraction)
        {
            errors.Add("basicFraction and refinedFraction must rise inside 0 to 1.");
        }

        foreach (var (name, max) in t.OverchargedMax)
        {
            var def = ImbueCatalogue.All.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (def == null || def.IsCustom)
            {
                errors.Add($"overchargedMax: '{name}' is not a catalogue property (custom properties set theirs under custom).");
            }
            else if (max < def.MaxVanilla)
            {
                errors.Add($"overchargedMax: '{name}' {max} is below its Full value {def.MaxVanilla}.");
            }
        }

        foreach (var (key, at) in t.Ladder)
        {
            if (!ImbueFamilies.IsKnowledgeKey(key))
            {
                errors.Add($"ladder: '{key}' is not a property or family the catalogue or the grant list knows.");
            }

            if (!TryRankOf(at, out _) && at != Path140)
            {
                errors.Add($"ladder: '{key}' at '{at}'; expected free, 1 to 4, or 140.");
            }
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var masters = 0;
        foreach (var b in t.Books)
        {
            if (b == null || string.IsNullOrWhiteSpace(b.Id) || !ids.Add(b.Id) || string.IsNullOrWhiteSpace(b.Name))
            {
                errors.Add($"books: a book has no id or name, or its id is used twice ({b?.Id}).");
                continue;
            }

            if (b.Rank is < 1 or > 5 || b.Price < 0)
            {
                errors.Add($"book {b.Id}: rank {b.Rank} (1 to 5) or price {b.Price} (not negative).");
            }

            masters += b.MasterFormulary ? 1 : 0;
            foreach (var g in b.Grants ?? [])
            {
                if (!ImbueFamilies.IsKnowledgeKey(g.Property))
                {
                    errors.Add($"book {b.Id}: '{g.Property}' is not a property or family.");
                }

                if (!Enum.TryParse<ImbueTier>(g.Tier, true, out var tier) || tier is < ImbueTier.Basic or > ImbueTier.Full)
                {
                    errors.Add($"book {b.Id}: tier '{g.Tier}' for '{g.Property}' (Basic, Refined or Full; Overcharged is the 140+ path's).");
                }
            }
        }

        if (masters != 1)
        {
            errors.Add($"books: {masters} Master Formularies, expected 1.");
        }

        if (t.BaseSlots is < 1 or > 10 || t.MasterExtraSlots < 0 || t.MasterBudgetPercent < 0)
        {
            errors.Add("baseSlots (1 to 10), masterExtraSlots and masterBudgetPercent (not negative).");
        }

        if (!t.MasterMode.Equals("slot", StringComparison.OrdinalIgnoreCase) && !t.BudgetMode)
        {
            errors.Add($"masterMode '{t.MasterMode}': expected slot or budget.");
        }

        foreach (var site in new[] { "Anywhere", "MuleII", "MuleIII", "Forge" })
        {
            if (!t.SiteCaps.TryGetValue(site, out var cap) || !Enum.TryParse<ImbueTier>(cap, true, out _))
            {
                errors.Add($"siteCaps: '{site}' is missing or not a tier (None, Basic, Refined, Full, Overcharged).");
            }
        }

        if (t.ForgeBonusPercent is < 0 or > 50 || t.QueensForgeBonusPercent is < 0 or > 50 || t.ForgeRange is < 0 or > 18 ||
            t.MuleRange is < 0 or > 18)
        {
            errors.Add("forge bonuses (0 to 50) and ranges (0 to 18).");
        }

        foreach (var (name, c) in t.Custom)
        {
            if (!DefaultCustom().ContainsKey(name))
            {
                errors.Add($"custom: '{name}' is not one of the design's custom properties.");
                continue;
            }

            if (c.Full <= 0 || c.Overcharged < 0 || c.Overcharged is > 0 && c.Overcharged < c.Full || c.Shards < 0 || c.Gold < 0 ||
                c.IngredientAmount < 0)
            {
                errors.Add($"custom {name}: full above 0, overcharged 0 or at least full, costs not negative.");
            }

            if (name == "Fortune's Favor" && c.Overcharged != 0)
            {
                errors.Add("custom Fortune's Favor: no Overcharged tier (the design's rule); set overcharged to 0.");
            }

            if (ImbueFamilies.ParseApplies(c.Applies) == ImbuableItem.None)
            {
                errors.Add($"custom {name}: applies '{c.Applies}' names no item kind.");
            }

            if (c.IngredientAmount > 0 && AssemblyHandler.FindTypeByName(c.Ingredient) == null)
            {
                errors.Add($"custom {name}: ingredient '{c.Ingredient}' is not an item type.");
            }
        }

        foreach (var name in t.CrafterGearTypes.Concat(t.Effects.ButchersEyeRareParts).Concat(t.Effects.ProspectorMining)
                     .Concat(t.Effects.ProspectorLumber))
        {
            if (AssemblyHandler.FindTypeByName(name) == null)
            {
                errors.Add($"'{name}' is not an item type.");
            }
        }

        if (t.Effects.FortuneTotalCap is < 0 or > 100 || t.Effects.FortuneEquipWindowSeconds < 0 || t.Effects.ShatterSeconds < 0 ||
            t.Effects.ShatterResistDrop < 0 || t.Effects.ButchersEyeRareChance is < 0 or > 100)
        {
            errors.Add("effects: a number is negative or a percent is above 100.");
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

    public static List<string> Apply(string json, string source)
    {
        var errors = new List<string>();
        var table = Parse(json, source, errors);

        if (table == null)
        {
            var lines = new List<string> { $"{FileName} REFUSED ({errors.Count} error{(errors.Count == 1 ? "" : "s")}); kept {Current.Source}:" };
            lines.AddRange(errors.Select(e => $"  {e}"));
            return lines;
        }

        Current = table;
        return [$"Loaded the imbuing table from {source}."];
    }
}
