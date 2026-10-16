// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/BasketWeavingBook.cs -> ours. "Making valuables with Basket
// Weaving": a Grandmaster Tinker reads it once and can soften reeds and weave baskets. ServUO keeps the learned flag on
// the PlayerMobile (PlayerFlag.BasketWeaving); pinned's has no such flag, so it lives on the account's learned set as
// the key flag:BasketWeaving, beside the Masonry and Glassblowing books (ClusterFRecipeStore, cc-P78), where it is
// saved, listed by [Recipes, and shared by the account's characters as every recipe is on this shard.
//
// What reads it: ScouringToxin on dry reeds, SoftenedReeds and the baskets at the Tinkering and Alchemy menus
// (TinkeringCraftRegistrations, AlchemyCraftRegistrations: ServUO's SetRequiresBasketWeaving, kept as a craft-time check
// in BasketWeaving.CheckCraft) and the shard's recipe gating.

using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Mobiles;

namespace Server.Items;

public static class BasketWeaving
{
    public const string Flag = "flag:BasketWeaving";

    public static bool Knows(Mobile m) => m is PlayerMobile pm && ClusterFRecipeStore.Knows(pm, Flag);

    public static bool Learn(Mobile m) => m is PlayerMobile pm && ClusterFRecipeStore.Learn(pm, Flag);

    public static bool Forget(Mobile m) => m is PlayerMobile pm && ClusterFRecipeStore.Forget(pm, Flag);
}

[SerializationGenerator(0, false)]
public partial class BasketWeavingBook : Item
{
    [Constructible]
    public BasketWeavingBook() : base(0xFBE) => Weight = 1.0;

    public override int LabelNumber => 1153529; // Making valuables with Basket Weaving

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else if (pm.Skills[SkillName.Tinkering].Base < 100.0)
        {
            pm.SendLocalizedMessage(1112255); // Only a Grandmaster Tinker can learn from this book.
        }
        else if (BasketWeaving.Knows(pm))
        {
            pm.SendLocalizedMessage(1080066); // You have already learned this information.
        }
        else
        {
            BasketWeaving.Learn(pm);
            pm.SendLocalizedMessage(1112254); // You have learned to make baskets. You will need gardeners to make reeds out of plants for you to make these items.

            Delete();
        }
    }
}
