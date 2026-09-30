// cc-P18, F-1: what Craft X does with a non-exceptional result in "exceptional only" (a reject). Only items this run
// made are moved or recycled; nothing else in the pack is touched, except that the salvage bag's own Salvage All
// salvages whatever is in the salvage bag (Chase, 2026-09-30).
//
// In order, the first that applies:
//   1. The player has a salvage bag and the bag can salvage the item: into the bag. When the run ends, the bag's own
//      Salvage All runs once (pinned Items/Containers/SalvageBag.cs, through SalvageBag-salvage-all.patch), with its
//      own conditions: an anvil and forge within 2 and a smithing tool for ingots, scissors for cloth.
//   2. Blacksmithy: smelted on the spot through the smith's own recycle (pinned Engines/Craft/Core/Resmelt.cs, through
//      Resmelt-smelt-one.patch). A forge is already needed to smith. A metal the crafter lacks the Mining for is not
//      smelted (as by hand) and goes on to 4.
//   3. Tailoring, with scissors in the pack: cut back on the spot (pinned IScissorable.Scissor, public; the item's own
//      rules, e.g. BaseClothing.cs:255).
//   4. Into the rejects bag the player picked when the run started, if it is still in their pack. Otherwise it stays
//      where crafting put it.
// F-3 HOOK (not routed): the Custodian trash bag. Until Clean Up Britannia (F-3) replaces Custodian scoring, crafted
// non-exceptional gear would earn tokens there, making crafting a token farm (brief cc-P18). Route it here once F-3 lands.

using Server.Items;

namespace Server.Engines.Craft;

public static class ClusterFCraftRejects
{
    public static void Handle(MakeXRun run, Item item)
    {
        var from = run.From;
        var pack = from.Backpack;

        if (pack == null)
        {
            return;
        }

        // 1. The salvage bag.
        if (SalvageBagTakes(item) && pack.FindItemByType<SalvageBag>() is { } salvageBag)
        {
            salvageBag.DropItem(item);
            run.SalvageBag ??= salvageBag;
            run.ToSalvageBag++;
            return;
        }

        // 2. Smelt: the smith's own recycle.
        if (run.System is DefBlacksmithy)
        {
            var result = item switch
            {
                BaseArmor armor   => new Resmelt.InternalTarget(run.System, run.Tool).Resmelt(from, armor, armor.Resource),
                BaseWeapon weapon => new Resmelt.InternalTarget(run.System, run.Tool).Resmelt(from, weapon, weapon.Resource),
                _                 => SmeltResult.Invalid
            };

            if (result == SmeltResult.Success)
            {
                run.Smelted++;
                return;
            }
        }

        // 3. Cut back with scissors.
        if (run.System is DefTailoring && item is IScissorable scissorable &&
            pack.FindItemByType<Scissors>() is { } scissors &&
            Scissors.CanScissor(from, scissorable) && scissorable.Scissor(from, scissors))
        {
            run.Cut++;
            return;
        }

        // F-3 HOOK: the Custodian trash bag would go here. Not routed (see the header).

        // 4. The rejects bag.
        var bag = run.RejectsBag;

        if (bag?.Deleted == false && (bag == pack || bag.IsChildOf(pack)))
        {
            bag.DropItem(item);
            run.Bagged++;
        }
    }

    /// <summary>At the end of a run, the salvage bag salvages, once, if this run put anything in it.</summary>
    public static void Finish(MakeXRun run)
    {
        if (run.ToSalvageBag > 0 && run.SalvageBag?.Deleted == false && !run.From.Deleted)
        {
            run.SalvageBag.SalvageAll(run.From);
        }
    }

    // What the salvage bag's Salvage All can salvage (its own tests, SalvageBag.cs:140-170).
    public static bool SalvageBagTakes(Item item) =>
        item is BaseWeapon weapon && CraftResources.GetType(weapon.Resource) == CraftResourceType.Metal ||
        item is BaseArmor armor && CraftResources.GetType(armor.Resource) is CraftResourceType.Metal or CraftResourceType.Leather ||
        item is BaseClothing;

    public static string Describe(MakeXRun run)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (run.Smelted > 0)
        {
            parts.Add($"{run.Smelted} smelted");
        }

        if (run.Cut > 0)
        {
            parts.Add($"{run.Cut} cut");
        }

        if (run.ToSalvageBag > 0)
        {
            parts.Add($"{run.ToSalvageBag} to the salvage bag");
        }

        if (run.Bagged > 0)
        {
            parts.Add($"{run.Bagged} to the rejects bag");
        }

        return parts.Count > 0 ? $"Rejects: {string.Join(", ", parts)}." : "";
    }
}
