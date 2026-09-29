// ServUO: Services/Craft/DefBlacksmithy.cs, the Stygian Abyss entries whose types are ours and not pinned's
// (craft registrations task, 2026-09-29; shard-migration/notes/craft-registrations.md). Pinned's DefBlacksmithy has no
// gargish entry at all, so before this file gargish plate, the plate and gargish shields and seventeen gargish blades
// existed in our tree and no player could make one.
//
//     :340-357  the four gargish plate pieces, both genders, and GargishAmulet          if (Core.SA), group 1111704
//     :425-435  SmallPlateShield ... GargishOrderShield                                  if (Core.SA), group 1011080
//     :643-663  GargishKatana ... Shortblade                                             if (Core.SA), group 1011081
//     :702-704  GargishBattleAxe, GargishAxe                                             if (Core.SA), group 1011082
//     :752-760  GargishBardiche ... GargishLance                                         if (Core.SA), group 1011083
//     :824-828  GargishWarHammer, GargishMaul, GargishTessen                             if (Core.SA), group 1011084
//     :912-915  CrushedGlass, PowderedIron                                               if (Core.SA), group 1011173
//
// Every skill range, resource, amount and message is ServUO's, entry by entry. ServUO sets no SetNeededExpansion on any
// of them; the `if (Core.SA)` guards are the only expansion gate it has, and they are kept.
//
// Registered ADDITIVELY, as IncubatorCarpentryRecipe.cs is and for the reasons argued in
// shard-migration/notes/cc6-followup-breath-incubator.md section 4b: DefBlacksmithy.CraftSystem is a public static set
// by DefBlacksmithy.Initialize(), AddCraft/AddSkill/AddRes are public, and ServerStarted runs after Initialize. Each
// entry lands at the end of its group, in ServUO's order.
//
// One value decision toward the pinned table: the gargish plate and GargishAmulet go in 1011078 (Platemail), not
// ServUO's 1111704. Pinned has no 1111704 group; ServUO folded ring, chain and plate into it, and pinned keeps the
// older split and files ServUO's other 1111704 entries of the same block (the samurai plate, PlateMempo ...) under
// 1011078. Group 1011173, which pinned also lacks, is kept verbatim: pinned has no group those two belong in, so it
// appears as a new group at the end of the menu.
//
// Left out, both recipe-gated (SmithRecipes 355 and 356) and both needing BloodOfTheDarkFather, which neither tree has:
// BritchesOfWarding (:359) and GlovesOfFeudalGrip (:929). Also not here: ServUO's SA entries for pinned types that
// pinned's table leaves out (DreadSword, GargishTalwar, BloodBlade, DualShortAxes, DualPointedSpear, DiscMace ...),
// which are a decision about the stock table, as OrcMaskTailoringRecipe.cs says of the four masks.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class BlacksmithyCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's Stygian Abyss blacksmithy entries for our types to DefBlacksmithy.CraftSystem. Idempotent; the
    /// test host calls it directly because ServerStarted never fires there.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var smith = DefBlacksmithy.CraftSystem;

        if (smith == null)
        {
            Console.WriteLine("[BlacksmithyCraftRegistrations] WARNING: DefBlacksmithy.CraftSystem is null; gargish smithing is not craftable.");
            return;
        }

        _registered = true;

        if (!Core.SA)
        {
            return;
        }

        int index;

        // Metal armour (ServUO 1111704, pinned's Platemail 1011078; see the header).
        smith.AddCraft(typeof(FemaleGargishPlateArms), 1011078, 1095336, 66.3, 116.3, typeof(IronIngot), 1044036, 18, 1044037);
        smith.AddCraft(typeof(FemaleGargishPlateChest), 1011078, 1095338, 75.0, 125.0, typeof(IronIngot), 1044036, 25, 1044037);
        smith.AddCraft(typeof(FemaleGargishPlateLegs), 1011078, 1095342, 68.8, 118.8, typeof(IronIngot), 1044036, 20, 1044037);
        smith.AddCraft(typeof(FemaleGargishPlateKilt), 1011078, 1095340, 58.9, 108.9, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishPlateArms), 1011078, 1095336, 66.3, 116.3, typeof(IronIngot), 1044036, 18, 1044037);
        smith.AddCraft(typeof(GargishPlateChest), 1011078, 1095338, 75.0, 125.0, typeof(IronIngot), 1044036, 25, 1044037);
        smith.AddCraft(typeof(GargishPlateLegs), 1011078, 1095342, 68.8, 118.8, typeof(IronIngot), 1044036, 20, 1044037);
        smith.AddCraft(typeof(GargishPlateKilt), 1011078, 1095340, 58.9, 108.9, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishAmulet), 1011078, 1098595, 60.0, 110.0, typeof(IronIngot), 1044036, 3, 1044037);

        // Shields.
        smith.AddCraft(typeof(SmallPlateShield), 1011080, 1095770, -25.0, 25.0, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishKiteShield), 1011080, 1095774, 4.6, 54.6, typeof(IronIngot), 1044036, 16, 1044037);
        smith.AddCraft(typeof(LargePlateShield), 1011080, 1095772, 24.3, 74.3, typeof(IronIngot), 1044036, 18, 1044037);
        smith.AddCraft(typeof(MediumPlateShield), 1011080, 1095771, -10.2, 39.8, typeof(IronIngot), 1044036, 14, 1044037);
        smith.AddCraft(typeof(GargishChaosShield), 1011080, 1095808, 85.0, 135.0, typeof(IronIngot), 1044036, 25, 1044037);
        smith.AddCraft(typeof(GargishOrderShield), 1011080, 1095810, 85.0, 135.0, typeof(IronIngot), 1044036, 25, 1044037);

        // Bladed.
        smith.AddCraft(typeof(GargishKatana), 1011081, 1097490, 44.1, 94.1, typeof(IronIngot), 1044036, 8, 1044037);
        smith.AddCraft(typeof(GargishKryss), 1011081, 1097492, 36.7, 86.7, typeof(IronIngot), 1044036, 8, 1044037);
        smith.AddCraft(typeof(GargishBoneHarvester), 1011081, 1097502, 33.0, 83.0, typeof(IronIngot), 1044036, 10, 1044037);
        smith.AddCraft(typeof(GargishTekagi), 1011081, 1097510, 55.0, 105.0, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishDaisho), 1011081, 1097512, 60.0, 110.0, typeof(IronIngot), 1044036, 15, 1044037);
        smith.AddCraft(typeof(GargishDagger), 1011081, 1095362, 0.0, 100.0, typeof(IronIngot), 1044036, 3, 1044037);
        smith.AddCraft(typeof(Shortblade), 1011081, 1095374, 28.0, 100.0, typeof(IronIngot), 1044036, 12, 1044037);

        // Axes.
        smith.AddCraft(typeof(GargishBattleAxe), 1011082, 1097480, 30.5, 80.5, typeof(IronIngot), 1044036, 14, 1044037);
        smith.AddCraft(typeof(GargishAxe), 1011082, 1097482, 34.2, 84.2, typeof(IronIngot), 1044036, 14, 1044037);

        // Polearms.
        smith.AddCraft(typeof(GargishBardiche), 1011083, 1097484, 31.7, 81.7, typeof(IronIngot), 1044036, 18, 1044037);
        smith.AddCraft(typeof(GargishWarFork), 1011083, 1097494, 42.9, 92.9, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishScythe), 1011083, 1097500, 39.0, 89.0, typeof(IronIngot), 1044036, 14, 1044037);
        smith.AddCraft(typeof(GargishPike), 1011083, 1097504, 47.0, 97.0, typeof(IronIngot), 1044036, 12, 1044037);
        smith.AddCraft(typeof(GargishLance), 1011083, 1097506, 48.0, 98.0, typeof(IronIngot), 1044036, 20, 1044037);

        // Bashing.
        smith.AddCraft(typeof(GargishWarHammer), 1011084, 1097496, 34.2, 84.2, typeof(IronIngot), 1044036, 16, 1044037);
        smith.AddCraft(typeof(GargishMaul), 1011084, 1097498, 19.4, 69.4, typeof(IronIngot), 1044036, 10, 1044037);
        index = smith.AddCraft(typeof(GargishTessen), 1011084, 1097508, 85.0, 135.0, typeof(IronIngot), 1044036, 16, 1044037);
        smith.AddSkill(index, SkillName.Tailoring, 50.0, 55.0);
        smith.AddRes(index, typeof(Cloth), 1044286, 10, 1044287);

        // Miscellaneous (ServUO's 1011173, kept; see the header).
        index = smith.AddCraft(typeof(CrushedGlass), 1011173, 1113351, 110.0, 135.0, typeof(BlueDiamond), 1032696, 1, 1044253);
        smith.AddRes(index, typeof(GlassSword), 1095371, 5, 1044253);

        index = smith.AddCraft(typeof(PowderedIron), 1011173, 1113353, 110.0, 135.0, typeof(WhitePearl), 1026253, 1, 1044253);
        smith.AddRes(index, typeof(IronIngot), 1044036, 20, 1044037);
    }
}
