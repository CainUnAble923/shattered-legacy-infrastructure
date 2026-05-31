using System;
using Server.Items;

namespace Server;

public static class ClusterFSouthMineDecor
{
    private static bool _enabled;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.southMineDecor.enabled", true);
        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSouthMineDecor", AccessLevel.Administrator, ClusterFSouthMineDecor_OnCommand);
    }

    [Usage("ClusterFSouthMineDecor [dryrun|replace]")]
    [Description("Seeds forge and anvil at the New Haven south mine camp. Default places only if missing.")]
    private static void ClusterFSouthMineDecor_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("ClusterF south mine decor is disabled.");
            return;
        }

        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "missing";
        var dryRun = mode == "dryrun";
        var replace = mode == "replace";

        var result = Seed(dryRun, replace);
        e.Mobile.SendMessage(result);
    }

    private static void OnWorldLoad()
    {
        if (!_enabled)
        {
            return;
        }

        var result = Seed(false, false);
        Console.WriteLine(result);
    }

    private static string Seed(bool dryRun, bool replace)
    {
        var forgeLocation = new Point3D(3508, 2746, 0);
        var anvilLocation = new Point3D(3510, 2746, 0);

        var existingForge = FindItem<Forge>(forgeLocation);
        var existingAnvil = FindItem<Anvil>(anvilLocation);

        if (replace)
        {
            existingForge?.Delete();
            existingAnvil?.Delete();
            existingForge = null;
            existingAnvil = null;
        }

        var placed = 0;

        if (existingForge == null)
        {
            if (!dryRun)
            {
                var forge = new Forge();
                forge.MoveToWorld(forgeLocation, Map.Trammel);
            }
            placed++;
        }

        if (existingAnvil == null)
        {
            if (!dryRun)
            {
                var anvil = new Anvil();
                anvil.MoveToWorld(anvilLocation, Map.Trammel);
            }
            placed++;
        }

        return $"ClusterF south mine decor {(dryRun ? "dry run" : "complete")}: placed {placed}, skipped {2 - placed}.";
    }

    private static T FindItem<T>(Point3D location) where T : Item
    {
        foreach (var item in World.Items.Values)
        {
            if (!item.Deleted && item is T && item.Map == Map.Trammel && item.Location == location)
            {
                return (T)item;
            }
        }

        return null;
    }
}
