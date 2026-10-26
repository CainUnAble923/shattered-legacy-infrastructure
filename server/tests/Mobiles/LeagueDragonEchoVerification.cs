// LeagueDragonEchoVerification.cs
//
// cc-P630 (P567 design, stage 1): the Echo of the Stygian Dragon itself. Notes in shard-migration
// notes/cc-P630-build-echo-summon.md.
//
// Facts:
//   1. Its profile is the design's weak one: 200/100/50, 300 hits, no mana, 12-16 damage (75 physical, 25 fire), 35 in
//      every resist, 80 Wrestling, Tactics and Magic Resist with every skill locked, no fame, karma, armor, resources
//      or loot, two slots, body 826 in League blue, dispellable at 100/20, never flees, never stabled or bonded.
//   2. Lethal damage dissolves it: no corpse, no carcass, gone from the registry, the slots back.
//   3. It may harm a wild creature and nothing else: not a player, a pet, a summon, a vendor, a guard or an escort, and
//      not a creature that was tamed after it was picked.
//   4. A saved Echo never resumes: after a load it deletes itself and is never registered.
//   cc-P647 (the P642 and P630 reviews):
//   5. A wild creature's lethal hit on the Echo gives its full-health owner no pet-loss count, AP or Renown; an ordinary
//      pet killed the same way still counts.
//   6. Between watchdog passes, the moment its caller stops qualifying (leash, rank, switch, a town, gone) it refuses harm
//      and dissolves on the next tick.

