// cc-P33 (F-3): places a Cleanup Officer and a "Trash - Keep Britannia Clean" barrel. Never at world load.
//
// Where OSI has them: ServUO pub57 places neither. TheCleanupOfficer and CleanupTrashBarrel appear only in its
// placement-tool list (Data/objects.xml:875, :6329), in no spawn or decoration file. Pinned's decoration has no trash
// barrel of any kind. So the spot is ours to choose: New Haven first, at the bank (proposal in shard-migration
// notes/cc-P33-clean-up-britannia.md). The command places at the caller's own tile, so the floor height is the
// caller's and nothing is guessed: stand where the Officer should stand and run it, dry run first.
//
// [ClusterFPlaceCleanUp [dryrun]
//   The Officer on the caller's tile, the barrel one tile east. Each is skipped if one already stands within
//   15 tiles (Officer) or 3 tiles (barrel), or if its tile cannot hold it. Nothing is moved or deleted.

using System;
using Server.Commands;
using Server.Engines.CleanUpBritannia;
using Server.Items;

namespace Server;

public static class ClusterFCleanUpPlacement
{
    public const int OfficerRange = 15;
    public const int BarrelRange = 3;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFPlaceCleanUp", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFPlaceCleanUp [dryrun]")]
    [Description("Places a Cleanup Officer on your tile and a Clean Up trash barrel one tile east, each only if none stands near and the tile can hold it. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places a Cleanup Officer on your tile and a Clean Up trash barrel one tile east, where none stands near.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        if (mode != "" && mode != "dryrun")
        {
            e.Mobile.SendMessage("Usage: [ClusterFPlaceCleanUp [dryrun]");
            return;
        }

        e.Mobile.SendMessage(Place(e.Mobile.Map, e.Mobile.Location, mode == "dryrun"));
    }

    // fits: the tile check, the map's own CanFit (height 16, mobiles not counted) unless a test supplies one; the
    // test host has no map files.
    public static string Place(Map map, Point3D at, bool dryRun, Func<Point3D, bool> fits = null)
    {
        fits ??= p => map.CanFit(p, 16, false, false);

        if (map == null || map == Map.Internal)
        {
            return "Clean Up placement: no map here.";
        }

        var verb = dryRun ? "would place" : "placed";
        var barrelAt = new Point3D(at.X + 1, at.Y, at.Z);

        string officer;

        if (Near<TheCleanupOfficer>(map, at, OfficerRange) is { } o)
        {
            officer = $"Officer already at {o.Location}";
        }
        else if (!fits(at)) // the caller stands there, so mobiles are not counted
        {
            officer = $"Officer not placed: {at} cannot hold a mobile";
        }
        else
        {
            if (!dryRun)
            {
                new TheCleanupOfficer { Direction = Direction.South }.MoveToWorld(at, map);
            }

            officer = $"Officer {verb} at {at}";
        }

        string barrel;

        if (NearItem<CleanupTrashBarrel>(map, barrelAt, BarrelRange) is { } b)
        {
            barrel = $"barrel already at {b.Location}";
        }
        else if (!fits(barrelAt))
        {
            barrel = $"barrel not placed: {barrelAt} cannot hold it";
        }
        else
        {
            if (!dryRun)
            {
                new CleanupTrashBarrel().MoveToWorld(barrelAt, map);
            }

            barrel = $"barrel {verb} at {barrelAt}";
        }

        return $"Clean Up placement{(dryRun ? " (dry run)" : "")} on {map}: {officer}; {barrel}.";
    }

    private static T Near<T>(Map map, Point3D p, int range) where T : Mobile
    {
        foreach (var m in map.GetMobilesInRange(p, range))
        {
            if (m is T t)
            {
                return t;
            }
        }

        return null;
    }

    private static T NearItem<T>(Map map, Point3D p, int range) where T : Item
    {
        foreach (var i in map.GetItemsInRange(p, range))
        {
            if (i is T t)
            {
                return t;
            }
        }

        return null;
    }
}
