// cc-P128: ServUO pub57 Scripts/Services/Craft/DefCooking.cs, the entries whose types this port brought and whose
// resources exist in pinned or ours, appended to DefCooking.CraftSystem at ServerStarted (the craft-registrations shape):
//
//     :213-216  ParrotWafer                                    if (Core.ML), Ingredients 1044496
//     :222-232  PlantPigment, NaturalDye, ColorFixative        if (Core.SA), Ingredients 1044496
//     :399-410  EnchantedApple, GrapesOfWrath, FruitBowl       if (Core.ML), Magical Foods 1073108
//
// Every group, skill range, resource, amount and message is ServUO's. Pinned's cooking menu has no 1073108 group, so
// the three magical foods appear as a group after it under ServUO's name. ServUO's DefCooking.GetChanceAtMin gives the
// apple and the grapes a 50% chance at minimum skill where every other entry has 0%; pinned's DefCooking is a pinned
// file with no such hook, so they start at 0% here (noted).
//
// Left out: the three ChocolateNutcracker entries (:447-463) need SweetCocoaButter, in neither tree; FoodEngraver
// (:395) is not ported (the engraving tools, notes); KhaldunTastyTreat (:365) is in neither tree.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class CookingCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>Idempotent; the test host calls it directly because ServerStarted never fires there.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var cooking = DefCooking.CraftSystem;

        if (cooking == null)
        {
            Console.WriteLine("[CookingCraftRegistrations] WARNING: DefCooking.CraftSystem is null; the ML and SA foods are not craftable.");
            return;
        }

        _registered = true;

        int index;

        if (Core.ML)
        {
            // Ingredients.
            index = cooking.AddCraft(typeof(ParrotWafer), 1044496, 1032246, 37.5, 87.5, typeof(Dough), 1044469, 1, 1044253);
            cooking.AddRes(index, typeof(JarHoney), 1044472, 1, 1044253);
            cooking.AddRes(index, typeof(RawFishSteak), 1044476, 10, 1044253);
        }

        if (Core.SA)
        {
            index = cooking.AddCraft(typeof(PlantPigment), 1044496, 1112132, 75.0, 100.0, typeof(PlantClippings), 1112131, 1, 1044253);
            cooking.AddRes(index, typeof(Bottle), 1023854, 1, 1044253);

            index = cooking.AddCraft(typeof(NaturalDye), 1044496, 1112136, 65.0, 115.0, typeof(PlantPigment), 1112132, 1, 1044253);
            cooking.AddRes(index, typeof(ColorFixative), 1112135, 1, 1044253);

            index = cooking.AddCraft(typeof(ColorFixative), 1044496, 1112135, 75.0, 100.0, typeof(BaseBeverage), 1022503, 1, 1044253);
            cooking.AddRes(index, typeof(SilverSerpentVenom), 1112173, 1, 1044253);
            cooking.SetBeverageType(index, BeverageType.Wine);
        }

        if (Core.ML)
        {
            // Magical Foods.
            index = cooking.AddCraft(typeof(EnchantedApple), 1073108, 1072952, 60.0, 85.0, typeof(Apple), 1044479, 1, 1044253);
            cooking.AddRes(index, typeof(GreaterHealPotion), 1073467, 1, 1044253);
            cooking.ForceNonExceptional(index);

            index = cooking.AddCraft(typeof(GrapesOfWrath), 1073108, 1072953, 95.0, 120.0, typeof(Grapes), 1073468, 1, 1044253);
            cooking.AddRes(index, typeof(GreaterStrengthPotion), 1073466, 1, 1044253);
            cooking.ForceNonExceptional(index);

            index = cooking.AddCraft(typeof(FruitBowl), 1073108, 1072950, 55.0, 105.0, typeof(EmptyWoodenBowl), 1073472, 1, 1044253);
            cooking.AddRes(index, typeof(Pear), 1044481, 3, 1044253);
            cooking.AddRes(index, typeof(Apple), 1044479, 3, 1044253);
            cooking.AddRes(index, typeof(Banana), 1073470, 3, 1044253);
        }
    }
}
