// WispOrbPossessionVerification.cs
//
// cc-P42 Parts I and K (bug-list D52 and D54, found by Chase in P39 step 7).
//
// I (D52). Possessing a Despise creature you are fighting left you attacking it. The possess path (WispOrb
// InternalTarget, a line-for-line port of ServUO's) relies on SetControlMaster ending the fight: ServUO's removes the
// aggressor and aggressed entries both ways and clears both combatants (pub57 BaseCreature.cs:6664-6673); pinned's does
// none of it (BaseCreature.cs:3748-3795). So the player kept the creature as Combatant, and the creature's defensive AI
// took the player as the aggressor near its anchor (DespiseAIHelper.FindAggressorNearAnchor: m.Combatant == creature).
//
// K (D54). Logging out in Despise destroyed the Wisp Orb. Internalizing the owner on logout exits the Despise region,
// and DespiseController.OnLeaveDespise dissolved the orb for any owner no longer in a Despise region, the internal map
// included, although OnLogin is written to bring the player back with their creature.
//
// Facts:
//   1. A player fighting a Despise creature possesses it through the orb's own target: afterwards neither has the
//      other as Combatant, no aggressor or aggressed entry joins them, and the creature's AI does not pick the player
//      as its aggressor.
//   2. The owner internalized on logout (as Mobile.Logout does) keeps the orb and the link; logging back in at the
//      logout location keeps them; walking out of the dungeon still dissolves the orb and frees the creature.
//
// cc-P46 Part H (bug-list D58, Chase after P42 on the test shard): every time the possessed creature started a fight
// with the opposing creatures, its master attacked it, and its death cost the master karma. Found by a probe on the
// timer wheel (shard-migration notes/cc-P46-smith-orders-2.md, Part H): the AI's "ControlOrder = OrderType.Attack",
// ported from ServUO, runs pinned's IssueAttack, which sets the creature's Combatant to ControlTarget, still the
// master while following; that harmful act made the master's Combatant the creature (Mobile.AggressiveAction). And
// pinned's NoKillAwards is not serialized, so a Despise creature loaded from a save paid karma to whoever hit it.
//   3. A possessed melee, mage or archer creature fights a melee or mage foe for 30 seconds of AI: it engages the foe,
//      its master's Combatant is never the creature, its Combatant is never its master or the master's party member,
//      it does nothing harmful to either, and no aggressor entry joins it and its master.
//   4. A possessed creature cannot harm its master or the master's party (CanBeHarmful), and its Combatant cannot be
//      set to its master.
//   5. A Despise creature written to a save and read back still gives no kill awards.
//   6. A master who did most of the damage to such a reloaded creature loses no karma or fame when a foe kills it.

