// ServUO: Services/Craft/DefTinkering.cs, the entries whose types are ours and not pinned's and whose resources a
// player can get (craft registrations task, 2026-09-29; shard-migration/notes/craft-registrations.md):
//
//     :209-215  GargishNecklace, GargishBracelet, GargishRing, GargishEarrings       if (Core.SA), Jewelry 1044049
//     :399-401  GargishCleaver, GargishButcherKnife                                 if (Core.SA), Utensils 1044048
//     :619-623  VoidOrb                                                             if (Core.SA), Assemblies 1044051
//     :706-736  the eight ML gem jewels, BrilliantAmberBracelet ... TurqouiseRing   if (Core.ML), Magic Jewelry 1073107
//
// Every group, skill range, resource, amount and message is ServUO's, entry by entry, and every group already exists in
// pinned's table. ServUO sets no SetNeededExpansion on any of them; its `if (Core.X)` guards are kept.
//
// Registered ADDITIVELY, as IncubatorCarpentryRecipe.cs is (shard-migration/notes/cc6-followup-breath-incubator.md
// section 4b). DefTinkering.CraftSystem is the same kind of public static, set by DefTinkering.Initialize().
//
// Left out:
//   - ArcanicRuneStone (:614). Its first resource is CrystalShards, which is ours and which no player can get: ServUO's
//     only source is a Ter Mur lumberjacking bonus (Services/Harvest/Lumberjacking.cs:111) that pinned lacks.
//   - BraceletOfPrimalConsumption (:773). Recipe-gated (TinkerRecipes) and needs BloodOfTheDarkFather (ported by
//     cc-P199).

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class TinkeringCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>
    /// Appends ServUO's gargish and Mondain's Legacy tinkering entries for our types to DefTinkering.CraftSystem.
    /// Idempotent; the test host calls it directly because ServerStarted never fires there.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var tinkering = DefTinkering.CraftSystem;

        if (tinkering == null)
        {
            Console.WriteLine("[TinkeringCraftRegistrations] WARNING: DefTinkering.CraftSystem is null; gargish jewellery is not craftable.");
            return;
        }

        _registered = true;

        int index;

        if (Core.SA)
        {
            // Jewelry.
            tinkering.AddCraft(typeof(GargishNecklace), 1044049, 1095784, 60.0, 110.0, typeof(IronIngot), 1044036, 3, 1044037);
            tinkering.AddCraft(typeof(GargishBracelet), 1044049, 1095785, 55.0, 105.0, typeof(IronIngot), 1044036, 3, 1044037);
            tinkering.AddCraft(typeof(GargishRing), 1044049, 1095786, 65.0, 115.0, typeof(IronIngot), 1044036, 3, 1044037);
            tinkering.AddCraft(typeof(GargishEarrings), 1044049, 1095787, 55.0, 105.0, typeof(IronIngot), 1044036, 3, 1044037);

            // Utensils.
            tinkering.AddCraft(typeof(GargishCleaver), 1044048, 1097478, 20.0, 70.0, typeof(IronIngot), 1044036, 3, 1044037);
            tinkering.AddCraft(typeof(GargishButcherKnife), 1044048, 1097486, 25.0, 75.0, typeof(IronIngot), 1044036, 2, 1044037);

            // Assemblies.
            index = tinkering.AddCraft(typeof(VoidOrb), 1044051, 1113354, 90.0, 104.3, typeof(DarkSapphire), 1032690, 1, 1044253);
            tinkering.AddSkill(index, SkillName.Magery, 80.0, 100.0);
            tinkering.AddRes(index, typeof(BlackPearl), 1015001, 50, 1044253);
            tinkering.ForceNonExceptional(index);
        }

        if (Core.ML)
        {
            // Magic Jewelry.
            index = tinkering.AddCraft(typeof(BrilliantAmberBracelet), 1073107, 1073453, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Amber), 1062607, 20, 1044240);
            tinkering.AddRes(index, typeof(BrilliantAmber), 1032697, 10, 1044240);

            index = tinkering.AddCraft(typeof(FireRubyBracelet), 1073107, 1073454, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Ruby), 1062603, 20, 1044240);
            tinkering.AddRes(index, typeof(FireRuby), 1032695, 10, 1044240);

            index = tinkering.AddCraft(typeof(DarkSapphireBracelet), 1073107, 1073455, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Sapphire), 1062602, 20, 1044240);
            tinkering.AddRes(index, typeof(DarkSapphire), 1032690, 10, 1044240);

            index = tinkering.AddCraft(typeof(WhitePearlBracelet), 1073107, 1073456, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Tourmaline), 1062606, 20, 1044240);
            tinkering.AddRes(index, typeof(WhitePearl), 1032694, 10, 1044240);

            index = tinkering.AddCraft(typeof(EcruCitrineRing), 1073107, 1073457, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Citrine), 1062604, 20, 1044240);
            tinkering.AddRes(index, typeof(EcruCitrine), 1032693, 10, 1044240);

            index = tinkering.AddCraft(typeof(BlueDiamondRing), 1073107, 1073458, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Diamond), 1062608, 20, 1044240);
            tinkering.AddRes(index, typeof(BlueDiamond), 1032696, 10, 1044240);

            index = tinkering.AddCraft(typeof(PerfectEmeraldRing), 1073107, 1073459, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Emerald), 1062601, 20, 1044240);
            tinkering.AddRes(index, typeof(PerfectEmerald), 1032692, 10, 1044240);

            index = tinkering.AddCraft(typeof(TurqouiseRing), 1073107, 1073460, 75.0, 125.0, typeof(IronIngot), 1044036, 5, 1044037);
            tinkering.AddRes(index, typeof(Amethyst), 1062605, 20, 1044240);
            tinkering.AddRes(index, typeof(Turquoise), 1032691, 10, 1044240);
        }

        RegisterDeployableTraps(tinkering);
    }

    /// <summary>
    /// cc-P275: the eight deployable trap kits (shard-migration notes/cc-P201-deployable-traps-design.md section 3), ours,
    /// in pinned's Traps group beside OSI's container traps, which stay as they are. Each gate is a setting
    /// (clusterf.traps.&lt;trap&gt;.tinkering, read at registration) and gains to 50 above it, but never past StockTop:
    /// the shard's craft ceilings stop every iron recipe at 130, and the extended metals teach above it
    /// (CraftCeilingsVerification K1). So the blast kit gains 90-130 and the shackle 110-130. The gas and blast potions
    /// and the darts are loaded into the finished kit, not crafted into it (DeployableTrapKit.cs).
    /// </summary>
    public const double StockTop = 130.0;

    public static void RegisterDeployableTraps(CraftSystem tinkering)
    {
        const int traps = 1044052; // Traps
        const int lacking = 1044253; // You don't have the components needed to make that.

        int Kit(Type type, DeployableTrapKind kind, Type firstRes, TextDefinition firstName, int amount)
        {
            var min = DeployableTraps.TinkeringMin(kind);
            return tinkering.AddCraft(type, traps, $"{DeployableTraps.NameOf(kind)} kit", min, Math.Min(min + 50, StockTop), firstRes, firstName, amount, lacking);
        }

        var index = Kit(typeof(SnareNetKit), DeployableTrapKind.SnareNet, typeof(Cloth), 1044286, 10);
        tinkering.AddRes(index, typeof(SpidersSilk), 1044360, 2, lacking);

        index = Kit(typeof(CaltropsKit), DeployableTrapKind.Caltrops, typeof(IronIngot), 1044036, 10);
        tinkering.AddRes(index, typeof(Springs), 1044171, 1, lacking);

        index = Kit(typeof(GasTrapKit), DeployableTrapKind.Gas, typeof(IronIngot), 1044036, 5);
        tinkering.AddRes(index, typeof(Gears), 1044254, 1, lacking);

        index = Kit(typeof(BlastTrapKit), DeployableTrapKind.Blast, typeof(IronIngot), 1044036, 5);
        tinkering.AddRes(index, typeof(Gears), 1044254, 1, lacking);

        index = Kit(typeof(DartTrapKit), DeployableTrapKind.Dart, typeof(IronIngot), 1044036, 5);
        tinkering.AddRes(index, typeof(Springs), 1044171, 1, lacking);
        tinkering.AddRes(index, typeof(Bolt), 1044570, 1, lacking);

        index = Kit(typeof(BearTrapKit), DeployableTrapKind.BearTrap, typeof(IronIngot), 1044036, 10);
        tinkering.AddRes(index, typeof(Springs), 1044171, 2, lacking);
        tinkering.AddRes(index, typeof(Gears), 1044254, 1, lacking);

        index = Kit(typeof(AlarmTrapKit), DeployableTrapKind.Alarm, typeof(IronIngot), 1044036, 4);
        tinkering.AddRes(index, typeof(Gears), 1044254, 1, lacking);
        tinkering.AddRes(index, typeof(Springs), 1044171, 1, lacking);

        index = Kit(typeof(ShackleTrapKit), DeployableTrapKind.Shackle, typeof(IronIngot), 1044036, 8);
        tinkering.AddRes(index, typeof(Gears), 1044254, 2, lacking);
        tinkering.AddRes(index, typeof(ParalyzeScroll), "Paralyze scroll", 1, lacking);
    }
}
