// cc-P150 (F-47, F-48): every number of the requisitions board, the Appraiser and Leads, in one table Chase tunes:
// Configuration/requisitions.json, written with the defaults on first start if missing, read at start and by
// [RequisitionsStaff reload. A bad file is refused whole, every error is printed, and the table in force is kept (the
// LeagueLadderTable pattern, ClusterFLeagueRanks.cs).
//
//   refreshHourUtc  the fixed server time the open requests roll over (ClusterFRequisitions.DayIndex)
//   perGuild        open requests per guild per character (F-47: 3)
//   leagueGate      the highest tier a League rank allows (F-47: "League rank gates the tier"); rows by minLeagueRank
//   tiers           per tier 0-5 (the character's rank index in the guild, ClusterFGuildSystem.GuildRankIndex, capped by
//                   the gate): the property's minimum intensity (percent of the catalogue row's Full value), how many
//                   properties the item needs in all, and the Renown and guild standing paid
//   themes          one or more rows per guild (all 18 of F-9's table; Custodians none): an item kind
//                   (RequisitionCategories), and a property (an Artificers' catalogue row name), or a slayer family (a
//                   SlayerName, or "any"), or neither for "anything magic"; weight picks among a guild's rows
//   slayerFamilies  the families a slayer row marked "any" draws from, when no League hunt names one
//   leads           days held, the pity kill count, the fame floor a kill needs, the chance a request rolls a Lead by
//                   League band (the gate tier), and the extra roll's chance by the creature's fame
//   areas           where a Lead can point: a key, the name shown, the region names it covers (pinned regions.json), the
//                   creature groups found there (SlayerNames or "any"), and the League rank that unlocks it
//   appraiser       the Appraiser's price: a base by item kind (near stock sell prices, GenericSell.cs and the SBInfo
//                   tables), gold per property, gold per intensity point, and the cap

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server;

public sealed class RequisitionLeagueGateRow
{
    public int MinLeagueRank { get; set; }
    public int MaxTier { get; set; }
}

public sealed class RequisitionTierRow
{
    public int Tier { get; set; }
    public int MinIntensity { get; set; }
    public int MinProperties { get; set; }
    public int Renown { get; set; }
    public int Reputation { get; set; }
}

public sealed class RequisitionThemeRow
{
    public string Guild { get; set; } = "";
    public string Category { get; set; } = "any";
    public string Property { get; set; } = "";
    public string Slayer { get; set; } = "";
    public int Weight { get; set; } = 1;
    public string Note { get; set; }

    [JsonIgnore]
    public RequisitionKind Kind =>
        !string.IsNullOrEmpty(Slayer) ? RequisitionKind.Slayer :
        string.IsNullOrEmpty(Property) ? RequisitionKind.AnyMagic : RequisitionKind.Property;
}

public sealed class RequisitionFameChanceRow
{
    public int FameMin { get; set; }
    public int Percent { get; set; }
}

public sealed class RequisitionLeadRules
{
    public int Days { get; set; } = 3;
    public int PityKills { get; set; } = 40;
    public int QualifyingFameMin { get; set; } = 1000;
    public List<int> ChancePercentByBand { get; set; } = [10, 15, 20, 25, 30, 35];
    public List<RequisitionFameChanceRow> ExtraRollChance { get; set; } =
    [
        new() { FameMin = 0, Percent = 2 },
        new() { FameMin = 1000, Percent = 5 },
        new() { FameMin = 5000, Percent = 10 },
        new() { FameMin = 15000, Percent = 20 },
    ];
}

public sealed class RequisitionAreaRow
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Regions { get; set; } = [];
    public List<string> Groups { get; set; } = ["any"];
    public int MinLeagueRank { get; set; }
    /// <summary>A facet name (Trammel, Felucca, ...) to limit the area to; null for any facet with that region.</summary>
    public string Facet { get; set; }
}

public sealed class RequisitionAppraiserRules
{
    public Dictionary<string, int> BaseByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = 25, ["ranged"] = 20, ["armor"] = 40, ["shield"] = 30, ["jewelry"] = 13,
        ["spellbook"] = 9, ["instrument"] = 10, ["clothing"] = 8, ["other"] = 10,
    };

    public int PerProperty { get; set; } = 25;
    public double PerIntensityPoint { get; set; } = 0.5;
    public int Max { get; set; } = 500;

    public int BaseFor(string priceKey) =>
        BaseByCategory.TryGetValue(priceKey ?? "other", out var v) ? v :
        BaseByCategory.TryGetValue("other", out var o) ? o : 10;
}

