// cc-P123: the six 2016 crafts' bulk order entries, generated from ServUO pub57's Data/Bulk Orders/<craft>/*.cfg by
// shard-migration notes/cc-P123-tools/gen_entries.py (not edited by hand). ServUO loads these files at run time; this
// shard's image carries pinned's Data folder only (Blacksmith and Tailoring), so the six crafts' lists are compiled in.
//
// Each row is ServUO's: the item type, its graphic and, where the file names one, its cliloc; a row with no cliloc
// gets the graphic's name as ServUO's loader gives it (SmallBulkEntry.cs:200). ServUO's fourth column, the graphic's
// hue in the deed gump, is not carried: pinned's SmallBulkEntry and deed gumps have no hue (a potion shows an
// uncoloured bottle, named by its cliloc).
//
// Renamed upstream, mapped to pinned's type: FlameStrikeScroll -> FlamestrikeScroll, MagicUntrapScroll ->
// MagicUnTrapScroll, TelekinisisScroll -> TelekinesisScroll, SmithyHammer -> SmithHammer (pinned has one smith's
// hammer).
// Not on the shard, so dropped where ServUO lists them (13 of 413 rows): Clippers, EnchantedApple, GrapesOfWrath,
// MetalContainerEngraver, NexusCore, RunicAtlas, SweetCocoaButter, ThreeTieredCake, WrathGrapes. A large deed keeps the
// rest of its list.

using System;
using Server.Items;

namespace Server.Engines.BulkOrders;

public static class CraftBulkEntries
{
    private static SmallBulkEntry E(Type type, int graphic, int number = 0) =>
        new(type, number != 0 ? number : graphic < 0x4000 ? 1020000 + graphic : 1078872 + graphic, graphic);

    // -- Alchemy ---------------------------------------------------------------------------------------------

    /// <summary>ServUO Alchemy/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] AlchemySmalls =
    [
        E(typeof(LesserHealPotion), 0xF0C, 1041328),
        E(typeof(HealPotion), 0xF0C, 1041329),
        E(typeof(GreaterHealPotion), 0xF0C, 1041330),
        E(typeof(LesserCurePotion), 0xF07, 1041315),
        E(typeof(CurePotion), 0xF07, 1041316),
        E(typeof(GreaterCurePotion), 0xF07, 1041317),
        E(typeof(LesserPoisonPotion), 0xF0A, 1041322),
        E(typeof(PoisonPotion), 0xF0A, 1041323),
        E(typeof(GreaterPoisonPotion), 0xF0A, 1041324),
        E(typeof(ParasiticPotion), 0xF0A, 1072848),
        E(typeof(DarkglowPotion), 0xF0A, 1072849),
        E(typeof(DeadlyPoisonPotion), 0xF0A, 1041325),
        E(typeof(LesserExplosionPotion), 0xF0D, 1041331),
        E(typeof(ExplosionPotion), 0xF0D, 1041332),
        E(typeof(GreaterExplosionPotion), 0xF0D, 1041333),
        E(typeof(ConfusionBlastPotion), 0xF06, 1072106),
        E(typeof(ConflagrationPotion), 0xF06, 1072095),
        E(typeof(GreaterConfusionBlastPotion), 0xF06, 1072109),
        E(typeof(GreaterConflagrationPotion), 0xF06, 1072098),
        E(typeof(RefreshPotion), 0xF0B, 1041326),
        E(typeof(TotalRefreshPotion), 0xF0B, 1041327),
        E(typeof(AgilityPotion), 0xF08, 1041318),
        E(typeof(GreaterAgilityPotion), 0xF08, 1041319),
        E(typeof(StrengthPotion), 0xF09, 1041320),
        E(typeof(GreaterStrengthPotion), 0xF09, 1041321),
        // NexusCore: not on the shard (dropped, as ServUO drops a type it cannot find)
    ];

