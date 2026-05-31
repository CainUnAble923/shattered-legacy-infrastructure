using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFWeightPatch — one-time backpack migration on player login
//
// On first login after deployment any player still carrying a vanilla Backpack
// has it swapped for a ClusterFBackpack.  All item contents are preserved.
//
// Safe to leave registered permanently — the check is a fast type test and is
// a no-op for players already on ClusterFBackpack.
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFWeightPatch
{
    public static void Configure()
    {
        EventSink.Connected += OnConnected;
    }

    private static void OnConnected(Mobile m)
    {
        if (m is not PlayerMobile pm) return;

        // Already migrated — nothing to do.
        if (pm.Backpack is ClusterFBackpack) return;

        // Swap vanilla Backpack → ClusterFBackpack.
        if (pm.Backpack is not Backpack oldPack) return;

        var items   = new List<Item>(oldPack.Items);
        var newPack = new ClusterFBackpack
        {
            LootType = oldPack.LootType,
            Hue      = oldPack.Hue
        };

        // Detach old, attach new
        pm.RemoveItem(oldPack);
        pm.AddItem(newPack);

        // Transfer contents
        foreach (var item in items)
            newPack.DropItem(item);

        // oldPack is now empty; delete cleanly
        oldPack.Delete();
    }
}
