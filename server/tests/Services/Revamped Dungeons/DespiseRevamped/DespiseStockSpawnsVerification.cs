// DespiseStockSpawnsVerification.cs
//
// cc-P42 Part J (bug-list D53; Chase, 2026-10-03). Trammel Despise runs the revamp only. Upstream's
// Data/Spawns/shared/trammel/Despise.json (43 spawners: 30 creature-only, 11 treasure-chest-only, 2 mixed) placed
// ettins, lizardmen and elementals beside the revamp's 47. The importer now keeps that file's chests and drops its
// creatures (ImportSpawners-despise-exclusion.patch into ClusterFDespiseStockSpawns.Admit), and
// [ClusterFDespiseStockCleanup does the same to a world that already has them. Felucca Despise stays classic.
//
// Facts:
//   1. The file is what the brief says: 43 Trammel spawners, 30 creature-only, 11 chest-only, 2 mixed.
//   2. A fresh import of the real Trammel file places no creature entry, all 11 chest spawners and the 2 mixed ones
//      with their chests only; the real Felucca file places all 43 with their creatures.
//   3. On a world that has the stock spawners (as an old import left them), beside the revamp's 47 and Felucca's 43:
//      the dry run changes nothing and names 30 deletions and 2 strips; the real run then deletes the 30, strips the
//      2 to their chests, keeps the 11, and leaves the revamp's 47 and Felucca's 43 alone; a second dry run finds
//      nothing.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Despise;
using Server.Engines.Spawners;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DespiseStockSpawnsVerification
{
    private const string FeluccaFile = "shared/felucca/Despise.json";

    private readonly ITestOutputHelper _out;

    public DespiseStockSpawnsVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
    }

    private static List<ClusterFStockSpawnData.StockSpawner> Trammel => ClusterFDespiseStockSpawns.TrammelStock();

    private static List<ClusterFStockSpawnData.StockSpawner> Felucca =>
        ClusterFStockSpawnData.LoadRelative(FeluccaFile, Map.Felucca);

    private static string Kind(IReadOnlyCollection<string> names)
    {
        var chests = names.Count(ClusterFDespiseStockSpawns.IsChest);
        return chests == names.Count ? "chest" : chests == 0 ? "creature" : "mixed";
    }

    private static List<BaseSpawner> InWorld(IEnumerable<ClusterFStockSpawnData.StockSpawner> stock, Map map)
    {
        var guids = stock.Select(s => s.Guid).ToHashSet();
        return World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && s.Map == map && guids.Contains(s.Guid)).ToList();
    }

    private static void DeleteAll(IEnumerable<BaseSpawner> spawners)
    {
        foreach (var s in spawners.ToList())
        {
            s.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheStockFileIsFortyThreeSpawners()
    {
        var kinds = Trammel.GroupBy(s => Kind(s.Names)).ToDictionary(g => g.Key, g => g.Count());
        _out.WriteLine(string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}")));
        Assert.Equal(43, Trammel.Count);
        Assert.Equal(30, kinds["creature"]);
        Assert.Equal(11, kinds["chest"]);
        Assert.Equal(2, kinds["mixed"]);
        Assert.Equal(43, Felucca.Count);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AFreshImportPlacesTheChestsAndNoCreaturesInTrammelOnly()
    {
        var dev = new PlayerMobile { AccessLevel = AccessLevel.Developer };
        dev.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);

        try
        {
            Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/{ClusterFDespiseStockSpawns.TrammelFile}"));
            Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/{FeluccaFile}"));

            var tram = InWorld(Trammel, Map.Trammel);
            _out.WriteLine($"Trammel placed {tram.Count}: " +
                           string.Join("; ", tram.Select(s => $"{s.Location} {string.Join("+", s.Entries.Select(e => e.SpawnedName))}")));

            Assert.Equal(13, tram.Count);
            Assert.All(tram, s => Assert.All(s.Entries, e => Assert.True(ClusterFDespiseStockSpawns.IsChest(e.SpawnedName), e.SpawnedName)));

            var mixedGuids = Trammel.Where(s => Kind(s.Names) == "mixed").Select(s => s.Guid).ToList();
            foreach (var g in mixedGuids)
            {
                var placed = tram.Single(s => s.Guid == g);
                Assert.Equal(Trammel.Single(s => s.Guid == g).Names.Count(ClusterFDespiseStockSpawns.IsChest), placed.Entries.Count);
            }

            var fel = InWorld(Felucca, Map.Felucca);
            Assert.Equal(43, fel.Count);
            Assert.Equal(32, fel.Count(s => s.Entries.Any(e => !ClusterFDespiseStockSpawns.IsChest(e.SpawnedName))));
        }
        finally
        {
            DeleteAll(InWorld(Trammel, Map.Trammel));
            DeleteAll(InWorld(Felucca, Map.Felucca));
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    private static Spawner Place(ClusterFStockSpawnData.StockSpawner s, Map map)
    {
        var spawner = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, s.Names) { Guid = s.Guid };
        spawner.Stop();
        spawner.MoveToWorld(s.Location, map);
        return spawner;
    }

    [Fact]
    public void TheCleanupRemovesTheStockCreaturesFromAWorldThatHasThem()
    {
        DespiseRevampedSetup.DeleteDespise();
        var revamp = DespiseSpawns.Generate();
        Assert.Equal(47, revamp.Count);

        var tram = Trammel.Select(s => Place(s, Map.Trammel)).ToList();
        var fel = Felucca.Select(s => Place(s, Map.Felucca)).ToList();

        try
        {
            var items = World.Items.Count;
            var dry = ClusterFDespiseStockSpawns.Run(true);
            _out.WriteLine(dry.Last());
            Assert.Equal(items, World.Items.Count);
            Assert.All(tram, s => Assert.False(s.Deleted));
            Assert.Equal(30, dry.Count(l => l.StartsWith("[DRY RUN] would delete", StringComparison.Ordinal)));
            Assert.Equal(2, dry.Count(l => l.StartsWith("[DRY RUN] would strip", StringComparison.Ordinal)));
            Assert.EndsWith("would delete 30 creature spawners, would strip the creatures from 2 mixed ones, kept 11 chest spawners.", dry.Last());

            var real = ClusterFDespiseStockSpawns.Run(false);
            _out.WriteLine(real.Last());
            Assert.EndsWith("deleted 30 creature spawners, stripped the creatures from 2 mixed ones, kept 11 chest spawners.", real.Last());

            var left = tram.Where(s => !s.Deleted).ToList();
            Assert.Equal(13, left.Count);
            Assert.All(left, s => Assert.All(s.Entries, e => Assert.True(ClusterFDespiseStockSpawns.IsChest(e.SpawnedName))));
            Assert.All(tram.Where(s => Kind(Trammel.Single(t => t.Guid == s.Guid).Names) == "creature"), s => Assert.True(s.Deleted));

            Assert.All(revamp, s => Assert.False(s.Deleted));
            Assert.All(fel, s => Assert.False(s.Deleted));
            Assert.All(fel, s => Assert.Equal(Felucca.Single(f => f.Guid == s.Guid).Names.Length, s.Entries.Count));

            var again = ClusterFDespiseStockSpawns.Run(true);
            Assert.EndsWith("would delete 0 creature spawners, would strip the creatures from 0 mixed ones, kept 13 chest spawners.", again.Last());
        }
        finally
        {
            DeleteAll(tram);
            DeleteAll(fel);
            DespiseRevampedSetup.DeleteDespise();
        }
    }
}
