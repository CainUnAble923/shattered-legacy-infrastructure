// NotedFixupsVerification.cs
//
// cc-P457 (cc-P402 F01-F04, F06, F07, F11-F13; cc-P429 W3). The second fix-up of the "Noted by the System" hidden
// achievements (customizations ClusterFNotedBySystem.cs). Each fact drives the game's own path where one exists: a kill
// through Mobile.Damage, a swing through Mining.CheckResources, a step through Mobile.Move (PlayerMobile.OnAfterMove,
// PlayerMobile-noted-step.patch) on tiles seeded for the fact, a deed through its own combine. Notes in shard-migration
// notes/cc-P457-noted-fixups-2.md.
//
// Facts:
//   1. A bard who hit the creature still touched it after it heals to full: no Not My Problem.
//   2. Empty vein at A, ore at B, empty at A again: the streak starts over at one.
//   3. Steps count where they land, at the player's height: onto snow and lava flow they count, off them they do not, a
//      floor over snow land is a floor, and a fire field a storey up is not underfoot.
//   4. Remote Work: an owner in sight 14 tiles away is near; 15 and 16 are remote.
//   5. Mule Train: three nearly full pack animals earn it; one on another facet, or a dead one, does not count.
//   6. Recycling counts only the recycler's own craft: a fresh item another player crafted does not (Chase, 10-22).
//   7. Every live counted tier: one short is not earned, the threshold is, and once more pays no more AP.
//   8. Crafted only: the large smith deed, a large commission's piece and a tailoring deed refuse a bought item; with
//      clusterf.bods.craftedOnly off, a small deed takes one.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Harvest;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Spells.Fourth;
using Server.Tests.Network;
using Xunit;

