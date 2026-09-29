// ServUO: Services/Craft/DefTailoring.cs:245 (CC9 close, 2026-09-29), the orc mask's tailoring entry:
//
//     AddCraft(typeof(OrcMask), 1011375, 1025147, 75.0, 100.0, typeof(Cloth), 1044455, 12, 1044287);
//
// in the Hats group (1011375), after the SE block's ClothNinjaHood and Kasa and before the four masks pinned already
// declares. It is one of only two things in ServUO that name the type (the other is Loot.HatTypes, which pinned's
// replacement omits; see Items/Equipment/Clothing/OrcMask.cs), so it ships with the type.
//
// Registered ADDITIVELY, the way IncubatorCarpentryRecipe.cs appends to carpentry and for the same reasons
// (shard-migration/notes/cc6-followup-breath-incubator.md section 4b); DefTailoring.CraftSystem is the same kind of
// public static, created by DefTailoring.Initialize(). Appended at ServerStarted, the entry lands at the end of the
// Hats group, after Kasa: exactly where ServUO puts it, since pinned's group ends with the same SE block.
//
// One value decision toward the pinned table: the resource name is 1044286, which is what all 57 cloth entries in
// pinned DefTailoring.cs use, where ServUO's table uses 1044455 throughout. Same resource, same amount; only the label
// in the gump differs, and this keeps the mask's label the same as its neighbours'. No SetNeededExpansion: ServUO sets
// none on it.
//
// Pinned also declares BearMask, DeerMask, TribalMask and HornedTribalMask and crafts none of them; ServUO crafts all
// four beside this one. Not added here: they are stock types, outside this batch, and a decision about the stock
// table. Recorded in shard-migration/notes/cc9-close.md.

using Server.Items;

namespace Server.Engines.Craft;

public static class OrcMaskTailoringRecipe
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's orc mask entry to DefTailoring.CraftSystem. Idempotent; the test host calls it directly because
    /// ServerStarted never fires there.
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
            System.Console.WriteLine("[OrcMaskTailoringRecipe] WARNING: DefTailoring.CraftSystem is null; the orc mask is not craftable.");
            return;
        }

        _registered = true;

        tailoring.AddCraft(typeof(OrcMask), 1011375, 1025147, 75.0, 100.0, typeof(Cloth), 1044286, 12, 1044287);
    }
}
