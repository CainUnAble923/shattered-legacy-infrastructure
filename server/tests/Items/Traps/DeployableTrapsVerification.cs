// cc-P275: deployable traps, phase 1 (customizations/Items/Traps/Deployable/, the Tinkering rows in
// TinkeringCraftRegistrations, the Rootdeep floor traps in ClusterFRootdeepSeeder). Notes: shard-migration
// notes/cc-P275-deployable-traps-build.md; the design: notes/cc-P201-deployable-traps-design.md.
//
// Facts:
//   1. Each of the eight traps fires on a monster that walks into it (its own effect: a hold, damage, a slow, poison, an
//      alarm), and never on a player, a pet or a summon (clusterf.traps.pvp off).
//   2. clusterf.traps.pvp on: a Felucca trap hits a player the owner may harm and flags the owner; Trammel stays
//      monster-only.
//   3. Owner presence: dormant at 13 tiles, logged out, dead or on another map; fires at 12; the alarm ignores the range.
//      The damage is the owner's (in the creature's damage entries, and the owner holds looting rights).
//   4. Charges and re-set: a bear trap snaps three times (four exceptional) and is spent; a fourth creature walks over
//      it; the owner's double-click re-sets it. A spent gas trap waits for a fresh potion dropped on it. Decay clocks.
//   5. Remove Trap: the set rolls the kit's window and a hit the window above it; a real set and a disarm each gain;
//      the owner packs it back into a kit, a stranger salvages it on a success.
//   6. The Den: below Master a boss walks through with a line and the trap stays armed; a Master's trap fires on it.
//      Journeyman sets one more trap.
//   7. Who shrugs: a flyer over a ground trap, the incorporeal through a dart; a Detect Hidden creature that spots a
//      trap does not set it off and reveals it.
//   8. Placement refusals: out of reach, a mobile on the tile, a guard zone, an empty gas kit, too many traps out.
//   9. The Stacks: a trainee's step in gives two practice nets once; a practice net holding a Penny Rat completes the
//      lesson. The eight Rootdeep floor traps: their classes, tiles, the home-range rule, placed once.
//  10. The eight Tinkering recipes and their gates (gains stop at the shard's stock top, 130); the kit and the trap survive a round trip with every field set.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.CreatureStealing;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DeployableTrapsVerification
{
    private static readonly Point3D Field = new(2000, 2000, 0);

    public DeployableTrapsVerification()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsurePoisons();
        ClusterFTutorial.Enabled = true;
        RootdeepStacksRegion.Initialize();
    }

    private sealed class Trapper : IDisposable
    {
        public readonly Account Account = new($"p275{Guid.NewGuid():N}"[..16], "p275-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Trapper(double removeTrap = 100, Point3D? at = null, Map? map = null)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Trapper", RawStr = 100, RawDex = 100, RawInt = 100 };
            Pm.AddItem(new Backpack());
            Pm.Skills.RemoveTrap.Cap = 200;
            Pm.Skills.RemoveTrap.Base = removeTrap;
            Account[0] = Pm;
            Pm.MoveToWorld(at ?? Field, map ?? Map.Trammel);
            Pm.Hits = Pm.HitsMax;

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public Trapper Den(int standing)
        {
            var data = ClusterFAccountPersistence.GetOrCreateGuild(Pm);
            data.JoinedGuilds.Add(ThievesDenStealRules.GuildKey);
            data.GuildReputation[ThievesDenStealRules.GuildKey] = standing;
            return this;
        }

        public T Keep<T>(T e) where T : IEntity
        {
            Litter.Add(e);
            return e;
        }

        public T Kit<T>(Action<T>? load = null) where T : DeployableTrapKit, new()
        {
            var kit = new T();
            load?.Invoke(kit);
            Pm.AddToBackpack(kit);
            return kit;
        }

        /// <summary>Sets a kit one tile east of the trapper (the arming already done).</summary>
        public DeployedTrap Set(DeployableTrapKit kit, bool hidden = false, int dx = 1)
        {
            var trap = kit.Set(Pm, new Point3D(Pm.X + dx, Pm.Y, Pm.Z), hidden);
            Assert.NotNull(trap);
            return Keep(trap!);
        }

        public void Dispose()
        {
            foreach (var e in Litter)
            {
                e.Delete();
            }

            foreach (var t in DeployableTraps.Placed.Where(t => t.Owner == Pm).ToList())
            {
                t.Delete();
            }

            Pm.Frozen = false;
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            Accounts.Remove(Account);
        }
    }

    /// <summary>A sturdy wild monster with no Detect Hidden, three tiles east of <paramref name="near"/>.</summary>
    private static Troll Monster(Trapper t, Point3D? at = null)
    {
        var troll = new Troll { HitsMaxSeed = 2000 };
        troll.Hits = troll.HitsMax;
        troll.Skills.DetectHidden.Base = 0;
        troll.MoveToWorld(at ?? new Point3D(t.Pm.X + 4, t.Pm.Y, t.Pm.Z), t.Pm.Map);
        return t.Keep(troll);
    }

    /// <summary>Walks <paramref name="m"/> onto the trap's tile from three tiles away, as Mobile.Move tells the trap.</summary>
    private static void WalkOnto(Mobile m, DeployedTrap trap)
    {
        var from = new Point3D(trap.X + 3, trap.Y, trap.Z);
        m.MoveToWorld(from, trap.Map);
        m.MoveToWorld(trap.Location, trap.Map);
        trap.OnMovement(m, from);
    }

    private static DeployableTrapKit LoadedKit(Trapper t, DeployableTrapKind kind) => kind switch
    {
        DeployableTrapKind.SnareNet => t.Kit<SnareNetKit>(),
        DeployableTrapKind.Caltrops => t.Kit<CaltropsKit>(),
        DeployableTrapKind.Gas      => t.Kit<GasTrapKit>(k => k.GasPoison = Poison.Regular),
        DeployableTrapKind.Blast    => t.Kit<BlastTrapKit>(k => k.BlastStrength = 1),
        DeployableTrapKind.Dart     => t.Kit<DartTrapKit>(k => k.Darts = 5),
        DeployableTrapKind.BearTrap => t.Kit<BearTrapKit>(),
        DeployableTrapKind.Alarm    => t.Kit<AlarmTrapKit>(),
        _                           => t.Kit<ShackleTrapKit>()
    };

    /// <summary>What a trap of this kind did to <paramref name="m"/> (hits before), or null when it did nothing visible.</summary>
    private static void AssertEffect(DeployableTrapKind kind, Mobile m, int hitsBefore)
    {
        switch (kind)
        {
            case DeployableTrapKind.SnareNet or DeployableTrapKind.Shackle:
                Assert.True(m.Frozen, $"{kind}: not held");
                break;
            case DeployableTrapKind.BearTrap:
                Assert.True(m.Frozen, "bear trap: not held");
                Assert.True(m.Hits < hitsBefore, "bear trap: no damage");
                break;
            case DeployableTrapKind.Caltrops:
                Assert.True(m.Hits < hitsBefore, "caltrops: no damage");
                Assert.True(DeployedTrap.IsSlowed(m), "caltrops: not slowed");
                break;
            case DeployableTrapKind.Gas:
                Assert.NotNull(m.Poison);
                break;
            case DeployableTrapKind.Blast or DeployableTrapKind.Dart:
                Assert.True(m.Hits < hitsBefore, $"{kind}: no damage");
                break;
        }
    }

    private static void AssertNothing(Mobile m, int hitsBefore, DeployedTrap trap, int charges)
    {
        Assert.False(m.Frozen);
        Assert.Equal(hitsBefore, m.Hits);
        Assert.Null(m.Poison);
        Assert.Equal(charges, trap.Charges);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EachTrapFiresOnAMonsterAndNeverOnAPlayerPetOrSummon()
    {
        Assert.False(DeployableTraps.PvpEnabled);

        foreach (var kind in DeployableTraps.AllKinds)
        {
            using var t = new Trapper();
            using var stranger = new Trapper(0, new Point3D(Field.X, Field.Y + 6, 0));

            var trap = t.Set(LoadedKit(t, kind));
            var charges = trap.Charges;
            Assert.True(charges > 0, $"{kind}: no charges");
            Assert.False(trap.Visible);
            Assert.True(trap.CanSeeStaffOnly(t.Pm));
            Assert.False(trap.CanSeeStaffOnly(stranger.Pm));

            // A player, a pet and a summon walk in: nothing.
            var hits = stranger.Pm.Hits;
            WalkOnto(stranger.Pm, trap);
            AssertNothing(stranger.Pm, hits, trap, charges);

            var pet = t.Keep(new Troll { HitsMaxSeed = 2000 });
            pet.Hits = pet.HitsMax;
            pet.MoveToWorld(new Point3D(Field.X + 5, Field.Y, 0), Map.Trammel);
            pet.SetControlMaster(stranger.Pm);
            hits = pet.Hits;
            WalkOnto(pet, trap);
            AssertNothing(pet, hits, trap, charges);

            var summon = t.Keep(new Troll { HitsMaxSeed = 2000, Summoned = true });
            summon.Hits = summon.HitsMax;
            hits = summon.Hits;
            WalkOnto(summon, trap);
            AssertNothing(summon, hits, trap, charges);

            // A monster: it fires.
            var monster = Monster(t);
            hits = monster.Hits;
            WalkOnto(monster, trap);
            Assert.Equal(charges - 1, trap.Charges);
            AssertEffect(kind, monster, hits);
            monster.Frozen = false;
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ThePvpSwitchLetsAFeluccaTrapHitAPlayerAndFlagsTheOwner()
    {
        const string key = "clusterf.traps.pvp";
        var kept = DeployableTraps.PvpEnabled;

        try
        {
            ServerConfiguration.SetSetting(key, true);

            using (var t = new Trapper(map: Map.Trammel))
            using (var victim = new Trapper(0, new Point3D(Field.X, Field.Y + 6, 0)))
            {
                var trap = t.Set(t.Kit<BearTrapKit>());
                var hits = victim.Pm.Hits;
                WalkOnto(victim.Pm, trap);
                AssertNothing(victim.Pm, hits, trap, 3);
            }

            using (var t = new Trapper(map: Map.Felucca))
            using (var victim = new Trapper(0, new Point3D(Field.X, Field.Y + 6, 0), Map.Felucca))
            {
                var trap = t.Set(t.Kit<BearTrapKit>());
                var hits = victim.Pm.Hits;
                WalkOnto(victim.Pm, trap);
                Assert.Equal(2, trap.Charges);
                Assert.True(victim.Pm.Hits < hits);
                Assert.Contains(victim.Pm.Aggressors, a => a.Attacker == t.Pm);
                victim.Pm.Frozen = false;
            }
        }
        finally
        {
            ServerConfiguration.SetSetting(key, kept);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ATrapFiresOnlyWhileItsOwnerIsPresentAndTheDamageIsTheOwners()
    {
        using var t = new Trapper();
        var trap = t.Set(t.Kit<BearTrapKit>());
        var monster = Monster(t);
        var near = t.Pm.Location;

        // 13 tiles: dormant.
        t.Pm.MoveToWorld(new Point3D(trap.X - 13, trap.Y, 0), Map.Trammel);
        WalkOnto(monster, trap);
        Assert.Equal(3, trap.Charges);

        // Logged out.
        t.Pm.MoveToWorld(near, Map.Trammel);
        t.Pm.NetState = null;
        WalkOnto(monster, trap);
        Assert.Equal(3, trap.Charges);
        t.Pm.NetState = t.Ns;

        // Another map.
        t.Pm.MoveToWorld(near, Map.Felucca);
        WalkOnto(monster, trap);
        Assert.Equal(3, trap.Charges);

        // Dead.
        t.Pm.MoveToWorld(near, Map.Trammel);
        t.Pm.Kill();
        Assert.False(t.Pm.Alive);
        WalkOnto(monster, trap);
        Assert.Equal(3, trap.Charges);
        t.Pm.Resurrect();
        Assert.False(monster.Frozen);

        // 12 tiles, alive, logged in: it fires, and the damage is the owner's.
        t.Pm.MoveToWorld(new Point3D(trap.X - 12, trap.Y, 0), Map.Trammel);
        var hits = monster.Hits;
        WalkOnto(monster, trap);
        Assert.Equal(2, trap.Charges);
        Assert.True(monster.Hits < hits);
        Assert.Contains(monster.DamageEntries.ToArray(), e => e.Damager == t.Pm);
        Assert.Contains(BaseCreature.GetLootingRights(monster.DamageEntries, monster.HitsMax), r => r.m_Mobile == t.Pm && r.m_HasRight);
        monster.Frozen = false;

        // The alarm ignores the range, not the map.
        var alarm = t.Set(t.Kit<AlarmTrapKit>(), dx: -1);
        var other = Monster(t, new Point3D(alarm.X - 5, alarm.Y, 0));
        t.Pm.MoveToWorld(new Point3D(alarm.X + 40, alarm.Y, 0), Map.Trammel);
        WalkOnto(other, alarm);
        Assert.Equal(9, alarm.Charges);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ChargesRunOutAndOnlyAHandReSetRearmsATrap()
    {
        using var t = new Trapper();
        var trap = t.Set(t.Kit<BearTrapKit>());
        Assert.Equal(3, trap.Charges);
        Assert.Equal(TimeSpan.FromMinutes(30), trap.DecayTime);

        for (var snap = 0; snap < 3; snap++)
        {
            var m = Monster(t);
            WalkOnto(m, trap);
            m.Frozen = false;
            Assert.Equal(2 - snap, trap.Charges);
        }

        Assert.False(trap.Armed);
        Assert.Equal(DeployableTraps.SpentArt(DeployableTrapKind.BearTrap), trap.ItemID);
        Assert.Equal(TimeSpan.FromMinutes(2), trap.DecayTime);

        var fourth = Monster(t);
        var hits = fourth.Hits;
        WalkOnto(fourth, trap);
        AssertNothing(fourth, hits, trap, 0);

        // Nothing re-arms it but the owner's hand.
        trap.OnDoubleClick(t.Pm);
        Assert.Equal(3, trap.Charges);
        Assert.Equal(DeployableTraps.SetArt(DeployableTrapKind.BearTrap), trap.ItemID);

        var exceptional = t.Set(t.Kit<BearTrapKit>(k => k.Exceptional = true), dx: -1);
        Assert.Equal(4, exceptional.Charges);

        // A spent gas trap waits for a fresh potion dropped on it.
        var gas = t.Set(t.Kit<GasTrapKit>(k => k.GasPoison = Poison.Regular), dx: 2);
        WalkOnto(Monster(t), gas);
        Assert.False(gas.Armed);
        Assert.False(gas.ReSet(t.Pm));
        Assert.False(gas.Armed);

        t.Pm.Skills.Poisoning.Base = 100;
        var potion = new GreaterPoisonPotion();
        t.Pm.AddToBackpack(potion);
        Assert.True(gas.OnDragDrop(t.Pm, potion));
        Assert.True(gas.Armed);
        Assert.Same(Poison.Greater, gas.GasPoison);
    }

    // ---------------------------------------------------------------- 5

    private sealed class SkillRolls : IDisposable
    {
        private readonly SkillCheckLocationHandler _old = Mobile.SkillCheckLocationHandler;
        public readonly List<(SkillName Skill, double Min, double Max)> Rolls = new();

        public SkillRolls()
        {
            Mobile.SkillCheckLocationHandler = (m, skill, min, max) =>
            {
                Rolls.Add((skill, min, max));
                return _old(m, skill, min, max);
            };
        }

        public void Dispose() => Mobile.SkillCheckLocationHandler = _old;
    }

    [Fact]
    public void RemoveTrapSetsAndDisarmsAndGainsOnBoth()
    {
        using var t = new Trapper(0);
        Assert.Equal(0, t.Pm.Skills.RemoveTrap.Base);

        DeployedTrap trap;
        using (var rolls = new SkillRolls())
        {
            trap = t.Set(t.Kit<SnareNetKit>());
            Assert.Equal(new[] { (SkillName.RemoveTrap, 0.0, 50.0) }, rolls.Rolls);
        }

        var afterSet = t.Pm.Skills.RemoveTrap.Base;
        Assert.True(afterSet > 0, "no Remove Trap gain on the set");

        using (var rolls = new SkillRolls())
        {
            var m = Monster(t);
            WalkOnto(m, trap);
            m.Frozen = false;
            Assert.Equal(new[] { (SkillName.RemoveTrap, 40.0, 120.0) }, rolls.Rolls);
        }

        // The owner disarms a spent net: a gain, and the kit back in the pack.
        trap.OnRemoveTrap(t.Pm);
        Assert.True(trap.Deleted);
        Assert.True(t.Pm.Skills.RemoveTrap.Base > afterSet, "no Remove Trap gain on the disarm");
        Assert.NotNull(t.Pm.Backpack.FindItemByType<SnareNetKit>());

        // A stranger salvages one on a success.
        t.Pm.Skills.RemoveTrap.Base = 100;
        var bear = t.Set(t.Kit<BearTrapKit>(k => k.Exceptional = true), dx: -1); // the netted troll still stands east
        using var thief = new Trapper(200, new Point3D(Field.X + 2, Field.Y + 1, 0));
        bear.OnRemoveTrap(thief.Pm);
        Assert.True(bear.Deleted);
        var salvaged = thief.Pm.Backpack.FindItemByType<BearTrapKit>();
        Assert.NotNull(salvaged);
        Assert.True(salvaged!.Exceptional);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void BelowMasterABossWalksThroughAndAMastersTrapHoldsIt()
    {
        foreach (var (standing, fires) in new[] { (-1, false), (5_000, false), (15_000, true) })
        {
            using var t = new Trapper();
            if (standing >= 0)
            {
                t.Den(standing);
            }

            var trap = t.Set(t.Kit<SnareNetKit>());
            var boss = t.Keep(new TheBurrowMother());
            boss.MoveToWorld(new Point3D(trap.X + 3, trap.Y, 0), Map.Trammel);
            boss.Skills.DetectHidden.Base = 0;
            Assert.True(DeployableTraps.IsBoss(boss));

            WalkOnto(boss, trap);
            Assert.Equal(fires, boss.Frozen);
            Assert.Equal(fires ? 0 : 1, trap.Charges);
            boss.Frozen = false;
        }

        // Journeyman: one more trap out.
        using var j = new Trapper(0);
        Assert.Equal(1, DeployableTraps.MaxOut(j.Pm));
        j.Den(5_000);
        Assert.Equal(2, DeployableTraps.MaxOut(j.Pm));
        j.Pm.Skills.RemoveTrap.Base = 100;
        Assert.Equal(4, DeployableTraps.MaxOut(j.Pm));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void FlyersIncorporealAndSharpEyedCreaturesShrugTrapsOff()
    {
        using var t = new Trapper();
        var snare = t.Set(t.Kit<SnareNetKit>());
        var eagle = t.Keep(new Eagle());
        WalkOnto(eagle, snare);
        Assert.False(eagle.Frozen);
        Assert.Equal(1, snare.Charges);

        var dart = t.Set(t.Kit<DartTrapKit>(k => k.Darts = 5), dx: -1);
        var wraith = t.Keep(new Wraith { HitsMaxSeed = 500 });
        wraith.Hits = wraith.HitsMax;
        wraith.Skills.DetectHidden.Base = 0;
        var hits = wraith.Hits;
        WalkOnto(wraith, dart);
        Assert.Equal(hits, wraith.Hits);
        Assert.Equal(5, dart.Charges);

        // The gas is not physical: the wraith breathes it.
        var gas = t.Set(t.Kit<GasTrapKit>(k => k.GasPoison = Poison.Regular), dx: 2);
        wraith.MoveToWorld(new Point3D(gas.X + 3, gas.Y + 3, 0), Map.Trammel);
        WalkOnto(wraith, gas);
        Assert.False(gas.Armed);

        // A creature with Detect Hidden far above the concealment spots the trap and walks around it.
        using var low = new Trapper(30, new Point3D(Field.X + 20, Field.Y, 0));
        var caltrops = low.Set(low.Kit<CaltropsKit>());
        var sharp = Monster(low);
        sharp.Skills.DetectHidden.Base = 200;
        hits = sharp.Hits;
        WalkOnto(sharp, caltrops);
        Assert.Equal(hits, sharp.Hits);
        Assert.Equal(5, caltrops.Charges);
        Assert.True(caltrops.Visible);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void ATrapRefusesBadGround()
    {
        using var t = new Trapper(0);

        Assert.Null(t.Kit<SnareNetKit>().Set(t.Pm, new Point3D(Field.X + 3, Field.Y, 0), false)); // out of reach
        Assert.Null(t.Kit<SnareNetKit>().Set(t.Pm, t.Pm.Location, false));                         // the setter stands there

        Assert.Null(t.Kit<ShackleTrapKit>().Set(t.Pm, new Point3D(Field.X + 1, Field.Y, 0), false)); // needs Remove Trap 80

        t.Pm.Skills.RemoveTrap.Base = 100;
        Assert.Null(t.Kit<GasTrapKit>().Set(t.Pm, new Point3D(Field.X + 1, Field.Y, 0), false));    // empty

        var town = new GuardedRegion("P275 town", Map.Trammel, 50, new Rectangle3D(new Point3D(1990, 1990, -128), new Point3D(2010, 2010, 127)));
        town.Register();
        try
        {
            Assert.Null(t.Kit<SnareNetKit>().Set(t.Pm, new Point3D(Field.X + 1, Field.Y, 0), false));
        }
        finally
        {
            town.Unregister();
        }

        // RT 100: three out at once.
        t.Set(t.Kit<SnareNetKit>(), dx: 1);
        t.Set(t.Kit<SnareNetKit>(), dx: -1);
        var third = t.Kit<SnareNetKit>().Set(t.Pm, new Point3D(Field.X, Field.Y + 1, 0), false);
        Assert.NotNull(third);
        t.Keep(third!);
        Assert.Null(t.Kit<SnareNetKit>().Set(t.Pm, new Point3D(Field.X, Field.Y - 1, 0), false));
        Assert.Equal(3, DeployableTraps.CountOut(t.Pm));
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheStacksHandOutPracticeNetsAndANettedRatCompletesTheLesson()
    {
        var stacks = new Point3D(5160, 1137, 0);
        Assert.IsType<RootdeepStacksRegion>(Region.Find(stacks, Map.Trammel));
        Assert.Same(RootdeepVaultRegion.Instance, Region.Find(stacks, Map.Trammel).Parent);
        Assert.IsType<RootdeepVaultRegion>(Region.Find(new Point3D(5150, 1150, 0), Map.Trammel));

        using var t = new Trapper(0, new Point3D(5180, 1137, 0));
        Assert.True(ClusterFTutorial.TryEnter(t.Pm));

        t.Pm.MoveToWorld(stacks, Map.Trammel);
        var nets = t.Pm.Backpack.Items.OfType<SnareNetKit>().Where(k => k.Practice).ToList();
        Assert.Equal(2, nets.Count);

        // Out and back in: no more nets while they carry one.
        t.Pm.MoveToWorld(new Point3D(5150, 1150, 0), Map.Trammel);
        t.Pm.MoveToWorld(stacks, Map.Trammel);
        Assert.Equal(2, t.Pm.Backpack.Items.OfType<SnareNetKit>().Count(k => k.Practice));

        var trap = t.Set(nets[0]);
        Assert.True(trap.Practice);
        var rat = t.Keep(new PennyRat());
        rat.MoveToWorld(new Point3D(trap.X + 3, trap.Y, 0), Map.Trammel);
        WalkOnto(rat, trap);
        Assert.True(rat.Frozen);
        Assert.True(TrapLesson.HasDone(t.Pm));
        Assert.False(trap.Armed);
        rat.Frozen = false;

        // Done: no more nets. And a practice net is never salvaged, and crumbles outside the Burrows.
        Assert.Equal(0, TrapLesson.OnEnterStacks(t.Pm));
        trap.OnRemoveTrap(t.Pm);
        Assert.Null(t.Pm.Backpack.FindItemByType<SnareNetKit>(true, k => !k.Practice));
        // P371: leave the Burrows for the island surface; this trainee has not graduated yet.
        t.Pm.MoveToWorld(new Point3D(3503, 2574, 14), Map.Trammel);
        nets[1].OnDoubleClick(t.Pm);
        Assert.True(nets[1].Deleted);
    }

    [Fact]
    public void TheRootdeepFloorTrapsAreStockClassesOffEveryHomeRangeAndPlacedOnce()
    {
        var rows = ClusterFRootdeepSeeder.FloorTraps;
        Assert.Equal(8, rows.Length);
        Assert.Equal(8, rows.Select(r => r.Location).Distinct().Count());

        foreach (var row in rows)
        {
            var trap = ClusterFRootdeepSeeder.MakeFloorTrap(row);
            try
            {
                Assert.Equal(row.Kind, trap.GetType().Name);
                Assert.True(Region.Find(new Point3D(row.X, row.Y, 0), Map.Trammel).IsPartOf<OldHavenBurrowsRegion>(), row.Label);

                // Those that hurt creatures sit outside every spawner's walking box; the gas trap hurts players only.
                if (trap is not GasTrap)
                {
                    foreach (var s in ClusterFRootdeepSeeder.Spawners)
                    {
                        var inBox = Math.Abs(s.X - row.X) <= s.HomeRange && Math.Abs(s.Y - row.Y) <= s.HomeRange;
                        Assert.False(inBox, $"{row.Label} is inside {s.Label}'s home range");
                    }
                }
            }
            finally
            {
                trap.Delete();
            }
        }

        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        try
        {
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var first = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal(8, first.FloorTraps);
            Assert.All(rows, r => Assert.NotNull(ClusterFRootdeepSeeder.FindFloorTrap(Map.Trammel, r)));
            var again = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal((0, 8), (again.FloorTraps, again.FloorTrapsPresent));
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            foreach (var row in rows)
            {
                ClusterFRootdeepSeeder.FindFloorTrap(Map.Trammel, row)?.Delete();
            }

            foreach (var row in ClusterFRootdeepSeeder.Spawners)
            {
                var s = ClusterFRootdeepSeeder.FindSpawner(Map.Trammel, row);
                s?.RemoveSpawns();
                s?.Delete();
            }

            foreach (var row in ClusterFRootdeepSeeder.Teleporters)
            {
                ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, row)?.Delete();
            }

            ClusterFRootdeepSeeder.FindCrawlway(Map.Trammel)?.Delete();
            var ankhAt = new Point3D(ClusterFRootdeepSeeder.AnkhAt.X, ClusterFRootdeepSeeder.AnkhAt.Y, 0);
            var ankhs = new List<Item>();
            foreach (var item in Map.Trammel.GetItemsInRange(ankhAt, 2))
            {
                if (ClusterFAnkhs.IsWorkingAnkh(item))
                {
                    ankhs.Add(item);
                }
            }

            ankhs.ForEach(a => a.Delete());
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void TinkeringMakesTheEightKitsAtTheirGates()
    {
        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        TinkeringCraftRegistrations.Register();
        var tinkering = DefTinkering.CraftSystem;

        var expected = new (Type Type, double Min)[]
        {
            (typeof(SnareNetKit), 30), (typeof(CaltropsKit), 40), (typeof(GasTrapKit), 60), (typeof(BlastTrapKit), 90),
            (typeof(DartTrapKit), 50), (typeof(BearTrapKit), 80), (typeof(AlarmTrapKit), 70), (typeof(ShackleTrapKit), 110)
        };

        foreach (var (type, min) in expected)
        {
            var craft = Assert.Single(tinkering.CraftItems, c => c.ItemType == type);
            Assert.Equal(1044052, (int)craft.GroupNameNumber); // Traps
            var skill = Assert.Single(craft.Skills);
            Assert.Equal(SkillName.Tinkering, skill.SkillToMake);
            Assert.Equal((min, Math.Min(min + 50, 130.0)), (skill.MinSkill, skill.MaxSkill)); // the shard's stock top
        }

        var bear = tinkering.CraftItems.SearchFor(typeof(BearTrapKit));
        Assert.Equal(new[] { (typeof(IronIngot), 10), (typeof(Springs), 2), (typeof(Gears), 1) }, bear.Resources.Select(r => (r.ItemType, r.Amount)));
    }

    [Fact]
    public void TheKitAndTheTrapSurviveARoundTrip()
    {
        using var t = new Trapper();
        var kit = t.Keep(new DartTrapKit
        {
            Exceptional = true, Crafter = t.Pm, Practice = true, GasPoison = Poison.Deadly, BlastStrength = 2, Darts = 4,
            DartPoison = Poison.Greater, DartPoisonCharges = 3
        });

        var kitCopy = ConsumablesVerification.RoundTrip(kit, s => new DartTrapKit(s));
        Assert.Equal(
            (true, t.Pm, true, Poison.Deadly, 2, 4, Poison.Greater, 3),
            (kitCopy.Exceptional, (PlayerMobile)kitCopy.Crafter!, kitCopy.Practice, kitCopy.GasPoison, kitCopy.BlastStrength, kitCopy.Darts,
                kitCopy.DartPoison, kitCopy.DartPoisonCharges)
        );
        kitCopy.Delete();

        var trap = t.Keep(new DeployedTrap(DeployableTrapKind.Blast, t.Pm, 155.5, true, true, true)
        {
            Charges = 2, AmbushUsed = true, GasPoison = Poison.Lethal, BlastStrength = 0, Darts = 3, DartPoison = Poison.Lesser,
            DartPoisonCharges = 1
        });

        var copy = ConsumablesVerification.RoundTrip(trap, s => new DeployedTrap(s));
        try
        {
            Assert.Equal(
                (DeployableTrapKind.Blast, (Mobile)t.Pm, 155.5, true, true, true, 2, true),
                (copy.Kind, copy.Owner!, copy.SetterSkill, copy.PlacedHidden, copy.Exceptional, copy.Practice, copy.Charges, copy.AmbushUsed)
            );
            Assert.Equal((Poison.Lethal, 0, 3, Poison.Lesser, 1), (copy.GasPoison, copy.BlastStrength, copy.Darts, copy.DartPoison, copy.DartPoisonCharges));
            Assert.Contains(copy, DeployableTraps.Placed);
        }
        finally
        {
            copy.Delete();
        }

        Assert.DoesNotContain(copy, DeployableTraps.Placed);
    }
}
