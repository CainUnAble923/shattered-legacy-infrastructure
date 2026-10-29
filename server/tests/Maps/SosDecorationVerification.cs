// SosDecorationVerification.cs
//
// cc-P680 (SoS swap S5): pinned's Trammel and Felucca decoration on the Siege of Sosaria land. Our decided copies of
// Data/Decoration/{Britannia,Trammel,Felucca} ship as Data/ShatteredLegacy/Decoration (made by shard-migration
// notes/cc-P680-tools/deco.py; every dropped or moved entry and its reason in out-decisions.csv), and
// Decorate-sos-decoration.patch reads them in place of pinned's. The test host has no map files: the land facts (no item
// in water or void, no door outside a doorway) are shard-migration tools/tests/test_s5_decoration.py.
//
// Facts:
//   D1. The three folders ship with all of pinned's 59 files, and pinned's own reader (DecorationList.ReadAll) reads
//       every one: each header names a type the server has.
//   D2. The entries: 20,486 Britannia (both facets), 2,150 Trammel, 2,566 Felucca, of pinned's 23,376, 2,192 and 2,731.
//   D3. Old Haven's shrine ankh (haven.cfg's AnkhNorth at 3606,2582, under a hall's upper floor on SoS) is left out,
//       and Moonglow's garden lamp post (4471,1180, whose light the author baked into his map) stays.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Commands;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosDecorationVerification
{
    private static readonly string[] Folders = ["Britannia", "Trammel", "Felucca"];

    // cc-P683 (SoS swap S6): the author's dungeon decoration ships in Trammel and Felucca beside pinned's decided cfgs.
    // It is not pinned's: these facts count pinned's files only, and SosDungeonDecorationVerification holds its own.
    private const string Ours = "sos-dungeons.cfg";

    private static IEnumerable<string> PinnedFiles(string root, string folder) =>
        Directory.GetFiles(Path.Combine(root, folder), "*.cfg").Where(f => Path.GetFileName(f) != Ours);

    private static string? Root()
    {
        // Never Core.BaseDirectory (see SosTrammelRegionsVerification.Find): the test assembly's folder, then up.
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(d.FullName, "Data", "ShatteredLegacy", "Decoration"),
                         Path.Combine(d.FullName, "Distribution", "Data", "ShatteredLegacy", "Decoration")
                     })
            {
                if (Directory.Exists(Path.Combine(candidate, "Britannia")))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, List<DecorationList>> ReadFolder(string root, string folder) =>
        PinnedFiles(root, folder)
            .ToDictionary(Path.GetFileName, f => DecorationList.ReadAll(f))!;

    private static IEnumerable<(string Header, Point3D Location)> Entries(string root, string folder, string file)
    {
        string? header = null;
        foreach (var raw in File.ReadLines(Path.Combine(root, folder, file)))
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

            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (header != null && parts.Length >= 3 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) &&
                int.TryParse(parts[2], out var z))
            {
                yield return (header, new Point3D(x, y, z));
            }
            else
            {
                header = s;
            }
        }
    }

    [Fact]
    public void D1_TheThreeFoldersShipAndPinnedsReaderReadsEveryFile()
    {
        var root = Root();
        Assert.NotNull(root);
        var files = 0;
        foreach (var folder in Folders)
        {
            foreach (var lists in ReadFolder(root!, folder).Values)
            {
                Assert.NotEmpty(lists);
                files++;
            }
        }

        Assert.Equal(59, files);
    }

    [Fact]
    public void D2_TheEntriesAreTheDecidedOnes()
    {
        var root = Root();
        Assert.NotNull(root);
        int Count(string folder) =>
            PinnedFiles(root!, folder).Sum(f => Entries(root!, folder, Path.GetFileName(f)).Count());

        Assert.Equal(20486, Count("Britannia"));
        Assert.Equal(2150, Count("Trammel"));
        Assert.Equal(2566, Count("Felucca"));
    }

    [Fact]
    public void D3_OldHavensShrineAnkhIsOutAndMoonglowsGardenLampStays()
    {
        var root = Root();
        Assert.NotNull(root);
        Assert.DoesNotContain(Entries(root!, "Trammel", "haven.cfg"),
            e => e.Header.StartsWith("AnkhNorth", StringComparison.Ordinal) && e.Location == new Point3D(3606, 2582, 10));
        Assert.Contains(Entries(root!, "Britannia", "moonglow.cfg"),
            e => e.Header.StartsWith("LampPost1", StringComparison.Ordinal) && e.Location == new Point3D(4471, 1180, 0));
    }
}
