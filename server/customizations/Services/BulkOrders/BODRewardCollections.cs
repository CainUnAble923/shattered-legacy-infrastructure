// cc-P122 (smith, tailor) and cc-P123 (the six other crafts): the 2016 reward collections, ported from ServUO
// Scripts/Services/BulkOrders/Rewards/Rewards.cs (SmithRewardCalculator :315-361, TailorRewardCalculator :667-698 and their
// constructors :393-484, :722-850; Tinkering :1017-1046, Carpentry :1200-1231, Inscription :1393-1411, Cooking :1517-1534,
// Fletching :1651-1672, Alchemy :1813-1835 and their constructors). In the 2016 system a turn-in's points buy one item from this list in the rewards
// gump; points left over can be banked (BulkOrderSystem).
//
// Every entry is ServUO's, in its order, with its cliloc, hue and price. Three kinds of reward are not on this shard yet and
// stay listed as named placeholders, never dropped (the prompt's rule): the gump shows them with "not on the shard yet" and
// will not sell them, so no player spends points on nothing. Listed in the notes:
//   BODRewardTitleDeed  (a reward title)      no reward-title system in pinned or ours
//   SmeltersTalisman    (smelting bonus)      no smelting-bonus talisman in pinned or ours
//   HarvestMap          (to a rich vein)      no harvest maps in pinned or ours
// cc-P123's six crafts add, on the same rule (listed in notes/cc-P123):
//   Recipe              (a craft recipe)      recipe rewards are F-43, not this port
//   WoodsmansTalisman, RunicMalletAndChisel, CauldronOfTransmutationDeed, AutomatonActuator, BlackrockMoonstone,
//   BlackrockAutomatonHead: not in pinned or ours, and not among ServUO's bulk-order items (Items/), so not ported here
//   FermentationBarrel  (wine making)         a bulk-order item not ported yet (see the notes)
// The Rock Hammer, the Master Craftsman Talisman and (cc-P123) the Master Chef's Apron, the Powder of Fortification keg,
// the Plum Tree, the special natural dyes and the Guaranteed Spellbook Improvement Talisman are ported beside this file
// (Items/).

using System;
using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.BulkOrders;

public delegate Item BODRewardConstructor(int type);

public class BODCollectionItem
{
    public BODCollectionItem(
        int itemID, int tooltip, int hue, int points, BODRewardConstructor constructor, int type = 0,
        string placeholder = null
    )
    {
        ItemID = itemID;
        Tooltip = tooltip;
        Hue = hue;
        Points = points;
        Constructor = constructor;
        RewardType = type;
        Placeholder = placeholder;
    }

    public int ItemID { get; }
    public int Tooltip { get; }
    public int Hue { get; }
    public int Points { get; }
    public BODRewardConstructor Constructor { get; }
    public int RewardType { get; }

    /// <summary>The ServUO reward this stands for, when it is not on the shard yet; null for a real reward.</summary>
    public string Placeholder { get; }

    public bool IsPlaceholder => Placeholder != null;

    public Item Construct() => IsPlaceholder ? null : Constructor?.Invoke(RewardType);
}

public static class BODRewardCollections
{
    private static List<BODCollectionItem> _smith;
    private static List<BODCollectionItem> _tailor;
    private static List<BODCollectionItem> _tinkering;
    private static List<BODCollectionItem> _carpentry;
    private static List<BODCollectionItem> _inscription;
    private static List<BODCollectionItem> _cooking;
    private static List<BODCollectionItem> _fletching;
    private static List<BODCollectionItem> _alchemy;

    public static IReadOnlyList<BODCollectionItem> Get(BODType type) =>
        type switch
        {
            BODType.Tailor      => _tailor ??= BuildTailor(),
            BODType.Smith       => _smith ??= BuildSmith(),
            BODType.Tinkering   => _tinkering ??= BuildTinkering(),
            BODType.Carpentry   => _carpentry ??= BuildCarpentry(),
            BODType.Inscription => _inscription ??= BuildInscription(),
            BODType.Cooking     => _cooking ??= BuildCooking(),
            BODType.Fletching   => _fletching ??= BuildFletching(),
            BODType.Alchemy     => _alchemy ??= BuildAlchemy(),
            _                   => Array.Empty<BODCollectionItem>()
        };

