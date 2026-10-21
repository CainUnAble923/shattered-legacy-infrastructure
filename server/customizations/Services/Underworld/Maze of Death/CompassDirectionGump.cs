// ServUO: Services/Underworld/Maze of Death/CompassDirectionGump.cs (cc-P197). The compass face (gump 9007) the
// Maze of Death draws for a player carrying a Golden Compass, redrawn on every step: an arrow (4501 north, 4503
// east, 4505 south, 4507 west) for each neighbouring tile that is on the maze's safe path. Pinned's legacy Gump.

using System.Collections.Generic;
using Server.Regions;

namespace Server.Gumps;

public class CompassDirectionGump : Gump
{
    private readonly Mobile _from;

    public CompassDirectionGump(Mobile from) : base(120, 50)
    {
        _from = from;
        var pointList = MazeOfDeathRegion.Path ?? new List<Point3D>();

        var cur = _from.Location;
        var northLoc = new Point3D(cur.X, cur.Y - 1, cur.Z);
        var eastLoc = new Point3D(cur.X + 1, cur.Y, cur.Z);
        var southLoc = new Point3D(cur.X, cur.Y + 1, cur.Z);
        var westLoc = new Point3D(cur.X - 1, cur.Y, cur.Z);

        // Empty radar
        AddImage(0, 0, 9007);

        // Arrows
        if (pointList.Contains(northLoc))
        {
            AddImage(100, 50, 4501);
        }

        if (pointList.Contains(eastLoc))
        {
            AddImage(100, 100, 4503);
        }

        if (pointList.Contains(southLoc))
        {
            AddImage(50, 100, 4505);
        }

        if (pointList.Contains(westLoc))
        {
            AddImage(50, 50, 4507);
        }
    }
}
