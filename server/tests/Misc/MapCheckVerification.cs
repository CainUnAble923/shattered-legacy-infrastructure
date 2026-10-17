// MapCheckVerification.cs
//
// cc-P135: the map checks behind [CheckFootprint, [ValidateSpots, [FindStaticProblems, [Walkable, [ShoreCheck and
// [ExportPoints (server/customizations/StaffHub/ClusterFMapCheck.cs), each over a hand-built region. The test gate's
// host loads no client files (pinned UOContent.Tests/Fixtures/TestServerInitializer.cs:34-39), so the region is an
// IClusterFTileSource built here: its own land, statics and tiledata flags. The real adapter over Map.Tiles and
// TileData is checked against known New Haven tiles in MapCheckRealDataVerification.cs when tile data is present.
// Notes: shard-migration/notes/cc-P135-vseuoaware-staff-commands.md.
//
// The region (32x32, grass at Z 0 unless said):
//   (2,2) water land; (3,2) impassable land; (4,2) wall static at Z 0; (5,2) wall at Z 20 (above stand height);
//   (6,2) table (Impassable and Surface) at Z 0; (7,2) wall at Z -1 (below the land); (8,2) wall at Z 16 (the edge)
//   (10..13,5) land Z 0, 3, 6, 7: a slope
//   (12,8) two rugs at Z 0 and one at Z 1; (13,8) a static at Z -10 and (14,8) one at Z -9; (15,8) an id with no tiledata
//   (20..23,20..23) water land at Z -5, so (21,21) and (22,22) touch no land; (25,20) a shallow bed: grass at Z -3 under
//   a Wet static at Z -3; (28,28) water land at Z -125, past the surface limit
//
// Facts:
//   1. A flat grass footprint CAN be placed; water, impassable land and an impassable non-surface static within 16 Z
//      each block a tile and say why.
//   2. A static above the stand height, one below the land, and a table (a surface) do not block; one at exactly 16 does.
//   3. The land Z spread is measured and a footprint past the limit CANNOT be placed with the spread said.
//   4. A footprint over 64 or off the map is refused before a tile is read.
//   5. Spot lines parse with an optional z and map, comments and blanks are skipped, bad lines are named, and only the
//      first 500 are read.
//   6. A spawner JSON and a decoration cfg give spots, the cfg one per map given.
//   7. Validation names an unknown map, a spot off the map and the blocked rule at the spot's own Z.
//   8. Static problems: a duplicate is the same id and Z (another Z is not one); buried is 10 or more below the land (9
//      is not); an id with no tiledata is named; the area cap refuses before reading.
//   9. Walkable counts each cause and lists the blocked tiles; the overlay is refused on live and outside 1..600 s.
//  10. Markers are red, named for the cause, not movable, expire when told, and [Walkable clear deletes them all.
//  11. Shore: water touching land is a hit with its step bucket, water surrounded by water is not, a shallow bed counts
//      as water, and only a surface outside -120..120 is a fault.
//  12. Exported points and rectangles read back through ModernUO's own JSON converters, and the points file validates.
//  13. A data file is read only under the given roots: no rooted path, no .., and a missing file is named.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Json;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MapCheckVerification
{
    // Land ids and item ids are the fake's own; their flags come from its tables, not from TileData.
    private const int Grass = 0x0003, Water = 0x00A8, Rock = 0x0220, Sand = 0x0016;
    private const int Wall = 0x0080, Table = 0x0B32, Rug = 0x0AD5, Shallow = 0x1797, Unknown = 0x7FFF;

    private sealed class FakeTiles : IClusterFTileSource
    {
        private readonly LandTile[,] _land;
        private readonly Dictionary<(int, int), List<StaticTile>> _statics = new();
        public int Reads;

        public FakeTiles(int width = 32, int height = 32, string name = "Fake")
        {
            Name = name;
            Width = width;
            Height = height;
            _land = new LandTile[width, height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    _land[x, y] = new LandTile((short)Grass, 0);
                }
            }
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public FakeTiles Land(int x, int y, int id, int z = 0)
        {
            _land[x, y] = new LandTile((short)id, (sbyte)z);
            return this;
        }

        public FakeTiles Water4x4(int x, int y, int z)
        {
            for (var dx = 0; dx < 4; dx++)
            {
                for (var dy = 0; dy < 4; dy++)
                {
                    Land(x + dx, y + dy, Water, z);
                }
            }

            return this;
        }

        public FakeTiles Static(int x, int y, int id, int z, int hue = 0)
        {
            if (!_statics.TryGetValue((x, y), out var l))
            {
                _statics[(x, y)] = l = [];
            }

            l.Add(new StaticTile((ushort)id, (byte)(x & 7), (byte)(y & 7), (sbyte)z, (short)hue));
            return this;
        }

        public LandTile GetLandTile(int x, int y)
        {
            Reads++;
            return _land[x, y];
        }

        public StaticTile[] GetStaticTiles(int x, int y)
        {
            Reads++;
            return _statics.TryGetValue((x, y), out var l) ? l.ToArray() : [];
        }

        public TileFlag LandFlags(int landId) => landId switch
        {
            Water => TileFlag.Wet,
            Rock  => TileFlag.Impassable,
            _     => TileFlag.None
        };

        public TileFlag ItemFlags(int itemId) => itemId switch
        {
            Wall    => TileFlag.Impassable,
            Table   => TileFlag.Impassable | TileFlag.Surface,
            Rug     => TileFlag.Surface,
            Shallow => TileFlag.Wet,
            _       => TileFlag.None
        };

        public bool HasItemData(int itemId) => itemId != Unknown;
    }

    private static FakeTiles Region() =>
        new FakeTiles()
            .Land(2, 2, Water).Land(3, 2, Rock)
            .Static(4, 2, Wall, 0).Static(5, 2, Wall, 20).Static(6, 2, Table, 0).Static(7, 2, Wall, -1).Static(8, 2, Wall, 16)
            .Land(11, 5, Grass, 3).Land(12, 5, Grass, 6).Land(13, 5, Grass, 7)
            .Static(12, 8, Rug, 0).Static(12, 8, Rug, 0).Static(12, 8, Rug, 1)
            .Static(13, 8, Rug, -10).Static(14, 8, Rug, -9).Static(15, 8, Unknown, 0)
            .Water4x4(20, 20, -5)
            .Land(25, 20, Grass, -3).Static(25, 20, Shallow, -3)
            .Land(28, 28, Water, -125);

    // ---- 1, 2 ------------------------------------------------------------------------------------

    [Fact]
    public void AFlatGrassFootprintCanBePlacedAndEachBlockerSaysWhy()
    {
        var t = Region();
        var ok = ClusterFMapCheck.CheckFootprint(t, 0, 10, 8, 8);
        Assert.True(ok.CanPlace, string.Join("\n", ok.Lines()));
        Assert.Empty(ok.Blocked);
        Assert.Contains("CAN be placed", ok.Lines().First());

        var bad = ClusterFMapCheck.CheckFootprint(t, 2, 2, 3, 1);
        Assert.False(bad.CanPlace);
        Assert.Equal(new[] {"(2,2): water", "(3,2): impassable land 0x0220", "(4,2): blocked by static 0x0080 at Z 0" },
            bad.Blocked.Select(b => b.ToString()).ToArray());
        Assert.Contains("CANNOT be placed. 3 blocked tiles", bad.Lines().First());
    }

    [Fact]
    public void AStaticAboveStandHeightBelowTheLandOrASurfaceDoesNotBlockAndOneAtTheEdgeDoes()
    {
        var t = Region();
        var r = ClusterFMapCheck.CheckFootprint(t, 5, 2, 4, 1);
        Assert.Equal(new[] { "(8,2): blocked by static 0x0080 at Z 16" }, r.Blocked.Select(b => b.ToString()).ToArray());
        Assert.Null(ClusterFMapCheck.Blocked(t, 5, 2));
        Assert.Null(ClusterFMapCheck.Blocked(t, 6, 2));
        Assert.Null(ClusterFMapCheck.Blocked(t, 7, 2));
        // With a spot Z of 5 the wall at Z 20 is within 16 and blocks; the one at Z 0 is below and does not.
        Assert.Equal("blocked by static 0x0080 at Z 20", ClusterFMapCheck.Blocked(t, 5, 2, 5));
        Assert.Null(ClusterFMapCheck.Blocked(t, 4, 2, 5));
    }

    // ---- 3, 4 ------------------------------------------------------------------------------------

    [Fact]
    public void TheZSpreadIsMeasuredAndRefusedPastTheLimit()
    {
        var t = Region();
        var r = ClusterFMapCheck.CheckFootprint(t, 10, 5, 4, 1, 5);
        Assert.Equal(0, r.MinZ);
        Assert.Equal(7, r.MaxZ);
        Assert.Equal(7, r.Spread);
        Assert.False(r.ZOk);
        Assert.False(r.CanPlace);
        Assert.Empty(r.Blocked);
        Assert.Contains("land Z 0..7 (spread 7, allowed 5)", r.Lines().First());
        Assert.True(ClusterFMapCheck.CheckFootprint(t, 10, 5, 4, 1, 7).CanPlace, "a spread of 7 is allowed when the limit is 7");
    }

    [Fact]
    public void AFootprintOverSixtyFourOrOffTheMapIsRefusedBeforeReading()
    {
        var t = Region();
        foreach (var (x, y, w, h) in new[] { (0, 0, 65, 1), (0, 0, 1, 65), (0, 0, 0, 5), (30, 30, 3, 3), (-1, 0, 2, 2) })
        {
            var r = ClusterFMapCheck.CheckFootprint(t, x, y, w, h);
            Assert.NotNull(r.Refused);
            Assert.False(r.CanPlace);
        }

        Assert.Equal(0, t.Reads);
        Assert.Contains("at most 64x64", ClusterFMapCheck.CheckFootprint(t, 0, 0, 65, 1).Refused);
        Assert.Contains("outside the 32x32 map Fake", ClusterFMapCheck.CheckFootprint(t, 30, 30, 3, 3).Refused);
    }

    // ---- 5, 6, 7 ---------------------------------------------------------------------------------

    [Fact]
    public void SpotLinesParseWithOptionalZAndMapAndBadLinesAreNamedAndTheFirstFiveHundredAreRead()
    {
        var r = ClusterFMapCheck.ParseSpotLines(["# a comment", "", "10,20", "11 21 5", "12,22,6,Felucca", "13,23,Malas", "x,y", "1,2,3,4,5", "14,24,z,Felucca"], "Trammel");
        Assert.Equal(4, r.Spots.Count);
        Assert.Equal((10, 20, (int?)null, "Trammel", "line 3"), (r.Spots[0].X, r.Spots[0].Y, r.Spots[0].Z, r.Spots[0].MapName, r.Spots[0].Source));
        Assert.Equal((11, 21, (int?)5, "Trammel"), (r.Spots[1].X, r.Spots[1].Y, r.Spots[1].Z, r.Spots[1].MapName));
        Assert.Equal((12, 22, (int?)6, "Felucca"), (r.Spots[2].X, r.Spots[2].Y, r.Spots[2].Z, r.Spots[2].MapName));
        Assert.Equal((13, 23, (int?)null, "Malas"), (r.Spots[3].X, r.Spots[3].Y, r.Spots[3].Z, r.Spots[3].MapName));
        Assert.Equal(new[] {"line 7: not x,y[,z][,map]: x,y", "line 8: not x,y[,z][,map]: 1,2,3,4,5", "line 9: z is not a number: 14,24,z,Felucca" }, r.BadLines);

        var many = ClusterFMapCheck.ParseSpotLines(Enumerable.Range(0, 503).Select(i => $"{i},{i}"), "Trammel");
        Assert.Equal(500, many.Spots.Count);
        Assert.Equal(3, many.Dropped);
        Assert.Contains("3 past the first 500 not read", many.Lines().First());
    }

    [Fact]
    public void ASpawnerJsonAndADecorationCfgGiveSpots()
    {
        const string json = """
            [
              { "$type": "Spawner", "name": "Spawner (113)", "location": [3650, 2509, 0], "map": "Trammel" },
              { "$type": "Spawner", "name": "obj", "location": { "x": 1, "y": 2, "z": 3 }, "map": "Felucca" },
              { "$type": "Spawner", "name": "str", "location": "(5, 6, 7)" },
              { "$type": "Spawner", "name": "none" }
            ]
            """;
        var s = ClusterFMapCheck.ParseSpawnerJson(json, "Malas");
        Assert.Equal(3, s.Spots.Count);
        Assert.Equal((3650, 2509, (int?)0, "Trammel", "spawner 1 'Spawner (113)'"), (s.Spots[0].X, s.Spots[0].Y, s.Spots[0].Z, s.Spots[0].MapName, s.Spots[0].Source));
        Assert.Equal((1, 2, (int?)3, "Felucca"), (s.Spots[1].X, s.Spots[1].Y, s.Spots[1].Z, s.Spots[1].MapName));
        Assert.Equal((5, 6, (int?)7, "Malas"), (s.Spots[2].X, s.Spots[2].Y, s.Spots[2].Z, s.Spots[2].MapName));
        Assert.Equal(new[] { "spawner 4 'none': no location" }, s.BadLines);
        Assert.Equal(new[] { "the file is not a JSON array of spawners" }, ClusterFMapCheck.ParseSpawnerJson("{}").BadLines);

        string[] cfg =
        [
            "# switch", "Static 0x108F", "5552 1864 11", "5399 1875 17", "",
            "# cot", "Static 0x11FD", "5554 1889 0 extra words", "not a line", ""
        ];
        var d = ClusterFMapCheck.ParseDecorationCfg(cfg, ["Trammel", "Felucca"]);
        Assert.Equal(6, d.Spots.Count);
        Assert.Equal((5552, 1864, (int?)11, "Trammel", "line 3 (Static 0x108F)"), (d.Spots[0].X, d.Spots[0].Y, d.Spots[0].Z, d.Spots[0].MapName, d.Spots[0].Source));
        Assert.Equal("Felucca", d.Spots[1].MapName);
        Assert.Equal((5554, 1889, (int?)0, "line 8 (Static 0x11FD)"), (d.Spots[4].X, d.Spots[4].Y, d.Spots[4].Z, d.Spots[4].Source));
        Assert.Equal(new[] { "line 9: not x y z under 'Static 0x11FD': not a line" }, d.BadLines);
    }

    [Fact]
    public void ValidationNamesAnUnknownMapASpotOffTheMapAndTheBlockedRuleAtTheSpotsOwnZ()
    {
        var t = Region();
        var r = ClusterFMapCheck.ParseSpotLines(["1,1", "2,2", "40,40", "5,2,5", "5,2", "9,9,0,Nowhere"], "Fake");
        ClusterFMapCheck.Validate(r, name => name == "Fake" ? t : null);
        Assert.Equal(new[] { null, "water", "outside the 32x32 map", "blocked by static 0x0080 at Z 20", null, "unknown map 'Nowhere'" },
            r.Spots.Select(s => s.Problem).ToArray());
        Assert.Equal(2, r.Ok);
        Assert.Equal(4, r.Problems);
        var lines = r.Lines().ToList();
        Assert.Equal("6 spots checked: 2 ok, 4 with problems.", lines[0]);
        Assert.Contains("  line 2: (2,2) on Fake: water", lines);
        Assert.Contains("  line 4: (5,2,5) on Fake: blocked by static 0x0080 at Z 20", lines);
    }

    // ---- 8 ---------------------------------------------------------------------------------------

    [Fact]
    public void StaticProblemsAreDuplicatesBuriedAndUnknownIdsWithTheAreaCap()
    {
        var t = Region();
        var r = ClusterFMapCheck.FindStaticProblems(t, 10, 8, 8, 1);
        Assert.Equal(8, r.Tiles);
        Assert.Equal(6, r.Statics);
        Assert.Equal(1, r.Duplicates);
        Assert.Equal(1, r.Buried);
        Assert.Equal(1, r.Unknown);
        Assert.Equal(new[] {"(12,8): duplicate static 0x0AD5 at Z 0", "(13,8): static 0x0AD5 Z -10 buried under land Z 0", "(15,8): static 0x7FFF has no tiledata entry" },
            r.Problems.Select(p => p.ToString()).ToArray());
        Assert.Equal("Checked 8 tiles, 6 statics in (10,8) 8x1: 1 duplicates, 1 buried, 1 unknown IDs.", r.Lines().First());

        var fresh = Region();
        Assert.NotNull(ClusterFMapCheck.FindStaticProblems(fresh, 0, 0, 513, 512).Refused);
        Assert.NotNull(ClusterFMapCheck.FindStaticProblems(fresh, 0, 0, 0, 1).Refused);
        Assert.Equal(0, fresh.Reads);
        // A box past the edge checks only the tiles that exist.
        Assert.Equal(4, ClusterFMapCheck.FindStaticProblems(fresh, 30, 30, 10, 10).Tiles);
    }

    // ---- 9, 10 -----------------------------------------------------------------------------------

    [Fact]
    public void WalkableCountsEachCauseAndListsTheBlockedTilesAndTheOverlayIsTestShardOnly()
    {
        var t = Region();
        var r = ClusterFMapCheck.CheckWalkable(t, 0, 2, 10, 1);
        Assert.Equal(10, r.Tiles);
        Assert.Equal(6, r.Walkable);
        Assert.Equal(1, r.Water);
        Assert.Equal(1, r.LandBlocked);
        Assert.Equal(2, r.StaticBlocked);
        Assert.Equal(new[] { "  (2,2): blocked by water", "  (3,2): blocked by impassable land 0x0220", "  (4,2): blocked by static 0x0080 at Z 0", "  (8,2): blocked by static 0x0080 at Z 16" },
            r.Lines().Skip(1).ToArray());
        Assert.Equal("10 tiles in (0,2) 10x1: 6 walkable, 1 water, 1 impassable land, 2 blocked by statics.", r.Lines().First());
        Assert.NotNull(ClusterFMapCheck.CheckWalkable(t, 0, 0, 512, 513).Refused);

        Assert.Null(ClusterFMapCheck.OverlayRefusal(true, 60));
        Assert.Contains("test shard only", ClusterFMapCheck.OverlayRefusal(false, 60));
        Assert.Contains("1 to 600 seconds", ClusterFMapCheck.OverlayRefusal(true, 0));
        Assert.Contains("1 to 600 seconds", ClusterFMapCheck.OverlayRefusal(true, 601));
    }

    [Fact]
    public void MarkersAreRedNamedForTheCauseNotMovableExpireWhenToldAndClearTogether()
    {
        // Far from anything this repo seeds (the LayoutCapture facts use the next tiles north).
        var map = Map.Trammel;
        var r = new ClusterFWalkResult { X = 5960, Y = 3920, Width = 3, Height = 1 };
        r.Blocked.Add(new ClusterFBlockedTile(5960, 3920, 0, "water"));
        r.Blocked.Add(new ClusterFBlockedTile(5961, 3920, 2, "static 0x0080 at Z 2"));
        r.Blocked.Add(new ClusterFBlockedTile(5962, 3920, 0, "impassable land 0x0220"));
        var before = ClusterFWalkMarker.LiveCount;
        try
        {
            Assert.Equal(2, ClusterFMapCheck.PlaceMarkers(map, r, 120, cap: 2));
            Assert.Equal(before + 2, ClusterFWalkMarker.LiveCount);
            var placed = new List<ClusterFWalkMarker>();
            foreach (var i in map.GetItemsInBounds<ClusterFWalkMarker>(new Rectangle2D(5960, 3920, 3, 1)))
            {
                placed.Add(i);
            }

            Assert.Equal(2, placed.Count);
            var water = placed.Single(m => m.X == 5960);
            Assert.Equal("blocked: water", water.Name);
            Assert.Equal(ClusterFWalkMarker.BlockedHue, water.Hue);
            Assert.Equal(ClusterFWalkMarker.Graphic, water.ItemID);
            Assert.False(water.Movable);
            Assert.Equal(0, water.Z);
            Assert.Equal(2, placed.Single(m => m.X == 5961).Z);
            Assert.Equal("blocked: static 0x0080 at Z 2", placed.Single(m => m.X == 5961).Name);
            Assert.InRange(water.Expires, Core.Now + TimeSpan.FromSeconds(119), Core.Now + TimeSpan.FromSeconds(121));
            Assert.Equal(before + 2, ClusterFWalkMarker.DeleteAll());
            Assert.Equal(0, ClusterFWalkMarker.LiveCount);
            Assert.True(water.Deleted);
        }
        finally
        {
            ClusterFWalkMarker.DeleteAll();
        }
    }

    // ---- 11 --------------------------------------------------------------------------------------

    [Fact]
    public void ShoreFindsWaterTouchingLandShallowBedsAndTheHeightBucketsAndOnlyASurfaceOutsideTheLimitIsAFault()
    {
        var t = Region();
        var r = ClusterFMapCheck.CheckShore(t, 18, 18, 10, 10);
        Assert.Equal(17, r.WaterTiles);       // 16 deep + 1 shallow bed
        Assert.Equal(13, r.ShoreHits);        // the 12 edge tiles of the 4x4 block and the bed; (21,21),(22,22),(21,22),(22,21) touch no land
        Assert.DoesNotContain(r.Hits, h => h.X is 21 or 22 && h.Y is 21 or 22);
        Assert.Equal(16, r.DeepWaterIds[Water]);
        Assert.Equal(1, r.ShallowBedIds[Grass]);
        Assert.Equal(13 * 1, r.ShoreIds.Values.Sum());  // every hit touches grass only
        Assert.Equal(Grass, r.ShoreIds.Keys.Single());
        // Coast height: the block's shore is 5 above the water (bucket "5 above"); the bed's is 3 above ("1-4 above").
        Assert.Equal(12, r.HeightBuckets[3]);
        Assert.Equal(1, r.HeightBuckets[2]);
        var bed = r.Hits.Single(h => h.X == 25 && h.Y == 20);
        Assert.Equal(-3, bed.WaterZ);
        Assert.Equal(3, bed.MaxStep);
        Assert.Equal(0, r.Problems);
        Assert.Contains("Region (18,18) 10x10: 17 water tiles (deep land + shallow beds), 13 touch non-water land, 0 with a water surface outside -120..120.", r.Lines());
        Assert.Contains("Coast height (shore Z minus water Z) per shore-touching water tile: below water x0, level (0) x0, 1-4 above x1, 5 above x12, 6-9 above x0, 10-24 above x0, 25+ above (cliff) x0", r.Lines());

        var deep = ClusterFMapCheck.CheckShore(t, 27, 27, 3, 3);
        Assert.Equal(1, deep.Problems);
        Assert.Contains("water surface Z -125 is outside -120..120", deep.Hits.Single().Problem);
        Assert.Equal(1, deep.HeightBuckets[6]);
        Assert.NotNull(ClusterFMapCheck.CheckShore(t, 0, 0, 1000, 1000).Refused);
    }

    // ---- 12 --------------------------------------------------------------------------------------

    public sealed record ExportDto(
        [property: JsonPropertyName("location")] Point3D Location,
        [property: JsonPropertyName("map")] Map Map,
        [property: JsonPropertyName("spawnBounds")] Rectangle3D SpawnBounds);

    [Fact]
    public void ExportedPointsAndRectanglesReadBackThroughModernUOsOwnConvertersAndThePointsFileValidates()
    {
        var points = new List<Point3D> { new(3650, 2509, 0), new(3598, 2450, 16) };
        var json = ClusterFMapCheck.PointsJson("Trammel", points);
        Assert.Contains("\"location\": [", json.Replace("\r", "").Replace("\n      ", " ").Replace("\n", ""));
        Assert.Contains("\"map\": \"Trammel\"", json);
        var back = JsonSerializer.Deserialize<List<ExportDto>>(json, JsonConfig.DefaultOptions);
        Assert.Equal(points, back.Select(d => d.Location).ToList());
        Assert.All(back, d => Assert.Equal(Map.Trammel, d.Map));

        var rect = ClusterFMapCheck.RectangleJson("Felucca", 100, 200, 10, 4, 7);
        var r = JsonSerializer.Deserialize<List<ExportDto>>(rect, JsonConfig.DefaultOptions).Single();
        Assert.Equal(Map.Felucca, r.Map);
        Assert.Equal(new Point3D(105, 202, 7), r.Location);
        Assert.Equal((100, 200, 110, 204), (r.SpawnBounds.Start.X, r.SpawnBounds.Start.Y, r.SpawnBounds.End.X, r.SpawnBounds.End.Y));
        Assert.Equal(10, r.SpawnBounds.Width);
        Assert.Equal(4, r.SpawnBounds.Height);

        // [ValidateSpots reads the exported points file as a spawner file.
        var spots = ClusterFMapCheck.ParseSpawnerJson(json);
        Assert.Equal(new[] { (3650, 2509, 0, "Trammel"), (3598, 2450, 16, "Trammel") }, spots.Spots.Select(s => (s.X, s.Y, s.Z.Value, s.MapName)).ToArray());
    }

    // ---- 13 --------------------------------------------------------------------------------------

    [Fact]
    public void ADataFileIsReadOnlyUnderTheGivenRootsAndNeverByARootedPathOrDotDot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sl-p135-" + Guid.NewGuid().ToString("N"));
        var other = root + "-other";
        Directory.CreateDirectory(Path.Combine(root, "Spawns"));
        Directory.CreateDirectory(other);
        var inside = Path.Combine(root, "Spawns", "a.json");
        var outside = Path.Combine(other, "b.json");
        File.WriteAllText(inside, "[]");
        File.WriteAllText(outside, "[]");
        try
        {
            Assert.Equal(Path.GetFullPath(inside), ClusterFMapCheck.ResolveReadable("Spawns/a.json", new[] { root }, out var e1));
            Assert.Null(e1);
            Assert.Equal(Path.GetFullPath(inside), ClusterFMapCheck.ResolveReadable(@"Spawns\a.json", new[] { other, root }, out _));
            Assert.Equal(Path.GetFullPath(outside), ClusterFMapCheck.ResolveReadable("b.json", new[] { root, other }, out _));
            Assert.Null(ClusterFMapCheck.ResolveReadable(outside, new[] { root }, out var e2));
            Assert.Contains("no rooted paths", e2);
            Assert.Null(ClusterFMapCheck.ResolveReadable("Spawns/../../" + Path.GetFileName(other) + "/b.json", new[] { root }, out var e3));
            Assert.Contains("no ..", e3);
            Assert.Null(ClusterFMapCheck.ResolveReadable("Spawns/missing.json", new[] { root }, out var e4));
            Assert.Contains("no file 'Spawns/missing.json'", e4);
            Assert.Null(ClusterFMapCheck.ResolveReadable("", new[] { root }, out var e5));
            Assert.Contains("relative to the server's Data folder", e5);
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(other, true);
        }
    }
}