    private static int Hue(CraftResource r) => CraftResources.GetHue(r);

    private static BODCollectionItem Placeholder(int itemID, int tooltip, int hue, int points, string what) =>
        new(itemID, tooltip, hue, points, null, 0, what);

    // ServUO Rewards.cs:321-360.
    private static List<BODCollectionItem> BuildSmith() =>
    [
        new(0x13E3, 1157219, 0, 10, SmithHammer),
        new(0xF39, 1157084, 0x973, 10, _ => new SturdyShovel()),
        new(0xE86, 1157085, 0x973, 25, _ => new SturdyPickaxe()),
        Placeholder(0x14F0, 1157181, 0, 25, "BODRewardTitleDeed (reward title 0)"),
        Placeholder(0x14F0, 1157182, 0, 100, "BODRewardTitleDeed (reward title 1)"),
        new(0x13C6, 1157086, 0, 100, MiningGloves, 1),
        new(0x13D5, 1157087, 0, 200, MiningGloves, 3),
        new(0xFB4, 1157090, 0, 200, _ => new ProspectorsTool()),
        new(0xE86, 1157089, 0, 200, _ => new GargoylesPickaxe()),
        Placeholder(0x2F5B, 1152674, Hue(CraftResource.Gold), 350, "SmeltersTalisman (Gold)"),
        new(0x9E2A, 1157264, 0, 400, t => new MasterCraftsmanTalisman(t, 0x9E2A, SkillName.Blacksmith), 10),
        new(0x13EB, 1157088, 0, 450, MiningGloves, 5),
        new(4102, 1157091, 0, 450, _ => new PowderOfTemperament()),
        Placeholder(0x2F5B, 1152675, Hue(CraftResource.Agapite), 475, "SmeltersTalisman (Agapite)"),
        new(0x9E7E, 1157216, 0, 500, _ => new RockHammer()),
        new(0x13E3, 1157092, Hue(CraftResource.DullCopper), 500, RunicHammer, 1),
        Placeholder(0x2F5B, 1152676, Hue(CraftResource.Verite), 525, "SmeltersTalisman (Verite)"),
        new(0x13E3, 1157093, Hue(CraftResource.ShadowIron), 550, RunicHammer, 2),
        new(0x9E2A, 1157218, 0, 550, t => new MasterCraftsmanTalisman(t, 0x9E2A, SkillName.Blacksmith), 25),
        Placeholder(0x2F5B, 1152677, Hue(CraftResource.Valorite), 575, "SmeltersTalisman (Valorite)"),
        Placeholder(0x14EC, 1152665, Hue(CraftResource.Gold), 600, "HarvestMap (Gold)"),
        new(0xFAF, 1157100, 0, 625, _ => new ColoredAnvil()),
        new(0x14F0, 1157105, 0x481, 625, SmithPowerScroll, 5),
        new(0x13E3, 1157094, Hue(CraftResource.Copper), 650, RunicHammer, 3),
        new(0x14F0, 1157106, 0x481, 675, SmithPowerScroll, 10),
        new(0x13E3, 1157095, Hue(CraftResource.Bronze), 700, RunicHammer, 4),
        new(0x13E3, 1157101, 0x482, 750, AncientHammer, 10),
        new(0x14F0, 1157107, 0x481, 800, SmithPowerScroll, 15),
        new(0x13E3, 1157102, 0x482, 850, AncientHammer, 15),
        Placeholder(0x14EC, 1152666, Hue(CraftResource.Agapite), 850, "HarvestMap (Agapite)"),
        new(0x14F0, 1157108, 0x481, 900, SmithPowerScroll, 20),
        new(0x9E2A, 1157265, 0, 900, t => new MasterCraftsmanTalisman(t, 0x9E2A, SkillName.Blacksmith), 50),
        new(0x13E3, 1157096, Hue(CraftResource.Gold), 950, RunicHammer, 5),
        Placeholder(0x14EC, 1152667, Hue(CraftResource.Verite), 950, "HarvestMap (Verite)"),
        new(0x13E3, 1157103, 0x482, 1000, AncientHammer, 30),
        new(0x13E3, 1157097, Hue(CraftResource.Agapite), 1050, RunicHammer, 6),
        Placeholder(0x14EC, 1152668, Hue(CraftResource.Valorite), 1050, "HarvestMap (Valorite)"),
        new(0x13E3, 1157104, 0x482, 1100, AncientHammer, 60),
        new(0x13E3, 1157098, Hue(CraftResource.Verite), 1150, RunicHammer, 7),
        new(0x13E3, 1157099, Hue(CraftResource.Valorite), 1200, RunicHammer, 8)
    ];

