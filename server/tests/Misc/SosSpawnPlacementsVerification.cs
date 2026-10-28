// SosSpawnPlacementsVerification.cs
//
// cc-P681 (SoS swap S9): pinned's stock spawners on the Siege of Sosaria land. shard-migration
// notes/cc-P681-tools/regrade.py graded every one on the SoS map files and wrote ClusterFSosSpawnPlacementsTable.cs; the
// importer asks ClusterFSosSpawnPlacements.Place before it builds each spawner (server/patches/
// ImportSpawners-sos-placements.patch). The map side (every home walkable, every town keeps its services) is the docs
// repo's tools/tests/test_sos_spawners.py; these are the shard's side.
//
// Facts:
//   1. Every moved and every dropped spawner is a stock spawner of the shipped files the shard imports, on that facet
//      (post-uoml/** and shared/**); none is both moved and dropped; no two moves name one spawner.
//   2. A real [GenerateSpawners of a stock file, on Trammel and on Felucca, builds each moved spawner at its new home,
//      builds no dropped one, and builds every other spawner of the file where the data puts it.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosSpawnPlacementsVerification
{
    private readonly ITestOutputHelper _out;

    public SosSpawnPlacementsVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
    }

    private static Map FacetMap(string facet) => facet == "Felucca" ? Map.Felucca : Map.Trammel;

    private static List<ClusterFStockSpawnData.StockSpawner> Stock(Map map) =>
        ClusterFStockSpawnData.Load(map, ClusterFStockSpawnData.ShardFolders);

    private static List<BaseSpawner> InWorld(Map map, ISet<Guid> guids) =>
        World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && s.Map == map && guids.Contains(s.Guid)).ToList();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryRowIsAStockSpawnerOfItsFacetOnceAndEitherMovedOrDropped()
    {
        Assert.NotEmpty(ClusterFSosSpawnPlacements.Moves);
        Assert.NotEmpty(ClusterFSosSpawnPlacements.Drops);

        foreach (var facet in new[] { "Trammel", "Felucca" })
        {
            var map = FacetMap(facet);
            var stock = Stock(map).Select(s => s.Guid).ToHashSet();
            var moves = ClusterFSosSpawnPlacements.Moves.Where(m => m.Facet == facet).Select(m => Guid.Parse(m.Guid)).ToList();
            var drops = ClusterFSosSpawnPlacements.Drops.Where(d => d.Facet == facet).Select(d => Guid.Parse(d.Guid)).ToList();
            _out.WriteLine($"{facet}: {stock.Count} stock spawners, {moves.Count} moved, {drops.Count} dropped");

            Assert.All(moves.Concat(drops), g => Assert.Contains(g, stock));
            Assert.Equal(moves.Count, moves.Distinct().Count());
            Assert.Equal(drops.Count, drops.Distinct().Count());
            Assert.Empty(moves.Intersect(drops));
            Assert.All(moves, g => Assert.NotNull(ClusterFSosSpawnPlacements.NewHome(map, g)));
            Assert.All(drops, g => Assert.True(ClusterFSosSpawnPlacements.IsDropped(map, g)));
        }

        Assert.Contains(ClusterFSosSpawnPlacements.Moves, m => m.Facet == "Felucca");
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData("Trammel")]
    [InlineData("Felucca")]
    public void ARealImportBuildsMovedSpawnersAtTheirNewHomeAndNoDroppedOne(string facet)
    {
        var map = FacetMap(facet);
        var stock = Stock(map);
        var moved = ClusterFSosSpawnPlacements.Moves.Where(m => m.Facet == facet).Select(m => Guid.Parse(m.Guid)).ToHashSet();
        var dropped = ClusterFSosSpawnPlacements.Drops.Where(d => d.Facet == facet).Select(d => Guid.Parse(d.Guid)).ToHashSet();

        // the stock file with the most moves and drops together
        var file = stock.GroupBy(s => s.File)
            .Select(g => (File: g.Key, Moves: g.Count(s => moved.Contains(s.Guid)), Drops: g.Count(s => dropped.Contains(s.Guid))))
            .Where(f => f.Moves > 0 && f.Drops > 0 && !f.File.Contains("Despise")) // Despise has its own import hook
            .OrderByDescending(f => f.Moves + f.Drops)
            .First();
        var rows = stock.Where(s => s.File == file.File).ToList();
        var guids = rows.Select(s => s.Guid).ToHashSet();
        _out.WriteLine($"{facet}: {file.File}, {rows.Count} spawners, {file.Moves} moved, {file.Drops} dropped");

        var dev = new PlayerMobile { AccessLevel = AccessLevel.Developer };
        dev.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        try
        {
            foreach (var s in InWorld(map, guids))
            {
                s.Delete();
            }

            Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/{file.File}"));
            var built = InWorld(map, guids).ToDictionary(s => s.Guid);

            foreach (var row in rows)
            {
                if (dropped.Contains(row.Guid))
                {
                    Assert.False(built.ContainsKey(row.Guid), $"dropped {row.Guid} was built");
                }
                else if (moved.Contains(row.Guid))
                {
                    Assert.True(built.TryGetValue(row.Guid, out var s), $"moved {row.Guid} was not built");
                    Assert.Equal(ClusterFSosSpawnPlacements.NewHome(map, row.Guid).Value, s.Location);
                    Assert.NotEqual(row.Location, s.Location);
                }
                else
                {
                    Assert.True(built.TryGetValue(row.Guid, out var s), $"{row.Guid} was not built");
                    Assert.Equal(row.Location, s.Location);
                }
            }

            Assert.Equal(rows.Count - file.Drops, built.Count);
        }
        finally
        {
            foreach (var s in InWorld(map, guids))
            {
                s.RemoveSpawns();
                s.Delete();
            }

            dev.Delete();
        }
    }
}
