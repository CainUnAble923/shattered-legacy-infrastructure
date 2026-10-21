// cc-P168: Craft This Order, the Masterwork Hammer of Hephaestus's (T3) own use. Ours (Chase, 2026-10-18: "one tier needs
// to be able to auto fill a BOD", answered T3, and "crafts the order for you", each item a normal craft).
//
// The player holds the T3 hammer, picks Craft This Order on its single-click menu and targets a smith deed in their
// backpack. The run is a Craft X run (ClusterFCraftRun.cs) that carries the deed (MakeXRun.Order), so every attempt is an
// ordinary CraftSystem.CreateItem at the normal craft delay: the skill roll and its gains, the ingots, one hammer use,
// familiarity and the T3 ingot saving, all as a hand-made craft. The deed decides what is made:
//   - a small deed: its item, in its metal (iron for an iron deed);
//   - a large deed: first every complete small smith deed lying directly in the backpack goes in, through the large
//     deed's own combine (pinned LargeBOD.EndCombine, as "Combine this deed with contained items" does, BODCombineContained);
//     then the missing count of each open line is crafted, line by line in the deed's order, in the deed's metal.
// Every finished item is offered to the deed through the deed's own add, EndCombine (pinned SmallBOD.EndCombine; for a
// large deed LargeSmithBOD.EndCombine -> LargeBODItemFill.Fill), so the checks (type, metal, exceptional) are exactly the
// ones a player's own combine meets. An item it refuses (a normal one for an exceptional-only deed) stays in the pack as a
// hand-made craft would; for a Society member the Hammer's auto-fill may still file it into another deed (HammerBODAutoFill).
// A failed attempt leaves what a failed craft leaves.
//
// It stops: the deed full (done); out of ingots or other materials; the hammer out of uses, exhausted or out of hand;
// moving (any step, so also away from the anvil and forge); taking damage (PlayerMobile.OnDamage, the point where damage
// has landed, calls OnHurt: PlayerMobile-craft-order-hurt.patch); the deed leaving the backpack; dying; a full or overloaded pack; the Stop button; 500
// attempts (ClusterFCraftRun.AttemptsCap). One progress line per attempt ("Crafting 7 of 20: ...") and a summary at the end.
// Never a non-smith deed, a deed outside the backpack (so never the bank, never another player's).

using System;
using System.Text.RegularExpressions;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.Craft;

public static class ClusterFCraftOrder
{
    public static void Choose(Mobile from, MasterworkHammerOfHephaestus hammer)
    {
        from.SendMessage(0x59, "Target the smith bulk order deed to craft.");
        from.BeginTarget(-1, false, TargetFlags.None, (m, targeted) => Begin(m, hammer, targeted as Item));
    }

    /// <summary>Starts crafting the deed, or says why not. The run, or null.</summary>
    public static MakeXRun Begin(Mobile from, MasterworkHammerOfHephaestus hammer, Item target)
    {
        if (!CanStart(from, hammer, target, out var why))
        {
            from.SendMessage(0x22, why);
            return null;
        }

        var deed = (BaseBOD)target;
        var system = DefBlacksmithy.CraftSystem;

        if (deed is LargeSmithBOD large)
        {
            var combined = CombineSmallDeeds(from, large);
            if (combined > 0)
            {
                from.SendMessage(0x59, $"{combined} small order{(combined == 1 ? "" : "s")} from your pack went into the large order.");
            }

            if (large.Complete)
            {
                from.SendMessage(0x44, "The large order is complete.");
                return null;
            }
        }

        var item = NextItem(deed, system);
        if (item == null)
        {
            from.SendMessage(0x22, "The hammer cannot make what this order still needs.");
            return null;
        }

        var run = ClusterFCraftRun.Begin(from, system, item, hammer, ResourceType(deed), Needed(deed), false, false, null, deed);
        if (run != null)
        {
            _running[from] = run;
        }

        return run;
    }

    // The order runs in progress, for OnHurt; a run leaves at its end (Ended) or at its crafter's next hurt after it.
    private static readonly System.Collections.Generic.Dictionary<Mobile, MakeXRun> _running = new();

