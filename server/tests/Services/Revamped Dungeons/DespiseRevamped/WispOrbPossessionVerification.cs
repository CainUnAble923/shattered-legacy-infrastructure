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
}
