// ColorWarsVerification.cs
//
// cc-P863: Color Wars, the Staff Hub Events tab's team event (StaffColorWars.cs, after Omni's Color Wars). Notes in
// shard-migration notes/cc-P863-color-wars.md.
//
// Facts: opening checks the arena, the isle and one at a time, and puts a stone at each team's base; the gate asks first
// and refuses the dead, the Young, the mounted and staff; joining puts everything the player had into a bag in their
// bank, lends the kit in the team's colour and balances the teams; teammates are green and the others orange; nobody
// fights while the teams gather, then only the other team; a death keeps the kit, scores for the killer's team and rises
// at the base; leaving by the stone takes the kit back (a split stack too) and gives everything back; the end pays the
// leading team, a draw pays nobody, and the first to CwKillsToWin ends it at once; nothing is learned in the arena, no
// travel in or out, nobody walks in from outside and a player who walks out is brought back; a logged-out player gets
// their things back where they joined; a run survives a save and an orphaned gate removes itself; the Events tab and
// the command run it; a bad Color Wars setting is refused whole.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.RewardStores;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ColorWarsVerification : IDisposable
{
    private static readonly Point3D GateSpot = new(1475, 1645, 20);   // Britain, off Haven Island
    private static readonly Point3D HavenSpot = new(3460, 2604, 18);  // on Haven Island
    private static readonly Point3D Corner1 = new(5450, 3820, 0);
    private static readonly Point3D Corner2 = new(5469, 3831, 0);
    private static bool _achievements;

    private readonly List<Account> _accounts = [];
    private readonly NotorietyHandler _notoriety = Notoriety.Handler;
    private readonly AllowHarmfulHandler _harmful = Mobile.AllowHarmfulHandler;
    private readonly AllowBeneficialHandler _beneficial = Mobile.AllowBeneficialHandler;

    public ColorWarsVerification()
    {
        ShardTestHost.EnsureAccounts();
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        ShardTestClock.Arm();
        Server.Misc.NotorietyHandlers.Initialize(); // pinned's Trammel rules; the host runs without them
        StaffColorWars.EnsureHooks();
        StaffColorWarsGump.Register();
        StaffEventsSettings.Current = new StaffEventsSettings();
        StaffColorWars.ForgetForTests();
    }

    public void Dispose()
    {
        if (StaffColorWars.Active is { } run)
        {
            run.Gate?.Delete();
            foreach (var s in run.Stones)
            {
                s.Delete();
            }
        }

        StaffColorWars.ForgetForTests();
        StaffEventsSettings.Current = new StaffEventsSettings();
        Notoriety.Handler = _notoriety;
        Mobile.AllowHarmfulHandler = _harmful;
        Mobile.AllowBeneficialHandler = _beneficial;

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    m.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    private PlayerMobile Character(AccessLevel level = AccessLevel.Player, Point3D? at = null, Map map = null)
    {
        var account = new Account($"p863{Guid.NewGuid():N}"[..16], "p863-test-only") { Young = false };
        _accounts.Add(account);

        var pm = new PlayerMobile
        {
            Player = true, Race = Race.Human, AccessLevel = level, Name = $"P863 {Guid.NewGuid():N}"[..12]
        };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(at ?? GateSpot, map ?? Map.Trammel);

        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = account;
        return pm;
    }

    private static ColorWarsRun Open(int teams = 2)
    {
        Assert.True(StaffColorWars.Open(Map.Trammel, Corner1, Corner2, teams, out var message), message);
        Assert.True(StaffColorWars.PlaceGate(Map.Trammel, GateSpot, out message), message);
        return StaffColorWars.Active;
    }

    private PlayerMobile Joined()
    {
        var pm = Character();
        Assert.True(StaffColorWars.Join(pm, out var message), message);
        var p = StaffColorWars.ParticipantOf(pm);
        Assert.True(StaffColorWars.Active.Base(p.Team) == pm.Location, $"joined at {pm.Location} {pm.Map}, base {StaffColorWars.Active.Base(p.Team)}, region {pm.Region?.GetType().Name}");
        return pm;
    }

    private static int Tokens(PlayerMobile pm) => FestivalTokens.Instance.Balance(pm);

    // ---------------------------------------------------------------- opening and joining

    [Fact]
    public void OpeningChecksTheArenaTheIsleAndOneAtATimeAndPutsAStoneAtEachBase()
    {
        // Too small, a wrong team count, the isle.
        Assert.False(StaffColorWars.Open(Map.Trammel, Corner1, new Point3D(Corner1.X + 4, Corner1.Y + 20, 0), 2, out _));
        Assert.False(StaffColorWars.Open(Map.Trammel, Corner1, Corner2, 3, out _));
        Assert.False(StaffColorWars.Open(Map.Trammel, Corner1, new Point3D(Corner1.X + 80, Corner1.Y + 80, 0), 2, out _));
        if (Server.Tutorial.TutorialWorld.Isle is { } isle)
        {
            Assert.False(StaffColorWars.Open(isle, Corner1, Corner2, 2, out _));
        }

        Assert.Null(StaffColorWars.Active);

        var run = Open(4);
        Assert.False(StaffColorWars.Open(Map.Trammel, Corner1, Corner2, 2, out _)); // one at a time
        Assert.IsType<ColorWarsRegion>(Region.Find(new Point3D(Corner1.X + 5, Corner1.Y + 5, 0), Map.Trammel));

        Assert.Equal(4, run.Stones.Count);
        foreach (var team in run.Teams)
        {
            var stone = (ColorWarsExitStone)run.Stones[(int)team];
            Assert.Equal(team, stone.Team);
            Assert.Equal(StaffColorWars.Hue(team), stone.Hue);
            Assert.True(run.Contains(stone.X, stone.Y));
            Assert.True(stone.GetDistanceToSqrt(run.Base(team)) <= 2);
        }

        // Four bases, all apart, all inside.
        var bases = run.Teams.Select(run.Base).ToList();
        Assert.Equal(4, bases.Select(b => (b.X, b.Y)).Distinct().Count());
        Assert.All(bases, b => Assert.True(run.Contains(b.X, b.Y)));

        // The gate never goes inside the arena.
        Assert.False(StaffColorWars.PlaceGate(Map.Trammel, bases[0], out _));
        Assert.IsType<ColorWarsGate>(run.Gate);
        Assert.Equal(GateSpot, run.Gate.Location);
    }

    [Fact]
    public void TheGateAsksFirstAndRefusesTheDeadTheYoungTheMountedAndStaff()
    {
        Open();
        var pm = Character();
        var gate = StaffColorWars.Active.Gate;

        // Stepping on it only asks.
        Assert.True(gate.OnMoveOver(pm));
        Assert.True(pm.HasGump<ColorWarsJoinGump>());
        Assert.Null(StaffColorWars.ParticipantOf(pm));
        Assert.False(ColorWarsJoinGump.Respond(pm, 0)); // "Not now"
        Assert.Null(StaffColorWars.ParticipantOf(pm));

        Assert.True(ColorWarsJoinGump.Respond(pm, ColorWarsJoinGump.BtnJoin));
        Assert.NotNull(StaffColorWars.ParticipantOf(pm));
        Assert.NotNull(StaffColorWars.CannotJoin(pm)); // already in

        var young = Character();
        young.Young = true;
        Assert.NotNull(StaffColorWars.CannotJoin(young));

        var dead = Character();
        dead.Kill();
        Assert.NotNull(StaffColorWars.CannotJoin(dead));

        var gm = Character(AccessLevel.GameMaster);
        Assert.NotNull(StaffColorWars.CannotJoin(gm));
        gate.OnMoveOver(gm);
        Assert.False(gm.HasGump<ColorWarsJoinGump>());

        // An ungraduated trainee on Haven Island cannot leave it, so cannot join (nothing is taken from them).
        var trainee = Character(at: HavenSpot);
        Assert.NotNull(StaffColorWars.CannotJoin(trainee));

        var rider = Character();
        var horse = new Horse();
        horse.MoveToWorld(rider.Location, rider.Map);
        horse.Rider = rider;
        Assert.NotNull(StaffColorWars.CannotJoin(rider));
        horse.Delete();
    }

    [Fact]
    public void JoiningStashesEverythingInTheBankLendsTheKitAndBalancesTheTeams()
    {
        var run = Open();
        var pm = Character();
        var ring = new GoldRing();
        pm.Backpack.DropItem(ring);
        var shirt = new Shirt();
        pm.AddItem(shirt);
        var from = pm.Location;

        Assert.True(StaffColorWars.Join(pm, out var message), message);
        var p = StaffColorWars.ParticipantOf(pm);

        // Their own things: in one bag in their bank.
        Assert.Same(p.Stash, ring.Parent);
        Assert.Same(p.Stash, shirt.Parent);
        Assert.Same(pm.BankBox, p.Stash.Parent);
        Assert.Contains(shirt, p.Worn);
        Assert.Equal(from, p.ReturnLocation);

        // The kit, in the team's colour, and the body too.
        var hue = StaffColorWars.Hue(p.Team);
        Assert.Equal(hue, pm.HueMod);
        Assert.NotEmpty(p.Kit);
        Assert.All(p.Kit, i => Assert.Equal(hue, i.Hue));
        Assert.Contains(p.Kit, i => i is Robe);
        Assert.Contains(p.Kit, i => i is VikingSword);
        Assert.Contains(p.Kit, i => i is Spellbook);
        Assert.Contains(p.Kit, i => i is Bandage { Amount: 50 });
        Assert.All(p.Kit, i => Assert.True(i.Parent == pm || i.Parent == pm.Backpack));
        Assert.DoesNotContain(p.Kit, i => i.LootType == LootType.Blessed);

        // At the team's base, inside the arena.
        Assert.Equal(run.Map, pm.Map);
        Assert.Equal(run.Base(p.Team), pm.Location);
        Assert.IsType<ColorWarsRegion>(pm.Region);

        // The teams stay even.
        var second = Joined();
        Assert.NotEqual(p.Team, StaffColorWars.ParticipantOf(second).Team);
        Joined();
        Joined();
        Assert.Equal(2, run.CountOf(ColorWarsTeam.Red));
        Assert.Equal(2, run.CountOf(ColorWarsTeam.Blue));
    }

    // ---------------------------------------------------------------- the fight

    [Fact]
    public void TeammatesAreGreenAndTheOtherTeamsOrange()
    {
        Open();
        var a = Joined();
        var others = new[] { Joined(), Joined(), Joined() }; // four players: two a team
        var team = StaffColorWars.ParticipantOf(a).Team;
        var mate = others.Single(o => StaffColorWars.ParticipantOf(o).Team == team);
        var foe = others.First(o => StaffColorWars.ParticipantOf(o).Team != team);

        Assert.Equal(Notoriety.Ally, Notoriety.Compute(a, mate));
        Assert.Equal(Notoriety.Enemy, Notoriety.Compute(a, foe));

        // Outside the event nothing changes.
        var outsider = Character();
        Assert.Null(StaffColorWars.TeamNotoriety(a, outsider));
    }

    [Fact]
    public void NobodyFightsWhileTheTeamsGatherThenOnlyTheOtherTeam()
    {
        Open();
        var a = Joined();
        var others = new[] { Joined(), Joined(), Joined() }; // four players: two a team
        var team = StaffColorWars.ParticipantOf(a).Team;
        var mate = others.Single(o => StaffColorWars.ParticipantOf(o).Team == team);
        var foe = others.First(o => StaffColorWars.ParticipantOf(o).Team != team);

        Assert.False(a.CanBeHarmful(foe, false));
        Assert.False(a.CanBeHarmful(mate, false));

        Assert.True(StaffColorWars.StartBattle(out var message), message);
        Assert.False(StaffColorWars.StartBattle(out _));
        Assert.True(a.CanBeHarmful(foe, false));   // in Trammel, where players cannot otherwise harm players
        Assert.False(a.CanBeHarmful(mate, false));

        Assert.True(a.CanBeBeneficial(mate, false));
        Assert.False(a.CanBeBeneficial(foe, false));
    }

    [Fact]
    public void ADeathKeepsTheKitScoresForTheKillerAndRisesAtTheBase()
    {
        var run = Open();
        var a = Joined();
        var b = Joined();
        StaffColorWars.StartBattle(out _);
        var pa = StaffColorWars.ParticipantOf(a);
        var pb = StaffColorWars.ParticipantOf(b);

        // Walked off its base, then killed by the other team.
        a.Location = new Point3D(run.Base(pa.Team).X + 2, run.Base(pa.Team).Y, run.Base(pa.Team).Z);
        a.LastKiller = b;
        a.Kill();

        Assert.False(a.Alive);
        Assert.True(a.Corpse == null || a.Corpse.Deleted);                     // no corpse left to loot
        Assert.All(pa.Kit, i => Assert.Same(a.Backpack, i.Parent));          // every kit piece back in the pack
        Assert.Equal(1, run.Scores[(int)pb.Team]);
        Assert.Equal(0, run.Scores[(int)pa.Team]);
        Assert.Equal(0, a.Kills);                                             // no murder count
        Assert.Equal(0, b.Kills);

        ShardTestClock.AdvanceSeconds(StaffEventsSettings.Current.CwReviveSeconds + 1);
        Assert.True(a.Alive);
        Assert.Equal(run.Base(pa.Team), a.Location);
        Assert.Equal(a.HitsMax, a.Hits);
        Assert.Null(a.FindItemOnLayer(Layer.OuterTorso) as DeathRobe);
        Assert.All(pa.Kit, i => Assert.False(i.Deleted));

        // A death to a teammate, or to nobody, scores nothing.
        b.LastKiller = null;
        b.Kill();
        Assert.Equal(1, run.Scores.Sum());
    }

    [Fact]
    public void LeavingByTheStoneTakesTheKitBackAndGivesEverythingBack()
    {
        var run = Open();
        var pm = Character();
        var ring = new GoldRing();
        pm.Backpack.DropItem(ring);
        var shirt = new Shirt();
        pm.AddItem(shirt);
        var from = pm.Location;
        StaffColorWars.Join(pm, out _);
        var p = StaffColorWars.ParticipantOf(pm);
        var kit = p.Kit.ToList();
        var stash = p.Stash;

        // A stack split off the kit, and a thing of the player's own won in the meantime.
        var split = new Bandage(5) { Hue = StaffColorWars.Hue(p.Team) };
        pm.Backpack.DropItem(split);
        var won = new Gold(10);
        pm.Backpack.DropItem(won);
        var dropped = p.Kit.OfType<Bow>().Single();      // a kit piece left on the arena floor
        dropped.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), pm.Map);

        var stone = run.Stones.Cast<ColorWarsExitStone>().First(s => s.Team == p.Team);
        var stranger = Character();
        stone.OnDoubleClick(stranger);
        Assert.Null(StaffColorWars.ParticipantOf(stranger));

        stone.OnDoubleClick(pm);

        Assert.Null(StaffColorWars.ParticipantOf(pm));
        Assert.All(kit, i => Assert.True(i.Deleted));
        Assert.True(split.Deleted);
        Assert.True(dropped.Deleted);
        Assert.False(won.Deleted);
        Assert.Same(pm.Backpack, ring.Parent);
        Assert.True(shirt.Parent == pm || shirt.Parent == pm.Backpack);
        Assert.True(stash.Deleted);
        Assert.Equal(-1, pm.HueMod);
        Assert.Equal(from, pm.Location);
        Assert.Equal(Map.Trammel, pm.Map);
    }

    [Fact]
    public void TheEndPaysTheLeadingTeamAndADrawPaysNobody()
    {
        var run = Open();
        var a = Joined();
        var b = Joined();
        var team = StaffColorWars.ParticipantOf(a).Team;
        var before = (Tokens(a), Tokens(b));
        run.Scores[(int)team] = 3;

        Assert.True(StaffColorWars.End(true, out var message), message);
        Assert.Null(StaffColorWars.Active);
        Assert.Equal(before.Item1 + StaffEventsSettings.Current.CwWinTokens, Tokens(a));
        Assert.Equal(before.Item2, Tokens(b));
        Assert.Null(StaffColorWars.ParticipantOf(a));
        Assert.Equal(-1, a.HueMod);
        Assert.Equal(GateSpot, a.Location);
        Assert.True(run.Gate.Deleted);
        Assert.All(run.Stones, s => Assert.True(s.Deleted));
        Assert.Null(Region.Find(new Point3D(Corner1.X + 5, Corner1.Y + 5, 0), Map.Trammel) as ColorWarsRegion);

        // A draw.
        run = Open();
        a.MoveToWorld(GateSpot, Map.Trammel);
        b.MoveToWorld(GateSpot, Map.Trammel);
        StaffColorWars.Join(a, out _);
        StaffColorWars.Join(b, out _);
        run.Scores[0] = run.Scores[1] = 2;
        before = (Tokens(a), Tokens(b));
        StaffColorWars.End(true, out _);
        Assert.Equal(before, (Tokens(a), Tokens(b)));
    }

    [Fact]
    public void TheFirstTeamToTheKillsToWinEndsItAtOnce()
    {
        StaffEventsSettings.Current = new StaffEventsSettings { CwKillsToWin = 1 };
        Open();
        var a = Joined();
        var b = Joined();
        StaffColorWars.StartBattle(out _);
        var before = Tokens(a);

        b.LastKiller = a;
        b.Kill();
        ShardTestClock.AdvanceSeconds(0.1);

        Assert.Null(StaffColorWars.Active);
        Assert.Equal(before + StaffEventsSettings.Current.CwWinTokens, Tokens(a));
        Assert.True(b.Alive);                                   // the dead go home alive
        Assert.Equal(GateSpot, b.Location);
    }

    // ---------------------------------------------------------------- the arena's rules

    [Fact]
    public void NothingIsLearnedNoTravelNobodyWalksInAndAPlayerWhoWalksOutIsBroughtBack()
    {
        var run = Open();
        var pm = Joined();
        var region = (ColorWarsRegion)pm.Region;

        Assert.False(region.AllowGain(pm, pm.Skills[SkillName.Swords], null));
        Assert.False(region.CheckTravel(pm, GateSpot, TravelCheckType.RecallFrom, out _));
        Assert.False(region.AllowHousing(pm, pm.Location));

        // An outsider cannot walk in.
        var outsider = Character(at: new Point3D(Corner1.X - 1, Corner1.Y + 3, Corner1.Z));
        Assert.False(region.OnMoveInto(outsider, Direction.East, new Point3D(Corner1.X, Corner1.Y + 3, 0), outsider.Location));
        Assert.True(region.OnMoveInto(pm, Direction.East, pm.Location, pm.Location));

        // A player who walks off the field is brought back to the base.
        var team = StaffColorWars.ParticipantOf(pm).Team;
        pm.Location = new Point3D(Corner1.X - 2, Corner1.Y + 3, Corner1.Z);
        ShardTestClock.AdvanceSeconds(0.1);
        Assert.Equal(run.Base(team), pm.Location);
        Assert.NotNull(StaffColorWars.ParticipantOf(pm));
    }

    [Fact]
    public void ALoggedOutPlayerGetsTheirThingsBackWhereTheyJoined()
    {
        Open();
        var pm = Character();
        var ring = new GoldRing();
        pm.Backpack.DropItem(ring);
        StaffColorWars.Join(pm, out _);

        // Logged out in the arena.
        var ns = pm.NetState;
        pm.NetState = null;
        ns.Mobile = null;
        ns.Dispose();
        pm.LogoutLocation = pm.Location;
        pm.LogoutMap = pm.Map;
        pm.Internalize();
        Assert.Equal(Map.Internal, pm.Map);

        StaffColorWars.End(false, out _);

        Assert.Same(pm.Backpack, ring.Parent);
        Assert.Equal(GateSpot, pm.LogoutLocation);
        Assert.Equal(Map.Trammel, pm.LogoutMap);
    }

    // ---------------------------------------------------------------- save, hub, command, settings

    [Fact]
    public void ARunSurvivesASaveAnUnknownVersionIsRefusedAndAnOrphanedGateGoes()
    {
        var run = Open();
        var pm = Joined();
        StaffColorWars.StartBattle(out _);
        var p = StaffColorWars.ParticipantOf(pm);
        run.Scores[(int)p.Team] = 2;

        var store = new StaffColorWarsStore();
        var buffer = new byte[8192];
        var writer = new BufferWriter(buffer, true);
        store.Serialize(writer);
        writer.Flush();

        store.Deserialize(new BufferReader(buffer)); // drops the live run, as a fresh start would have none
        Assert.Null(StaffColorWars.Active);
        StaffColorWars.RegisterPending();
        StaffColorWars.RegisterPending(); // idempotent

        var back = StaffColorWars.Active;
        Assert.NotNull(back);
        Assert.NotSame(run, back);
        Assert.Equal(ColorWarsPhase.Fighting, back.Phase);
        Assert.Equal(2, back.Scores[(int)p.Team]);
        Assert.Same(run.Gate, back.Gate);
        Assert.Equal(run.Stones, back.Stones);
        var q = StaffColorWars.ParticipantOf(pm);
        Assert.Equal(p.Team, q.Team);
        Assert.Same(p.Stash, q.Stash);
        Assert.Equal(p.Kit, q.Kit);
        Assert.Equal(p.ReturnLocation, q.ReturnLocation);
        Assert.IsType<ColorWarsRegion>(Region.Find(new Point3D(Corner1.X + 5, Corner1.Y + 5, 0), Map.Trammel));

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(StaffColorWarsStore.CurrentVersion + 1);
        w2.Flush();
        Assert.Throws<InvalidDataException>(() => store.Deserialize(new BufferReader(bad)));

        // A gate saved with no run behind it removes itself after load; the live run's gate stays.
        var orphan = new ColorWarsGate();
        orphan.MoveToWorld(GateSpot, Map.Trammel);
        var gb = new byte[1024];
        var gw = new BufferWriter(gb, true);
        orphan.Serialize(gw);
        gw.Flush();
        orphan.Delete();
        var copy = (ColorWarsGate)Activator.CreateInstance(typeof(ColorWarsGate), orphan.Serial)!;
        copy.Deserialize(new BufferReader(gb));
        ShardTestClock.AdvanceSeconds(0.1);
        Assert.True(copy.Deleted);
        Assert.False(back.Gate.Deleted);
    }

    [Fact]
    public void TheEventsTabAndTheCommandRunColorWarsForAGameMasterOnly()
    {
        var gm = Character(AccessLevel.GameMaster);
        var hub = new StaffHubGump(gm, new StaffHubState { Tab = StaffHubTab.Events });
        Assert.Contains(StaffHubGump.BtnColorWars, hub.Entries.OfType<GumpButton>().Select(b => b.ButtonID));
        hub.Respond(gm, StaffHubGump.BtnColorWars);
        Assert.True(gm.HasGump<StaffColorWarsGump>());

        // The gump: corners, open, gate, start, end.
        gm.MoveToWorld(Corner1, Map.Trammel);
        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnCorner1);
        gm.MoveToWorld(Corner2, Map.Trammel);
        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnCorner2);

        var player = Character();
        StaffColorWarsGump.Respond(player, StaffColorWarsGump.BtnCorner1); // a player's press does nothing
        Assert.Null(StaffColorWars.CornersOf(player));
        StaffColorWarsGump.Respond(player, StaffColorWarsGump.BtnOpen2);
        Assert.Null(StaffColorWars.Active);

        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnOpen2);
        Assert.NotNull(StaffColorWars.Active);
        gm.MoveToWorld(GateSpot, Map.Trammel);
        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnGateHere);
        Assert.Equal(GateSpot, StaffColorWars.Active.Gate.Location);
        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnStart);
        Assert.Equal(ColorWarsPhase.Fighting, StaffColorWars.Active.Phase);
        StaffColorWarsGump.Respond(gm, StaffColorWarsGump.BtnCancel);
        Assert.Null(StaffColorWars.Active);

        // The typed command.
        gm.MoveToWorld(Corner1, Map.Trammel);
        CommandSystem.Handle(gm, "[ColorWars corner1");
        gm.MoveToWorld(Corner2, Map.Trammel);
        CommandSystem.Handle(gm, "[ColorWars corner2");
        CommandSystem.Handle(gm, "[ColorWars open 4");
        Assert.Equal(4, StaffColorWars.Active.TeamCount);
        gm.MoveToWorld(GateSpot, Map.Trammel);
        CommandSystem.Handle(gm, "[ColorWars gate");
        CommandSystem.Handle(gm, "[ColorWars start");
        Assert.Equal(ColorWarsPhase.Fighting, StaffColorWars.Active.Phase);
        CommandSystem.Handle(gm, "[ColorWars end");
        Assert.Null(StaffColorWars.Active);
    }

    [Fact]
    public void ABadColorWarsSettingIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(StaffEventsSettings.Parse("{\"CwWinTokens\": -5}", errors));
        Assert.NotEmpty(errors);

        errors.Clear();
        Assert.Null(StaffEventsSettings.Parse("{\"CwMinSide\": 1}", errors));

        errors.Clear();
        var ok = StaffEventsSettings.Parse("{\"CwKillsToWin\": 10, \"CwKitSpellbook\": false}", errors);
        Assert.Empty(errors);
        Assert.Equal(10, ok.CwKillsToWin);
        Assert.False(ok.CwKitSpellbook);
        Assert.Equal(15, ok.CwWinTokens); // a missing key keeps its default
    }
}
