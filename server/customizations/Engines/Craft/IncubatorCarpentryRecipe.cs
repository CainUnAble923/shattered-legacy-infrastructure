// ServUO: Services/Craft/DefCarpentry.cs:229 (CC6 follow-up, Q-053, P12), the incubator's carpentry recipe:
//
//     index = AddCraft(typeof(Incubator), 1044294, 1112479, 90.0, 115.0, typeof(Board), 1044041, 100, 1044351);
//
// inside ServUO's `#region SA / if (Core.SA)` block of the "Other" group (1044294), after GargishBanner and before
// ChickenCoop and ExodusSummoningAlter, none of which this shard has. Without it the incubator exists and no player
// can make one - "ported but unreachable", the shape P12 exists to fix - so it ships with the type.
//
// Registered ADDITIVELY rather than by a patch to pinned DefCarpentry.cs, argued in
// shard-migration/notes/cc6-followup-breath-incubator.md §4: CraftSystem.AddCraft and SetNeededExpansion are public,
// DefCarpentry.CraftSystem is created by DefCarpentry.Initialize(), and this repo already appends to it after that
// point from EventSink.ServerStarted (ClusterFLumberjackingExtension, the carpentry sub-resources). The entry lands at
// the end of the "Other" group, which is where ServUO's SA block sits relative to everything pinned has.
//
// The resource is typeof(Log) with name 1044041 ("Boards or Logs"), as every wood entry in pinned DefCarpentry.cs is;
// CraftItem's type table (server/customizations/CraftItem.cs:64) makes Log and Board interchangeable at consumption,
// so 100 boards make one exactly as ServUO's typeof(Board) does. Skill 90.0-115.0, no exceptional restriction, no
// recipe, no second resource: verbatim.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class IncubatorCarpentryRecipe
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's incubator entry to DefCarpentry.CraftSystem. Idempotent; the test host calls it directly
    /// because ServerStarted never fires there.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var carpentry = DefCarpentry.CraftSystem;

        if (carpentry == null)
        {
            Console.WriteLine("[IncubatorCarpentryRecipe] WARNING: DefCarpentry.CraftSystem is null; the incubator is not craftable.");
            return;
        }

        if (!Core.SA)
        {
            return;
        }

        _registered = true;

        var index = carpentry.AddCraft(typeof(Incubator), 1044294, 1112479, 90.0, 115.0, typeof(Log), 1044041, 100, 1044351);
        carpentry.SetNeededExpansion(index, Expansion.SA);
    }
}
