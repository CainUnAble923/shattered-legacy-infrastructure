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

// cc-P17 PT-08: ServUO's gargish cloth armour, DefTailoring.cs:709-725 (#region Cloth Armor, inside `if (Core.SA)`):
//
//     index = AddCraft(typeof(GargishClothArmsArmor), 1111748, 1021027, 87.1, 137.1, typeof(Cloth), 1044455, 8, 1044287);
//     ... Chest 1021029 94.0-144.0 x8, Legs 1021033 91.2-141.2 x10, Kilt 1021031 82.9-132.9 x6, then the four Female*
//     with the same clilocs, skills and cloth.
//
// Pinned has every one of the eight types, under its own names, and never registers them (it wears them only as
// creation clothes, CharacterCreation.cs:773-797). ServUO's names resolve to pinned's by TypeAlias: GargishCloth*Armor
// is GargishCloth*Type1 and FemaleGargishCloth*Armor is Type2 (GargishClothArmsType1.cs:6 and so on; the kilt Type2 has
// no alias, and ServUO's art 0x407 is pinned's Type2 art, GargishClothKiltType2.cs:9). The art matches ServUO's piece
// for piece (ServUO Items/Equipment/Armor/GargishClothArmor.cs:15-549). ServUO's ninth line, GargishClothWingArmor
// (1115393), is not here: pinned has no such type. Neither are its gargish garments (GargishRobe, GargishFancyRobe,
// GargishSash, GargoyleHalfApron, RobeofRite): no type in pinned or ours. notes/cc-P17-playtest-bugs-1.md, PT-08.
//
// Like ServUO, the male and female entries share a name (PT-10); no label is added.

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
    /// Appends ServUO's leather talons entry and its eight gargish cloth armour entries to DefTailoring.CraftSystem. Idempotent; the test host calls it directly
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

        foreach (var (type, name, min, max, cloth) in GargishClothArmour)
        {
            tailoring.AddCraft(type, 1111748, name, min, max, typeof(Cloth), 1044455, cloth, 1044287);
        }
    }

    // ServUO's order and values: type, name cliloc, minimum and maximum skill, yards of cloth.
    public static readonly (Type Type, int Name, double Min, double Max, int Cloth)[] GargishClothArmour =
    {
        (typeof(GargishClothArmsType1), 1021027, 87.1, 137.1, 8),
        (typeof(GargishClothChestType1), 1021029, 94.0, 144.0, 8),
        (typeof(GargishClothLegsType1), 1021033, 91.2, 141.2, 10),
        (typeof(GargishClothKiltType1), 1021031, 82.9, 132.9, 6),
        (typeof(GargishClothArmsType2), 1021027, 87.1, 137.1, 8),
        (typeof(GargishClothChestType2), 1021029, 94.0, 144.0, 8),
        (typeof(GargishClothLegsType2), 1021033, 91.2, 141.2, 10),
        (typeof(GargishClothKiltType2), 1021031, 82.9, 132.9, 6),
    };
}
