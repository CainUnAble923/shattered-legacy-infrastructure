// cc-P128: raised garden beds and the potted coffee plant (ServUO Services/Plants -> customizations/Services/Plants/),
// reached through the three plant-engine patches (PlantItem-raised-garden-hooks, PlantSystem-raised-garden-upkeep,
// Seed-raised-garden-plot).
//
// Facts:
//   1. The deed's four shapes place their components (9, 6, 6, 4) with ServUO's ids; the deed names itself.
//   2. A seed used on an empty plot in a house raises a mound and plants it with no water (a stock bowl refuses dry
//      dirt); the plot knows its mound and the mound its plot; a second seed is refused.
//   3. A mound grows with no water and takes no malady damage; a stock bowl with dry dirt is damaged by the same check.
//   4. A bed with a planted plot cannot be chopped.
//   5. A potted coffee plant grows a stage per check and gives a pod at stage four to someone the secure level allows.
//   6. A mound and its plot survive a round trip together; the three pinned files carry their patched lines.
//
// Host notes: a house with a floor, as LootBoxFanfareVerification's (no multi data in the host); the mound is at the
// plot's spot plus 5 Z, as the seed target puts it.

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Plants;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RaisedGardenVerification
{
    private readonly ITestOutputHelper _out;

    public RaisedGardenVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
    }

    private static readonly Point3D Wild = new(1400, 1400, 0);

    // A house with a floor, as LootBoxFanfareVerification's: the host has no multi data.
    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    private sealed class Garden : IDisposable
    {
        public readonly PlayerMobile Owner;
        public readonly PlayerMobile Stranger;
        public readonly TestHouse House;
        public readonly RaisedGardenAddon Bed;
        public readonly List<Item> Loose = new();

        public Garden(RaisedGardenDirection direction = RaisedGardenDirection.Small)
        {
            ShardTestClock.Arm();
            Owner = new PlayerMobile { Player = true, Race = Race.Human };
            Owner.AddItem(new Backpack());
            Owner.MoveToWorld(Wild, Map.Trammel);

            Stranger = new PlayerMobile { Player = true, Race = Race.Human };
            Stranger.AddItem(new Backpack());
            Stranger.MoveToWorld(new Point3D(Wild.X + 1, Wild.Y, 0), Map.Trammel);

            House = new TestHouse(Owner);
            House.MoveToWorld(Wild, Map.Trammel);

            Bed = new RaisedGardenAddon(direction);
            Bed.MoveToWorld(new Point3D(Wild.X - 2, Wild.Y - 2, 0), Map.Trammel);
        }

        public GardenAddonComponent Plot(int index = 0) => (GardenAddonComponent)Bed.Components[index];

        public T Pack<T>(T item, PlayerMobile who = null) where T : Item
        {
            (who ?? Owner).Backpack.DropItem(item);
            return item;
        }

        public void Dispose()
        {
            foreach (var item in Loose)
            {
                item.Delete();
            }

            foreach (var comp in new List<AddonComponent>(Bed.Components))
            {
                (comp as GardenAddonComponent)?.Plant?.Delete();
            }

            Bed.Delete();
            House.Delete();
            foreach (var pm in new[] { Owner, Stranger })
            {
                foreach (var item in new List<Item>(pm.Backpack.Items))
                {
                    item.Delete();
                }

                pm.Delete();
            }
        }
    }

    [Fact]
    public void TheDeedPlacesTheFourBeds()
    {
        var large = new RaisedGardenAddon(RaisedGardenDirection.Large);
        var east = new RaisedGardenAddon(RaisedGardenDirection.East);
        var south = new RaisedGardenAddon(RaisedGardenDirection.South);
        var small = new RaisedGardenAddon(RaisedGardenDirection.Small);
        var deed = new RaisedGardenDeed { Direction = RaisedGardenDirection.East };

        try
        {
            Assert.Equal(9, large.Components.Count);
            Assert.Equal(6, east.Components.Count);
            Assert.Equal(6, south.Components.Count);
            Assert.Equal(4, small.Components.Count);

            Assert.All(small.Components, c => Assert.IsType<GardenAddonComponent>(c));
            Assert.Equal(19234, small.Components[0].ItemID);
            Assert.Equal(19238, small.Components[3].ItemID);
            Assert.Equal(1150359, small.Components[0].LabelNumber);
            Assert.False(small.HasPlants);

            Assert.Equal(1150359, deed.LabelNumber);
            var addon = deed.Addon;
            Assert.Equal(6, addon.Components.Count);
            Assert.IsType<RaisedGardenDeed>(addon.Deed);
            addon.Delete();
        }
        finally
        {
            large.Delete();
            east.Delete();
            south.Delete();
            small.Delete();
            deed.Delete();
        }
    }

    [Fact]
    public void ASeedOnAPlotInAHouseRaisesAMoundAndPlantsIt()
    {
        using var g = new Garden();
        var plot = g.Plot();
        Assert.Same(g.House, BaseHouse.FindHouseAt(plot));

        var seed = g.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Blue, true));
        var second = g.Pack(new Seed(PlantType.Snowdrops, PlantHue.Red, true));

        seed.OnDoubleClick(g.Owner);
        Assert.NotNull(g.Owner.Target);
        g.Owner.Target.Invoke(g.Owner, plot);

        Assert.True(seed.Deleted);
        var mound = Assert.IsType<RaisedGardenPlantItem>(plot.Plant);
        Assert.Same(plot, mound.Component);
        Assert.Equal(new Point3D(plot.X, plot.Y, plot.Z + 5), mound.Location);
        Assert.Equal(PlantStatus.Seed, mound.PlantStatus);
        Assert.Equal(PlantType.CampionFlowers, mound.PlantType);
        Assert.Equal(PlantHue.Blue, mound.PlantHue);
        Assert.Equal(2323, mound.ItemID);
        Assert.False(mound.Movable);
        Assert.False(mound.RequiresUpkeep);
        Assert.True(mound.ValidGrowthLocation);
        Assert.True(mound.IsUsableBy(g.Owner));
        Assert.False(mound.IsUsableBy(g.Stranger));
        Assert.True(g.Bed.HasPlants);
        Assert.Equal(1060827, mound.PlantSystem.GetLocalizedDirtStatus()); // soft, dry or not

        second.OnDoubleClick(g.Owner);
        g.Owner.Target.Invoke(g.Owner, plot);
        Assert.False(second.Deleted); // This plot already has a plant!

        // A stock bowl with dry dirt refuses the same seed: the no-water planting is the mound's alone.
        var bowl = g.Pack(new PlantItem());
        bowl.PlantSystem.Water = 0;
        bowl.PlantSeed(g.Owner, second);
        Assert.False(second.Deleted);
        Assert.Equal(PlantStatus.BowlOfDirt, bowl.PlantStatus);
    }

    [Fact]
    public void AMoundGrowsWithoutWaterOrMaladies()
    {
        using var g = new Garden();
        var plot = g.Plot();
        var mound = new RaisedGardenPlantItem();
        mound.MoveToWorld(new Point3D(plot.X, plot.Y, plot.Z + 5), plot.Map);
        mound.Component = plot;
        plot.Plant = mound;
        mound.PlantSeed(g.Owner, g.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Red, true)));
        Assert.Equal(PlantStatus.Seed, mound.PlantStatus);

        mound.PlantSystem.Water = 0;
        mound.ForceGrow = true;
        Assert.Equal(PlantStatus.Stage2, mound.PlantStatus);
        Assert.Equal(2323, mound.ItemID); // the mound of dirt until the plant shows (pinned: PlantStatus.Plant, stage 4)
        mound.ForceGrow = true;
        mound.ForceGrow = true;

        Assert.Equal(PlantStatus.Stage4, mound.PlantStatus);
        Assert.Equal(mound.PlantSystem.MaxHits, mound.PlantSystem.Hits);
        Assert.Equal(0, mound.PlantSystem.Infestation + mound.PlantSystem.Fungus + mound.PlantSystem.Poison + mound.PlantSystem.Disease);
        Assert.Equal(0xC62, mound.ItemID); // the green mound (ServUO's GreenBowlID past stage 3)

        // The same check on a stock bowl with dry dirt costs it hits (a seedling put in by hand; the host's bowl is in the pack).
        var bowl = g.Pack(new PlantItem { PlantType = PlantType.CampionFlowers, PlantHue = PlantHue.Red });
        bowl.PlantStatus = PlantStatus.Seed;
        // cc-P175: at full hits. Dry dirt deals 2 x RandomMinMax(3, 6) = 6 to 12 (pinned PlantSystem.cs:619); from a new bowl's
        // 10 hits (the empty bowl's maximum) that killed the seedling about half the time, and the fact asserted it lived. It
        // lives with fewer hits or, on a 12, dies: either way the dry dirt cost it, and it never grows (asserted below).
        bowl.PlantSystem.Hits = bowl.PlantSystem.MaxHits;
        bowl.PlantSystem.Water = 0;
        bowl.PlantSystem.NextGrowth = Core.Now;
        _out.WriteLine($"bowl before: status={bowl.PlantStatus} hits={bowl.PlantSystem.Hits} of {bowl.PlantSystem.MaxHits} water={bowl.PlantSystem.Water} valid={bowl.ValidGrowthLocation}");
        bowl.PlantSystem.DoGrowthCheck();
        _out.WriteLine($"bowl after: status={bowl.PlantStatus} hits={bowl.PlantSystem.Hits} of {bowl.PlantSystem.MaxHits} indicator={bowl.PlantSystem.GrowthIndicator}");
        Assert.True(bowl.PlantStatus is PlantStatus.Seed or PlantStatus.BowlOfDirt, $"the dry bowl grew: {bowl.PlantStatus}");
        Assert.True(bowl.PlantStatus == PlantStatus.BowlOfDirt || bowl.PlantSystem.Hits < bowl.PlantSystem.MaxHits,
            $"bowl hits {bowl.PlantSystem.Hits} of {bowl.PlantSystem.MaxHits}");
    }

    [Fact]
    public void AnOccupiedBedCannotBeChopped()
    {
        using var g = new Garden();
        var plot = g.Plot();
        var mound = new RaisedGardenPlantItem();
        mound.MoveToWorld(new Point3D(plot.X, plot.Y, plot.Z + 5), plot.Map);
        mound.Component = plot;
        plot.Plant = mound;

        Assert.True(g.Bed.HasPlants);
        g.Bed.OnChop(g.Owner);
        Assert.False(g.Bed.Deleted);

        mound.Delete();
        Assert.Null(plot.Plant);
        Assert.False(g.Bed.HasPlants);
    }

    [Fact]
    public void APottedCoffeePlantGrowsAndYieldsAPod()
    {
        ShardTestClock.Arm();
        var staff = new PlayerMobile { Player = true, AccessLevel = AccessLevel.GameMaster };
        staff.AddItem(new Backpack());
        staff.MoveToWorld(Wild, Map.Trammel);
        var plant = new PottedCoffeePlant();
        plant.MoveToWorld(Wild, Map.Trammel);

        try
        {
            Assert.Equal(PlantStatus.Stage1, plant.PlantStatus);
            Assert.Equal(0x9A20, plant.ItemID);

            plant.OnDoubleClick(staff);
            Assert.Null(staff.Backpack.FindItemByType<CoffeePod>()); // not ready

            for (var i = 0; i < 3; i++)
            {
                plant.NextGrowth = Core.Now - TimeSpan.FromMinutes(1);
                plant.OnTick();
            }

            Assert.Equal(PlantStatus.Stage4, plant.PlantStatus);
            Assert.Equal(0x9A23, plant.ItemID);

            plant.OnDoubleClick(staff);
            Assert.NotNull(staff.Backpack.FindItemByType<CoffeePod>());
            Assert.Equal(PlantStatus.Stage3, plant.PlantStatus);
            Assert.True(plant.NextGrowth > Core.Now + TimeSpan.FromHours(22));
        }
        finally
        {
            foreach (var item in new List<Item>(staff.Backpack.Items))
            {
                item.Delete();
            }

            plant.Delete();
            staff.Delete();
        }
    }

    [Fact]
    public void AMoundAndItsPlotSurviveARoundTrip()
    {
        using var g = new Garden();
        var plot = g.Plot(1);
        var mound = new RaisedGardenPlantItem(true);
        mound.MoveToWorld(new Point3D(plot.X, plot.Y, plot.Z + 5), plot.Map);
        mound.Component = plot;
        plot.Plant = mound;
        mound.PlantSeed(g.Owner, g.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Red, true)));

        var copy = ConsumablesVerification.RoundTrip(mound, s => new RaisedGardenPlantItem(s));
        g.Loose.Add(copy);

        Assert.Equal(PlantStatus.Seed, copy.PlantStatus);
        Assert.Equal(PlantHue.Red, copy.PlantHue);
        Assert.True(copy.PlantSystem.FertileDirt);
        Assert.Same(plot, copy.Component);
    }

    [Fact]
    public void TheThreePinnedFilesCarryTheirPatchedLinesInTheBuildTree()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        var root = Path.Combine(dir.FullName, "Projects", "UOContent");

        var expected = new (string File, string Line)[]
        {
            ("Engines/Plants/PlantItem.cs", "public virtual bool RequiresUpkeep => true;"),
            ("Engines/Plants/PlantSystem.cs", "if (!Plant.RequiresUpkeep)"),
            ("Engines/Plants/Seed.cs", "targeted is GardenAddonComponent addon")
        };

        foreach (var (file, line) in expected)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.True(text.Contains(line, StringComparison.Ordinal), $"{file} lacks '{line}': its cc-P128 patch did not apply");
        }
    }
}
