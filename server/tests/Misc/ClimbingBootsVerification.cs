// ClimbingBootsVerification.cs
//
// cc-P238 (F-36, ours). Climbing boots: customizations ClusterFClimbing.cs (the hop, the climbed-tile rules, the sale),
// ClimbingBoots.cs (the three tiers), ClusterFMinedBlocks.cs and ClusterFAccountData.MinedBlocks (the logbook's blocks),
// and server/patches PlayerMobile-climbed-tile-rules.patch and HousePlacement-climbed-tile.patch. Notes in
// shard-migration notes/cc-P238-climbing-boots.md.
//
// The test host has no map, so most facts give ClusterFClimbing a terrain grid of their own (FakeTerrain: mountain,
// walkable or blocked per tile, with its z); fact 3 runs the real map terrain against a seeded stone wall instead.
//
// Facts:
//   1 a hop lands only within range and on an allowed tile (mountain or walkable, z from the terrain), never on a
//     blocked one, never too high, never on the spot it stands.
//   2 refused into a town, a dungeon, a house region and a house; nothing moves and no stamina goes.
//   3 the real terrain refuses a landing inside a static (Map.CanFit), and takes the land's own z otherwise.
//   4 on a climbed tile: attack, harmful cast (CanBeHarmful), taming, Mark and house placement refused; a monster's harm
//     still reaches the climber; back on the ground all are allowed again.
//   5 the hop up orders followers to stay, the hop down to follow; none is released or moved.
//   6 stamina cost (more for a higher climb, half from Delver's) and the cooldown.
//   7 Deepwarden's ledge memory returns within 30 tiles and not beyond; lower tiers have none.
//   8 the Liaison's gates refuse one short of each (rank, Mining, blocks mined, price), take exactly the price, and an
//     upgrade consumes the boots before it; lost boots are recovered.
//   9 the logbook records a block once (the targeted tile's, not the miner's), lists its ores, and counts it.
//  10 the account's blocks round-trip through a save; an unknown record version is refused.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Spells.Sixth;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ClimbingBootsVerification : IDisposable
{
    private static readonly Point3D Spot = new(2000, 2000, 0);

    private readonly ITestOutputHelper _out;
    private readonly IClimbTerrain _realTerrain;
    private readonly FakeTerrain _terrain = new();
    private readonly List<Account> _accounts = [];
    private readonly List<IEntity> _things = [];
    private readonly List<Region> _regions = [];

    public ClimbingBootsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        _realTerrain = ClusterFClimbing.Terrain;
        ClusterFClimbing.Terrain = _terrain;
        ClusterFClimbing.ResetCooldowns();
    }

    public void Dispose()
    {
        ClusterFClimbing.Terrain = _realTerrain;
        ClusterFClimbing.ResetCooldowns();

        foreach (var r in _regions)
        {
            r.Unregister();
        }

        foreach (var e in _things)
        {
            e.Delete();
        }

        foreach (var a in _accounts)
        {
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    ClusterFAccountPersistence.Get(a)?.ClearGuildData();
                    m.Delete();
                }
            }

            Accounts.Remove(a);
        }
    }

    // ---------------------------------------------------------------- helpers

    private sealed class FakeTerrain : IClimbTerrain
    {
        public readonly Dictionary<(int X, int Y), ClimbTile> Tiles = new();

        public ClimbTile Probe(Map map, int x, int y, int nearZ) =>
            Tiles.TryGetValue((x, y), out var t) ? t : new ClimbTile(ClimbTileKind.Walkable, 0);

        public void Mountain(int x, int y, int z) => Tiles[(x, y)] = new ClimbTile(ClimbTileKind.Mountain, z);
        public void Blocked(int x, int y) => Tiles[(x, y)] = new ClimbTile(ClimbTileKind.Blocked, 0);
    }

    private PlayerMobile NewPlayer(Point3D? at = null)
    {
        var a = new Account($"p238{Guid.NewGuid():N}"[..16], "p238-test-only");
        _accounts.Add(a);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P238 {Guid.NewGuid():N}"[..12] };
        pm.AddItem(new Backpack());
        a[0] = pm;
        pm.RawDex = 100;
        pm.Stam = pm.StamMax;
        pm.MoveToWorld(at ?? Spot, Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = a;
        return pm;
    }

    private static ClimbingBoots Wear(PlayerMobile pm, int tier)
    {
        var boots = ClimbingBoots.Create(tier, ((IAccount)pm.Account).Username);
        pm.AddItem(boots);
        Assert.Same(pm, boots.Parent);
        Assert.Equal(Layer.Shoes, boots.Layer);
        return boots;
    }

    private T Thing<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private void AddRegion(Region r)
    {
        r.Register();
        _regions.Add(r);
    }

    private static ClusterFClimbing.HopResult Hop(PlayerMobile pm, ClimbingBoots b, int dx, int dy) =>
        ClusterFClimbing.TryHop(pm, b, pm.X + dx, pm.Y + dy);

    // A house with a floor (the host has no multi data), as LootBoxFanfareVerification's.
    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x0519, x, y, 0, TileFlag.None));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AHopLandsOnlyWithinRangeOnAnAllowedTile()
    {
        var pm = NewPlayer();
        var boots = Wear(pm, 1); // hop 4
        _terrain.Mountain(Spot.X + 3, Spot.Y, 30);
        _terrain.Mountain(Spot.X + 5, Spot.Y, 20);
        _terrain.Blocked(Spot.X + 2, Spot.Y);
        _terrain.Mountain(Spot.X, Spot.Y + 2, 100);

        Assert.Equal(ClusterFClimbing.HopResult.TooFar, Hop(pm, boots, 0, 0));
        Assert.Equal(ClusterFClimbing.HopResult.TooFar, Hop(pm, boots, 5, 0));   // a mountain, but 5 > 4
        Assert.Equal(ClusterFClimbing.HopResult.NoFooting, Hop(pm, boots, 2, 0));
        Assert.Equal(ClusterFClimbing.HopResult.TooHigh, Hop(pm, boots, 0, 2)); // 100 z up
        Assert.Equal(Spot, pm.Location);
        Assert.False(ClusterFClimbing.IsOnClimbedTile(pm));

        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 3, 0));
        Assert.Equal(new Point3D(Spot.X + 3, Spot.Y, 30), pm.Location); // z from the terrain, not the target
        Assert.True(ClusterFClimbing.IsOnClimbedTile(pm));

        // Not worn, no hop; a walkable tile is allowed too (the hop down).
        ClusterFClimbing.ResetCooldowns();
        var carried = ClimbingBoots.Create(1, ((IAccount)pm.Account).Username);
        pm.Backpack.DropItem(carried);
        Assert.Equal(ClusterFClimbing.HopResult.NotWorn, Hop(pm, carried, -1, 0));
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, -3, 1));
        Assert.Equal(new Point3D(Spot.X, Spot.Y + 1, 0), pm.Location);
        Assert.False(ClusterFClimbing.IsOnClimbedTile(pm));

        // Boots a GM made with [add bind to the first player to wear them.
        var wearer = NewPlayer(new Point3D(Spot.X + 40, Spot.Y, 0));
        var stranger = NewPlayer(new Point3D(Spot.X + 42, Spot.Y, 0));
        wearer.RawStr = stranger.RawStr = 50; // clothing refuses a wearer with no strength at all (BaseClothing.CanEquip)
        var added = Thing(new DeepwardensClimbingBoots());
        Assert.Equal("", added.BoundAccount);
        Assert.True(added.CanEquip(wearer));
        Assert.True(wearer.EquipItem(added));
        Assert.Equal(((IAccount)wearer.Account).Username, added.BoundAccount);
        Assert.False(added.CanEquip(stranger)); // now bound: another account's character cannot wear them

        // cc-P192's Fountain of Youth checks for a type named ClimbingBoots: its "climbing" access is available now.
        Assert.True(Server.Engines.FountainOfYouth.FountainPool.AccessAvailable("climbing"));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AHopIsRefusedIntoATownADungeonAndAHouse()
    {
        var pm = NewPlayer();
        var boots = Wear(pm, 3); // hop 12
        AddRegion(new TownRegion("p238 town", Map.Trammel, 50, new Rectangle3D(Spot.X + 2, Spot.Y - 1, -128, 3, 3, 256)));
        AddRegion(new DungeonRegion("p238 dungeon", Map.Trammel, 50, new Rectangle3D(Spot.X - 4, Spot.Y - 1, -128, 3, 3, 256)));
        var house = Thing(new TestHouse(pm));
        house.MoveToWorld(new Point3D(Spot.X, Spot.Y + 8, 0), Map.Trammel);

        var stam = pm.Stam;
        Assert.Equal(ClusterFClimbing.HopResult.Forbidden, Hop(pm, boots, 3, 0));   // the town
        Assert.Equal(ClusterFClimbing.HopResult.Forbidden, Hop(pm, boots, -3, 0));  // the dungeon
        Assert.Equal(ClusterFClimbing.HopResult.Forbidden, Hop(pm, boots, 0, 8));   // the house
        Assert.True(Server.Region.Find(new Point3D(Spot.X, Spot.Y + 8, 0), Map.Trammel).IsPartOf<HouseRegion>());
        Assert.Equal(Spot, pm.Location);
        Assert.Equal(stam, pm.Stam);

        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 0, -6));     // open country
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheRealTerrainRefusesALandingInsideAStatic()
    {
        using var tiles = TestTileRows.Seed(TileRows.StoneWall);
        var real = new MapClimbTerrain();
        var open = new Point3D(Spot.X + 30, Spot.Y + 30, 0);

        var before = real.Probe(Map.Trammel, open.X, open.Y, 0);
        Assert.Equal(ClimbTileKind.Walkable, before.Kind);
        Assert.Equal(Map.Trammel.GetAverageZ(open.X, open.Y), before.Z);

        var wall = Thing(new Item(0x0080) { Movable = false });
        wall.MoveToWorld(open, Map.Trammel);
        Assert.True(wall.ItemData.Impassable);
        Assert.Equal(ClimbTileKind.Blocked, real.Probe(Map.Trammel, open.X, open.Y, 0).Kind);

        // Through the hop, with the real terrain.
        ClusterFClimbing.Terrain = real;
        var pm = NewPlayer(new Point3D(open.X - 2, open.Y, 0));
        var boots = Wear(pm, 1);
        Assert.Equal(ClusterFClimbing.HopResult.NoFooting, Hop(pm, boots, 2, 0));
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 2, 1));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void OnAClimbedTileOnlyMiningProspectingAndHoppingAreLeft()
    {
        var pm = NewPlayer();
        var boots = Wear(pm, 1);
        var orc = Thing(new Orc());
        orc.MoveToWorld(new Point3D(Spot.X + 4, Spot.Y, 0), Map.Trammel);

        // On the ground: all allowed.
        Assert.True(pm.CanBeHarmful(orc, false));
        Assert.True(pm.AllowSkillUse(SkillName.AnimalTaming));
        Assert.True(pm.CheckSpellCast(new MarkSpell(pm, null)));

        _terrain.Mountain(Spot.X + 2, Spot.Y, 20);
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 2, 0));
        Assert.True(ClusterFClimbing.IsOnClimbedTile(pm));

        Assert.False(pm.CanBeHarmful(orc, false));                // attacks and harmful spells (the same check)
        Assert.False(pm.AllowSkillUse(SkillName.AnimalTaming));   // taming
        Assert.True(pm.AllowSkillUse(SkillName.Mining));          // mining is still allowed
        Assert.False(pm.CheckSpellCast(new MarkSpell(pm, null))); // Mark
        Assert.Equal(HousePlacementResult.BadRegion, HousePlacement.Check(pm, 0x64, new Point3D(Spot.X + 10, Spot.Y, 0), out _));
        Assert.True(orc.CanBeHarmful(pm, false));                 // a monster's harm still reaches the climber

        // Staff are never held to it.
        pm.AccessLevel = AccessLevel.GameMaster;
        Assert.False(ClusterFClimbing.IsOnClimbedTile(pm));
        pm.AccessLevel = AccessLevel.Player;

        ClusterFClimbing.ResetCooldowns();
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, -2, 0));
        Assert.True(pm.CanBeHarmful(orc, false));
        Assert.True(pm.AllowSkillUse(SkillName.AnimalTaming));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void PetsAreToldToStayAndThenToFollowAndNoneIsLost()
    {
        var pm = NewPlayer();
        var boots = Wear(pm, 1);
        var horse = Thing(new Horse());
        horse.MoveToWorld(new Point3D(Spot.X - 1, Spot.Y, 0), Map.Trammel);
        Assert.True(horse.SetControlMaster(pm));
        horse.ControlTarget = pm;
        horse.ControlOrder = OrderType.Follow;

        _terrain.Mountain(Spot.X + 3, Spot.Y, 25);
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 3, 0));
        Assert.Equal(OrderType.Stay, horse.ControlOrder);
        Assert.True(horse.Controlled);
        Assert.Same(pm, horse.ControlMaster);
        Assert.Equal(new Point3D(Spot.X - 1, Spot.Y, 0), horse.Location);

        ClusterFClimbing.ResetCooldowns();
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, -3, 1));
        Assert.Equal(OrderType.Follow, horse.ControlOrder);
        Assert.Same(pm, horse.ControlTarget);
        Assert.True(horse.Controlled);
        Assert.Same(pm, horse.ControlMaster);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void StaminaCostGrowsWithTheClimbAndTheCooldownApplies()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer();
        var boots = Wear(pm, 1);
        _terrain.Mountain(Spot.X + 2, Spot.Y, 32);
        _terrain.Mountain(Spot.X + 4, Spot.Y, 32);

        var stam = pm.Stam;
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 2, 0));
        Assert.Equal(stam - (5 + 32 / 4), pm.Stam);                // 13 for 32 z up

        Assert.Equal(ClusterFClimbing.HopResult.Cooldown, Hop(pm, boots, 2, 0));
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(2400));
        Assert.Equal(ClusterFClimbing.HopResult.Cooldown, Hop(pm, boots, 2, 0));
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(200));
        stam = pm.Stam; // read on the hop's own tick: the host regenerates stamina as the clock moves (Q-060)
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 2, 0));
        Assert.Equal(stam - 5, pm.Stam);                           // level: the base cost only

        Assert.Equal(13, ClusterFClimbing.StaminaCost(1, 32));
        Assert.Equal(7, ClusterFClimbing.StaminaCost(2, 32));      // Surefoot: half, rounded up
        Assert.Equal(7, ClusterFClimbing.StaminaCost(3, 32));
        Assert.Equal(5, ClusterFClimbing.StaminaCost(1, -20));     // down costs no more than level

        ShardTestClock.Advance(TimeSpan.FromSeconds(3));
        pm.Stam = 4;
        Assert.Equal(ClusterFClimbing.HopResult.TooTired, Hop(pm, boots, -2, 0));
        Assert.Equal(4, pm.Stam);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void LedgeMemoryReturnsWithinThirtyTilesAndNotBeyond()
    {
        var pm = NewPlayer();
        var boots = Wear(pm, 3);
        var ledge = new Point3D(Spot.X + 5, Spot.Y, 40);
        _terrain.Mountain(ledge.X, ledge.Y, ledge.Z);

        Assert.Equal(ClusterFClimbing.HopResult.NoLedge, ClusterFClimbing.TryLedgeReturn(pm, boots)); // none yet

        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(pm, boots, 5, 0));
        Assert.Equal((Map.Trammel, ledge), (boots.LedgeMap, boots.LedgeLocation));

        // Down, then walk 30 tiles away: one hop back up.
        pm.MoveToWorld(new Point3D(ledge.X + 30, ledge.Y, 0), Map.Trammel);
        ClusterFClimbing.ResetCooldowns();
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, ClusterFClimbing.TryLedgeReturn(pm, boots));
        Assert.Equal(ledge, pm.Location);

        // 31 tiles away: no.
        pm.MoveToWorld(new Point3D(ledge.X - 31, ledge.Y, 0), Map.Trammel);
        ClusterFClimbing.ResetCooldowns();
        Assert.Equal(ClusterFClimbing.HopResult.NoLedge, ClusterFClimbing.TryLedgeReturn(pm, boots));
        Assert.Equal(new Point3D(ledge.X - 31, ledge.Y, 0), pm.Location);

        // Delver's boots remember nothing.
        var other = NewPlayer(new Point3D(Spot.X, Spot.Y + 20, 0));
        var delvers = Wear(other, 2);
        _terrain.Mountain(other.X + 3, other.Y, 20);
        Assert.Equal(ClusterFClimbing.HopResult.Hopped, Hop(other, delvers, 3, 0));
        Assert.Null(delvers.LedgeMap);
        other.MoveToWorld(new Point3D(other.X + 2, other.Y, 0), Map.Trammel);
        ClusterFClimbing.ResetCooldowns();
        Assert.Equal(ClusterFClimbing.HopResult.NoLedge, ClusterFClimbing.TryLedgeReturn(other, delvers));
    }

    // ---------------------------------------------------------------- 8

    // A Compact member who meets every gate of a tier, with its price in the pack.
    private (PlayerMobile Pm, CharacterGuildData Guild) Qualified(int tier)
    {
        var t = ClusterFClimbing.Tier(tier);
        var pm = NewPlayer();
        var acct = (IAccount)pm.Account;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var guild = data.GetOrCreateGuildData(pm.Serial);
        guild.JoinedGuilds.Add("mining");
        guild.GuildReputation["mining"] = t.RankStanding;
        guild.GuildCurrency["mining"] = t.Vouchers;
        pm.Skills.Mining.Base = t.Mining;
        for (var i = 0; i < t.BlocksMined; i++)
        {
            data.MinedBlocks.Record("Trammel", 100 + i, 50, "Iron", Core.Now);
        }

        pm.Backpack.DropItem(new Gold(t.Gold));
        var ingots = t.IngotType.CreateInstance<Item>(); // Platinum and the rest take an optional amount
        ingots.Amount = t.Ingots;
        pm.Backpack.DropItem(ingots);
        return (pm, guild);
    }

    [Fact]
    public void TheGatesRefuseOneShortOfEachAndTheUpgradeConsumesTheBootsBefore()
    {
        var t1 = ClusterFClimbing.Tier(1);
        foreach (var shortOf in new[] { "rank", "mining", "blocks", "vouchers", "gold", "ingots" })
        {
            var (pm, guild) = Qualified(1);
            var acct = (IAccount)pm.Account;
            switch (shortOf)
            {
                case "rank": guild.GuildReputation["mining"] = t1.RankStanding - 1; break;
                case "mining": pm.Skills.Mining.Base = t1.Mining - 0.1; break;
                case "blocks":
                    var data = ClusterFAccountPersistence.GetOrCreate(acct);
                    var record = new MinedBlockRecord();
                    for (var i = 0; i < t1.BlocksMined - 1; i++)
                    {
                        record.Record("Trammel", 100 + i, 50, "Iron", Core.Now);
                    }

                    typeof(ClusterFAccountData).GetProperty(nameof(ClusterFAccountData.MinedBlocks))!.SetValue(data, record);
                    break;
                case "vouchers": guild.GuildCurrency["mining"] = t1.Vouchers - 1; break;
                case "gold": pm.Backpack.ConsumeTotal(typeof(Gold), 1); break;
                case "ingots": pm.Backpack.ConsumeTotal(t1.IngotType, 1); break;
            }

            var gold = pm.Backpack.GetAmount(typeof(Gold));
            var vouchers = guild.GetCurrency("mining");
            Assert.False(ClusterFClimbing.BuyNext(pm, guild, out var why), $"one short of {shortOf}");
            Assert.Null(ClusterFClimbing.BootsOn(pm));
            Assert.Equal(gold, pm.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(vouchers, guild.GetCurrency("mining"));
            _out.WriteLine($"one short of {shortOf}: {why}");
        }

        // Every gate met: bought, the exact price taken, bound and registered.
        var (buyer, g) = Qualified(1);
        var buyerAcct = (IAccount)buyer.Account;
        Assert.True(ClusterFClimbing.BuyNext(buyer, g, out var bought), bought);
        var boots = Assert.IsType<SurveyorsClimbingBoots>(ClusterFClimbing.BootsOn(buyer));
        Assert.Equal(0, buyer.Backpack.GetAmount(typeof(Gold)));
        Assert.Equal(0, g.GetCurrency("mining"));
        Assert.Equal(0, buyer.Backpack.GetAmount(t1.IngotType));
        Assert.Equal(LootType.Blessed, boots.LootType);
        Assert.Equal(buyerAcct.Username, boots.BoundAccount);
        Assert.Equal(1, ClusterFClimbing.RegisteredTier(buyerAcct));
        Assert.Equal(CraftResources.GetHue(CraftResource.Verite), boots.Hue);

        // The upgrade to Delver's: one short of the next rank is refused; met, it consumes the Surveyor's.
        var t2 = ClusterFClimbing.Tier(2);
        g.GuildReputation["mining"] = t2.RankStanding - 1;
        g.GuildCurrency["mining"] = t2.Vouchers;
        buyer.Skills.Mining.Base = t2.Mining;
        var data2 = ClusterFAccountPersistence.GetOrCreate(buyerAcct);
        for (var i = data2.MinedBlocks.Count; i < t2.BlocksMined; i++)
        {
            data2.MinedBlocks.Record("Felucca", 100 + i, 60, "Iron", Core.Now);
        }

        buyer.Backpack.DropItem(new Gold(t2.Gold));
        var frost = t2.IngotType.CreateInstance<Item>();
        frost.Amount = t2.Ingots;
        buyer.Backpack.DropItem(frost);

        Assert.False(ClusterFClimbing.BuyNext(buyer, g, out _));
        g.GuildReputation["mining"] = t2.RankStanding;
        Assert.True(ClusterFClimbing.BuyNext(buyer, g, out var upgraded), upgraded);
        Assert.True(boots.Deleted);
        Assert.IsType<DelversClimbingBoots>(ClusterFClimbing.BootsOn(buyer));
        Assert.Equal(2, ClusterFClimbing.RegisteredTier(buyerAcct));

        // Lost: recovered for the restore price, as the satchels are.
        ClusterFClimbing.BootsOn(buyer)!.Delete();
        g.GuildCurrency["mining"] = ClusterFClimbing.RestoreVouchers;
        Assert.True(ClusterFClimbing.Recover(buyer, g, out var recovered), recovered);
        Assert.IsType<DelversClimbingBoots>(ClusterFClimbing.BootsOn(buyer));
        Assert.Equal(0, g.GetCurrency("mining"));
        Assert.False(ClusterFClimbing.Recover(buyer, g, out _));

        // The Liaison's page shows the count and the gates.
        var page = new MinersCompactLiaisonGump(buyer, MinersCompactLiaisonGump.View.ClimbingBoots);
        var labels = page.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
        Assert.Contains(labels, l => l == $"Blocks mined (Prospector's Logbook): {t2.BlocksMined:N0}");
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheLogbookRecordsTheTargetedBlockOnceAndCountsIt()
    {
        var pm = NewPlayer(new Point3D(807, 400, 0)); // the last column of bank 100
        var acct = (IAccount)pm.Account;
        var mining = Mining.System;
        var def = mining.OreAndStone;
        var res = def.Resources[0];

        void Mine(Point3D target, Item ore)
        {
            mining.MutateType(ore.GetType(), pm, null, def, Map.Trammel, target, res);
            mining.Give(pm, ore, false);
        }

        Mine(new Point3D(809, 400, 0), new IronOre(2));          // targeted bank 101, not the miner's 100
        var blocks = ClusterFAccountPersistence.GetOrCreate(acct).MinedBlocks;
        Assert.Equal(1, blocks.Count);
        Assert.True(blocks.Blocks.ContainsKey(("Trammel", 101, 50)));

        Mine(new Point3D(814, 405, 0), new CopperOre(2));        // the same bank again: still one, now two ores
        Assert.Equal(1, blocks.Count);
        Assert.Equal(["Copper", "Iron"], blocks.Blocks[("Trammel", 101, 50)].Ores.ToArray());

        Mine(new Point3D(806, 400, 0), new IronOre(2));          // the next bank west
        Assert.Equal(2, blocks.Count);

        mining.Give(pm, new IronOre(1), false);                  // no target noted (not a swing): not counted
        Assert.Equal(2, blocks.Count);

        var logbook = new ProspectorsLogbookGump(pm, ClusterFAccountPersistence.GetOrCreate(acct));
        Assert.Contains(logbook.Entries.OfType<GumpLabel>(), l => l.Text == "Blocks mined: 2");
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void TheAccountsBlocksRoundTripThroughASave()
    {
        var data = new ClusterFAccountData();
        var when = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        data.MinedBlocks.Record("Trammel", 101, 50, "Iron", when);
        data.MinedBlocks.Record("Trammel", 101, 50, "Copper", when.AddDays(1));
        data.MinedBlocks.Record("Felucca", 7, 8, "Valorite", when.AddDays(2));

        var buffer = new byte[65536];
        var w = new BufferWriter(buffer, true);
        data.Serialize(w);
        w.Flush();

        var read = new ClusterFAccountData(new BufferReader(buffer));
        Assert.Equal(2, read.MinedBlocks.Count);
        var b = read.MinedBlocks.Blocks[("Trammel", 101, 50)];
        Assert.Equal(when, b.FirstMined);
        Assert.Equal(["Copper", "Iron"], b.Ores.ToArray());
        Assert.Equal(["Valorite"], read.MinedBlocks.Blocks[("Felucca", 7, 8)].Ores.ToArray());

        var bad = new byte[16];
        var bw = new BufferWriter(bad, true);
        bw.Write(MinedBlockRecord.CurrentVersion + 1);
        bw.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => new MinedBlockRecord(new BufferReader(bad)));
    }
}
