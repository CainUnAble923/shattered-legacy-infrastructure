// ServUO: Services/Craft/DefCarpentry.cs, the two entries for types of ours, not yet registered here, whose resources a
// player can get (craft registrations task, 2026-09-29; shard-migration/notes/craft-registrations.md):
//
//     :460  GargishGnarledStaff   if (Core.SA), Weapons 1044566      :604  AudChar   if (Core.SA), Instruments 1044293
//
// Verbatim but for the one decision IncubatorCarpentryRecipe.cs and KingsCollectionCarpentryRecipes.cs already made:
// typeof(Log) with name 1044041 ("Boards or Logs") for ServUO's typeof(Board), as every wood entry in pinned
// DefCarpentry.cs is; CraftItem's type table makes the two interchangeable at consumption. No SetNeededExpansion: ServUO
// sets none. Registered additively, for the reasons those two files give.
//
// Left out: all six Darkwood pieces (:529-564). Each needs one peerless ingredient, and five of the six ingredients
// (LardOfParoxysmus, DreadHornMane, DiseasedBark, EyeOfTheTravesty, CapturedEssence) are declared by pinned
// (Items/Resources/MiscMLResources.cs) and dropped by nothing in pinned or ours. The sixth, DarkwoodLegs, has its
// GrizzledBones (pinned Ilhenir.cs:142) but also needs Putrefaction, whose only source in either tree is our
// BasePeerless.PackResources, which nothing calls.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class CarpentryCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's gargish gnarled staff and aud-char entries to DefCarpentry.CraftSystem. Idempotent; the test
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
            Console.WriteLine("[CarpentryCraftRegistrations] WARNING: DefCarpentry.CraftSystem is null; the gargish staff and aud-char are not craftable.");
            return;
        }

        _registered = true;

        if (!Core.SA)
        {
            return;
        }

        var index = carpentry.AddCraft(typeof(GargishGnarledStaff), 1044566, 1097488, 78.9, 128.9, typeof(Log), 1044041, 16, 1044351);
        carpentry.AddRes(index, typeof(EcruCitrine), 1026252, 1, 1053098);

        index = carpentry.AddCraft(typeof(AudChar), 1044293, 1095315, 78.9, 103.9, typeof(Log), 1044041, 35, 1044351);
        carpentry.AddSkill(index, SkillName.Musicianship, 45.0, 50.0);
        carpentry.AddRes(index, typeof(Granite), 1044514, 3, 1044513);
    }
}