public sealed class RequisitionTable
{
    public const string FileName = "requisitions.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string About { get; set; }
    public int RefreshHourUtc { get; set; } = 5;
    public int PerGuild { get; set; } = 3;
    public List<RequisitionLeagueGateRow> LeagueGate { get; set; } = [];
    public List<RequisitionTierRow> Tiers { get; set; } = [];
    public List<RequisitionThemeRow> Themes { get; set; } = [];
    public List<string> SlayerFamilies { get; set; } = [];
    public RequisitionLeadRules Leads { get; set; } = new();
    public List<RequisitionAreaRow> Areas { get; set; } = [];
    public RequisitionAppraiserRules Appraiser { get; set; } = new();

    /// <summary>Where the table in force came from: "built-in defaults" or the file's path.</summary>
    [JsonIgnore]
    public string Source { get; set; } = "built-in defaults";

    private static RequisitionTable _defaults;
    private static RequisitionTable _current;

    public static RequisitionTable Defaults => _defaults ??= BuildDefaults();

    public static RequisitionTable Current
    {
        get => _current ??= Defaults;
        set => _current = value;
    }

    // -- Lookups ------------------------------------------------------------------------------------

    public RequisitionTierRow TierFor(int tier)
    {
        RequisitionTierRow best = null;
        foreach (var row in Tiers)
            if (row.Tier <= tier && (best == null || row.Tier > best.Tier))
                best = row;
        return best ?? Tiers.FirstOrDefault() ?? new RequisitionTierRow { MinIntensity = 20, MinProperties = 1, Renown = 10, Reputation = 40 };
    }

    /// <summary>The highest tier this League rank allows (0 for an unregistered character).</summary>
    public int GateFor(int leagueRank)
    {
        var gate = 0;
        foreach (var row in LeagueGate)
            if (leagueRank >= row.MinLeagueRank)
                gate = Math.Max(gate, row.MaxTier);
        return Math.Clamp(gate, 0, 5);
    }

    public List<RequisitionThemeRow> ThemesFor(string guildKey) =>
        Themes.Where(t => t.Guild.Equals(guildKey, StringComparison.OrdinalIgnoreCase)).ToList();

    public RequisitionAreaRow Area(string key) =>
        Areas.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Areas this League rank may be sent to, holding <paramref name="group"/> when one is named.</summary>
    public List<RequisitionAreaRow> AreasFor(int leagueRank, string group) =>
        Areas.Where(a => a.MinLeagueRank <= leagueRank &&
                         (group == null || a.Groups.Any(g => g.Equals(group, StringComparison.OrdinalIgnoreCase))))
            .ToList();

    public int LeadChancePercent(int leagueRank)
    {
        var band = GateFor(leagueRank);
        var list = Leads.ChancePercentByBand;
        if (list == null || list.Count == 0) return 0;
        return list[Math.Clamp(band, 0, list.Count - 1)];
    }

    public int ExtraRollPercent(int fame)
    {
        var best = 0;
        var bestMin = int.MinValue;
        foreach (var row in Leads.ExtraRollChance)
            if (fame >= row.FameMin && row.FameMin >= bestMin)
            {
                bestMin = row.FameMin;
                best = row.Percent;
            }
        return Math.Clamp(best, 0, 100);
    }

    public List<SlayerName> SlayerFamilyNames()
    {
        var list = new List<SlayerName>();
        foreach (var s in SlayerFamilies)
            if (Enum.TryParse<SlayerName>(s, true, out var name) && name != SlayerName.None)
                list.Add(name);
        return list;
    }

    // -- Defaults -----------------------------------------------------------------------------------

    private static RequisitionThemeRow T(string guild, string category, string property = "", string slayer = "", int weight = 1, string note = null) =>
        new() { Guild = guild, Category = category, Property = property, Slayer = slayer, Weight = weight, Note = note };

    private static RequisitionAreaRow A(string key, string name, string[] regions, string[] groups, int minRank, string facet = null) =>
        new() { Key = key, Name = name, Regions = regions.ToList(), Groups = groups.ToList(), MinLeagueRank = minRank, Facet = facet };

