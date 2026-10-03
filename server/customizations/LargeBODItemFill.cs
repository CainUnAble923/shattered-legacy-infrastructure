// LargeBODItemFill.cs
//
// cc-P42 Part H (Chase, 2026-10-03; found in P39 step 7). A large Smith bulk order deed takes the crafted items
// directly, as well as completed small deeds. Stock fills a large deed only by small deeds (pinned LargeBOD.EndCombine,
// LargeBOD.cs:94-152, "That is not a bulk order" for anything else), and finding small deeds that match a large deed's
// item, material, quality and amount is so unlikely that large deeds were unusable in practice.
//
// An item fills its entry the way a small deed of that entry fills: the same checks, in the same words, as pinned's
// SmallBOD.EndCombine (SmallBOD.cs:117-169): the item's type (or a subclass) on the request and an armor, clothing or
// weapon; the deed's ore or leather when it names one; exceptional when the deed asks for it. One item adds 1 to its
// entry, up to the deed's amount. Pinned's small deed checks nothing more (no "crafted" or maker check, and ServUO
// pub57's SmallBOD.cs:310-381 checks no more either), so neither does this. The item must be in the backpack, as
// pinned's combine target requires (BODTarget.cs:18-22).
//
// Small deeds still combine through pinned's own code (LargeSmithBOD.EndCombine passes them to base). Turn-in is
// unchanged: a large deed is complete when every entry is full. Smith only: LargeSmithBOD is our full-file replacement,
// so the override costs no pinned patch. Tailor would need the same override on pinned's LargeTailorBOD (a patch).
// Not OSI; see the deviation register.

using Server.Gumps;
using Server.Items;

namespace Server.Engines.BulkOrders;

public static class LargeBODItemFill
{
    /// <summary>Fills <paramref name="deed"/>'s entry for <paramref name="item"/>, or says why not, in pinned's words.</summary>
    public static void Fill(LargeBOD deed, Mobile from, Item item)
    {
        if (!item.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1045158); // You must have the item in your backpack to target it.
            return;
        }

        var objectType = item.GetType();
        var armor = item as BaseArmor;
        var clothing = item as BaseClothing;
        var weapon = item as BaseWeapon;

        LargeBulkEntry entry = null;
        if (armor != null || clothing != null || weapon != null)
        {
            foreach (var e in deed.Entries)
            {
                var type = e.Details.Type;
                if (type != null && (objectType == type || objectType.IsSubclassOf(type)))
                {
                    entry = e;
                    break;
                }
            }
        }

        if (entry == null)
        {
            from.SendLocalizedMessage(1045169); // The item is not in the request.
            return;
        }

        if (entry.Amount >= deed.AmountMax)
        {
            // The maximum amount of requested items have already been combined to this deed.
            from.SendLocalizedMessage(1045166);
            return;
        }

        var material = SmallBOD.GetMaterial(armor?.Resource ?? clothing?.Resource ?? CraftResource.None);

        // cc-P46 Part A: the post-Valorite ores too, as pinned's small deed checks them since SmallBOD-post-valorite-
        // material.patch (which also maps them in GetMaterial).
        if ((deed.Material >= BulkMaterialType.DullCopper && deed.Material <= BulkMaterialType.Valorite ||
             deed.Material >= BulkMaterialType.Platinum && deed.Material <= BulkMaterialType.Celestial) &&
            material != deed.Material)
        {
            from.SendLocalizedMessage(1045168); // The item is not made from the requested ore.
        }
        else if (deed.Material >= BulkMaterialType.Spined && deed.Material <= BulkMaterialType.Barbed &&
                 material != deed.Material)
        {
            from.SendLocalizedMessage(1049352); // The item is not made from the requested leather type.
        }
        else if (deed.RequireExceptional && armor?.Quality != ArmorQuality.Exceptional &&
                 clothing?.Quality != ClothingQuality.Exceptional &&
                 weapon?.Quality != WeaponQuality.Exceptional)
        {
            from.SendLocalizedMessage(1045167); // The item must be exceptional.
        }
        else
        {
            item.Delete();
            ++entry.Amount;

            from.SendLocalizedMessage(1045170); // The item has been combined with the deed.
            from.SendGump(new LargeBODGump(deed));

            if (!deed.Complete)
            {
                deed.BeginCombine(from);
            }
        }
    }
}