using System;
using Server;
using Server.Engines.LeagueEchoes;
using Server.Engines.LeagueRegister;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueDragonEchoVerification
{
    public LeagueDragonEchoVerification() => ShardTestHost.EnsureAccounts();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ItsProfileIsTheDesignsWeakOne()
    {
        var echo = new LeagueDragonEcho();
        try
        {
            Assert.Equal(200, echo.RawStr);
            Assert.Equal(100, echo.RawDex);
            Assert.Equal(50, echo.RawInt);
            Assert.Equal(300, echo.HitsMax);
            Assert.Equal(0, echo.ManaMax);
            Assert.Equal(12, echo.DamageMin);
            Assert.Equal(16, echo.DamageMax);
            Assert.Equal(75, echo.PhysicalDamage);
            Assert.Equal(25, echo.FireDamage);
            Assert.Equal(35, echo.PhysicalResistance);
            Assert.Equal(35, echo.FireResistance);
            Assert.Equal(35, echo.ColdResistance);
            Assert.Equal(35, echo.PoisonResistance);
            Assert.Equal(35, echo.EnergyResistance);

            Assert.Equal(80.0, echo.Skills.Wrestling.Base);
            Assert.Equal(80.0, echo.Skills.Tactics.Base);
            Assert.Equal(80.0, echo.Skills.MagicResist.Base);
            Assert.Equal(0.0, echo.Skills.Magery.Base);
            for (var i = 0; i < echo.Skills.Length; i++)
            {
                Assert.Equal(SkillLock.Locked, echo.Skills[i].Lock);
            }

            Assert.Equal(0, echo.Fame);
            Assert.Equal(0, echo.Karma);
            Assert.Equal(0, echo.VirtualArmor);
            Assert.True(echo.NoKillAwards);
            Assert.Equal(2, echo.ControlSlots);
            Assert.Equal(826, echo.Body.BodyID);
            Assert.Equal(1323, echo.Hue);
            Assert.Equal(AIType.AI_Melee, echo.AI);
            Assert.Equal(FightMode.None, echo.FightMode);
            Assert.Equal(100.0, echo.DispelDifficulty);
            Assert.Equal(20.0, echo.DispelFocus);
            Assert.False(echo.CanFlee);
            Assert.False(echo.CanAutoStable);
            Assert.False(echo.IsBondable);
            Assert.Equal(0, echo.Meat + echo.Hides + echo.Scales + echo.Feathers + echo.Wool);
            Assert.Equal(-1, echo.TreasureMapLevel);
            Assert.True(echo.Backpack == null || echo.Backpack.Items.Count == 0);
            Assert.Equal("an Echo of the Stygian Dragon", echo.Name);
            Assert.IsNotType<StygianDragon>(echo);
        }
        finally
        {
            echo.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void LethalDamageDissolvesItWithNoCorpse()
    {
        var a = LeagueEchoesVerification.NewAccount();
        var b = LeagueEchoesVerification.NewAccount();
        var pm = LeagueEchoesVerification.NewCharacter(a);
        var killer = LeagueEchoesVerification.NewCharacter(b);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            // The kill counter the achievements keep (CreatureDeathEvent, any LastKiller player): an orc's death counts.
            var orc = new Orc();
            orc.MoveToWorld(new Point3D(LeagueEchoesVerification.Wild.X + 5, LeagueEchoesVerification.Wild.Y, 0), Map.Trammel);
            var kills = ClusterFAchievementSystem.GetCounter(b.Username, "kills");
            orc.LastKiller = killer;
            orc.Kill();
            Assert.Equal(kills + 1, ClusterFAchievementSystem.GetCounter(b.Username, "kills"));
            var orcCorpses = new System.Collections.Generic.List<Corpse>();
            foreach (var c in Map.Trammel.GetItemsInRange<Corpse>(orc.Location, 2))
            {
                orcCorpses.Add(c);
            }

            orcCorpses.ForEach(c => c.Delete());

            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));
            Assert.True(echo!.IsDispellable);
            Assert.True(echo.Backpack == null || echo.Backpack.Items.Count == 0);
            var at = echo.Location;

            echo.LastKiller = killer;
            echo.Kill();

            // It dissolved before dying: no death event, so nothing counted.
            Assert.Equal(kills + 1, ClusterFAchievementSystem.GetCounter(b.Username, "kills"));

            Assert.True(echo.Deleted);
            Assert.Equal(0, pm.Followers);
            Assert.Null(LeagueEchoSystem.ActiveFor(a));
            var corpses = 0;
            foreach (var _ in Map.Trammel.GetItemsInRange<Corpse>(at, 3))
            {
                corpses++;
            }

            Assert.Equal(0, corpses);
        }
        finally
        {
            LeagueEchoesVerification.Cleanup(a, b);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ItMayHarmAWildCreatureAndNothingElse()
    {
        var a = LeagueEchoesVerification.NewAccount();
        var b = LeagueEchoesVerification.NewAccount();
        var pm = LeagueEchoesVerification.NewCharacter(a);
        var player = LeagueEchoesVerification.NewCharacter(b, null!);
        LeagueEchoSettings.EnabledOverride = true;

        var at = new Point3D(LeagueEchoesVerification.Wild.X + 2, LeagueEchoesVerification.Wild.Y, 0);
        var orc = new Orc();
        var pet = new Horse { Controlled = true, ControlMaster = player };
        var summon = new SummonedDaemon { Summoned = true };
        var vendor = new Provisioner();
        var guard = new WarriorGuard();
        var escort = new EscortableMage();
        Mobile[] others = [orc, pet, summon, vendor, guard, escort];
        foreach (var m in others)
        {
            m.MoveToWorld(at, Map.Trammel);
        }

        try
        {
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));

            Assert.True(LeagueEchoPolicy.IsLegalTarget(orc));
            Assert.True(echo!.CanBeHarmful(orc, false));
            Assert.True(echo.IsEnemy(orc));

            Assert.False(echo.CanBeHarmful(player, false));
            Assert.False(echo.CanBeHarmful(pm, false));
            Assert.False(echo.CanBeHarmful(pet, false));
            Assert.False(echo.CanBeHarmful(summon, false));
            Assert.False(echo.CanBeHarmful(vendor, false));
            Assert.False(echo.CanBeHarmful(guard, false));
            Assert.False(echo.CanBeHarmful(escort, false));
            Assert.False(echo.IsEnemy(player));

            // Picked while wild, tamed before the swing: no longer a target, and the watchdog drops it.
            echo.Combatant = orc;
            Assert.Same(orc, echo.Combatant);
            orc.Controlled = true;
            orc.ControlMaster = player;
            Assert.False(echo.CanBeHarmful(orc, false));
            LeagueEchoSystem.Watch();
            Assert.Null(echo.Combatant);
        }
        finally
        {
            foreach (var m in others)
            {
                m.Delete();
            }

            LeagueEchoesVerification.Cleanup(a, b);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ASavedEchoDeletesItselfAfterLoad()
    {
        var a = LeagueEchoesVerification.NewAccount();
        var pm = LeagueEchoesVerification.NewCharacter(a);
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            ShardTestClock.Arm();
            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            echo!.Serialize(writer);
            writer.Flush();

            var copy = new LeagueDragonEcho(echo.Serial);
            echo.Delete();
            copy.Deserialize(new BufferReader(buffer));

            Assert.Null(LeagueEchoSystem.ActiveFor(a));
            ShardTestClock.AdvanceSeconds(0.1);
            Assert.True(copy.Deleted);
            Assert.Null(LeagueEchoSystem.ActiveFor(a));
        }
        finally
        {
            LeagueEchoesVerification.Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 5 (cc-P647, P642 R1)

    [Fact]
    public void AWildCreaturesLethalHitOnTheEchoGivesItsOwnerNoPetLossAchievement()
    {
        var a = LeagueEchoesVerification.NewAccount();
        var pm = LeagueEchoesVerification.NewCharacter(a); // online, full health
        var wild = LeagueEchoesVerification.Wild;
        var orc = new Orc();
        orc.MoveToWorld(new Point3D(wild.X + 2, wild.Y, 0), Map.Trammel);
        var horse = new Horse();
        LeagueEchoSettings.EnabledOverride = true;
        try
        {
            var counter = ClusterFAchievementSystem.NotedCounter(TriggerKind.PetDiedAtFullHp);
            var data = ClusterFAccountPersistence.GetOrCreate(a);
            var (ap, renown) = (data.AchievementPoints, data.Renown);
            pm.Hits = pm.HitsMax;

            Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));
            echo!.Damage(echo.Hits + 50, orc); // real damage: Hits reaches 0 before OnBeforeDeath dissolves it

            Assert.True(echo.Deleted);
            Assert.Equal(pm.HitsMax, pm.Hits);
            Assert.Equal(0, ClusterFAchievementSystem.GetCounter(a.Username, counter));
            Assert.False(ClusterFAchievementSystem.HasEarned(a, ClusterFAchievementSystem.AcceptableLossesKey));
            Assert.Equal(ap, data.AchievementPoints);
            Assert.Equal(renown, data.Renown);

            // The control: an ordinary pet killed the same way still counts.
            horse.MoveToWorld(new Point3D(wild.X + 1, wild.Y + 1, 0), Map.Trammel);
            Assert.True(horse.SetControlMaster(pm));
            horse.Damage(horse.Hits + 50, orc);
            Assert.Equal(1, ClusterFAchievementSystem.GetCounter(a.Username, counter));
        }
        finally
        {
            horse.Delete();
            orc.Delete();
            LeagueEchoesVerification.Cleanup(a);
        }
    }

    // ---------------------------------------------------------------- 6 (cc-P647, P630 review)

    [Fact]
    public void BetweenWatchdogPassesItRefusesHarmTheMomentItsCallerStopsQualifying()
    {
        var a = LeagueEchoesVerification.NewAccount();
        var pm = LeagueEchoesVerification.NewCharacter(a);
        var wild = LeagueEchoesVerification.Wild;
        var orc = new Orc();
        orc.MoveToWorld(new Point3D(wild.X + 2, wild.Y, 0), Map.Trammel);
        LeagueEchoSettings.EnabledOverride = true;
        Server.Regions.GuardedRegion town = null;
        try
        {
            void Refuses(string why, Action stop, Action undo)
            {
                ShardTestClock.Arm(); // the watchdog's one-second timer never runs inside this
                ClusterFAccountPersistence.GetOrCreate(a).LeagueEchoNextUseUtc = DateTime.MinValue;
                Assert.Equal(EchoCall.Called, LeagueEchoSystem.TryCall(pm, out var echo));
                Assert.True(echo!.CanBeHarmful(orc, false), why);

                stop();

                Assert.False(echo.CanBeHarmful(orc, false), why);
                ShardTestClock.AdvanceSeconds(0.1);
                Assert.True(echo.Deleted, why);
                Assert.Equal(0, pm.Followers);
                undo();
            }

            Refuses("leash", () => pm.MoveToWorld(new Point3D(wild.X + 13, wild.Y, 0), Map.Trammel),
                () => pm.MoveToWorld(wild, Map.Trammel));
            Refuses("rank", () => ClusterFLeagueRanks.SetRank(pm, LeagueRegisterSettings.Rank("Blaze")),
                () => ClusterFLeagueRanks.SetRank(pm, LeagueRegisterSettings.Rank("Mythril")));
            Refuses("switch", () => LeagueEchoSettings.EnabledOverride = false, () => LeagueEchoSettings.EnabledOverride = true);

            Refuses("caller in a town", () =>
            {
                town = new Server.Regions.GuardedRegion("P647 echo town", Map.Trammel, 50,
                    new Rectangle3D(new Point3D(pm.X - 1, pm.Y - 1, -128), new Point3D(pm.X + 1, pm.Y + 1, 127)));
                town.Register();
            }, () =>
            {
                town.Unregister();
                town = null;
            });

            Refuses("owner gone", () => pm.Delete(), () => { });
        }
        finally
        {
            town?.Unregister();
            orc.Delete();
            LeagueEchoesVerification.Cleanup(a);
        }
    }
}
