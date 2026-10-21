// ClusterFStoreBought.cs
//
// cc-P263 ("fix instead of reward", Store-Bought). Before this, any armor, weapon or piece of clothing of the right type,
// material and quality filled a bulk order or a smith commission, so an NPC smith's stock filled plain iron orders with
// no crafting at all. Pinned's small deed checks type, material and quality only (SmallBOD.EndCombine), as does ServUO's;
// pinned already tells the two apart elsewhere: Resmelt says "store-bought" (500418) for an item that is not
// PlayerConstructed. That is the rule here: an order takes only crafted items. Loot is not crafted either, so it no
// longer fills an order (the notes flag it for Chase). Applied by SmallBOD-crafted-only.patch, LargeBODItemFill and
// SmithCommissionSystem.IsMatch / IsMatchForPieceKey. clusterf.bods.craftedOnly (default true) turns it off.

using Server.Items;

namespace Server;

public static class ClusterFStoreBought
{
    public const string Refusal = "System: That was bought, not made. Orders take only items crafted by a player.";

    /// <summary>On by default; the test host runs no Configure, so the default is what the facts see.</summary>
    public static bool CraftedOnly { get; set; } = true;

    public static void Configure() =>
        CraftedOnly = ServerConfiguration.GetOrUpdateSetting("clusterf.bods.craftedOnly", true);

    /// <summary>An armor, weapon or clothing item nobody crafted (an NPC's stock, or loot). Anything else is never refused.</summary>
    public static bool IsStoreBought(Item item) => item switch
    {
        BaseArmor a    => !a.PlayerConstructed,
        BaseWeapon w   => !w.PlayerConstructed,
        BaseClothing c => !c.PlayerConstructed,
        _              => false
    };

    /// <summary>True when an order must refuse <paramref name="item"/>.</summary>
    public static bool Refuses(Item item) => CraftedOnly && IsStoreBought(item);
}