    /// <summary>ServUO Alchemy/largeexplosive.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeExplosive =
    [
        E(typeof(ExplosionPotion), 0xF0D, 1041332),
        E(typeof(ConfusionBlastPotion), 0xF06, 1072106),
        E(typeof(ConflagrationPotion), 0xF06, 1072095),
        E(typeof(GreaterExplosionPotion), 0xF0D, 1041333),
        E(typeof(GreaterConfusionBlastPotion), 0xF06, 1072109),
        E(typeof(GreaterConflagrationPotion), 0xF06, 1072098),
    ];

    /// <summary>ServUO Alchemy/largegreater.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeGreater =
    [
        E(typeof(TotalRefreshPotion), 0xF0B, 1041327),
        E(typeof(GreaterAgilityPotion), 0xF08, 1041319),
        E(typeof(GreaterStrengthPotion), 0xF09, 1041321),
    ];

    /// <summary>ServUO Alchemy/largelesser.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeLesser =
    [
        E(typeof(LesserHealPotion), 0xF0C, 1041328),
        E(typeof(LesserCurePotion), 0xF07, 1041315),
        E(typeof(LesserPoisonPotion), 0xF0A, 1041322),
        E(typeof(LesserExplosionPotion), 0xF0D, 1041331),
    ];

    /// <summary>ServUO Alchemy/largeregular.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeRegular =
    [
        E(typeof(RefreshPotion), 0xF0B, 1041326),
        E(typeof(HealPotion), 0xF0C, 1041329),
        E(typeof(CurePotion), 0xF07, 1041316),
    ];

    /// <summary>ServUO Alchemy/largetoxic.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeToxic =
    [
        E(typeof(PoisonPotion), 0xF0A, 1041323),
        E(typeof(GreaterPoisonPotion), 0xF0A, 1041324),
        E(typeof(ParasiticPotion), 0xF0A, 1072848),
        E(typeof(DarkglowPotion), 0xF0A, 1072849),
        E(typeof(DeadlyPoisonPotion), 0xF0A, 1041325),
    ];

    // -- Carpentry -------------------------------------------------------------------------------------------

    /// <summary>ServUO Carpentry/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] CarpentrySmalls =
    [
        E(typeof(FootStool), 0xB5E),
        E(typeof(BarrelStaves), 0x1EB1),
        E(typeof(BarrelLid), 0x1DB8),
        E(typeof(Stool), 0xA2A),
        E(typeof(GnarledStaff), 0x13F8),
        E(typeof(QuarterStaff), 0xE89),
        E(typeof(ShepherdsCrook), 0xE81),
        E(typeof(Tetsubo), 0x27A6),
        E(typeof(Bokuto), 0x27A8),
        E(typeof(WoodenBox), 0x9AA),
        E(typeof(SmallCrate), 0x9A9, 1015093),
        E(typeof(MediumCrate), 0xE3F, 1015094),
        E(typeof(LargeCrate), 0xE3C, 1015096),
        E(typeof(WoodenChest), 0xE43),
        E(typeof(EmptyBookcase), 0xA9E),
        E(typeof(WoodenBench), 0xB2D),
        E(typeof(WoodenThrone), 0xB2E, 1015082),
        E(typeof(TallCabinet), 0x2815),
        E(typeof(ShortCabinet), 0x2817),
        E(typeof(RedArmoire), 0x2857),
        E(typeof(ElegantArmoire), 0x2859),
        E(typeof(MapleArmoire), 0x285B),
        E(typeof(CherryArmoire), 0x285D),
        E(typeof(LapHarp), 0xEB2),
        E(typeof(Lute), 0xEB3),
        E(typeof(Drums), 0xE9C),
        E(typeof(Harp), 0xEB1),
        E(typeof(PlainWoodenChest), 0x280B),
        E(typeof(OrnateWoodenChest), 0x280D),
        E(typeof(GildedWoodenChest), 0x280F),
        E(typeof(WoodenFootLocker), 0x2811),
        E(typeof(FinishedWoodenChest), 0x2813),
        E(typeof(WildStaff), 0x2D25, 1031557),
        E(typeof(ArcanistsWildStaff), 0x2D25, 1073549),
        E(typeof(AncientWildStaff), 0x2D25, 1073550),
        E(typeof(ThornedWildStaff), 0x2D25, 1073551),
        E(typeof(HardenedWildStaff), 0x2D25, 1073552),
    ];

