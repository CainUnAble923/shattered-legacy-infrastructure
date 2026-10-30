// PlaceBuildVerification.cs
//
// cc-P734: saved builds are decoration files in pinned's [Decorate format with coordinates relative to the build's
// north-west corner; [PlaceBuild places one at a point (PlaceBuild.cs). Notes in shard-migration
// notes/cc-P734-build-review.md.
//
// Facts:
//   1. Shift moves every coordinate line by the origin and leaves type lines, comments, blank lines and a coordinate
//      line's extra text alone; Points reads the moved locations.
//   2. Placing a build puts each piece at origin + offset with its type and properties (a named static, a door with
//      its facing), and placing it again places nothing (pinned's DecorationList skips a piece already there).
//   3. ClearBuild removes exactly the build's pieces (dry run removes nothing) and leaves a movable item on the same
//      tile alone.

using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PlaceBuildVerification
{
    private const string Sample =
        "# a tiny practice build\n" +
        "Static 0x0007\n" +
        "0 0 0\n" +
        "1 0 0\n" +
        "\n" +
        "Static 0xA854 (Name=Practice signpost)\n" +
        "2 1 0\n" +
        "\n" +
        "DarkWoodDoor (Facing=WestCCW)\n" +
        "1 2 0\n";

    private static List<Item> ItemsAt(Map map, int x, int y)
    {
        var list = new List<Item>();
        foreach (var item in map.GetItemsAt(x, y))
        {
            list.Add(item);
        }

        return list;
    }

    [Fact]
    public void ShiftMovesOnlyCoordinateLines()
    {
        var moved = PlaceBuild.Shift("# 1 2 3 stays\nStatic 0x0007\n0 0 0\n-1 2 5 extra words\n\n", 100, 200, 10);
        var lines = moved.Split('\n');
        Assert.Equal("# 1 2 3 stays", lines[0]);
        Assert.Equal("Static 0x0007", lines[1]);
        Assert.Equal("100 200 10", lines[2]);
        Assert.Equal("99 202 15 extra words", lines[3]);
        Assert.Equal("", lines[4]);
        Assert.Equal(new[] { new Point3D(100, 200, 10), new Point3D(99, 202, 15) }, PlaceBuild.Points(moved));
    }

    [Fact]
    public void PlacingPutsEachPieceAtTheOriginAndPlacingAgainSkips()
    {
        var map = Map.Felucca;
        var origin = new Point3D(5700, 1180, 0);
        var before = World.Items.Keys.ToHashSet();
        try
        {
            Assert.Equal(4, PlaceBuild.Place(Sample, map, origin));
            Assert.Contains(ItemsAt(map, 5700, 1180), i => i is Static && i.ItemID == 0x0007 && i.Z == 0);
            Assert.Contains(ItemsAt(map, 5701, 1180), i => i is Static && i.ItemID == 0x0007);
            Assert.Contains(ItemsAt(map, 5702, 1181), i => i is Static && i.ItemID == 0xA854 && i.Name == "Practice signpost");
            var door = ItemsAt(map, 5701, 1182).OfType<DarkWoodDoor>().Single();
            Assert.Equal(0x06A9, door.ItemID);   // WestCCW, closed: hung along the wall line (guide section 2.4)

            Assert.Equal(0, PlaceBuild.Place(Sample, map, origin));
        }
        finally
        {
            foreach (var item in World.Items.Values.Where(i => !before.Contains(i.Serial)).ToList())
            {
                item.Delete();
            }
        }
    }

    [Fact]
    public void ClearRemovesTheBuildsPiecesOnly()
    {
        var map = Map.Felucca;
        var origin = new Point3D(5720, 1180, 0);
        var before = World.Items.Keys.ToHashSet();
        try
        {
            PlaceBuild.Place(Sample, map, origin);
            var loose = new Gold(5);
            loose.MoveToWorld(origin, map);
            Assert.Equal(4, PlaceBuild.Clear(Sample, map, origin, dryRun: true));
            Assert.Equal(3, ItemsAt(map, origin.X, origin.Y).Count + ItemsAt(map, 5721, 1180).Count);
            Assert.Equal(4, PlaceBuild.Clear(Sample, map, origin, dryRun: false));
            Assert.Equal(new Item[] { loose }, ItemsAt(map, origin.X, origin.Y));
            Assert.Empty(ItemsAt(map, 5722, 1181));
        }
        finally
        {
            foreach (var item in World.Items.Values.Where(i => !before.Contains(i.Serial)).ToList())
            {
                item.Delete();
            }
        }
    }
}
