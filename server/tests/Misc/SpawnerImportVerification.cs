// SpawnerImportVerification.cs
//
// cc-P32 Part A, PT-07: pinned's JSON spawner import deleted every spawner of the same type on the same
// x,y before placing each one (ImportSpawnersCommand.cs:228-242). GetItemsAt matches x,y only
// (Map.ItemEnumerator.cs:31), and pinned's own data stacks spawners on one tile, at z, z+1, z+2 and also at
// the very same z, so each later one deleted the one before with what it had spawned. 338 of the 5,532
// spawners in shared/** and post-uoml/** were lost that way, 8 of them New Haven vendors.
// server/patches/ImportSpawners-guid-key.patch removes that delete; the import's own guid match
// (:251-254) still replaces the same spawner on a re-run. Notes: notes/cc-P32-spawner-importer-bods-self-repair.md.
//
// Facts, all through the real command as a Developer types it:
//   1. Spawners stacked on one x,y, at different z and at the same z, all survive an import.
//   2. Importing the same file again leaves exactly the same spawners: each one replaced by its guid, the
//      old object deleted, nothing added.
//   3. The same spawner (the data's guid) on the same x,y,z is still replaced, and a spawner the data does
//      not name on that tile is left alone.
//   4. Pinned's post-uoml/trammel/Vendors.json, imported for real, keeps every one of its spawners, the
//      eight New Haven vendors P17 found missing among them, and a second import changes nothing.
//   5. With pinned's Healer spawner back, the New Haven services seeder retires its own stand-in (P17's,
//      at 3462,2559,35) and places no second one; its dry run only reports it.

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
public class SpawnerImportVerification
{
    private const string TempFolder = "cc-p32-spawns";
    private const string VendorsFile = "Data/Spawns/post-uoml/trammel/Vendors.json";

    private readonly ITestOutputHelper _out;