    /// <summary>ServUO Carpentry/LargeArmoire.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeArmoire =
    [
        E(typeof(RedArmoire), 0x2857),
        E(typeof(ElegantArmoire), 0x2859),
        E(typeof(MapleArmoire), 0x285B),
        E(typeof(CherryArmoire), 0x285D),
    ];

    /// <summary>ServUO Carpentry/LargeCabinets.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCabinets =
    [
        E(typeof(TallCabinet), 0x2815),
        E(typeof(ShortCabinet), 0x2817),
    ];

    /// <summary>ServUO Carpentry/LargeChests.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeChests =
    [
        E(typeof(PlainWoodenChest), 0x280B),
        E(typeof(OrnateWoodenChest), 0x280D),
        E(typeof(GildedWoodenChest), 0x280F),
        E(typeof(WoodenFootLocker), 0x2811),
        E(typeof(FinishedWoodenChest), 0x2813),
    ];

    /// <summary>ServUO Carpentry/LargeElvenWeapons.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeElvenWeapons =
    [
        E(typeof(WildStaff), 0x2D25, 1031557),
        E(typeof(ArcanistsWildStaff), 0x2D25, 1073549),
        E(typeof(AncientWildStaff), 0x2D25, 1073550),
        E(typeof(ThornedWildStaff), 0x2D25, 1073551),
        E(typeof(HardenedWildStaff), 0x2D25, 1073552),
    ];

    /// <summary>ServUO Carpentry/LargeInstruments.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeInstruments =
    [
        E(typeof(LapHarp), 0xEB2),
        E(typeof(Lute), 0xEB3),
        E(typeof(Drums), 0xE9C),
        E(typeof(Harp), 0xEB1),
    ];

    /// <summary>ServUO Carpentry/LargeWeapons.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeWeapons =
    [
        E(typeof(GnarledStaff), 0x13F8),
        E(typeof(QuarterStaff), 0xE89),
        E(typeof(ShepherdsCrook), 0xE81),
        E(typeof(Tetsubo), 0x27A6),
        E(typeof(Bokuto), 0x27A8),
    ];

    /// <summary>ServUO Carpentry/LargeWoodFurniture.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeWoodFurniture =
    [
        E(typeof(WoodenBox), 0x9AA),
        E(typeof(EmptyBookcase), 0xA9E),
        E(typeof(WoodenBench), 0xB2D),
        E(typeof(WoodenThrone), 0xB2E, 1015082),
    ];

    // -- Cooking ---------------------------------------------------------------------------------------------

    /// <summary>ServUO Cooking/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] CookingSmalls =
    [
        // SweetCocoaButter: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(SackFlour), 0x1039),
        E(typeof(Dough), 0x103D),
        E(typeof(UnbakedFruitPie), 0x1042, 1041334),
        E(typeof(UnbakedPeachCobbler), 0x1042, 1041335),
        E(typeof(UnbakedApplePie), 0x1042, 1041336),
        E(typeof(UnbakedPumpkinPie), 0x1042, 1041342),
        E(typeof(Cookies), 0x160B),
        E(typeof(Cake), 0x9E9),
        E(typeof(Muffins), 0x9EB),
        // ThreeTieredCake: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(MisoSoup), 0x284D),
        E(typeof(WhiteMisoSoup), 0x284E),
        E(typeof(RedMisoSoup), 0x284F),
        E(typeof(AwaseMisoSoup), 0x2850),
        E(typeof(WasabiClumps), 0x24EB),
        E(typeof(SushiRolls), 0x283E),
        E(typeof(SushiPlatter), 0x2840),
        E(typeof(GreenTea), 0x284C),
        E(typeof(SweetDough), 0x103d, 1041340),
        E(typeof(CakeMix), 0x103F, 1041002),
        E(typeof(CookieMix), 0x103F),
        E(typeof(UnbakedQuiche), 0x1042, 1041339),
        E(typeof(UnbakedMeatPie), 0x1042, 1041338),
        E(typeof(UncookedSausagePizza), 0x1083, 1041337),
        E(typeof(UncookedCheesePizza), 0x1083, 1041341),
        E(typeof(Quiche), 0x1041, 1041345),
        E(typeof(MeatPie), 0x1041, 1041347),
        E(typeof(SausagePizza), 0x1040, 1044517),
        E(typeof(CheesePizza), 0x1040, 1044516),
        E(typeof(FruitPie), 0x1041, 1041346),
        E(typeof(PeachCobbler), 0x1041, 1041344),
        E(typeof(ApplePie), 0x1041, 1041343),
        E(typeof(PumpkinPie), 0x1041, 1041348),
        E(typeof(ChickenLeg), 0x1608),
    ];

