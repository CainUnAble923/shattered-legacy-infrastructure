using System;
using Server.Items;
using Server.Mobiles;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFHuntersSatchelHooks
//
// Wires up the Hunter's Satchel auto-routing features via partial class hooks:
//
//   Corpse.OnItemAdded        — when a carved item is dropped on a corpse, find
//                               the nearest player with a satchel and grab it
//                               automatically (T1 "grab from cut corpse").
//
//   ClusterFBackpack.TryDropItem — intercepts items *before* they land in the
//                               player's backpack (SkinningKnife leather, any
//                               PlaceInBackpack call) and routes accepted types
//                               directly into the satchel.  Intercepting here
//                               rather than OnItemAdded avoids two problems:
//                               (1) CheckHold double-counting the item that is
//                               already in the pack, and (2) the stacking-bypass
//                               where items that merge with existing backpack
//                               stacks never trigger OnItemAdded at all.
//
//   ClusterFHuntersSatchelSystem.Configure() — WorldLoad re-registers
//               SeasonedHuntersSatchel auto-harvest timers after server restart
//               (OnAdded does not fire on deserialization).
// ─────────────────────────────────────────────────────────────────────────────

// ── Corpse hook: auto-grab carved items into nearby satchel ───────────────────

namespace Server.Items
{
    public partial class Corpse
    {
        public override void OnItemAdded(Item item)
        {
            base.OnItemAdded(item);
            TryRouteCarvedItemToSatchel(item);
        }

        private void TryRouteCarvedItemToSatchel(Item item)
        {
            if (!HuntersSatchel.Accepts(item)) return;
            if (Map == null || Map == Map.Internal) return;

            // Find the closest player within 2 tiles who has a satchel.
            // In practice this is the person who just carved the corpse.
            foreach (var mob in Map.GetMobilesInRange(Location, 2))
            {
                if (mob is not PlayerMobile pm || pm.Backpack == null) continue;

                var satchel = pm.Backpack.FindItemByType<HuntersSatchel>();
                if (satchel == null) continue;

                if (satchel.TryRoute(pm, item))
                    return;   // routed successfully — stop looking
            }
        }
    }
}

// ── ClusterFBackpack hook: intercept items before they enter the pack ─────────
// Overriding TryDropItem (pre-placement) instead of OnItemAdded (post-placement)
// ensures the item's weight is not yet counted in the backpack when CheckHold
// propagates upward, and items that would stack with existing pack items are
// routed before the stacking occurs (stacking deletes the dropped item and
// never fires OnItemAdded).

namespace Server.Items
{
    public partial class ClusterFBackpack
    {
        public override bool TryDropItem(Mobile from, Item dropped, bool sendFullMessage)
        {
            // Item is being moved OUT of a satchel — don't re-route it back in.
            if (dropped.Parent is HuntersSatchel)
                return base.TryDropItem(from, dropped, sendFullMessage);

            if (HuntersSatchel.Accepts(dropped))
            {
                var satchel = FindItemByType<HuntersSatchel>();
                if (satchel != null)
                {
                    var owner = from ?? Parent as Mobile ?? RootParent as Mobile;
                    if (satchel.TryRoute(owner, dropped))
                        return true;   // routed directly into satchel
                }
            }

            return base.TryDropItem(from, dropped, sendFullMessage);
        }
    }
}

// ── WorldLoad: re-register SeasonedHuntersSatchel timers after restart ────────

namespace Server
{
    public static class ClusterFHuntersSatchelSystem
    {
        public static void Configure()
        {
            EventSink.WorldLoad += OnWorldLoad;
        }

        private static void OnWorldLoad()
        {
            var restarted = 0;
            foreach (var item in World.Items.Values)
            {
                if (item is Items.SeasonedHuntersSatchel satchel && !satchel.Deleted)
                {
                    satchel.TryRestartAutoHarvest();
                    restarted++;
                }
            }

            if (restarted > 0)
                Console.WriteLine($"[ClusterFHuntersSatchelSystem] Re-registered auto-harvest timers for {restarted} Seasoned Hunter's Satchel(s).");
        }
    }
}
