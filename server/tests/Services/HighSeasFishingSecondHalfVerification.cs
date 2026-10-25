// HighSeasFishingSecondHalfVerification.cs
//
// cc-P269: High Seas fishing, the second half. The baits and hooks on the pole (FishingPole-high-seas-bait-hooks.patch,
// Items/Fish/Bait.cs, Hooks.cs), the patched cast (Fishing-high-seas-bait-hooks-lava.patch, HighSeasFishing.cs), lava
// fishing, the fish pies (Items/Fish/FishPies.cs, AOS-fish-pies.patch, the cooking recipes) and the Oracle of the Sea
// with the charybdis spawner. Notes: shard-migration notes/cc-P269-high-seas-fishing-second-half.md.
//
// The test host has no tile data (D27): a LandTarget reads tile 0 and a StaticTarget's ItemID is masked to 0, so the
// casts here aim at a Static item (a water static 0x1797, a lava static 4846) beside the fisher, which pinned's
// GetHarvestDetails reads by its own ItemID. Random numbers are pinned where a fact needs them by swapping
// BuiltInRng.Generator for a constant source (as CraftXVerification).
//
// Facts:
//   1. A bait changes the catch: at the same roll an unbaited pole gives a shore fish where a pole baited for the fairy
//      salmon gives the salmon (twice the chance; three times enhanced; bait for another fish does nothing), and a
//      baited cast through the patched Fishing uses one bait.
//   2. Bait goes on a pole through its target, swaps, pours into a cup of the same bait, and refuses the wrong gear.
//   3. A hook's uses: tied on through its target (its hue on the pole, an old hook back in the pack), worn one use by
//      each cast that catches, gone at none (the pole's own hue back); the junk-proof hook skips the footwear row; the
//      pole's bait and hook survive a save.
//   4. Lava fishing only in lava: no definition for lava without the hook, the water definition for a hooked pole on
//      water, and with the hook on lava the patched StartHarvesting takes the cast and FinishHarvesting gives lava's
//      catch (a stone paver at this roll, never a fish).
//   5. A fish pie's buff: +5 damage increase while it lasts and gone after, one of a kind at a time, one pie eaten off
//      a stack; a fire soak takes 5 off a fire hit; the Meditation pie raises the skill; all sixteen construct and
//      round-trip, and the sixteen recipes are on the cooking menu.
//   6. The Oracle and the charybdis spawner: the Oracle names a spot and spends a charge, an empty one does nothing, a
//      charybdis-baited cast into the spot with 100 Fishing snags Charybdis, and the spawner round-trips.
//   7. Corgul: his artifacts construct, his odds by facet, 75 crew on his island, a round trip with the crew, and at
//      death the crew die and the corpse holds a message in a bottle and two nets.
//   8. A profession fish order: the port of delivery, one line of ten shore fish, the crate in the boat's hold, only the
//      ordered fish go in and stay in, delivery pays a reward and standing, a destroyed crate forfeits, the standings
//      survive a save, ServUO's tiers and categories, a tier 1 bait reward.
//   9. [GenFishMongers: sixteen mongers (eight docks, two facets) with their ports, then a re-run skips them all.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Engines.Quests;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HighSeasFishingSecondHalfVerification
{
    private static readonly Point3D Shore = new(1200, 1200, 0);
    private const int WaterStatic = 0x1797;
    private const int LavaStatic = 4846;

    private readonly ITestOutputHelper _out;

    public HighSeasFishingSecondHalfVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsurePoisons();
    }

    // Every draw returns Value.
    private sealed class ConstantRandom : Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private static ConstantRandom PinRandom(double value)
    {
        var rng = new ConstantRandom { Value = value };
        BuiltInRng.Generator = rng;
        return rng;
    }

    private static PlayerMobile NewPlayer(double fishing, Point3D? at = null, Map map = null)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.RawStr = 100;
        pm.Skills.Fishing.Base = fishing;
        pm.MoveToWorld(at ?? Shore, map ?? Map.Trammel);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static Static Tile(int itemID, Point3D at, Map map)
    {
        var tile = new Static(itemID);
        tile.MoveToWorld(at, map);
        return tile;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
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

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABaitChangesTheCatch()
    {
        // The fairy salmon: a rare fish of Ter Mur, not deep water, from 85.8 Fishing, at 0.75% a cast.
        var pm = NewPlayer(100, map: Map.TerMur);
        var pole = new FishingPole();
        pm.Backpack.DropItem(pole);
        try
        {
            // 0.012: over the salmon's plain 0.0075, under its baited 0.015. The roll's fallback (a shore fish) takes
            // the same draw and lands, so an unbaited cast is a common fish and a baited one the salmon.
            PinRandom(0.012);

            var plain = FishInfo.GetSpecialItem(pm, pole, pm.Location, false);
            Assert.NotNull(plain);
            Assert.True(BaseHighseasFish.ShoreFish.Contains(plain), $"an unbaited cast gave {plain.Name}");

            pole.BaitType = typeof(FairySalmon);
            pole.BaitUses = 5;
            Assert.Same(typeof(FairySalmon), FishInfo.GetSpecialItem(pm, pole, pm.Location, false));

            // Bait for another fish is no bait for this one.
            pole.BaitType = typeof(FireFish);
            Assert.True(BaseHighseasFish.ShoreFish.Contains(FishInfo.GetSpecialItem(pm, pole, pm.Location, false)));

            // Enhanced bait triples it: at 0.02 plain bait (0.015) misses and enhanced (0.0225) lands.
            pole.BaitType = typeof(FairySalmon);
            PinRandom(0.02);
            Assert.NotSame(typeof(FairySalmon), FishInfo.GetSpecialItem(pm, pole, pm.Location, false));
            pole.EnhancedBait = true;
            Assert.Same(typeof(FairySalmon), FishInfo.GetSpecialItem(pm, pole, pm.Location, false));

            // Through the patched cast: a baited catch uses one bait, and the bait type is cleared with the last.
            PinRandom(0.0);
            pole.EnhancedBait = false;
            pole.BaitUses = 2;
            var water = Tile(WaterStatic, new Point3D(pm.X + 2, pm.Y, 0), pm.Map);
            try
            {
                var system = Fishing.System;
                var def = system.GetDefinition(WaterStatic, false);
                Assert.NotNull(def);

                system.FinishHarvesting(pm, pole, def, water, new object());
                Assert.Equal(1, pole.BaitUses);
                Assert.Same(typeof(FairySalmon), pole.BaitType);

                system.FinishHarvesting(pm, pole, def, water, new object());
                Assert.Equal(0, pole.BaitUses);
                Assert.Null(pole.BaitType);
                Assert.False(pole.EnhancedBait);
            }
            finally
            {
                Cleanup(water);
            }
        }
        finally
        {
            BuiltInRng.Reset();
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BaitGoesOnThePoleThroughItsTarget()
    {
        var pm = NewPlayer(60);
        var pole = new FishingPole();
        var trap = new LobsterTrap();
        pm.Backpack.DropItem(pole);
        pm.Backpack.DropItem(trap);

        var salmonIndex = FishInfo.GetIndexFromType(typeof(FairySalmon));
        var bait = new Bait(salmonIndex) { UsesRemaining = 1 };
        pm.Backpack.DropItem(bait);
        try
        {
            Assert.Same(typeof(FairySalmon), bait.BaitType);
            Assert.Equal(FishInfo.GetFishHue(typeof(FairySalmon)), bait.Hue);

            // Fish bait refuses a lobster trap and stays.
            bait.OnDoubleClick(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, trap);
            Assert.Null(trap.BaitType);
            Assert.False(bait.Deleted);

            // One use: the cursor comes at once; the pole takes the bait and the empty cup is destroyed.
            bait.OnDoubleClick(pm);
            pm.Target.Invoke(pm, pole);
            Assert.Same(typeof(FairySalmon), pole.BaitType);
            Assert.Equal(1, pole.BaitUses);
            Assert.True(bait.Deleted);

            // Several uses: the amount asked for goes on; enhanced bait makes the pole enhanced.
            var more = new Bait(salmonIndex) { UsesRemaining = 10, Enhanced = true };
            pm.Backpack.DropItem(more);
            more.TryBeginTarget(pm, 4);
            pm.Target.Invoke(pm, pole);
            Assert.Equal(5, pole.BaitUses);
            Assert.True(pole.EnhancedBait);
            Assert.Equal(6, more.UsesRemaining);

            // Another fish's bait swaps the old bait out (the uses add, as ServUO's do).
            var other = new Bait(FishInfo.GetIndexFromType(typeof(FireFish))) { UsesRemaining = 1 };
            pm.Backpack.DropItem(other);
            other.OnDoubleClick(pm);
            pm.Target.Invoke(pm, pole);
            Assert.Same(typeof(FireFish), pole.BaitType);

            // A cup pours into a cup of the same bait.
            var cup = new Bait(salmonIndex) { UsesRemaining = 3 };
            pm.Backpack.DropItem(cup);
            cup.TryBeginTarget(pm, 3);
            pm.Target.Invoke(pm, more);
            Assert.Equal(9, more.UsesRemaining);
            Assert.True(cup.Deleted);

            // Crab bait refuses a pole and goes on a trap.
            var crabBait = new Bait(FishInfo.GetIndexFromType(typeof(StoneCrab))) { UsesRemaining = 1 };
            pm.Backpack.DropItem(crabBait);
            crabBait.OnDoubleClick(pm);
            pm.Target.Invoke(pm, pole);
            Assert.Same(typeof(FireFish), pole.BaitType);
            crabBait.OnDoubleClick(pm);
            pm.Target.Invoke(pm, trap);
            Assert.Same(typeof(StoneCrab), trap.BaitType);
            Assert.True(crabBait.Deleted);

            var copy = RoundTrip(more);
            Assert.Equal(9, copy.UsesRemaining);
            Assert.Equal(salmonIndex, copy.Index);
            Assert.True(copy.Enhanced);
            Cleanup(copy, more);
        }
        finally
        {
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AHookIsTiedOnAndWornByEachCatch()
    {
        var pm = NewPlayer(100);
        var pole = new FishingPole { Hue = 0x21 };
        pm.Backpack.DropItem(pole);
        var water = Tile(WaterStatic, new Point3D(pm.X + 2, pm.Y, 0), pm.Map);
        try
        {
            var lava = new LavaHook(30);
            pm.Backpack.DropItem(lava);
            lava.OnDoubleClick(pm);
            pm.Target.Invoke(pm, pole);
            Assert.Equal(HookType.Lava, pole.HookType);
            Assert.Equal(30, pole.HookUses);
            Assert.Equal(2075, pole.Hue);
            Assert.True(lava.Deleted);

            // Another hook: the lava hook comes back to the pack with its uses, the junk-proof one goes on.
            var junk = new JunkProofHook(2);
            pm.Backpack.DropItem(junk);
            junk.OnDoubleClick(pm);
            pm.Target.Invoke(pm, pole);
            Assert.Equal(HookType.JunkProof, pole.HookType);
            Assert.Equal(2, pole.HookUses);
            var back = pm.Backpack.FindItemByType<LavaHook>();
            Assert.NotNull(back);
            Assert.Equal(30, back.Uses);

            // The pole's hook and bait survive a save.
            pole.BaitType = typeof(GiantKoi);
            pole.BaitUses = 7;
            pole.EnhancedBait = true;
            var copy = RoundTrip(pole);
            Assert.Equal(HookType.JunkProof, copy.HookType);
            Assert.Equal(2, copy.HookUses);
            Assert.Same(typeof(GiantKoi), copy.BaitType);
            Assert.Equal(7, copy.BaitUses);
            Assert.True(copy.EnhancedBait);
            Assert.Equal(pole.Hue, copy.Hue);
            Cleanup(copy);
            pole.BaitType = null;

            // Each catch through the patched cast wears it by one; at none the pole is a plain pole in its own hue.
            PinRandom(0.0);
            var system = Fishing.System;
            var def = system.GetDefinition(WaterStatic, false);

            system.FinishHarvesting(pm, pole, def, water, new object());
            Assert.Equal(1, pole.HookUses);
            Assert.Equal(HookType.JunkProof, pole.HookType);

            system.FinishHarvesting(pm, pole, def, water, new object());
            Assert.Equal(0, pole.HookUses);
            Assert.Equal(HookType.None, pole.HookType);
            Assert.Equal(0x21, pole.Hue);

            // A cast that catches nothing wears a hook but spends no bait (ServUO's OnFishedHarvest).
            pole.HookType = HookType.Dredging;
            pole.HookUses = 5;
            pole.BaitType = typeof(GiantKoi);
            pole.BaitUses = 3;
            pole.OnFishedHarvest(pm, false);
            Assert.Equal(4, pole.HookUses);
            Assert.Equal(3, pole.BaitUses);

            // The junk-proof hook skips the footwear row. At 50 Fishing and a roll of 0.05 the find table's first row
            // that lands is the footwear (chance 0.105; the magic fish row's 0.03 misses); with the hook the next row,
            // the empty one, lands instead.
            pm.Skills.Fishing.Base = 50;
            PinRandom(0.05);
            var resource = def.Resources[0];
            pole.HookType = HookType.None;
            var footwear = system.MutateType(typeof(Fish), pm, pole, def, pm.Map, pm.Location, resource);
            Assert.Same(typeof(Boots), footwear);

            pole.HookType = HookType.JunkProof;
            pole.HookUses = 10;
            Assert.Null(system.MutateType(typeof(Fish), pm, pole, def, pm.Map, pm.Location, resource));
        }
        finally
        {
            BuiltInRng.Reset();
            Cleanup(water, pm);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void LavaIsFishedOnlyWithTheLavaHookAndOnlyInLava()
    {
        var pm = NewPlayer(100);
        var pole = new FishingPole();
        pm.Backpack.DropItem(pole);
        var lavaTile = Tile(LavaStatic, new Point3D(pm.X + 2, pm.Y, 0), pm.Map);
        var water = Tile(WaterStatic, new Point3D(pm.X - 2, pm.Y, 0), pm.Map);
        try
        {
            var system = Fishing.System;

            // No hook: lava is no fishing water at all (pinned's StartHarvesting finds no definition for it).
            Assert.Null(system.GetDefinition(LavaStatic, false));
            Assert.False(HighSeasFishing.IsLavaHarvest(system, pm, pole, lavaTile));
            system.StartHarvesting(pm, pole, lavaTile);
            Assert.True(pm.CanBeginAction(system), "an unhooked pole started a cast on lava");

            // The lava hook on water fishes water.
            pole.HookType = HookType.Lava;
            pole.HookUses = 20;
            Assert.False(HighSeasFishing.IsLavaHarvest(system, pm, pole, water));
            Assert.Same(system.GetDefinition(), system.GetDefinition(WaterStatic, false));

            // The lava hook on lava: the patched StartHarvesting takes the cast (the fishing lock is held).
            Assert.True(HighSeasFishing.IsLavaHarvest(system, pm, pole, lavaTile));
            Assert.True(HighSeasFishing.LavaDefinition.Validate(LavaStatic, false));
            Assert.True(HighSeasFishing.LavaDefinition.Validate(0x1F4, true));
            system.StartHarvesting(pm, pole, lavaTile);
            Assert.False(pm.CanBeginAction(system), "a lava-hooked pole did not start a cast on lava");
            pm.EndAction(system);

            // And finishes it by lava's rules. At 100 Fishing and a roll of 0.05 the footwear, cracked rock and paver
            // rows land (0.079 each), the weapon row (0.005) misses, and the last row that landed is the catch.
            PinRandom(0.05);
            system.FinishHarvesting(pm, pole, HighSeasFishing.LavaDefinition, lavaTile, system);
            Assert.NotNull(pm.Backpack.FindItemByType<StonePaver>());
            Assert.Null(pm.Backpack.FindItemByType<Fish>());
            Assert.Null(pm.Backpack.FindItemByType<BaseHighseasFish>());
            Assert.Equal(19, pole.HookUses);

            // The rows open by skill: under 80 only the footwear row, which lands from 80; the rock from 80, the paver
            // from 85. At a roll of 0.01, 84 Fishing lands the footwear and the rock (0.016 each), and the rock is last.
            Assert.Same(typeof(StonePaver), HighSeasFishing.GetSpecialLavaItem(pm, pm.Map));
            PinRandom(0.01);
            pm.Skills.Fishing.Base = 84;
            Assert.Contains(HighSeasFishing.GetSpecialLavaItem(pm, pm.Map), new[] { typeof(CrackedLavaRockEast), typeof(CrackedLavaRockSouth) });
            pm.Skills.Fishing.Base = 79.9;
            Assert.Null(HighSeasFishing.GetSpecialLavaItem(pm, pm.Map));

            // Stone footwear holds its wearer; the cracked rock splits under an axe.
            var boots = new StoneFootwear(5899);
            Assert.Equal(3.0, boots.Weight);
            pm.AddItem(boots);
            var step = MovementEventArgs.Create(pm, Direction.North);
            StoneFootwear.EventSink_Movement(step);
            Assert.True(step.Blocked);
            boots.Delete();

            var rock = new CrackedLavaRockSouth();
            pm.Backpack.DropItem(rock);
            var hatchet = new Hatchet();
            rock.Axe(pm, hatchet);
            hatchet.Delete();
            Assert.True(rock.Deleted);
            Assert.True(
                pm.Backpack.FindItemByType<GeodeEast>() != null || pm.Backpack.FindItemByType<GeodeSouth>() != null ||
                pm.Backpack.FindItemByType<GeodeShardEast>() != null || pm.Backpack.FindItemByType<GeodeShardSouth>() != null ||
                pm.Backpack.FindItemByType<LavaRock>() != null
            );
        }
        finally
        {
            BuiltInRng.Reset();
            Cleanup(lavaTile, water, pm);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AFishPieGivesItsBuffForItsTime()
    {
        var pm = NewPlayer(60);
        var rat = new Rat();
        rat.MoveToWorld(new Point3D(pm.X + 1, pm.Y, 0), pm.Map);
        try
        {
            var baseDamage = AosAttributes.GetValue(pm, AosAttribute.WeaponDamage);

            var pies = new BullFishPie { Amount = 2 };
            pm.Backpack.DropItem(pies);
            pies.OnDoubleClick(pm);
            Assert.Equal(1, pies.Amount);
            Assert.False(pies.Deleted);
            Assert.True(BaseFishPie.IsUnderEffects(pm, FishPieEffect.WeaponDam));
            Assert.Equal(baseDamage + 5, AosAttributes.GetValue(pm, AosAttribute.WeaponDamage));

            // One of a kind at a time: a second bull fish pie is refused and kept.
            pies.OnDoubleClick(pm);
            Assert.Equal(1, pies.Amount);
            Assert.Equal(baseDamage + 5, AosAttributes.GetValue(pm, AosAttribute.WeaponDamage));

            // Its time up (the timer's call), the bonus goes.
            BaseFishPie.RemoveBuff(pm, FishPieEffect.WeaponDam);
            Assert.Equal(baseDamage, AosAttributes.GetValue(pm, AosAttribute.WeaponDamage));
            Assert.Equal(TimeSpan.FromMinutes(5), pies.Duration);

            // The fire fish pie takes 5 off a fire hit (all fire, no resistance: 20 lands as 15).
            var plain = AOS.Damage(pm, rat, 20, 0, 100, 0, 0, 0);
            pm.Hits = pm.HitsMax;
            var fire = new FireFishPie();
            pm.Backpack.DropItem(fire);
            fire.OnDoubleClick(pm);
            Assert.True(fire.Deleted);
            var soaked = AOS.Damage(pm, rat, 20, 0, 100, 0, 0, 0);
            Assert.Equal(plain - 5, soaked);
            // A cold hit is not a fire hit.
            pm.Hits = pm.HitsMax;
            Assert.Equal(plain, AOS.Damage(pm, rat, 20, 0, 0, 100, 0, 0));
            BaseFishPie.RemoveBuff(pm, FishPieEffect.FireSoak);

            // The great barracuda pie: +8 hit chance.
            var baseHci = AosAttributes.GetValue(pm, AosAttribute.AttackChance);
            var barracuda = new GreatBarracudaPie();
            pm.Backpack.DropItem(barracuda);
            barracuda.OnDoubleClick(pm);
            Assert.Equal(baseHci + 8, AosAttributes.GetValue(pm, AosAttribute.AttackChance));
            BaseFishPie.RemoveBuff(pm, FishPieEffect.HitChance);

            // The autumn dragonfish pie: +10 Meditation.
            pm.Skills.Meditation.Base = 50;
            var autumn = new AutumnDragonfishPie();
            pm.Backpack.DropItem(autumn);
            autumn.OnDoubleClick(pm);
            Assert.Equal(60.0, pm.Skills.Meditation.Value, 1);
            BaseFishPie.RemoveBuff(pm, FishPieEffect.MedBoost);

            // All sixteen construct, carry their label and buff line, each a different effect, and round-trip.
            Assert.Equal(16, Charydbis.Pies.Length);
            var effects = new HashSet<FishPieEffect>();
            foreach (var type in Charydbis.Pies)
            {
                var pie = (BaseFishPie)Loot.Construct(type);
                Assert.True(pie.LabelNumber > 0 && pie.BuffName > 0 && pie.BuffAmount > 0, type.Name);
                Assert.NotEqual(FishPieEffect.None, pie.Effect);
                effects.Add(pie.Effect);
                pie.Exceptional = true;
                var copy = RoundTrip(pie);
                Assert.True(copy.Exceptional, type.Name);
                Assert.Equal(pie.Hue, copy.Hue);
                Cleanup(pie, copy);
            }

            Assert.Equal(16, effects.Count);

            // The recipes: sixteen pies on the cooking menu, each from its steak at an oven, band II for recipe gating.
            if (DefCooking.CraftSystem == null)
            {
                DefCooking.Initialize();
            }

            CookingCraftRegistrations.Register();
            var cooking = DefCooking.CraftSystem;

            foreach (var type in Charydbis.Pies)
            {
                var craft = cooking.CraftItems.SearchFor(type);
                Assert.True(craft != null, $"{type.Name} is not on the cooking menu");
                Assert.True(craft.NeedOven, $"{type.Name} needs no oven");
                var min = ClusterFRecipeGate.MinMainSkill(cooking, craft);
                Assert.True(min is 55.0 or 61.0, $"{type.Name} from {min}");
            }
        }
        finally
        {
            foreach (FishPieEffect effect in Enum.GetValues(typeof(FishPieEffect)))
            {
                BaseFishPie.RemoveBuff(pm, effect);
            }

            Cleanup(rat, pm);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheOracleFindsCharybdisAndABaitedCastRaisesIt()
    {
        var saved = CharydbisSpawner.SpawnInstance;
        var spawner = new CharydbisSpawner();
        CharydbisSpawner.SpawnInstance = spawner;

        var pm = NewPlayer(100);
        var oracle = new OracleOfTheSea();
        var pole = new FishingPole();
        pm.Backpack.DropItem(oracle);
        pm.Backpack.DropItem(pole);
        try
        {
            Assert.Equal(5, oracle.UsesRemaining);
            oracle.OnDoubleClick(pm);
            Assert.Equal(4, oracle.UsesRemaining);
            Assert.True(spawner.IsSummoned);
            Assert.Same(Map.Trammel, spawner.Map);
            Assert.Equal(10, spawner.CurrentLocation.Width);
            Assert.True(spawner.NextSpawn > Core.Now);

            // Once summoned, the Oracle only repeats the spot and spends nothing.
            oracle.OnDoubleClick(pm);
            Assert.Equal(4, oracle.UsesRemaining);

            // An empty Oracle is a plain spyglass: it searches nothing.
            spawner.Reset();
            oracle.UsesRemaining = 0;
            oracle.OnDoubleClick(pm);
            Assert.False(spawner.IsSummoned);
            Assert.Equal(0, oracle.UsesRemaining);

            oracle.UsesRemaining = 1;
            oracle.OnDoubleClick(pm);
            Assert.True(spawner.IsSummoned);

            var spot = new Point3D(spawner.CurrentLocation.X + 5, spawner.CurrentLocation.Y + 5, -5);

            // Unbaited, or baited for another fish: bubbles, no charybdis.
            Assert.False(HighSeasFishing.TrySummonCharybdis(pm, pole, spot));
            pole.BaitType = typeof(FireFish);
            pole.BaitUses = 2;
            Assert.False(HighSeasFishing.TrySummonCharybdis(pm, pole, spot));

            // Baited for charybdis, under 100 Fishing: nothing.
            pole.BaitType = typeof(Charydbis);
            pole.BaitUses = 2;
            pm.Skills.Fishing.Base = 99;
            Assert.False(HighSeasFishing.TrySummonCharybdis(pm, pole, spot));

            // Outside the spot: nothing.
            pm.Skills.Fishing.Base = 100;
            Assert.False(HighSeasFishing.TrySummonCharybdis(pm, pole, new Point3D(spot.X + 50, spot.Y, -5)));
            Assert.Null(spawner.Charydbis);

            // In the spot: snagged. One bait used, Charybdis made, and only once.
            Assert.True(HighSeasFishing.TrySummonCharybdis(pm, pole, spot));
            Assert.NotNull(spawner.Charydbis);
            Assert.True(spawner.HasSpawned);
            Assert.Equal(1, pole.BaitUses);
            var charybdis = spawner.Charydbis;
            Assert.False(HighSeasFishing.TrySummonCharybdis(pm, pole, spot));

            // The spawner round-trips its state.
            var buffer = new byte[4096];
            var writer = new BufferWriter(buffer, true);
            spawner.Serialize(writer);
            writer.Flush();
            var copy = new CharydbisSpawner();
            copy.Deserialize(new BufferReader(buffer));
            Assert.True(copy.IsSummoned);
            Assert.True(copy.HasSpawned);
            Assert.Same(Map.Trammel, copy.Map);
            Assert.Equal(spawner.CurrentLocation, copy.CurrentLocation);
            Assert.Same(charybdis, copy.Charydbis);

            // Its death tells the spawner: inside seven hours of the reading it waits out the rest.
            spawner.OnCharybdisKilled();
            Assert.True(spawner.NextSpawn > Core.Now);
            spawner.Reset();
            Assert.False(spawner.IsSummoned);
            Assert.True(charybdis.Deleted || !charybdis.Alive);
            copy.Reset();
        }
        finally
        {
            spawner.Reset();
            CharydbisSpawner.SpawnInstance = saved;
            Cleanup(pm);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void CorgulRaisesHisCrewDropsHisLootAndTakesThemWithHim()
    {
        var corgul = new CorgulTheSoulBinder();
        try
        {
            corgul.MoveToWorld(CorgulTheSoulBinder.SpawnLoc, Map.Trammel);
            Assert.Equal(65000, corgul.HitsMax);
            Assert.Equal("Corgul the Soulbinder", corgul.Name);

            // His own artifacts: the sash and the handbook on the undead, both real items.
            Assert.Equal(2, corgul.UniqueList.Length);
            foreach (var type in corgul.UniqueList.Concat(corgul.SharedList).Concat(corgul.DecorativeList))
            {
                var item = Loot.Construct(type);
                Assert.True(item != null, $"{type.Name} did not construct");
                var copy = RoundTrip(item);
                Assert.Equal(item.LabelNumber, copy.LabelNumber);
                Cleanup(item, copy);
            }

            var sash = new CorgulsEnchantedSash();
            Assert.Equal(5, sash.Attributes.DefendChance);
            Assert.Equal(1, sash.Attributes.BonusStam);
            sash.Delete();

            // His odds: at a roll of 0.1, a decorative deed in Trammel (25%, the shared 8% misses), a shared artifact in
            // Felucca (12%).
            PinRandom(0.1);
            var trammel = corgul.RollArtifact();
            Assert.True(trammel is EnchantedBladeDeed or EnchantedVortexDeed, trammel?.GetType().Name);
            trammel.Delete();
            corgul.MoveToWorld(CorgulTheSoulBinder.SpawnLoc, Map.Felucca);
            var felucca = corgul.RollArtifact();
            Assert.Contains(felucca.GetType(), corgul.SharedList);
            felucca.Delete();
            PinRandom(0.5);
            Assert.Null(corgul.RollArtifact());
            BuiltInRng.Reset();
            corgul.MoveToWorld(CorgulTheSoulBinder.SpawnLoc, Map.Trammel);

            // The crew: 15 at each of the five landing spots, seven kinds, all on his island.
            corgul.SpawnCrew = true;
            Assert.Equal(75, corgul.Helpers.Count);
            Assert.All(corgul.Helpers, bc => Assert.True(CorgulTheSoulBinder.IslandBounds.Contains(bc.Location)));
            Assert.All(corgul.Helpers, bc => Assert.Same(Map.Trammel, bc.Map));
            Assert.True(corgul.Helpers.Select(bc => bc.GetType()).Distinct().Count() > 1);

            // He round-trips with his crew list. The copy carries his serial and his items, so it is not deleted
            // (deleting it would take his serial and his pack out of the world, as P193's champion facts found).
            var copyCorgul = RoundTrip(corgul);
            Assert.Equal(75, copyCorgul.Helpers.Count);
            Assert.Same(corgul.Helpers[0], copyCorgul.Helpers[0]);

            foreach (var type in new[]
                     {
                         typeof(BoundSoul), typeof(SoulboundApprenticeMage), typeof(SoulboundBattleMage),
                         typeof(SoulboundPirateCaptain), typeof(SoulboundPirateRaider), typeof(SoulboundSpellSlinger),
                         typeof(SoulboundSwashbuckler)
                     })
            {
                var crew = (BaseCreature)Activator.CreateInstance(type);
                Assert.True(crew.HitsMax > 0, type.Name);
                var copy = RoundTrip(crew);
                Assert.Equal(crew.Name, copy.Name);
                Cleanup(crew, copy);
            }

            // Dead, his crew die with him and the corpse holds the bottle and two nets.
            var helpers = corgul.Helpers.ToList();
            corgul.Kill();
            Assert.All(helpers, bc => Assert.False(bc.Alive && !bc.Deleted, $"{bc.GetType().Name} outlived Corgul"));
            var corpse = corgul.Corpse;
            Assert.NotNull(corpse);
            Assert.NotNull(corpse.FindItemByType<MessageInABottle>());
            var nets = 0;
            foreach (var _ in corpse.FindItemsByType<SpecialFishingNet>())
            {
                nets++;
            }

            Assert.Equal(2, nets);
            corpse.Delete();
        }
        finally
        {
            BuiltInRng.Reset();
            foreach (var bc in corgul.Helpers.ToList())
            {
                bc.Delete();
            }

            Cleanup(corgul);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void AFishOrderIsTakenFilledDeliveredAndPaid()
    {
        var pm = NewPlayer(60);
        var quester = new FishMonger { Port = "Britain" };
        var turnIn = new FishMonger { Port = "Trinsic" };
        var stranger = new FishMonger { Port = null };
        var boat = new SmallBoat { Owner = pm };
        quester.MoveToWorld(new Point3D(pm.X + 1, pm.Y, 0), Map.Trammel);
        turnIn.MoveToWorld(new Point3D(pm.X + 2, pm.Y, 0), Map.Trammel);
        stranger.MoveToWorld(new Point3D(pm.X + 3, pm.Y, 0), Map.Trammel);
        boat.MoveToWorld(new Point3D(pm.X + 4, pm.Y + 4, -5), Map.Trammel);
        try
        {
            Assert.Same(boat, FishQuestHelper.GetBoat(pm));
            Assert.Equal(1116728, ProfessionalFisherQuest.GetTitle(quester)); // Professional Fisher Quest - Britain
            Assert.Equal(1116507, ProfessionalFisherQuest.GetTitle(stranger)); // the floating emporium's, by default

            // The port of delivery: another port, never the giver's, never a nameless one.
            for (var i = 0; i < 20; i++)
            {
                Assert.Same(turnIn, FishQuestHelper.GetRandomMonger(pm, quester));
            }

            // An order: one line (a small boat), a shore fish at 60 Fishing, ten of it.
            var lines = ProfessionalFisherQuest.BuildLines(pm, boat);
            Assert.Single(lines);
            var (type, amount) = lines.First();
            Assert.InRange(FishQuestHelper.GetIndexForType(type), 0, 11);
            Assert.Equal(10, amount);

            // Accepted: the crate is in the boat's hold, and it is the player's open order.
            var crate = ProfessionalFisherQuest.Accept(pm, quester, turnIn, boat, ProfessionalFisherQuest.GetTitle(quester), lines);
            Assert.NotNull(crate);
            Assert.Same(boat.Hold, crate.Parent);
            Assert.Contains(crate, ShippingCrate.OrdersOf(pm));
            Assert.False(crate.Completed);
            Assert.Equal(1116498, crate.GetDeliveryInfo()); // Trinsic (1116497 is Britain)
            Assert.Null(FishQuestHelper.GetRandomMonger(pm, quester)); // the one other port is taken by this order

            // The giver shows the order; the port of delivery refuses with the boat away.
            Assert.True(ProfessionalFisherQuest.HasFishQuest(pm, quester, true));
            Assert.True(ProfessionalFisherQuest.HasFishQuest(pm, turnIn, false));

            // A fish the order does not need is refused; the ordered fish goes in and fills it, and cannot come out.
            var wrongType = type == typeof(Walleye) ? typeof(YellowPerch) : typeof(Walleye);
            var wrong = (Item)Activator.CreateInstance(wrongType);
            Assert.False(crate.OnDragDrop(pm, wrong));
            wrong.Delete();

            var fish = (Item)Activator.CreateInstance(type);
            fish.Amount = 10;
            pm.Backpack.DropItem(fish);
            Assert.True(crate.OnDragDrop(pm, fish));
            Assert.Equal(10, crate.LineHave(0));
            Assert.True(crate.Completed);
            var reject = LRReason.Inspecific;
            Assert.False(crate.CheckLift(pm, fish, ref reject));
            Assert.Equal(LRReason.CannotLift, reject);

            // Delivered: the fish are used up, the crate is gone, the reward is in the pack, and the standing counts it.
            var packBefore = pm.Backpack.Items.Count;
            ProfessionalFisherQuest.Complete(pm, crate);
            Assert.True(crate.Deleted);
            Assert.True(fish.Deleted);
            Assert.Empty(ShippingCrate.OrdersOf(pm));
            Assert.True(pm.Backpack.Items.Count > packBefore, "no reward came");
            var entry = PlayerFishingEntry.GetEntry(pm);
            var index = FishQuestHelper.GetIndexForType(type);
            Assert.Equal(10, entry.Reputation);
            Assert.Equal(1, entry.TimesFished[index]);
            Assert.Equal(15, entry.HaveFished[index]);

            // A forfeited order (the crate destroyed) costs 20 standing and lowers what that fish asks for.
            var forfeit = ProfessionalFisherQuest.Accept(pm, quester, turnIn, boat, 1116728, new Dictionary<Type, int> { [type] = 15 });
            forfeit.Delete();
            Assert.Equal(-10, entry.Reputation);
            Assert.Equal(10, entry.HaveFished[index]);

            // The standings survive a save.
            var buffer = new byte[4096];
            var writer = new BufferWriter(buffer, true);
            PlayerFishingEntry.SaveAll(writer);
            writer.Flush();
            PlayerFishingEntry.LoadAll(new BufferReader(buffer));
            var loaded = PlayerFishingEntry.GetEntry(pm);
            Assert.NotSame(entry, loaded);
            Assert.Equal(-10, loaded.Reputation);
            Assert.Equal(10, loaded.HaveFished[index]);
            Assert.Equal(1, loaded.TimesFished[index]);

            // ServUO's reward tiers (its order of tests kept: 150 and 210 never reach tiers 3 and 4) and categories.
            Assert.Equal(1, FishQuestHelper.GetTier(99));
            Assert.Equal(2, FishQuestHelper.GetTier(100));
            Assert.Equal(2, FishQuestHelper.GetTier(300));
            Assert.Equal(4, FishQuestHelper.GetCategory(0.05));
            Assert.Equal(3, FishQuestHelper.GetCategory(0.2));
            Assert.Equal(2, FishQuestHelper.GetCategory(0.3));
            Assert.Equal(1, FishQuestHelper.GetCategory(0.9));

            // A tier 1 bait reward: one of the fifteen rare fish, uses of half the points.
            PinRandom(0.9);
            var before = new HashSet<Item>(pm.Backpack.Items);
            FishQuestHelper.GiveRewards(pm, entry, 50);
            var bait = pm.Backpack.Items.OfType<Bait>().First(b => !before.Contains(b));
            Assert.Equal(13, bait.Index);
            Assert.Equal(25, bait.UsesRemaining);
            Assert.False(bait.Enhanced);
        }
        finally
        {
            BuiltInRng.Reset();
            Cleanup(boat, quester, turnIn, stranger, pm);
        }
    }

    [Fact]
    public void GenFishMongersPlacesSixteenMongersOnceAndSkipsAfter()
    {
        var placed = new List<FishMonger>();
        try
        {
            var dry = GenerateHighSeas.PlaceFishMongers(true);
            Assert.Equal(16, dry.Count);
            Assert.All(dry, line => Assert.Contains("would place", line));

            var real = GenerateHighSeas.PlaceFishMongers(false);
            Assert.Equal(16, real.Count(line => line.Contains("placed")));

            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                foreach (var (port, location, homeRange) in GenerateHighSeas.FishMongerSpots)
                {
                    FishMonger monger = null;

                    foreach (var m in map.GetMobilesInRange(location, 0))
                    {
                        monger ??= m as FishMonger;
                    }

                    Assert.True(monger != null, $"no monger at {port} ({map})");
                    Assert.Equal(port, monger.PortName);
                    Assert.Equal(homeRange, monger.RangeHome);
                    Assert.True(monger.Blessed);
                    placed.Add(monger);
                }
            }

            var again = GenerateHighSeas.PlaceFishMongers(false);
            Assert.All(again, line => Assert.Contains("skipped", line));
            Assert.Equal(16, placed.Distinct().Count());
        }
        finally
        {
            foreach (var monger in FishQuestHelper.Mongers.ToList())
            {
                monger.Delete();
            }
        }
    }
}
