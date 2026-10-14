// cc-P98: [CaptureLayout and [StampLayout (server/customizations/ClusterFLayoutCapture.cs), after Kamras's Building
// Duplicator. Notes in shard-migration notes/cc-P98-building-duplicator.md.
//
// Facts:
//   1. A capture of a fixture area round-trips through the generator's reading of the layout file
//      (notes/cc-P31-tools/gen_office_layout.py: area, id as hex, name, x, y, z, as RegistrarOfficeVerification
//      reads the design file) to the same table as the fixture; hue survives; doors, addon bases and hidden items
//      are left out.
//   2. The size cap refuses a box over 64 x 64 before reading anything.
//   3. A bad line anywhere in the file stops the stamp before anything is placed.
//   4. Undo after a stamp that stopped part way removes every item it made, the one it was placing included.
//   5. Nudge moves the last stamp; undo removes it; there is nothing left to undo after.
//
// The fixture area is in Trammel far from anything this repo seeds, at x 5960 to 5965, y 3910 to 3913. Every item a
// fact places is deleted at its end.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LayoutCaptureVerification
{
    private static readonly Map M = Map.Trammel;
    private const int X0 = 5960, Y0 = 3910, X1 = 5965, Y1 = 3913;

    // What the generator makes of a layout file: the design components as MineCampComponent rows, in file order.
    private static List<MineCampComponent> GeneratorTable(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("components").EnumerateArray().Select(d => new MineCampComponent(
            d.GetProperty("area").GetString(),
            Convert.ToInt32(d.GetProperty("id").GetString(), 16),
            d.GetProperty("name").GetString(),
            d.GetProperty("x").GetInt32(),
            d.GetProperty("y").GetInt32(),
            d.GetProperty("z").GetInt32())).ToList();
    }

    // The map's item enumerable is a ref struct, so a loop, not LINQ.
    private static List<Item> ItemsIn(int x0, int y0, int x1, int y1)
    {
        var list = new List<Item>();
        foreach (var i in M.GetItemsInBounds<Item>(new Rectangle2D(x0, y0, x1 - x0 + 1, y1 - y0 + 1), true))
        {
            if (i.X >= x0 && i.X <= x1 && i.Y >= y0 && i.Y <= y1)
            {
                list.Add(i);
            }
        }

        return list;
    }

    private static Static Put(int id, int x, int y, int z, int hue = 0, bool visible = true)
    {
        var s = new Static(id) { Hue = hue, Visible = visible };
        s.MoveToWorld(new Point3D(x, y, z), M);
        return s;
    }

    [Fact]
    public void ACaptureRoundTripsThroughTheGeneratorToTheSameTable()
    {
        var placed = new List<Item>
        {
            Put(0x0A99, X0, Y0, 0, 0x481),          // bookcase, hued
            Put(0x0B26, X0 + 2, Y0 + 1, 5),         // candelabra
            Put(0x0E42, X1, Y1, 0),                 // chest graphic, a corner of the box
            Put(0x11C9, X0 + 1, Y0 + 3, 3, 0x21),   // potted tree, hued
        };
        var hidden = Put(0x1F13, X0 + 3, Y0 + 2, 0, visible: false);     // a spawner-like hidden item
        var door = new MetalDoor(DoorFacing.WestCW);
        door.MoveToWorld(new Point3D(X0 + 4, Y0, 0), M);
        var oven = new StoneOvenEastAddon();
        oven.MoveToWorld(new Point3D(X0 + 5, Y0 + 2, 0), M);
        try
        {
            var pieces = ClusterFLayoutCapture.Capture(M, new Point2D(X1, Y1), new Point2D(X0, Y0), -128, 127, "room", out var refused);
            Assert.Null(refused);

            // What the box holds that the capture must keep: the four statics, and any map statics the test host has
            // there (none expected), but not the door, the oven's base, or the hidden item. The oven's components are
            // ordinary visible items and are kept, as the original kept them.
            var ovenParts = oven.Components.Select(c => (c.ItemID & 0xFFFF, c.X, c.Y, c.Z, c.Hue)).ToList();
            var mapStatics = new List<(int, int, int, int, int)>();
            for (var x = X0; x <= X1; x++)
            {
                for (var y = Y0; y <= Y1; y++)
                {
                    foreach (var t in M.Tiles.GetStaticTiles(x, y))
                    {
                        mapStatics.Add((t.ID & 0xFFFF, x, y, t.Z, t.Hue));
                    }
                }
            }

            var expected = placed.Select(i => (i.ItemID, i.X, i.Y, i.Z, i.Hue)).Concat(ovenParts.Where(p => p.Item2 <= X1 && p.Item3 <= Y1))
                .Concat(mapStatics).OrderBy(p => p.Item3).ThenBy(p => p.Item2).ThenBy(p => p.Item4).ThenBy(p => p.Item1).ThenBy(p => p.Item5).ToList();
            Assert.Equal(expected, pieces.Select(p => (p.ItemID, p.X, p.Y, p.Z, p.Hue)).ToList());
            Assert.DoesNotContain(pieces, p => p.ItemID == (door.ItemID & 0xFFFF) && p.X == door.X && p.Y == door.Y);
            Assert.DoesNotContain(pieces, p => p.ItemID == 0x1F13);

            var origin = new Point3D(X0, Y0, pieces.Min(p => p.Z));
            var json = ClusterFLayoutCapture.ToJson("fixture-room", M, origin, pieces, "LayoutCaptureVerification");

            // Through the generator's reading: the same table as the fixture, row for row.
            var table = GeneratorTable(json);
            var want = pieces.Select(p => new MineCampComponent("room", p.ItemID, p.Name, p.X, p.Y, p.Z)).ToList();
            Assert.Equal(want, table);
            using (var doc = JsonDocument.Parse(json))
            {
                Assert.Equal("Trammel", doc.RootElement.GetProperty("map").GetString());
                Assert.Matches("^0x[0-9A-F]{4}$", doc.RootElement.GetProperty("components")[0].GetProperty("id").GetString());
            }

            // And back through the stamp's own check: the same pieces, hues included.
            var layout = ClusterFLayoutCapture.Parse(json, out var errors);
            Assert.Empty(errors);
            Assert.Equal(pieces, layout.Pieces);
            Assert.Equal(origin, layout.Origin);
            Assert.Same(M, layout.Map);
        }
        finally
        {
            foreach (var i in placed)
            {
                i.Delete();
            }

            hidden.Delete();
            door.Delete();
            oven.Delete();
        }
    }

    [Fact]
    public void TheSizeCapRefusesABigBoxBeforeReadingAnything()
    {
        var s = Put(0x0A99, X0, Y0, 0);
        try
        {
            var pieces = ClusterFLayoutCapture.Capture(M, new Point2D(X0, Y0), new Point2D(X0 + 64, Y0 + 3), -128, 127, "x", out var refused);
            Assert.Null(pieces);
            Assert.Contains("65 x 4", refused);
            Assert.Equal(0, ClusterFLayoutCapture.TilesRead);

            Assert.Null(ClusterFLayoutCapture.Capture(M, new Point2D(X0, Y0), new Point2D(X0 + 3, Y0 - 64), -128, 127, "x", out refused));
            Assert.Contains("4 x 65", refused);
            Assert.Equal(0, ClusterFLayoutCapture.TilesRead);

            // 64 x 64 is allowed, and does read.
            Assert.NotNull(ClusterFLayoutCapture.Capture(M, new Point2D(X0, Y0), new Point2D(X0 + 63, Y0 + 63), -128, 127, "x", out refused));
            Assert.Null(refused);
            Assert.True(ClusterFLayoutCapture.TilesRead > 0);
        }
        finally
        {
            s.Delete();
        }
    }

    private static string FileWith(params string[] components) =>
        "{ \"map\": \"Trammel\", \"name\": \"t\", \"origin\": { \"x\": 0, \"y\": 0, \"z\": 0 }, \"components\": [ " +
        string.Join(", ", components) + " ] }";

    private const string Good1 = "{ \"area\": \"a\", \"id\": \"0x0A99\", \"name\": \"bookcase\", \"x\": 0, \"y\": 0, \"z\": 0, \"hue\": 1153 }";
    private const string Good2 = "{ \"area\": \"a\", \"id\": \"0x0B26\", \"name\": \"candelabra\", \"x\": 1, \"y\": 0, \"z\": 0 }";

    [Fact]
    public void ABadLineStopsTheStampBeforeAnythingIsPlaced()
    {
        var gm = new Mobile();
        try
        {
            foreach (var bad in new[]
            {
                "{ \"area\": \"a\", \"id\": \"0xZZZZ\", \"name\": \"x\", \"x\": 2, \"y\": 0, \"z\": 0 }",
                "{ \"area\": \"a\", \"id\": \"0x0A99\", \"name\": \"x\", \"x\": 2, \"y\": 0, \"z\": 300 }",
                "{ \"area\": \"\", \"id\": \"0x0A99\", \"name\": \"x\", \"x\": 2, \"y\": 0, \"z\": 0 }",
                "{ \"area\": \"a\", \"id\": \"0x0A99\", \"name\": \"x\", \"x\": \"two\", \"y\": 0, \"z\": 0 }",
                "{ \"area\": \"a\", \"id\": \"0x0A99\", \"name\": \"x\", \"x\": 2, \"y\": 0, \"z\": 0, \"hue\": -1 }",
            })
            {
                var layout = ClusterFLayoutCapture.Parse(FileWith(Good1, Good2, bad), out var errors);
                Assert.Null(layout);
                Assert.Single(errors);
                Assert.StartsWith("component 3: ", errors[0]);
            }

            Assert.Null(ClusterFLayoutCapture.Parse(FileWith(Good1).Replace("Trammel", "Narnia"), out var e2));
            Assert.Contains("Narnia", e2[0]);
            Assert.Null(ClusterFLayoutCapture.Parse("{ not json", out _));

            // The command reads and checks the whole file, and only then stamps; Stamp is never reached here. And a
            // checked layout that would land outside Z there places nothing either.
            var before = ItemsIn(X0, Y0, X0 + 5, Y0 + 5).Count;
            var high = "{ \"area\": \"a\", \"id\": \"0x0B26\", \"name\": \"candelabra\", \"x\": 1, \"y\": 0, \"z\": 10 }";
            var ok = ClusterFLayoutCapture.Parse(FileWith(Good1, high), out _);
            var r = ClusterFLayoutCapture.Stamp(gm, ok, M, new Point3D(X0, Y0, 120));   // 10 + 120 is past 127
            Assert.Equal(0, r.Placed);
            Assert.NotNull(r.Error);
            Assert.Equal(before, ItemsIn(X0, Y0, X0 + 5, Y0 + 5).Count);
            Assert.Equal(0, ClusterFLayoutCapture.UndoCount(gm));
        }
        finally
        {
            while (ClusterFLayoutCapture.Undo(gm) >= 0)
            {
            }

            gm.Delete();
        }
    }

    [Fact]
    public void UndoAfterAPartialStampRemovesEveryPlacedItem()
    {
        var gm = new Mobile();
        try
        {
            var comps = Enumerable.Range(0, 6).Select(i =>
                $"{{ \"area\": \"a\", \"id\": \"0x0A99\", \"name\": \"bookcase\", \"x\": {i}, \"y\": 0, \"z\": 0 }}").ToArray();
            var layout = ClusterFLayoutCapture.Parse(FileWith(comps), out var errors);
            Assert.Empty(errors);

            var made = new List<int>();
            var r = ClusterFLayoutCapture.Stamp(gm, layout, M, new Point3D(X0, Y0 + 2, 0), i =>
            {
                made.Add(i);
                if (i == 3)
                {
                    throw new InvalidOperationException("the fact stops it here");
                }
            });
            Assert.Equal(3, r.Placed);
            Assert.Contains("stopped after 3 of 6", r.Error);
            Assert.Equal(3, ItemsIn(X0, Y0 + 2, X0 + 5, Y0 + 2).Count);

            // Four items were made: three placed and the one being placed when it stopped. Undo removes all four.
            Assert.Equal(4, ClusterFLayoutCapture.Undo(gm));
            Assert.Empty(ItemsIn(X0, Y0 + 2, X0 + 5, Y0 + 2));
            Assert.Equal(-1, ClusterFLayoutCapture.Undo(gm));
        }
        finally
        {
            while (ClusterFLayoutCapture.Undo(gm) >= 0)
            {
            }

            gm.Delete();
        }
    }

    [Fact]
    public void NudgeMovesTheLastStampAndUndoRemovesIt()
    {
        var gm = new Mobile();
        try
        {
            var layout = ClusterFLayoutCapture.Parse(FileWith(Good1, Good2), out _);
            var r = ClusterFLayoutCapture.Stamp(gm, layout, M, new Point3D(X0, Y0, 0));
            Assert.Null(r.Error);
            Assert.Equal(2, r.Placed);
            var placed = ItemsIn(X0, Y0, X0 + 1, Y0);
            Assert.Equal(2, placed.Count);
            Assert.Contains(placed, i => i.ItemID == 0x0A99 && i.Hue == 1153 && i is Static && !i.Movable);

            Assert.Equal(2, ClusterFLayoutCapture.Nudge(gm, 0, 2, 1));
            Assert.Empty(ItemsIn(X0, Y0, X0 + 1, Y0));
            Assert.All(ItemsIn(X0, Y0 + 2, X0 + 1, Y0 + 2), i => Assert.Equal(1, i.Z));

            Assert.Equal(2, ClusterFLayoutCapture.Undo(gm));
            Assert.Empty(ItemsIn(X0, Y0, X0 + 1, Y0 + 2));
            Assert.Equal(-1, ClusterFLayoutCapture.Nudge(gm, 1, 0, 0));
        }
        finally
        {
            while (ClusterFLayoutCapture.Undo(gm) >= 0)
            {
            }

            gm.Delete();
        }
    }
}
