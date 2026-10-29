// SosNewSpawnsVerification.cs
//
// cc-P682 (SoS swap S10): the people of the author's new towns and keeps, and the wild on his new land. The author ships
// no spawns; shard-migration notes/cc-P682-tools/build_spawns.py surveys the SoS map and writes server/data/Spawns/sos/
// {towns,keeps,wild}/*.json in pinned's spawn format, which the Dockerfile puts beside pinned's own in Data/Spawns and the
// console's fresh-world order builds with the stock [GenerateSpawners Data/Spawns/sos/**/*.json. The map side (every
// home walkable, services indoors and reachable, wild clear of towns and closures, density against stock's) is the docs
// repo's tools/tests/test_sos_new_spawns.py; these are the shard's side.
//
// Facts:
//   1. Every file of the set is a list of Spawner records on Trammel or Felucca, every guid unique and none of stock's,
//      every entry a mobile type the server has.
//   2. On each facet every new town has a banker, a healer, a smith and a provisioner, at a home inside the town's region
//      (Data/ShatteredLegacy/regions-trammel.json; Felucca stands on the same SoS land).
//   3. A real [GenerateSpawners of the set, on Trammel and on Felucca, builds every record where the file puts it, with
//      its count and ranges: none of the import's hooks (Despise, Haven, S9's placements) refuses or moves one.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosNewSpawnsVerification
{
    private static readonly string[] NewTowns =
        ["Valunstead", "Hammerhill", "Kameron Kove", "Elmhaven", "Kettleburg", "Fort Yondall"];

    private static readonly string[] CoreRoles = ["Banker", "Healer", "Blacksmith", "Provisioner"];

    private readonly ITestOutputHelper _out;

    public SosNewSpawnsVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
    }

    private sealed record Row(string File, Guid Guid, string Map, Point3D Location, int Count, int HomeRange, string[] Names);

    private static string SetDirectory => Path.Combine(ClusterFStockSpawnData.SpawnsDirectory, "sos");

    private static List<Row> Rows()
    {
        var rows = new List<Row>();
        foreach (var file in Directory.EnumerateFiles(SetDirectory, "*.json", SearchOption.AllDirectories).Order())
        {
            var rel = Path.GetRelativePath(SetDirectory, file).Replace('\\', '/');
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            foreach (var s in doc.RootElement.EnumerateArray())
            {
                Assert.Equal("Spawner", s.GetProperty("$type").GetString());
                var loc = s.GetProperty("location").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                rows.Add(new Row(
                    rel,
                    Guid.Parse(s.GetProperty("guid").GetString()!),
                    s.GetProperty("map").GetString()!,
                    new Point3D(loc[0], loc[1], loc[2]),
                    s.GetProperty("count").GetInt32(),
                    s.GetProperty("homeRange").GetInt32(),
                    s.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray()
                ));
            }
        }

        return rows;
    }

    private static Dictionary<string, List<(int X1, int Y1, int X2, int Y2)>> TownRects()
    {
        var path = Path.Combine(Server.Core.BaseDirectory, "Data", "ShatteredLegacy", "regions-trammel.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<string, List<(int, int, int, int)>>();
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("Name", out var n) && NewTowns.Contains(n.GetString()))
            {
                result[n.GetString()!] = r.GetProperty("Area").EnumerateArray()
                    .Select(a => (a.GetProperty("x1").GetInt32(), a.GetProperty("y1").GetInt32(),
                        a.GetProperty("x2").GetInt32(), a.GetProperty("y2").GetInt32()))
                    .ToList();
            }
        }

        return result;
    }

    private static Map FacetMap(string facet) => facet == "Felucca" ? Map.Felucca : Map.Trammel;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryRecordIsASpawnerOfMobilesOnTrammelOrFeluccaWithItsOwnGuid()
    {
        var rows = Rows();
        _out.WriteLine($"{rows.Count} spawners in {rows.Select(r => r.File).Distinct().Count()} files");
        Assert.True(rows.Count > 500, $"{rows.Count} spawners");
        Assert.All(rows, r => Assert.Contains(r.Map, new[] { "Trammel", "Felucca" }));
        Assert.Equal(rows.Count, rows.Select(r => r.Guid).Distinct().Count());

        var stock = new HashSet<Guid>();
        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            stock.UnionWith(ClusterFStockSpawnData.Load(map, "post-uoml", "shared", "uoml").Select(s => s.Guid));
        }

        Assert.DoesNotContain(rows, r => stock.Contains(r.Guid));

        foreach (var name in rows.SelectMany(r => r.Names).Distinct())
        {
            var type = AssemblyHandler.FindTypeByName(name);
            Assert.True(type != null && typeof(Mobile).IsAssignableFrom(type), $"{name} is not a mobile type");
        }
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData("Trammel")]
    [InlineData("Felucca")]
    public void EveryNewTownHasItsBankHealerSmithAndProvisionerInsideTheTown(string facet)
    {
        var rows = Rows();
        var rects = TownRects();
        foreach (var town in NewTowns)
        {
            Assert.True(rects.ContainsKey(town), $"{town} region");
            var file = "towns/" + town.Replace(" ", "") + ".json";
            foreach (var role in CoreRoles)
            {
                var have = rows.Where(r => r.File == file && r.Map == facet && r.Names.Contains(role)).ToList();
                Assert.True(have.Count > 0, $"{facet} {town}: no {role}");
                Assert.All(have, r => Assert.True(
                    rects[town].Any(a => r.Location.X >= a.X1 && r.Location.X < a.X2 && r.Location.Y >= a.Y1 && r.Location.Y < a.Y2),
                    $"{facet} {town} {role} at {r.Location} is outside the town"));
            }
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData("Trammel")]
    [InlineData("Felucca")]
    public void ARealImportBuildsEveryRecordWhereTheFilePutsIt(string facet)
    {
        var map = FacetMap(facet);
        var all = Rows();
        var rows = all.Where(r => r.Map == facet).ToList();
        var guids = all.Select(r => r.Guid).ToHashSet(); // the import builds both facets' records: clean up both

        List<BaseSpawner> InWorld() =>
            World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && guids.Contains(s.Guid)).ToList();

        var dev = new PlayerMobile { AccessLevel = AccessLevel.Developer };
        dev.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        try
        {
            foreach (var s in InWorld())
            {
                s.Delete();
            }

            Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/sos/**/*.json"));
            var built = InWorld().Where(s => s.Map == map).ToDictionary(s => s.Guid);
            _out.WriteLine($"{facet}: {rows.Count} records, {built.Count} built");

            foreach (var row in rows)
            {
                Assert.True(built.TryGetValue(row.Guid, out var s), $"{row.File} {row.Guid} was not built");
                Assert.Equal(row.Location, s.Location);
                Assert.Equal(row.Count, s.Count);
                Assert.Equal(row.HomeRange, s.HomeRange);
                Assert.Equal(row.Names.Length, s.Entries.Count);
            }
        }
        finally
        {
            foreach (var s in InWorld())
            {
                s.RemoveSpawns();
                s.Delete();
            }

            dev.Delete();
        }
    }
}
