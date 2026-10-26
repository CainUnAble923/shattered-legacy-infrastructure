// cc-P33 (F-3): what ServUO pub57's BaseTrash (Items/Containers/BaseTrash.cs) adds to a trash container, held here
// so pinned's own TrashBarrel and TrashChest can use it through two small patches
// (server/patches/TrashBarrel-clean-up-britannia.patch, TrashChest-clean-up-britannia.patch) and our
// CleanupTrashBarrel through plain calls. ServUO keeps the list on the container (m_Cleanup, BaseTrash.cs:22); pinned's
// containers have no such field, so it is kept beside them, weakly, and is not saved (ServUO does not save it either:
// every Deserialize starts a new list).
//
//   AddCleanupItem        BaseTrash.cs:80-117. Records who dropped what, and how many (cc-P656: no longer its points).
//   ConfirmCleanupItem    BaseTrash.cs:119-133. Marks an item as really thrown away, as the container deletes it, and
//                         (cc-P656) prices it then.
//   Award                 the payout block of each container's Empty (TrashBarrel.cs:159-171, the same in
//                         CleanupTrashBarrel.cs and TrashChest.cs): each dropper is paid the sum of their confirmed
//                         items, with the count of everything they dropped.
//   AppraiseforCleanup    BaseTrash.cs:30-52. The "Appraise for Cleanup" context entry.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Points;
using Server.Mobiles;

namespace Server.Items;

public static class CleanUpTrash
{
    // cc-P656 (P476 U2, P432 #1/#2): a record is a dropper's share of one item, in units (its Amount when dropped, or
    // what a merged stack added). Points are priced when the container deletes the item, from the amount it holds then
    // (ConfirmCleanupItem). Until P656 they were fixed by serial at the drop: lifting one ingot off a stack of 1,000 left
    // the 1,000-ingot price on the lifted piece (Mobile.LiftItemDupe keeps the lifted piece's serial), so throwing that
    // one ingot back paid for the whole stack. A stack that merged into another scored 0.
    private sealed class CleanupArray
    {
        public Mobile Mobile;
        public Item Item;
        public int Amount;
        public double Points;
        public bool Confirm;
    }

    private static readonly ConditionalWeakTable<Container, List<CleanupArray>> _cleanup = new();

    private static List<CleanupArray> For(Container trash) => _cleanup.GetOrCreateValue(trash);

    public static int PendingCount(Container trash) => _cleanup.TryGetValue(trash, out var list) ? list.Count : 0;

    /// <summary>
    /// cc-P656: the amounts of the container's loose items before a drop, so AddCleanupItem can tell which stack a
    /// dropped stack merged into (Container.TryDropItem deletes the dropped item and adds its amount to that stack).
    /// </summary>
    public static Dictionary<Item, int> Snapshot(Container trash)
    {
        var amounts = new Dictionary<Item, int>();

        foreach (var item in trash.Items)
        {
            amounts[item] = item.Amount;
        }

        return amounts;
    }

    public static bool AddCleanupItem(Container trash, Mobile from, Item item, Dictionary<Item, int> before = null)
    {
        if (!CleanUpBritanniaData.Enabled)
        {
            return false;
        }

        var cleanup = For(trash);

        // Merged into a stack already in the container: the dropper's share is what they added to it.
        if (item.Deleted)
        {
            if (before == null)
            {
                return false;
            }

            foreach (var stack in trash.Items)
            {
                if (before.TryGetValue(stack, out var was) && stack.Amount - was == item.Amount &&
                    CleanUpBritanniaData.GetPoints(stack) > 0)
                {
                    cleanup.Add(new CleanupArray { Mobile = from, Item = stack, Amount = item.Amount });
                    return true;
                }
            }

            return false;
        }

        var added = false;

        if (item is BaseContainer c)
        {
            foreach (var child in c.FindItemsByType<Item>())
            {
                added |= Record(cleanup, from, child);
            }
        }
        else
        {
            added = Record(cleanup, from, item);
        }

        return added;
    }

    // A whole item dropped in is the dropper's: whoever's records it had are dropped (it left and came back, or was
    // never theirs in this amount).
    private static bool Record(List<CleanupArray> cleanup, Mobile from, Item item)
    {
        if (CleanUpBritanniaData.GetPoints(item) <= 0)
        {
            return false;
        }

        cleanup.RemoveAll(x => x.Item == item);
        cleanup.Add(new CleanupArray { Mobile = from, Item = item, Amount = item.Amount });
        return true;
    }

    public static void ConfirmCleanupItem(Container trash, Item item)
    {
        if (!_cleanup.TryGetValue(trash, out var cleanup))
        {
            return;
        }

        if (item is BaseContainer c)
        {
            foreach (var child in c.FindItemsByType<Item>())
            {
                Price(cleanup, child);
            }
        }
        else
        {
            Price(cleanup, item);
        }
    }

    // cc-P656: the item's value as it is deleted, shared by its droppers' units. Never more than the item is worth: if
    // the records add up to more units than it holds (a piece was lifted off), each share shrinks to fit; if it holds
    // more (someone stacked onto it by hand), the unrecorded part pays no one.
    private static void Price(List<CleanupArray> cleanup, Item item)
    {
        var records = cleanup.FindAll(r => r.Item == item);

        if (records.Count == 0)
        {
            return;
        }

        var value = CleanUpBritanniaData.GetPoints(item);
        var units = Math.Max(records.Sum(r => r.Amount), Math.Max(item.Amount, 1));

        foreach (var entry in records)
        {
            entry.Points = value * entry.Amount / units;
            entry.Confirm = true;
        }
    }

    public static void Award(Container trash)
    {
        if (!_cleanup.TryGetValue(trash, out var cleanup) || !cleanup.Any(x => x.Mobile != null))
        {
            return;
        }

        foreach (var m in cleanup.Select(x => x.Mobile).Distinct())
        {
            if (cleanup.Find(x => x.Mobile == m && x.Confirm) != null)
            {
                var point = cleanup.Where(x => x.Mobile == m && x.Confirm).Sum(x => x.Points);

                // You have received approximately ~1_VALUE~points for turning in ~2_COUNT~items for Clean Up Britannia.
                m.SendLocalizedMessage(1151280, $"{point}\t{cleanup.Count(r => r.Mobile == m)}");
                CleanUpBritanniaData.Instance.AwardPoints(m, point);
            }
        }

        cleanup.Clear();
    }

    public static void AddAppraiseEntry(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (CleanUpBritanniaData.Enabled && from is PlayerMobile)
        {
            list.Add(new AppraiseforCleanup());
        }
    }

    private class AppraiseforCleanup : ContextMenuEntry
    {
        public AppraiseforCleanup() : base(1151298, 2) // Appraise for Cleanup
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            from.Target = new AppraiseforCleanupTarget(from);

            // Target items to see how many Clean Up Britannia points you will receive for throwing them away.
            // Continue targeting items until done, then press the ESC key to cancel the targeting cursor.
            from.SendLocalizedMessage(1151299);
        }
    }
}
