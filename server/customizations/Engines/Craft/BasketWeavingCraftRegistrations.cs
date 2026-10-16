// cc-P128: ServUO pub57 Scripts/Services/Craft/DefTinkering.cs, the basket weaving and the three other entries this
// port brought, appended to DefTinkering.CraftSystem at ServerStarted beside TinkeringCraftRegistrations (the
// craft-registrations shape):
//
//     :259-317  SoftenedReeds and the eleven baskets       if (Core.SA), Wooden Items 1044042, Tinkering 75 to 100
//     :351      Clippers                                   Tools 1044046, Tinkering 50
//     :602-605  HitchingRope                               if (Core.ML), Assemblies 1044051, with Animal Lore 15
//
// Every group, skill range, resource, amount and message is ServUO's; the three groups exist in pinned's table.
// ServUO's SetRequiresBasketWeaving (the basket-weaving book, refused at craft time with 1112253) is
// ClusterFRecipeGate.RequiredKeys here: the same key the book teaches, checked where the shard's recipe gate checks
// everything else, which also hides the entries until the book is read (ServUO shows them and refuses the craft;
// recorded in the notes). SetRequireResTarget (choose which reed stack to use) has no pinned equivalent: pinned consumes
// the first stack of the type and the reeds colour themselves from it (MiscSAResources.cs header).
//
// Left out: EnchantedPicnicBasket (:319-324) is in neither tree; HitchingPost (:607-610) likewise, so the rope is
// craftable and the post is not.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class BasketWeavingCraftRegistrations
{
    private static bool _registered;

    /// <summary>The types ServUO gates on the basket-weaving book (SetRequiresBasketWeaving), in both tables.</summary>
    public static readonly Type[] BookGated =
    {
        typeof(SoftenedReeds), typeof(RoundBasket), typeof(RoundBasketHandles), typeof(SmallBushel), typeof(PicnicBasket2),
        typeof(WinnowingBasket), typeof(SquareBasket), typeof(BasketCraftable), typeof(TallRoundBasket),
        typeof(SmallSquareBasket), typeof(TallBasket), typeof(SmallRoundBasket)
    };

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

        var tinkering = DefTinkering.CraftSystem;

        if (tinkering == null)
        {
            Console.WriteLine("[BasketWeavingCraftRegistrations] WARNING: DefTinkering.CraftSystem is null; baskets are not craftable.");
            return;
        }

        _registered = true;

        foreach (var type in BookGated)
        {
            ClusterFRecipeGate.RequiredKeys[type] = BasketWeaving.Flag;
        }

        int index;

        if (Core.SA)
        {
            index = tinkering.AddCraft(typeof(SoftenedReeds), 1044042, 1112249, 75.0, 100.0, typeof(DryReeds), 1112248, 1, 1112250);
            tinkering.AddRes(index, typeof(ScouringToxin), 1112292, 2, 1112326);

            Basket(tinkering, typeof(RoundBasket), 1112293, 2, 3);
            Basket(tinkering, typeof(RoundBasketHandles), 1112357, 2, 3);
            Basket(tinkering, typeof(SmallBushel), 1112337, 1, 2);
            Basket(tinkering, typeof(PicnicBasket2), 1023706, 1, 2);
            Basket(tinkering, typeof(WinnowingBasket), 1026274, 2, 3);
            Basket(tinkering, typeof(SquareBasket), 1112295, 2, 3);
            Basket(tinkering, typeof(BasketCraftable), 1022448, 2, 3);
            Basket(tinkering, typeof(TallRoundBasket), 1112297, 3, 4);
            Basket(tinkering, typeof(SmallSquareBasket), 1112296, 1, 2);
            Basket(tinkering, typeof(TallBasket), 1112299, 3, 4);
            Basket(tinkering, typeof(SmallRoundBasket), 1112298, 1, 2);
        }

        tinkering.AddCraft(typeof(Clippers), 1044046, 1112117, 50.0, 50.0, typeof(IronIngot), 1044036, 4, 1044037);

        if (Core.ML)
        {
            index = tinkering.AddCraft(typeof(HitchingRope), 1044051, 1071124, 60.0, 120.0, typeof(Rope), 1020934, 1, 1044253);
            tinkering.AddSkill(index, SkillName.AnimalLore, 15.0, 100.0);
            tinkering.AddRes(index, typeof(ResolvesBridle), 1074761, 1, 1044253);
        }
    }

    private static void Basket(CraftSystem tinkering, Type type, int name, int reeds, int shafts)
    {
        var index = tinkering.AddCraft(type, 1044042, name, 75.0, 100.0, typeof(SoftenedReeds), 1112249, reeds, 1112251);
        tinkering.AddRes(index, typeof(Shaft), 1027125, shafts, 1044351);
    }
}
