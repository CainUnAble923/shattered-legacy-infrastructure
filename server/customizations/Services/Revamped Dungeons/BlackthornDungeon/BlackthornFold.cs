// cc-P691 (SoS swap): where Blackthorn Dungeon stands now. On the Siege of Sosaria map its stock area (6151-6563,
// 2301-2839) holds the author's own dungeons (P677 switched it off), so the map patch copies EA's dungeon from stock
// Trammel into site A, the Fold's empty room (trammel.mapops; shard-migration notes/cc-P691-blackthorn-to-fold.md), and
// it is played on the Fold (map 32, FoldMaps.cs), which draws the same file. Trammel and Felucca draw the copy too, but
// there the Fold's mirror region (FoldRegions.cs) sends any player who reaches it home.
//
// The copy is too tall for one place (Chase 2026-10-30: "All of it, two offsets"), so it is cut along the invasion
// block's south edge and each piece moves by a multiple of 8:
//   the dungeon proper   stock x 6176-6472, y 2556-2804   +(40, 1248)   -> 6216-6512, 3804-4052 (west of the isle)
//   the invasion rooms   stock x 6248-6490, y 2315-2587   +(656, 1480)  -> 6904-7146, 3795-4067 (east of the isle)
// Every ServUO coordinate in this folder stays as ServUO wrote it and goes through Move, so one place says where.
// Where the two pieces met on stock (the dungeon's north corridor, x 6356-6370, walking from y 2588 into the invasion
// block's cave at 2587) a teleporter pair carries the walk across.
//
// The way in: the author's Blackthorn Castle on SoS Trammel (the go spot 1533, 1415) has no cellar; it stands on water
// statics at Z -5 with its ground floor at 15. A cellar hatch (OSI's 0x3E65, a server item, P678's way) goes in the
// keep's south-west storeroom (x 1513-1521, y 1432-1438, its door at 1516, 1431 from the dining hall), on the room's
// clear east side (its barrels and crates stand west and north: britain.cfg and the test world), set one below the
// floor so it is walked onto, with one teleporter down to the dungeon's stock landing (the foot of its east
// stairs). The stock way out, five tiles at the stairs' top (ServUO's 6440, 2677-2681, Z 20), comes back beside the
// hatch.

using System.Linq;

namespace Server.Engines.Blackthorn;

public static class BlackthornFold
{
    /// <summary>The Fold, map 32 (null before FoldMaps.Configure).</summary>
    public static Map Map => FoldMaps.Fold;

    /// <summary>The invasion block on stock Trammel, x 6248-6490, y 2315-2587.</summary>
    public static readonly Rectangle2D StockInvasionRooms = new(6248, 2315, 243, 273);

    /// <summary>The dungeon proper on stock Trammel with its north-west cave, x 6176-6472, y 2556-2804.</summary>
    public static readonly Rectangle2D StockDungeon = new(6176, 2556, 297, 249);

    public static readonly Point2D DungeonOffset = new(40, 1248);
    public static readonly Point2D InvasionRoomsOffset = new(656, 1480);

    /// <summary>The offset of the piece a stock spot belongs to: the invasion block's, else the dungeon's.</summary>
    public static Point2D OffsetOf(int x, int y) => StockInvasionRooms.Contains(new Point2D(x, y)) ? InvasionRoomsOffset : DungeonOffset;

    /// <summary>A ServUO (stock Trammel) spot moved to where its piece stands on the Fold.</summary>
    public static Point3D Move(Point3D stock)
    {
        var o = OffsetOf(stock.X, stock.Y);
        return new Point3D(stock.X + o.X, stock.Y + o.Y, stock.Z);
    }

    public static Point3D Move(int x, int y, int z) => Move(new Point3D(x, y, z));

    /// <summary>A stock rectangle moved by the offset of the piece its start corner belongs to.</summary>
    public static Rectangle2D Move(Rectangle2D stock)
    {
        var o = OffsetOf(stock.X, stock.Y);
        return new Rectangle2D(stock.X + o.X, stock.Y + o.Y, stock.Width, stock.Height);
    }

    /// <summary>The dungeon proper on the Fold, x 6216-6512, y 3804-4052.</summary>
    public static readonly Rectangle2D Dungeon = new(6216, 3804, 297, 249);

    /// <summary>The invasion rooms on the Fold, x 6904-7146, y 3795-4067.</summary>
    public static readonly Rectangle2D InvasionRooms = new(6904, 3795, 243, 273);

    public static readonly Rectangle2D[] Bounds = { Dungeon, InvasionRooms };

    /// <summary>True for a spot of either piece on the Fold.</summary>
    public static bool Contains(Point3D p, Map map) => map != null && map == Map && Bounds.Any(r => r.Contains(p));

    /// <summary>ServUO's teleporter landing, the foot of the east stairs (6432, 2679, 0 on stock).</summary>
    public static readonly Point3D Landing = Move(6432, 2679, 0);

    /// <summary>
    ///     True when this server's map has the copy: the landing fits a person. Without the map overlay the Fold is void
    ///     there and nothing is placed (the Rootdeep rule: never send a player into void). A seam for tests.
    /// </summary>
    public static System.Func<bool> MapReady { get; set; } = () => Map is { } map && map.CanFit(Landing, 16, false, false);

    // -- The way in and out --

    public const int HatchItemId = 0x3E65; // OSI's "hatch", a Surface 3 high
    public const string HatchName = "a cellar hatch";

    /// <summary>The hatch on Trammel: the storeroom floor is Z 15, so its top (Z 17) is one step up.</summary>
    public static readonly Point3D Hatch = new(1519, 1437, 14);

    /// <summary>The hatch's teleporter, at the Z a player stands at on the hatch (Mobile.Move fires items at that Z).</summary>
    public static readonly Point3D HatchTeleporter = new(1519, 1437, 17);

    /// <summary>Where the way out lands: two tiles east of the hatch, so nobody is sent straight back down.</summary>
    public static readonly Point3D BesideHatch = new(1521, 1437, 15);

    /// <summary>ServUO's way out (stock teleporters.json, the Trammel end): the top of the east stairs, five tiles.</summary>
    public static readonly Point3D[] WayOut = Enumerable.Range(2677, 5).Select(y => Move(6440, y, 20)).ToArray();

    /// <summary>
    ///     The Agent of the Crown's spot in the storeroom, by its east wall (ServUO's stood in the stock castle, 1488, 1473,
    ///     the moat on SoS). Clear of the room's crates and barrels (britain.cfg: 1518-1520, 1432-1433 and x 1512; the test
    ///     world's barrels at 1513-1518, 1435-1437).
    /// </summary>
    public static readonly Point3D AgentSpot = new(1521, 1434, 15);

    // -- The join between the two pieces (stock x 6356-6370 on the corridor's rows) --

    public const int JoinX1 = 6356, JoinX2 = 6370;

    /// <summary>
    ///     (where it stands, where it lands) for every join teleporter, both on the Fold: the dungeon corridor's last row
    ///     (stock 2588) to the cave mouth room's floor (2585), and the invasion block's last row (2587) to the corridor
    ///     (2589). Every arrival is a plain floor tile, one row past the other side's teleporters.
    /// </summary>
    public static readonly (Point3D At, Point3D To)[] Join =
        Enumerable.Range(JoinX1, JoinX2 - JoinX1 + 1).Select(x => (Move(x, 2588, 0), Move(x, 2585, 0)))
            .Concat(Enumerable.Range(JoinX1, JoinX2 - JoinX1 + 1).Select(x => (Move(x, 2587, 0), Move(x, 2589, 0))))
            .ToArray();
}
