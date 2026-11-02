// cc-P79 (F-40): the loot box numbers, in one table Chase can tune. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
// cc-P763 (P759's approved plan, shard-migration notes/cc-P759-loot-plan.md): the contents are data too. Every general
// pool, every theme's slot and its Epic and Legendary lists, and every season's pool is a list of entries here; the
// rarity roll (Common, Rare, Epic, Legendary) has its odds by grade and its bonuses here; the six tiers are named as
// grades (Plain to Celestial), so the rarity words mean only the rarity roll.
//
// Loaded from Configuration/loot-boxes.json the way cc-P48's League ladder is: a missing file is written with the
// built-in defaults below; a file that fails a check is refused whole and the table in force is kept; [LootBox reload
// reads it again. The test host runs no Configure and uses the defaults. A field the file leaves out keeps its default,
// and so does a pool row, theme or season it leaves out (a file written before cc-P763 still loads).
//
// An entry's Item is a type name (any constructible item, made with no arguments) or a special the roller makes in
// code, starting with @ (LootBoxRoller.Specials lists them: the ore for the opener's Mining, a runic tool, a bag of
// holding, a piece of registered art). Every entry is checked when the file loads: an unknown type or special refuses it.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Items;

namespace Server.LootBoxes;

// Appended only: a saved box stores its theme by number.
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
    League,
    Fisher,     // cc-P763
    Sailor,
    Herbalist,
    Society,
    Delver,
    LeagueDive
}

public enum BoxPool
{
    Consumables,
    Resources,
    MagicGear,
    Recipes,
    Cosmetics,
    Rare,
    Tools // cc-P763
}

/// <summary>cc-P763: the rarity rolled when a box is earned, on top of its grade. Saved by number.</summary>
public enum BoxRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>A named step of a ladder: its display name and its hue (a CraftResource name, or a hue number).</summary>
public sealed record LadderStep(string Name, string Resource, int Hue);

/// <summary>cc-P763: what one rarity adds (LootTable.RarityBonus).</summary>
public sealed class RarityStep
{
    public string Name { get; set; } = "";
    public int Hue { get; set; }
    public int ExtraSlots { get; set; }
    public double StackMultiplier { get; set; } = 1.0;
    public int PoolShift { get; set; }

    /// <summary>Minor, Major or Capstone: the least fanfare a box of this rarity plays.</summary>
    public string Fanfare { get; set; } = "Minor";

    /// <summary>Whether the award and the opening say the "rare" line (FanfareLines.BoxRare).</summary>
    public bool RareLine { get; set; }

    public FanfareLevel Level => Enum.TryParse<FanfareLevel>(Fanfare, true, out var l) ? l : FanfareLevel.Minor;
}

