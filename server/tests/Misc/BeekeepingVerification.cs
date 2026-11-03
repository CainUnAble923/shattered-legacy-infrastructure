// BeekeepingVerification.cs
//
// cc-P787: beekeeping. Notes in shard-migration notes/cc-P787-beekeeping.md. Each fact was seen red first (the notes'
// red table).
//
// Facts:
//   1. The table: the defaults validate and round-trip as JSON; a candle row making a weapon, a beekeeper row selling a
//      power scroll, an unknown setting and shrinkHealth above growHealth are each refused whole.
//   2. Placement: a co-owner places a hive from its deed inside the house as one lockdown; not outside a house, not by a
//      stranger, and at most maxHivesPerHouse a house.
//   3. A colony: a swarm starts it in a locked-down hive; it goes Colonizing, Brooding, Producing over the saved check
//      times, grows a bee a healthy day and makes honey and wax; a second sweep at the same time is no second day, and
//      a released hive does not live its days.
//   4. Flowers and water: blooming plant bowls and flower art count as flowers, troughs and filled water pitchers as
//      water, within range (Agility widens it); a wilting bowl, an empty pitcher and things out of range do not.
//   5. Ailments and potions: parasites (Poison), disease (Cure), Heal and Greater Heal, Strength (half losses) and
//      Agility; a potion the hive does not need is kept.
//   6. Neglect lowers health and then bees, and at none the bees leave: the hive stays, empty, its honey kept.
//   7. Harvest: the honey and wax ready come out as jars of honey and raw beeswax, a tool use each, the tool worn out
//      at none; the wax pot renders a batch into stock beeswax less the slumgum.
//   8. The candles are in Alchemy's Candles group and each makes its stock candle from beeswax and thread; the wax pot
//      is in Tinkering.
//   9. Every beekeeper also sells the deed, the swarm, the tool and the wax pot at the table's prices.
//  10. Wild hives: seeding places one per spot and a second seeding none; a comb gives a honeycomb and a sting that
//      never kills; an emptied hive regrows after regrowHours; a hive tool catches one swarm per regrowth.
//  11. A hive and a wild hive survive a save and load.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Beekeeping;
using Server.Engines.Craft;
using Server.Engines.Plants;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BeekeepingVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];
    private readonly Func<int, int> _roll;

    // A quiet spot well away from towns and other facts.
    private static readonly Point3D Yard = new(5420, 3820, 0);

    public BeekeepingVerification(ITestOutputHelper output)
    {
        _out = output;
        BeekeepingTable.Current = BeekeepingTable.Defaults;
        _roll = HouseBeehive.Roll;
        HouseBeehive.Roll = _ => 99; // no ailment unless a fact asks for one
    }

    public void Dispose()
    {
        HouseBeehive.Roll = _roll;
        BeekeepingTable.Current = BeekeepingTable.Defaults;
        foreach (var e in _made)
        {
            e?.Delete();
        }
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    private static BeekeepingTable T => BeekeepingTable.Current;

    // The host has no multi data: a small house whose floor is the 7 x 7 it stands on.
    private sealed class FloorHouse : SmallOldHouse
    {
        private static readonly MultiComponentList Floor = MakeFloor();

        public FloorHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        private static MultiComponentList MakeFloor()
        {
            var list = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    list.Add(new MultiTileEntry(0x495, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(list);
        }

        public override MultiComponentList Components => Floor;

        // Pinned finds a house's AOS lockdown allowance by its type (HousePlacementEntry), which this test subclass is
        // not: it keeps SmallOldHouse's classic allowance (425 lockdowns) instead.
        public override bool IsAosRules => false;
    }

    private PlayerMobile Player(Point3D at, string name = "Beekeeper")
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = name });
        pm.AddItem(new Backpack());
        pm.RawStr = 100;
        pm.Hits = pm.HitsMax;
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private (BaseHouse House, PlayerMobile Owner) House(int dx = 0)
    {
        var at = new Point3D(Yard.X + dx, Yard.Y, 0);
        var owner = Player(at, "Owner");
        var house = Track(new FloorHouse(owner));
        house.MoveToWorld(at, Map.Trammel);
        return (house, owner);
    }

    /// <summary>A hive locked down in a fresh house, with a colony of the given size and health.</summary>
    private (HouseBeehive Hive, PlayerMobile Owner) LivingHive(int population = 5, int health = 80, int dx = 0)
    {
        var (house, owner) = House(dx);
        var deed = new BeehiveDeed();
        owner.Backpack.DropItem(deed);
        Assert.Null(HouseBeehive.Place(owner, deed, new Point3D(house.X + 1, house.Y, 0), Map.Trammel));
        var hive = house.LockDowns.OfType<HouseBeehive>().Single();
        _made.Add(hive);
        var swarm = new BeeSwarm();
        owner.Backpack.DropItem(swarm);
        Assert.True(hive.AddSwarm(owner, swarm));
        hive.Population = population;
        hive.Health = health;
        return (hive, owner);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTableValidatesAndRefusesForbiddenRowsWhole()
    {
        var errors = new List<string>();
        BeekeepingTable.Validate(BeekeepingTable.BuildDefaults(), errors);
        Assert.Empty(errors);

        var json = BeekeepingTable.ToJson(BeekeepingTable.Defaults);
        var back = BeekeepingTable.Parse(json, "round-trip", errors);
        Assert.Empty(errors);
        Assert.NotNull(back);
        Assert.Equal(4, back!.Candles.Count);
        Assert.Equal(4, back.Beekeeper.Count);
        Assert.Equal(12, back.Wild.Spots.Count);
        Assert.Equal(2, back.MaxHivesPerHouse);

        void Refused(Func<string, string> edit, string what)
        {
            var e = new List<string>();
            Assert.Null(BeekeepingTable.Parse(edit(json), what, e));
            _out.WriteLine($"{what}: {string.Join("; ", e)}");
            Assert.NotEmpty(e);
        }

        Refused(j => j.Replace("\"item\": \"CandleLarge\"", "\"item\": \"Longsword\""), "a candle row making a weapon");
        Refused(j => j.Replace("\"type\": \"WaxPot\"", "\"type\": \"PowerScroll\""), "a beekeeper row selling a power scroll");
        Refused(j => j.Replace("\"enabled\": true,", "\"enabled\": true, \"grantsSkill\": 5,"), "an unknown setting");
        Refused(j => j.Replace("\"shrinkHealth\": 30", "\"shrinkHealth\": 90"), "shrinkHealth above growHealth");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AHiveIsPlacedInItsOwnersHouseAsOneLockdownUpToTheCap()
    {
        var (house, owner) = House();
        var before = house.LockDownCount;

        var outside = new BeehiveDeed();
        owner.Backpack.DropItem(outside);
        Assert.NotNull(HouseBeehive.Place(owner, outside, new Point3D(house.X + 20, house.Y, 0), Map.Trammel));
        Assert.False(outside.Deleted);

        var stranger = Player(new Point3D(house.X - 1, house.Y, 0), "Stranger");
        var theirs = new BeehiveDeed();
        stranger.Backpack.DropItem(theirs);
        Assert.NotNull(HouseBeehive.Place(stranger, theirs, new Point3D(house.X - 1, house.Y - 1, 0), Map.Trammel));
        Assert.False(theirs.Deleted);

        for (var i = 0; i < T.MaxHivesPerHouse; i++)
        {
            var deed = new BeehiveDeed();
            owner.Backpack.DropItem(deed);
            Assert.Null(HouseBeehive.Place(owner, deed, new Point3D(house.X + i, house.Y + 1, 0), Map.Trammel));
            Assert.True(deed.Deleted);
        }

        var hives = house.LockDowns.OfType<HouseBeehive>().ToList();
        _made.AddRange(hives);
        Assert.Equal(T.MaxHivesPerHouse, hives.Count);
        Assert.Equal(before + T.MaxHivesPerHouse, house.LockDownCount);
        Assert.All(hives, h => Assert.True(h.IsLockedInHouse));
        Assert.All(hives, h => Assert.False(h.Movable));

        var third = new BeehiveDeed();
        owner.Backpack.DropItem(third);
        Assert.NotNull(HouseBeehive.Place(owner, third, new Point3D(house.X - 2, house.Y + 1, 0), Map.Trammel));
        Assert.False(third.Deleted);
        Assert.Equal(T.MaxHivesPerHouse, HouseBeehive.HivesIn(house));

        Assert.True(hives[0].CanTend(owner));
        Assert.False(hives[0].CanTend(stranger));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AColonyLivesItsDaysOnTheSavedCheckTimesOnly()
    {
        var (hive, _) = LivingHive(population: 3, health: 80);
        Assert.Equal(HiveStage.Colonizing, hive.Stage);
        Assert.NotNull(hive.Bees);
        Assert.Equal(0x91B, hive.Bees!.ItemID);

        // Enough flowers and water for a full colony, at the hive.
        for (var i = 0; i < 6; i++)
        {
            Track(new Item(0xC83) { Movable = false }).MoveToWorld(new Point3D(hive.X, hive.Y - 1, 0), Map.Trammel);
        }

        Track(new Item(0xB41) { Movable = false }).MoveToWorld(new Point3D(hive.X - 1, hive.Y, 0), Map.Trammel);
        Track(new Item(0xB42) { Movable = false }).MoveToWorld(new Point3D(hive.X - 1, hive.Y + 1, 0), Map.Trammel);
        Track(new Item(0xE7B) { Movable = false }).MoveToWorld(new Point3D(hive.X - 2, hive.Y, 0), Map.Trammel);

        var now = hive.NextCheck;
        Assert.False(hive.CheckDue(now - TimeSpan.FromMinutes(1)));    // not yet due
        Assert.Equal(0, hive.Age);

        Assert.True(hive.CheckDue(now));
        Assert.Equal(1, hive.Age);
        Assert.Equal(HiveStage.Brooding, hive.Stage);
        Assert.Equal(4, hive.Population);
        Assert.Equal(90, hive.Health);
        Assert.Equal(now + TimeSpan.FromHours(T.CheckHours), hive.NextCheck);

        Assert.False(hive.CheckDue(now));                              // the same sweep again: no second day
        Assert.Equal(1, BeekeepingSystem.Sweep(hive.NextCheck));       // the shared sweep runs it
        Assert.Equal(2, hive.Age);
        Assert.Equal(HiveStage.Producing, hive.Stage);
        Assert.Equal(0, hive.Honey);                                   // the day it starts producing

        // Three days late still counts one day.
        Assert.True(hive.CheckDue(hive.NextCheck + TimeSpan.FromDays(3)));
        Assert.Equal(3, hive.Age);
        Assert.Equal(6, hive.Population);
        Assert.Equal(100, hive.Health);
        Assert.Equal(2, hive.Honey);                                   // 6 bees x 0.3 = 1.8
        Assert.Equal(1, hive.Wax);                                     // 6 bees x 0.2 = 1.2

        // Released from the house it waits: its check passes without a day.
        hive.House!.Release(hive.House.Owner, hive);
        Assert.False(hive.IsLockedInHouse);
        Assert.False(hive.CheckDue(hive.NextCheck));
        Assert.Equal(3, hive.Age);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void FlowersAndWaterAreReadWithinRange()
    {
        var (hive, _) = LivingHive();
        var now = Core.Now;
        Assert.Equal(0, hive.CountFlowers(now));
        Assert.Equal(0, hive.CountWater(now));

        var blooming = Track(new PlantItem());
        blooming.PlantStatus = PlantStatus.FullGrownPlant;
        blooming.MoveToWorld(new Point3D(hive.X + 1, hive.Y, 0), Map.Trammel);
        var sapling = Track(new PlantItem());
        sapling.PlantStatus = PlantStatus.Sapling;
        sapling.MoveToWorld(new Point3D(hive.X + 1, hive.Y + 1, 0), Map.Trammel);
        Track(new Item(0xC86)).MoveToWorld(new Point3D(hive.X, hive.Y + 2, 0), Map.Trammel);          // red poppies
        Track(new Item(0xC86)).MoveToWorld(new Point3D(hive.X + T.Range + 1, hive.Y, 0), Map.Trammel); // out of range

        Track(new Item(0xB41)).MoveToWorld(new Point3D(hive.X - 1, hive.Y, 0), Map.Trammel);          // trough
        Track(new Pitcher(BeverageType.Water)).MoveToWorld(new Point3D(hive.X - 1, hive.Y + 1, 0), Map.Trammel);
        var empty = Track(new Pitcher(BeverageType.Water));
        empty.Quantity = 0;
        empty.MoveToWorld(new Point3D(hive.X - 1, hive.Y - 1, 0), Map.Trammel);
        Track(new Pitcher(BeverageType.Milk)).MoveToWorld(new Point3D(hive.X - 2, hive.Y, 0), Map.Trammel);

        Assert.Equal(2, hive.CountFlowers(now));
        Assert.Equal(2, hive.CountWater(now));

        hive.AgilityUntil = now + TimeSpan.FromHours(1);
        Assert.Equal(T.Range + T.AgilityRange, hive.RangeAt(now));
        Assert.Equal(3, hive.CountFlowers(now));                       // the far poppies are in reach now

        Assert.Equal(HiveNeed.TooLow, HouseBeehive.FlowerNeed(2, 5, T));    // 5 bees need 3 flowers
        Assert.Equal(HiveNeed.Fine, HouseBeehive.FlowerNeed(3, 5, T));
        Assert.Equal(HiveNeed.TooLow, HouseBeehive.WaterNeed(1, 5, T));     // and 2 water
        Assert.Equal(HiveNeed.Fine, HouseBeehive.WaterNeed(2, 5, T));
        Assert.Equal(HiveNeed.TooMuch, HouseBeehive.WaterNeed(2 * T.TooMuchWaterFactor + 1, 5, T));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AilmentsComeAndEachPotionDoesItsPart()
    {
        var (hive, owner) = LivingHive(population: 5, health: 60);
        var now = Core.Now;

        HouseBeehive.Roll = _ => 0; // both ailments
        hive.Day(now, 10, 3);
        Assert.True(hive.Parasites);
        Assert.True(hive.Disease);
        Assert.Equal(60 - 2 * T.HealthLossPerProblem, hive.Health);
        HouseBeehive.Roll = _ => 99;

        void Pour(BasePotion potion)
        {
            owner.Backpack.DropItem(potion);
            hive.Pour(owner, potion);
        }

        var cure = new CurePotion();
        Pour(new PoisonPotion());
        Assert.False(hive.Parasites);
        Assert.True(hive.Disease);
        Pour(cure);
        Assert.False(hive.Disease);
        Assert.True(cure.Deleted);

        var kept = new CurePotion();
        Pour(kept);                                                    // nothing to cure: kept
        Assert.False(kept.Deleted);

        var h = hive.Health;
        Pour(new HealPotion());
        Assert.Equal(h + T.HealAmount, hive.Health);
        Pour(new GreaterHealPotion());
        Assert.Equal(Math.Min(100, h + T.HealAmount + T.GreaterHealAmount), hive.Health);

        Pour(new StrengthPotion());
        Assert.True(hive.Hardy(Core.Now));
        hive.Health = 80;
        hive.Day(Core.Now, 0, 0);                                      // two problems, halved
        Assert.Equal(80 - T.HealthLossPerProblem, hive.Health);

        Pour(new AgilityPotion());
        Assert.Equal(T.Range + T.AgilityRange, hive.RangeAt(Core.Now));

        Assert.True(owner.Backpack.GetAmount(typeof(Bottle)) >= 6);    // each used potion leaves its bottle
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void NeglectEndsWithTheBeesLeavingAndTheHiveKept()
    {
        var (hive, _) = LivingHive(population: 3, health: 40);
        hive.Stage = HiveStage.Producing;
        hive.Honey = 4;
        var now = Core.Now;

        hive.Day(now, 0, 0);                                           // no flowers, no water: two problems
        Assert.Equal(40 - 2 * T.HealthLossPerProblem, hive.Health);
        Assert.Equal(2, hive.Population);                              // below shrinkHealth: a bee less

        for (var i = 0; i < 5 && hive.Lived; i++)
        {
            hive.Day(now, 0, 0);
        }

        Assert.False(hive.Lived);
        Assert.Equal(HiveStage.Empty, hive.Stage);
        Assert.Equal(0, hive.Population);
        Assert.False(hive.Deleted);
        Assert.Null(hive.Bees);
        Assert.Equal(4, hive.Honey);                                   // honey kept to harvest
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void HarvestGivesHoneyAndRawWaxAndTheWaxPotRendersIt()
    {
        var (hive, owner) = LivingHive();
        var tool = new HiveTool();
        owner.Backpack.DropItem(tool);
        Assert.Equal(T.HiveToolUses, tool.UsesRemaining);

        Assert.False(hive.Harvest(owner, tool));                       // nothing yet
        Assert.Equal(T.HiveToolUses, tool.UsesRemaining);

        hive.Honey = 3;
        hive.Wax = 7;
        Assert.True(hive.Harvest(owner, tool));
        Assert.Equal(3, owner.Backpack.GetAmount(typeof(JarHoney)));
        Assert.Equal(7, owner.Backpack.GetAmount(typeof(RawBeeswax)));
        Assert.Equal(0, hive.Honey);
        Assert.Equal(0, hive.Wax);
        Assert.Equal(T.HiveToolUses - 1, tool.UsesRemaining);

        tool.UsesRemaining = 1;
        hive.Honey = 1;
        Assert.True(hive.Harvest(owner, tool));
        Assert.True(tool.Deleted);                                     // worn out

        var stranger = Player(new Point3D(hive.X + 1, hive.Y + 1, 0), "Thief");
        var theirs = new HiveTool();
        stranger.Backpack.DropItem(theirs);
        hive.Honey = 2;
        Assert.False(hive.Harvest(stranger, theirs));
        Assert.Equal(2, hive.Honey);

        var pot = new WaxPot();
        owner.Backpack.DropItem(pot);
        Assert.NotNull(pot.Render(owner));                             // no heat source here
        Assert.Null(pot.Render(owner, checkHeat: false));
        var batch = T.WaxPotBatch;
        var made = batch - batch * T.WaxPotWastePercent / 100;
        Assert.Equal(7 - batch, owner.Backpack.GetAmount(typeof(RawBeeswax)));
        Assert.Equal(made, owner.Backpack.GetAmount(typeof(Beeswax)));

        Track(new Item(0x92B) { Movable = false }).MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), Map.Trammel);
        Assert.Null(pot.Render(owner));                                // at a stone oven
        Assert.Equal(0, owner.Backpack.GetAmount(typeof(RawBeeswax)));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheCandlesAreInAlchemyAndEachIsMadeFromBeeswax()
    {
        if (DefAlchemy.CraftSystem == null)
        {
            DefAlchemy.Initialize();
        }

        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        BeekeepingSystem.RegisterRecipes();
        Assert.Equal(T.Candles.Count, BeekeepingSystem.Candles.Count);
        Assert.NotNull(BeekeepingSystem.WaxPotCraft);
        Assert.Same(BeekeepingSystem.WaxPotCraft, DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(WaxPot)));

        var crafter = Player(new Point3D(Yard.X + 40, Yard.Y, 0), "Chandler");
        crafter.Skills.Alchemy.CapFixedPoint = 1500;
        crafter.Skills.Alchemy.Base = 150.0;

        foreach (var c in T.Candles)
        {
            var entry = DefAlchemy.CraftSystem.CraftItems.SearchFor(c.ItemType);
            Assert.NotNull(entry);
            Assert.Equal(BeekeepingSystem.CandlesGroup, entry.GroupNameString);
            Assert.Equal(typeof(Beeswax), entry.Resources[0].ItemType);
            Assert.Equal(c.Wax, entry.Resources[0].Amount);
            Assert.Equal(c.MinSkill, entry.Skills[0].MinSkill);

            var tool = new MortarPestle();
            crafter.Backpack.DropItem(tool);
            crafter.Backpack.DropItem(new Beeswax(c.Wax));
            crafter.Backpack.DropItem(new SpoolOfThread(c.Thread));
            var before = crafter.Backpack.Items.ToHashSet();
            entry.CompleteCraft(1, false, crafter, DefAlchemy.CraftSystem, typeof(Beeswax), tool, null);
            var made = crafter.Backpack.Items.FirstOrDefault(i => !before.Contains(i) && c.ItemType!.IsInstanceOfType(i));
            Assert.True(made != null, $"{c.Key} made nothing");
            Assert.Equal(c.ItemType, made!.GetType());
            Assert.Equal(0, crafter.Backpack.GetAmount(typeof(Beeswax)));
            Assert.Equal(0, crafter.Backpack.GetAmount(typeof(SpoolOfThread)));
            made.Delete();
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void EveryBeekeeperSellsTheBeekeepingRows()
    {
        var vendor = Track(new Beekeeper());
        vendor.MoveToWorld(new Point3D(Yard.X + 60, Yard.Y, 0), Map.Trammel);
        var buy = vendor.GetBuyInfo().OfType<GenericBuyInfo>().ToList();

        foreach (var row in T.Beekeeper)
        {
            var rows = buy.Where(b => b.Type == row.ItemType).ToList();
            Assert.True(rows.Count == 1, $"{row.Type}: {rows.Count} rows");
            Assert.Equal(row.Price, rows[0].Price);
        }

        Assert.Contains(buy, b => b.Type == typeof(JarHoney));         // stock rows stay
        Assert.Contains(buy, b => b.Type == typeof(Beeswax));
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void WildHivesSeedOnceGiveCombsRegrowAndGiveOneSwarm()
    {
        var spot = new WildSpot { Name = "fact spot", X = Yard.X + 80, Y = Yard.Y, Z = 0 };
        BeekeepingTable.Current = BeekeepingTable.BuildDefaults();
        BeekeepingTable.Validate(BeekeepingTable.Current, []);
        BeekeepingTable.Current.Wild.Spots = [spot];

        WildHiveSeeder.Seed();
        var wild = WildHiveSeeder.FindAt(Map.Trammel, spot.Location);
        Assert.NotNull(wild);
        _made.Add(wild!);
        WildHiveSeeder.Seed();
        var near = 0;
        foreach (var item in Map.Trammel.GetItemsInRange(spot.Location, 2))
        {
            if (item is WildBeehive)
            {
                near++;
            }
        }

        Assert.Equal(1, near);
        Assert.False(wild!.Movable);

        var pm = Player(new Point3D(spot.X + 1, spot.Y, 0), "Forager");
        pm.Hits = 3;
        var now = Core.Now;
        Assert.True(wild.TakeComb(pm, now));
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(Honeycomb)));
        Assert.True(pm.Hits >= 1);                                     // a sting never kills
        Assert.False(pm.Deleted || !pm.Alive);

        var comb = pm.Backpack.FindItemByType<Honeycomb>();
        comb.Open(pm);
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(JarHoney)));
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(RawBeeswax)));

        // A hive tool: a failed try, a swarm, then no second swarm before regrowth.
        var tool = new HiveTool();
        pm.Backpack.DropItem(tool);
        pm.Hits = pm.HitsMax;
        Assert.False(wild.CatchSwarm(pm, tool, now, 99));
        Assert.True(wild.CatchSwarm(pm, tool, now, 0));
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(BeeSwarm)));
        Assert.False(wild.CatchSwarm(pm, tool, now, 0));
        Assert.Equal(T.HiveToolUses - 2, tool.UsesRemaining);

        while (wild.Combs > 0)
        {
            pm.Hits = pm.HitsMax;
            wild.TakeComb(pm, now);
        }

        Assert.False(wild.TakeComb(pm, now));                          // emptied
        Assert.Equal(WildBeehive.EmptyHue, wild.Hue);
        Assert.False(wild.RegrowIfDue(now + TimeSpan.FromHours(T.Wild.RegrowHours) - TimeSpan.FromMinutes(1)));
        BeekeepingSystem.Sweep(now + TimeSpan.FromHours(T.Wild.RegrowHours));
        Assert.Equal(T.Wild.CombsPerHive, wild.Combs);
        Assert.False(wild.SwarmTaken);
        Assert.Equal(0, wild.Hue);
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void AHiveAndAWildHiveSurviveASaveAndLoad()
    {
        var original = Track(new HouseBeehive
        {
            Stage = HiveStage.Producing, Age = 7, Population = 9, Health = 85, Parasites = true, Disease = false,
            Honey = 5, Wax = 4, NextCheck = new DateTime(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc),
            StrengthUntil = new DateTime(2026, 11, 3, 20, 0, 0, DateTimeKind.Utc)
        });

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = new HouseBeehive(original.Serial);
        try
        {
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(HiveStage.Producing, copy.Stage);
            Assert.Equal(7, copy.Age);
            Assert.Equal(9, copy.Population);
            Assert.Equal(85, copy.Health);
            Assert.True(copy.Parasites);
            Assert.False(copy.Disease);
            Assert.Equal(5, copy.Honey);
            Assert.Equal(4, copy.Wax);
            Assert.Equal(original.NextCheck, copy.NextCheck);
            Assert.Equal(original.StrengthUntil, copy.StrengthUntil);
        }
        finally
        {
            copy.Delete();
        }

        var wild = Track(new WildBeehive("save spot") { Combs = 1, SwarmTaken = true });
        buffer = new byte[65536];
        writer = new BufferWriter(buffer, true);
        wild.Serialize(writer);
        writer.Flush();
        var wildCopy = new WildBeehive(wild.Serial);
        try
        {
            wildCopy.Deserialize(new BufferReader(buffer));
            Assert.Equal("save spot", wildCopy.Spot);
            Assert.Equal(1, wildCopy.Combs);
            Assert.True(wildCopy.SwarmTaken);
        }
        finally
        {
            wildCopy.Delete();
        }
    }
}
