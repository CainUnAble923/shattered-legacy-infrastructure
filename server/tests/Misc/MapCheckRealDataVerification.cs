// MapCheckRealDataVerification.cs
//
// cc-P135: the map checks over the REAL maps, when the test host has them. The gate's host loads no client files
// (pinned UOContent.Tests/Fixtures/TestServerInitializer.cs:34-39: it reads MODERNUO_TEST_DATA_DIR, else
// C:\Ultima Online Classic), so every fact here is [SkippableFact] and skips without tile data, as pinned's own
// pathfinding tests do (Fixtures/TileDataRequirement.cs). With tile data (the builder image run by hand with the
// client folder mounted and MODERNUO_TEST_DATA_DIR set, notes section 5) they:
//   1. check the real adapter (Map.Tiles and TileData) against tiles read independently from the UOP by name hash.
//      cc-P675: on the Siege of Sosaria map (the server's map overlay mounted over the client folder; shard-migration
//      notes/cc-P675-tools/spots.py read them): Trammel 3503,2574 (stock New Haven, forest now) is land 0x0006 Z 14 with
//      no statics, and Valunstead 479,388 holds floors 0x04AB at Z 20 and 40 and roof 0x0596 at 49 on land 0x0006 Z 15.
//      It was cc-P134's stock read (notes/cc-P134-vseuoaware-art-checks-mcp.md section 4) until the swap;
//   2. run [ValidateSpots' rule over the twelve spawn files the World setup tab lists and over every decoration file
//      pinned's [Decorate reads, and over New Haven's statics and shore, writing the reports to SL_MAPCHECK_REPORT_DIR
//      when it is set (the notes quote them). These report; they fail only if a file cannot be read.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MapCheckRealDataVerification
{
    private readonly ITestOutputHelper _out;

    public MapCheckRealDataVerification(ITestOutputHelper output) => _out = output;

    // The spawn files the shard imports (scripts/Shard-Console.ps1 World setup, notes/reachability-audit.md section 5).
    private static readonly string[] SpawnFiles =
    [
        "Spawns/post-uoml/termur/Abyss.json", "Spawns/post-uoml/termur/TerMur.json", "Spawns/post-uoml/termur/Underworld.json",
        "Spawns/shared/malas/Citadel.json", "Spawns/shared/malas/Labyrinth.json",
        "Spawns/shared/trammel/Sanctuary.json", "Spawns/shared/felucca/Sanctuary.json",
        "Spawns/shared/trammel/PrismOfLight.json", "Spawns/shared/felucca/PrismOfLight.json",
        "Spawns/post-uoml/trammel/Vendors.json", "Spawns/post-uoml/felucca/Vendors.json",
        "Spawns/post-uoml/trammel/Outdoors.json"
    ];

    // Decorate.cs:37-46: which maps each folder decorates.
    private static readonly (string Folder, string[] Maps)[] DecorationFolders =
    [
        ("Decoration/Britannia", ["Trammel", "Felucca"]), ("Decoration/Trammel", ["Trammel"]), ("Decoration/Felucca", ["Felucca"]),
        ("Decoration/Ilshenar", ["Ilshenar"]), ("Decoration/Malas", ["Malas"]), ("Decoration/Tokuno", ["Tokuno"])
    ];

    /// <summary>The Data folder: beside the test host, or pinned's Distribution/Data above it (the builder image).</summary>
    private static string FindData()
    {
        var dir = new DirectoryInfo(Core.BaseDirectory);
        for (var d = dir; d != null; d = d.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(d.FullName, "Data", "Spawns"), Path.Combine(d.FullName, "Distribution", "Data", "Spawns") })
            {
                if (Directory.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }
            }
        }

        return null;
    }

    private void Report(string name, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        foreach (var l in list)
        {
            _out.WriteLine(l);
        }

        var dir = Environment.GetEnvironmentVariable("SL_MAPCHECK_REPORT_DIR");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, name), list);
        }
    }

    [SkippableFact]
    public void TheRealAdapterReadsTheSosMapAsTheIndependentUopReadDid()
    {
        TileDataRequirement.SkipIfMissing();
        var src = new ClusterFMapTileSource(Map.Trammel);
        var land = src.GetLandTile(3503, 2574);
        Assert.Equal(0x0006, land.ID);
        Assert.Equal(14, land.Z);
        Assert.Empty(src.GetStaticTiles(3503, 2574));
        Assert.Equal(0x0006, src.GetLandTile(479, 388).ID);
        Assert.Equal(15, src.GetLandTile(479, 388).Z);
        var statics = src.GetStaticTiles(479, 388).Select(s => (s.ID, s.Z)).OrderBy(s => s.Z).ToArray();
        Assert.Equal(new[] { (0x04AB, 20), (0x04AB, 40), (0x0596, 49) }, statics);
        // And the flags come from the real tiledata: deep water is Wet, grass is not, a wall is Impassable.
        Assert.True((src.LandFlags(0x00A8) & TileFlag.Wet) != 0, "0x00A8 water is Wet");
        Assert.True((src.LandFlags(0x0003) & (TileFlag.Wet | TileFlag.Impassable)) == 0, "0x0003 grass is open");
        Assert.True((src.ItemFlags(0x0080) & TileFlag.Impassable) != 0, "0x0080 wall is Impassable");
        Assert.True(src.HasItemData(0x0080));
        Assert.Null(ClusterFMapCheck.Blocked(src, 3503, 2574));
    }

    [SkippableFact]
    public void ValidateSpotsOverThePinnedSpawnFiles()
    {
        TileDataRequirement.SkipIfMissing();
        var data = FindData();
        Skip.If(data == null, "no Data/Spawns beside the test host");
        var lines = new List<string> { $"[ValidateSpots over the twelve world-setup spawn files in {data}" };
        var totalSpots = 0;
        var totalProblems = 0;
        foreach (var rel in SpawnFiles)
        {
            var path = Path.Combine(data, rel);
            Assert.True(File.Exists(path), path);
            var spots = ClusterFMapCheck.ParseSpawnerJson(File.ReadAllText(path), null, int.MaxValue);
            ClusterFMapCheck.Validate(spots, ClusterFMapTileSource.ForMapName);
            totalSpots += spots.Spots.Count;
            totalProblems += spots.Problems;
            lines.Add("");
            lines.Add($"== {rel}");
            lines.AddRange(spots.Lines(int.MaxValue));
        }

        lines.Insert(1, $"{totalSpots} spawner locations, {totalProblems} with problems.");
        Report("validate-spots-spawns.txt", lines);
    }

    [SkippableFact]
    public void ValidateSpotsOverThePinnedDecorationFiles()
    {
        TileDataRequirement.SkipIfMissing();
        var data = FindData();
        Skip.If(data == null, "no Data beside the test host");
        var lines = new List<string> { $"[ValidateSpots over every decoration .cfg pinned's [Decorate reads, in {data}" };
        var totalSpots = 0;
        var totalProblems = 0;
        var byCause = new SortedDictionary<string, int>();
        foreach (var (folder, maps) in DecorationFolders)
        {
            var dir = Path.Combine(data, folder);
            if (!Directory.Exists(dir))
            {
                lines.Add($"== {folder}: no such folder");
                continue;
            }

            foreach (var file in Directory.GetFiles(dir, "*.cfg").OrderBy(f => f, StringComparer.Ordinal))
            {
                var spots = ClusterFMapCheck.ParseDecorationCfg(File.ReadAllLines(file), maps, int.MaxValue);
                ClusterFMapCheck.Validate(spots, ClusterFMapTileSource.ForMapName);
                totalSpots += spots.Spots.Count;
                totalProblems += spots.Problems;
                foreach (var s in spots.Spots.Where(s => s.Problem != null))
                {
                    var cause = s.Problem.StartsWith("blocked by static") ? "blocked by a static" : s.Problem.StartsWith("impassable land") ? "impassable land" : s.Problem;
                    byCause[cause] = byCause.GetValueOrDefault(cause) + 1;
                }

                lines.Add("");
                lines.Add($"== {folder}/{Path.GetFileName(file)} ({string.Join(", ", maps)})");
                lines.AddRange(spots.Lines(40));
            }
        }

        lines.Insert(1, $"{totalSpots} decoration spots (one per map a folder decorates), {totalProblems} with problems: " +
                        string.Join(", ", byCause.Select(k => $"{k.Key} x{k.Value}")));
        Report("validate-spots-decoration.txt", lines);
    }

    [SkippableFact]
    public void NewHavenStaticsWalkabilityAndShore()
    {
        TileDataRequirement.SkipIfMissing();
        // New Haven and its coast: Trammel 3400..3700 x 2400..2700, 300x300, inside the 512x512 cap.
        var src = new ClusterFMapTileSource(Map.Trammel);
        var statics = ClusterFMapCheck.FindStaticProblems(src, 3400, 2400, 300, 300);
        var walk = ClusterFMapCheck.CheckWalkable(src, 3400, 2400, 300, 300);
        var shore = ClusterFMapCheck.CheckShore(src, 3400, 2400, 300, 300);
        Assert.Null(statics.Refused);
        Assert.Null(walk.Refused);
        Assert.Null(shore.Refused);
        var lines = new List<string> { "[FindStaticProblems, [Walkable and [ShoreCheck over New Haven, Trammel (3400,2400) 300x300", "" };
        lines.AddRange(statics.Lines(int.MaxValue));
        lines.Add("");
        lines.AddRange(walk.Lines(0));
        lines.Add("");
        lines.AddRange(shore.Lines(40));
        // A footprint where the New Haven bank stands cannot be placed (walls), and one on the open field north can.
        lines.Add("");
        lines.AddRange(ClusterFMapCheck.CheckFootprint(src, 3503, 2574, 8, 8).Lines());
        Report("new-haven.txt", lines);
    }
}
