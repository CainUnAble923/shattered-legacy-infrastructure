// ServUO: Services/Craft/DefCarpentry.cs:613-636 (CC9 close, 2026-09-29), the four King's Collection instruments'
// carpentry entries. They are the only thing in ServUO that names the four deeds, so without them the instruments exist
// and no player can make one. They ship with the types (Items/Equipment/Instruments/{Cello,Cowbell,Trumpet,
// WallMountedBell}.cs).
//
//     index = AddCraft(typeof(CelloDeed), 1044293, 1098390, 75.0, 105.0, typeof(Board), 1044041, 15, 1044351);
//     AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
//     AddRes(index, typeof(Cloth), 1044286, 5, 1044287);
//     SetNeededThemePack(index, ThemePack.Kings);
//     ... WallMountedBellSouthDeed / WallMountedBellEastDeed: 75.0-105.0, 50 boards, 50 iron ingots
//     ... TrumpetDeed / CowBellDeed: 85.0-105.0, 10 boards, 15 iron ingots
//
// Registered ADDITIVELY, exactly as IncubatorCarpentryRecipe.cs is and for the reasons argued in
// shard-migration/notes/cc6-followup-breath-incubator.md section 4b: CraftSystem.AddCraft, AddSkill and AddRes are
// public, and EventSink.ServerStarted runs after DefCarpentry.Initialize(). The entries land at the end of the
// Instruments group (1044293), after BambooFlute, which is where ServUO puts them relative to everything pinned has.
//
// Two value decisions, both toward the pinned table, both the incubator's: typeof(Log) with name 1044041 ("Boards or
// Logs") for ServUO's typeof(Board), as every wood entry in pinned DefCarpentry.cs is (CraftItem's type table makes the
// two interchangeable at consumption); and no SetNeededExpansion, because ServUO sets none on these four.
//
// One ServUO call has no pinned equivalent: SetNeededThemePack(index, ThemePack.Kings). In ServUO it sets
// CraftItem.RequiredThemePack, which only CraftGumpItem.cs:107 reads, to print one line naming the theme pack. Nothing
// refuses the craft on it. Pinned has no ThemePack type at all. Dropped; the loss is that line of gump text.

using Server.Items;

namespace Server.Engines.Craft;

public static class KingsCollectionCarpentryRecipes
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's four King's Collection instrument entries to DefCarpentry.CraftSystem. Idempotent; the test
    /// host calls it directly because ServerStarted never fires there.
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
            System.Console.WriteLine(
                "[KingsCollectionCarpentryRecipes] WARNING: DefCarpentry.CraftSystem is null; the instruments are not craftable."
            );
            return;
        }

        _registered = true;

        var index = carpentry.AddCraft(typeof(CelloDeed), 1044293, 1098390, 75.0, 105.0, typeof(Log), 1044041, 15, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(Cloth), 1044286, 5, 1044287);

        index = carpentry.AddCraft(typeof(WallMountedBellSouthDeed), 1044293, 1154162, 75.0, 105.0, typeof(Log), 1044041, 50, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(IronIngot), 1044036, 50, 1044037);

        index = carpentry.AddCraft(typeof(WallMountedBellEastDeed), 1044293, 1154163, 75.0, 105.0, typeof(Log), 1044041, 50, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(IronIngot), 1044036, 50, 1044037);

        index = carpentry.AddCraft(typeof(TrumpetDeed), 1044293, 1098388, 85.0, 105.0, typeof(Log), 1044041, 10, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(IronIngot), 1044036, 15, 1044037);

        index = carpentry.AddCraft(typeof(CowBellDeed), 1044293, 1098418, 85.0, 105.0, typeof(Log), 1044041, 10, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(IronIngot), 1044036, 15, 1044037);
    }
}
