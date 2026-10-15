// cc-P123: the six 2016 crafts' reward calculators, ported from ServUO pub57 Scripts/Services/BulkOrders/Rewards/Rewards.cs
// (Tinkering :1015-1195, Carpentry :1198-1388, Inscription :1391-1512, Cooking :1515-1646, Fletching :1649-1808, Alchemy
// :1811-1946), on pinned's RewardCalculator as the smith and tailor ones are.
//
// Points (what a turn-in is worth in the rewards gump) and the gold tables are ServUO's. Gold for a small deed is half the
// shop price of what it asks for when a shop sells it (BulkOrderSystem.ComputeGold, P122); these tables pay the rest. Fame
// is pinned's (points / 50, squared), as ServUO's RewardCalculator has it. The reward lists themselves are in
// BODRewardCollections.cs; the old per-deed reward groups are not used under the 2016 rules, so each calculator has one
// empty group (pinned's LookupRewards needs one).
//
// Corrected from ServUO (fixed on purpose, each marked "ServUO:" below and listed in the notes): the gold cells where a
// digit has plainly slipped, in rows whose neighbours run in order: 60000 (for 6000), 1250 (for 12500), 1350 (for 13500)
// and 1750 (for 17500). A 5-part regular yew fletching deed of 10 would otherwise pay sixty thousand. (ServUO's own row
// index reads the carpentry and tinkering tables one row short of their labels, so some marked cells are never read;
// they are corrected all the same, and the index is ServUO's.) ServUO's gold tables for Inscription, Cooking
// and Alchemy have no 2-part row and Inscription none past 5 parts; a list that lost an item the shard does not have
// (CraftBulkEntries) can be 2 parts, so it is paid as the 3-part row, and anything longer as the longest row.

using System;
using Server.Items;

namespace Server.Engines.BulkOrders;

public abstract class CraftRewardCalculator : RewardCalculator
{
    protected CraftRewardCalculator() => Groups = [new RewardGroup(0)];

    protected static int QuantityPoints(int quantity) =>
        quantity switch
        {
            10 => 10,
            15 => 25,
            20 => 50,
            _  => 0
        };

    protected static int QuantityIndex(int quantity) => quantity == 20 ? 2 : quantity == 15 ? 1 : 0;

    protected static int WoodIndex(BulkMaterialType material) =>
        material switch
        {
            BulkMaterialType.Frostwood => 5,
            BulkMaterialType.Bloodwood => 4,
            BulkMaterialType.Heartwood => 3,
            BulkMaterialType.YewWood   => 2,
            BulkMaterialType.AshWood   => 1,
            _                          => 0
        };

    protected static int WoodPoints(BulkMaterialType material) =>
        material switch
        {
            BulkMaterialType.OakWood   => 300,
            BulkMaterialType.AshWood   => 350,
            BulkMaterialType.YewWood   => 400,
            BulkMaterialType.Heartwood => 450,
            BulkMaterialType.Bloodwood => 500,
            BulkMaterialType.Frostwood => 550,
            _                          => 0
        };

    /// <summary>ServUO's last step: the table's gold, plus or minus about ten percent.</summary>
    protected static int Vary(int gold) => Utility.RandomMinMax(gold * 9 / 10, gold * 10 / 9);

    /// <summary>The per-part tables (Inscription, Cooking, Alchemy): ServUO's row for the part count, see the header.</summary>
    protected static int ByParts(int[][] table, int itemCount, int quantity)
    {
        var row = Math.Clamp(itemCount, 1, table.Length) - 1;

        if (table[row].Length == 0)
        {
            row++; // no 2-part row: paid as 3 parts
        }

        return Vary(table[row][QuantityIndex(quantity)]);
    }
}

// -- Tinkering (Rewards.cs:1015-1195) ------------------------------------------------------------------------------------

public sealed class TinkeringRewardCalculator : CraftRewardCalculator
{
    public static readonly TinkeringRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        points += itemCount switch
        {
            3 => 200,
            4 => 300,
            5 => 350,
            6 => 400,
            _ => 0
        };

