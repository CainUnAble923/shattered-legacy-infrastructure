// ServUO: Services/Craft/DefTailoring.cs:543, the one tailoring entry for a type of ours that a player can reach
// (craft registrations task, 2026-09-29; shard-migration/notes/craft-registrations.md section 2):
//
//     AddCraft(typeof(LeatherTalons), 1015288, 1095728, 40.4, 65.4, typeof(Leather), 1044462, 6, 1044453);
//
// in the Footwear group (1015288), inside `#region SA / if (Core.SA)`, after ThighBoots. Verbatim, including ServUO's
// message 1044453 where pinned's leather entries use 1044463. No SetNeededExpansion: ServUO sets none.
//
// Registered ADDITIVELY, as OrcMaskTailoringRecipe.cs is (shard-migration/notes/cc6-followup-breath-incubator.md
// section 4b). A separate file from the orc mask's because that one belongs to the CC9 batch and its fact.
//
// ServUO's DefTailoring names 27 other types of ours, and none of them is here, because none would be reachable:
//
//   - 25 are recipe-gated (AddRecipe). 13 are the Tiger Pelt and Dragon Turtle pieces, which also need TigerPelt and
//     DragonTurtleScute, in neither tree (Q-066, gate 8). The other 12 (AssassinsCowl, MagesHood, the three belts,
//     ElegantCollar and their six Crimson/Fortune/Mace-and-Shield/Scholarly upgrades) have no recipe source we have:
//     ServUO hands the six upgrades out from the Forsaken Foes event (Services/Seasonal Events/ForsakenFoes/Gumps.cs:
//     53-58) and the six base recipes only from TreasureMapChest.GetRandomRecipe, and pinned's chests make no scrolls.
//     Seven of the twelve also need Lodestone or FeyWings, in neither tree.
//   - CuffsOfTheArchmage (:820) is recipe-gated and needs BloodOfTheDarkFather, in neither tree.
//
// When one of those gates is built, register the entry with AddQuestRecipe, not AddRecipe: pinned's tailor satchel draws
// from every AddRecipe-registered tailoring recipe (Q-064), which ServUO's never does.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class TailoringCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's leather talons entry to DefTailoring.CraftSystem. Idempotent; the test host calls it directly
    /// because ServerStarted never fires there.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var tailoring = DefTailoring.CraftSystem;

        if (tailoring == null)
        {
            Console.WriteLine("[TailoringCraftRegistrations] WARNING: DefTailoring.CraftSystem is null; leather talons are not craftable.");
            return;
        }

        _registered = true;

        if (!Core.SA)
        {
            return;
        }

        tailoring.AddCraft(typeof(LeatherTalons), 1015288, 1095728, 40.4, 65.4, typeof(Leather), 1044462, 6, 1044453);
    }
}
