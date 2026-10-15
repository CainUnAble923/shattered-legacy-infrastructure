// cc-P122: the 2016 reward collections for smith and tailor bulk orders, ported from ServUO
// Scripts/Services/BulkOrders/Rewards/Rewards.cs (SmithRewardCalculator :315-361, TailorRewardCalculator :667-698 and their
// constructors :393-484, :722-850). In the 2016 system a turn-in's points buy one item from this list in the rewards
// gump; points left over can be banked (BulkOrderSystem).
//
// Every entry is ServUO's, in its order, with its cliloc, hue and price. Three kinds of reward are not on this shard yet and
// stay listed as named placeholders, never dropped (the prompt's rule): the gump shows them with "not on the shard yet" and
// will not sell them, so no player spends points on nothing. Listed in the notes:
//   BODRewardTitleDeed  (a reward title)      no reward-title system in pinned or ours
//   SmeltersTalisman    (smelting bonus)      no smelting-bonus talisman in pinned or ours
//   HarvestMap          (to a rich vein)      no harvest maps in pinned or ours
// The Rock Hammer and the Master Craftsman Talisman are ported beside this file (Items/).

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

    public static IReadOnlyList<BODCollectionItem> Get(BODType type) =>
        type switch
        {
            BODType.Tailor => _tailor ??= BuildTailor(),
            BODType.Smith  => _smith ??= BuildSmith(),
            _              => Array.Empty<BODCollectionItem>() // the six other crafts: P123
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
