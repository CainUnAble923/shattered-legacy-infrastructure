// HavenTownVerification.cs
//
// cc-P678 (SoS swap S7): New Haven is the author's Haven town on the Siege of Sosaria map, OSI's pre-UOML Haven rebuilt,
// so its people, regions and training ground come from pinned's pre-UOML data, and the ML plaza's and Old Haven's ruin's
// stop coming (ClusterFHavenTownSpawns, HavenTownRegions). Notes: shard-migration notes/cc-P678-new-haven-moves.md.
//
// Facts, from the shipped data files and through the real [GenerateSpawners command:
//   T1. The import hook refuses, by the files' own guids: every post-uoml Vendors/TownsLife spawner inside the ML plaza
//       (P17's eight New Haven vendors among them), the ruin's spawners inside the town (the undead, the spellbinder
//       field, the brigand camps, the ruin's lone Healer, the wildlife at the town's west and east edges) and every pre-UOML spawner outside the town; and nothing else
//       of those files (the town crier, the pumpkin fields, the escorts at the docks stay).
//   T2. Real imports: shared/trammel/TownsPeople.json places every spawner, the town crier at 3652,2619 among them
//       (cc-P188 refused him while Old Haven was a ruin); post-uoml/trammel/Outdoors.json places none of the ruin's in
//       the town and the rest; uoml/trammel/Vendors.json places only Haven's own (the Second Bank's banker at 3620,2617).
//   T3. The undead field moves to the north woods: one spawner at 3640,2418,34 with pinned's entries (70 zombies,
//       70 skeletons, 20 spellbinders, 90 at once), home and walking range 40, so nothing it spawns reaches the town
//       (y 2460); a second seed places nothing; it is inside Old Haven Training.
//   T4. Regions: "New Haven" is a guarded TownRegion over pinned "Haven"'s rectangles, its go location the start, and the
//       start, the bank, the healer, the docks, the public moongate and the hatch are in it; the ML plaza (3503,2574) is
//       not, and "New Haven Moongate" is gone; the Second Bank of Haven and the other shops are under it with their
//       names; the inn building is "the New Haven Inn" with no logout delay.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HavenTownVerification
{
    private const string TownsPeopleFile = "shared/trammel/TownsPeople.json";
    private const string OutdoorsFile = "post-uoml/trammel/Outdoors.json";
    private const string HavenVendorsFile = "uoml/trammel/Vendors.json";

    private static readonly Point3D CrierAt = new(3652, 2619, 0);
    private static readonly Guid CrierGuid = new("8b75bcf8-7b83-4d94-8573-92aff88cd41f");

    private readonly ITestOutputHelper _out;

    public HavenTownVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
        ApprenticeTrainingRegion.Initialize();
        HavenTownRegions.Initialize();
    }

    private static PlayerMobile NewDeveloper()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Builder", AccessLevel = AccessLevel.Developer };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        return pm;
    }

    private static void Import(Mobile dev, string file) =>
        Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/{file}"));

    private static List<BaseSpawner> InWorld(IEnumerable<ClusterFStockSpawnData.StockSpawner> stock)
    {
        var guids = stock.Select(s => s.Guid).ToHashSet();
        return World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && s.Map == Map.Trammel && guids.Contains(s.Guid)).ToList();
    }

    private static void DeleteAll(IEnumerable<Item> items)
    {
        foreach (var i in items.ToList())
        {
            if (i is BaseSpawner s)
            {
                s.RemoveSpawns();
            }

            i.Delete();
        }
    }

    // ---------------------------------------------------------------- T1

    [Fact]
    public void T1_TheHookRefusesThePlazaTheRuinAndPreUomlOutsideTownAndNothingElse()
    {
        var refused = ClusterFHavenTownSpawns.Refused;

        var plaza = ClusterFHavenTownSpawns.PlazaFiles.SelectMany(f => ClusterFStockSpawnData.LoadRelative(f, Map.Trammel))
            .Where(s => ClusterFHavenTownSpawns.InOldPlaza(s.Location)).ToList();
        var ruin = ClusterFStockSpawnData.Load(Map.Trammel, ClusterFStockSpawnData.ShardFolders)
            .Where(s => HavenSpots.InTown(s.Location) && s.Names.Any(ClusterFHavenTownSpawns.RuinEntries.Contains)).ToList();
        var haven = ClusterFHavenTownSpawns.HavenFiles.SelectMany(f => ClusterFStockSpawnData.LoadRelative(f, Map.Trammel)).ToList();
        _out.WriteLine($"plaza {plaza.Count}, ruin {ruin.Count}, pre-UOML {haven.Count} ({haven.Count(s => HavenSpots.InTown(s.Location))} in town)");

        Assert.True(plaza.Count >= 50, $"{plaza.Count} plaza spawners");
        Assert.Equal(10, ruin.Count); // 5 undead, the undead field, the ruin's Healer, 2 brigand camps, 2 edge wildlife
        Assert.Contains(ruin, s => s.Location == new Point3D(3650, 2509, 0) && s.Guid == ClusterFHavenTownSpawns.RuinFieldGuid);
        Assert.Contains(ruin, s => s.Location == new Point3D(3651, 2627, 0) && s.Names.SequenceEqual(["Skeleton"]));
        Assert.Contains(haven, s => s.Location == HavenSpots.Bank && s.Names.Contains("Banker"));

        var expected = plaza.Concat(ruin).Concat(haven.Where(s => !HavenSpots.InTown(s.Location))).Select(s => s.Guid).ToHashSet();
        Assert.Equal(expected.OrderBy(g => g), refused.OrderBy(g => g));

        // and what stays: the crier, a pumpkin field, the escorts at the docks
        Assert.DoesNotContain(CrierGuid, refused);
        var stays = ClusterFStockSpawnData.Load(Map.Trammel, ClusterFStockSpawnData.ShardFolders)
            .Where(s => HavenSpots.InTown(s.Location) && !refused.Contains(s.Guid)).ToList();
        Assert.Contains(stays, s => s.Guid == CrierGuid);
        Assert.Contains(stays, s => s.Names.Contains("Pumpkin"));
        Assert.Contains(stays, s => s.Names.Contains("EscortableMage"));
    }

    // ---------------------------------------------------------------- T2

    [Fact]
    public void T2_RealImportsPlaceTheCrierNoRuinAndOnlyHavensPreUomlPeople()
    {
        var dev = NewDeveloper();
        var placed = new List<Item>();
        try
        {
            var towns = ClusterFStockSpawnData.LoadRelative(TownsPeopleFile, Map.Trammel);
            var outdoors = ClusterFStockSpawnData.LoadRelative(OutdoorsFile, Map.Trammel);
            var vendors = ClusterFStockSpawnData.LoadRelative(HavenVendorsFile, Map.Trammel);
            DeleteAll(InWorld(towns.Concat(outdoors).Concat(vendors)));

            Import(dev, TownsPeopleFile);
            var t = InWorld(towns);
            placed.AddRange(t);
            Assert.Equal(towns.Count, t.Count);
            Assert.Contains(t, s => s.Guid == CrierGuid && s.Location == CrierAt);

            Import(dev, OutdoorsFile);
            var o = InWorld(outdoors);
            placed.AddRange(o);
            var ruinHere = outdoors.Count(s => ClusterFHavenTownSpawns.Refused.Contains(s.Guid));
            _out.WriteLine($"{OutdoorsFile}: {o.Count} of {outdoors.Count}, {ruinHere} refused");
            Assert.True(ruinHere >= 7, $"{ruinHere}");
            Assert.Equal(outdoors.Count - ruinHere, o.Count);
            Assert.DoesNotContain(o, s => HavenSpots.InTown(s.Location) && s.Entries.Any(e => e.SpawnedName is "Zombie" or "Skeleton"));

            Import(dev, HavenVendorsFile);
            var v = InWorld(vendors);
            placed.AddRange(v);
            Assert.Equal(vendors.Count(s => HavenSpots.InTown(s.Location)), v.Count);
            Assert.All(v, s => Assert.True(HavenSpots.InTown(s.Location), s.Location.ToString()));
            Assert.Contains(v, s => s.Location == HavenSpots.Bank && s.Entries.Any(e => e.SpawnedName == "Banker"));
        }
        finally
        {
            DeleteAll(placed);
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- T3

    [Fact]
    public void T3_TheUndeadFieldMovesToTheNorthWoods()
    {
        DeleteAll(World.Items.Values.OfType<Spawner>().Where(s => s.Guid == ClusterFHavenTownSpawns.TrainingFieldGuid));
        var field = ClusterFHavenTownSpawns.SeedTrainingField(Map.Trammel);
        try
        {
            Assert.NotNull(field);
            Assert.Equal(new Point3D(3640, 2418, 34), field!.Location);
            Assert.Equal(90, field.Count);
            Assert.Equal(40, field.HomeRange);
            Assert.Equal(40, field.WalkingRange);
            Assert.Equal(TimeSpan.FromMinutes(5), field.MinDelay);
            Assert.Equal(TimeSpan.FromMinutes(10), field.MaxDelay);
            var entries = field.Entries.ToDictionary(e => e.SpawnedName, e => e.SpawnedMaxCount);
            Assert.Equal(70, entries["Zombie"]);
            Assert.Equal(70, entries["Skeleton"]);
            Assert.Equal(20, entries["spellbinder"]);

            // nothing it spawns reaches the town, whose north edge is y 2460
            Assert.True(field.Y + field.HomeRange < 2460 && field.Y + field.WalkingRange < 2460);
            Assert.Equal(ApprenticeTraining.OldHavenTraining, Region.Find(field.Location, Map.Trammel).Name);

            Assert.Null(ClusterFHavenTownSpawns.SeedTrainingField(Map.Trammel));
            Assert.Same(field, ClusterFHavenTownSpawns.FindTrainingField(Map.Trammel));
        }
        finally
        {
            if (field != null)
            {
                DeleteAll([field]);
            }
        }
    }

    // ---------------------------------------------------------------- T4

    [Fact]
    public void T4_NewHavenIsTheAuthorsTownWithItsShops()
    {
        var town = Region.Find(HavenTownRegions.TownName, Map.Trammel);
        var tr = Assert.IsType<TownRegion>(town);
        Assert.Same(HavenTownRegions.Town, tr);
        Assert.Equal(HavenSpots.Start, tr.GoLocation);
        Assert.Equal("New Haven", tr.RuneName);
        // the test host loads no regions.json, so the island is there only on a shard
        Assert.Equal(Region.Find("Haven Island", Map.Trammel), tr.Parent);

        var stock = HavenTownRegions.ReadStock();
        var haven = stock.Single(s => s.Name == HavenTownRegions.StockTownName);
        Assert.Equal(haven.Area, tr.Area);

        foreach (var p in new[] { HavenSpots.Start, HavenSpots.Bank, HavenSpots.Healer, HavenSpots.Dock, HavenSpots.PublicMoongate,
                     OldHavenBurrowsRegion.HoleEntrance })
        {
            Assert.True(Region.Find(p, Map.Trammel).IsPartOf(HavenTownRegions.TownName), p.ToString());
        }

        Assert.False(Region.Find(new Point3D(3503, 2574, 14), Map.Trammel).IsPartOf(HavenTownRegions.TownName));
        Assert.Null(Region.Regions.FirstOrDefault(r => r.Map == Map.Trammel && r.Name == "New Haven Moongate"));
        Assert.Single(Region.Regions, r => r.Map == Map.Trammel && r.Name == HavenTownRegions.TownName);

        Assert.Equal("the Second Bank of Haven", Region.Find(HavenSpots.Bank, Map.Trammel).Name);
        Assert.Equal("the Healers of Haven", Region.Find(HavenSpots.Healer, Map.Trammel).Name);
        var shops = stock.Where(s => s.Parent == HavenTownRegions.StockTownName && !HavenTownRegions.LeftOut.Contains(s.Name)).ToList();
        Assert.True(shops.Count >= 20, $"{shops.Count} shops");
        Assert.All(shops, s => Assert.Same(tr, Region.Find(s.Name, Map.Trammel)?.Parent));

        var inn = Assert.IsType<NoHousingRegion>(Region.Find(new Point3D(3671, 2615, 0), Map.Trammel));
        Assert.Equal(HavenTownRegions.InnName, inn.Name);
        Assert.True(inn.NoLogoutDelay);
        Assert.Same(tr, inn.Parent);
    }
}
