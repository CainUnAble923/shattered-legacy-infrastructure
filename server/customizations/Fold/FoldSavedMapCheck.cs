// FoldSavedMapCheck.cs
//
// cc-P661 (P481 F1): a save that names a map index nobody registered is reported, not crashed on.
//
// Pinned reads a map as Map.Maps[index] (IGenericReader.cs:120), so an index with no map (a Fold index whose
// registration was ever removed, say) loads as null without an exception. What that leaves:
//   - a player: Mobile.Deserialize moves every player off the world to Internal and keeps the map as LogoutMap
//     (Mobile.cs:6511-6517), so the map lost is LogoutMap, null. Login only puts a player back when LogoutMap is set
//     (Mobile.cs:1320-1324), so they would log in to the Internal map and stand nowhere. This check names them in the
//     log and sets their way back to the New Haven start (FoldMirrorRegion.Home), so they log in somewhere real.
//     Staff are named but not moved.
//   - an item or a creature standing in that world: a null map, outside every sector. Named and counted, never deleted:
//     registering the index again brings them back on the next start.
// Runs at Initialize, after World.Load (Main.cs:671-673), every start, and changes nothing when there is nothing to find.

using System.Collections.Generic;
using Server.Logging;
using Server.Regions;

namespace Server;

public static class FoldSavedMapCheck
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(FoldSavedMapCheck));

    /// <summary>Most entities named one by one in the log; the rest are counted.</summary>
    public const int MaxNamed = 20;

    public static void Initialize()
    {
        foreach (var line in Run())
        {
            logger.Warning("{Line}", line);
        }
    }

    /// <summary>Finds what a save left without a map, sends lost players' way back to New Haven, and returns the report.</summary>
    public static List<string> Run()
    {
        var lines = new List<string>();
        var (home, homeMap) = FoldMirrorRegion.Home;

        foreach (var m in World.Mobiles.Values)
        {
            if (m.Deleted)
            {
                continue;
            }

            if (m.Player)
            {
                if (m.Map != Map.Internal || m.LogoutMap != null)
                {
                    continue;
                }

                if (m.AccessLevel > AccessLevel.Player)
                {
                    lines.Add($"Saved map check: staff character {m.Name} ({m.Serial}) has no map to log in to (an unregistered map index in the save); left as it is");
                    continue;
                }

                m.LogoutMap = homeMap;
                m.LogoutLocation = home;
                lines.Add($"Saved map check: {m.Name} ({m.Serial}) was saved on a map that is not registered; their next login is at the New Haven start {home}");
            }
            else if (m.Map == null)
            {
                lines.Add($"Saved map check: {m.GetType().Name} {m.Serial} stands on a map that is not registered; kept, outside the world");
            }
        }

        foreach (var item in World.Items.Values)
        {
            if (!item.Deleted && item.Parent == null && item.Map == null)
            {
                lines.Add($"Saved map check: {item.GetType().Name} {item.Serial} lies on a map that is not registered; kept, outside the world");
            }
        }

        if (lines.Count > MaxNamed)
        {
            var more = lines.Count - MaxNamed;
            lines.RemoveRange(MaxNamed, more);
            lines.Add($"Saved map check: and {more} more");
        }

        return lines;
    }
}
