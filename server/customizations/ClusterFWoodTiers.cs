// cc-P67 Part A (F-28 item 3, Chase 2026-10-02; cc-P54 decision 10, Chase 2026-10-05): the eight extended woods as a
// craft-skill ladder, like the post-Valorite metals (ClusterFMetalTiers).
//
// Until now every extended wood needed a flat 100 in Carpentry and Fletching (ClusterFLumberjackingExtension), so a wood
// said nothing about skill and no wood could carry a craft past its stock items. F-28 re-gates them on the 200 cap with
// the overseer's default that Chase approved: Ironwood 100, Ghostwood 115, Emberbark 130, Frostbark 140, Shadowbark 155,
// Runewood 170, Voidwood 185, Starwood 200, the same numbers in both crafts. Every number that keys an extended wood to
// Carpentry or Fletching reads this table: the two craft menus' sub-resources and the craft gain ceiling
// (ClusterFCraftGain). Lumberjacking's harvest numbers are separate (the trees still need 100 Lumberjacking).
//
// The OSI woods (Oak 65 .. Frostwood 100, pinned DefCarpentry.cs:601-607, DefBowFletching.cs:264-270) stay as pinned.

using Server.Items;

namespace Server;

public static class ClusterFWoodTiers
{
    public const double Ironwood   = 100.0;
    public const double Ghostwood  = 115.0;
    public const double Emberbark  = 130.0;
    public const double Frostbark  = 140.0;
    public const double Shadowbark = 155.0;
    public const double Runewood   = 170.0;
    public const double Voidwood   = 185.0;
    public const double Starwood   = 200.0;

    /// <summary>Carpentry or Fletching (Base) needed to work the wood; 0 for anything that is not an extended wood.</summary>
    public static double RequiredSkill(CraftResource r) => r switch
    {
        CraftResource.Ironwood   => Ironwood,
        CraftResource.Ghostwood  => Ghostwood,
        CraftResource.Emberbark  => Emberbark,
        CraftResource.Frostbark  => Frostbark,
        CraftResource.Shadowbark => Shadowbark,
        CraftResource.Runewood   => Runewood,
        CraftResource.Voidwood   => Voidwood,
        CraftResource.Starwood   => Starwood,
        _                        => 0.0
    };

    /// <summary>
    /// The craft skill a craft in this wood can teach up to: the next wood's requirement (Starwood, the last, to the cap:
    /// its own 200). 0 for the OSI woods, whose crafts teach to the item's own maximum as stock (ClusterFCraftGain).
    /// </summary>
    public static double GainCeiling(CraftResource r) => r switch
    {
        CraftResource.Ironwood   => Ghostwood,
        CraftResource.Ghostwood  => Emberbark,
        CraftResource.Emberbark  => Frostbark,
        CraftResource.Frostbark  => Shadowbark,
        CraftResource.Shadowbark => Runewood,
        CraftResource.Runewood   => Voidwood,
        CraftResource.Voidwood   => Starwood,
        CraftResource.Starwood   => Starwood,
        _                        => 0.0
    };
}