    /// <summary>
    /// From PlayerMobile.OnDamage (PlayerMobile-craft-order-hurt.patch), called only when damage has landed: marks the
    /// crafter's order run hurt, and it stops after the attempt in hand.
    /// </summary>
    public static void OnHurt(Mobile m)
    {
        if (!_running.TryGetValue(m, out var run))
        {
            return;
        }

        if (run.StoppedBy == null)
        {
            run.Hurt = true;
        }
        else
        {
            _running.Remove(m);
        }
    }

    /// <summary>From ClusterFCraftRun.End: the run is over.</summary>
    public static void Ended(MakeXRun run)
    {
        if (_running.TryGetValue(run.From, out var mine) && mine == run)
        {
            _running.Remove(run.From);
        }
    }

    public static bool CanStart(Mobile from, MasterworkHammerOfHephaestus hammer, Item target, out string why)
    {
        why = null;

        if (!from.Alive)
        {
            why = "You cannot do that while dead.";
        }
        else if (hammer == null || hammer.Deleted || hammer.HeldBy != from)
        {
            why = "You must hold the Masterwork Hammer of Hephaestus.";
        }
        else if (hammer.Exhausted || hammer.UsesRemaining < 1)
        {
            why = "The Masterwork Hammer of Hephaestus is exhausted.";
        }
        else if (target is not (SmallSmithBOD or LargeSmithBOD))
        {
            why = target is BaseBOD ? "Only a smith's bulk order can be crafted this way." : "That is not a bulk order deed.";
        }
        else if (from.Backpack == null || !target.IsChildOf(from.Backpack))
        {
            why = "The order must be in your backpack.";
        }
        else if (((BaseBOD)target).Complete)
        {
            why = "That order is already complete.";
        }
        else
        {
            DefBlacksmithy.CheckAnvilAndForge(from, 2, out var anvil, out var forge);
            if (!anvil || !forge)
            {
                why = "You must be near an anvil and a forge.";
            }
        }

        return why == null;
    }

    /// <summary>A large deed takes the complete small smith deeds lying directly in the backpack, by its own combine.</summary>
    public static int CombineSmallDeeds(Mobile from, LargeSmithBOD large)
    {
        var smalls = new System.Collections.Generic.List<SmallSmithBOD>();
        foreach (var item in from.Backpack.Items)
        {
            if (item is SmallSmithBOD { Complete: true } small)
            {
                smalls.Add(small);
            }
        }

        var combined = 0;
        foreach (var small in smalls)
        {
            if (large.Complete)
            {
                break;
            }

            large.EndCombine(from, small);
            if (small.Deleted)
            {
                combined++;
            }
        }

        Tidy(from);
        return combined;
    }

