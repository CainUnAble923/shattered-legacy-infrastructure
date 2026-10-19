// ClusterFCorpseValueVerification.cs
//
// cc-P175 Part B (Chase 2026-10-18: "monster corpse values"). A corpse a Custodian clears pays a base by the creature that
// died, its Fame x 0.001 (ClusterFCorpseValue), plus each item inside at its Clean Up value. Until cc-P175 every corpse
// paid a flat 0.10.
//
// Facts:
//   1. The sample table: real kills (the Custodian's damage on record, then Kill), each corpse's base is the creature's
//      Fame x 0.001 for its killer: rabbit 0.15, orc 1.5, lich 8, dragon 15, ancient wyrm 22.5, a champion (Meraktus)
//      70, the Burrows' scamp 0.6 and mother 3, and ours (the Dread Warhorse). The contents still pay on top.
//   2. What pays no base: a summoned creature, a tamed pet, a bonded pet, a creature with no Fame, a corpse another
//      player earned, and a corpse with no death on record. A party member of the killer earns it.
//   3. Corpse points count toward the lifetime rank like any other Clean Up points.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.CleanUpBritannia;
using Server.Engines.PartySystem;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ClusterFCorpseValueVerification
{
    private static readonly Point3D Spot = new(1450, 1650, 0);

    private readonly ITestOutputHelper _out;

    public ClusterFCorpseValueVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems(); // a corpse's contents pay by the craft tables
        CleanUpCraftHost.EnsureAccounts();     // and points live on the character, as cc-P33's facts set up
    }

    private static PlayerMobile Player(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    // Kills the creature with the damage on record from the given players, and returns its corpse.
    private static Corpse KillBy(BaseCreature bc, params Mobile[] damagers)
    {
        bc.MoveToWorld(Spot, Map.Trammel);
        foreach (var m in damagers)
        {
            bc.RegisterDamage(Math.Max(1, bc.HitsMax / Math.Max(1, damagers.Length)), m);
        }

        bc.Kill();
        return Assert.IsAssignableFrom<Corpse>(bc.Corpse);
    }

    public static IEnumerable<object[]> Samples() =>
    [
        [typeof(Rabbit), 0.15], [typeof(Mongbat), 0.15], [typeof(Skeleton), 0.45], [typeof(Orc), 1.5],
        [typeof(DreadSpider), 5.0], [typeof(Lich), 8.0], [typeof(OgreLord), 15.0], [typeof(Dragon), 15.0],
        [typeof(AncientWyrm), 22.5], [typeof(Meraktus), 70.0], [typeof(BurrowScamp), 0.6], [typeof(TheBurrowMother), 3.0],
        [typeof(DreadWarhorse), -1.0] // ours: whatever its Fame says
    ];

    // ---------------------------------------------------------------- 1

    [Theory]
    [MemberData(nameof(Samples))]
    public void ACorpsePaysItsCreaturesFameOverAThousand(Type type, double expected)
    {
        var pm = Player("p175 killer");
        var bc = (BaseCreature)Activator.CreateInstance(type)!;
        var fame = bc.Fame;
        Corpse corpse = null;
        try
        {
            corpse = KillBy(bc, pm);
            var contents = 0.0;
            foreach (var item in corpse.FindItemsByType<Item>())
            {
                contents += CleanUpBritanniaData.GetPoints(item);
            }

            var base_ = ClusterFCorpseValue.BaseFor(corpse, pm);

            _out.WriteLine($"| {type.Name} | {fame} | {base_:0.###} | {contents:0.##} | {ClusterFCustodianSystem.ComputePoints(corpse, pm):0.##} |");

            Assert.Equal(fame * ClusterFCorpseValue.PointsPerFame, base_, 9);
            if (expected >= 0)
            {
                Assert.Equal(expected, base_, 9);
            }

            Assert.Equal(base_ + contents, ClusterFCustodianSystem.ComputePoints(corpse, pm), 9);
        }
        finally
        {
            corpse?.Delete();
            bc.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SummonedTamedBondedFamelessAndUnearnedCorpsesPayNoBase()
    {
        var pm = Player("p175 custodian");
        var other = Player("p175 other");
        var made = new List<Item>();
        var mobs = new List<Mobile> { pm, other };
        try
        {
            double BaseOf(Action<Orc> setup, Mobile killer, Mobile clearer)
            {
                var orc = new Orc();
                mobs.Add(orc);
                setup(orc);
                var state = $"controlled {orc.Controlled}, master {orc.GetMaster()?.Name}, summoned {orc.Summoned}, fame {orc.Fame}";
                var c = KillBy(orc, killer);
                _out.WriteLine($"{state} -> after the kill: controlled {orc.Controlled}, master {orc.GetMaster()?.Name}, recorded {ClusterFCorpseValue.RecordedBase(c)}");
                made.Add(c);
                return ClusterFCorpseValue.BaseFor(c, clearer);
            }

            Assert.Equal(1.5, BaseOf(_ => { }, pm, pm), 9); // the control: a wild orc pm killed

            Assert.Equal(0.0, BaseOf(o => o.Summoned = true, pm, pm));
            // Tamed as AnimalTaming tames: the tamer in Owners, then control (an unbonded pet's control is released
            // before the death event fires; Owners is what still says it was a pet).
            Assert.Equal(0.0, BaseOf(o => { o.Owners.Add(other); o.Controlled = true; o.ControlMaster = other; }, pm, pm));
            Assert.Equal(0.0, BaseOf(o => { o.Owners.Add(other); o.Controlled = true; o.ControlMaster = other; o.IsBonded = true; }, pm, pm));
            Assert.Equal(0.0, BaseOf(o => o.Fame = 0, pm, pm));
            Assert.Equal(0.0, BaseOf(_ => { }, other, pm)); // another player earned it

            var unrecorded = new Corpse(new Orc(), []);
            made.Add(unrecorded);
            Assert.Equal(0.0, ClusterFCorpseValue.BaseFor(unrecorded, pm));

            // A party member of the killer earns it.
            var party = new Party(other);
            other.Party = party;
            party.Members.Add(new PartyMemberInfo(pm));
            pm.Party = party;
            Assert.Equal(1.5, BaseOf(_ => { }, other, pm), 9);
        }
        finally
        {
            pm.Party = null;
            other.Party = null;
            foreach (var i in made)
            {
                i.Delete();
            }

            foreach (var m in mobs)
            {
                m.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void CorpsePointsCountTowardTheLifetimeRank()
    {
        var account = new Account($"p175{Guid.NewGuid():N}"[..16], "p175-test-only");
        var pm = Player("p175 ranker");
        account[0] = pm;
        var dragon = new Dragon();
        Corpse corpse = null;
        try
        {
            corpse = KillBy(dragon, pm);
            var before = CleanUpBritanniaData.Instance.GetLifetimePoints(pm);
            var points = ClusterFCustodianSystem.ComputePoints(corpse, pm);
            ClusterFCustodianSystem.AwardPoints(pm, points);
            Assert.True(points >= 15.0);
            Assert.Equal(before + points, CleanUpBritanniaData.Instance.GetLifetimePoints(pm), 6);
        }
        finally
        {
            corpse?.Delete();
            dragon.Delete();
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
