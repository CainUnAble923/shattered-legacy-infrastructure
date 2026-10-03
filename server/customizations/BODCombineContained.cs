// BODCombineContained.cs
//
// F-17 (cc-P22), ported: OSI's "Combine this deed with contained items" (cliloc 1157304) on small and large bulk
// order deeds. Port source: ServUO pub57 Scripts/Services/BulkOrders/SmallBODs/SmallBODGump.cs:102-103 (the button)
// and :168-176 (case 4), and LargeBODs/LargeBODGump.cs:112 and :154-169. The button itself is added to pinned's
// SmallBODGump and LargeBODGump by server/patches/BOD-combine-contained.patch, which calls Begin below; no hook
// reaches a pinned gump's layout (argued in shard-migration notes/cc-P22-small-features-1.md, F-17).
//
// What it does, as ServUO does it: a target cursor; on a container, every item directly in it (a large deed: every
// small deed directly in it) goes through the deed's own EndCombine, pinned's checks unchanged (type, exceptional,
// material; a large deed's entry, quality, material, amount and completion). An item that fails stays where it is,
// with pinned's message for it. Like ServUO, the backpack itself may be targeted (ServUO accepts any Container).
// cc-P42 Part H: a large Smith deed takes crafted items too (LargeBODItemFill), so for one every item in the
// container is offered, small deeds and items alike; a mixed bag fills every entry it can.
//
// Ours on top of ServUO, each a small addition, none changing what combines:
//   - The container must be the backpack or inside it. ServUO checks nothing here; pinned's single combine refuses
//     an item outside the backpack (BODTarget.cs:18-22, cliloc 1045158), and EndCombine itself does not check, so
//     without this a bag on the ground or in the bank would feed the deed.
//   - The loop stops when the deed is full, and says how many items went in. ServUO keeps calling EndCombine, which
//     repeats "the maximum amount ... already combined" once per remaining item.

using System.Collections.Generic;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.BulkOrders;

public static class BODCombineContained
{
    /// <summary>The reply button the patch adds. Pinned's gumps use 1 (EXIT) and 2 (Combine).</summary>
    public const int ButtonID = 3;

    /// <summary>Combine this deed with contained items.</summary>
    public const int Cliloc = 1157304;

    public static void Begin(Mobile from, BaseBOD deed)
    {
        from.BeginTarget(-1, false, TargetFlags.None, (m, targeted) => Combine(m, deed, targeted));
    }

    /// <summary>Combines every item directly in the targeted container. Returns how many went in.</summary>
    public static int Combine(Mobile from, BaseBOD deed, object targeted)
    {
        if (deed == null || deed.Deleted || !deed.IsChildOf(from.Backpack))
        {
            return 0;
        }

        if (targeted is not Container container)
        {
            from.SendMessage(0x3B2, "That is not a container.");
            return 0;
        }

        if (container != from.Backpack && !container.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1045158); // You must have the item in your backpack to target it.
            return 0;
        }

        var items = new List<Item>();
        foreach (var item in container.Items)
        {
            if (item != deed && (deed is not LargeBOD || deed is LargeSmithBOD || item is SmallBOD))
            {
                items.Add(item);
            }
        }

        var combined = 0;
        foreach (var item in items)
        {
            if (IsFull(deed))
            {
                break;
            }

            if (item.Deleted || item.Parent != container)
            {
                continue;
            }

            deed.EndCombine(from, item);

            if (item.Deleted)
            {
                combined++;
            }
        }

        from.SendMessage(0x3B2, combined == 1
            ? "1 item was combined with the deed."
            : $"{combined} items were combined with the deed.");
        return combined;
    }

    // Pinned's own test: SmallBOD.Complete is AmountCur == AmountMax, LargeBOD.Complete every entry full.
    public static bool IsFull(BaseBOD deed) => deed.Complete;
}