    public static RequisitionTable BuildDefaults()
    {
        var t = new RequisitionTable
        {
            About = "Requisitions, the Appraiser and Leads (cc-P150, F-47 and F-48). Edit and run [RequisitionsStaff reload. " +
                    "A bad file is refused whole and the table in force is kept. tiers: by the character's rank index in the " +
                    "guild (0 Initiate to 5 top), capped by leagueGate (the League rank's highest tier). themes: a guild's rows; " +
                    "property is an Artificers' catalogue row name, slayer a SlayerName or any, neither means anything magic. " +
                    "leads: days held, the pity kill count (guaranteed by then), the fame floor, the Lead chance per League " +
                    "band (0-5) and the extra roll chance by creature fame. appraiser: base by item kind + perProperty x count + " +
                    "perIntensityPoint x total intensity, capped at max.",
            RefreshHourUtc = 5,
            PerGuild = 3,
            LeagueGate =
            [
                new() { MinLeagueRank = 0, MaxTier = 0 },
                new() { MinLeagueRank = 1, MaxTier = 1 },   // Iron
                new() { MinLeagueRank = 5, MaxTier = 2 },   // Bronze
                new() { MinLeagueRank = 8, MaxTier = 3 },   // Verite
                new() { MinLeagueRank = 11, MaxTier = 4 },  // Toxic
                new() { MinLeagueRank = 14, MaxTier = 5 },  // Obsidian
            ],
            Tiers =
            [
                new() { Tier = 0, MinIntensity = 20, MinProperties = 1, Renown = 10, Reputation = 40 },
                new() { Tier = 1, MinIntensity = 30, MinProperties = 1, Renown = 20, Reputation = 60 },
                new() { Tier = 2, MinIntensity = 40, MinProperties = 2, Renown = 35, Reputation = 90 },
                new() { Tier = 3, MinIntensity = 55, MinProperties = 2, Renown = 55, Reputation = 130 },
                new() { Tier = 4, MinIntensity = 70, MinProperties = 3, Renown = 80, Reputation = 180 },
                new() { Tier = 5, MinIntensity = 85, MinProperties = 3, Renown = 110, Reputation = 250 },
            ],
            Themes =
            [
                // Warriors' Brotherhood: swords and maces with Hit Chance or a hit spell (F-47)
                T("warriors", "sword", "Hit Chance Increase", weight: 3),
                T("warriors", "mace", "Hit Lightning", weight: 2),
                T("warriors", "sword", "Hit Lower Defense", weight: 2),
                T("warriors", "mace", "Hit Fireball", weight: 1),
                T("warriors", "sword", slayer: "any", weight: 2),
                T("warriors", "mace", slayer: "Repond", weight: 1),
                T("warriors", "shield", "Defense Chance Increase", weight: 1),
                // Arcane Society: spellbooks and Lower Mana Cost jewelry (F-47)
                T("arcane", "spellbook", "Lower Mana Cost", weight: 3),
                T("arcane", "jewelry", "Lower Mana Cost", weight: 2),
                T("arcane", "jewelry", "Faster Cast Recovery", weight: 2),
                T("arcane", "spellbook", "Spell Damage Increase", weight: 2),
                T("arcane", "jewelry", "Lower Reagent Cost", weight: 1),
                T("arcane", "spellbook", "Faster Casting", weight: 1),
                // Healers' Covenant
                T("healers", "armor", "Hit Points Regen", weight: 3),
                T("healers", "jewelry", "Enhance Potions", weight: 2),
                T("healers", "armor", "Poison Resist Bonus", weight: 1),
                T("healers", "jewelry", "Bonus Intelligence", weight: 1),
                // Order of the Alembic
                T("alembic", "jewelry", "Enhance Potions", weight: 3),
                T("alembic", "armor", "Poison Resist Bonus", weight: 2),
                T("alembic", "jewelry", "Bonus Intelligence", weight: 1),
                T("alembic", "hat", "Luck", weight: 1),
                // Rangers' League: bows (F-47)
                T("rangers", "bow", "Hit Chance Increase", weight: 3),
                T("rangers", "bow", "Hit Lower Defense", weight: 2),
                T("rangers", "bow", slayer: "any", weight: 2),
                T("rangers", "leatherArmor", "Stamina Regen", weight: 1),
                T("rangers", "bow", "Velocity", weight: 1),
                // Miners' Compact
                T("mining", "armor", "Bonus Strength", weight: 3),
                T("mining", "armor", "Lower Stat Req (Armor)", weight: 2),
                T("mining", "jewelry", "Bonus Strength", weight: 2),
                T("mining", "weapon", "Durability (Weapon)", weight: 1),
                // Society of Smiths
                T("smithing", "metalArmor", "Physical Resist Bonus", weight: 3),
                T("smithing", "armor", "Durability (Armor)", weight: 2),
                T("smithing", "sword", "Durability (Weapon)", weight: 1),
                T("smithing", "shield", "Defense Chance Increase", weight: 2),
                // Tinkers' Union
                T("tinkers", "jewelry", "Bonus Dexterity", weight: 3),
                T("tinkers", "jewelry", "Luck", weight: 2),
                T("tinkers", "jewelry", "Defense Chance Increase", weight: 2),
                T("tinkers", "jewelry", "Night Sight", weight: 1),
                // Thieves' Den: "no questions asked" goods (F-47)
                T("thieves", "any", weight: 3, note: "no questions asked: any magic item with enough properties"),
                T("thieves", "jewelry", "Bonus Dexterity", weight: 2),
                T("thieves", "fencing", "Hit Lower Attack", weight: 2),
                T("thieves", "leatherArmor", "Night Sight", weight: 1),
                // Twin Paths Dojo
                T("dojo", "sword", "Swing Speed Increase", weight: 3),
                T("dojo", "fencing", "Hit Stamina Leech", weight: 2),
                T("dojo", "armor", "Stamina Regen", weight: 1),
                T("dojo", "sword", "Hit Lightning", weight: 1),
                // Keepers of the Last Door
                T("keepers", "necrobook", "Spell Damage Increase", weight: 3),
                T("keepers", "jewelry", "Spell Damage Increase", weight: 2),
                T("keepers", "jewelry", "Lower Mana Cost", weight: 1),
                T("keepers", "weapon", slayer: "Silver", weight: 2),
                T("keepers", "weapon", slayer: "Exorcism", weight: 1),
                // Foresters' Union
                T("foresters", "axe", "Hit Chance Increase", weight: 3),
                T("foresters", "axe", "Damage Increase", weight: 2),
                T("foresters", "bow", "Damage Increase", weight: 1),
                T("foresters", "armor", "Lower Stat Req (Armor)", weight: 1),
                // Tailors' Circle
                T("tailoring", "leatherArmor", "Mana Regen", weight: 3),
                T("tailoring", "leatherArmor", "Fire Resist Bonus", weight: 2),
                T("tailoring", "hat", "Luck", weight: 2),
                T("tailoring", "leatherArmor", "Lower Mana Cost", weight: 1),
                // Maritime Brotherhood
                T("maritime", "armor", "Cold Resist Bonus", weight: 2),
                T("maritime", "jewelry", "Luck", weight: 2),
                T("maritime", "fencing", "Hit Lightning", weight: 2),
                T("maritime", "armor", "Energy Resist Bonus", weight: 1),
                T("maritime", "weapon", slayer: "ReptilianDeath", weight: 1),
                // Merchants' Exchange
                T("merchants", "jewelry", "Luck", weight: 3),
                T("merchants", "any", "Luck", weight: 2),
                T("merchants", "armor", "Hit Point Increase", weight: 1),
                T("merchants", "jewelry", "Lower Reagent Cost", weight: 1),
                // Bards' Consortium: slayer instruments
                T("bards", "instrument", slayer: "any", weight: 4),
                T("bards", "jewelry", "Bonus Intelligence", weight: 2),
                T("bards", "armor", "Mana Regen", weight: 1),
                T("bards", "jewelry", "Mana Increase", weight: 1),
                // Artificers' Order: raw material for the table
                T("artificers", "any", weight: 3, note: "anything magic with enough properties, for unravelling"),
                T("artificers", "jewelry", "Faster Casting", weight: 2),
                T("artificers", "armor", "Mage Armor", weight: 1),
                T("artificers", "weapon", "Spell Channeling", weight: 1),
                // The Custodians: none (F-47: "Custodians can have none")
            ],
            SlayerFamilies =
            [
                "Silver", "Repond", "ReptilianDeath", "Exorcism", "ArachnidDoom", "ElementalBan", "Fey",
                "DragonSlaying", "OrcSlaying", "TrollSlaughter", "OgreTrashing", "Terathan", "Ophidian",
            ],
            Leads = new RequisitionLeadRules(),
            Areas =
            [
                A("graveyard", "the Britain Graveyard", ["Britain Graveyard"], ["Silver"], 0),
                A("rootdeep", "the Rootdeep Burrows", ["the Rootdeep Burrows"], ["any"], 0),
                A("despise", "Despise", ["Despise", "Despise Passage"], ["Repond", "ReptilianDeath", "OgreTrashing"], 0),
                A("orccave", "the Orc Cave", ["Orc Cave"], ["OrcSlaying", "Repond"], 0),
                A("covetous", "Covetous", ["Covetous"], ["Silver", "Repond"], 1),
                A("deceit", "Deceit", ["Deceit"], ["Silver"], 1),
                A("shame", "Shame", ["Shame"], ["ElementalBan", "EarthShatter"], 1),
                A("wrong", "Wrong", ["Wrong"], ["Repond", "OrcSlaying"], 1),
                A("orcfort", "the Orc Fortress", ["Orc Fortress"], ["OrcSlaying"], 2),
                A("destard", "Destard", ["Destard"], ["DragonSlaying", "ReptilianDeath"], 5),
                A("hythloth", "Hythloth", ["Hythloth"], ["Exorcism", "DaemonDismissal"], 5),
                A("ice", "Ice", ["Ice"], ["ElementalBan", "SummerWind"], 5),
                A("fire", "Fire", ["Fire"], ["Exorcism", "FlameDousing"], 5),
                A("terathan", "Terathan Keep", ["Terathan Keep"], ["Terathan", "ArachnidDoom"], 5),
                A("khaldun", "Khaldun", ["Khaldun"], ["Silver"], 8),
                A("doom", "Doom", ["Doom"], ["Exorcism", "Silver"], 11),
            ],
            Appraiser = new RequisitionAppraiserRules(),
        };

        return t;
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
    };