        if (material is >= BulkMaterialType.DullCopper and <= BulkMaterialType.Valorite)
        {
            points += 200 + 50 * (material - BulkMaterialType.DullCopper);
        }

        if (exceptional)
        {
            points += 200;
        }

        return points;
    }

    private static readonly int[][][] _goldTable =
    [
        [ // 1-part (regular)
            [150, 150, 225, 225, 300, 300, 350, 350, 400],
            [225, 225, 325, 325, 450, 450, 450, 500, 500],
            [300, 400, 500, 500, 600, 750, 750, 900, 900]
        ],
        [ // 1-part (exceptional)
            [300, 300, 400, 500, 600, 600, 600, 700, 700],
            [450, 450, 650, 750, 900, 900, 900, 1000, 1000],
            [600, 750, 850, 1000, 1200, 1800, 1800, 1800, 2000]
        ],
        [ // 3-part (regular)
            [2500, 2500, 2500, 3500, 3500, 3500, 4500, 4500, 4500],
            [4000, 4000, 4000, 5500, 5500, 5500, 7000, 7000, 7000],
            [6000, 7000, 7500, 8000, 8000, 9000, 9000, 10000, 10000]
        ],
        [ // 3-part (exceptional)
            [4000, 4000, 5000, 5750, 6500, 6500, 6500, 7500, 8500],
            [6500, 6500, 7500, 8500, 10000, 10000, 10000, 12500, 12500],
            [8000, 10000, 10000, 12500, 12500, 15000, 15000, 20000, 20000]
        ],
        [ // 4-part (regular)
            [4000, 4000, 4000, 5000, 5000, 5000, 6000, 6000, 6000],
            [6000, 6000, 6000, 7500, 7500, 7500, 9000, 9000, 9000],
            [8000, 9000, 9500, 10000, 10000, 15000, 17500, 17500, 17500]
        ],
        [ // 4-part (exceptional)
            [5000, 5000, 6000, 6750, 7500, 7500, 8500, 8500, 9500],
            [7500, 7500, 8500, 9500, 11250, 11250, 11250, 15000, 15000],
            [10000, 12500, 12500, 15000, 15000, 20000, 20000, 25000, 25000] // ServUO: 1250, 1250
        ],
        [ // 4-part (regular)
            [4000, 4000, 4000, 5000, 5000, 5000, 7000, 7000, 7000],
            [6000, 6000, 6000, 7500, 7500, 7500, 9000, 9000, 9000],
            [8000, 9000, 9500, 10000, 10000, 15000, 15000, 20000, 20000]
        ],
        [ // 4-part (exceptional)
            [5000, 5000, 6000, 6750, 7500, 7500, 9000, 9000, 15000],
            [7500, 7500, 8500, 9500, 11250, 11250, 15000, 15000, 15000],
            [10000, 12500, 12500, 15000, 15000, 20000, 20000, 25000, 25000] // ServUO: 1250, 1250
        ],
        [ // 5-part (regular)
            [5000, 5000, 6000, 6000, 7500, 7500, 9000, 9000, 10500], // ServUO: 60000
            [7500, 7500, 7500, 11250, 11250, 11250, 15000, 15000, 15000],
            [10000, 10000, 12500, 15000, 15000, 20000, 20000, 25000, 25000] // ServUO: 1250
        ],
        [ // 5-part (exceptional)
            [7500, 7500, 8500, 9500, 10000, 10000, 12500, 12500, 15000],
            [11250, 11250, 12500, 13500, 15000, 15000, 20000, 20000, 25000], // ServUO: 1250
            [15000, 17500, 17500, 20000, 20000, 30000, 30000, 40000, 50000] // ServUO: 1750, 1750
        ]
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var typeIndex = (itemCount == 6 ? 3 : itemCount == 5 ? 2 : itemCount == 4 ? 1 : 0) * 2 + (exceptional ? 1 : 0);
        var mtrlIndex = material is >= BulkMaterialType.DullCopper and <= BulkMaterialType.Valorite
            ? 1 + (material - BulkMaterialType.DullCopper)
            : 0;

        return Vary(_goldTable[typeIndex][QuantityIndex(quantity)][mtrlIndex]);
    }
}

