// SosDungeonDecorationVerification.cs
//
// cc-P683 (SoS swap S6): the author's dungeon decoration (Zigholtul's SOS Trammel Dungeons.cfg, 775 lines: 200
// teleporters) ships as Data/ShatteredLegacy/Decoration/{Trammel,Felucca}/sos-dungeons.cfg (made by shard-migration
// notes/cc-P683-tools/port.py), beside pinned's decided cfgs, so the stock [Decorate places it on each facet
// (Decorate-sos-decoration.patch reads those folders in place of pinned's). The surface entrances of every dungeon S18
// has not opened are commented out: the dungeons are sealed by the absence of their way in. The land facts (every end
// walkable, no shipped teleporter into a sealed dungeon) need the map and are shard-migration
// tools/tests/test_sos_dungeon_decoration.py.
//
// Facts:
//   S1. Both files ship, pinned's own reader (DecorationList.ReadAll) reads them, and the one type they name is
//       pinned's Server.Items.Teleporter: 130 placements a facet.
//   S2. A real Generate of each file on its facet places 130 Teleporters, each where the file says, every PointDest
//       the file's and every MapDest that facet (a Trammel copy loaded on Felucca would send players to Trammel).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Server;
using Server.Commands;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosDungeonDecorationVerification
{
    private const int Shipped = 130;

    private static string? CfgPath(string facet)
    {
        // Never Core.BaseDirectory (see SosTrammelRegionsVerification.Find): the test assembly's folder, then up.
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            foreach (var root in new[]
                     {
                         Path.Combine(d.FullName, "Data", "ShatteredLegacy", "Decoration"),
                         Path.Combine(d.FullName, "Distribution", "Data", "ShatteredLegacy", "Decoration")
                     })
            {
                var path = Path.Combine(root, facet, "sos-dungeons.cfg");
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static readonly Regex Header = new(@"^Teleporter 0x1BC3 \(PointDest=\((-?\d+), (-?\d+), (-?\d+)\); MapDest=(\w+)\)$");

    // (location, PointDest, MapDest) of every active placement, read from the text
    private static List<(Point3D At, Point3D Dest, string Map)> Placements(string path)
    {
        var result = new List<(Point3D, Point3D, string)>();
        Match? header = null;
        foreach (var raw in System.IO.File.ReadLines(path))
        {
            var s = raw.Trim();
            if (s.Length == 0)
            {
                header = null;
                continue;
            }

            if (s.StartsWith('#'))
            {
                continue;
            }

            var m = Header.Match(s);
            if (m.Success)
            {
                header = m;
                continue;
            }

            Assert.True(header != null, $"{path}: a line with no header: {s}");
            var p = s.Split(' ').Select(int.Parse).ToArray();
            int G(int i) => int.Parse(header!.Groups[i].Value);
            result.Add((new Point3D(p[0], p[1], p[2]), new Point3D(G(1), G(2), G(3)), header!.Groups[4].Value));
        }

        return result;
    }

    [Theory]
    [InlineData("Trammel")]
    [InlineData("Felucca")]
    public void S1_TheFileShipsAndPinnedsReaderReadsOneTypeTeleporter(string facet)
    {
        var path = CfgPath(facet);
        Assert.NotNull(path);
        var lists = DecorationList.ReadAll(path!);
        Assert.NotEmpty(lists);
        var placements = Placements(path!);
        Assert.Equal(Shipped, placements.Count);
        Assert.All(placements, p => Assert.Equal(facet, p.Map));
        Assert.Same(typeof(Teleporter), AssemblyHandler.FindTypeByName("Teleporter"));
    }

    [Theory]
    [InlineData("Trammel")]
    [InlineData("Felucca")]
    public void S2_ARealGenerateOnItsFacetPlacesEveryTeleporterWithItsDestination(string facet)
    {
        var map = facet == "Felucca" ? Map.Felucca : Map.Trammel;
        var path = CfgPath(facet);
        Assert.NotNull(path);
        var want = Placements(path!);
        var placed = new List<Teleporter>();

        List<Teleporter> At(Point3D p)
        {
            // GetItemsAt is a ref-struct enumerable: a loop, not LINQ
            var found = new List<Teleporter>();
            foreach (var t in map.GetItemsAt<Teleporter>(p))
            {
                if (!t.Deleted && t.Z == p.Z)
                {
                    found.Add(t);
                }
            }

            return found;
        }

        try
        {
            foreach (var w in want)
            {
                foreach (var t in At(w.At))
                {
                    t.Delete();
                }
            }

            var count = DecorationList.ReadAll(path!).Sum(l => l.Generate([map]));
            Assert.Equal(Shipped, count);

            foreach (var w in want)
            {
                var here = At(w.At);
                placed.AddRange(here);
                var t = Assert.Single(here);
                Assert.Equal(w.Dest, t.PointDest);
                Assert.Same(map, t.MapDest);
                Assert.True(t.Active);
            }
        }
        finally
        {
            foreach (var t in placed)
            {
                t.Delete();
            }
        }
    }
}
