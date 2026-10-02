// cc-P33 (F-3): what ServUO pub57's BaseTrash (Items/Containers/BaseTrash.cs) adds to a trash container, held here
// so pinned's own TrashBarrel and TrashChest can use it through two small patches
// (server/patches/TrashBarrel-clean-up-britannia.patch, TrashChest-clean-up-britannia.patch) and our
// CleanupTrashBarrel through plain calls. ServUO keeps the list on the container (m_Cleanup, BaseTrash.cs:22); pinned's
// containers have no such field, so it is kept beside them, weakly, and is not saved (ServUO does not save it either:
// every Deserialize starts a new list).
//
//   AddCleanupItem        BaseTrash.cs:80-117. Records who dropped what, and its points, when dropped.
//   ConfirmCleanupItem    BaseTrash.cs:119-133. Marks an item as really thrown away, as the container deletes it.
//   Award                 the payout block of each container's Empty (TrashBarrel.cs:159-171, the same in
//                         CleanupTrashBarrel.cs and TrashChest.cs): each dropper is paid the sum of their confirmed
//                         items, with the count of everything they dropped.
//   AppraiseforCleanup    BaseTrash.cs:30-52. The "Appraise for Cleanup" context entry.

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
    private sealed class CleanupArray
    {
        public Mobile Mobile;
        public Item Item;
        public double Points;
        public bool Confirm;
        public Serial Serial;
    }

    private static readonly ConditionalWeakTable<Container, List<CleanupArray>> _cleanup = new();

    private static List<CleanupArray> For(Container trash) => _cleanup.GetOrCreateValue(trash);

    public static int PendingCount(Container trash) => _cleanup.TryGetValue(trash, out var list) ? list.Count : 0;

    public static bool AddCleanupItem(Container trash, Mobile from, Item item)
    {
        if (!CleanUpBritanniaData.Enabled)
        {
            return false;
        }

        var cleanup = For(trash);
        var added = false;

        if (item is BaseContainer c)
        {
            var list = new List<Item>();

            foreach (var child in c.FindItemsByType<Item>())
            {
                list.Add(child);
            }

            for (var i = list.Count - 1; i >= 0; --i)
            {
                var points = CleanUpBritanniaData.GetPoints(list[i]);

                if (points > 0 && cleanup.Find(x => x.Serial == list[i].Serial) == null)
                {
                    cleanup.Add(new CleanupArray { Mobile = from, Item = list[i], Points = points, Serial = list[i].Serial });
                    added = true;
                }
            }
        }
        else
        {
            var points = CleanUpBritanniaData.GetPoints(item);

            if (points > 0 && cleanup.Find(x => x.Serial == item.Serial) == null)
            {
                cleanup.Add(new CleanupArray { Mobile = from, Item = item, Points = points, Serial = item.Serial });
                added = true;
            }
        }

        return added;
    }

    public static void ConfirmCleanupItem(Container trash, Item item)
    {
        if (!_cleanup.TryGetValue(trash, out var cleanup))
        {
            return;
        }

        if (item is BaseContainer c)
        {
            var serials = new HashSet<Serial>();

            foreach (var child in c.FindItemsByType<Item>())
            {
                serials.Add(child.Serial);
            }

            foreach (var entry in cleanup.Where(r => serials.Contains(r.Item.Serial)))
            {
                entry.Confirm = true;
            }
        }
        else
        {
            foreach (var entry in cleanup.Where(r => r.Item.Serial == item.Serial))
            {
                entry.Confirm = true;
            }
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