// -- Carpentry (Rewards.cs:1198-1388) ------------------------------------------------------------------------------------

public sealed class CarpentryRewardCalculator : CraftRewardCalculator
{
    public static readonly CarpentryRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        if (exceptional)
        {
            points += 200;
        }

        points += WoodPoints(material);

        if (itemCount > 1)
        {
            points += LookupTypePoints(_types, type);
        }

        return points;
    }

    private static readonly RewardType[] _types =
    [
        new(250, typeof(TallCabinet), typeof(ShortCabinet)),
        new(250, typeof(RedArmoire), typeof(ElegantArmoire), typeof(MapleArmoire), typeof(CherryArmoire)),
        new(
            300, typeof(PlainWoodenChest), typeof(OrnateWoodenChest), typeof(GildedWoodenChest), typeof(WoodenFootLocker),
            typeof(FinishedWoodenChest)
        ),
        new(
            350, typeof(WildStaff), typeof(ArcanistsWildStaff), typeof(AncientWildStaff), typeof(ThornedWildStaff),
            typeof(HardenedWildStaff)
        ),
        new(250, typeof(LapHarp), typeof(Lute), typeof(Drums), typeof(Harp)),
        new(200, typeof(GnarledStaff), typeof(QuarterStaff), typeof(ShepherdsCrook), typeof(Tetsubo), typeof(Bokuto)),
        new(300, typeof(WoodenBox), typeof(EmptyBookcase), typeof(WoodenBench), typeof(WoodenThrone))
    ];

    private static readonly int[][][] _goldTable =
    [
        [ // 1-part (regular)
            [150, 150, 225, 225, 300, 300],
            [225, 225, 325, 325, 450, 450],
            [300, 400, 500, 500, 600, 750]
        ],
        [ // 1-part (exceptional)
            [300, 300, 400, 500, 600, 600],
            [450, 450, 650, 750, 900, 900],
            [600, 750, 850, 1000, 1200, 1800]
        ],
        [ // 2-part (regular)
            [2000, 2000, 2000, 2500, 2500, 2500],
            [3000, 3000, 3000, 3750, 3750, 3750],
            [4000, 4500, 4500, 5000, 5000, 7500]
        ],
        [ // 2-part (exceptional)
            [2500, 2500, 3000, 3350, 3350, 4000],
            [3750, 3750, 4250, 4750, 5200, 5200],
            [5000, 6100, 6100, 7000, 7000, 10000]
        ],
        [ // 4-part (regular)
            [4000, 4000, 4000, 5000, 5000, 5000],
            [6000, 6000, 6000, 7500, 7500, 7500],
            [8000, 9000, 9500, 10000, 10000, 15000]
        ],
        [ // 4-part (exceptional)
            [5000, 5000, 6000, 6750, 7500, 7500],
            [7500, 7500, 8500, 9500, 11250, 11250],
            [10000, 12500, 12500, 15000, 15000, 20000] // ServUO: 1250, 1250
        ],
        [ // 5-part (regular)
            [5000, 5000, 6000, 6000, 7500, 7500], // ServUO: 60000
            [7500, 7500, 7500, 11250, 11250, 11250],
            [10000, 10000, 12500, 15000, 15000, 20000] // ServUO: 1250
        ],
        [ // 5-part (exceptional)
            [7500, 7500, 8500, 9500, 10000, 10000],
            [11250, 11250, 12500, 13500, 15000, 15000], // ServUO: 1250, 1350
            [15000, 17500, 17500, 20000, 20000, 30000] // ServUO: 1750, 1750
        ]
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        // ServUO's index: 5 parts, 4 parts, else the first rows (a 2-part list reads the 1-part row, as ServUO's does).
        var typeIndex = (itemCount == 5 ? 2 : itemCount == 4 ? 1 : 0) * 2 + (exceptional ? 1 : 0);

        return Vary(_goldTable[typeIndex][QuantityIndex(quantity)][WoodIndex(material)]);
    }
}

