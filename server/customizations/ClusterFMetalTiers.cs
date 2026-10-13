// cc-P53 Part B (D64, Chase 2026-10-04): the post-Valorite metals tiered to the 200 skill cap.
//
// The eight Blacksmithy requirements were spread over a 300 cap (Platinum 100, Toxic 125, Blaze 150, Frost 175,
// Obsidian 200, Mythril 225, Adamantium 250, Celestial 300), so at the shard's 200 top Mythril and above could never be
// worked. Chase approved even 12.5 steps ending at the cap. Every number that keys a post-Valorite metal to Blacksmithy
// or Mining skill reads this one table, so the tiers cannot drift apart again: the craft menu's sub-resources
// (ClusterFMiningExtension), the commission offer brackets and completion ranges (ClusterFSmithCommissions), the BOD
// completion ranges (ClusterFSmithBODSystem), the small and large BOD material gates (server/patches), and the salvage
// bag's resmelt difficulty (SmithGuildSalvageBag). Before this, each of those carried its own copy of a 300-era table,
// and two of the copies did not match the craft requirement they gated. Rescaled tables: shard-migration notes
// cc-P53-two-hundred-cap.md, Part B.

using Server.Engines.BulkOrders;
using Server.Items;

namespace Server;

public static class ClusterFMetalTiers
{
    public const double Platinum   = 112.5;
    public const double Toxic      = 125.0;
    public const double Blaze      = 137.5;
    public const double Frost      = 150.0;
    public const double Obsidian   = 162.5;
    public const double Mythril    = 175.0;
    public const double Adamantium = 187.5;
    public const double Celestial  = 200.0;

    /// <summary>
    /// cc-P36: the New Haven dungeon's STARTER metal, worked from 45 Blacksmithy and Tinkering and mined from 45 Mining. Its
    /// enum value (18) sits after Celestial only because values are saved as ints; it is not post-Valorite. It has no
    /// gain ceiling (it teaches as the stock metals do), no bulk-order material and no post-Valorite value anywhere.
    /// </summary>
    public const double Dawnstone  = 45.0;

    /// <summary>
    /// True for the eight metals after Valorite, Platinum .. Celestial, and nothing else. Dawnstone is never one (cc-P36):
    /// use this, not a comparison with Valorite or Platinum, wherever "post-Valorite" is meant.
    /// </summary>
    public static bool IsPostValorite(CraftResource r) => r is >= CraftResource.Platinum and <= CraftResource.Celestial;

    /// <summary>Blacksmithy (Base) needed to work the metal: 0 for iron and the stock metals' own values below 100.</summary>
    public static double RequiredSkill(CraftResource r) => r switch
    {
        CraftResource.DullCopper => 65.0,
        CraftResource.ShadowIron => 70.0,
        CraftResource.Copper     => 75.0,
        CraftResource.Bronze     => 80.0,
        CraftResource.Gold       => 85.0,
        CraftResource.Agapite    => 90.0,
        CraftResource.Verite     => 95.0,
        CraftResource.Valorite   => 99.0,
        CraftResource.Platinum   => Platinum,
        CraftResource.Toxic      => Toxic,
        CraftResource.Blaze      => Blaze,
        CraftResource.Frost      => Frost,
        CraftResource.Obsidian   => Obsidian,
        CraftResource.Mythril    => Mythril,
        CraftResource.Adamantium => Adamantium,
        CraftResource.Celestial  => Celestial,
        CraftResource.Dawnstone  => Dawnstone, // cc-P36: below 99, so the smelt patch's post-Valorite branch never takes it
        _                        => 0.0
    };

    /// <summary>
    /// cc-P61 Part A (D83, Chase 2026-10-05): the Blacksmithy a craft in this metal can teach up to, the next metal's
    /// requirement (Celestial, the last, to the cap: its own 200). 0 for iron and the stock metals, whose crafts teach
    /// to the item's own maximum as stock (ClusterFCraftGain), and 0 for Dawnstone, which teaches as they do (cc-P36).
    /// </summary>
    public static double GainCeiling(CraftResource r) => r switch
    {
        CraftResource.Platinum   => Toxic,
        CraftResource.Toxic      => Blaze,
        CraftResource.Blaze      => Frost,
        CraftResource.Frost      => Obsidian,
        CraftResource.Obsidian   => Mythril,
        CraftResource.Mythril    => Adamantium,
        CraftResource.Adamantium => Celestial,
        CraftResource.Celestial  => Celestial,
        _                        => 0.0
    };

    /// <summary>The same for a bulk order's material; 0 for anything that is not a post-Valorite metal.</summary>
    public static double GainCeiling(BulkMaterialType m) => m switch
    {
        BulkMaterialType.Platinum   => GainCeiling(CraftResource.Platinum),
        BulkMaterialType.Toxic      => GainCeiling(CraftResource.Toxic),
        BulkMaterialType.Blaze      => GainCeiling(CraftResource.Blaze),
        BulkMaterialType.Frost      => GainCeiling(CraftResource.Frost),
        BulkMaterialType.Obsidian   => GainCeiling(CraftResource.Obsidian),
        BulkMaterialType.Mythril    => GainCeiling(CraftResource.Mythril),
        BulkMaterialType.Adamantium => GainCeiling(CraftResource.Adamantium),
        BulkMaterialType.Celestial  => GainCeiling(CraftResource.Celestial),
        _                           => 0.0
    };

    /// <summary>The same table for a bulk order's material; 999 for anything that is not a post-Valorite metal.</summary>
    public static double PostValoriteRequiredSkill(BulkMaterialType m) => m switch
    {
        BulkMaterialType.Platinum   => Platinum,
        BulkMaterialType.Toxic      => Toxic,
        BulkMaterialType.Blaze      => Blaze,
        BulkMaterialType.Frost      => Frost,
        BulkMaterialType.Obsidian   => Obsidian,
        BulkMaterialType.Mythril    => Mythril,
        BulkMaterialType.Adamantium => Adamantium,
        BulkMaterialType.Celestial  => Celestial,
        _                           => 999.0
    };
}
