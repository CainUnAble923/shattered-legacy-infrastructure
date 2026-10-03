// OldHavenCleanupVerification.cs
//
// cc-P42 Part B (P21 section 7 defect 2; Chase, 2026-10-02: fix it). [ClusterFOldHavenCleanup's spawner box starts
// at X 3520 and New Haven reaches X 3545 (3559 at its Necromancers Guild Hall), and BlacksmithGuildmaster is on its
// list, so a real run deleted pinned's New Haven Blacksmith Guildmaster spawner at 3526,2536,20. The box also holds
// pinned's Healer spawner in the ruins (post-uoml/trammel/Outdoors.json, 3618,2614,0), within 8 tiles of the
// Healer it hunts at 3617,2614, so it took that one and its Healer too. The fix: any spawner pinned's spawn files
// place (ClusterFStockSpawnData: post-uoml/** and shared/**, by guid, or by x,y,z and names for one placed before
// guids were kept), and any NPC such a spawner spawned, is left alone.
//
// Facts:
//   1. Of the 40 stock Trammel spawners inside the box, exactly two spawn a type on the cleanup's list: the
//      Blacksmith Guildmaster (Vendors.json, 3526,2536,20) and the ruins' Healer (Outdoors.json, 3618,2614,0).
//   2. A real run keeps both, a same-place copy of the guildmaster's with a new guid, and the stock Healer's NPC;
//      it deletes the stray vendor spawners and the stray guildmaster it is meant to remove.
//   3. The dry run changes nothing and reports the same counts and the same kept spawners as the real run.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Spawners;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class OldHavenCleanupVerification
{
    private readonly ITestOutputHelper _out;

    public OldHavenCleanupVerification(ITestOutputHelper output) => _out = output;

    private static List<ClusterFStockSpawnData.StockSpawner> ListedStock() =>
        ClusterFOldHavenCleanup.StockInBox()
            .Where(s => s.Names.Any(n => ClusterFOldHavenCleanup.SpawnerVendorTypes.Contains(n)))
            .ToList();

    [Fact]
    public void TwoStockSpawnersInTheBoxSpawnAListedType()
    {
        var inBox = ClusterFOldHavenCleanup.StockInBox();
        foreach (var s in inBox)
        {
            _out.WriteLine($"{s.File} {s.Location} [{string.Join(", ", s.Names)}]");
        }

        Assert.Equal(40, inBox.Count);

        var listed = ListedStock().Select(s => $"{s.File} {s.Location.X},{s.Location.Y},{s.Location.Z} {string.Join("+", s.Names)}")
            .Order().ToList();
        Assert.Equal(
            [
                "post-uoml/trammel/Outdoors.json 3618,2614,0 Healer",
                "post-uoml/trammel/Vendors.json 3526,2536,20 BlacksmithGuildmaster"
            ],
            listed
        );
    }

    private sealed class Scene : IDisposable
    {
        public readonly List<IEntity> Made = [];
        public readonly List<Spawner> Stock = [];
        public Spawner OldGuidCopy;
        public Healer StockHealerNpc;
        public Spawner StrayGuildmasterSpawner, StrayHealerSpawner;
        public BlacksmithGuildmaster StrayGuildmaster;

        private T Add<T>(T e) where T : IEntity
        {
            Made.Add(e);
            return e;
        }

        private Spawner Place(string name, Point3D at)
        {
            var spawner = Add(new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, name));
            spawner.Stop();
            spawner.MoveToWorld(at, Map.Trammel);
            return spawner;
        }

        public Scene()
        {
            foreach (var s in ListedStock())
            {
                var spawner = Place(s.Names[0], s.Location);
                spawner.Guid = s.Guid; // as the importer leaves it (BaseSpawner.Dto.cs:25)
                Stock.Add(spawner);
            }

            // A copy of the guildmaster's on its tile with a fresh guid: a spawner placed before guids were kept.
            OldGuidCopy = Place("BlacksmithGuildmaster", new Point3D(3526, 2536, 20));

            // The stock Healer's NPC, standing on the Healer target the cleanup hunts (3617,2614, 8 tiles).
            StockHealerNpc = Add(new Healer());
            StockHealerNpc.MoveToWorld(new Point3D(3617, 2614, 0), Map.Trammel);
            StockHealerNpc.Spawner = Stock.Single(s => s.Location.X == 3618);

            // What the cleanup is for: stray vendor spawners in the ruins and a stray guildmaster.
            StrayGuildmasterSpawner = Place("BlacksmithGuildmaster", new Point3D(3596, 2595, 0));
            StrayHealerSpawner = Place("Healer", new Point3D(3640, 2600, 0));
            StrayGuildmaster = Add(new BlacksmithGuildmaster());
            StrayGuildmaster.MoveToWorld(new Point3D(3645, 2615, 0), Map.Trammel);
        }

        public IEnumerable<IEntity> Kept => [..Stock, OldGuidCopy, StockHealerNpc];
        public IEnumerable<IEntity> Strays => [StrayGuildmasterSpawner, StrayHealerSpawner, StrayGuildmaster];

        public void Dispose()
        {
            foreach (var e in Made)
            {
                e.Delete();
            }
        }
    }

    private static string Summary(List<string> lines) =>
        lines.Single(l => l.StartsWith("ClusterF Old Haven cleanup", StringComparison.Ordinal));

    private static List<string> KeptLines(List<string> lines) =>
        lines.Where(l => l.StartsWith("[STOCK, KEPT]", StringComparison.Ordinal)).Order().ToList();

    [Fact]
    public void ARealRunKeepsNewHavensSpawnersAndRemovesTheStrays()
    {
        using var w = new Scene();
        Assert.Equal(2, w.Stock.Count);

        var dry = ClusterFOldHavenCleanup.Run(true);
        foreach (var l in dry.Where(l => !l.StartsWith("[NOT FOUND]", StringComparison.Ordinal)))
        {
            _out.WriteLine($"dry: {l}");
        }

        Assert.All(w.Kept.Concat(w.Strays), e => Assert.False(e.Deleted));
        Assert.Contains(dry, l => l.StartsWith("[DRY RUN] Spawner at (3596,2595)", StringComparison.Ordinal));
        Assert.Contains(dry, l => l.StartsWith("[DRY RUN] Spawner at (3640,2600)", StringComparison.Ordinal));
        Assert.Contains(dry, l => l.StartsWith("[DRY RUN] NPC: BlacksmithGuildmaster", StringComparison.Ordinal) && l.EndsWith("(3645,2615)"));
        Assert.DoesNotContain(dry, l => l.StartsWith("[DRY RUN] NPC: Healer", StringComparison.Ordinal) && l.EndsWith("(3617,2614)"));
        Assert.DoesNotContain(dry, l => l.StartsWith("[DRY RUN] Spawner at (3526,2536)", StringComparison.Ordinal));
        Assert.DoesNotContain(dry, l => l.StartsWith("[DRY RUN] Spawner at (3618,2614)", StringComparison.Ordinal));
        Assert.True(KeptLines(dry).Count(l => l.Contains("(3526,2536,20)") || l.Contains("(3618,2614,0)")) >= 3);

        var real = ClusterFOldHavenCleanup.Run(false);
        _out.WriteLine($"real: {Summary(real)}");

        Assert.All(w.Kept, e => Assert.False(e.Deleted, $"{e.GetType().Name} at {e.Location} was deleted"));
        Assert.All(w.Strays, e => Assert.True(e.Deleted, $"{e.GetType().Name} at {e.Location} survived"));

        // The dry run said what the real run did.
        Assert.Equal(Summary(dry).Replace("dry run", "complete").Replace("Would delete", "Deleted"), Summary(real));
        Assert.Equal(KeptLines(dry), KeptLines(real));
    }
}