// -- Fletching (Rewards.cs:1649-1808) ------------------------------------------------------------------------------------

public sealed class FletchingRewardCalculator : CraftRewardCalculator
{
    public static readonly FletchingRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        if (exceptional)
        {
            points += 200;
        }

        points += WoodPoints(material);

        if (itemCount > 1)
        {
            points += LookupTypePoints(_types, type);
        }

        return points;
    }

    private static readonly RewardType[] _types =
    [
        new(200, typeof(Arrow), typeof(Bolt)),
        new(300, typeof(Bow), typeof(CompositeBow), typeof(Yumi)),
        new(300, typeof(Crossbow), typeof(HeavyCrossbow), typeof(RepeatingCrossbow)),
        new(
            350, typeof(MagicalShortbow), typeof(RangersShortbow), typeof(LightweightShortbow), typeof(MysticalShortbow),
            typeof(AssassinsShortbow)
        ),
        new(
            250, typeof(ElvenCompositeLongbow), typeof(BarbedLongbow), typeof(SlayerLongbow), typeof(FrozenLongbow),
            typeof(LongbowOfMight)
        )
    ];

    private static readonly int[][][] _goldTable =
    [
        [ // 1-part (regular)
            [150, 150, 225, 225, 300, 300],
            [225, 225, 325, 325, 450, 450],
            [300, 400, 500, 500, 600, 750]
        ],
        [ // 1-part (exceptional)
            [300, 300, 400, 500, 600, 600],
            [450, 450, 650, 750, 900, 900],
            [600, 750, 850, 1000, 1200, 1800]
        ],
        [ // 4-part (regular)
            [4000, 4000, 4000, 5000, 5000, 5000],
            [6000, 6000, 6000, 7500, 7500, 7500],
            [8000, 9000, 9500, 10000, 10000, 15000]
        ],
        [ // 4-part (exceptional)
            [5000, 5000, 6000, 6750, 7500, 7500],
            [7500, 7500, 8500, 9500, 11250, 11250],
            [10000, 12500, 12500, 15000, 15000, 20000] // ServUO: 1250, 1250
        ],
        [ // 5-part (regular)
            [5000, 5000, 6000, 6000, 7500, 7500], // ServUO: 60000
            [7500, 7500, 7500, 11250, 11250, 11250],
            [10000, 10000, 12500, 15000, 15000, 20000] // ServUO: 1250
        ],
        [ // 5-part (exceptional)
            [7500, 7500, 8500, 9500, 10000, 10000],
            [11250, 11250, 12500, 13500, 15000, 15000], // ServUO: 1250, 1350
            [15000, 17500, 17500, 20000, 20000, 30000] // ServUO: 1750, 1750
        ]
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var typeIndex = (itemCount == 5 ? 2 : itemCount == 4 ? 1 : 0) * 2 + (exceptional ? 1 : 0);

        return Vary(_goldTable[typeIndex][QuantityIndex(quantity)][WoodIndex(material)]);
    }
}

// -- Inscription (Rewards.cs:1391-1512) ----------------------------------------------------------------------------------

public sealed class InscriptionRewardCalculator : CraftRewardCalculator
{
    public static readonly InscriptionRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        if (itemCount > 1)
        {
            points += LookupTypePoints(_types, type);
        }

