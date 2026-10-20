// HighSeasFishingVerification.cs
//
// cc-P193: the fishing half of High Seas, first sitting. The fish catalogue (Services/Expansions/High Seas/Items/Fish),
// the lobster traps, the fish trophies, the two sea champions and their artifacts, the Fisherman's set, and the two
// patches that hand them to players (Fishing-high-seas-fish.patch, TaxidermyKit-fish-trophies.patch). Notes:
// shard-migration notes/cc-P193-port-high-seas-fishing.md.
//
// The test host registers the six maps with no tile data (D27), so every water here is "shore" to FishInfo (no deep
// water, no dungeon unless a region says so) and a LandTarget reads tile 0; the facts work with that rather than against
// it, and say so where it matters.
//
// Facts:
//   1. Every ported fish, crab, lobster, steak and meat constructs, has a label, round-trips; a rare one keeps its fisher
//      and date through the round trip, and wears the hue FishInfo gives its type.
//   2. The FishInfo table: 36 rows (16 rare, 19 legendary, the charybdis bait row), index and type agree both ways,
//      IsRareFish knows a rare crab and not a plain one.
//   3. The roll: a pole at shore gives shore fish (never crabs, never deep or dungeon fish) from 50 Fishing and nothing
//      under 50; a trap gives crabs and lobsters only; a Twisted Weald region makes the water a dungeon.
//   4. Bait strength: 1 for another fish, 2 (3 enhanced) for the baited one, plus a worn piece of fishing attire.
//   5. The Fishing patch: GetResourceType hands out High Seas fish, MutateType leaves a rare fish alone, Give signs and
//      weighs it.
//   6. The lobster trap: bobs count, a bob catches only crabs and lobsters into the trap, the twentieth bob is always
//      lost, and bait, owner and use survive a round trip.
//   7. Trophies: the table covers 35 fish, each a FishInfo row; the taxidermy kit, through its patched target, turns a
//      rare fish into a deed for ten boards and the kit; the deed builds the right addon; the addon folds back into a deed.
//   8. The sea champions: both construct, round-trip, list only constructible artifacts, register and release their
//      spawn (a tentacle, an eel), and award an artifact to a damager.
//   9. The artifacts, the bikini and the four Fisherman's pieces construct, round-trip, carry their labels and set values.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.CannedEvil;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Targeting;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HighSeasFishingVerification
{
    private readonly ITestOutputHelper _out;

    public HighSeasFishingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsurePoisons();
    }

    private static readonly Point3D Shore = new(1200, 1200, 0);

    private static PlayerMobile NewPlayer(double fishing, Point3D? at = null, Map map = null)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.Skills.Fishing.Base = fishing;
        pm.MoveToWorld(at ?? Shore, map ?? Map.Trammel);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!; // the runtime type, not T: a RareFish variable holds a BlueMarlin
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static void Cleanup(params IEntity[] entities)
    {
        foreach (var e in entities)
        {
            e?.Delete();
        }
    }

    /// <summary>Every fish item type this port adds: the FishInfo rows that are items, the common pools, the plain crab and lobster, the steaks and meats.</summary>
    private static IEnumerable<Type> CatalogueTypes()
    {
        var set = new HashSet<Type>();

        foreach (var info in FishInfo.FishInfos)
        {
            if (typeof(Item).IsAssignableFrom(info.Type))
            {
                set.Add(info.Type);
            }
        }

        foreach (var t in BaseHighseasFish.DeepWaterFish) set.Add(t);
        foreach (var t in BaseHighseasFish.ShoreFish) set.Add(t);
        foreach (var t in BaseHighseasFish.DungeonFish) set.Add(t);
        foreach (var t in BaseHighseasFish.LobstersAndCrabs) set.Add(t);
        foreach (var t in BaseHighseasFish.Lobsters) set.Add(t);
        foreach (var t in BaseHighseasFish.Crabs) set.Add(t);
        foreach (var t in Charydbis.Steaks) set.Add(t);

        return set;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryFishConstructsHasALabelAndRoundTrips()
    {
        var types = CatalogueTypes().ToList();
        Assert.True(types.Count >= 100, $"only {types.Count} catalogue types");

        var rare = 0;

        foreach (var type in types)
        {
            var item = Loot.Construct(type);
            Assert.True(item != null, $"{type.Name} did not construct");
            Assert.True(item.LabelNumber > 0, $"{type.Name} has no label");

            if (item is BaseHighseasFish fish)
            {
                Assert.True(fish.Stackable, $"{type.Name} is not stackable when caught");
            }

            var hue = FishInfo.GetFishHue(type);

            if (FishInfo.IsRareFish(type))
            {
                Assert.Equal(hue, item.Hue);
                rare++;
            }

            Item copy;

            if (item is RareFish rareFish)
            {
                var fisher = NewPlayer(50);
                rareFish.Fisher = fisher;
                rareFish.DateCaught = new DateTime(2026, 10, 20, 12, 0, 0);
                copy = RoundTrip(rareFish);
                Assert.Same(fisher, ((RareFish)copy).Fisher);
                Assert.Equal(rareFish.DateCaught, ((RareFish)copy).DateCaught);
                fisher.Delete();
            }
            else if (item is RareCrabAndLobster rareCrab)
            {
                var fisher = NewPlayer(50);
                rareCrab.Fisher = fisher;
                rareCrab.DateCaught = new DateTime(2026, 10, 20, 12, 0, 0);
                copy = RoundTrip(rareCrab);
                Assert.Same(fisher, ((RareCrabAndLobster)copy).Fisher);
                Assert.Equal(rareCrab.DateCaught, ((RareCrabAndLobster)copy).DateCaught);
                fisher.Delete();
            }
            else
            {
                copy = RoundTrip(item);
            }

            Assert.Equal(item.ItemID, copy.ItemID);
            Assert.Equal(item.Hue, copy.Hue);
            Assert.Equal(item.LabelNumber, copy.LabelNumber);

            Cleanup(item, copy);
        }

        Assert.Equal(35, rare); // 16 rare + 19 legendary (the charybdis row is a mobile)
        _out.WriteLine($"{types.Count} catalogue types, {rare} rare");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheTableHas36RowsAndKnowsItsIndexes()
    {
        var infos = FishInfo.FishInfos;
        Assert.Equal(36, infos.Count);
        Assert.Equal(16, infos.Count(i => i.BaseChance == FishInfo.RareChance));
        Assert.Equal(20, infos.Count(i => i.BaseChance == FishInfo.LegendaryChance)); // 19 legendary and the charybdis bait row

        for (var i = 0; i < infos.Count; i++)
        {
            Assert.Equal(i, FishInfo.GetIndexFromType(infos[i].Type));
            Assert.Same(infos[i].Type, FishInfo.GetTypeFromIndex(i));
            Assert.Equal(infos[i].Hue, FishInfo.GetFishHue(i));
        }

        Assert.Equal(-1, FishInfo.GetIndexFromType(typeof(Crab)));
        Assert.True(FishInfo.IsRareFish(typeof(StoneCrab)));
        Assert.True(FishInfo.IsRareFish(typeof(BlueMarlin)));
        Assert.False(FishInfo.IsRareFish(typeof(Crab)));
        Assert.False(FishInfo.IsRareFish(typeof(Fish)));
        Assert.False(FishInfo.IsRareFish(typeof(Amberjack)));

        // The map names resolved to maps; the region and keyword rows stayed strings.
        Assert.Same(Map.Trammel, FishInfo.GetInfo(typeof(YellowtailBarracuda)).Location);
        Assert.Same(Map.Tokuno, FishInfo.GetInfo(typeof(GiantKoi)).Location);
        Assert.Equal("Twisted Weald", FishInfo.GetInfo(typeof(UnicornFish)).Location);
        Assert.Equal("cannotfishup", FishInfo.GetInfo(typeof(Charydbis)).Location);
        Assert.Equal(1116086, (int)FishInfo.GetFishLabel(typeof(UnicornFish)));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ThePoleGivesShoreFishFromFiftyAndTheTrapGivesCrabs()
    {
        var pm = NewPlayer(60);
        var pole = new FishingPole();
        var trap = new LobsterTrap();
        pm.Backpack.DropItem(pole);
        pm.Backpack.DropItem(trap);
        try
        {
            var seen = new HashSet<Type>();

            for (var i = 0; i < 400; i++)
            {
                var type = FishInfo.GetSpecialItem(pm, pole, pm.Location, false);

                if (type == null)
                {
                    continue;
                }

                seen.Add(type);
                Assert.True(BaseHighseasFish.ShoreFish.Contains(type), $"a pole at shore gave {type.Name}");
            }

            Assert.True(seen.Count > 0, "400 casts at 60 Fishing gave no High Seas fish");

            pm.Skills.Fishing.Base = 49.9;

            for (var i = 0; i < 100; i++)
            {
                Assert.Null(FishInfo.GetSpecialItem(pm, pole, pm.Location, false));
            }

            pm.Skills.Fishing.Base = 60;
            var caught = new HashSet<Type>();

            for (var i = 0; i < 400; i++)
            {
                var type = FishInfo.GetSpecialItem(pm, trap, pm.Location, false);

                if (type == null)
                {
                    continue;
                }

                caught.Add(type);
                Assert.True(BaseHighseasFish.LobstersAndCrabs.Contains(type), $"a trap gave {type.Name}");
            }

            Assert.True(caught.Count > 0, "400 bobs at 60 Fishing caught nothing");

            // A rare-only roll (the lava trap's) never falls back to the common pools.
            for (var i = 0; i < 100; i++)
            {
                Assert.Null(FishInfo.GetSpecialItem(pm, trap, pm.Location, 0, true));
            }

            // A Twisted Weald region makes the water a dungeon: dungeon fish from 106, and no shore fish.
            var weald = new Region("Twisted Weald", Map.Trammel, 60,
                new Rectangle3D(new Point3D(Shore.X - 5, Shore.Y - 5, -128), new Point3D(Shore.X + 5, Shore.Y + 5, 127)));
            weald.Register();
            try
            {
                pm.MoveToWorld(Shore, Map.Trammel); // a mobile's region is read on a move, not when a region registers
                Assert.True(pm.Region.IsPartOf("Twisted Weald"));
                Assert.True(FishInfo.IsDungeon(pm.Location, pm.Map, pm.Region));
                pm.Skills.Fishing.Base = 110;
                var dungeon = new HashSet<Type>();

                for (var i = 0; i < 400; i++)
                {
                    var type = FishInfo.GetSpecialItem(pm, pole, pm.Location, false);

                    if (type == null)
                    {
                        continue;
                    }

                    dungeon.Add(type);
                    Assert.True(BaseHighseasFish.DungeonFish.Contains(type) || type == typeof(UnicornFish) || type == typeof(RainbowFish),
                        $"a pole in the Weald gave {type.Name}");
                }

                Assert.True(dungeon.Overlaps(BaseHighseasFish.DungeonFish), "400 casts in the Weald gave no dungeon fish");
            }
            finally
            {
                weald.Unregister();
            }

            _out.WriteLine($"shore pool seen {seen.Count}, trap pool seen {caught.Count}");
        }
        finally
        {
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void BaitStrengthIsTwoForTheBaitedFishAndAttireAddsItsBonus()
    {
        var pm = NewPlayer(100);
        try
        {
            var blue = FishInfo.GetInfo(typeof(BlueLobster));
            var red = FishInfo.GetInfo(typeof(StoneCrab));

            Assert.Equal(1.0, blue.GetBaitStrength(null, pm, false));
            Assert.Equal(1.0, blue.GetBaitStrength(typeof(StoneCrab), pm, true));
            Assert.Equal(2.0, blue.GetBaitStrength(typeof(BlueLobster), pm, false));
            Assert.Equal(3.0, blue.GetBaitStrength(typeof(BlueLobster), pm, true));
            Assert.Equal(2.0, red.GetBaitStrength(typeof(StoneCrab), pm, false));

            // AddItem, not EquipItem: the host has no tile data (D27), and the set tests wear their pieces this way.
            var hat = new FishermansHat();
            pm.AddItem(hat);
            Assert.Equal(2.1, blue.GetBaitStrength(typeof(BlueLobster), pm, false), 6);

            var vest = new FishermansVest();
            pm.AddItem(vest);
            Assert.Equal(2.2, blue.GetBaitStrength(typeof(BlueLobster), pm, false), 6);
        }
        finally
        {
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheFishingPatchHandsOutHighSeasFishAndSignsARareOne()
    {
        var pm = NewPlayer(60);
        var pole = new FishingPole();
        pm.Backpack.DropItem(pole);
        try
        {
            var system = Fishing.System;
            var def = system.GetDefinition();
            var resource = def.Resources[0];

            var seen = new HashSet<Type>();

            for (var i = 0; i < 300; i++)
            {
                var type = system.GetResourceType(pm, pole, def, pm.Map, pm.Location, resource);
                Assert.NotNull(type);
                seen.Add(type);
                Assert.True(type == typeof(Fish) || BaseHighseasFish.ShoreFish.Contains(type), $"GetResourceType gave {type.Name}");
            }

            Assert.True(seen.Overlaps(BaseHighseasFish.ShoreFish), "300 casts through the patched system gave no High Seas fish");

            // A rare fish survives MutateType, whatever the find table would roll at 120.
            pm.Skills.Fishing.Base = 120;

            for (var i = 0; i < 100; i++)
            {
                Assert.Same(typeof(BlueMarlin), system.MutateType(typeof(BlueMarlin), pm, pole, def, pm.Map, pm.Location, resource));
                Assert.Same(typeof(StoneCrab), system.MutateType(typeof(StoneCrab), pm, pole, def, pm.Map, pm.Location, resource));
            }

            // Give signs and weighs a rare fish and makes it one catch.
            var marlin = new BlueMarlin();
            Assert.True(marlin.Stackable);
            Assert.True(system.Give(pm, marlin, false));
            Assert.Same(pm, marlin.Fisher);
            Assert.NotEqual(DateTime.MinValue, marlin.DateCaught);
            Assert.False(marlin.Stackable);
            Assert.InRange(marlin.Weight, 1, 200);
            Assert.True(marlin.IsChildOf(pm.Backpack));

            // A common High Seas fish is not signed: it has no fisher to sign.
            var jack = new Amberjack();
            Assert.True(system.Give(pm, jack, false));
            Assert.True(jack.Stackable);

            _out.WriteLine($"GetResourceType gave {seen.Count} types; the marlin weighs {marlin.Weight}");
        }
        finally
        {
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheTrapBobsCatchesCrabsAndIsLostByTheTwentiethBob()
    {
        var pm = NewPlayer(60);
        var trap = new LobsterTrap();
        try
        {
            trap.MoveToWorld(new Point3D(Shore.X + 2, Shore.Y, 0), Map.Trammel);
            trap.Owner = pm;
            trap.InUse = true;
            trap.ItemID = LobsterTrap.BuoyID;
            Assert.Equal(0, trap.Bobs);

            // A bob counts: from -5 the count is -4 and the loss roll (bobs x 5 > a roll under 100) cannot fire, so the
            // count is read without the 5%-a-bob chance of losing the trap getting in the way.
            trap.Bobs = -5;
            trap.OnTick();
            Assert.False(trap.Deleted);
            Assert.Equal(-4, trap.Bobs);

            // Bob until something is caught (the roll gives a High Seas crab or lobster about half the time at 60 Fishing,
            // else half the bobs give a plain crab or lobster); each bob is reset below zero so the trap is never lost.
            var caught = false;

            for (var i = 1; i <= 20; i++)
            {
                trap.Bobs = -1;
                trap.OnTick();
                Assert.False(trap.Deleted, "the trap was lost on a bob that cannot lose it");

                if (trap.Items.Count > 0)
                {
                    caught = true;
                    break;
                }
            }

            {
                Assert.True(caught, "twenty bobs caught nothing");

                foreach (var item in trap.Items)
                {
                    Assert.IsAssignableFrom<BaseCrabAndLobster>(item);
                }

                // Bait: three uses of blue lobster bait; a catch uses one.
                trap.BaitType = typeof(BlueLobster);
                trap.BaitUses = 3;
                trap.EnhancedBait = true;
                var copy = RoundTrip(trap);
                Assert.Same(typeof(BlueLobster), copy.BaitType);
                Assert.Equal(3, copy.BaitUses);
                Assert.True(copy.EnhancedBait);
                Assert.Same(pm, copy.Owner);
                Assert.True(copy.InUse);
                Assert.Equal(trap.Bobs, copy.Bobs);
                copy.Delete();

                // The twentieth bob and later are always lost (bobs x 5 > a roll under 100).
                trap.Bobs = 20;
                trap.OnTick();
                Assert.True(trap.Deleted, "the trap survived its twenty-first bob");
            }

            // A fresh trap with no bait reads none, and a used-up bait clears itself on load.
            var fresh = new LobsterTrap();
            Assert.Null(fresh.BaitType);
            Assert.Equal(-1, fresh.BaitIndex);
            fresh.BaitType = typeof(StoneCrab);
            Assert.Equal(FishInfo.GetIndexFromType(typeof(StoneCrab)), fresh.BaitIndex);
            fresh.BaitType = null;
            Assert.Equal(-1, fresh.BaitIndex);
            Assert.False(fresh.EnhancedBait);
            fresh.Delete();

            var lava = new LavaLobsterTrap();
            Assert.Contains(0x1F4, lava.UseableTiles);
            Assert.DoesNotContain(0x00A8, lava.UseableTiles);
            Assert.Contains(0x00A8, new LobsterTrap().UseableTiles);
            lava.Delete();
        }
        finally
        {
            Cleanup(pm, trap);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheKitMakesAFishTrophyDeedAndTheDeedBuildsTheAddon()
    {
        Assert.Equal(35, FishTrophies.Table.Length);

        foreach (var info in FishTrophies.Table)
        {
            Assert.True(FishInfo.IsRareFish(info.FishType), $"{info.FishType.Name} is mounted but not in FishInfo");
            Assert.True(info.DeedNumber > 0 && info.AddonNumber > 0);
        }

        var pm = NewPlayer(100);
        pm.Skills.Carpentry.Base = 100;
        try
        {
            var kit = new TaxidermyKit();
            pm.Backpack.DropItem(kit);
            pm.Backpack.DropItem(new Board(10));

            var fisher = NewPlayer(100);
            var marlin = new BlackMarlin { Fisher = fisher, Weight = 150, DateCaught = new DateTime(2026, 10, 20) };
            pm.Backpack.DropItem(marlin);

            // Through the kit's own patched target, as a player would.
            kit.OnDoubleClick(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, marlin);

            Assert.True(marlin.Deleted, "the fish was not used up");
            Assert.True(kit.Deleted, "the kit was not used up");
            Assert.Equal(0, pm.Backpack.GetAmount(typeof(Board)));

            var deed = pm.Backpack.FindItemByType<FishTrophyDeed>();
            Assert.NotNull(deed);
            var info = FishTrophies.InfoFor(typeof(BlackMarlin));
            Assert.Equal(info.DeedNumber, deed.LabelNumber);
            Assert.Equal(info.AddonNumber, deed.TrophyName);
            Assert.Equal(150, deed.TrophyWeight);
            Assert.Same(fisher, deed.Fisher);
            Assert.Equal(FishTrophies.IndexOf(typeof(BlackMarlin)), deed.TrophyID);

            var copy = RoundTrip(deed);
            Assert.Equal(deed.TrophyID, copy.TrophyID);
            Assert.Equal(deed.TrophyWeight, copy.TrophyWeight);
            Assert.Same(fisher, copy.Fisher);
            copy.Delete();

            // A marlin hangs as three components, hued as the fish (2500), facing north.
            var addon = Assert.IsType<FishTrophy>(deed.ConstructTrophy(true));
            Assert.Equal(3, addon.Components.Count);
            Assert.All(addon.Components, c => Assert.Equal(2500, c.Hue));
            Assert.Same(typeof(BlackMarlin), addon.FishType);
            Assert.Equal(150, addon.FishWeight);

            var back = Assert.IsType<FishTrophyDeed>(addon.TrophyDeed);
            Assert.Equal(deed.LabelNumber, back.LabelNumber);
            Assert.Equal(deed.TrophyID, back.TrophyID);
            back.Delete();

            var saved = RoundTrip(addon);
            Assert.Same(typeof(BlackMarlin), saved.FishType);
            Assert.Equal(150, saved.FishWeight);
            saved.Delete();
            addon.Delete();

            // A dragonfish is one component; a lobster one; a mounting-board fish two.
            var dragon = new FishTrophy(typeof(AutumnDragonfish), TrophyStyle.Dragonfish, false, 1116185, 30, pm, Core.Now);
            Assert.Single(dragon.Components);
            Assert.Equal(17640, dragon.Components[0].ItemID);
            dragon.Delete();
            var board = new FishTrophy(typeof(BullFish), TrophyStyle.BoardMahi, true, 1116190, 30, pm, Core.Now);
            Assert.Equal(2, board.Components.Count);
            board.Delete();

            // Not a rare fish: the kit says so and keeps itself.
            var kit2 = new TaxidermyKit();
            pm.Backpack.DropItem(kit2);
            pm.Backpack.DropItem(new Board(10));
            var crab = new Crab();
            pm.Backpack.DropItem(crab);
            Assert.False(FishTrophies.TryMake(pm, kit2, crab));
            Assert.False(kit2.Deleted);
            Assert.False(crab.Deleted);

            fisher.Delete();
        }
        finally
        {
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheSeaChampionsConstructRoundTripSpawnAndAward()
    {
        var charybdis = new Charydbis();
        var osiredon = new Osiredon();
        try
        {
            charybdis.MoveToWorld(Shore, Map.Trammel);
            osiredon.MoveToWorld(new Point3D(Shore.X + 20, Shore.Y, 0), Map.Trammel);

            Assert.Equal("charydbis", charybdis.Name);
            Assert.Equal(1244, charybdis.Body.BodyID);
            Assert.Equal(30000, charybdis.HitsMax);
            Assert.True(charybdis.CanSwim && charybdis.CantWalk);
            Assert.Equal("osiredon the scalis enforcer", osiredon.Name);
            Assert.Equal(1068, osiredon.Body.BodyID);
            Assert.Equal(65000, osiredon.HitsMax); // ServUO asks 75,000; pinned BaseCreature.HitsMax stops at 65,000 (D-140)
            Assert.True(osiredon.NoGoodies);

            foreach (var champ in new BaseSeaChampion[] { charybdis, osiredon })
            {
                Assert.Empty(champ.StatueTypes);
                Assert.Equal(ChampionSkullType.None, champ.SkullType);

                foreach (var list in new[] { champ.UniqueList, champ.SharedList, champ.DecorativeList })
                {
                    foreach (var type in list)
                    {
                        var art = Loot.Construct(type);
                        Assert.True(art != null, $"{type.Name} did not construct");
                        art.Delete();
                    }
                }
            }

            Assert.Equal(4, charybdis.UniqueList.Length);
            Assert.Equal(4, charybdis.SharedList.Length);
            Assert.Equal(4, osiredon.UniqueList.Length);
            Assert.Empty(osiredon.SharedList);

            // Spawn bookkeeping: a tentacle registers with its master and leaves on delete; an eel the same.
            var tent = new GiantTentacle(charybdis);
            Assert.Equal(1, charybdis.Tentacles);
            var t2 = RoundTrip(tent);
            Assert.Same(charybdis, t2.Master);
            t2.Delete();
            tent.Delete();
            Assert.Equal(0, charybdis.Tentacles);

            var eel = new ParasiticEel(osiredon);
            Assert.Equal(1, osiredon.Eels);
            Assert.Same(Poison.DeadlyParasitic, eel.HitPoison);
            eel.Delete();
            Assert.Equal(0, osiredon.Eels);

            Assert.True(Charydbis.IsSeaTile(new LandTile(0xA8, -5)));
            Assert.False(Charydbis.IsSeaTile(new LandTile(0xA8, 0)));
            Assert.False(Charydbis.IsSeaTile(new LandTile(3, -5)));

            // The artifact goes to a damager in range with room in the pack.
            var pm = NewPlayer(100, new Point3D(Shore.X + 3, Shore.Y, 0));
            charybdis.RegisterDamage(pm, 500);
            var blade = new CullingBlade();
            charybdis.AwardArtifact(blade);
            Assert.True(blade.IsChildOf(pm.Backpack), "the artifact did not reach the damager");

            // A damager that is not a player is never registered.
            charybdis.RegisterDamage(osiredon, 500);
            var helm = new HelmOfVengence();
            charybdis.AwardArtifact(helm);
            Assert.True(helm.IsChildOf(pm.Backpack), "the artifact went elsewhere");
            pm.Delete();

            // Last, because a round-tripped copy takes the original's serial in the world and deleting the copy drops it:
            // a master lookup after this would find nothing (probed, not deduced; the test host's World, not the port).
            var c2 = RoundTrip(charybdis);
            Assert.Equal(charybdis.Body, c2.Body);
            Assert.Equal(charybdis.HitsMax, c2.HitsMax);
            c2.Delete();
            var o2 = RoundTrip(osiredon);
            Assert.Equal(osiredon.Body, o2.Body);
            o2.Delete();
        }
        finally
        {
            Cleanup(charybdis, osiredon);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheArtifactsAndTheFishermansSetConstructAndRoundTrip()
    {
        var types = new[]
        {
            typeof(CullingBlade), typeof(RuneEngravedPegLeg), typeof(SmilingMoonBlade), typeof(WandOfThunderingGlory),
            typeof(HelmOfVengence), typeof(LeviathanHideBracers), typeof(EnchantedCoralBracelet), typeof(RingOfTheSoulbinder),
            typeof(EnchantedBladeDeed), typeof(EnchantedVortexDeed), typeof(YellowPolkaDotBikini),
            typeof(FishermansHat), typeof(FishermansVest), typeof(FishermansEelskinGloves), typeof(FishermansTrousers),
            typeof(MentoSeasoning), typeof(SamuelsSecretSauce), typeof(DarkTruffle), typeof(FreshGinger)
        };

        foreach (var type in types)
        {
            var item = Loot.Construct(type);
            Assert.True(item != null, $"{type.Name} did not construct");
            Assert.True(item.LabelNumber > 0 || item.Name != null, $"{type.Name} has no name");
            var copy = RoundTrip(item);
            Assert.Equal(item.ItemID, copy.ItemID);
            Assert.Equal(item.Hue, copy.Hue);
            Cleanup(item, copy);
        }

        var blade = new CullingBlade();
        blade.GetDamageTypes(null, out var phys, out _, out _, out _, out _, out var chaos, out _);
        Assert.Equal(0, phys);
        Assert.Equal(100, chaos);
        Assert.Equal(40, blade.WeaponAttributes.HitLowerDefend);
        blade.Delete();

        var moon = new SmilingMoonBlade();
        moon.GetDamageTypes(null, out _, out _, out var cold, out _, out _, out _, out _);
        Assert.Equal(100, cold);
        moon.Delete();

        var bracers = new LeviathanHideBracers();
        Assert.Equal(2, bracers.AbsorptionAttributes.CastingFocus);
        Assert.Equal(SetItem.None, bracers.SetID);
        var b2 = RoundTrip(bracers);
        Assert.Equal(2, b2.AbsorptionAttributes.CastingFocus);
        Cleanup(bracers, b2);

        foreach (var piece in new ISetItem[] { new FishermansHat(), new FishermansVest(), new FishermansEelskinGloves(), new FishermansTrousers() })
        {
            Assert.Equal(SetItem.Fisherman, piece.SetID);
            Assert.Equal(4, piece.Pieces);
            Assert.Equal(2578, piece.SetHue);
            var attire = Assert.IsAssignableFrom<IFishingAttire>(piece);
            Assert.Equal(10, attire.BaitBonus);
            Assert.Equal(50, attire.SetBonus);
            ((Item)piece).Delete();
        }

        var trousers = new FishermansTrousers();
        Assert.Equal(1, trousers.ArmorAttributes.MageArmor);
        Assert.Equal(125, trousers.InitMinHits);
        trousers.Delete();

        var addon = new EnchantedBladeDeed().Addon;
        Assert.IsType<EnchantedBladeAddon>(addon);
        Assert.Equal(14240, addon.Components[0].ItemID);
        addon.Delete();

        var deed = new EnchantedVortexAddon().Deed;
        Assert.IsType<EnchantedVortexDeed>(deed);
        deed.Delete();
    }
}
