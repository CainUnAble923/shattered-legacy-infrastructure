// GuardWitnessVerification.cs
//
// cc-P628, guards that patrol and see, stage 1: the incident/witness contract (customizations ClusterFGuardWitness) and
// its observe-only calls from GuardedRegion (server/patches/GuardedRegion-witness-observe.patch).
// Design: shard-migration notes/cc-P608-guards-patrol-design.md; notes: notes/cc-P628-build-guard-patrols-pilot.md.
//
//   1. Sight: alive, player access, same map, within 14 tiles, CanSee (hidden is unseen) and line of sight (a stone wall
//      blocks it), to the actor and to the action's place.
//   2. A crime in a pilot town is recorded at the event through the real paths (CriminalAction, AggressiveAction), with
//      exactly the guards, townspeople and players who saw it (a hidden onlooker included); outside a pilot town, or
//      switched off, nothing.
//   3. A "guards" call through real speech reports only what the caller saw in the last 15 seconds (else an
//      investigation with no suspect); it is heard by guards within 15 tiles spoken, 18 yelled, 1 whispered, around a
//      wall too; the stock guard response still runs.
//   4. A jurisdiction is named regions on one facet: a child region counts, the same name on another facet does not.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuardWitnessVerification : IDisposable
{
    // Two test towns on Trammel's empty south-west: the pilot and an ordinary guarded town.
    private static readonly Point3D Spot = new(300, 3300, 0);
    private static readonly Point3D Elsewhere = new(500, 3300, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = new();
    private readonly List<Region> _regions = new();
    private IDisposable _tiles;

    public GuardWitnessVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestClock.Arm();
        ClusterFGuardWitness.ResetForTests();
        ClusterFGuardWitness.Jurisdictions.Clear();
        ClusterFGuardWitness.Jurisdictions.Add(new GuardJurisdiction("p628-pilot", Map.Trammel, ["P628 Pilot"]));
        ClusterFGuardWitness.Enabled = true;

        Town("P628 Pilot", Spot);
        Town("P628 Other", Elsewhere);
    }

    public void Dispose()
    {
        foreach (var g in GuardsNear(Spot, 40))
        {
            g.Delete(); // and any the stock response made
        }

        foreach (var g in GuardsNear(Elsewhere, 40))
        {
            g.Delete();
        }

        foreach (var e in _made)
        {
            e.Delete();
        }

        foreach (var r in _regions)
        {
            r.Unregister();
        }

        _tiles?.Dispose();
        ClusterFGuardWitness.ResetForTests();
    }

    private static List<BaseGuard> GuardsNear(Point3D p, int range)
    {
        var list = new List<BaseGuard>();
        foreach (var g in Map.Trammel.GetMobilesInRange<BaseGuard>(p, range))
        {
            list.Add(g);
        }

        return list;
    }

    private void Town(string name, Point3D centre)
    {
        var r = new TownRegion(name, Map.Trammel, 50,
            new Rectangle3D(new Point3D(centre.X - 40, centre.Y - 40, -128), new Point3D(centre.X + 40, centre.Y + 40, 127)));
        r.Register();
        _regions.Add(r);
    }

    private static Point3D At(int dx, int dy = 0) => new(Spot.X + dx, Spot.Y + dy, Spot.Z);

    private PlayerMobile Player(string name, Point3D at, bool hidden = false)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = name };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Trammel);
        pm.Hidden = hidden;
        _made.Add(pm);
        return pm;
    }

    private WarriorGuard Guard(Point3D at)
    {
        var g = new WarriorGuard();
        g.MoveToWorld(at, Map.Trammel);
        _made.Add(g);
        return g;
    }

    private Item Wall(Point3D at)
    {
        _tiles ??= TestTileRows.Seed(TileRows.StoneWall);
        var wall = new Item(0x0080) { Movable = false };
        wall.MoveToWorld(at, Map.Trammel);
        _made.Add(wall);
        return wall;
    }

    private static void SayGuards(Mobile m, MessageType type = MessageType.Regular) =>
        m.DoSpeech("Guards!", [0x0007], type, 0);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void SightNeedsRangeLineOfSightAndAVisibleActor()
    {
        var actor = Player("Actor", Spot);
        Assert.Equal("P628 Pilot", actor.Region.Name);

        Assert.True(ClusterFGuardWitness.Sees(Player("Near", At(10)), actor, actor.Location));
        Assert.True(ClusterFGuardWitness.Sees(Player("Edge", At(0, 14)), actor, actor.Location));
        Assert.False(ClusterFGuardWitness.Sees(Player("Far", At(15)), actor, actor.Location));

        var dead = Player("Dead", At(3));
        dead.Kill();
        Assert.False(ClusterFGuardWitness.Sees(dead, actor, actor.Location));

        var staff = Player("Staff", At(-3));
        staff.AccessLevel = AccessLevel.GameMaster;
        Assert.False(ClusterFGuardWitness.Sees(staff, actor, actor.Location)); // staff sight is never a guard's

        // A stone wall between them.
        var behind = Player("Behind", At(-6, 5));
        Assert.True(ClusterFGuardWitness.Sees(behind, actor, actor.Location));
        Wall(At(-3, 3));
        Wall(At(-3, 2));
        Wall(At(-2, 3));
        Assert.False(behind.InLOS(actor));
        Assert.False(ClusterFGuardWitness.Sees(behind, actor, actor.Location));

        // The action's place counts too: the actor in sight, the spot it acted on behind the wall.
        var watcher = Player("Watcher", At(-6, 0));
        Assert.True(ClusterFGuardWitness.Sees(watcher, actor, actor.Location));
        Assert.False(watcher.InLOS(At(0, 6)));
        Assert.False(ClusterFGuardWitness.Sees(watcher, actor, At(0, 6)));

        // Hidden at the event: unseen, by anyone.
        actor.Hidden = true;
        Assert.False(ClusterFGuardWitness.Sees(Player("Close", At(1)), actor, actor.Location));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ACrimeIsRecordedAtTheEventWithExactlyItsWitnesses()
    {
        var culprit = Player("Culprit", Spot);
        var seer = Player("Seer", At(5));
        var hidden = Player("Lurker", At(3), hidden: true);
        var far = Player("Far", At(16));
        var behind = Player("Behind", At(-6, 5));
        Wall(At(-3, 3));
        Wall(At(-3, 2));
        Wall(At(-2, 3));
        var shopkeeper = new Provisioner();
        shopkeeper.MoveToWorld(At(0, 4), Map.Trammel);
        _made.Add(shopkeeper);
        var guard = Guard(At(0, -6));

        culprit.CriminalAction(true); // the real path: Mobile.CriminalAction -> GuardedRegion.OnCriminalAction

        var incident = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        _out.WriteLine(string.Join(", ", incident.Witnesses.Select(w => $"{w.Mobile.Name} {w.Role}")));
        Assert.Equal(IncidentKind.CriminalAction, incident.Kind);
        Assert.Same(culprit, incident.Culprit);
        Assert.Equal(Spot, incident.Location);
        Assert.Equal(
            new Mobile[] { seer, hidden, shopkeeper, guard }.OrderBy(m => m.Serial.Value),
            incident.Witnesses.Select(w => w.Mobile).OrderBy(m => m.Serial.Value)
        );
        Assert.Equal(WitnessRole.Player, incident.Witnesses.Single(w => w.Mobile == seer).Role);
        Assert.Equal(WitnessRole.Civilian, incident.Witnesses.Single(w => w.Mobile == shopkeeper).Role);
        Assert.Equal(WitnessRole.Guard, incident.Witnesses.Single(w => w.Mobile == guard).Role);
        Assert.True(incident.SeenByGuard);
        Assert.Equal(WitnessRole.Player, incident.Witnesses.Single(w => w.Mobile == hidden).Role); // a hidden witness still saw it
        Assert.DoesNotContain(incident.Witnesses, w => w.Mobile == far || w.Mobile == behind);

        // Event time: hiding and running afterwards changes nothing recorded.
        culprit.Hidden = true;
        culprit.MoveToWorld(At(20), Map.Trammel);
        Assert.Equal(4, incident.Witnesses.Count);
        Assert.Equal(Spot, incident.Witnesses[0].LastSeen);

        // The same culprit's attack a moment later is the same incident, now an aggression with a victim.
        culprit.Hidden = false;
        culprit.MoveToWorld(Spot, Map.Trammel);
        seer.AggressiveAction(culprit, true); // the real path: Mobile.AggressiveAction -> GuardedRegion.OnAggressed
        incident = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        Assert.Equal(IncidentKind.Aggression, incident.Kind);
        Assert.Same(seer, incident.Victim);

        // In an ordinary guarded town, or switched off: nothing is recorded.
        var other = Player("Other", Elsewhere);
        Player("OtherSeer", new Point3D(Elsewhere.X + 3, Elsewhere.Y, 0));
        other.CriminalAction(true);
        Assert.Empty(ClusterFGuardWitness.IncidentsIn("p628-other"));
        ClusterFGuardWitness.Enabled = false;
        ShardTestClock.AdvanceSeconds(2.0);
        culprit.CriminalAction(true);
        Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ACallReportsOnlyWhatTheCallerSawAndIsHeardInSpeechRange()
    {
        var culprit = Player("Culprit", Spot);
        var seer = Player("Seer", At(5));
        var blind = Player("Blind", At(3), hidden: false);
        Wall(At(1, 2));
        Wall(At(2, 2));
        Wall(At(3, 2));
        blind.MoveToWorld(At(2, 4), Map.Trammel); // behind the wall from the culprit
        var nearGuard = Guard(At(5, -10));        // 10 from the seer
        var cornerGuard = Guard(At(5, 14));        // 14 from the seer, out of the culprit's sight range
        var yellGuard = Guard(At(22));             // 17 from the seer
        culprit.CriminalAction(true);
        var incident = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        Assert.False(incident.SeenBy(blind));

        // The witness calls: their incident, heard by the guards within 15 tiles (around corners too), not at 17.
        SayGuards(seer);
        var report = ClusterFGuardWitness.Reports.Last();
        Assert.Same(seer, report.Caller);
        Assert.Same(incident, report.Incident);
        Assert.Contains(nearGuard, report.HeardBy);
        Assert.Contains(cornerGuard, report.HeardBy);
        Assert.DoesNotContain(yellGuard, report.HeardBy);

        // The stock response still runs (observe only): an idle guard took the culprit as its focus.
        Assert.Contains(GuardsNear(Spot, 15), g => g.Focus == culprit);

        // Two seconds between one caller's calls; a yell then carries 18.
        SayGuards(seer, MessageType.Yell);
        Assert.Single(ClusterFGuardWitness.Reports, r => r.Caller == seer);
        ShardTestClock.AdvanceSeconds(2.1);
        SayGuards(seer, MessageType.Yell);
        Assert.Contains(yellGuard, ClusterFGuardWitness.Reports.Last().HeardBy);

        // A whisper carries one tile.
        ShardTestClock.AdvanceSeconds(2.1);
        SayGuards(seer, MessageType.Whisper);
        Assert.Empty(ClusterFGuardWitness.Reports.Last().HeardBy);

        // Someone who did not see it raises an investigation, with no suspect.
        SayGuards(blind);
        var investigation = ClusterFGuardWitness.Reports.Last();
        Assert.Same(blind, investigation.Caller);
        Assert.Null(investigation.Incident);

        // After 15 seconds even the witness's report is stale.
        ShardTestClock.AdvanceSeconds(16.0);
        SayGuards(seer);
        Assert.Null(ClusterFGuardWitness.Reports.Last().Incident);

        // The dead call nobody.
        var count = ClusterFGuardWitness.Reports.Count;
        var ghost = Player("Ghost", At(6));
        ghost.Kill();
        SayGuards(ghost);
        Assert.Equal(count, ClusterFGuardWitness.Reports.Count);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AJurisdictionIsNamedRegionsOnOneFacet()
    {
        ClusterFGuardWitness.ResetForTests();
        var cove = Assert.Single(ClusterFGuardWitness.Jurisdictions);
        Assert.Equal("cove-trammel", cove.Id);
        Assert.Same(Map.Trammel, cove.Map);

        var tram = new TownRegion("Cove", Map.Trammel, 50, new Rectangle3D(new Point3D(10, 10, -128), new Point3D(20, 20, 127)));
        var child = new GuardedRegion("Cove Inn", Map.Trammel, tram, new Rectangle3D(new Point3D(12, 12, -128), new Point3D(14, 14, 127)));
        var fel = new TownRegion("Cove", Map.Felucca, 50, new Rectangle3D(new Point3D(10, 10, -128), new Point3D(20, 20, 127)));
        var britain = new TownRegion("Britain", Map.Trammel, 50, new Rectangle3D(new Point3D(30, 30, -128), new Point3D(40, 40, 127)));

        Assert.True(cove.Covers(tram));
        Assert.True(cove.Covers(child));
        Assert.False(cove.Covers(fel));
        Assert.False(cove.Covers(britain));
        Assert.Same(cove, ClusterFGuardWitness.JurisdictionOf(child));
        Assert.Null(ClusterFGuardWitness.JurisdictionOf(fel));
    }
}
