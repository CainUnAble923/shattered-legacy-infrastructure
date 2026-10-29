// SosTreasureLandVerification.cs
//
// cc-P684 (SoS swap S11): treasure maps on the Siege of Sosaria land. server/data/treasure.cfg replaces pinned's
// Data/treasure.cfg (made by shard-migration notes/cc-P684-tools/treasure.py: pinned's 193 spots judged on SoS, kept,
// moved or dropped, and 245 on the new land), and TreasureMap.ValidateLocation asks ClusterFSosTreasureLand.Refused, so
// the revamp's random chest locations keep out of S14's unfinished land and, on Felucca, Trammel's towns and dungeons. The
// land facts (every spot diggable and outside towns, dungeons and closures on the map) are shard-migration
// tools/tests/test_sos_s11.py.
//
// Facts:
//   T1. The shipped treasure.cfg is ours: 436 spots, every one parsed, none in a closure box, some in the Haven box
//       (TreasureMap.GetRandomHavenLocation, the level-0 maps' spots, is never Point2D.Zero).
//   T2. Refused: a tile in each closure box on Trammel and Felucca; on Felucca a tile inside a Trammel TownRegion or
//       DungeonRegion, and not on Trammel (whose own regions ValidateLocation reads); never a tile outside them, or
//       on another map.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Regions;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosTreasureLandVerification
{
    private static List<(int X, int Y)> Spots()
    {
        var path = Path.Combine(Server.Core.BaseDirectory, "Data", "treasure.cfg");
        Assert.True(File.Exists(path), path);
        return File.ReadAllLines(path)
            .Select(l => l.Split(' '))
            .Where(p => p.Length >= 2)
            .Select(p => (int.Parse(p[0]), int.Parse(p[1])))
            .ToList();
    }

    [Fact]
    public void T1_TheShippedTreasureCfgIsOurs()
    {
        var spots = Spots();
        Assert.Equal(436, spots.Count);
        Assert.DoesNotContain(spots, s => ClusterFSosTreasureLand.InClosure(s.X, s.Y));
        Assert.Contains(spots, s => TreasureMap.IsInHavenIsland(new Point2D(s.X, s.Y)));
        Assert.NotEqual(Point2D.Zero, TreasureMap.GetRandomHavenLocation());
    }

    [Fact]
    public void T2_RefusedKeepsChestsOutOfClosuresAndFeluccasTownsAndDungeons()
    {
        foreach (var (x1, y1, x2, y2) in ClusterFSosTreasureLand.Closures)
        {
            var (x, y) = ((x1 + x2) / 2, (y1 + y2) / 2);
            Assert.True(ClusterFSosTreasureLand.Refused(x, y, 0, Map.Trammel), $"{x},{y} Trammel");
            Assert.True(ClusterFSosTreasureLand.Refused(x, y, 0, Map.Felucca), $"{x},{y} Felucca");
            Assert.False(ClusterFSosTreasureLand.Refused(x, y, 0, Map.Malas), $"{x},{y} Malas");
            Assert.True(ClusterFSosTreasureLand.Refused(x1, y1, 0, Map.Trammel));
            Assert.True(ClusterFSosTreasureLand.Refused(x2, y2, 0, Map.Trammel));
            Assert.False(ClusterFSosTreasureLand.Refused(x1 - 1, y1, 0, Map.Trammel));
        }

        // A spot outside every box and every region (Valunstead's, below, is not registered yet)
        Assert.False(ClusterFSosTreasureLand.Refused(300, 300, 0, Map.Felucca));

        var town = new TownRegion("P684 town", Map.Trammel, 50, new Rectangle3D(290, 290, -128, 20, 20, 256));
        var dungeon = new DungeonRegion("P684 dungeon", Map.Trammel, 50, new Rectangle3D(330, 290, -128, 20, 20, 256));
        town.Register();
        dungeon.Register();
        try
        {
            Assert.True(ClusterFSosTreasureLand.Refused(300, 300, 0, Map.Felucca));
            Assert.True(ClusterFSosTreasureLand.Refused(340, 300, 0, Map.Felucca));
            Assert.False(ClusterFSosTreasureLand.Refused(300, 300, 0, Map.Trammel)); // Trammel's own regions: ValidateLocation's
            Assert.False(ClusterFSosTreasureLand.Refused(370, 300, 0, Map.Felucca));
        }
        finally
        {
            town.Unregister();
            dungeon.Unregister();
        }
    }
}