using N = Server.ClusterFNotedBySystem;
using A = Server.ClusterFAchievementSystem;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NotedFixupsVerification : IDisposable
{
    private static readonly Point3D Spot = new(1700, 1900, 0);

    private readonly List<Player> _players = [];
    private readonly List<IEntity> _things = [];

    public NotedFixupsVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (A.Definitions.Count == 0)
        {
            A.Configure();
        }

        LootTable.Current = LootTable.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ClusterFStoreBought.CraftedOnly = true;
        ShardTestHost.EnsureCraftSystems(); // the large commission's teaching pick reads the smithing menu
        ShardTestClock.Arm();
    }

    public void Dispose()
    {
        ClusterFFanfare.FlushAll();
        foreach (var p in _players)
        {
            p.Dispose();
        }

        foreach (var e in _things)
        {
            e.Delete();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p457{Guid.NewGuid():N}"[..16], "p457-test-only");
        public readonly PlayerMobile Pm;
        private readonly NetState _ns;

        // A client, unless the fact walks: pinned throttles a client's steps (Mobile.Move), and a test client has no clock.
        public Player(Point3D at, bool client)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P457 {Guid.NewGuid():N}"[..12] };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            if (client)
            {
                _ns = PacketTestUtilities.CreateTestNetState();
                _ns.Account = Account;
                Pm.NetState = _ns;
                _ns.Mobile = Pm;
            }
        }

        public int Count(TriggerKind kind) => A.GetCounter(Account.Username, A.NotedCounter(kind));

        public bool Has(string key) => A.HasEarned(Account, key);

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);

        public void Dispose()
        {
            if (_ns != null)
            {
                Pm.NetState = null;
                _ns.Mobile = null;
            }

            A.ResetForAccount(Account.Username);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private Player NewPlayer(Point3D at, bool client = true)
    {
        var p = new Player(at, client);
        _players.Add(p);
        return p;
    }

    private T Thing<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private T Placed<T>(T m, Point3D at, Map map = null) where T : Mobile
    {
        Thing(m).MoveToWorld(at, map ?? Map.Trammel);
        return m;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABardWhoHitTheCreatureStillTouchedItAfterItHealsToFull()
    {
        var at = new Point3D(Spot.X, Spot.Y, Spot.Z);
        var bard = NewPlayer(at);
        var provoked = Placed(new Orc(), new Point3D(at.X + 1, at.Y, at.Z));
        provoked.BardProvoked = true;
        provoked.BardMaster = bard.Pm;

        // Untouched: Not My Problem counts.
        var clean = Placed(new Orc(), new Point3D(at.X + 2, at.Y, at.Z));
        clean.Damage(clean.Hits + 50, provoked);
        Assert.Equal(1, bard.Count(TriggerKind.ProvokedKills));

        // Hit by the bard, then healed to full: pinned's list has forgotten the bard (Mobile.cs:2043); the kill does not count.
        var hit = Placed(new Orc(), new Point3D(at.X + 3, at.Y, at.Z));
        hit.Damage(1, bard.Pm);
        hit.Hits = hit.HitsMax;
        Assert.Null(hit.FindDamageEntryFor(bard.Pm));
        hit.Damage(hit.Hits + 50, provoked);
        Assert.False(hit.Alive);
        Assert.True(bard.Count(TriggerKind.ProvokedKills) == 1, "a creature the bard hit counted as never touched");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EmptyAtAOreAtBEmptyAtAStartsTheStreakOver()
    {
        var p = NewPlayer(new Point3D(Spot.X, Spot.Y + 10, Spot.Z));
        var mining = Mining.System;
        var def = mining.OreAndStone;
        var a = new Point3D(Spot.X + 10, Spot.Y + 10, Spot.Z);
        var b = new Point3D(Spot.X + 30, Spot.Y + 10, Spot.Z);
        var bank = def.GetBank(Map.Trammel, a.X, a.Y);
        bank.Consume(bank.Current, p.Pm);

        for (var i = 0; i < 19; i++)
        {
            Assert.False(mining.CheckResources(p.Pm, null, def, Map.Trammel, a, false));
        }

        Assert.True(mining.CheckResources(p.Pm, null, def, Map.Trammel, b, false)); // ore at B
        Assert.False(mining.CheckResources(p.Pm, null, def, Map.Trammel, a, false));
        Assert.False(p.Has(A.RockBottomKey), "the streak at A survived a swing at ore");

        // A fresh run of twenty at A still earns it.
        for (var i = 0; i < 19; i++)
        {
            mining.CheckResources(p.Pm, null, def, Map.Trammel, a, false);
        }

        Assert.True(p.Has(A.RockBottomKey));
    }

    // ---------------------------------------------------------------- 3

    // Rows read 2026-10-24 from D:/UO/UltimaOnlineVanilla/tiledata.mul (3,188,736 bytes, md5
    // dd3acaacb5d953827c80af71ed2f736d), field for field as TileData.Load reads it. Land: 0x0003 "grass" and 0x011A
    // "snow", both flags 0 (walkable; the first snow rows, 0x010C on, are impassable mountain). Items: 0x136E "lava"
    // flags 0x80020 (Damaging, not Impassable, not Surface, height 0: a lava flow walked through), and 0x0491 "floor
    // tile" flags 0x6201 (Surface), height 0. Lava land 0x01F4-0x01F7 and the lava pools are impassable.
    private const int GrassLand = 0x0003;
    private const int SnowLand = 0x011A;
    private static readonly TileRow LavaFlow = new(0x136E, "lava", 0x80020, 255, 0, 0, 0, 0, 0);
    private static readonly TileRow FloorTile = new(0x0491, "floor tile", 0x6201, 255, 0, 0, 0, 0, 0);

    private static readonly MethodInfo SetMaxLandValue =
        typeof(TileData).GetProperty(nameof(TileData.MaxLandValue))!.GetSetMethod(true)!;

    // The two land rows, with the land mask raised (it is 0 in the host, as MaxItemValue is: TestTileRows), and one row
    // of eight tiles of Trammel given to GroundAt through N.TilesFor: pinned's TileMatrix keeps no block set without map
    // files. Everywhere else is grass with nothing on it. All put back on dispose.
    private sealed class SeededRow : IDisposable
    {
        private readonly int _x0, _y;
        private readonly LandData _oldGrass, _oldSnow;
        private readonly int _oldMaxLand;
        private readonly int[] _land = new int[8];
        private readonly StaticTile[][] _statics = new StaticTile[8][];

        public SeededRow(int x0, int y)
        {
            _x0 = x0;
            _y = y;
            _oldGrass = TileData.LandTable[GrassLand];
            _oldSnow = TileData.LandTable[SnowLand];
            _oldMaxLand = TileData.MaxLandValue;

            TileData.LandTable[GrassLand] = new LandData("grass", TileFlag.None);
            TileData.LandTable[SnowLand] = new LandData("snow", TileFlag.None);
            SetMaxLandValue.Invoke(null, [TileData.LandTable.Length - 1]);

            for (var i = 0; i < 8; i++)
            {
                _land[i] = GrassLand;
                _statics[i] = [];
            }

            N.TilesFor = (map, x, ty) =>
                map == Map.Trammel && ty == _y && x >= _x0 && x < _x0 + 8
                    ? (new LandTile((short)_land[x - _x0], 0), _statics[x - _x0])
                    : (new LandTile(GrassLand, 0), []);
        }

        public void Land(int x, int id) => _land[x - _x0] = id;

        public void Static(int x, int id, int z) => _statics[x - _x0] = [new StaticTile((ushort)id, (sbyte)z)];

        public void Dispose()
        {
            N.TilesFor = null;
            TileData.LandTable[GrassLand] = _oldGrass;
            TileData.LandTable[SnowLand] = _oldSnow;
            SetMaxLandValue.Invoke(null, [_oldMaxLand]);
        }
    }

    [Fact]
    public void StepsCountWhereTheyLandAtThePlayersHeight()
    {
        // One block row: x0 grass (start), +1 snow, +2 grass, +3 lava flow on grass, +4 grass, +5 a floor on snow land,
        // +6 grass with a fire field a storey up.
        const int x0 = 4000, y0 = 3200;
        using var rows = TestTileRows.Seed(LavaFlow, FloorTile);
        using var block = new SeededRow(x0, y0);
        block.Land(x0 + 1, SnowLand);
        block.Static(x0 + 3, LavaFlow.ItemID, 0);
        block.Land(x0 + 5, SnowLand);
        block.Static(x0 + 5, FloorTile.ItemID, 0);
        var caster = Placed(new Orc(), new Point3D(x0 + 6, y0 + 6, 0)); // the field's timer asks its caster for sight
        var upstairs = Thing(new FireFieldItem(0x398C, new Point3D(x0 + 6, y0, 20), caster, Map.Trammel, TimeSpan.FromMinutes(10), 0));
        upstairs.MoveToWorld(new Point3D(x0 + 6, y0, 20), Map.Trammel); // AdjustField may have moved it; a storey up

        Assert.Equal(N.Ground.Snow, N.GroundAt(Map.Trammel, x0 + 1, y0, 0));
        Assert.Equal(N.Ground.Lava, N.GroundAt(Map.Trammel, x0 + 3, y0, 0));
        Assert.Equal(N.Ground.Other, N.GroundAt(Map.Trammel, x0 + 5, y0, 0));
        Assert.Equal(N.Ground.Fire, N.GroundAt(Map.Trammel, x0 + 6, y0, 20));
        Assert.Equal(N.Ground.Other, N.GroundAt(Map.Trammel, x0 + 6, y0, 0));

        var p = NewPlayer(new Point3D(x0, y0, 0), client: false);
        var chest = new PlateChest { Layer = Layer.InnerTorso }; // barefoot, in armor: the set that reads the ground
        p.Pm.AddItem(chest);
        p.Pm.Direction = Direction.East;

        void Step(int toX, int snow, int hot)
        {
            Assert.True(p.Pm.Move(Direction.East), $"the step to {toX} was refused");
            Assert.Equal(toX, p.Pm.X);
            Assert.True(snow == p.Count(TriggerKind.SnowTiles), $"at {toX - x0}: snow {p.Count(TriggerKind.SnowTiles)}, want {snow}");
            Assert.True(hot == p.Count(TriggerKind.HotFootTiles), $"at {toX - x0}: hot foot {p.Count(TriggerKind.HotFootTiles)}, want {hot}");
        }

        Step(x0 + 1, 1, 0); // onto snow
        Step(x0 + 2, 1, 0); // off it
        Step(x0 + 3, 1, 1); // into the lava flow
        Step(x0 + 4, 1, 1); // out of it
        Step(x0 + 5, 1, 1); // a floor over snow land
        Step(x0 + 6, 1, 1); // a fire field 20 above the feet
        Assert.False(upstairs.Deleted);

        // A turn in place is no step.
        p.Pm.Move(Direction.North);
        Assert.Equal(x0 + 6, p.Pm.X);
        Assert.Equal(1, p.Count(TriggerKind.SnowTiles));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void RemoteWorkFifteenTilesIsRemote()
    {
        foreach (var (dx, remote) in new[] { (14, false), (15, true), (16, true) })
        {
            var at = new Point3D(Spot.X, Spot.Y + 40 + dx, Spot.Z);
            var p = NewPlayer(at);
            var dog = Placed(new Dog(), new Point3D(at.X + dx, at.Y, at.Z));
            Assert.True(dog.SetControlMaster(p.Pm));
            var prey = Placed(new Rabbit(), new Point3D(at.X + dx + 1, at.Y, at.Z));
            prey.Damage(prey.Hits + 50, dog);
            Assert.False(prey.Alive);
            Assert.True(remote == p.Has(A.RemoteWorkKey), $"owner {dx} tiles away: remote {p.Has(A.RemoteWorkKey)}, want {remote}");
        }
    }

    // ---------------------------------------------------------------- 5

    private PackHorse FullMule(Player p, Point3D at, Map map = null)
    {
        var mule = Placed(new PackHorse(), at, map);
        Assert.True(mule.SetControlMaster(p.Pm));
        mule.ControlOrder = OrderType.Follow;
        var pack = mule.Backpack;
        Assert.NotNull(pack);
        pack.DropItem(new Item(0x1BF2) { Weight = pack.MaxWeight });
        return mule;
    }

    private static void TenSteps(Player p)
    {
        for (var i = 0; i < 10; i++)
        {
            N.Stepped(p.Pm, N.Ground.Other);
        }
    }

    [Fact]
    public void MuleTrainCountsLivingFollowersOnTheOwnersFacet()
    {
        // Three, all here: Mule Train.
        var at = new Point3D(Spot.X + 50, Spot.Y, Spot.Z);
        var p = NewPlayer(at);
        for (var i = 1; i <= 3; i++)
        {
            FullMule(p, new Point3D(at.X + i, at.Y, at.Z));
        }

        Assert.Equal(3, N.CountNearlyFullPackAnimals(p.Pm));
        TenSteps(p);
        Assert.True(p.Has(A.MuleTrainKey));

        // The third on Felucca at the same spot: two.
        var q = NewPlayer(new Point3D(at.X, at.Y + 5, at.Z));
        FullMule(q, new Point3D(at.X + 1, at.Y + 5, at.Z));
        FullMule(q, new Point3D(at.X + 2, at.Y + 5, at.Z));
        FullMule(q, new Point3D(at.X + 3, at.Y + 5, at.Z), Map.Felucca);
        Assert.Equal(2, N.CountNearlyFullPackAnimals(q.Pm));
        TenSteps(q);
        Assert.False(q.Has(A.MuleTrainKey), "a pack animal on another facet counted");

        // The third a dead bonded pet: two.
        var r = NewPlayer(new Point3D(at.X, at.Y + 10, at.Z));
        FullMule(r, new Point3D(at.X + 1, at.Y + 10, at.Z));
        FullMule(r, new Point3D(at.X + 2, at.Y + 10, at.Z));
        var ghost = FullMule(r, new Point3D(at.X + 3, at.Y + 10, at.Z));
        ghost.IsBonded = true;
        ghost.IsDeadPet = true;
        Assert.Equal(2, N.CountNearlyFullPackAnimals(r.Pm));
        TenSteps(r);
        Assert.False(r.Has(A.MuleTrainKey), "a dead pack animal counted");
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void RecyclingCountsOnlyTheRecyclersOwnCraft()
    {
        var crafter = NewPlayer(new Point3D(Spot.X, Spot.Y + 80, Spot.Z));
        var recycler = NewPlayer(new Point3D(Spot.X + 1, Spot.Y + 80, Spot.Z));

        // The craft hook (CraftItem-fail-streak-achievement.patch) records who made it; the item changes hands.
        var gift = Thing(new PlateGorget { PlayerConstructed = true, Quality = ArmorQuality.Exceptional, Created = Core.Now });
        N.CraftAttempt(crafter.Pm, typeof(PlateGorget), false, gift);
        Assert.Same(crafter.Pm, N.CrafterOf(gift));
        recycler.Pm.Backpack.DropItem(gift);
        N.Recycled(recycler.Pm, gift);
        Assert.Equal(0, recycler.Count(TriggerKind.FreshRecycles));
        Assert.False(recycler.Has(A.QualityControlKey), "another player's fresh exceptional craft counted");

        // The recycler's own: it counts.
        var own = Thing(new PlateGorget { PlayerConstructed = true, Created = Core.Now });
        N.CraftAttempt(recycler.Pm, typeof(PlateGorget), false, own);
        N.Recycled(recycler.Pm, own);
        Assert.Equal(1, recycler.Count(TriggerKind.FreshRecycles));
        Assert.True(recycler.Has(A.BuiltToBeRecycledKey));
    }

    // ---------------------------------------------------------------- 7

    // The counted kinds ClusterFNotedBySystem's header lists, each with a live event. The data-only kinds (no event yet)
    // and P262's own achievements, which share the "noted." prefix, are not among them.
    private static readonly TriggerKind[] LiveCounted =
    [
        TriggerKind.NeverReachedKills, TriggerKind.LowHpRetreats, TriggerKind.ProvokedKills, TriggerKind.PetDiedAtFullHp,
        TriggerKind.RetameAfterRelease, TriggerKind.FreshRecycles, TriggerKind.CraftFailStreak, TriggerKind.DepletedSwingStreak,
        TriggerKind.DiedAfterArrival, TriggerKind.RunebooksFilled, TriggerKind.OverweightSteps, TriggerKind.HotFootTiles,
        TriggerKind.SnowTiles, TriggerKind.PantslessTiles, TriggerKind.DiedAtHome, TriggerKind.SameTileBoxStreak,
        TriggerKind.BoxTooSoon
    ];

    private static readonly TriggerKind[] Streaks =
        [TriggerKind.CraftFailStreak, TriggerKind.DepletedSwingStreak, TriggerKind.SameTileBoxStreak];

    // Every live tier past the first event, at the count the P263 list gives it (notes/cc-P263-noted-by-the-system-
    // achievements.md). Written here, not read back from the definitions, so a changed number fails.
    private static readonly Dictionary<string, int> Thresholds = new()
    {
        [A.RockEnthusiastKey] = 50, [A.ProfessionalCowardKey] = 25, [A.SomebodyElsesProblemKey] = 100,
        [A.MeatShieldKey] = 10, [A.ItsComplicatedKey] = 10, [A.CircularEconomyKey] = 1000, [A.SkillIssueKey] = 10,
        [A.PersistenceKey] = 25, [A.RockBottomKey] = 20, [A.StillNothingKey] = 100, [A.DirectionallyChallengedKey] = 10,
        [A.RuneArchivistKey] = 10, [A.AmbitionKey] = 100, [A.BeastOfBurdenKey] = 1000, [A.HotFootKey] = 25,
        [A.FrostbittenKey] = 500, [A.PantslessWonderKey] = 1000, [A.HauntedPropertyKey] = 5, [A.SuperstitiousKey] = 5,
        [A.LuckySpotKey] = 25, [A.CantWaitKey] = 10
    };

    [Fact]
    public void EveryLiveCountedTierIsEarnedAtItsThresholdAndOnlyOnce()
    {
        var tiers = A.Definitions.Values
            .Where(d => d.Trigger.Threshold > 1 && LiveCounted.Contains(d.Trigger.Kind))
            .ToList();
        Assert.Equal(Thresholds.Keys.OrderBy(k => k), tiers.Select(d => d.Key).OrderBy(k => k));
        foreach (var def in tiers)
        {
            Assert.True(Thresholds[def.Key] == def.Trigger.Threshold, $"{def.Key} at {def.Trigger.Threshold}, the list says {Thresholds[def.Key]}");
        }

        var i = 0;
        foreach (var def in tiers)
        {
            var p = NewPlayer(new Point3D(Spot.X + 100 + i++, Spot.Y, Spot.Z));
            var kind = def.Trigger.Kind;
            var streak = Streaks.Contains(kind);
            void Event(int n)
            {
                if (streak)
                {
                    A.NotifyNotedStreak(p.Pm, kind, n);
                }
                else
                {
                    A.NotifyNoted(p.Pm, kind);
                }
            }

            var threshold = (int)def.Trigger.Threshold;
            for (var n = 1; n < threshold; n++)
            {
                Event(n);
            }

            Assert.False(p.Has(def.Key), $"{def.Key} earned one short of {threshold}");
            var ap = p.Data.AchievementPoints;
            Event(threshold);
            Assert.True(p.Has(def.Key), $"{def.Key} not earned at {threshold}");
            Assert.Equal(ap + def.AP, p.Data.AchievementPoints);

            var after = p.Data.AchievementPoints;
            Event(threshold + 1);
            Assert.Equal(after, p.Data.AchievementPoints);
            p.Dispose();
            _players.Remove(p);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void CraftedOnlyHoldsAtEveryOrderAndCanBeTurnedOff()
    {
        var p = NewPlayer(new Point3D(Spot.X, Spot.Y + 120, Spot.Z));
        var pack = p.Pm.Backpack;

        // The large smith deed (LargeBODItemFill).
        var large = Thing(new LargeSmithBOD(10, false, BulkMaterialType.None, null));
        large.Entries = LargeBulkEntry.ConvertEntries(large, LargeBulkEntry.LargePlate);
        var boughtChest = Thing(new PlateChest());
        pack.DropItem(boughtChest);
        LargeBODItemFill.Fill(large, p.Pm, boughtChest);
        Assert.Equal(0, large.Entries.Single(e => e.Details.Type == typeof(PlateChest)).Amount);
        var madeChest = Thing(new PlateChest { PlayerConstructed = true });
        pack.DropItem(madeChest);
        LargeBODItemFill.Fill(large, p.Pm, madeChest);
        Assert.Equal(1, large.Entries.Single(e => e.Details.Type == typeof(PlateChest)).Amount);

        // A large commission's piece.
        p.Pm.Skills.Blacksmith.Base = 50;
        var lc = SmithCommissionSystem.GenerateLarge(p.Pm);
        Assert.NotNull(lc);
        var key = lc.SetDef!.ItemKeys[0];
        var bought = Thing((Item)Activator.CreateInstance(SmithCommissionPool.GetItemType(key)!)!);
        if (bought is BaseArmor ba)
        {
            ba.Resource = lc.Material;
            if (lc.RequireExceptional)
            {
                ba.Quality = ArmorQuality.Exceptional;
            }
        }

        Assert.False(SmithCommissionSystem.IsMatchForPieceKey(lc, key, bought), "a bought piece filled a large commission");
        ClusterFAccountPersistence.GetOrCreateGuild(p.Pm).SmithLargeCommissions.Clear();

        // A tailoring deed (SmallBOD.EndCombine, SmallBOD-crafted-only.patch).
        var tailor = Thing(new SmallTailorBOD(0, 10, typeof(FancyShirt), 1027933, 0x1EFD, false, BulkMaterialType.None));
        var shopShirt = Thing(new FancyShirt());
        pack.DropItem(shopShirt);
        tailor.EndCombine(p.Pm, shopShirt);
        Assert.Equal(0, tailor.AmountCur);
        var sewnShirt = new FancyShirt { PlayerConstructed = true };
        pack.DropItem(sewnShirt);
        tailor.EndCombine(p.Pm, sewnShirt);
        Assert.Equal(1, tailor.AmountCur);

        // clusterf.bods.craftedOnly off: a small deed takes a bought item.
        var smith = Thing(new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None));
        var shopSword = Thing(new Longsword());
        pack.DropItem(shopSword);
        try
        {
            ClusterFStoreBought.CraftedOnly = false;
            smith.EndCombine(p.Pm, shopSword);
            Assert.Equal(1, smith.AmountCur);
        }
        finally
        {
            ClusterFStoreBought.CraftedOnly = true;
        }
    }
}
