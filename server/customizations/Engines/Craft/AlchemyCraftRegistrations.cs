// cc-P128: ServUO pub57 Scripts/Services/Craft/DefAlchemy.cs, the entries whose types this port brought and whose
// resources exist in pinned or ours, appended to DefAlchemy.CraftSystem at ServerStarted (the craft-registrations
// shape, shard-migration/notes/craft-registrations.md):
//
//     :147-149  ElixirOfRebirth                                   if (Core.SA),  Healing and Curative 1116348
//     :154-158  BarrabHemolymphConcentrate (recipe 900)           if (Core.TOL), Healing and Curative 1116348
//     :187-217  Jukari 901, Barako 903, Urali 904, Sakkhra 905    if (Core.TOL), Enhancement 1116349
//     :311-314  NaturalDye                                        if (Core.SA),  Miscellaneous 1116353
//     :327-346  PlantPigment, ColorFixative, CrystalGranules, SoftenedReeds   if (Core.SA), Ingredients 1044495
//
// Every group, skill range, resource, amount, message and recipe id is ServUO's, entry by entry. Pinned's alchemy menu
// has none of ServUO's four groups (it keeps the older Refresh ... Explosion split), so these appear as four groups
// after it, under ServUO's own names (the craft-registrations precedent for 1011173).
//
// Left out: KurakAmbushersEssence (:193-197) needs TigerPelt, in neither tree (Q-066). ServUO's SetRequireResTarget and
// SetItemHue have no pinned equivalent: the pigment and the reeds colour themselves in OnCraft from the resource consumed
// (MiscSAResources.cs header). SetRequiresBasketWeaving on SoftenedReeds is ClusterFRecipeGate.RequiredKeys (the
// basket-weaving book, BasketWeavingBook.cs).

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class AlchemyCraftRegistrations
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

        var alchemy = DefAlchemy.CraftSystem;

        if (alchemy == null)
        {
            Console.WriteLine("[AlchemyCraftRegistrations] WARNING: DefAlchemy.CraftSystem is null; the SA and TOL potions are not craftable.");
            return;
        }

        _registered = true;

        int index;

        if (Core.SA)
        {
            // Healing and Curative.
            index = alchemy.AddCraft(typeof(ElixirOfRebirth), 1116348, 1112762, 65.0, 115.0, typeof(MedusaBlood), 1031702, 1, 1044253);
            alchemy.AddRes(index, typeof(SpidersSilk), 1044360, 3, 1044368);
            alchemy.AddRes(index, typeof(Bottle), 1044529, 1, 500315);
        }

        if (Core.TOL)
        {
            index = alchemy.AddCraft(typeof(BarrabHemolymphConcentrate), 1116348, 1156724, 51.0, 151.0, typeof(Bottle), 1044529, 1, 500315);
            alchemy.AddRes(index, typeof(Ginseng), 1044356, 20, 1044364);
            alchemy.AddRes(index, typeof(PlantClippings), 1112131, 5, 1044253);
            alchemy.AddRes(index, typeof(MyrmidexEggsac), 1156725, 5, 1044253);
            alchemy.AddRecipe(index, (int)AlchemyRecipes.BarrabHemolymphConcentrate);

            // Enhancement.
            index = alchemy.AddCraft(typeof(JukariBurnPoiltice), 1116349, 1156726, 51.0, 151.0, typeof(Bottle), 1044529, 1, 500315);
            alchemy.AddRes(index, typeof(BlackPearl), 1044353, 20, 1044361);
            alchemy.AddRes(index, typeof(Vanilla), 1080000, 10, 1080008);
            alchemy.AddRes(index, typeof(LavaBerry), 1156727, 5, 1044253);
            alchemy.AddRecipe(index, (int)AlchemyRecipes.JukariBurnPoiltice);

            index = alchemy.AddCraft(typeof(BarakoDraftOfMight), 1116349, 1156729, 51.0, 151.0, typeof(Bottle), 1044529, 1, 500315);
            alchemy.AddRes(index, typeof(SpidersSilk), 1044360, 20, 1044368);
            alchemy.AddRes(index, typeof(BaseBeverage), 1022459, 10, 1044253);
            alchemy.AddRes(index, typeof(PerfectBanana), 1156730, 5, 1044253);
            alchemy.SetBeverageType(index, BeverageType.Liquor);
            alchemy.AddRecipe(index, (int)AlchemyRecipes.BarakoDraftOfMight);

            index = alchemy.AddCraft(typeof(UraliTranceTonic), 1116349, 1156734, 51.0, 151.0, typeof(Bottle), 1044529, 1, 500315);
            alchemy.AddRes(index, typeof(MandrakeRoot), 1044357, 20, 1044365);
            alchemy.AddRes(index, typeof(YellowScales), 1156799, 10, 1044253);
            alchemy.AddRes(index, typeof(RiverMoss), 1156731, 5, 1044253);
            alchemy.AddRecipe(index, (int)AlchemyRecipes.UraliTranceTonic);

            index = alchemy.AddCraft(typeof(SakkhraProphylaxisPotion), 1116349, 1156732, 51.0, 151.0, typeof(Bottle), 1044529, 1, 500315);
            alchemy.AddRes(index, typeof(Nightshade), 1044358, 20, 1044366);
            alchemy.AddRes(index, typeof(BaseBeverage), 1022503, 10, 1044253);
            alchemy.AddRes(index, typeof(BlueCorn), 1156733, 5, 1044253);
            alchemy.SetBeverageType(index, BeverageType.Wine);
            alchemy.AddRecipe(index, (int)AlchemyRecipes.SakkhraProphylaxisPotion);
        }

        if (Core.SA)
        {
            // Miscellaneous.
            index = alchemy.AddCraft(typeof(NaturalDye), 1116353, 1112136, 75.0, 100.0, typeof(PlantPigment), 1112132, 1, 1044253);
            alchemy.AddRes(index, typeof(ColorFixative), 1112135, 1, 1044253);

            // Ingredients.
            index = alchemy.AddCraft(typeof(PlantPigment), 1044495, 1112132, 33.0, 83.0, typeof(PlantClippings), 1112131, 1, 1044253);
            alchemy.AddRes(index, typeof(Bottle), 1023854, 1, 1044253);

            index = alchemy.AddCraft(typeof(ColorFixative), 1044495, 1112135, 75.0, 100.0, typeof(SilverSerpentVenom), 1112173, 1, 1044253);
            alchemy.AddRes(index, typeof(BaseBeverage), 1022503, 1, 1044253);
            alchemy.SetBeverageType(index, BeverageType.Wine);

            alchemy.AddCraft(typeof(CrystalGranules), 1044495, 1112329, 75.0, 100.0, typeof(ShimmeringCrystals), 1075095, 1, 1044253);

            index = alchemy.AddCraft(typeof(SoftenedReeds), 1044495, 1112249, 75.0, 100.0, typeof(DryReeds), 1112248, 1, 1112250);
            alchemy.AddRes(index, typeof(ScouringToxin), 1112292, 2, 1112326);
        }
    }
}

/// <summary>ServUO Services/Craft/DefAlchemy.cs:7-14, the recipe ids of the Eodon potions.</summary>
public enum AlchemyRecipes
{
    BarrabHemolymphConcentrate = 900,
    JukariBurnPoiltice = 901,
    KurakAmbushersEssence = 902,
    BarakoDraftOfMight = 903,
    UraliTranceTonic = 904,
    SakkhraProphylaxisPotion = 905
}
