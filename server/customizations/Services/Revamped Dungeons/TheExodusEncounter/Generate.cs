// ServUO: Quests/TheExodusEncounter/Generate.cs (cc-P195), with the three ServUO pieces [GenExodusQuest leaves to
// other commands and to the boot folded into one:
//   1. the Ver Lor Reg controller (ServUO: VerLorRegController.Initialize places it at 849,648,-40 on every boot and
//      its constructor starts the boss), placed and started here instead, the shard's rule;
//   2. the way out: a public moongate at 761,641,0 and an Ankh of Sacrifice at 757,641,0 (ServUO's Generate);
//   3. the 39 Nexus machines and the Keypunch Reader (ServUO: [GenExodusNexus, Decorate over
//      Data/Decoration/Exodus; ExodusDecoration here);
//   4. the 16 spawners and the Arch Zealot (ServUO: [XmlLoad Spawns/TheExodusEncounterQuest.xml; ExodusSpawns here).
// Everything is tagged into the "exodusencounter" collection so [DeleteExodusEncounter takes it down (ServUO has no
// delete). A re-run deletes first (CommandRerun.Replaces). The Exodus Dungeon's own stock spawners (pinned
// shared/ilshenar/Exodus.json: minions, overseers and the rest) are not touched; ServUO adds to them too.

using Server.Commands;
using Server.Items;

namespace Server.Engines.Exodus;

public static class ExodusGenerator
{
    public static void Initialize()
    {
        CommandSystem.Register("GenExodusEncounter", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteExodusEncounter", AccessLevel.Administrator, Delete_OnCommand);
    }

    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true,
        Summary = "The Exodus Encounter on Ilshenar: the Ver Lor Reg controller and Clockwork Exodus, the exit moongate and ankh, the 39 Nexus machines and the Keypunch Reader, the 16 spawners and the Arch Zealot. Deletes its own earlier placement first, so it can run again.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteExodusEncounter();
        var report = Generate();

        e.Mobile.SendMessage(deleted > 0 ? $"Removed the earlier placement ({deleted} entities). {report}" : report);
    }

    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true,
        Summary = "DELETES everything GenExodusEncounter placed (controller and boss, moongate, ankh, Nexus machines, reader, spawners and their spawns, the Arch Zealot).")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteExodusEncounter();
        e.Mobile.SendMessage($"Exodus Encounter: {deleted} entities deleted.");
    }

    public static int DeleteExodusEncounter() => WeakEntityCollection.Delete(ExodusEncounter.CollectionKey);

    public static bool AnyPresent() => VerLorRegController.IlshenarInstance is { Deleted: false } || ExodusSpawns.AnyPresent();

    /// <summary>Places the encounter on Ilshenar. Returns a one-line report.</summary>
    public static string Generate()
    {
        var map = Map.Ilshenar;

        var controller = new VerLorRegController();
        WeakEntityCollection.Add(ExodusEncounter.CollectionKey, controller);
        controller.MoveToWorld(ExodusEncounter.ControllerLocation, map);
        controller.StartHere();

        var gate = new PublicMoongate();
        WeakEntityCollection.Add(ExodusEncounter.CollectionKey, gate);
        gate.MoveToWorld(ExodusEncounter.MoongateLocation, map);

        var ankh = new AnkhOfSacrificeAddon(true);
        WeakEntityCollection.Add(ExodusEncounter.CollectionKey, ankh);
        ankh.MoveToWorld(ExodusEncounter.AnkhLocation, map);

        var decoration = ExodusDecoration.Generate(map);
        var spawners = ExodusSpawns.Generate();
        ExodusSpawns.PlaceArchZealot();

        return $"Exodus Encounter: the controller and Clockwork Exodus, the moongate and ankh, {decoration.Count - 1} Nexus machines and the Keypunch Reader, {spawners.Count} spawners, the Arch Zealot.";
    }
}