    /// <summary>What the deed still needs made next: the small deed's item, or a large deed's first open line's.</summary>
    public static CraftItem NextItem(BaseBOD deed, CraftSystem system)
    {
        if (deed is SmallSmithBOD small)
        {
            return small.Complete || small.Type == null ? null : system.CraftItems.SearchFor(small.Type);
        }

        if (deed is LargeSmithBOD large)
        {
            foreach (var entry in large.Entries)
            {
                if (entry.Amount < large.AmountMax && entry.Details.Type != null &&
                    system.CraftItems.SearchFor(entry.Details.Type) is { } item)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>Items the deed still needs, and how many it holds of how many in all.</summary>
    public static (int done, int total) Count(BaseBOD deed)
    {
        if (deed is SmallBOD small)
        {
            return (small.AmountCur, small.AmountMax);
        }

        var large = (LargeBOD)deed;
        var done = 0;
        foreach (var entry in large.Entries)
        {
            done += Math.Min(entry.Amount, large.AmountMax);
        }

        return (done, large.AmountMax * large.Entries.Length);
    }

    public static int Needed(BaseBOD deed)
    {
        var (done, total) = Count(deed);
        return total - done;
    }

    /// <summary>The ingot the deed's metal is made from; iron for a deed that names none.</summary>
    public static Type ResourceType(BaseBOD deed)
    {
        var material = deed is SmallBOD s ? s.Material : ((LargeBOD)deed).Material;
        var resource = material == BulkMaterialType.None ? CraftResource.Iron : CraftResourceFor(material);
        var types = CraftResources.GetInfo(resource)?.ResourceTypes;
        return types is { Length: > 0 } ? types[0] : typeof(IronIngot);
    }

    private static CraftResource CraftResourceFor(BulkMaterialType material) => material switch
    {
        BulkMaterialType.DullCopper => CraftResource.DullCopper,
        BulkMaterialType.ShadowIron => CraftResource.ShadowIron,
        BulkMaterialType.Copper     => CraftResource.Copper,
        BulkMaterialType.Bronze     => CraftResource.Bronze,
        BulkMaterialType.Gold       => CraftResource.Gold,
        BulkMaterialType.Agapite    => CraftResource.Agapite,
        BulkMaterialType.Verite     => CraftResource.Verite,
        BulkMaterialType.Valorite   => CraftResource.Valorite,
        BulkMaterialType.Platinum   => CraftResource.Platinum,
        BulkMaterialType.Toxic      => CraftResource.Toxic,
        BulkMaterialType.Blaze      => CraftResource.Blaze,
        BulkMaterialType.Frost      => CraftResource.Frost,
        BulkMaterialType.Obsidian   => CraftResource.Obsidian,
        BulkMaterialType.Mythril    => CraftResource.Mythril,
        BulkMaterialType.Adamantium => CraftResource.Adamantium,
        BulkMaterialType.Celestial  => CraftResource.Celestial,
        _                           => CraftResource.Iron
    };

    /// <summary>
    /// From HammerBODAutoFill.TryAutoFill: during an order run, offers the finished item to the order's deed through the
    /// deed's own EndCombine. True when the deed took it.
    /// </summary>
    public static bool TryFill(Mobile from, Item crafted)
    {
        var run = DefBlacksmithy.CraftSystem.GetContext(from)?.Run;

        if (run?.Order is not { Deleted: false } deed || from.Backpack == null || !deed.IsChildOf(from.Backpack))
        {
            return false;
        }

        deed.EndCombine(from, crafted);
        Tidy(from);

        if (!crafted.Deleted)
        {
            return false;
        }

        run.Filled++;
        return true;
    }

    // The deed's own combine opens its gump and a new combine cursor for a player combining by hand; the run needs neither.
    private static void Tidy(Mobile from)
    {
        from.CloseGump<SmallBODGump>();
        from.CloseGump<LargeBODGump>();

        if (from.Target is BODTarget)
        {
            Target.Cancel(from);
        }
    }

    /// <summary>The order's stops, before the common ones; it also moves a large order on to its next open line.</summary>
    public static MakeXStop? CheckStop(MakeXRun run)
    {
        var deed = run.Order;
        var from = run.From;

        if (deed.Deleted || from.Backpack == null || !deed.IsChildOf(from.Backpack))
        {
            return MakeXStop.OrderGone;
        }

        if (deed.Complete)
        {
            return MakeXStop.Done;
        }

        if (run.StopRequested)
        {
            return MakeXStop.Stopped;
        }

        if (run.Hurt)
        {
            return MakeXStop.Damaged;
        }

        if (run.Tool is not MasterworkHammerOfHephaestus hammer || hammer.HeldBy != from || hammer.Exhausted)
        {
            return MakeXStop.Tool;
        }

        if (run.Attempts >= ClusterFCraftRun.AttemptsCap)
        {
            return MakeXStop.Stopped;
        }

        var next = NextItem(deed, run.System);
        if (next == null)
        {
            return MakeXStop.CannotCraft;
        }

        run.Item = next;
        return null;
    }

    /// <summary>One line before each attempt: "Crafting 7 of 20: Plate Chest ...".</summary>
    public static void Progress(MakeXRun run)
    {
        if (run.Order == null)
        {
            return;
        }

        var (done, total) = Count(run.Order);
        run.From.SendMessage(0x59, $"Crafting {done + 1} of {total}: {ItemName(run.Item)} ...");
    }

    public static string GumpLine(MakeXRun run)
    {
        var (done, total) = Count(run.Order);
        return $"{done} of {total} in the order";
    }

    public static string ItemName(CraftItem item) =>
        Regex.Replace(item.ItemType.Name, "(?<=[a-z])(?=[A-Z])", " ");

    public static string StopText(MakeXStop reason) => ClusterFCraftRun.StopText(reason).Replace("Craft X", "Craft order");

    public static string Summary(MakeXRun run)
    {
        var (done, total) = Count(run.Order);
        var kept = run.Made - run.Filled;
        return $"Made {run.Made} ({run.Failed} failed, {run.Exceptional} exceptional), {run.Filled} into the order " +
               $"(now {done} of {total}){(kept > 0 ? $", {kept} not taken by it" : "")}.";
    }
}