    /// <summary>ServUO Cooking/smallsregular.cfg.</summary>
    public static readonly SmallBulkEntry[] CookingSmallsRegular =
    [
        // EnchantedApple: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(TribalPaint), 0x9EC, 1040000),
        // GrapesOfWrath: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(EggBomb), 0x2808),
        E(typeof(CookedBird), 0x9B7),
        E(typeof(FishSteak), 0x97B),
        E(typeof(FriedEggs), 0x9B6),
        E(typeof(LambLeg), 0x160A),
        E(typeof(Ribs), 0x9F2),
    ];

    /// <summary>ServUO Cooking/LargeBarbeque.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeBarbeque =
    [
        E(typeof(CookedBird), 0x9B7),
        E(typeof(FishSteak), 0x97B),
        E(typeof(FriedEggs), 0x9B6),
        E(typeof(LambLeg), 0x160A),
        E(typeof(Ribs), 0x9F2),
    ];

    /// <summary>ServUO Cooking/LargeDough.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeDough =
    [
        // SweetCocoaButter: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(SackFlour), 0x1039),
        E(typeof(Dough), 0x103D),
    ];

    /// <summary>ServUO Cooking/LargeFruits.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeFruits =
    [
        // EnchantedApple: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(TribalPaint), 0x9EC, 1040000),
        // WrathGrapes: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(EggBomb), 0x2808),
    ];

    /// <summary>ServUO Cooking/LargeMiso.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeMiso =
    [
        E(typeof(MisoSoup), 0x284D),
        E(typeof(WhiteMisoSoup), 0x284E),
        E(typeof(RedMisoSoup), 0x284F),
        E(typeof(AwaseMisoSoup), 0x2850),
    ];

    /// <summary>ServUO Cooking/LargeSushi.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeSushi =
    [
        E(typeof(WasabiClumps), 0x24EB),
        E(typeof(SushiRolls), 0x283E),
        E(typeof(SushiPlatter), 0x2840),
        E(typeof(GreenTea), 0x284C),
    ];

    /// <summary>ServUO Cooking/LargeSweets.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeSweets =
    [
        E(typeof(Cookies), 0x160B),
        E(typeof(Cake), 0x9E9),
        E(typeof(Muffins), 0x9EB),
        // ThreeTieredCake: not on the shard (dropped, as ServUO drops a type it cannot find)
    ];

    /// <summary>ServUO Cooking/LargeUnbakedPies.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeUnbakedPies =
    [
        E(typeof(UnbakedFruitPie), 0x1042, 1041334),
        E(typeof(UnbakedPeachCobbler), 0x1042, 1041335),
        E(typeof(UnbakedApplePie), 0x1042, 1041336),
        E(typeof(UnbakedPumpkinPie), 0x1042, 1041342),
    ];

    // -- Fletching -------------------------------------------------------------------------------------------

    /// <summary>ServUO Fletching/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] FletchingSmalls =
    [
        E(typeof(Bow), 0x13B2),
        E(typeof(CompositeBow), 0x26CC),
        E(typeof(Yumi), 0x27F0),
        E(typeof(Crossbow), 0xF4F),
        E(typeof(HeavyCrossbow), 0x13FD),
        E(typeof(RepeatingCrossbow), 0x26CD),
        E(typeof(MagicalShortbow), 0x2D1F),
        E(typeof(RangersShortbow), 0x2D1F, 1073509),
        E(typeof(LightweightShortbow), 0x2D1F, 1073510),
        E(typeof(MysticalShortbow), 0x2D1F, 1073511),
        E(typeof(AssassinsShortbow), 0x2D1F, 1073512),
        E(typeof(ElvenCompositeLongbow), 0x2D1E),
        E(typeof(BarbedLongbow), 0x2D1E, 1073505),
        E(typeof(SlayerLongbow), 0x2D1E, 1073506),
        E(typeof(FrozenLongbow), 0x2D1E, 1073507),
        E(typeof(LongbowOfMight), 0x2D1E, 1073508),
    ];