using System;
using System.Linq;
using Server;
using Server.Engines.Despise;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class WispOrbPossessionVerification
{
    private readonly ITestOutputHelper _out;

    public WispOrbPossessionVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.Karma = 5000;
        pm.MoveToWorld(loc, Map.Trammel);
        return pm;
    }

    private static bool Joined(Mobile a, Mobile b) =>
        a.Aggressors.Any(e => e.Attacker == b || e.Defender == b) ||
        a.Aggressed.Any(e => e.Attacker == b || e.Defender == b);

    // The orb's own target, as a double-click and a click on the creature give it.
    private static void Possess(PlayerMobile pm, WispOrb orb, DespiseCreature creature)
    {
        orb.OnDoubleClick(pm);
        Assert.NotNull(pm.Target);
        pm.Target.Invoke(pm, creature);
    }

    // ---------------------------------------------------------------- 1 (Part I)

    [Fact]
    public void PossessingTheCreatureYouFightEndsTheFightBothWays()
    {
        var pm = NewPlayer(new Point3D(5400, 560, 0));
        var orb = new WispOrb(pm, Alignment.Good);
        pm.Backpack.DropItem(orb);

        var silenii = new Silenii(3);
        silenii.MoveToWorld(new Point3D(5401, 560, 0), Map.Trammel);

        try
        {
            // The fight: each harms the other and each has the other as Combatant.
            pm.DoHarmful(silenii);
            silenii.DoHarmful(pm);
            pm.Warmode = true;
            pm.Combatant = silenii;
            silenii.Combatant = pm;
            Assert.True(Joined(pm, silenii));

            Possess(pm, orb, silenii);

            Assert.Same(silenii, orb.Pet);
            Assert.Same(pm, silenii.ControlMaster);
            _out.WriteLine($"after possession: player combatant {pm.Combatant}, creature combatant {silenii.Combatant}");

            Assert.NotSame(silenii, pm.Combatant);
            Assert.Null(silenii.Combatant);
            Assert.False(Joined(pm, silenii), "an aggressor or aggressed entry still joins the player and the creature");
            Assert.False(Joined(silenii, pm), "an aggressor or aggressed entry still joins the creature and the player");
            Assert.NotSame(pm, DespiseAIHelper.FindAggressorNearAnchor(silenii));
        }
        finally
        {
            orb.Delete();
            silenii.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2 (Part K)

    [Fact]
    public void LoggingOutInDespiseKeepsTheOrbAndWalkingOutStillDissolvesIt()
    {
        ShardTestClock.Arm();

        var ownController = DespiseController.Instance == null;
        var controller = DespiseController.Instance ?? new DespiseController();
        if (ownController)
        {
            controller.MoveToWorld(new Point3D(5571, 626, 30), Map.Trammel);
        }

        var inside = new Point3D(5400, 560, 0); // Despise Good
        Assert.IsType<DespiseRegion>(Region.Find(inside, Map.Trammel));

        var pm = NewPlayer(inside);
        var orb = new WispOrb(pm, Alignment.Good);
        pm.Backpack.DropItem(orb);
        var silenii = new Silenii(3);
        silenii.MoveToWorld(new Point3D(5401, 560, 0), Map.Trammel);

        try
        {
            Possess(pm, orb, silenii);
            Assert.Same(silenii, orb.Pet);

            // Logout, as Mobile.Logout does it (Mobile.cs:9056-9066).
            pm.LogoutLocation = pm.Location;
            pm.LogoutMap = pm.Map;
            pm.Internalize();
            ShardTestClock.AdvanceSeconds(1);

            Assert.False(orb.Deleted, "the orb was destroyed when its owner logged out");
            Assert.Same(silenii, orb.Pet);
            Assert.Same(orb, silenii.Orb);
            Assert.Same(pm, silenii.ControlMaster);

            // Login: back where they logged out, then the controller's login handler.
            pm.MoveToWorld(pm.LogoutLocation, pm.LogoutMap);
            DespiseController.OnLogin(pm);
            ShardTestClock.AdvanceSeconds(1);

            Assert.False(orb.Deleted);
            Assert.Same(silenii, orb.Pet);
            Assert.Same(pm, silenii.ControlMaster);

            // Walking out still dissolves it.
            pm.MoveToWorld(new Point3D(1500, 1500, 0), Map.Trammel);
            Assert.IsNotType<DespiseRegion>(Region.Find(pm.Location, Map.Trammel));
            ShardTestClock.AdvanceSeconds(1);

            Assert.True(orb.Deleted, "walking out of Despise must still dissolve the orb");
            Assert.Null(silenii.Orb);
            Assert.False(silenii.Controlled);
        }
        finally
        {
            orb.Delete();
            silenii.Delete();
            pm.Delete();
            if (ownController)
            {
                controller.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 3 (cc-P46 Part H, D58)

    // A master online (a NetState wakes the sector, so the AI runs) in war mode, possessing a good creature beside them,
    // a wild evil one three tiles away, and a party member beside the master. Runs the AI on the timer wheel.
    private sealed class Fight : IDisposable
    {
        public readonly PlayerMobile Master;
        public readonly PlayerMobile Friend;
        public readonly WispOrb Orb;
        public readonly DespiseCreature Pet;
        public readonly DespiseCreature Foe;
        private readonly Server.Network.NetState _ns;
        private readonly Server.Accounting.Account _account;

        public Fight(DespiseCreature pet, DespiseCreature foe)
        {
            ShardTestClock.Arm();
            ShardTestHost.EnsureAccounts();
            ShardTestHost.EnsureSkillChecks();

            var at = new Point3D(5400, 560, 0); // Despise Good
            Master = NewPlayer(at);
            _account = new Server.Accounting.Account($"p46h{Guid.NewGuid():N}"[..16], "p46-test-only");
            _ns = Server.Tests.Network.PacketTestUtilities.CreateTestNetState();
            _ns.Account = _account;
            Master.NetState = _ns;
            _ns.Mobile = Master;
            Master.Warmode = true;

            Friend = NewPlayer(new Point3D(5399, 560, 0));
            var party = new Server.Engines.PartySystem.Party(Master);
            Master.Party = party;
            party.Add(Friend);

            Orb = new WispOrb(Master, Alignment.Good);
            Master.Backpack.DropItem(Orb);
            Pet = pet;
            Pet.MoveToWorld(new Point3D(5401, 560, 0), Map.Trammel);
            Possess(Master, Orb, Pet);
            Assert.Same(Pet, Orb.Pet);

            Foe = foe;
            Foe.MoveToWorld(new Point3D(5404, 560, 0), Map.Trammel);
        }

        public void Dispose()
        {
            Master.NetState = null;
            _ns.Mobile = null;
            Server.Accounting.Accounts.Remove(_account);
            Orb.Delete();
            Pet.Delete();
            Foe.Delete();
            Friend.Delete();
            Master.Delete();
        }
    }

    public static System.Collections.Generic.IEnumerable<object[]> Pairs()
    {
        // A melee, a mage and an archer creature possessed; a melee and a mage foe.
        foreach (var pet in new[] { "Silenii", "ForestNymph", "Sagittarri" })
        {
            foreach (var foe in new[] { "Phantom", "Naba" })
            {
                yield return new object[] { pet, foe };
            }
        }
    }

    private static DespiseCreature Make(string name) =>
        (DespiseCreature)Activator.CreateInstance(typeof(DespiseCreature).Assembly.GetType($"Server.Engines.Despise.{name}"), 5);

    [Theory]
    [MemberData(nameof(Pairs))]
    public void APossessedCreatureFightingNeverBecomesItsMastersCombatant(string petName, string foeName)
    {
        using var f = new Fight(Make(petName), Make(foeName));

        var harmedOwn = new System.Collections.Generic.List<string>();
        void Watch(AggressiveActionEventArgs e)
        {
            if (e.Aggressor == f.Pet && (e.Aggressed == f.Master || e.Aggressed == f.Friend))
            {
                harmedOwn.Add($"{petName} -> {e.Aggressed.Name ?? (e.Aggressed == f.Master ? "master" : "party member")}");
            }
        }

        EventSink.AggressiveAction += Watch;
        var masterOnPet = 0;
        var petOnMaster = 0;
        var engaged = false;
        try
        {
            for (var i = 0; i < 120; i++) // 30 seconds
            {
                ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
                masterOnPet += f.Master.Combatant == f.Pet ? 1 : 0;
                petOnMaster += f.Pet.Combatant == f.Master || f.Pet.Combatant == f.Friend ? 1 : 0;
                engaged |= f.Pet.Combatant == f.Foe;
            }
        }
        finally
        {
            EventSink.AggressiveAction -= Watch;
        }

        _out.WriteLine($"{petName} vs {foeName}: engaged the foe {engaged}; ticks with the master on the creature " +
                       $"{masterOnPet}, the creature on its master or party {petOnMaster}; harmful acts on its own: " +
                       $"{harmedOwn.Count}");
        // The master's side first, so a red names what went wrong; then that the fight really happened.
        Assert.Empty(harmedOwn);
        Assert.Equal(0, masterOnPet);
        Assert.Equal(0, petOnMaster);
        Assert.False(Joined(f.Master, f.Pet));
        Assert.True(engaged, "the possessed creature never took the foe as its Combatant: the fight did not start");
    }

    // ---------------------------------------------------------------- 4 (cc-P46 Part H)

    [Fact]
    public void APossessedCreatureCannotHarmItsMasterOrTheirParty()
    {
        using var f = new Fight(new Silenii(5), new Phantom(5));

        Assert.False(f.Pet.CanBeHarmful(f.Master, false));
        Assert.False(f.Pet.CanBeHarmful(f.Friend, false));
        Assert.True(f.Pet.CanBeHarmful(f.Foe, false));

        // Pinned's Combatant setter refuses a target CanBeHarmful refuses (Mobile.cs:737-743).
        f.Pet.Combatant = f.Master;
        Assert.NotSame(f.Master, f.Pet.Combatant);
        Assert.Null(f.Master.Combatant);
    }

    // ---------------------------------------------------------------- 5 (cc-P46 Part H)

    // A Despise creature written to a save and read back, as a world load does: the Serial constructor, then Deserialize.
    private static Silenii Reloaded(Silenii original)
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = new Silenii(original.Serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void ADespiseCreatureLoadedFromASaveStillGivesNoKillAwards()
    {
        var original = new Silenii(5);
        try
        {
            Assert.True(original.NoKillAwards);
            var copy = Reloaded(original);
            _out.WriteLine($"constructed: NoKillAwards {original.NoKillAwards}; after a save and load: {copy.NoKillAwards}");
            Assert.True(copy.NoKillAwards, "a Despise creature loaded from a save pays fame and karma on death");
        }
        finally
        {
            original.Delete();
        }
    }

    // ---------------------------------------------------------------- 6 (cc-P46 Part H)

    [Fact]
    public void AMasterWhoHitTheirCreatureLosesNoKarmaWhenItDies()
    {
        var spot = new Point3D(1500, 1520, 0); // outside Despise: only the kill awards are under test
        var master = NewPlayer(spot);
        var foe = new Phantom(5);
        foe.MoveToWorld(new Point3D(spot.X + 2, spot.Y, 0), Map.Trammel);

        // The creature as a world load leaves it: written, deleted, read back into the world.
        var original = new Silenii(5);
        var copy = Reloaded(original);
        original.Delete();
        World.AddEntity(copy);
        copy.MoveToWorld(new Point3D(spot.X + 1, spot.Y, 0), Map.Trammel);

        try
        {
            Assert.True(copy.Karma > 0, "a good creature");
            var karma = master.Karma;
            var fame = master.Fame;

            copy.Damage(copy.Hits * 3 / 4, master); // the master's swings, most of the damage
            copy.Damage(copy.Hits + 100, foe);      // the opposing creature finishes it

            _out.WriteLine($"creature karma {copy.Karma}: master karma {karma} -> {master.Karma}, fame {fame} -> {master.Fame}");
            Assert.False(copy.Alive);
            Assert.Equal(karma, master.Karma);
            Assert.Equal(fame, master.Fame);
        }
        finally
        {
            copy.Delete();
            foe.Delete();
            master.Delete();
        }
    }
}