/// <summary>
/// cc-P763: one entry of a pool, a theme slot, an Epic or Legendary list or a season. Amount: Min to Max (Max 0 is Min),
/// plus PerGrade times the grade, times the rarity's stack multiplier for a stackable. Offered at grades MinGrade to
/// MaxGrade and rarities from MinRarity up. In a theme slot or list, entries with no Group are each given; entries
/// sharing a Group give one, by Weight. Chance (percent by rarity, Common to Legendary) makes an entry a chance.
/// </summary>
public sealed class PoolEntry
{
    public string Item { get; set; } = "";
    public int Weight { get; set; } = 10;
    public int Min { get; set; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Max { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PerGrade { get; set; }
    public int MinGrade { get; set; } = 1;
    public int MaxGrade { get; set; } = 6;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MinRarity { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int[]? Chance { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Group { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Arg { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Skill { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Hue { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ItemId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int AltId { get; set; }

    public BoxRarity LeastRarity => Enum.TryParse<BoxRarity>(MinRarity, true, out var r) ? r : BoxRarity.Common;

    public bool OfferedAt(int grade, BoxRarity rarity) => grade >= MinGrade && grade <= MaxGrade && rarity >= LeastRarity;
}

/// <summary>cc-P763: a theme's slot, its Epic and Legendary lists, and the ladder that names its boxes.</summary>
public sealed class ThemeDef
{
    /// <summary>Metal, Wood, Leather, Craft (by the craft: wood, leather or metal) or Grade.</summary>
    public string Ladder { get; set; } = "Grade";

    public PoolEntry[] Slot { get; set; } = [];
    public PoolEntry[] Epic { get; set; } = [];
    public PoolEntry[] Legendary { get; set; } = [];
}

/// <summary>cc-P763: a season's pool: one extra slot while its seasonal switch's window is on.</summary>
public sealed class SeasonDef
{
    public string Label { get; set; } = "";
    public PoolEntry[] Items { get; set; } = [];
}

public sealed class LootTable
{
    public const string FileName = "loot-boxes.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public string Source { get; init; } = "built-in defaults";

    // Slots per grade 1 to 6.
    public int[] Slots { get; init; } = [2, 3, 3, 4, 5, 6];

    // The currency slot: gold at every grade, Renown from grade 2.
    public int[] Gold { get; init; } = [250, 750, 2_000, 5_000, 12_000, 30_000];
    public int[] Renown { get; init; } = [0, 25, 50, 100, 200, 400];

    // General pool weights by grade 1 to 6. cc-P763: Tools is new; the six pools before it keep their weights.
    public Dictionary<string, int[]> PoolWeights { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Consumables"] = [50, 40, 30, 20, 15, 10],
        ["Resources"] = [30, 30, 25, 20, 15, 10],
        ["MagicGear"] = [15, 20, 25, 25, 25, 20],
        ["Recipes"] = [5, 8, 12, 15, 15, 15],
        ["Cosmetics"] = [0, 2, 6, 12, 15, 20],
        ["Rare"] = [0, 0, 2, 8, 15, 25],
        ["Tools"] = [8, 8, 8, 8, 8, 8],
    };

    // cc-P763: the grade ladder (it was named Common to Celestial before the rarity roll took those words). It names the
    // boxes of the themes with no material ladder, and colours the fanfare by grade.
    public LadderStep[] Grades { get; init; } =
    [
        new("Plain", "", 0x0386),
        new("Sturdy", "", 0x0044),
        new("Fine", "", 0x0059),
        new("Grand", "", 0x0013),
        new("Exalted", "", 0x002B),
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

    // cc-P763: the rarity roll's odds by grade 1 to 6, percent Common, Rare, Epic, Legendary (the plan's table).
    public double[][] RarityOdds { get; init; } =
    [
        [85, 13, 2, 0],
        [75, 20, 4.5, 0.5],
        [65, 25, 8, 2],
        [50, 32, 14, 4],
        [40, 35, 18, 7],
        [25, 40, 25, 10],
    ];

    // cc-P763: what each rarity adds, Common to Legendary. The hue is the box's colour (grey, blue, purple, orange).
    public RarityStep[] RarityBonus { get; init; } =
    [
        new() { Name = "Common", Hue = 0x0386, ExtraSlots = 0, StackMultiplier = 1.0, PoolShift = 0, Fanfare = "Minor" },
        new() { Name = "Rare", Hue = 0x0059, ExtraSlots = 1, StackMultiplier = 1.5, PoolShift = 5, Fanfare = "Minor", RareLine = true },
        new() { Name = "Epic", Hue = 0x0013, ExtraSlots = 1, StackMultiplier = 2.0, PoolShift = 10, Fanfare = "Major", RareLine = true },
        new() { Name = "Legendary", Hue = 0x002B, ExtraSlots = 2, StackMultiplier = 3.0, PoolShift = 15, Fanfare = "Capstone", RareLine = true },
    ];

    // Grade by source. Achievements: by AP (the ceilings for grades 1 to 5; above the last, 6), overrides by key.
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

    // cc-P763: an achievement key to a theme, ahead of its category. The shard has one fishing achievement (Sole
    // Survivor, a boot fished up) and none for boats, docks or gardens; add keys here as those are written.
    public Dictionary<string, string> AchievementTheme { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["joke.sole_survivor"] = "Fisher",
    };

    // Bosses: grade by type name (or a base class name), default for the rest.
    public int BossDefaultTier { get; init; } = 4;
    public Dictionary<string, int> BossTier { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseChampion"] = 5,
        ["BasePeerless"] = 5,
        ["Harrower"] = 6,
        ["ClockworkExodus"] = 5, // cc-P195: the Exodus Encounter's boss, 50,000 hits, a peerless-sized fight
    };

    // League rank index 1 to 17 (Iron to Celestial) to grade.
    public int[] LeagueRankTier { get; init; } = [1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6];

    public int DungeonClearTier { get; init; } = 3;
    public int LoopTier { get; init; } = 6;

    // cc-P763: the first-time sources fishing, sailing and gardening have (once per character each).
    public int FishGrade { get; init; } = 2;             // a High Seas fish's first catch of its kind
    public int RareFishGrade { get; init; } = 4;         // a rare or legendary fish's
    public int[] SosGrade { get; init; } = [2, 3, 4, 5]; // an SOS chest's first recovery, by its level 1 to 4
    public int PlantGrade { get; init; } = 2;            // a plant type first set to decorative

    // cc-P763: guild promotion. Each new rank of these guilds is a box of its theme, grade = rank (1 to 5). A theme
    // "A|B" gives A at odd ranks and B at even ones.
    public Dictionary<string, string> GuildTheme { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smithing"] = "Society",
        ["arcane"] = "Society",
        ["tailoring"] = "Society",
        ["tinkers"] = "Society",
        ["foresters"] = "Society",
        ["artificers"] = "Society",
        ["maritime"] = "Fisher|Sailor",
        ["healers"] = "Herbalist",
    };

    // cc-P763: a League Dive box for every this-many guild work orders finished, at the character's League rank's grade.
    public int LeagueDiveEvery { get; init; } = 10;

    // cc-P763: the Reagent Bag's weight cut, percent, by the box's rarity (Common to Legendary).
    public int[] ReagentBagCut { get; init; } = [25, 50, 75, 90];

    // cc-P763: TheArt's Bags of Holding by size 1 to 5: their names and item caps (no weight cap).
    public string[] BagOfHoldingNames { get; init; } =
        ["Lesser Bag of Holding", "Bag of Holding", "Greater Bag of Holding", "Legendary Bag of Holding", "Grand Bag of Holding"];

    public int[] BagOfHoldingCaps { get; init; } = [12, 24, 36, 48, 125];

    // cc-P763: the Boss slot's trophy, from the registered Hestia and Archaaz art (north-wall ID, west-wall ID).
    public int[][] Trophies { get; init; } =
    [
        [0x3CF8, 0x3CF9], [0x3CFA, 0x3CFB], [0x3CFC, 0x3CFD], [0x3CFE, 0x3CFF], [0x3D00, 0x3D01], [0x3D02, 0x3D03],
        [0x3D04, 0x3D05], [0x3D06, 0x3D07], [0x3D08, 0x3D09], [0x3D0A, 0x3D0B], [0x3D0C, 0x3D0D],
    ];

    // -- The contents (cc-P763) --

    public Dictionary<string, PoolEntry[]> Pools { get; init; } = DefaultPools();
    public Dictionary<string, ThemeDef> Themes { get; init; } = DefaultThemes();
    public Dictionary<string, SeasonDef> Seasons { get; init; } = DefaultSeasons();

    // A box opened but not emptied (a full bank) is cleaned up after this many days.
    public int AbandonedBoxDays { get; init; } = 7;

    public static LootTable Defaults { get; } = new();

    public static LootTable Current { get; set; } = Defaults;

    // -- The default contents --

    private static PoolEntry E(string item, int w = 10, int min = 1, int per = 0, int minG = 1, int maxG = 6,
        string? rarity = null, string? group = null, int arg = 0, string? skill = null, int[]? chance = null,
        string? name = null, int id = 0, int alt = 0, int max = 0, int hue = 0) =>
        new()
        {
            Item = item, Weight = w, Min = min, Max = max, PerGrade = per, MinGrade = minG, MaxGrade = maxG,
            MinRarity = rarity, Group = group, Arg = arg, Skill = skill, Chance = chance, Name = name, ItemId = id,
            AltId = alt, Hue = hue
        };

    private static PoolEntry Decor(string name, int id, int alt = 0, int w = 10) => E("@Decor", w, name: name, id: id, alt: alt);

    public static Dictionary<string, PoolEntry[]> DefaultPools() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Consumables"] =
        [
            E("@Potion", 30, min: 2, per: 1),
            E("Bandage", 30, min: 10, per: 10),
            E("EnchantedApple", 6, min: 1, per: 1),
            E("BegPizza", 4, min: 2), E("BegStew", 4, min: 2), E("FruitBowl", 4), E("BegCheeseWedge", 4, min: 2),
            E("BegSake", 3), E("BegWine", 3),
            E("ExplodingTarPotion", 5, min: 2, minG: 3),
            E("GoldFoil", 3), E("SkinTingeingTincture", 2),
        ],
        ["Resources"] =
        [
            E("@Metal", 20, min: 0, per: 10), E("@Wood", 20, min: 0, per: 10), E("@Leather", 15, min: 0, per: 10),
            E("@Gems", 15, min: 0, per: 1),
            E("Cloth", 10, min: 10, per: 10), E("Bone", 6, min: 5, per: 5),
            E("FertileDirt", 7, min: 5, per: 5), E("@Seed", 7, min: 1, per: 1),
        ],
        ["MagicGear"] = [E("@MagicGear", 100)],
        ["Tools"] =
        [
            E("Skeletonkey", 12),
            E("SewingKit", 10), E("TinkerTools", 10), E("SmithHammer", 10), E("FletcherTools", 10), E("MortarPestle", 10),
            E("Sextant", 6),
            E("@RunicMallet", 6, rarity: "Epic"),
            E("CircuitTrapTrainingKit", 5), E("CylinderTrapTrainingKit", 5), E("SliderTrapTrainingKit", 5),
        ],
        ["Recipes"] = [E("@Recipe", 100)],
        ["Cosmetics"] =
        [
            E("@BoxSash", 20),
            E("FlowerGarland", 8),
            E("DaggerBelt", 4), E("SwordBelt", 4), E("MaceBelt", 4), E("FirstAidBelt", 4),
            E("SkeletonCostume", 3), E("PixieCostume", 3), E("SatyrCostume", 3), E("CentaurCostume", 3), E("GazerCostume", 3),
            Decor("a painting: Beata Beatrix", 0x3C9E, w: 2), Decor("a painting: Morgan le Fay", 0x3C9F, w: 2),
            Decor("a painting: Ophelia", 0x3CA0, w: 2), Decor("a painting: Sir Galahad at the Ruined Chapel", 0x3CA1, w: 2),
            Decor("a painting: The Fairy Feller's Master-Stroke", 0x3CA2, w: 2),
            Decor("a painting: The Garden of the Hesperides", 0x3CA3, w: 2), Decor("a painting: The Lady of Shalott", 0x3CA4, w: 2),
            Decor("a painting: The Love Song", 0x3CA5, w: 2), Decor("a painting: The Tune of the Seven Towers", 0x3CA6, w: 2),
            Decor("a painting: Wars of the Roses", 0x3CA7, w: 2),
            Decor("a crystal ball", 0x6DC8, w: 3),
            Decor("a floating crystal (green)", 0x6DD8, w: 2), Decor("a floating crystal (purple)", 0x6DE1, w: 2),
            Decor("a partial-hue flask", 0x6DBB, w: 1), Decor("a partial-hue flask", 0x6DBD, w: 1),
            Decor("a partial-hue flask", 0x6DBF, w: 1), Decor("a partial-hue flask", 0x6DC1, w: 1),
            Decor("an oil lantern", 0x3C8C, w: 3),
            // The Noted joke items: 3 of 96 weight, 3 percent of this pool.
            E("StrategicBoulder", 1), E("BucketOfPopcorn", 1), E("LuckyRug", 1),
        ],
        ["Rare"] =
        [
            E("@BoxInBox", 18, minG: 2),
            E("@MuleSchematic", 18),
            E("@LeagueTitle", 10),
            E("MetallicDyeTub", 6), E("SpecialDyeTub", 5), E("FurnitureDyeTub", 5),
            E("PetBondingPotion", 7), E("ElixirOfRebirth", 6),
            E("@ReagentBag", 8),
            E("@BagOfHolding", 4, arg: 1, rarity: "Rare"),
            E("@BagOfHolding", 6, arg: 2, rarity: "Epic"),
            E("@BagOfHolding", 3, arg: 3, rarity: "Epic"),
            E("@BagOfHolding", 4, arg: 4, rarity: "Legendary"),
        ],
    };

    public static Dictionary<string, ThemeDef> DefaultThemes() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Miner"] = new()
        {
            Ladder = "Metal",
            Slot =
            [
                E("@Ore", min: 10, per: 10), E("@Gems", min: 0, per: 1),
                E("Pickaxe", group: "tool", maxG: 3), E("Shovel", group: "tool", maxG: 3), E("ProspectorsTool", group: "tool", minG: 4),
            ],
            Epic = [E("ProspectorsTool"), E("@Ore", min: 20, per: 20)],
            Legendary = [E("JacobsPickaxe")],
        },
        ["Woodsman"] = new()
        {
            Ladder = "Wood",
            Slot = [E("@Boards", min: 10, per: 10), E("Hatchet", group: "tool", maxG: 3), E("Axe", group: "tool", minG: 4)],
            Epic = [E("LargeBattleAxe", group: "axe"), E("ExecutionersAxe", group: "axe")],
            Legendary = [E("@RunicTool", skill: "Carpentry")],
        },
        ["Artisan"] = new()
        {
            Ladder = "Craft",
            Slot = [E("@CraftResources", min: 10, per: 10), E("@PatternBook")],
            Epic = [E("@RunicTool")],
            Legendary = [E("@RunicTool", arg: 1)],
        },
        ["Hunter"] = new()
        {
            Ladder = "Leather",
            Slot = [E("@Hides", min: 10, per: 5), E("@SlayerWeapon")],
            Epic = [E("@SlayerWeapon", arg: 2)],
            Legendary = [E("@BagOfHolding", arg: 2)],
        },
        ["Explorer"] = new()
        {
            Slot = [E("@TreasureMap"), E("BlankMap")],
            Epic = [E("@TreasureMap", arg: 4)],
            Legendary = [E("@TreasureMap", arg: 5), E("@BagOfHolding", arg: 2)],
        },
        ["Tamer"] = new()
        {
            Slot =
            [
                E("TastyTreat", 10, min: 2, group: "treat"), E("DeliciouslyTastyTreat", 10, min: 2, group: "treat", rarity: "Rare"),
                E("IrresistiblyTastyTreat", 10, min: 2, group: "treat", rarity: "Epic"),
                E("@Mount"),
            ],
            Epic = [E("PetBondingPotion")],
            Legendary = [E("ElixirOfRebirth"), E("@Mount")],
        },
        ["Scholar"] = new()
        {
            Slot = [E("@Reagents", min: 0, per: 10), E("@Scroll"), E("@ReagentBag", chance: [10, 25, 0, 0])],
            Epic = [E("@ReagentBag"), E("@Reagents", min: 0, per: 20)],
            Legendary = [E("@ReagentBag")],
        },
        ["Boss"] = new()
        {
            Slot = [E("@Trophy"), E("@Mount")],
            Epic = [E("@Trophy")],
            Legendary = [E("@BagOfHolding", arg: 4)],
        },
        ["Loop"] = new()
        {
            Slot = [E("@LoopSash"), E("@RarePick")],
            Epic = [E("@RarePick"), E("@RarePick")],
            Legendary = [E("@BagOfHolding", arg: 5)],
        },
        ["League"] = new()
        {
            Slot = [E("@RenownBonus", arg: 50), E("@LeagueCloak")],
            Epic = [E("@LeagueTitle")],
            Legendary = [E("@BagOfHolding", arg: 5)],
        },
        ["Fisher"] = new()
        {
            Slot = [E("Bait"), E("FishingPole"), E("FishSteak", min: 5, per: 5)],
            Epic = [E("BegFishingPole"), E("SpecialFishingNet")],
            Legendary = [E("FishingPole"), E("@BagOfHolding", arg: 2)],
        },
        ["Sailor"] = new()
        {
            Slot = [E("Sextant"), E("Spyglass"), E("Rope"), E("BreadLoaf", min: 3, per: 1)],
            Epic = [E("MessageInABottle")],
            Legendary = [E("SmallBoatDeed")],
        },
        ["Herbalist"] = new()
        {
            Slot =
            [
                E("@Seed", min: 2, per: 1), E("FertileDirt", min: 10, per: 5), E("MortarPestle"),
                E("GreaterHealPotion", min: 2, per: 1, group: "potion"), E("GreaterCurePotion", min: 2, per: 1, group: "potion"),
                E("GreaterPoisonPotion", min: 2, per: 1, group: "potion"), E("GreaterStrengthPotion", min: 2, per: 1, group: "potion"),
            ],
            Epic = [E("SeedBox")],
            Legendary = [E("ChickenCoop"), E("@Seed", min: 2, arg: 1)],
        },
        ["Society"] = new()
        {
            Ladder = "Craft",
            Slot = [E("@CraftResources", min: 10, per: 10), E("@CraftTool")],
            Epic = [E("@RunicTool")],
            Legendary = [E("@RunicTool", arg: 1), E("@BagOfHolding", arg: 2)],
        },
        ["Delver"] = new()
        {
            Slot = [E("@TreasureMap"), E("@Potion", min: 2, per: 1), E("Bandage", min: 10, per: 5), E("Skeletonkey")],
            Epic = [E("@SlayerWeapon", arg: 2)],
            Legendary = [E("@BagOfHolding", arg: 2)],
        },
        ["LeagueDive"] = new()
        {
            Slot = [E("@RenownBonus", arg: 50), E("@Potion", min: 2, per: 1), E("Bandage", min: 10, per: 5)],
            Epic = [E("@LeagueTitle")],
            Legendary = [E("@BagOfHolding", arg: 5)],
        },
    };

    public static Dictionary<string, SeasonDef> DefaultSeasons() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["halloween"] = new()
        {
            Label = "Halloween",
            Items =
            [
                E("Lollipops", min: 3), E("WrappedCandy", min: 3), E("JellyBeans", min: 3), E("Taffy", min: 3), E("NougatSwirl", min: 3),
                E("CreepyCake"), E("PumpkinPizza"), E("HarvestWine"),
                E("PaintedDaemonMask", 4), E("PaintedEvilClownMask", 4), E("PaintedEvilJesterMask", 4), E("PaintedPlagueMask", 4),
                E("PaintedPorcelainMask", 4),
                E("HalloweenPumpkin"), E("PumpkinScarecrow", 5),
                E("SkullsOnPike", 5), E("ColoredSmallWebs", 5), E("ChairInAGhostCostume", 5), E("HalloweenGuillotine", 5),
                E("PumpkinCarvingKit"),
            ],
        },
        ["winter"] = new()
        {
            Label = "Winter",
            Items =
            [
                E("SnowGlobeOne", 4), E("SnowGlobeTwo", 3), E("SnowGlobeThree", 3), E("Snowman"), E("HolidayTreeDeed", 5),
                E("HolidayBell"), E("WreathDeed"), E("GiftBox"), E("RockingHorse", 5), E("FirefliesDeed", 5),
                E("CandyCane", min: 3),
                Decor("a Father Christmas statue", 0x3C40, w: 3), Decor("Santa's bag", 0x3C53, w: 3),
                Decor("a present", 0x3C54, w: 3), Decor("a present", 0x3C55, w: 3), Decor("a present", 0x3C56, w: 3),
                Decor("an eggnog tray", 0x3C59, w: 3), Decor("a pan of Christmas cookies", 0x3C5B, w: 3),
                Decor("a tray of candy canes", 0x3C5C, w: 3), Decor("a holiday platter", 0x3C5D, w: 3),
                Decor("a roast turkey", 0x3C5E, w: 3), Decor("a gingerbread house", 0x3C5F, w: 3),
                Decor("a decorated Christmas tree", 0x3C52, w: 2), Decor("a garland", 0x3C61, 0x3C62, w: 3),
                Decor("a holiday painting", 0x3C63, 0x3C64, w: 3), Decor("a holiday painting", 0x3C65, 0x3C66, w: 3),
                Decor("a snow-covered painting", 0x3C68, 0x3C67, w: 3), Decor("a snow-covered shield", 0x3C6A, 0x3C69, w: 3),
            ],
        },
        ["spring"] = new()
        {
            Label = "Spring",
            Items =
            [
                E("FlowerGarland", 15), E("@Seed", 25, min: 2, per: 1), E("PottedPlant", 10), E("PottedPlant1", 10),
                E("PottedCactus", 10), E("ValentineChocolate", 15), E("HeartShapedBox", 15),
            ],
        },
    };

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

    /// <summary>cc-P763: the per-key theme first (AchievementTheme), then the category's.</summary>
    public BoxTheme ThemeForAchievement(string key, string category) =>
        AchievementTheme.TryGetValue(key, out var name) && Enum.TryParse<BoxTheme>(name, true, out var theme)
            ? theme
            : ThemeForCategory(category);

    /// <summary>cc-P763: a guild's promotion theme at a rank, or null when its promotions give no box.</summary>
    public BoxTheme? ThemeForGuildRank(string guildKey, int rank)
    {
        if (!GuildTheme.TryGetValue(guildKey, out var text))
        {
            return null;
        }

        var parts = text.Split('|');
        var pick = parts.Length > 1 && rank % 2 == 0 ? parts[1] : parts[0];
        return Enum.TryParse<BoxTheme>(pick.Trim(), true, out var theme) ? theme : null;
    }

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

    public RarityStep RarityOf(BoxRarity rarity) => RarityBonus[Math.Clamp((int)rarity, 0, RarityBonus.Length - 1)];

    public PoolEntry[] PoolEntries(BoxPool pool) => Pools.TryGetValue(pool.ToString(), out var e) ? e : [];

    public ThemeDef ThemeOf(BoxTheme theme) => Themes.TryGetValue(theme.ToString(), out var d) ? d : new ThemeDef();

    /// <summary>cc-P763: the rarity for a roll 0 to 100 at a grade (the odds rows, Common first).</summary>
    public BoxRarity RarityForRoll(int grade, double roll)
    {
        var row = RarityOdds[Math.Clamp(grade, 1, 6) - 1];
        var at = 0.0;
        for (var i = 0; i < row.Length && i < 4; i++)
        {
            at += row[i];
            if (roll < at)
            {
                return (BoxRarity)i;
            }
        }

        // A roll at the very top: the last rarity with any odds.
        for (var i = Math.Min(row.Length, 4) - 1; i >= 0; i--)
        {
            if (row[i] > 0)
            {
                return (BoxRarity)i;
            }
        }

        return BoxRarity.Common;
    }

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
        public LadderStep[]? Grades { get; set; }
        public LadderStep[]? MetalLadder { get; set; }
        public LadderStep[]? LeatherLadder { get; set; }
        public LadderStep[]? WoodLadder { get; set; }
        public double[][]? RarityOdds { get; set; }
        public RarityStep[]? RarityBonus { get; set; }
        public int[]? AchievementApCeilings { get; set; }
        public Dictionary<string, int>? AchievementTier { get; set; }
        public Dictionary<string, string>? CategoryTheme { get; set; }
        public Dictionary<string, string>? AchievementTheme { get; set; }
        public int? BossDefaultTier { get; set; }
        public Dictionary<string, int>? BossTier { get; set; }
        public int[]? LeagueRankTier { get; set; }
        public int? DungeonClearTier { get; set; }
        public int? LoopTier { get; set; }
        public int? FishGrade { get; set; }
        public int? RareFishGrade { get; set; }
        public int[]? SosGrade { get; set; }
        public int? PlantGrade { get; set; }
        public Dictionary<string, string>? GuildTheme { get; set; }
        public int? LeagueDiveEvery { get; set; }
        public int[]? ReagentBagCut { get; set; }
        public string[]? BagOfHoldingNames { get; set; }
        public int[]? BagOfHoldingCaps { get; set; }
        public int[][]? Trophies { get; set; }
        public Dictionary<string, PoolEntry[]>? Pools { get; set; }
        public Dictionary<string, ThemeDef>? Themes { get; set; }
        public Dictionary<string, SeasonDef>? Seasons { get; set; }
        public int? AbandonedBoxDays { get; set; }
    }

    public static string ToJson(LootTable t) =>
        JsonSerializer.Serialize(
            new FileForm
            {
                About = "Loot boxes (cc-P79, F-40; contents, rarity and seasons cc-P763). Six grades; arrays are grade 1 to 6. " +
                        "RarityOdds: per grade, percent Common, Rare, Epic, Legendary. Pools, Themes and Seasons hold the " +
                        "contents: Item is a type name or an @special. Edit and run [LootBox reload; a bad file is refused whole.",
                Slots = t.Slots, Gold = t.Gold, Renown = t.Renown, PoolWeights = t.PoolWeights, Grades = t.Grades,
                MetalLadder = t.MetalLadder, LeatherLadder = t.LeatherLadder, WoodLadder = t.WoodLadder,
                RarityOdds = t.RarityOdds, RarityBonus = t.RarityBonus,
                AchievementApCeilings = t.AchievementApCeilings, AchievementTier = t.AchievementTier,
                CategoryTheme = t.CategoryTheme, AchievementTheme = t.AchievementTheme, BossDefaultTier = t.BossDefaultTier,
                BossTier = t.BossTier, LeagueRankTier = t.LeagueRankTier, DungeonClearTier = t.DungeonClearTier,
                LoopTier = t.LoopTier, FishGrade = t.FishGrade, RareFishGrade = t.RareFishGrade, SosGrade = t.SosGrade,
                PlantGrade = t.PlantGrade, GuildTheme = t.GuildTheme, LeagueDiveEvery = t.LeagueDiveEvery,
                ReagentBagCut = t.ReagentBagCut, BagOfHoldingNames = t.BagOfHoldingNames, BagOfHoldingCaps = t.BagOfHoldingCaps,
                Trophies = t.Trophies, Pools = t.Pools, Themes = t.Themes, Seasons = t.Seasons,
                AbandonedBoxDays = t.AbandonedBoxDays
            },
            Json
        );

    private static Dictionary<string, TV> Merged<TV>(Dictionary<string, TV>? fromFile, Dictionary<string, TV> defaults)
    {
        var d = new Dictionary<string, TV>(defaults, StringComparer.OrdinalIgnoreCase);
        if (fromFile != null)
        {
            foreach (var (k, v) in fromFile)
            {
                d[k] = v;
            }
        }

        return d;
    }

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
            // cc-P763: a pool row the file leaves out (Tools, in a file from before it) keeps its default.
            PoolWeights = Merged(f.PoolWeights, d.PoolWeights),
            Grades = f.Grades ?? d.Grades,
            MetalLadder = f.MetalLadder ?? d.MetalLadder,
            LeatherLadder = f.LeatherLadder ?? d.LeatherLadder,
            WoodLadder = f.WoodLadder ?? d.WoodLadder,
            RarityOdds = f.RarityOdds ?? d.RarityOdds,
            RarityBonus = f.RarityBonus ?? d.RarityBonus,
            AchievementApCeilings = f.AchievementApCeilings ?? d.AchievementApCeilings,
            AchievementTier = f.AchievementTier != null ? new Dictionary<string, int>(f.AchievementTier, StringComparer.OrdinalIgnoreCase) : d.AchievementTier,
            CategoryTheme = f.CategoryTheme != null ? new Dictionary<string, string>(f.CategoryTheme, StringComparer.OrdinalIgnoreCase) : d.CategoryTheme,
            AchievementTheme = f.AchievementTheme != null ? new Dictionary<string, string>(f.AchievementTheme, StringComparer.OrdinalIgnoreCase) : d.AchievementTheme,
            BossDefaultTier = f.BossDefaultTier ?? d.BossDefaultTier,
            BossTier = f.BossTier != null ? new Dictionary<string, int>(f.BossTier, StringComparer.OrdinalIgnoreCase) : d.BossTier,
            LeagueRankTier = f.LeagueRankTier ?? d.LeagueRankTier,
            DungeonClearTier = f.DungeonClearTier ?? d.DungeonClearTier,
            LoopTier = f.LoopTier ?? d.LoopTier,
            FishGrade = f.FishGrade ?? d.FishGrade,
            RareFishGrade = f.RareFishGrade ?? d.RareFishGrade,
            SosGrade = f.SosGrade ?? d.SosGrade,
            PlantGrade = f.PlantGrade ?? d.PlantGrade,
            GuildTheme = f.GuildTheme != null ? new Dictionary<string, string>(f.GuildTheme, StringComparer.OrdinalIgnoreCase) : d.GuildTheme,
            LeagueDiveEvery = f.LeagueDiveEvery ?? d.LeagueDiveEvery,
            ReagentBagCut = f.ReagentBagCut ?? d.ReagentBagCut,
            BagOfHoldingNames = f.BagOfHoldingNames ?? d.BagOfHoldingNames,
            BagOfHoldingCaps = f.BagOfHoldingCaps ?? d.BagOfHoldingCaps,
            Trophies = f.Trophies ?? d.Trophies,
            Pools = Merged(f.Pools, d.Pools),
            Themes = Merged(f.Themes, d.Themes),
            Seasons = Merged(f.Seasons, d.Seasons),
            AbandonedBoxDays = f.AbandonedBoxDays ?? d.AbandonedBoxDays
        };

        void SixOf(string name, Array a)
        {
            if (a.Length != 6)
            {
                errors.Add($"{name}: {a.Length} values, expected 6 (grade 1 to 6).");
            }
        }

        SixOf("slots", t.Slots);
        SixOf("gold", t.Gold);
        SixOf("renown", t.Renown);
        SixOf("grades", t.Grades);
        SixOf("metalLadder", t.MetalLadder);
        SixOf("leatherLadder", t.LeatherLadder);
        SixOf("woodLadder", t.WoodLadder);
        SixOf("rarityOdds", t.RarityOdds);

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
            errors.Add("slots: every grade needs at least 2 (the theme and currency slots).");
        }

        if (Array.Exists(t.Gold, g => g < 0) || Array.Exists(t.Renown, r => r < 0))
        {
            errors.Add("gold or renown: negative.");
        }

        if (t.AchievementApCeilings.Length != 5)
        {
            errors.Add($"achievementApCeilings: {t.AchievementApCeilings.Length} values, expected 5 (grades 1 to 5).");
        }

        foreach (var (key, theme) in t.CategoryTheme)
        {
            if (!Enum.TryParse<BoxTheme>(theme, true, out _))
            {
                errors.Add($"categoryTheme.{key}: '{theme}' is not a box theme.");
            }
        }

        foreach (var (key, theme) in t.AchievementTheme)
        {
            if (!Enum.TryParse<BoxTheme>(theme, true, out _))
            {
                errors.Add($"achievementTheme.{key}: '{theme}' is not a box theme.");
            }
        }

        foreach (var (key, text) in t.GuildTheme)
        {
            foreach (var part in text.Split('|'))
            {
                if (!Enum.TryParse<BoxTheme>(part.Trim(), true, out _))
                {
                    errors.Add($"guildTheme.{key}: '{part}' is not a box theme.");
                }
            }
        }

        if (t.LeagueRankTier.Length == 0 || Array.Exists(t.LeagueRankTier, x => x is < 1 or > 6))
        {
            errors.Add("leagueRankTier: needs grades 1 to 6.");
        }

        if (t.AbandonedBoxDays < 1)
        {
            errors.Add("abandonedBoxDays: at least 1.");
        }

        // cc-P763: the rarity, the sources and the bags.
        for (var g = 0; g < t.RarityOdds.Length; g++)
        {
            var row = t.RarityOdds[g];
            if (row is not { Length: 4 } || Array.Exists(row, x => x < 0) || Math.Abs(Sum(row) - 100) > 0.001)
            {
                errors.Add($"rarityOdds[{g + 1}]: needs 4 percents (Common, Rare, Epic, Legendary), none negative, summing to 100.");
            }
        }

        if (t.RarityBonus.Length != 4)
        {
            errors.Add($"rarityBonus: {t.RarityBonus.Length} rows, expected 4 (Common, Rare, Epic, Legendary).");
        }

        foreach (var step in t.RarityBonus)
        {
            if (step.ExtraSlots < 0 || step.StackMultiplier < 1 || step.PoolShift < 0 ||
                !Enum.TryParse<FanfareLevel>(step.Fanfare, true, out _))
            {
                errors.Add($"rarityBonus.{step.Name}: extraSlots and poolShift at least 0, stackMultiplier at least 1, " +
                           "fanfare Minor, Major or Capstone.");
            }
        }

        foreach (var (name, grade) in new[] { ("fishGrade", t.FishGrade), ("rareFishGrade", t.RareFishGrade), ("plantGrade", t.PlantGrade) })
        {
            if (grade is < 1 or > 6)
            {
                errors.Add($"{name}: a grade 1 to 6.");
            }
        }

        if (t.SosGrade.Length != 4 || Array.Exists(t.SosGrade, x => x is < 1 or > 6))
        {
            errors.Add("sosGrade: 4 grades (SOS levels 1 to 4), each 1 to 6.");
        }

        if (t.LeagueDiveEvery < 1)
        {
            errors.Add("leagueDiveEvery: at least 1.");
        }

        if (t.ReagentBagCut.Length != 4 || Array.Exists(t.ReagentBagCut, x => x is < 0 or > 100))
        {
            errors.Add("reagentBagCut: 4 percents (Common to Legendary), 0 to 100.");
        }

        if (t.BagOfHoldingNames.Length != 5 || t.BagOfHoldingCaps.Length != 5 || Array.Exists(t.BagOfHoldingCaps, x => x < 1))
        {
            errors.Add("bagOfHoldingNames and bagOfHoldingCaps: 5 each (sizes 1 to 5), every cap at least 1.");
        }

        if (t.Trophies.Length == 0 ||
            Array.Exists(t.Trophies, p => p is not { Length: >= 1 and <= 2 } || Array.Exists(p, id => id is < 1 or > 0xFFFF)))
        {
            errors.Add("trophies: at least one, each one or two item IDs.");
        }

        // cc-P763: the contents. Every pool, every theme and every season with entries that can be made.
        foreach (var pool in Enum.GetNames<BoxPool>())
        {
            if (!t.Pools.TryGetValue(pool, out var entries) || entries.Length == 0)
            {
                errors.Add($"pools.{pool}: no entries.");
                continue;
            }

            CheckEntries($"pools.{pool}", entries, errors);
        }

        foreach (var theme in Enum.GetNames<BoxTheme>())
        {
            if (!t.Themes.TryGetValue(theme, out var def) || def.Slot.Length == 0)
            {
                errors.Add($"themes.{theme}: no slot entries.");
                continue;
            }

            if (def.Ladder is not ("Metal" or "Wood" or "Leather" or "Craft" or "Grade"))
            {
                errors.Add($"themes.{theme}.ladder: '{def.Ladder}' is not Metal, Wood, Leather, Craft or Grade.");
            }

            if (def.Epic.Length == 0 || def.Legendary.Length == 0)
            {
                errors.Add($"themes.{theme}: the Epic and Legendary lists need an entry each.");
            }

            CheckEntries($"themes.{theme}.slot", def.Slot, errors);
            CheckEntries($"themes.{theme}.epic", def.Epic, errors);
            CheckEntries($"themes.{theme}.legendary", def.Legendary, errors);
        }

        foreach (var (name, season) in t.Seasons)
        {
            if (season.Items.Length == 0 || string.IsNullOrWhiteSpace(season.Label))
            {
                errors.Add($"seasons.{name}: needs a label and entries.");
                continue;
            }

            CheckEntries($"seasons.{name}", season.Items, errors);
        }

        return errors.Count == 0 ? t : null;
    }

    private static double Sum(double[] row)
    {
        var s = 0.0;
        foreach (var x in row)
        {
            s += x;
        }

        return s;
    }

    private static void CheckEntries(string where, PoolEntry[] entries, List<string> errors)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            var at = $"{where}[{i}] ({e.Item})";
            if (e.Weight < 0 || e.Min < 0 || e.Max < 0 || e.PerGrade < 0 || e.MinGrade is < 1 or > 6 || e.MaxGrade < e.MinGrade)
            {
                errors.Add($"{at}: weight, min, max and perGrade at least 0; minGrade 1 to 6, maxGrade not below it.");
            }

            if (e.MinRarity != null && !Enum.TryParse<BoxRarity>(e.MinRarity, true, out _))
            {
                errors.Add($"{at}: minRarity '{e.MinRarity}' is not Common, Rare, Epic or Legendary.");
            }

            if (e.Chance != null && (e.Chance.Length != 4 || Array.Exists(e.Chance, c => c is < 0 or > 100)))
            {
                errors.Add($"{at}: chance needs 4 percents (Common to Legendary), 0 to 100.");
            }

            if (e.Skill != null && !Enum.TryParse<SkillName>(e.Skill, true, out _))
            {
                errors.Add($"{at}: skill '{e.Skill}' is not a skill.");
            }

            if (LootBoxRoller.CheckEntry(e) is { } problem)
            {
                errors.Add($"{at}: {problem}");
            }
        }
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