    /// <summary>ServUO Fletching/smallsregular.cfg.</summary>
    public static readonly SmallBulkEntry[] FletchingSmallsRegular =
    [
        E(typeof(Arrow), 0xF3E),
        E(typeof(Bolt), 0x1BFB),
        E(typeof(Kindling), 0xDE1),
        E(typeof(Shaft), 0x1BD4),
    ];

    /// <summary>ServUO Fletching/LargeAmmunition.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeAmmunition =
    [
        E(typeof(Arrow), 0xF3E),
        E(typeof(Bolt), 0x1BFB),
    ];

    /// <summary>ServUO Fletching/LargeElvenBows1.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeElvenBows1 =
    [
        E(typeof(MagicalShortbow), 0x2D1F),
        E(typeof(RangersShortbow), 0x2D1F, 1073509),
        E(typeof(LightweightShortbow), 0x2D1F, 1073510),
        E(typeof(MysticalShortbow), 0x2D1F, 1073511),
        E(typeof(AssassinsShortbow), 0x2D1F, 1073512),
    ];

    /// <summary>ServUO Fletching/LargeElvenBows2.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeElvenBows2 =
    [
        E(typeof(ElvenCompositeLongbow), 0x2D1E),
        E(typeof(BarbedLongbow), 0x2D1E, 1073505),
        E(typeof(SlayerLongbow), 0x2D1E, 1073506),
        E(typeof(FrozenLongbow), 0x2D1E, 1073507),
        E(typeof(LongbowOfMight), 0x2D1E, 1073508),
    ];

    /// <summary>ServUO Fletching/LargeHumanBows1.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeHumanBows1 =
    [
        E(typeof(Bow), 0x13B2),
        E(typeof(CompositeBow), 0x26CC),
        E(typeof(Yumi), 0x27F0),
    ];

    /// <summary>ServUO Fletching/LargeHumanBows2.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeHumanBows2 =
    [
        E(typeof(Crossbow), 0xF4F),
        E(typeof(HeavyCrossbow), 0x13FD),
        E(typeof(RepeatingCrossbow), 0x26CD),
    ];

    // -- Inscription -----------------------------------------------------------------------------------------

