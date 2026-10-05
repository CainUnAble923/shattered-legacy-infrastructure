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
        _                        => 0.0
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