    public static string ToJson(RequisitionTable table) => JsonSerializer.Serialize(table, Json);

    /// <summary>
    /// Reads a table from JSON. Returns null and fills errors when anything is wrong; a table is accepted whole or not at
    /// all. Checked: syntax; the hour and the per-guild count; a row for tier 0; every tier row 0-5 with sane numbers;
    /// every theme's guild is a registered guild, its category a known kind, its property a catalogue row (or empty)
    /// and its slayer a SlayerName or "any"; every area has a key, a name and a region, and its groups parse; the
    /// appraiser numbers are not negative.
    /// </summary>
    public static RequisitionTable Parse(string json, string source, List<string> errors)
    {
        RequisitionTable table;
        try
        {
            table = JsonSerializer.Deserialize<RequisitionTable>(json, Json);
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

    public static void Validate(RequisitionTable t, List<string> errors)
    {
        ClusterFGuildSystem.EnsureRegistered();

        if (t.RefreshHourUtc is < 0 or > 23) errors.Add($"refreshHourUtc {t.RefreshHourUtc} is not 0 to 23");
        if (t.PerGuild is < 1 or > 10) errors.Add($"perGuild {t.PerGuild} is not 1 to 10");

        if (t.Tiers == null || t.Tiers.All(r => r.Tier != 0)) errors.Add("tiers: no row for tier 0");
        foreach (var row in t.Tiers ?? new List<RequisitionTierRow>())
        {
            if (row.Tier is < 0 or > 5) errors.Add($"tiers: tier {row.Tier} is not 0 to 5");
            if (row.MinIntensity is < 0 or > 100) errors.Add($"tiers: tier {row.Tier} minIntensity {row.MinIntensity} is not 0 to 100");
            if (row.MinProperties is < 0 or > 5) errors.Add($"tiers: tier {row.Tier} minProperties {row.MinProperties} is not 0 to 5");
            if (row.Renown < 0 || row.Reputation < 0) errors.Add($"tiers: tier {row.Tier} pays a negative amount");
        }

        foreach (var row in t.LeagueGate ?? new List<RequisitionLeagueGateRow>())
            if (row.MaxTier is < 0 or > 5 || row.MinLeagueRank < 0)
                errors.Add($"leagueGate: minLeagueRank {row.MinLeagueRank} maxTier {row.MaxTier} is out of range");

        var i = 0;
        foreach (var row in t.Themes ?? new List<RequisitionThemeRow>())
        {
            i++;
            if (ClusterFGuildSystem.GetDef(row.Guild ?? "") == null) errors.Add($"themes[{i}]: \"{row.Guild}\" is not a guild key");
            if (!RequisitionCategories.Known(row.Category)) errors.Add($"themes[{i}]: \"{row.Category}\" is not an item kind ({string.Join(", ", RequisitionCategories.Keys)})");
            if (!string.IsNullOrEmpty(row.Property) && !RequisitionProperties.IsKnownProperty(row.Property)) errors.Add($"themes[{i}]: \"{row.Property}\" is not a catalogue property");
            if (!string.IsNullOrEmpty(row.Slayer) && !row.Slayer.Equals("any", StringComparison.OrdinalIgnoreCase) &&
                !(Enum.TryParse<SlayerName>(row.Slayer, true, out var s) && s != SlayerName.None))
                errors.Add($"themes[{i}]: \"{row.Slayer}\" is not a slayer family");
            if (!string.IsNullOrEmpty(row.Property) && !string.IsNullOrEmpty(row.Slayer)) errors.Add($"themes[{i}]: a row names a property or a slayer, not both");
            if (row.Weight < 1) errors.Add($"themes[{i}]: weight {row.Weight} is below 1");
        }

        foreach (var s in t.SlayerFamilies ?? new List<string>())
            if (!(Enum.TryParse<SlayerName>(s, true, out var name) && name != SlayerName.None))
                errors.Add($"slayerFamilies: \"{s}\" is not a slayer family");

        if (t.Leads == null) errors.Add("leads: missing");
        else
        {
            if (t.Leads.Days < 1) errors.Add($"leads.days {t.Leads.Days} is below 1");
            if (t.Leads.PityKills < 2) errors.Add($"leads.pityKills {t.Leads.PityKills} is below 2");
            if (t.Leads.QualifyingFameMin < 0) errors.Add("leads.qualifyingFameMin is negative");
            foreach (var p in t.Leads.ChancePercentByBand ?? new List<int>())
                if (p is < 0 or > 100) errors.Add($"leads.chancePercentByBand: {p} is not 0 to 100");
            foreach (var r in t.Leads.ExtraRollChance ?? new List<RequisitionFameChanceRow>())
                if (r.Percent is < 0 or > 100) errors.Add($"leads.extraRollChance: {r.Percent} is not 0 to 100");
        }

        i = 0;
        foreach (var a in t.Areas ?? new List<RequisitionAreaRow>())
        {
            i++;
            if (string.IsNullOrWhiteSpace(a.Key) || string.IsNullOrWhiteSpace(a.Name)) errors.Add($"areas[{i}]: a key and a name are needed");
            if (a.Regions == null || a.Regions.Count == 0) errors.Add($"areas[{i}] {a.Key}: no regions");
            foreach (var g in a.Groups ?? new List<string>())
                if (!g.Equals("any", StringComparison.OrdinalIgnoreCase) && !(Enum.TryParse<SlayerName>(g, true, out var n) && n != SlayerName.None))
                    errors.Add($"areas[{i}] {a.Key}: group \"{g}\" is not a slayer family or any");
            if (a.MinLeagueRank < 0) errors.Add($"areas[{i}] {a.Key}: minLeagueRank is negative");
        }

        if (t.Areas != null && t.Areas.Select(a => a.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != t.Areas.Count)
            errors.Add("areas: a key appears twice");

        if (t.Appraiser == null) errors.Add("appraiser: missing");
        else
        {
            if (t.Appraiser.PerProperty < 0 || t.Appraiser.PerIntensityPoint < 0 || t.Appraiser.Max < 1) errors.Add("appraiser: perProperty and perIntensityPoint must be 0 or more, max 1 or more");
            foreach (var (k, v) in t.Appraiser.BaseByCategory ?? new Dictionary<string, int>())
                if (v < 0) errors.Add($"appraiser.baseByCategory.{k} is negative");
        }
    }

    /// <summary>At start: read the file into force, or write the defaults when there is none. Returns console lines.</summary>
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
        lines.Add($"requisitions table in force: {path} ({table.Themes.Count} theme rows, {table.Areas.Count} areas, refresh {table.RefreshHourUtc:00}:00 UTC)");
        return lines;
    }
}