    /// <summary>ServUO Inscription/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] InscriptionSmalls =
    [
        E(typeof(ReactiveArmorScroll), 0x1F2D),
        E(typeof(ClumsyScroll), 0x1F2E),
        E(typeof(CreateFoodScroll), 0x1F2F),
        E(typeof(FeeblemindScroll), 0x1F30),
        E(typeof(HealScroll), 0x1F31),
        E(typeof(MagicArrowScroll), 0x1F32),
        E(typeof(NightSightScroll), 0x1F33),
        E(typeof(WeakenScroll), 0x1F34),
        // 2nd Circle
        E(typeof(AgilityScroll), 0x1F35),
        E(typeof(CunningScroll), 0x1F36),
        E(typeof(CureScroll), 0x1F37),
        E(typeof(HarmScroll), 0x1F38),
        E(typeof(MagicTrapScroll), 0x1F39),
        E(typeof(MagicUnTrapScroll), 0x1F3A), // ServUO MagicUntrapScroll
        E(typeof(ProtectionScroll), 0x1F3B),
        E(typeof(StrengthScroll), 0x1F3C),
        // 3rd Circle
        E(typeof(BlessScroll), 0x1F3D),
        E(typeof(FireballScroll), 0x1F3E),
        E(typeof(MagicLockScroll), 0x1F3F),
        E(typeof(PoisonScroll), 0x1F40),
        E(typeof(TelekinesisScroll), 0x1F41), // ServUO TelekinisisScroll
        E(typeof(TeleportScroll), 0x1F42),
        E(typeof(UnlockScroll), 0x1F43),
        E(typeof(WallOfStoneScroll), 0x1F44),
        // 4th Circle
        E(typeof(ArchCureScroll), 0x1F45),
        E(typeof(ArchProtectionScroll), 0x1F46),
        E(typeof(CurseScroll), 0x1F47),
        E(typeof(FireFieldScroll), 0x1F48),
        E(typeof(GreaterHealScroll), 0x1F49),
        E(typeof(LightningScroll), 0x1F4A),
        E(typeof(ManaDrainScroll), 0x1F4B),
        E(typeof(RecallScroll), 0x1F4C),
        // 5th Circle
        E(typeof(BladeSpiritsScroll), 0x1F4D),
        E(typeof(DispelFieldScroll), 0x1F4E),
        E(typeof(IncognitoScroll), 0x1F4F),
        E(typeof(MagicReflectScroll), 0x1F50),
        E(typeof(MindBlastScroll), 0x1F51),
        E(typeof(ParalyzeScroll), 0x1F52),
        E(typeof(PoisonFieldScroll), 0x1F53),
        E(typeof(SummonCreatureScroll), 0x1F54),
        // 6th Circle
        E(typeof(DispelScroll), 0x1F55),
        E(typeof(EnergyBoltScroll), 0x1F56),
        E(typeof(ExplosionScroll), 0x1F57),
        E(typeof(InvisibilityScroll), 0x1F58),
        E(typeof(MarkScroll), 0x1F59),
        E(typeof(MassCurseScroll), 0x1F5A),
        E(typeof(ParalyzeFieldScroll), 0x1F5B),
        E(typeof(RevealScroll), 0x1F5C),
        // 7th Circle
        E(typeof(ChainLightningScroll), 0x1F5D),
        E(typeof(EnergyFieldScroll), 0x1F5E),
        E(typeof(FlamestrikeScroll), 0x1F5F), // ServUO FlameStrikeScroll
        E(typeof(GateTravelScroll), 0x1F60),
        E(typeof(ManaVampireScroll), 0x1F61),
        E(typeof(MassDispelScroll), 0x1F62),
        E(typeof(MeteorSwarmScroll), 0x1F63),
        E(typeof(PolymorphScroll), 0x1F64),
        // 8th Circle
        E(typeof(EarthquakeScroll), 0x1F65),
        E(typeof(EnergyVortexScroll), 0x1F66),
        E(typeof(ResurrectionScroll), 0x1F67),
        E(typeof(SummonAirElementalScroll), 0x1F68),
        E(typeof(SummonDaemonScroll), 0x1F69),
        E(typeof(SummonEarthElementalScroll), 0x1F6A),
        E(typeof(SummonFireElementalScroll), 0x1F6B),
        E(typeof(SummonWaterElementalScroll), 0x1F6C),
        // Necromancy
        E(typeof(AnimateDeadScroll), 0x2260),
        E(typeof(BloodOathScroll), 0x2261),
        E(typeof(CorpseSkinScroll), 0x2262),
        E(typeof(CurseWeaponScroll), 0x2263),
        E(typeof(EvilOmenScroll), 0x2264),
        E(typeof(HorrificBeastScroll), 0x2265),
        E(typeof(LichFormScroll), 0x2266),
        E(typeof(MindRotScroll), 0x2267),
        E(typeof(PainSpikeScroll), 0x2268),
        E(typeof(PoisonStrikeScroll), 0x2269),
        E(typeof(StrangleScroll), 0x226A),
        E(typeof(SummonFamiliarScroll), 0x226B),
        E(typeof(VampiricEmbraceScroll), 0x226C),
        E(typeof(VengefulSpiritScroll), 0x226D),
        E(typeof(WitherScroll), 0x226E),
        E(typeof(WraithFormScroll), 0x226F),
        // Books
        E(typeof(Spellbook), 0xEFA),
        E(typeof(NecromancerSpellbook), 0x2253),
        E(typeof(Runebook), 0x22C5),
        // RunicAtlas: not on the shard (dropped, as ServUO drops a type it cannot find)
    ];