    // ServUO Rewards.cs:673-697.
    private static List<BODCollectionItem> BuildTailor() =>
    [
        new(0xF9D, 1157219, 0, 10, SewingKit),
        Placeholder(0x14F0, 1157183, 0, 10, "BODRewardTitleDeed (reward title 2)"),
        new(0x1765, 1157109, 0, 10, Cloth, 0),
        Placeholder(0x14F0, 1157184, 0, 25, "BODRewardTitleDeed (reward title 3)"),
        new(0x1761, 1157109, 0, 50, Cloth, 1),
        Placeholder(0x14F0, 1157185, 0, 100, "BODRewardTitleDeed (reward title 4)"),
        new(0x1765, 1157109, 0, 100, Cloth, 2),
        new(0x1765, 1157109, 0, 150, Cloth, 3),
        new(0x170D, 1157110, 0, 150, Sandals, 3),
        new(0x1765, 1157109, 0, 200, Cloth, 4),
        new(0x9E25, 1157264, 0, 200, t => new MasterCraftsmanTalisman(t, 0x9E25, SkillName.Tailoring), 10),
        new(0x14F0, 1157111, 0, 300, StretchedHide),
        new(0x1765, 1157109, 0, 300, Cloth, 5),
        new(0x9E25, 1157218, 0, 300, t => new MasterCraftsmanTalisman(t, 0x9E25, SkillName.Tailoring), 25),
        new(0xF9D, 1157115, Hue(CraftResource.SpinedLeather), 350, RunicKit, 1),
        new(0x9E25, 1157265, 0, 350, t => new MasterCraftsmanTalisman(t, 0x9E25, SkillName.Tailoring), 50),
        new(0x14F0, 1157118, 0x481, 400, TailorPowerScroll, 5),
        new(0x14F0, 1157112, 0, 400, Tapestry),
        new(0x14F0, 1157113, 0, 450, BearRug),
        new(0x14F0, 1157119, 0x481, 500, TailorPowerScroll, 10),
        new(0x14F0, 1157114, 0, 550, _ => new ClothingBlessDeed()),
        new(0x14F0, 1157120, 0x481, 575, TailorPowerScroll, 15),
        new(0xF9D, 1157116, Hue(CraftResource.HornedLeather), 600, RunicKit, 2),
        new(0x14F0, 1157121, 0x481, 650, TailorPowerScroll, 20),
        new(0xF9D, 1157117, Hue(CraftResource.BarbedLeather), 700, RunicKit, 3)
    ];

