// cc-P128: ServUO pub57 Scripts/Services/Craft/DefCooking.cs, the entries whose types this port brought and whose
// resources exist in pinned or ours, appended to DefCooking.CraftSystem at ServerStarted (the craft-registrations shape):
//
//     :213-216  ParrotWafer                                    if (Core.ML), Ingredients 1044496
//     :222-232  PlantPigment, NaturalDye, ColorFixative        if (Core.SA), Ingredients 1044496
//     :399-410  EnchantedApple, GrapesOfWrath, FruitBowl       if (Core.ML), Magical Foods 1073108
//     :469-565  the sixteen fish pies (cc-P269)               if (Core.SA), Fish Pies 1116340, at an oven
//
// Every group, skill range, resource, amount and message is ServUO's. Pinned's cooking menu has no 1073108 group, so
// the three magical foods appear as a group after it under ServUO's name. ServUO's DefCooking.GetChanceAtMin gives the
// apple and the grapes a 50% chance at minimum skill where every other entry has 0%; pinned's DefCooking is a pinned
// file with no such hook, so they start at 0% here (noted).
//
// cc-P269: the fish pies are Cooking band II (minimum 55 or 61), so recipe gating (F-43) shows them to an account that
// knows the Cooking band II book, like every other band II dish. Mento Seasoning, Samuel's Secret Sauce and Dark
// Truffle are sold only by the Sea Market's tavern keeper in ServUO, and Fresh Ginger by bakers and farmers there; no
// vendor here stocks them yet (shard-migration notes/cc-P269-high-seas-fishing-second-half.md).
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

        if (Core.SA)
        {
            // Fish Pies (cc-P269).
            index = cooking.AddCraft(typeof(GreatBarracudaPie), 1116340, 1116214, 61.0, 110.0, typeof(GreatBarracudaSteak), 1116298, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.AddRes(index, typeof(ZoogiFungus), 1029911, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(GiantKoiPie), 1116340, 1116216, 61.0, 110.0, typeof(GiantKoiSteak), 1044253, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.AddRes(index, typeof(WoodenBowlOfPeas), 1025628, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(FireFishPie), 1116340, 1116217, 55.0, 105.0, typeof(FireFishSteak), 1116307, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Carrot), 1023191, 1, 1044253);
            cooking.AddRes(index, typeof(SamuelsSecretSauce), 1116338, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(StoneCrabPie), 1116340, 1116227, 55.0, 105.0, typeof(StoneCrabMeat), 1116317, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Cabbage), 1023195, 1, 1044253);
            cooking.AddRes(index, typeof(SamuelsSecretSauce), 1116338, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(BlueLobsterPie), 1116340, 1116228, 55.0, 105.0, typeof(BlueLobsterMeat), 1116318, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(TribalBerry), 1040001, 1, 1044253);
            cooking.AddRes(index, typeof(SamuelsSecretSauce), 1116338, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(ReaperFishPie), 1116340, 1116218, 55.0, 105.0, typeof(ReaperFishSteak), 1116308, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Pumpkin), 1023178, 1, 1044253);
            cooking.AddRes(index, typeof(SamuelsSecretSauce), 1116338, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(CrystalFishPie), 1116340, 1116219, 55.0, 105.0, typeof(CrystalFishSteak), 1116309, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Apple), 1022512, 1, 1044253);
            cooking.AddRes(index, typeof(SamuelsSecretSauce), 1116338, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(BullFishPie), 1116340, 1116220, 55.0, 105.0, typeof(BullFishSteak), 1116310, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Squash), 1023186, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(SummerDragonfishPie), 1116340, 1116221, 55.0, 105.0, typeof(SummerDragonfishSteak), 1116311, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Onion), 1023182, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(FairySalmonPie), 1116340, 1116222, 55.0, 105.0, typeof(FairySalmonSteak), 1116312, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(EarOfCorn), 1023199, 1, 1044253);
            cooking.AddRes(index, typeof(DarkTruffle), 1116300, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(LavaFishPie), 1116340, 1116223, 55.0, 105.0, typeof(LavaFishSteak), 1116313, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(CheeseWheel), 1044486, 1, 1044253);
            cooking.AddRes(index, typeof(DarkTruffle), 1116300, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(AutumnDragonfishPie), 1116340, 1116224, 55.0, 105.0, typeof(AutumnDragonfishSteak), 1116314, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Pear), 1022452, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(SpiderCrabPie), 1116340, 1116229, 55.0, 105.0, typeof(SpiderCrabMeat), 1116320, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(Lettuce), 1023184, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(YellowtailBarracudaPie), 1116340, 1116098, 55.0, 105.0, typeof(YellowtailBarracudaSteak), 1116301, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(BaseBeverage), 1022503, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(HolyMackerelPie), 1116340, 1116225, 55.0, 105.0, typeof(HolyMackerelSteak), 1116315, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(JarHoney), 1022540, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);

            index = cooking.AddCraft(typeof(UnicornFishPie), 1116340, 1116226, 55.0, 105.0, typeof(UnicornFishSteak), 1116316, 1, 1044253);
            cooking.AddRes(index, typeof(Dough), 1024157, 1, 1044253);
            cooking.AddRes(index, typeof(FreshGinger), 1031235, 1, 1044253);
            cooking.AddRes(index, typeof(MentoSeasoning), 1116299, 1, 1044253);
            cooking.SetNeedOven(index, true);
        }
    }
}