    /// <summary>ServUO Inscription/LargeBooks.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeBooks =
    [
        E(typeof(Spellbook), 0xEFA),
        E(typeof(NecromancerSpellbook), 0x2253),
        E(typeof(Runebook), 0x22C5),
        // RunicAtlas: not on the shard (dropped, as ServUO drops a type it cannot find)
    ];

    /// <summary>ServUO Inscription/LargeCircle1.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle1 =
    [
        E(typeof(ClumsyScroll), 0x1F2E),
        E(typeof(FeeblemindScroll), 0x1F30),
        E(typeof(WeakenScroll), 0x1F34),
    ];

    /// <summary>ServUO Inscription/LargeCircle1and2.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle1and2 =
    [
        E(typeof(HealScroll), 0x1F31),
        E(typeof(AgilityScroll), 0x1F35),
        E(typeof(CunningScroll), 0x1F36),
        E(typeof(CureScroll), 0x1F37),
        E(typeof(StrengthScroll), 0x1F3C),
    ];

    /// <summary>ServUO Inscription/LargeCircle4.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle4 =
    [
        E(typeof(CurseScroll), 0x1F47),
        E(typeof(GreaterHealScroll), 0x1F49),
        E(typeof(RecallScroll), 0x1F4C),
    ];

    /// <summary>ServUO Inscription/LargeCircle5.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle5 =
    [
        E(typeof(BladeSpiritsScroll), 0x1F4D),
        E(typeof(DispelFieldScroll), 0x1F4E),
        E(typeof(MagicReflectScroll), 0x1F50),
        E(typeof(ParalyzeScroll), 0x1F52),
        E(typeof(SummonCreatureScroll), 0x1F54),
    ];

    /// <summary>ServUO Inscription/LargeCircle7.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle7 =
    [
        E(typeof(ChainLightningScroll), 0x1F5D),
        E(typeof(FlamestrikeScroll), 0x1F5F), // ServUO FlameStrikeScroll
        E(typeof(ManaVampireScroll), 0x1F61),
        E(typeof(MeteorSwarmScroll), 0x1F63),
        E(typeof(PolymorphScroll), 0x1F64),
    ];

    /// <summary>ServUO Inscription/LargeCircle8.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeCircle8 =
    [
        E(typeof(SummonAirElementalScroll), 0x1F68),
        E(typeof(SummonDaemonScroll), 0x1F69),
        E(typeof(SummonEarthElementalScroll), 0x1F6A),
        E(typeof(SummonFireElementalScroll), 0x1F6B),
        E(typeof(SummonWaterElementalScroll), 0x1F6C),
    ];

    /// <summary>ServUO Inscription/LargeNecromancy1.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeNecromancy1 =
    [
        E(typeof(BloodOathScroll), 0x2261),
        E(typeof(CorpseSkinScroll), 0x2262),
        E(typeof(CurseWeaponScroll), 0x2263),
        E(typeof(EvilOmenScroll), 0x2264),
        E(typeof(PainSpikeScroll), 0x2268),
    ];

    /// <summary>ServUO Inscription/LargeNecromancy2.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeNecromancy2 =
    [
        E(typeof(AnimateDeadScroll), 0x2260),
        E(typeof(HorrificBeastScroll), 0x2265),
        E(typeof(MindRotScroll), 0x2267),
        E(typeof(SummonFamiliarScroll), 0x226B),
    ];

    /// <summary>ServUO Inscription/LargeNecromancy3.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeNecromancy3 =
    [
        E(typeof(PoisonStrikeScroll), 0x2269),
        E(typeof(StrangleScroll), 0x226A),
        E(typeof(WitherScroll), 0x226E),
    ];

    // -- Tinkering -------------------------------------------------------------------------------------------

