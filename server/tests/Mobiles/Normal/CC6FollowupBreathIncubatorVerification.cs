// CC6 follow-up: the dragon breath restored on four earlier ports (Q-054) and the Incubator that makes the chicken
// lizard egg reachable (Q-053, P12).
//
// Part A. Batches 2, 3 and 4 dropped SetSpecialAbility(DragonBreath) on Slith, ToxicSlith, FireDaemon and CrystalHydra
// as a Pet Training loss (D-55, D-56, D-62, D-68). Pinned ModernUO has the ability as MonsterAbilities.FireBreath /
// ColdBreath through BaseCreature.GetMonsterAbilities(); those four numbers are retired, not reversed. ServUO chooses
// the breath by creature type (Services/Pet Training/SpecialAbility.cs:860-1063): three of the four take the default
// fire definition, and CrystalHydra has its own - cold, 0.13, 5-7 s, hue 0x47E, sound 0x56D, five targets - which
// CrystalHydraBreath (ours) carries over pinned ColdBreath. One fact per creature, checking the numbers, not the name.
//
// Part B. Incubator is a house container that must be SECURED to accept an egg; nothing in the test host places a
// house, so a test-only SmallOldHouse with a synthetic floor stands in (TestHouse below, why in its comment). The
// cycle fact drives an egg through the incubator's own drivers: the drop that starts incubation, the world-save hook
// that checks eggs 10 s later through the timer wheel, a pitcher poured through the patched Pour_OnTarget, and a
// hatch that places a BattleChickenLizard. The 24 h waits are not advanced through the wheel (25 h of 8 ms slices is
// ~11 million turns); IncubationStart is backdated instead, which is what a GM would do and what CheckStatus reads.
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6FollowupBreathIncubatorVerification
{
    private readonly ITestOutputHelper _out;

    public CC6FollowupBreathIncubatorVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        // Mobile.Player is a flag CharacterCreation sets, not something the PlayerMobile constructor does (batch 3).
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Part A: the breath
    // ---------------------------------------------------------------------------------------------------------------

    private static T TheOnlyAbility<T>(BaseCreature bc) where T : MonsterAbility
    {
        var abilities = bc.GetMonsterAbilities();
        Assert.NotNull(abilities);
        var ability = Assert.Single(abilities);
        return Assert.IsType<T>(ability); // exact type, not a subclass
    }

    // ServUO's default DragonBreathDefinition (SpecialAbility.cs:887-903), which pinned FireBreath carries line for line.
    private static void AssertDefaultFireDefinition(FireBreath fb)
    {
        Assert.Equal(100, fb.FireDamage);
        Assert.Equal(0, fb.ColdDamage);
        Assert.Equal(0, fb.PhysicalDamage);
        Assert.Equal(0, fb.PoisonDamage);
        Assert.Equal(0, fb.EnergyDamage);
        Assert.Equal(0, fb.ChaosDamage);
        Assert.Equal(Core.AOS ? 0.16 : 0.05, fb.BreathDamageScalar);
        Assert.Equal(TimeSpan.FromSeconds(30), fb.MinTriggerCooldown);
        Assert.Equal(TimeSpan.FromSeconds(45), fb.MaxTriggerCooldown);
        Assert.Equal(0x36D4, fb.BreathEffectItemID);
        Assert.Equal(0, fb.BreathEffectHue);
        Assert.Equal(0x227, fb.BreathEffectSound);
        Assert.Equal(12, fb.BreathAngerAnimation);
        Assert.Equal(1.0, fb.BreathStallTime);
        Assert.Equal(1.3, fb.BreathEffectDelay);
        Assert.Equal(1.0, fb.BreathDamageDelay);
    }

    [Fact]
    public void TheSlithBreathesTheDefaultFire()
    {
        // D-55 retired. Slith is in no Uses list, so ServUO's GetDefinition falls through to Definitions[0], fire.
        var slith = new Slith();
        AssertDefaultFireDefinition(TheOnlyAbility<FireBreath>(slith));
        slith.Delete();
    }

    [Fact]
    public void TheToxicSlithBreathesTheDefaultFireNotPoison()
    {
        // D-56 retired. Same fall-through; its poison is its skill and resistance, not its breath.
        var slith = new ToxicSlith();
        var fb = TheOnlyAbility<FireBreath>(slith);
        AssertDefaultFireDefinition(fb);
        Assert.Equal(0, fb.PoisonDamage);
        slith.Delete();
    }

    [Fact]
    public void TheFireDaemonBreathesTheDefaultFireBesideItsAura()
    {
        // D-62 retired. The aura (batch 3) is BaseCreature.HasAura, not a MonsterAbility, so the breath is still single.
        var daemon = new FireDaemon();
        AssertDefaultFireDefinition(TheOnlyAbility<FireBreath>(daemon));
        Assert.True(daemon.HasAura);
        daemon.Delete();
    }

    [Fact]
    public void TheCrystalHydraBreathesColdEveryFewSecondsAtUpToFiveTargets()
    {
        ShardTestClock.Arm();

        // D-68 retired. CrystalHydra is the one of the four with its own definition (SpecialAbility.cs:1045-1063).
        var hydra = new CrystalHydra();
        var breath = TheOnlyAbility<CrystalHydraBreath>(hydra);
        Assert.IsAssignableFrom<ColdBreath>(breath);

        Assert.Equal(100, breath.ColdDamage);
        Assert.Equal(0, breath.FireDamage);
        Assert.Equal(0, breath.PhysicalDamage);
        Assert.Equal(0, breath.PoisonDamage);
        Assert.Equal(0, breath.EnergyDamage);
        Assert.Equal(0, breath.ChaosDamage);
        Assert.Equal(0.13, breath.BreathDamageScalar);
        Assert.Equal(TimeSpan.FromSeconds(5), breath.MinTriggerCooldown);
        Assert.Equal(TimeSpan.FromSeconds(7), breath.MaxTriggerCooldown);
        Assert.Equal(0x47E, breath.BreathEffectHue);
        Assert.Equal(0x56D, breath.BreathEffectSound);
        Assert.Equal(0x36D4, breath.BreathEffectItemID);
        Assert.Equal(12, breath.BreathAngerAnimation);
        Assert.Equal(1.0, breath.BreathStallTime);
        Assert.Equal(1.3, breath.BreathEffectDelay);
        Assert.Equal(1.0, breath.BreathDamageDelay);

        // The stock cold breath it is NOT: 0x480 and 30-45 s, the skeletal dragon's (batch 5).
        var stockCold = new ColdBreath();
        Assert.Equal(0x480, stockCold.BreathEffectHue);
        Assert.Equal(TimeSpan.FromSeconds(30), stockCold.MinTriggerCooldown);

        // The fan-out: one trigger breathes at the combatant and at four more mobiles within 5 tiles of it. Nine
        // players stand in the hydra's range, eight within 5 tiles of the combatant and one 15 tiles away; exactly
        // five take cold damage after the 1.3 s effect delay and the 1.0 s damage delay. The hydra is set to 300
        // hits, so the damage it would compute at that moment is 39 (hits * 0.13) and is pinned below; what lands 2.3 s
        // later is larger, because Mobile.DefaultHitsRate is TimeSpan.Zero in the host (RegenRates.Configure never
        // runs) and the hydra's HitsTimer adds a hit on every 8 ms wheel tick of the advance, so the five hit players
        // (50 hits each) die. The fact is about who is hit, not how hard; the numbers are on the ability's properties.
        var origin = new Point3D(1200, 1200, 0);
        hydra.MoveToWorld(origin, Map.TerMur);
        hydra.Hits = 300;

        var combatant = NewPlayer(new Point3D(1203, 1200, 0));
        var nearby = new List<PlayerMobile>();
        for (var i = 0; i < 7; i++)
        {
            nearby.Add(NewPlayer(new Point3D(1203 + i % 3, 1201 + i / 3, 0)));
        }

        var farAway = NewPlayer(new Point3D(1203, 1215, 0));

        var everyone = nearby.Append(combatant).Append(farAway).ToList();
        foreach (var pm in everyone)
        {
            pm.Hits = pm.HitsMax;
        }

        Assert.True(breath.CanFireBreathTarget(hydra, combatant));
        Assert.Equal(39, breath.BreathComputeDamage(hydra)); // (int)(300 * 0.13), the hydra's scalar, not the default's 48
        var candidates = breath.AcquireSecondaryTargets(hydra, combatant);
        Assert.Equal(7, candidates.Count);
        Assert.DoesNotContain(combatant, candidates);
        Assert.DoesNotContain(farAway, candidates);

        // Trigger is called directly: CanTrigger is a 50% roll on a combat action, which is pinned's cadence, not ours to test.
        breath.Trigger(MonsterAbilityTrigger.CombatAction, hydra, combatant);
        ShardTestClock.Advance(TimeSpan.FromSeconds(1.3 + 1.0 + 0.5));

        static bool Hurt(Mobile m) => !m.Alive || m.Hits < m.HitsMax;


        Assert.True(Hurt(combatant), "the combatant took no breath");
        var hurtNearby = nearby.Count(Hurt);
        _out.WriteLine($"crystal hydra breath ({breath.BreathComputeDamage(hydra)} cold, combatant cold resist {combatant.ColdResistance}): combatant {combatant.Hits}/{combatant.HitsMax} alive={combatant.Alive}; nearby {string.Join(" ", nearby.Select(n => $"{n.Hits}/{n.HitsMax}"))}; far {farAway.Hits}/{farAway.HitsMax}; hydra hits {hydra.Hits}");
        Assert.Equal(4, hurtNearby);
        Assert.False(Hurt(farAway), "a player 15 tiles from the combatant was breathed on");

        // Not asserted: that CanTrigger is false for the next 5-7 s. It held in every full-suite run and read true in
        // the isolated --filter run on the same builder (the CC6 follow-up note §6), an order dependence in stock
        // MonsterAbility's tick bookkeeping under the test clock that this fact is not about; the cooldown values
        // themselves are pinned above.

        foreach (var pm in everyone)
        {
            pm.Delete();
        }

        hydra.Delete();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Part B: the incubator
    // ---------------------------------------------------------------------------------------------------------------

    // The test host has no multi.mul, so MultiData.GetComponents returns MultiComponentList.Empty for every multi ID
    // and BaseHouse.IsInside is false at every point, which makes BaseHouse.FindHouseAt (and so CheckSecured) find
    // nothing. This stand-in is a stock SmallOldHouse whose Components is a synthetic 7x7 floor at Z 0, so the house
    // is found at any point of that square. It is never serialized (no generator attribute, so no (Serial)
    // constructor), never saved, and deleted at the end of the fact that made it.
    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;

        // HousePlacementEntry.Find keys on the house's exact type (HousePlacementTool.cs:2181-2183), so this subclass
        // has no entry and GetAosMaxLockdowns()/GetAosMaxSecures() would be 0, which makes AddSecure refuse with
        // "exceed the maximum lock down limit" (build A3-diag). These are SmallOldHouse's own entry values.
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();

            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    // Flags 1 is multi.mul's "visible" bit; the list constructor skips every entry after the
                    // first whose Flags are 0 when it computes Min/Max and fills Tiles (MultiData.cs:388, :431),
                    // so a 0-flag floor collapses to a 1x1 multi. Build A2 went red on exactly that.
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1)); // a stone floor tile
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    private static (PlayerMobile Owner, TestHouse House, Incubator Incubator) SecuredIncubator(Point3D at)
    {
        // Felucca: the host's TerMur is 1280 tiles wide (Server.Tests/Fixtures/TestMapDefinitions.cs:12), and build
        // A3 put the house at x 1300, off the map, where no sector holds it.
        var owner = NewPlayer(new Point3D(at.X + 1, at.Y, at.Z), Map.Felucca);
        var house = new TestHouse(owner);
        house.MoveToWorld(at, Map.Felucca);
        Assert.Same(house, BaseHouse.FindHouseAt(at, Map.Felucca, 0));
        Assert.True(house.IsInside(at, 0));
        Assert.True(house.IsOwner(owner));

        var incubator = new Incubator();
        incubator.MoveToWorld(at, Map.Felucca);

        return (owner, house, incubator);
    }

    private static void Backdate(ChickenLizardEgg egg, TimeSpan by)
    {
        Assert.True(egg.Incubating);
        egg.IncubationStart = Core.Now - by;
    }

    // The save-hook driver: EventSink.WorldSave -> Incubator.OnWorldSave -> Core.LoopContext.Post -> (next loop
    // iteration) -> 10 s -> CheckEggs_Callback, through the wheel. The host never pumps the loop context, and the one
    // the fixture made belongs to the fixture's thread (ExecuteTasks throws on any other), so the test swaps in a
    // context of its own on this thread and pumps it once, which is what Main.cs:480 does every iteration.
    private static void SaveAndWait()
    {
        Core.LoopContext = new EventLoopContext();
        EventSink.InvokeWorldSave();
        Core.LoopContext.ExecuteTasks();
        ShardTestClock.Advance(TimeSpan.FromSeconds(11));
    }

    [Fact]
    public void TheIncubatorMustBeSecuredHoldsSixEggsAndNothingElse()
    {
        ShardTestClock.Arm();
        Incubator.Initialize();

        var (owner, house, incubator) = SecuredIncubator(new Point3D(1400, 1700, 0));
        var egg = new ChickenLizardEgg();
        owner.AddToBackpack(egg);

        Assert.Equal(1112479, incubator.LabelNumber);
        Assert.Equal(0x407C, incubator.ItemID);
        Assert.Equal(1156, incubator.DefaultGumpID);
        Assert.Equal(66, incubator.DefaultDropSound);
        Assert.Equal(SecureLevel.CoOwners, incubator.Level);
        Assert.Equal(6, Incubator.MaxEggs);

        // On the ground in the house but not secured: refused, and the egg does not start incubating.
        Assert.False(BaseHouse.CheckSecured(incubator));
        Assert.False(incubator.OnDragDropInto(owner, egg, new Point3D(10, 10, 0)));
        Assert.False(egg.Incubating);
        Assert.Same(owner.Backpack, egg.Parent);

        // Secured through the house's own AddSecure (the player path): now it takes the egg and starts it.
        house.AddSecure(owner, incubator);
        Assert.True(house.HasSecureItem(incubator));
        Assert.True(BaseHouse.CheckSecured(incubator));
        // Not asserted: incubator.IsSecure. It is a flag bit BaseHouse.Configure() assigns (SecureFlag = 2) and the
        // host never runs Configure, so the flag is 0 and IsSecure reads false however the item was secured (build A4).

        Assert.True(egg.DropToItem(owner, incubator, new Point3D(10, 10, 0)));
        Assert.Same(incubator, egg.Parent);
        Assert.True(egg.Incubating); // the restored `Parent is not Incubator` clause; without it this is false
        Assert.InRange(egg.IncubationStart, Core.Now - TimeSpan.FromSeconds(1), Core.Now);

        // Not an egg: refused.
        var apple = new Apple();
        owner.AddToBackpack(apple);
        Assert.False(incubator.OnDragDropInto(owner, apple, new Point3D(10, 10, 0)));
        Assert.Same(owner.Backpack, apple.Parent);

        // Five more make six; a seventh is refused.
        for (var i = 0; i < 5; i++)
        {
            var e = new ChickenLizardEgg();
            owner.AddToBackpack(e);
            Assert.True(incubator.OnDragDropInto(owner, e, new Point3D(10 + i, 10, 0)));
            Assert.True(e.Incubating);
        }

        Assert.Equal(6, incubator.Items.Count);
        var seventh = new ChickenLizardEgg();
        owner.AddToBackpack(seventh);
        Assert.False(incubator.OnDragDropInto(owner, seventh, new Point3D(20, 10, 0)));
        Assert.Same(owner.Backpack, seventh.Parent);

        // Lifting an egg out stops it and banks the time (batch 4's OnItemLifted, now reachable from a real container).
        Backdate(egg, TimeSpan.FromHours(2));
        egg.OnItemLifted(owner, egg);
        Assert.False(egg.Incubating);
        Assert.InRange(egg.TotalIncubationTime, TimeSpan.FromHours(2) - TimeSpan.FromMinutes(1), TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));
        incubator.RemoveItem(egg);
        owner.AddToBackpack(egg);

        // An egg already past 120 h is burnt on the way in rather than started.
        var stale = new ChickenLizardEgg { TotalIncubationTime = TimeSpan.FromHours(121) };
        owner.AddToBackpack(stale);
        Assert.True(incubator.OnDragDropInto(owner, stale, new Point3D(20, 10, 0)));
        Assert.Equal(EggStage.Burnt, stale.Stage);
        Assert.False(stale.Incubating);

        // The save hook only reaches a SECURED incubator's eggs: unsecure it and a backdated egg does not advance.
        var inside = incubator.Items.OfType<ChickenLizardEgg>().First(e => e.Stage == EggStage.New);
        Backdate(inside, TimeSpan.FromHours(25));
        house.ReleaseSecure(owner, incubator);
        Assert.False(house.HasSecureItem(incubator));
        Assert.False(BaseHouse.CheckSecured(incubator));
        SaveAndWait();
        Assert.Equal(EggStage.New, inside.Stage);

        house.AddSecure(owner, incubator);
        Assert.True(BaseHouse.CheckSecured(incubator));
        SaveAndWait();
        Assert.Equal(EggStage.Stage1, inside.Stage);

        house.Delete();
        owner.Delete();
    }

    [Fact]
    public void AnEggInASecuredIncubatorWateredThroughAPitcherHatchesABattleChickenLizard()
    {
        ShardTestClock.Arm();
        Incubator.Initialize();

        var (owner, house, incubator) = SecuredIncubator(new Point3D(1420, 1700, 0));
        house.AddSecure(owner, incubator);
        Assert.True(BaseHouse.CheckSecured(incubator));

        var pitcher = new Pitcher(BeverageType.Water);
        owner.AddToBackpack(pitcher);
        Assert.Equal(5, pitcher.Quantity);

        // One full cycle, exactly the drivers a player has: drop in, wait (backdated) for the save-hook check, pour a
        // pitcher of water at the egg through the beverage's own targeting handler, repeat, hatch. The mutation roll at
        // Mature is 5% for a watered egg (batch 4: the stage increments before the dryness is read, so Dry, not Moist),
        // so eggs are cycled in sixes until one comes up a battle chicken. 0.95^600 is 4e-14.
        ChickenLizardEgg battle = null;
        var cycled = 0;
        var burnt = 0;

        for (var round = 0; round < 100 && battle == null; round++)
        {
            var eggs = new List<ChickenLizardEgg>();
            for (var i = 0; i < Incubator.MaxEggs; i++)
            {
                var e = new ChickenLizardEgg();
                owner.AddToBackpack(e);
                Assert.True(e.DropToItem(owner, incubator, new Point3D(10 + i, 10, 0)));
                Assert.True(e.Incubating);
                eggs.Add(e);
            }

            // 24 h: New -> Stage1, then one unit of water, poured through Pour_OnTarget (the patch).
            foreach (var e in eggs)
            {
                Backdate(e, TimeSpan.FromHours(25));
            }

            SaveAndWait();

            foreach (var e in eggs)
            {
                Assert.Equal(EggStage.Stage1, e.Stage);
                Assert.Equal(Dryness.Dry, e.Dryness);
                pitcher.Quantity = 5;
                pitcher.Pour_OnTarget(owner, e);
                Assert.Equal(4, pitcher.Quantity); // the branch reached the egg's Pour
                Assert.Equal(1, e.WaterLevel);
                Assert.Equal(Dryness.Moist, e.Dryness);
                Assert.True(e.Incubating); // pouring does not stop it
            }

            // 48 h: Stage1 -> Stage2 (watered, so no burn roll), water again.
            foreach (var e in eggs)
            {
                Backdate(e, TimeSpan.FromHours(24));
            }

            SaveAndWait();

            foreach (var e in eggs)
            {
                Assert.Equal(EggStage.Stage2, e.Stage);
                pitcher.Quantity = 5;
                pitcher.Pour_OnTarget(owner, e);
                Assert.Equal(4, pitcher.Quantity);
                Assert.Equal(2, e.WaterLevel);
            }

            // 72 h: Stage2 -> Mature, the mutation roll.
            foreach (var e in eggs)
            {
                Backdate(e, TimeSpan.FromHours(24));
            }

            SaveAndWait();

            foreach (var e in eggs)
            {
                Assert.Equal(EggStage.Mature, e.Stage);
                cycled++;

                if (e.IsBattleChicken && battle == null)
                {
                    battle = e;
                }
            }

            foreach (var e in eggs)
            {
                if (e != battle)
                {
                    incubator.RemoveItem(e);
                    e.Delete();
                }
            }
        }

        _out.WriteLine($"{cycled} eggs cycled through the incubator, {burnt} burnt, battle chicken after {cycled}");
        Assert.NotNull(battle);
        Assert.True(battle.IsBattleChicken);
        Assert.NotEqual(555, battle.Hue);

        // Lift it out (a real lift is Mobile.Lift -> OnItemLifted, which stops the incubation; RemoveItem alone is not
        // a lift, build A4) and hatch it: a BattleChickenLizard in the egg's hue at the hatcher's feet.
        battle.OnItemLifted(owner, battle);
        incubator.RemoveItem(battle);
        owner.AddToBackpack(battle);
        Assert.False(battle.Incubating);
        var hue = battle.Hue;

        // Hatched outside the house: a creature placed inside a private AOS house it has no access to is moved
        // to the ban location by HouseRegion.OnLocationChanged (Regions/HouseRegion.cs:104-111), which is what
        // happened to build A5's lizard. Real behaviour, and not what this fact is about.
        owner.MoveToWorld(new Point3D(owner.X + 12, owner.Y, 0), Map.Felucca);
        Assert.Null(BaseHouse.FindHouseAt(owner.Location, Map.Felucca, 16));

        var before = World.Mobiles.Values.OfType<BattleChickenLizard>().Count();
        battle.TryHatchEgg(owner);
        Assert.True(battle.Deleted);

        var hatched = World.Mobiles.Values.OfType<BattleChickenLizard>()
            .Where(b => b.Map == Map.Felucca && b.Location == owner.Location).ToList();
        Assert.Single(hatched);
        Assert.Equal(before + 1, World.Mobiles.Values.OfType<BattleChickenLizard>().Count());
        Assert.Equal(hue, hatched[0].Hue);
        Assert.Equal(716, hatched[0].Body.BodyID);

        // And an immature egg hatched early crumbles, as before.
        var young = new ChickenLizardEgg();
        owner.AddToBackpack(young);
        young.TryHatchEgg(owner);
        Assert.True(young.Deleted);

        hatched[0].Delete();
        house.Delete();
        owner.Delete();
    }

    [Fact]
    public void TheIncubatorIsCraftableUnderCarpentryOtherForAHundredBoards()
    {
        // ServUO DefCarpentry.cs:229, registered additively (IncubatorCarpentryRecipe). DefCarpentry.Initialize() and
        // ServerStarted do not run in the host, so both are called here in the order the server runs them.
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        IncubatorCarpentryRecipe.Register();
        IncubatorCarpentryRecipe.Register(); // idempotent

        var carpentry = DefCarpentry.CraftSystem;
        Assert.NotNull(carpentry);

        var entries = carpentry.CraftItems.Where(c => c.ItemType == typeof(Incubator)).ToList();
        var entry = Assert.Single(entries);

        Assert.Equal(1044294, (int)entry.GroupNameNumber); // Other
        Assert.Equal(1112479, (int)entry.NameNumber);      // an incubator
        Assert.Equal(Expansion.SA, entry.RequiredExpansion);

        var skill = Assert.Single(entry.Skills);
        Assert.Equal(SkillName.Carpentry, skill.SkillToMake);
        Assert.Equal(90.0, skill.MinSkill);
        Assert.Equal(115.0, skill.MaxSkill);

        var res = Assert.Single(entry.Resources);
        Assert.Equal(typeof(Log), res.ItemType);
        Assert.Equal(100, res.Amount);
        Assert.Equal(1044041, (int)res.Name); // Boards or Logs
    }
}