    public SpawnerImportVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
    }

    // ---------------------------------------------------------------- helpers

    private static PlayerMobile NewDeveloper()
    {
        var pm = new PlayerMobile { AccessLevel = AccessLevel.Developer };
        pm.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        return pm;
    }

    private static void Import(Mobile dev, string pattern) =>
        Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners {pattern}"));

    private sealed record Entry(Guid Guid, int X, int Y, int Z, string Spawns);

    // A spawn file in pinned's format, under Core.BaseDirectory where the command looks. Returns its pattern.
    private static string WriteSpawnFile(string name, IEnumerable<Entry> entries)
    {
        var dir = Path.Combine(Core.BaseDirectory, TempFolder);
        Directory.CreateDirectory(dir);

        var json = entries.Select(e => new Dictionary<string, object>
        {
            ["$type"] = "Spawner", // cc-P30: upstream #2505 discriminator (was "type" at 7c9215d97)
            ["guid"] = e.Guid.ToString(),
            ["name"] = "cc-P32 test",
            ["location"] = new[] { e.X, e.Y, e.Z },
            ["map"] = "Trammel",
            ["count"] = 1,
            ["homeRange"] = 0,
            ["walkingRange"] = 0,
            ["entries"] = new[] { new Dictionary<string, object> { ["name"] = e.Spawns, ["maxCount"] = 1, ["probability"] = 100 } }
        });

        File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(json));
        return $"{TempFolder}/{name}";
    }

    private static List<BaseSpawner> SpawnersWith(ISet<Guid> guids) =>
        World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && guids.Contains(s.Guid)).ToList();

    private static void DeleteSpawners(ISet<Guid> guids)
    {
        foreach (var s in SpawnersWith(guids))
        {
            s.Delete();
        }
    }

    private static HashSet<Guid> GuidsIn(string relativePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Core.BaseDirectory, relativePath)));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("guid").GetGuid()).ToHashSet();
    }

    private static void RemoveTempFolder()
    {
        var dir = Path.Combine(Core.BaseDirectory, TempFolder);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    // An empty Trammel tile far from anything the other facts touch.
    private const int X = 1400, Y = 1400, Z = 0;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void SpawnersStackedOnOneXYAllSurvive()
    {
        var dev = NewDeveloper();
        var entries = new[]
        {
            new Entry(Guid.NewGuid(), X, Y, Z, "Rabbit"),
            new Entry(Guid.NewGuid(), X, Y, Z + 1, "Rat"),
            new Entry(Guid.NewGuid(), X, Y, Z + 2, "Bird"),
            // pinned's data also stacks distinct spawners on the very same z (330 of its 338 losses)
            new Entry(Guid.NewGuid(), X, Y, Z + 2, "Cat")
        };
        var guids = entries.Select(e => e.Guid).ToHashSet();

        try
        {
            Import(dev, WriteSpawnFile("stacked.json", entries));

            var survivors = SpawnersWith(guids);
            _out.WriteLine($"after one import: {survivors.Count} of {entries.Length} stacked spawners, at " +
                           string.Join(", ", survivors.Select(s => $"{s.Location} {s.Entries[0].SpawnedName}")));

            Assert.Equal(guids.OrderBy(g => g), survivors.Select(s => s.Guid).OrderBy(g => g));
            Assert.All(survivors, s => Assert.Equal(new Point2D(X, Y), new Point2D(s.X, s.Y)));
        }
        finally
        {
            DeleteSpawners(guids);
            RemoveTempFolder();
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ReimportingTheSameFileReplacesAndAddsNothing()
    {
        var dev = NewDeveloper();
        var entries = new[]
        {
            new Entry(Guid.NewGuid(), X + 10, Y, Z, "Rabbit"),
            new Entry(Guid.NewGuid(), X + 10, Y, Z + 1, "Rat"),
            new Entry(Guid.NewGuid(), X + 12, Y, Z, "Bird")
        };
        var guids = entries.Select(e => e.Guid).ToHashSet();

        try
        {
            var pattern = WriteSpawnFile("again.json", entries);
            Import(dev, pattern);
            var first = SpawnersWith(guids);
            var allBefore = World.Items.Values.OfType<BaseSpawner>().Count(s => !s.Deleted);

            Import(dev, pattern);
            var second = SpawnersWith(guids);
            var allAfter = World.Items.Values.OfType<BaseSpawner>().Count(s => !s.Deleted);

            _out.WriteLine($"first import {first.Count}, second {second.Count}; spawners in the world {allBefore} -> {allAfter}");

            Assert.Equal(entries.Length, first.Count);
            Assert.Equal(entries.Length, second.Count);
            Assert.Equal(allBefore, allAfter);
            Assert.Equal(guids.OrderBy(g => g), second.Select(s => s.Guid).OrderBy(g => g));
            Assert.All(first, s => Assert.True(s.Deleted, $"{s.Guid} from the first import was not replaced"));
        }
        finally
        {
            DeleteSpawners(guids);
            RemoveTempFolder();
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheSameSpawnerIsReplacedAndAStrangerOnItsTileIsKept()
    {
        var dev = NewDeveloper();
        var entry = new Entry(Guid.NewGuid(), X + 20, Y, Z, "Rabbit");
        var guids = new HashSet<Guid> { entry.Guid };

        // A staff-placed spawner of the same type on the same x,y,z, which the data does not name.
        var stranger = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "Rat");
        stranger.MoveToWorld(new Point3D(entry.X, entry.Y, entry.Z), Map.Trammel);

        try
        {
            var pattern = WriteSpawnFile("same.json", [entry]);
            Import(dev, pattern);
            var before = Assert.Single(SpawnersWith(guids));

            Import(dev, pattern);
            var after = Assert.Single(SpawnersWith(guids));

            _out.WriteLine($"same guid: {before.Serial} -> {after.Serial}, old deleted {before.Deleted}; stranger deleted {stranger.Deleted}");

            Assert.NotSame(before, after);
            Assert.True(before.Deleted);
            Assert.Equal(before.Location, after.Location);
            Assert.False(stranger.Deleted, "a spawner the data does not name was deleted from its tile");
        }
        finally
        {
            DeleteSpawners(guids);
            stranger.Delete();
            RemoveTempFolder();
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    // P17's table (notes/cc-P17-playtest-bugs-1.md 1.2), confirmed against the data by cc-P32.
    private static readonly (int X, int Y, int Z, string Spawns)[] NewHavenLost =
    {
        (3463, 2558, 35, "Healer"),
        (3526, 2536, 20, "BlacksmithGuildmaster"),
        (3526, 2536, 21, "IronWorker"),
        (3470, 2519, 49, "HairStylist"),
        (3461, 2566, 36, "Herbalist"),
        (3461, 2566, 37, "Alchemist"),
        (3446, 2606, 32, "LeatherWorker"),
        (3446, 2606, 31, "Furtrader")
    };

    [Fact]
    public void PinnedVendorsFileKeepsEverySpawnerWithNewHavensVendors()
    {
        var dev = NewDeveloper();
        var guids = GuidsIn(VendorsFile);

        try
        {
            Import(dev, VendorsFile);
            var first = SpawnersWith(guids);
            _out.WriteLine($"{VendorsFile}: {guids.Count} spawners in the file, {first.Count} in the world after one import");

            foreach (var (x, y, z, spawns) in NewHavenLost)
            {
                var here = first.Where(s => s.X == x && s.Y == y && s.Z == z).ToList();
                _out.WriteLine($"  {x},{y},{z} {spawns}: {string.Join(", ", here.Select(s => s.Entries[0].SpawnedName))}");
                Assert.Contains(here, s => s.Entries.Any(e => e.SpawnedName == spawns));
            }

            Assert.Equal(guids.Count, first.Count);

            Import(dev, VendorsFile);
            var second = SpawnersWith(guids);
            Assert.Equal(guids.Count, second.Count);
            Assert.All(first, s => Assert.True(s.Deleted));
        }
        finally
        {
            DeleteSpawners(guids);
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void SeederRetiresItsStandInHealerOnceThePinnedOneIsBack()
    {
        var dev = NewDeveloper();
        var guids = GuidsIn(VendorsFile);
        var map = Map.Trammel;

        static bool InSeededArea(Item item) =>
            !item.Deleted && item.Parent == null && item.Map == Map.Trammel &&
            item.X >= 3400 && item.X <= 3570 && item.Y >= 2440 && item.Y <= 2660;

        var before = World.Items.Values.Where(InSeededArea).ToHashSet();

        // The stand-in as P17's seeder placed it on a world without pinned's Healer.
        Assert.Null(ClusterFNewHavenServicesSeeder.FindStandInHealerSpawner(map));
        Assert.False(ClusterFNewHavenServicesSeeder.HasDataHealerSpawner(map));
        var seeded = ClusterFNewHavenServicesSeeder.Seed(false);
        var standIn = ClusterFNewHavenServicesSeeder.FindStandInHealerSpawner(map);

        try
        {
            Assert.True(seeded.HealerPlaced);
            Assert.NotNull(standIn);
            var standInHealers = standIn.Spawned.Keys.OfType<Healer>().ToList();

            Import(dev, VendorsFile);
            Assert.True(ClusterFNewHavenServicesSeeder.HasDataHealerSpawner(map),
                "pinned's Healer spawner is not in the world after importing Vendors.json");

            var dry = ClusterFNewHavenServicesSeeder.Seed(true);
            _out.WriteLine(dry.Message);
            Assert.True(dry.StandInRetired);
            Assert.False(standIn.Deleted, "the dry run deleted the stand-in");

            var real = ClusterFNewHavenServicesSeeder.Seed(false);
            _out.WriteLine(real.Message);
            Assert.True(real.StandInRetired);
            Assert.False(real.HealerPlaced);
            Assert.True(standIn.Deleted);
            Assert.All(standInHealers, h => Assert.True(h.Deleted, "a stand-in Healer outlived its spawner"));
            Assert.Null(ClusterFNewHavenServicesSeeder.FindStandInHealerSpawner(map));

            var again = ClusterFNewHavenServicesSeeder.Seed(false);
            Assert.False(again.StandInRetired);
            Assert.False(again.HealerPlaced);
        }
        finally
        {
            standIn?.Delete();
            DeleteSpawners(guids);

            // What the seeder placed for decoration, so later facts start from the same world.
            var placed = World.Items.Values.Where(i => InSeededArea(i) && !before.Contains(i)).ToList();
            foreach (var item in placed)
            {
                item.Delete();
            }

            _out.WriteLine($"removed {placed.Count} items the seeder placed");
            dev.Delete();
        }
    }
}