    /// <summary>ServUO Tinkering/smalls.cfg.</summary>
    public static readonly SmallBulkEntry[] TinkeringSmalls =
    [
        E(typeof(GoldRing), 0x108A, 1011207),
        E(typeof(GoldBracelet), 0x1086, 1011219),
        E(typeof(GoldEarrings), 0x1087, 1011208),
        E(typeof(KeyRing), 0x1011),
        E(typeof(Key), 0x1010),
        E(typeof(Globe), 0x1047),
        E(typeof(Spyglass), 0x14F5),
        E(typeof(BarrelTap), 0x1004),
        E(typeof(BarrelHoops), 0x10E1),
        E(typeof(MortarPestle), 0xE9B),
        E(typeof(SmithHammer), 0x13E3), // ServUO SmithyHammer
        E(typeof(Skillet), 0x97F, 1044567),
        E(typeof(SewingKit), 0xF9D),
        E(typeof(FletcherTools), 0x1022),
        E(typeof(ButcherKnife), 0x13F6),
        E(typeof(SpoonLeft), 0x9F8),
        E(typeof(ForkLeft), 0x9F4),
        E(typeof(Plate), 0x9D7),
        E(typeof(KnifeLeft), 0x9F6),
        E(typeof(Goblet), 0x99A),
        E(typeof(PewterMug), 0xFFF),
        // Clippers: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(Tongs), 0xFBB),
        E(typeof(SledgeHammer), 0xFB5),
        E(typeof(Saw), 0x1034),
        E(typeof(Froe), 0x10E5),
        E(typeof(FlourSifter), 0x103E),
        // JointingPlane,0x1030
        // MouldingPlane,0x102C
        // SmoothingPlane,0x1032
        E(typeof(Nunchaku), 0x27AE),
        E(typeof(Hatchet), 0xF43),
        E(typeof(Pickaxe), 0xE86),
        E(typeof(Cleaver), 0xEC3),
        E(typeof(Pitchfork), 0xE87),
        E(typeof(Lantern), 0xA25),
        E(typeof(Candelabra), 0xA27),
        E(typeof(Scales), 0x1852),
    ];

    /// <summary>ServUO Tinkering/smallsregular.cfg.</summary>
    public static readonly SmallBulkEntry[] TinkeringSmallsRegular =
    [
        E(typeof(Gears), 0x1053),
        E(typeof(Axle), 0x105B),
        E(typeof(Springs), 0x105D),
        E(typeof(AxleGears), 0x1051),
        E(typeof(ClockParts), 0x104F),
        E(typeof(Clock), 0x104B),
        E(typeof(PotionKeg), 0x1940),
        E(typeof(ClockFrame), 0x104D),
        // MetalContainerEngraver: not on the shard (dropped, as ServUO drops a type it cannot find)
        E(typeof(Hinge), 0x1055, 1011202),
        E(typeof(SextantParts), 0x1059),
    ];

    /// <summary>ServUO Tinkering/LargeDining.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeDining =
    [
        E(typeof(ButcherKnife), 0x13F6),
        E(typeof(Spoon), 0x9F8),
        E(typeof(Fork), 0x9F4),
        E(typeof(Plate), 0x9D7),
        E(typeof(Knife), 0x9F6),
        E(typeof(Goblet), 0x99A),
    ];

    /// <summary>ServUO Tinkering/LargeJewelry.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeJewelry =
    [
        E(typeof(GoldRing), 0x108A, 1011207),
        E(typeof(GoldBracelet), 0x1086, 1011219),
        E(typeof(GoldEarrings), 0x1087, 1011208),
    ];

    /// <summary>ServUO Tinkering/LargeKeyGlobe.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeKeyGlobe =
    [
        E(typeof(KeyRing), 0x1011),
        E(typeof(Key), 0x1010),
        E(typeof(Globe), 0x1047),
        E(typeof(Spyglass), 0x14F5),
    ];

    /// <summary>ServUO Tinkering/LargeTools.cfg.</summary>
    public static readonly SmallBulkEntry[] LargeTools =
    [
        E(typeof(MortarPestle), 0xE9B),
        E(typeof(SmithHammer), 0x13E3), // ServUO SmithyHammer
        E(typeof(Skillet), 0x97F),
        E(typeof(SewingKit), 0xF9D),
        E(typeof(FletcherTools), 0x1022),
    ];
}
