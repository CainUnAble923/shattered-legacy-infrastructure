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
//   cc-P647 (the P628, P635 and P640 reviews): observation runs only on the test shard; one action's callbacks are one
//   incident and a distinct offense another; a witness must see the victim's place; a pet's crime names its owner with no
//   second incident; acts stock guards reject are not reportable; the caller limit keeps only recent callers. cc-P659 (the
//   P657 review): a finished act is never reopened by a later one in the same moment and place. The fixture
//   installs pinned's notoriety rules so real harmful and beneficial acts are crimes.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
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
    private readonly Func<bool> _testShard = ClusterFGuardWitness.IsTestShard;

    // Pinned's notoriety rules (the test host runs without them), so a real harmful or beneficial act is a crime; restored after.
    private readonly NotorietyHandler _notoriety = Notoriety.Handler;
    private readonly int[] _hues = (int[])Notoriety.Hues.Clone();
    private readonly AllowHarmfulHandler _harmful = Mobile.AllowHarmfulHandler;
    private readonly AllowBeneficialHandler _beneficial = Mobile.AllowBeneficialHandler;

    public GuardWitnessVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestClock.Arm();
        Server.Misc.NotorietyHandlers.Initialize();
        ClusterFGuardWitness.ResetForTests();
        ClusterFGuardWitness.IsTestShard = () => true; // cc-P647: observation runs only on the test shard
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
        ClusterFGuardWitness.IsTestShard = _testShard;
        Notoriety.Handler = _notoriety;
        Array.Copy(_hues, Notoriety.Hues, _hues.Length);
        Mobile.AllowHarmfulHandler = _harmful;
        Mobile.AllowBeneficialHandler = _beneficial;
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

        // The same culprit's attack later is another offense (cc-P647). Its two callbacks, the criminal flag and then the
        // aggression (Mobile.DoHarmful -> GuardedRegion.OnCriminalAction, OnAggressed), are one incident with a victim.
        culprit.Hidden = false;
        culprit.MoveToWorld(Spot, Map.Trammel);
        ShardTestClock.AdvanceSeconds(2.0);
        culprit.DoHarmful(seer);
        Assert.Equal(2, ClusterFGuardWitness.IncidentsIn("p628-pilot").Count);
        var attack = ClusterFGuardWitness.IncidentsIn("p628-pilot")[^1];
        Assert.Equal(IncidentKind.Aggression, attack.Kind);
        Assert.Same(seer, attack.Victim);

        // In an ordinary guarded town, or switched off: nothing is recorded.
        var other = Player("Other", Elsewhere);
        Player("OtherSeer", new Point3D(Elsewhere.X + 3, Elsewhere.Y, 0));
        other.CriminalAction(true);
        Assert.Empty(ClusterFGuardWitness.IncidentsIn("p628-other"));
        ClusterFGuardWitness.Enabled = false;
        ShardTestClock.AdvanceSeconds(2.0);
        culprit.CriminalAction(true);
        Assert.Equal(2, ClusterFGuardWitness.IncidentsIn("p628-pilot").Count);
    }

    // cc-P647 (P628 and P635 reviews): the callbacks of one action are one incident; a second offense by the same culprit
    // in the same moment, on another victim at another place, is its own incident with its own witnesses.
    [Fact]
    public void OneActionIsOneIncidentAndADistinctOffenseIsAnother()
    {
        var culprit = Player("Culprit", Spot);
        var first = Player("First", At(1));
        var second = Player("Second", At(21));
        var late = Player("Late", At(32)); // 32 from the first place, 12 from the second

        culprit.DoHarmful(first); // the criminal flag, then the aggression: one action
        var one = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        Assert.Equal(IncidentKind.Aggression, one.Kind);
        Assert.Same(first, one.Victim);
        Assert.False(one.SeenBy(late));

        // In the same moment the culprit is across the street, on someone else.
        culprit.MoveToWorld(At(20), Map.Trammel);
        culprit.DoHarmful(second);
        var incidents = ClusterFGuardWitness.IncidentsIn("p628-pilot");
        _out.WriteLine(string.Join("; ", incidents.Select(i => $"#{i.Id} {i.Kind} {i.Victim?.Name} at {i.Location}: " +
                                                            string.Join(", ", i.Witnesses.Select(w => w.Mobile.Name)))));
        Assert.Equal(2, incidents.Count);
        Assert.Same(first, incidents[0].Victim);
        Assert.Equal(Spot, incidents[0].Location);
        Assert.False(incidents[0].SeenBy(late));
        Assert.Same(second, incidents[1].Victim);
        Assert.Equal(At(20), incidents[1].Location);
        Assert.True(incidents[1].SeenBy(late));
        Assert.Equal(At(20), incidents[1].Witnesses.Single(w => w.Mobile == late).LastSeen);
    }

    // cc-P659 (P657 review): a finished act is not reopened by the culprit's next one, even in the same server moment at the
    // same spot. A separate victimless crime is its own incident, with its own place and the witnesses who saw that one
    // (a newcomer is not added to the attack they missed); a second attack on the same victim is a third incident.
    [Fact]
    public void ALaterActInTheSameMomentAndPlaceIsItsOwnIncident()
    {
        var culprit = Player("Culprit", Spot);
        var victim = Player("Victim", At(1));

        culprit.DoHarmful(victim); // the criminal flag, then the aggression: one action
        var attack = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        Assert.Equal(IncidentKind.Aggression, attack.Kind);
        Assert.Same(victim, attack.Victim);

        // Neither the clock nor the culprit moves; someone steps into view, then the culprit commits a victimless crime.
        var newcomer = Player("Newcomer", At(-3));
        culprit.CriminalAction(true);
        var incidents = ClusterFGuardWitness.IncidentsIn("p628-pilot");
        _out.WriteLine(string.Join("; ", incidents.Select(i => $"#{i.Id} {i.Kind} {i.Victim?.Name} at {i.ActionAt}: " +
                                                            string.Join(", ", i.Witnesses.Select(w => w.Mobile.Name)))));
        Assert.Equal(2, incidents.Count);
        Assert.Same(attack, incidents[0]);
        Assert.Equal(IncidentKind.Aggression, attack.Kind);
        Assert.Same(victim, attack.Victim);
        Assert.Equal(At(1), attack.ActionAt);
        Assert.False(attack.SeenBy(newcomer));
        Assert.Equal(IncidentKind.CriminalAction, incidents[1].Kind);
        Assert.Null(incidents[1].Victim);
        Assert.Equal(Spot, incidents[1].ActionAt);
        Assert.True(incidents[1].SeenBy(newcomer));

        // The same victim again, still in the same moment: another attack, another incident.
        culprit.DoHarmful(victim);
        incidents = ClusterFGuardWitness.IncidentsIn("p628-pilot");
        Assert.Equal(3, incidents.Count);
        Assert.Equal(IncidentKind.CriminalAction, incidents[1].Kind);
        Assert.Null(incidents[1].Victim);
        Assert.Equal(IncidentKind.Aggression, incidents[2].Kind);
        Assert.Same(victim, incidents[2].Victim);
        Assert.True(incidents[2].SeenBy(newcomer));
    }

    // cc-P647 (P628 and P635 reviews): a witness must see the place of the action too. Around a corner, someone who sees
    // the attacker but not the victim, or the helper but not the criminal they heal, did not witness it.
    [Fact]
    public void AWitnessMustSeeTheVictimOfAnAttackOrAssist()
    {
        var culprit = Player("Culprit", Spot);
        var victim = Player("Victim", At(0, 6));
        var watcher = Player("Watcher", At(-6, 0));
        var clear = Player("Clear", At(2, 3));
        Wall(At(-3, 3));
        Wall(At(-3, 2));
        Wall(At(-2, 3));
        Assert.True(watcher.InLOS(culprit));
        Assert.False(watcher.InLOS(victim));

        culprit.DoHarmful(victim);
        var attack = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        _out.WriteLine($"attack: {string.Join(", ", attack.Witnesses.Select(w => w.Mobile.Name))}");
        Assert.Same(victim, attack.Victim);
        Assert.True(attack.SeenBy(clear));
        Assert.False(attack.SeenBy(watcher));

        // A heal for a criminal: the helper in sight, the one helped around the corner.
        ShardTestClock.AdvanceSeconds(2.0);
        var helper = Player("Helper", Spot);
        culprit.Delete();
        victim.Criminal = true;
        helper.DoBeneficial(victim);
        var assist = ClusterFGuardWitness.IncidentsIn("p628-pilot")[^1];
        _out.WriteLine($"assist: {assist.Kind} {string.Join(", ", assist.Witnesses.Select(w => w.Mobile.Name))}");
        Assert.Equal(IncidentKind.CriminalAssist, assist.Kind);
        Assert.Same(helper, assist.Culprit);
        Assert.True(assist.SeenBy(clear));
        Assert.False(assist.SeenBy(watcher));
    }

    // cc-P647 (P640 review): a pet's crime forwards its owner's criminal flag (and, for an attack, an indirect harmful act).
    // That is not a second witnessed crime at the owner's spot: one incident, the pet's, naming the owner, and only those
    // who saw the owner learn where the owner was.
    [Fact]
    public void APetsCrimeNamesItsOwnerWithoutASecondIncident()
    {
        var owner = Player("Owner", At(30));
        var bystander = Player("Bystander", At(31)); // beside the owner, 30 tiles from the pet's act
        var victim = Player("Victim", At(1));
        var onlooker = Player("Onlooker", At(3, 3));
        var near = Player("Near", At(10)); // sees the pet and, 20 tiles off, not the owner
        var pet = new Dog();
        pet.MoveToWorld(Spot, Map.Trammel);
        _made.Add(pet);
        Assert.True(pet.SetControlMaster(owner));
        Assert.Same(owner, pet.GetMaster());

        pet.DoHarmful(victim);
        var incidents = ClusterFGuardWitness.IncidentsIn("p628-pilot");
        _out.WriteLine(string.Join("; ", incidents.Select(i => $"#{i.Id} {i.Kind} by {i.Culprit?.Name} on {i.Victim?.Name}: " +
                                                            string.Join(", ", i.Witnesses.Select(w => w.Mobile.Name)))));
        var incident = Assert.Single(incidents);
        Assert.Same(pet, incident.Culprit);
        Assert.Same(victim, incident.Victim);
        Assert.Same(owner, incident.ResponsibleMaster);
        Assert.True(incident.SeenBy(onlooker));
        Assert.True(incident.SeenBy(near));
        Assert.False(incident.SeenBy(bystander));
        Assert.All(incident.Witnesses, w => Assert.Null(w.MasterLastSeen)); // nobody here saw the owner

        // A witness who saw the owner as well learns the owner's place.
        ShardTestClock.AdvanceSeconds(2.0);
        owner.MoveToWorld(At(5, -5), Map.Trammel);
        pet.DoHarmful(near);
        var second = ClusterFGuardWitness.IncidentsIn("p628-pilot")[^1];
        Assert.Equal(2, ClusterFGuardWitness.IncidentsIn("p628-pilot").Count);
        Assert.Same(pet, second.Culprit);
        Assert.Equal(At(5, -5), second.Witnesses.Single(w => w.Mobile == onlooker).MasterLastSeen);
        Assert.Null(second.Witnesses.SingleOrDefault(w => w.Mobile == owner)); // the owner is not their own witness
    }

    // cc-P647 (P640 review): an act stock guards would not answer (GuardedRegion.IsGuardCandidate: a blessed or staff
    // mobile, an invulnerable creature, a guard) leaves no incident a witness could report as a suspect.
    [Fact]
    public void ActsStockGuardsRejectAreNotReportable()
    {
        var seer = Player("Seer", At(5));
        var blessed = Player("Blessed", Spot);
        blessed.Blessed = true;
        var vendor = new Healer(); // a wandering healer is always invulnerable
        vendor.MoveToWorld(At(0, 2), Map.Trammel);
        _made.Add(vendor);
        Assert.True(vendor.IsInvulnerable);
        var criminal = Player("Criminal", At(1, 1));
        criminal.Criminal = true;

        blessed.CriminalAction(true);
        vendor.CriminalAction(true);
        blessed.DoBeneficial(criminal); // the assist hook: a heal for a criminal
        Assert.Empty(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        SayGuards(seer);
        Assert.Null(ClusterFGuardWitness.Reports.Last().Incident);

        // The same acts by an ordinary player are recorded and reportable.
        ShardTestClock.AdvanceSeconds(2.1);
        var helper = Player("Helper", At(-1));
        helper.DoBeneficial(criminal);
        var assist = Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
        Assert.Equal(IncidentKind.CriminalAssist, assist.Kind);
        Assert.Same(helper, assist.Culprit);
        SayGuards(seer);
        Assert.Same(assist, ClusterFGuardWitness.Reports.Last().Incident);
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

    // cc-P647 (P628 review): off the test shard neither the command nor the switch turns observation on.
    [Fact]
    public void ObservationRunsOnlyOnTheTestShard()
    {
        ClusterFGuardWitness.Configure(); // registers [GuardWitness
        var admin = Player("Admin", At(30));
        admin.AccessLevel = AccessLevel.Administrator;
        var culprit = Player("Culprit", Spot);
        Player("Seer", At(5));
        ClusterFGuardWitness.Enabled = false;
        ClusterFGuardWitness.IsTestShard = () => false;

        Assert.True(CommandSystem.Handle(admin, $"{CommandSystem.Prefix}GuardWitness on"));
        Assert.False(ClusterFGuardWitness.Enabled);
        ClusterFGuardWitness.Enabled = true; // a script's switch: still inert
        Assert.False(ClusterFGuardWitness.Enabled);
        culprit.CriminalAction(true);
        Assert.Empty(ClusterFGuardWitness.IncidentsIn("p628-pilot"));

        ClusterFGuardWitness.IsTestShard = () => true;
        ShardTestClock.AdvanceSeconds(2.0);
        culprit.CriminalAction(true);
        Assert.Single(ClusterFGuardWitness.IncidentsIn("p628-pilot"));
    }

    // cc-P647 (P640 review): the two-second caller limit keeps only the callers still inside it.
    [Fact]
    public void CallerHistoryKeepsOnlyCallersInsideTheLimit()
    {
        var callers = new[] { Player("A", At(1)), Player("B", At(2)), Player("C", At(3)) };
        foreach (var c in callers)
        {
            SayGuards(c);
        }

        Assert.Equal(3, ClusterFGuardWitness.CallerHistoryCount);
        callers[2].Delete();
        ShardTestClock.AdvanceSeconds(2.1);

        var fresh = Player("Fresh", At(4));
        SayGuards(fresh);
        Assert.Equal(1, ClusterFGuardWitness.CallerHistoryCount);
        var count = ClusterFGuardWitness.Reports.Count;
        SayGuards(fresh); // still limited
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
