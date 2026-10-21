// ServUO: the Craftsman path to the Exodus ritual (cc-P195): "From the hands of the skilled artisan all of these Keys
// may be built by means of Tailoring, Alchemy, Carpentry, and Smithy" (ExodusArchZealotGump 1153689). ServUO's six
// entries, verbatim, each under its Core.SA guard, appended to pinned's craft systems at ServerStarted (the
// craft-registrations shape of CarpentryCraftRegistrations.cs):
//
//   DefBlacksmithy.cs:923-927   ExodusSacrificalDagger   Blacksmithy 95-120: 12 iron ingots, 2 blue diamonds, 2 fire rubies,
//                                                        10 blackrock; never exceptional
//   DefCarpentry.cs:233-237     ExodusSummoningAlter     Carpentry 95-120 (Magery 75-120): 100 boards, 10 granite,
//                                                        10 blackrock, 1 nexus core
//   DefInscription.cs:426-429   ExodusSummoningRite      Inscription 95-120: 5 daemon blood, 1 taint, 5 daemon bone,
//                                                        1 Summon Daemon scroll
//   DefTailoring.cs:365-369     RobeofRite               Tailoring 101.5-120: 6 leather, 1 fire ruby, 5 gold dust,
//                                                        6 abyssal cloth; never exceptional
//   DefAlchemy.cs:316-320       NexusCore                Alchemy 90-120: 10 mandrake root, 10 spiders' silk,
//                                                        5 dark sapphires, 5 crushed glass; never exceptional
//   DefAlchemy.cs:373-374       GoldDust                 Alchemy 90-120: 1,000 gold; never exceptional
//
// typeof(Log) with 1044041 ("Boards or Logs") stands for ServUO's typeof(Board), as every wood entry in pinned
// DefCarpentry is (CraftItem's type table makes the two one at consumption). Where the shard's rules meet this:
// every entry is a craft item under the shard's recipe gate (cc-P78: band by minimum skill, hidden until its book or
// scroll is known), so the Craftsman path is a recipe path here; nothing is exempted. Abyssal cloth has no source on
// the shard yet (AbyssalCloth.cs), so the robe's entry waits on one.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class ExodusCraftRegistrations
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

        var smithy = DefBlacksmithy.CraftSystem;
        var carpentry = DefCarpentry.CraftSystem;
        var inscription = DefInscription.CraftSystem;
        var tailoring = DefTailoring.CraftSystem;
        var alchemy = DefAlchemy.CraftSystem;

        if (smithy == null || carpentry == null || inscription == null || tailoring == null || alchemy == null)
        {
            Console.WriteLine("[ExodusCraftRegistrations] WARNING: a craft system is null; the Exodus ritual's keys are not craftable.");
            return;
        }

        _registered = true;

        if (!Core.SA)
        {
            return;
        }

        var index = smithy.AddCraft(typeof(ExodusSacrificalDagger), 1011173, 1153500, 95.0, 120.0, typeof(IronIngot), 1044036, 12, 1044253);
        smithy.AddRes(index, typeof(BlueDiamond), 1032696, 2, 1044253);
        smithy.AddRes(index, typeof(FireRuby), 1032695, 2, 1044253);
        smithy.AddRes(index, typeof(SmallPieceofBlackrock), 1150016, 10, 1044253);
        smithy.ForceNonExceptional(index);

        index = carpentry.AddCraft(typeof(ExodusSummoningAlter), 1044294, 1153502, 95.0, 120.0, typeof(Log), 1044041, 100, 1044351);
        carpentry.AddSkill(index, SkillName.Magery, 75.0, 120.0);
        carpentry.AddRes(index, typeof(Granite), 1044607, 10, 1044253);
        carpentry.AddRes(index, typeof(SmallPieceofBlackrock), 1150016, 10, 1044253);
        carpentry.AddRes(index, typeof(NexusCore), 1153501, 1, 1044253);

        index = inscription.AddCraft(typeof(ExodusSummoningRite), 1044294, 1153498, 95.0, 120.0, typeof(DaemonBlood), 1023965, 5, 1044253);
        inscription.AddRes(index, typeof(Taint), 1032679, 1, 1044253);
        inscription.AddRes(index, typeof(DaemonBone), 1017412, 5, 1044253);
        inscription.AddRes(index, typeof(SummonDaemonScroll), 1016017, 1, 1044253);

        index = tailoring.AddCraft(typeof(RobeofRite), 1111747, 1153510, 101.5, 120.0, typeof(Leather), 1044462, 6, 1044253);
        tailoring.AddRes(index, typeof(FireRuby), 1032695, 1, 1044253);
        tailoring.AddRes(index, typeof(GoldDust), 1098337, 5, 1044253);
        tailoring.AddRes(index, typeof(AbyssalCloth), 1113350, 6, 1044253);
        tailoring.ForceNonExceptional(index);

        index = alchemy.AddCraft(typeof(NexusCore), 1116353, 1153501, 90.0, 120.0, typeof(MandrakeRoot), 1015013, 10, 1044253);
        alchemy.AddRes(index, typeof(SpidersSilk), 1015007, 10, 1044253);
        alchemy.AddRes(index, typeof(DarkSapphire), 1032690, 5, 1044253);
        alchemy.AddRes(index, typeof(CrushedGlass), 1113351, 5, 1044253);
        alchemy.ForceNonExceptional(index);

        index = alchemy.AddCraft(typeof(GoldDust), 1044495, 1153504, 90.0, 120.0, typeof(Gold), 3000083, 1000, 1150747);
        alchemy.ForceNonExceptional(index);
    }
}