    // Rewards.cs:1017-1046.
    private static List<BODCollectionItem> BuildTinkering() =>
    [
        new(0x1EBC, 1157219, 0, 10, _ => new TinkerTools { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157186, 0, 25, "BODRewardTitleDeed (reward title 5)"),
        Placeholder(0x14F0, 1157187, 0, 50, "BODRewardTitleDeed (reward title 6)"),
        Placeholder(0x14F0, 1157190, 0, 210, "BODRewardTitleDeed (reward title 9)"),
        Placeholder(0x2831, 1157288, 0, 225, "Recipe 0 (F-43)"),
        Placeholder(0x14F0, 1157188, 0, 250, "BODRewardTitleDeed (reward title 7)"),
        Placeholder(0x2831, 1157287, 0, 310, "Recipe 1 (F-43)"),
        Placeholder(0x14F0, 1157189, 0, 225, "BODRewardTitleDeed (reward title 8)"),
        Placeholder(0x2831, 1157289, 0, 350, "Recipe 2 (F-43)"),
        new(0x9E2B, 1157264, 0, 400, t => new MasterCraftsmanTalisman(t, 0x9E2B, SkillName.Tinkering), 10),
        Placeholder(0x2F5B, 1152674, Hue(CraftResource.Gold), 450, "SmeltersTalisman (Gold)"),
        Placeholder(0x14EC, 1152665, Hue(CraftResource.Gold), 500, "HarvestMap (Gold)"),
        new(0x9E2B, 1157218, 0, 550, t => new MasterCraftsmanTalisman(t, 0x9E2B, SkillName.Tinkering), 25),
        Placeholder(0x2F5B, 1152675, Hue(CraftResource.Agapite), 600, "SmeltersTalisman (Agapite)"),
        Placeholder(0x14EC, 1152666, Hue(CraftResource.Agapite), 650, "HarvestMap (Agapite)"),
        new(0x1940, 1157221, 0, 700, _ => new PowderOfFortKeg()),
        Placeholder(0x9CE9, 1157290, 0, 750, "AutomatonActuator"),
        Placeholder(0x2F5B, 1152676, Hue(CraftResource.Verite), 800, "SmeltersTalisman (Verite)"),
        Placeholder(0x14EC, 1152667, Hue(CraftResource.Verite), 850, "HarvestMap (Verite)"),
        new(0x9E2B, 1157265, 0, 900, t => new MasterCraftsmanTalisman(t, 0x9E2B, SkillName.Tinkering), 50),
        new(0x9E7E, 1157216, 0, 950, _ => new RockHammer()),
        Placeholder(0x9CAA, 1157286, 1175, 1000, "BlackrockMoonstone"),
        Placeholder(0x2F5B, 1152677, Hue(CraftResource.Valorite), 1050, "SmeltersTalisman (Valorite)"),
        Placeholder(0x14EC, 1152668, Hue(CraftResource.Valorite), 1100, "HarvestMap (Valorite)"),
        Placeholder(0x9DB1, 1157220, 1175, 1200, "BlackrockAutomatonHead")
    ];

    // Rewards.cs:1200-1231.
    private static List<BODCollectionItem> BuildCarpentry() =>
    [
        new(0x1028, 1157219, 0, 10, _ => new DovetailSaw { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157191, 0, 25, "BODRewardTitleDeed (reward title 10)"),
        Placeholder(0x14F0, 1157192, 0, 50, "BODRewardTitleDeed (reward title 11)"),
        Placeholder(0x14F0, 1157193, 0, 250, "BODRewardTitleDeed (reward title 12)"),
        new(0x9E2C, 1157264, 0, 300, t => new MasterCraftsmanTalisman(t, 0x9E2C, SkillName.Carpentry), 10),
        Placeholder(0x2F5A, 1152678, Hue(CraftResource.YewWood), 350, "WoodsmansTalisman (Yew)"),
        new(0x9E2C, 1157218, 0, 450, t => new MasterCraftsmanTalisman(t, 0x9E2C, SkillName.Carpentry), 25),
        Placeholder(0x12B3, 1157293, Hue(CraftResource.DullCopper), 450, "RunicMalletAndChisel (Dull Copper)"),
        Placeholder(0x12B3, 1157294, Hue(CraftResource.ShadowIron), 450, "RunicMalletAndChisel (Shadow Iron)"),
        Placeholder(0x14EC, 1152669, Hue(CraftResource.YewWood), 500, "HarvestMap (Yew)"),
        new(0x1029, 1157223, Hue(CraftResource.OakWood), 550, RunicDovetailSaw, 0),
        Placeholder(0x12B3, 1157295, Hue(CraftResource.Copper), 600, "RunicMalletAndChisel (Copper)"),
        Placeholder(0x12B3, 1157296, Hue(CraftResource.Bronze), 650, "RunicMalletAndChisel (Bronze)"),
        Placeholder(0x2F5A, 1152679, Hue(CraftResource.Heartwood), 650, "WoodsmansTalisman (Heartwood)"),
        Placeholder(0x14EC, 1152670, Hue(CraftResource.Heartwood), 700, "HarvestMap (Heartwood)"),
        new(0x1029, 1157224, Hue(CraftResource.AshWood), 750, RunicDovetailSaw, 1),
        new(0x9E2C, 1157265, 0, 800, t => new MasterCraftsmanTalisman(t, 0x9E2C, SkillName.Carpentry), 50),
        Placeholder(0x2F5A, 1152680, Hue(CraftResource.Bloodwood), 850, "WoodsmansTalisman (Bloodwood)"),
        Placeholder(0x14EC, 1152671, Hue(CraftResource.Bloodwood), 900, "HarvestMap (Bloodwood)"),
        Placeholder(0x12B3, 1157297, Hue(CraftResource.Gold), 900, "RunicMalletAndChisel (Gold)"),
        new(0x1029, 1157225, Hue(CraftResource.YewWood), 950, RunicDovetailSaw, 2),
        Placeholder(0x2F5A, 1152681, Hue(CraftResource.Frostwood), 1000, "WoodsmansTalisman (Frostwood)"),
        Placeholder(0x12B3, 1157298, Hue(CraftResource.Agapite), 1000, "RunicMalletAndChisel (Agapite)"),
        Placeholder(0x14EC, 1152672, Hue(CraftResource.Frostwood), 1050, "HarvestMap (Frostwood)"),
        Placeholder(0x12B3, 1157299, Hue(CraftResource.Verite), 1100, "RunicMalletAndChisel (Verite)"),
        new(0x1029, 1157226, Hue(CraftResource.Heartwood), 1150, RunicDovetailSaw, 3),
        Placeholder(0x12B3, 1157300, Hue(CraftResource.Valorite), 1150, "RunicMalletAndChisel (Valorite)")
    ];

    // Rewards.cs:1393-1411.
    private static List<BODCollectionItem> BuildInscription() =>
    [
        new(0x0FBF, 1157219, 0, 10, _ => new ScribesPen { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157194, 0, 25, "BODRewardTitleDeed (reward title 13)"),
        Placeholder(0x14F0, 1157195, 0, 50, "BODRewardTitleDeed (reward title 14)"),
        Placeholder(0x14F0, 1157196, 0, 210, "BODRewardTitleDeed (reward title 15)"),
        Placeholder(0x2831, 1156443, 0, 210, "Recipe 3 (F-43)"),
        new(0x182B, 1157205, 2741, 250, NaturalDye, 3),
        new(0x9E28, 1157264, 0, 275, t => new MasterCraftsmanTalisman(t, 0x9E28, SkillName.Inscribe), 10),
        new(0x182B, 1157205, 2740, 310, NaturalDye, 4),
        new(0x9E28, 1157218, 0, 350, t => new MasterCraftsmanTalisman(t, 0x9E28, SkillName.Inscribe), 25),
        new(0x182B, 1157205, 2732, 375, NaturalDye, 5),
        new(0x9E28, 1157265, 0, 410, t => new MasterCraftsmanTalisman(t, 0x9E28, SkillName.Inscribe), 50),
        new(0x182B, 1157205, 2731, 450, NaturalDye, 6),
        new(0x182B, 1157205, 2735, 475, NaturalDye, 7),
        new(0x9E28, 1157291, 0, 500, t => new GuaranteedSpellbookImprovementTalisman(t), 10)
    ];

    // Rewards.cs:1517-1534.
    private static List<BODCollectionItem> BuildCooking() =>
    [
        new(0x97F, 1157219, 0, 10, _ => new Skillet { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157197, 0, 25, "BODRewardTitleDeed (reward title 13)"),
        Placeholder(0x2831, 1031233, 0, 25, "Recipe 4 (F-43)"),
        Placeholder(0x14F0, 1157198, 0, 50, "BODRewardTitleDeed (reward title 14)"),
        Placeholder(0x14F0, 1157199, 0, 210, "BODRewardTitleDeed (reward title 15)"),
        new(0x9E27, 1157264, 0, 250, t => new MasterCraftsmanTalisman(t, 0x9E27, SkillName.Cooking), 10),
        new(0x9E27, 1157218, 0, 300, t => new MasterCraftsmanTalisman(t, 0x9E27, SkillName.Cooking), 25),
        new(0x9E27, 1157265, 0, 350, t => new MasterCraftsmanTalisman(t, 0x9E27, SkillName.Cooking), 50),
        new(0x153D, 1157227, 1990, 410, _ => new MasterChefsApron()),
        new(0x14F0, 1076605, 0, 475, _ => new PlumTreeAddonDeed()),
        new(0x182B, 1157278, 2740, 525, NaturalDye, 8),
        new(0x182B, 1157278, 2732, 625, NaturalDye, 9),
        Placeholder(0x9E36, 1157229, 0, 625, "FermentationBarrel")
    ];

    // Rewards.cs:1651-1672.
    private static List<BODCollectionItem> BuildFletching() =>
    [
        new(0x1022, 1157219, 0, 10, _ => new FletcherTools { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157200, 0, 25, "BODRewardTitleDeed (reward title 17)"),
        new(0x9E29, 1157264, 0, 210, t => new MasterCraftsmanTalisman(t, 0x9E29, SkillName.Fletching), 10),
        Placeholder(0x2F5A, 1152678, Hue(CraftResource.YewWood), 225, "WoodsmansTalisman (Yew)"),
        Placeholder(0x14EC, 1152669, Hue(CraftResource.YewWood), 310, "HarvestMap (Yew)"),
        new(0x9E29, 1157218, 0, 325, t => new MasterCraftsmanTalisman(t, 0x9E29, SkillName.Fletching), 25),
        Placeholder(0x2F5A, 1152679, Hue(CraftResource.Heartwood), 360, "WoodsmansTalisman (Heartwood)"),
        Placeholder(0x14EC, 1152670, Hue(CraftResource.Heartwood), 375, "HarvestMap (Heartwood)"),
        new(0x9E29, 1157265, 0, 410, t => new MasterCraftsmanTalisman(t, 0x9E29, SkillName.Fletching), 50),
        new(0x1022, 1157223, Hue(CraftResource.OakWood), 425, RunicFletcherTools, 0),
        Placeholder(0x2F5A, 1152680, Hue(CraftResource.Bloodwood), 510, "WoodsmansTalisman (Bloodwood)"),
        Placeholder(0x14EC, 1152671, Hue(CraftResource.Bloodwood), 525, "HarvestMap (Bloodwood)"),
        new(0x1022, 1157224, Hue(CraftResource.AshWood), 650, RunicFletcherTools, 1),
        Placeholder(0x2F5A, 1152681, Hue(CraftResource.Frostwood), 750, "WoodsmansTalisman (Frostwood)"),
        Placeholder(0x14EC, 1152672, Hue(CraftResource.Frostwood), 950, "HarvestMap (Frostwood)"),
        new(0x1022, 1157225, Hue(CraftResource.YewWood), 1000, RunicFletcherTools, 2),
        new(0x1022, 1157226, Hue(CraftResource.Heartwood), 1100, RunicFletcherTools, 3)
    ];

    // Rewards.cs:1813-1835.
    private static List<BODCollectionItem> BuildAlchemy() =>
    [
        new(0xE9B, 1157219, 0, 10, _ => new MortarPestle { UsesRemaining = 250 }),
        Placeholder(0x14F0, 1157183, 0, 25, "BODRewardTitleDeed (reward title 20)"),
        Placeholder(0x14F0, 1157202, 0, 50, "BODRewardTitleDeed (reward title 21)"),
        Placeholder(0x14F0, 1157203, 0, 210, "BODRewardTitleDeed (reward title 22)"),
        new(0x182B, 1157278, 2741, 225, NaturalDye, 0),
        Placeholder(0x975, 1152660, Hue(CraftResource.AshWood), 250, "CauldronOfTransmutationDeed (Ash)"),
        Placeholder(0x975, 1152656, Hue(CraftResource.Bronze), 260, "CauldronOfTransmutationDeed (Bronze)"),
        new(0x9E26, 1157264, 0, 275, t => new MasterCraftsmanTalisman(t, 0x9E26, SkillName.Alchemy), 10),
        Placeholder(0x975, 1152661, Hue(CraftResource.YewWood), 300, "CauldronOfTransmutationDeed (Yew)"),
        Placeholder(0x975, 1152657, Hue(CraftResource.Gold), 310, "CauldronOfTransmutationDeed (Gold)"),
        new(0x9E26, 1157218, 0, 325, t => new MasterCraftsmanTalisman(t, 0x9E26, SkillName.Alchemy), 25),
        Placeholder(0x975, 1152658, Hue(CraftResource.Agapite), 350, "CauldronOfTransmutationDeed (Agapite)"),
        Placeholder(0x975, 1152662, Hue(CraftResource.Heartwood), 360, "CauldronOfTransmutationDeed (Heartwood)"),
        new(0x9E26, 1157265, 0, 375, t => new MasterCraftsmanTalisman(t, 0x9E26, SkillName.Alchemy), 50),
        new(0x182B, 1157278, 2731, 400, NaturalDye, 1),
        Placeholder(0x975, 1152663, Hue(CraftResource.Bloodwood), 410, "CauldronOfTransmutationDeed (Bloodwood)"),
        new(0x182B, 1157278, 2735, 425, NaturalDye, 2),
        Placeholder(0x975, 1152659, Hue(CraftResource.Verite), 450, "CauldronOfTransmutationDeed (Verite)")
    ];

    // -- The six crafts' constructors (cc-P123) ----------------------------------------------------------------------

    // Rewards.cs:1252-1262.
    private static Item RunicDovetailSaw(int type) =>
        type switch
        {
            1 => new RunicDovetailSaw(CraftResource.AshWood, 35),
            2 => new RunicDovetailSaw(CraftResource.YewWood, 25),
            3 => new RunicDovetailSaw(CraftResource.Heartwood, 15),
            _ => new RunicDovetailSaw(CraftResource.OakWood, 45)
        };

    // Rewards.cs:1689-1699.
    private static Item RunicFletcherTools(int type) =>
        type switch
        {
            1 => new RunicFletcherTool(CraftResource.AshWood, 35),
            2 => new RunicFletcherTool(CraftResource.YewWood, 25),
            3 => new RunicFletcherTool(CraftResource.Heartwood, 15),
            _ => new RunicFletcherTool(CraftResource.OakWood, 45)
        };

    // Rewards.cs:255-271 (RewardCalculator.NaturalDye).
    private static Item NaturalDye(int type) =>
        type switch
        {
            0 => new SpecialNaturalDye(DyeType.WindAzul),
            1 => new SpecialNaturalDye(DyeType.DullRuby),
            2 => new SpecialNaturalDye(DyeType.PoppieWhite),
            3 => new SpecialNaturalDye(DyeType.WindAzul, true),
            4 => new SpecialNaturalDye(DyeType.UmbranViolet, true),
            5 => new SpecialNaturalDye(DyeType.ZentoOrchid, true),
            6 => new SpecialNaturalDye(DyeType.DullRuby, true),
            7 => new SpecialNaturalDye(DyeType.PoppieWhite, true),
            8 => new SpecialNaturalDye(DyeType.UmbranViolet),
            9 => new SpecialNaturalDye(DyeType.ZentoOrchid),
            _ => throw new InvalidOperationException()
        };

    // -- Smith constructors (ServUO :406-483) ------------------------------------------------------------------------

    private static Item SmithHammer(int type) => new SmithHammer { UsesRemaining = 250 };

    private static Item MiningGloves(int type) =>
        type switch
        {
            1 => new LeatherGlovesOfMining(1),
            3 => new StuddedGlovesOfMining(3),
            5 => new RingmailGlovesOfMining(5),
            _ => throw new InvalidOperationException()
        };

    private static Item RunicHammer(int type) =>
        type is >= 1 and <= 8
            ? new RunicHammer(CraftResource.Iron + type, Core.AOS ? 55 - type * 5 : 50)
            : throw new InvalidOperationException();

    private static Item SmithPowerScroll(int type) =>
        type is 5 or 10 or 15 or 20 ? new PowerScroll(SkillName.Blacksmith, 100 + type) : throw new InvalidOperationException();

    private static Item AncientHammer(int type) =>
        type is 10 or 15 or 30 or 60 ? new AncientSmithyHammer(type) : throw new InvalidOperationException();

    // -- Tailor constructors (ServUO :734-848) -----------------------------------------------------------------------

    private static Item SewingKit(int type) => new SewingKit { UsesRemaining = 250 };

    private static readonly int[][] _clothHues =
    [
        [0x483, 0x48C, 0x488, 0x48A],
        [0x495, 0x48B, 0x486, 0x485],
        [0x48D, 0x490, 0x48E, 0x491],
        [0x48F, 0x494, 0x484, 0x497],
        [0x489, 0x47F, 0x482, 0x47E],
        [0xAAC, 0xAB4, 0xAAF, 0xAB5, 0xAAB]
    ];

    private static Item Cloth(int type) =>
        type >= 0 && type < _clothHues.Length
            ? new UncutCloth(100) { Hue = _clothHues[type].RandomElement() }
            : throw new InvalidOperationException();

    private static readonly int[] _sandalHues = [0x489, 0x47F, 0x482, 0x47E, 0x48F, 0x494, 0x484, 0x497];

    private static Item Sandals(int type) => new Sandals(_sandalHues.RandomElement());

    private static Item StretchedHide(int type) =>
        Utility.Random(4) switch
        {
            0 => new SmallStretchedHideEastDeed(),
            1 => new SmallStretchedHideSouthDeed(),
            2 => new MediumStretchedHideEastDeed(),
            _ => new MediumStretchedHideSouthDeed()
        };

    private static Item Tapestry(int type) =>
        Utility.Random(4) switch
        {
            0 => new LightFlowerTapestryEastDeed(),
            1 => new LightFlowerTapestrySouthDeed(),
            2 => new DarkFlowerTapestryEastDeed(),
            _ => new DarkFlowerTapestrySouthDeed()
        };

    private static Item BearRug(int type) =>
        Utility.Random(4) switch
        {
            0 => new BrownBearRugEastDeed(),
            1 => new BrownBearRugSouthDeed(),
            2 => new PolarBearRugEastDeed(),
            _ => new PolarBearRugSouthDeed()
        };

    private static Item RunicKit(int type) =>
        type is >= 1 and <= 3
            ? new RunicSewingKit(CraftResource.RegularLeather + type, 60 - type * 15)
            : throw new InvalidOperationException();

    private static Item TailorPowerScroll(int type) =>
        type is 5 or 10 or 15 or 20 ? new PowerScroll(SkillName.Tailoring, 100 + type) : throw new InvalidOperationException();
}
