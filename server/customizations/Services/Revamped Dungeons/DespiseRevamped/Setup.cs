// ServUO: Services/Revamped Dungeons/DespiseRevamped/Setup.cs (CC4 Despise).
//
// [SetupDespise (GameMaster) places the controller, the two ankhs, the six gate teleporters and converts the
// stock teleporters at the dungeon mouth into DespiseTeleporters. [DeleteDespise removes everything it placed.
//
// ServUO's Read Me has the GM run [XmlLoad spawns/despiserevamped.xml FIRST and [SetupDespise second, because
// the controller finds its spawners by name rather than creating them. ModernUO has neither XmlSpawner nor
// XmlLoad, so when no Despise spawners exist yet, [SetupDespise generates the same 47 from
// DespiseSpawns.Definitions before creating the controller. One command instead of two; the objects are the same.
//
// Not placed (D-30): the MysteriousWisp at (1303, 1088, 0) Trammel outside the entrance. It is the Despise
// crystal-point vendor and the Whispering With Wisps quest giver, and rests on Services/PointsSystems (S7),
// RunicReforging.GenerateRandomItem and the Town Cryer quest system, none of which are on this shard.
//
// ServUO ends the command with DespiseController.Instance.CheckSpawnersVersion3(), a save-upgrade hook that
// also deleted and re-created the six gates it had just placed. Dropped with the rest of that hook.

using System.Collections.Generic;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Despise;

public static class DespiseRevampedSetup
{
    public const string CollectionKey = "despise";

    public static void Initialize()
    {
        CommandSystem.Register("SetupDespise", AccessLevel.GameMaster, SetupDespise_OnCommand);
        CommandSystem.Register("DeleteDespise", AccessLevel.GameMaster, DeleteDespise_OnCommand);
    }

    private static void DeleteDespise_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteDespise();
        e.Mobile.SendMessage($"Despise removed ({deleted} objects).");
    }

    public static int DeleteDespise()
    {
        var deleted = WeakEntityCollection.Delete(CollectionKey);
        DespiseController.Instance = null;
        return deleted;
    }

    public static void SetupDespise_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(SetupDespise());
    }

    /// <summary>
    ///     ServUO's SetupDespise_OnCommand body. Returns the message the command sends.
    /// </summary>
    public static string SetupDespise()
    {
        if (DespiseController.Instance != null)
        {
            return "Despise appears to already be setup";
        }

        DespiseController.RemoveAnkh();

        if (!DespiseSpawns.AnyPresent())
        {
            DespiseSpawns.Generate();
        }

        var controller = new DespiseController();
        WeakEntityCollection.Add(CollectionKey, controller);
        controller.MoveToWorld(new Point3D(5571, 626, 30), Map.Trammel);

        var ankh = new DespiseAnkh(Alignment.Good);
        WeakEntityCollection.Add(CollectionKey, ankh);
        ankh.MoveToWorld(new Point3D(5474, 525, 79), Map.Trammel);

        ankh = new DespiseAnkh(Alignment.Evil);
        WeakEntityCollection.Add(CollectionKey, ankh);
        ankh.MoveToWorld(new Point3D(5472, 754, 10), Map.Trammel);

        SetupTeleporters();

        // Wisp: ServUO places a MysteriousWisp at (1303, 1088, 0). Not ported, D-30.

        // Teleporters at the dungeon mouth become DespiseTeleporters, so a possessed creature stays behind.
        var old = new List<Teleporter>();

        foreach (var item in Map.Trammel.GetItemsInRange(new Point3D(5588, 631, 30), 2))
        {
            if (item is Teleporter teleporter)
            {
                old.Add(teleporter);
            }
        }

        foreach (var teleporter in old)
        {
            var tele = new DespiseTeleporter
            {
                PointDest = teleporter.PointDest,
                MapDest = teleporter.MapDest
            };

            WeakEntityCollection.Add(CollectionKey, tele);
            tele.MoveToWorld(teleporter.Location, teleporter.Map);

            teleporter.Delete();
        }

        return "Despise setup complete";
    }

    public static void SetupTeleporters()
    {
        // Gate1
        var gate1 = new GateTeleporter(3948, 1965, new Point3D(5458, 610, 50), Map.Trammel);
        var gate2 = new GateTeleporter(3948, 1960, new Point3D(5476, 737, 5), Map.Trammel);
        WeakEntityCollection.Add(CollectionKey, gate1);
        WeakEntityCollection.Add(CollectionKey, gate2);

        gate1.MoveToWorld(new Point3D(5476, 737, 5), Map.Trammel);
        gate2.MoveToWorld(new Point3D(5458, 610, 50), Map.Trammel);

        // Gate2
        gate1 = new GateTeleporter(3948, 1960, new Point3D(5460, 675, 20), Map.Trammel);
        gate2 = new GateTeleporter(3948, 1965, new Point3D(5460, 523, 60), Map.Trammel);
        WeakEntityCollection.Add(CollectionKey, gate1);
        WeakEntityCollection.Add(CollectionKey, gate2);

        gate1.MoveToWorld(new Point3D(5460, 523, 60), Map.Trammel);
        gate2.MoveToWorld(new Point3D(5460, 675, 20), Map.Trammel);

        // Gate3
        gate1 = new GateTeleporter(3948, 1965, new Point3D(5387, 628, 30), Map.Trammel);
        gate2 = new GateTeleporter(3948, 1960, new Point3D(5388, 753, 5), Map.Trammel);
        WeakEntityCollection.Add(CollectionKey, gate1);
        WeakEntityCollection.Add(CollectionKey, gate2);

        gate1.MoveToWorld(new Point3D(5388, 753, 5), Map.Trammel);
        gate2.MoveToWorld(new Point3D(5387, 628, 30), Map.Trammel);
    }
}
