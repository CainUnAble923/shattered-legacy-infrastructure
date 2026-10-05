// cc-P57 Part D (Chase 2026-10-05, the cc-P56 Part C proposal): our own client clilocs. The player package adds our
// entries to a copy of the player's own Cliloc.enu (player-package/payload/app/Build-UoOverrides.ps1, entries in
// player-package/vendor/shattered-legacy-cliloc/entries.json) and TazUO reads that copy through -uofilesoverride.
//
// Our block is 1,900,000 to 1,999,999: EA's numbers stop at 1,166,081 below 3,000,000 and nothing is used from 1,200,000
// to 2,999,999 (counted in EA's Cliloc.enu, cc-P56 Part C; the block is recorded in
// player-package/vendor/shattered-legacy-cliloc/registry.csv). Pinned sends any number above 0x7FFF as is
// (Server/ContextMenus/ContextMenuEntry.cs:39-48), and a menu holding one goes out on the newer packet, which carries the
// number whole (ContextMenu.cs:51-54, ContextMenuSystem.cs:149, :189-191).
//
// The package's Pester tests read this file and fail when it and entries.json disagree, as they do for the art records.
// A client without the package's entries has no text for these numbers: TazUO draws an empty, zero-width row there
// (PopupMenuGump.cs:49-67, Label.cs), so the entry cannot be clicked until the player takes the update.

using Server.Engines.BulkOrders;

namespace Server;

public static class ShardClilocs
{
    public const int BlockFirst = 1_900_000;
    public const int BlockLast  = 1_999_999;

    /// <summary>"Breed": the pack mule's breeding entry (it showed "Use", 3006132, until cc-P57).</summary>
    public const int Breed = 1_900_000;

    /// <summary>"Smelt Ore": the ore satchels' smelt entry (it showed "Smelt", 3006143, until cc-P57).</summary>
    public const int SmeltOre = 1_900_001;

    /// <summary>"Metal Familiarity": the Hammer of Hephaestus entry (it showed "Knowledge", 1112530, until cc-P57).</summary>
    public const int MetalFamiliarity = 1_900_002;

    // cc-P61 Part C (bug-list D85, Chase 2026-10-05): a post-Valorite smith deed's "All items must be made with ... ingots."
    // line. EA's lines stop at Valorite (1045142-1045149, "All items must be made with valorite ingots."), so pinned's
    // GetMaterialNumberFor gave these metals 0 and the deed gumps and tooltips said nothing (SmallBODGump.cs:85-98,
    // LargeBODGump.cs:106-119, and the two accept gumps). The wording is EA's, with the metal named in Title Case (D-114).

    /// <summary>"All items must be made with Platinum ingots."</summary>
    public const int PlatinumIngots = 1_900_003;

    /// <summary>"All items must be made with Toxic ingots."</summary>
    public const int ToxicIngots = 1_900_004;

    /// <summary>"All items must be made with Blaze ingots."</summary>
    public const int BlazeIngots = 1_900_005;

    /// <summary>"All items must be made with Frost ingots."</summary>
    public const int FrostIngots = 1_900_006;

    /// <summary>"All items must be made with Obsidian ingots."</summary>
    public const int ObsidianIngots = 1_900_007;

    /// <summary>"All items must be made with Mythril ingots."</summary>
    public const int MythrilIngots = 1_900_008;

    /// <summary>"All items must be made with Adamantium ingots."</summary>
    public const int AdamantiumIngots = 1_900_009;

    /// <summary>"All items must be made with Celestial ingots."</summary>
    public const int CelestialIngots = 1_900_010;

    /// <summary>The deed line for a post-Valorite material; 0 (no line, pinned's value) for any other.</summary>
    public static int PostValoriteMaterial(BulkMaterialType material) => material switch
    {
        BulkMaterialType.Platinum   => PlatinumIngots,
        BulkMaterialType.Toxic      => ToxicIngots,
        BulkMaterialType.Blaze      => BlazeIngots,
        BulkMaterialType.Frost      => FrostIngots,
        BulkMaterialType.Obsidian   => ObsidianIngots,
        BulkMaterialType.Mythril    => MythrilIngots,
        BulkMaterialType.Adamantium => AdamantiumIngots,
        BulkMaterialType.Celestial  => CelestialIngots,
        _                           => 0
    };
}