        return points;
    }

    // ServUO lists RunicAtlas with the books; there is none on the shard (CraftBulkEntries).
    private static readonly RewardType[] _types =
    [
        new(200, typeof(ClumsyScroll), typeof(FeeblemindScroll), typeof(WeakenScroll)),
        new(300, typeof(CurseScroll), typeof(GreaterHealScroll), typeof(RecallScroll)),
        new(300, typeof(PoisonStrikeScroll), typeof(WitherScroll), typeof(StrangleScroll)),
        new(250, typeof(MindRotScroll), typeof(SummonFamiliarScroll), typeof(AnimateDeadScroll), typeof(HorrificBeastScroll)),
        new(200, typeof(HealScroll), typeof(AgilityScroll), typeof(CunningScroll), typeof(CureScroll), typeof(StrengthScroll)),
        new(
            250, typeof(BloodOathScroll), typeof(CorpseSkinScroll), typeof(CurseWeaponScroll), typeof(EvilOmenScroll),
            typeof(PainSpikeScroll)
        ),
        new(
            300, typeof(BladeSpiritsScroll), typeof(DispelFieldScroll), typeof(MagicReflectScroll), typeof(ParalyzeScroll),
            typeof(SummonCreatureScroll)
        ),
        new(
            350, typeof(ChainLightningScroll), typeof(FlamestrikeScroll), typeof(ManaVampireScroll), typeof(MeteorSwarmScroll),
            typeof(PolymorphScroll)
        ),
        new(
            400, typeof(SummonAirElementalScroll), typeof(SummonDaemonScroll), typeof(SummonEarthElementalScroll),
            typeof(SummonFireElementalScroll), typeof(SummonWaterElementalScroll)
        ),
        new(450, typeof(Spellbook), typeof(NecromancerSpellbook), typeof(Runebook))
    ];

    private static readonly int[][] _goldTable =
    [
        [150, 225, 300],      // singles
        [],                   // no 2 piece
        [4000, 6000, 8000],   // 3-part
        [5000, 7500, 10000],  // 4-part
        [7500, 11250, 15000]  // 5-part
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type) =>
        ByParts(_goldTable, itemCount, quantity);
}

// -- Cooking (Rewards.cs:1515-1646) --------------------------------------------------------------------------------------

public sealed class CookingRewardCalculator : CraftRewardCalculator
{
    public static readonly CookingRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        if (exceptional)
        {
            points += 200;
        }

        if (itemCount > 1)
        {
            points += LookupTypePoints(_types, type);
        }

        return points;
    }

    // ServUO also lists SweetCocoaButter, ThreeTieredCake, EnchantedApple and GrapesOfWrath, which the shard does not have.
    private static readonly RewardType[] _types =
    [
        new(200, typeof(SackFlour), typeof(Dough)),
        new(250, typeof(UnbakedFruitPie), typeof(UnbakedPeachCobbler), typeof(UnbakedApplePie), typeof(UnbakedPumpkinPie)),
        new(300, typeof(CookedBird), typeof(FishSteak), typeof(FriedEggs), typeof(LambLeg), typeof(Ribs)),
        new(350, typeof(Cookies), typeof(Cake), typeof(Muffins)),
        new(400, typeof(TribalPaint), typeof(EggBomb)),
        new(450, typeof(MisoSoup), typeof(WhiteMisoSoup), typeof(RedMisoSoup), typeof(AwaseMisoSoup)),
        new(500, typeof(WasabiClumps), typeof(SushiRolls), typeof(SushiPlatter), typeof(GreenTea))
    ];

    private static readonly int[][] _goldTable =
    [
        [150, 225, 300],      // singles
        [],                   // no 2 piece
        [4000, 6000, 8000],   // 3-part
        [5000, 7500, 10000],  // 4-part
        [7500, 11250, 15000], // 5-part
        [7500, 11250, 15000]  // 6-part
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type) =>
        ByParts(_goldTable, itemCount, quantity);
}

// -- Alchemy (Rewards.cs:1811-1946) --------------------------------------------------------------------------------------

public sealed class AlchemyRewardCalculator : CraftRewardCalculator
{
    public static readonly AlchemyRewardCalculator Instance = new();

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var points = QuantityPoints(quantity);

        points += itemCount switch
        {
            3 when type == typeof(RefreshPotion) || type == typeof(HealPotion) || type == typeof(CurePotion) => 250,
            3 => 300,
            4 => 200,
            5 => 400,
            6 => 350,
            _ => 0
        };

        return points;
    }

    private static readonly int[][] _goldTable =
    [
        [150, 225, 300],      // singles
        [],                   // no 2 piece
        [4000, 6000, 8000],   // 3-part
        [5000, 7500, 10000],  // 4-part
        [7500, 11250, 15000], // 5-part
        [7500, 11250, 15000]  // 6-part
    ];

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type) =>
        ByParts(_goldTable, itemCount, quantity);
}
