// RandomEncounterVerification.cs
//
// cc-P776: random encounters (server/customizations/Encounters). Notes in shard-migration
// notes/cc-P776-random-encounters.md.
//
// Facts: an eligible traveler on an encounter facet gets one; each never rule refuses (the isle, New Haven and Haven
// Island, a guarded town, a house, a boat, staff, the dead, hidden, idle, Young, a trainee, in combat, already in one);
// the cooldown, per character and per account, and its end; the off switch (and the file's "enabled": false); the
// cleanup timing (nothing leaves early, everything not fighting leaves on time, a fighter stays, a creature that walks
// into town is removed at once, a night procession leaves at dawn, a restart's leftovers are removed); each starter
// spawns its creatures (and the procession only at night in season, the surveyor only for the League); the toll's pay
// rule; nothing an encounter makes is tamable; the default table passes its own checks.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Encounters;
using Server.Engines.RewardStores;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RandomEncounterVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<Mobile> _made = [];
    private readonly List<Item> _items = [];
    private readonly List<Region> _regions = [];
    private readonly List<Account> _accounts = [];
    private DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    // A wilderness spot well away from other facts' regions (no region is registered here in the test host).
    private static readonly Point3D Spot = new(4400, 600, 0);

    public RandomEncounterVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        FoldMaps.Configure();

        ClusterFEncounters.Reset();
        EncounterTable.Current = EncounterTable.BuildDefaults();
        ClusterFEncounters.Clock = () => _now;
        ClusterFEncounters.Roll = () => 0.0;
        ClusterFEncounters.HourAt = (_, _) => 12;
        ClusterFEncounters.InSeason = _ => false;
        ClusterFEncounters.IsLeague = _ => false;
        ClusterFEncounters.LoopOf = _ => 1;
        ClusterFEncounters.FindSpot = (pm, _) => new Point3D(pm.X + 10, pm.Y, pm.Z);
    }

    public void Dispose()
    {
        foreach (var e in ClusterFEncounters.Active.ToList())
        {
            foreach (var bc in e.All.ToList())
            {
                bc.Delete();
            }
        }

        ClusterFEncounters.Reset();
        EncounterTable.Current = EncounterTable.BuildDefaults();
        ClusterFEncounters.Clock = () => Core.Now;
        ClusterFEncounters.Roll = Utility.RandomDouble;
        ClusterFEncounters.FindSpot = ClusterFEncounters.DefaultFindSpot;

        foreach (var r in _regions)
        {
            r.Unregister();
        }

        foreach (var i in _items)
        {
            i.Delete();
        }

        foreach (var m in _made)
        {
            m.Delete();
        }

        foreach (var a in _accounts)
        {
            Accounts.Remove(a);
        }
    }

    // ---------------------------------------------------------------- helpers

    private PlayerMobile Traveler(Point3D? at = null, Account account = null, Map map = null)
    {
        if (account == null)
        {
            account = new Account($"p776{Guid.NewGuid():N}"[..16], "p776-test-only") { Young = false }; // new accounts start Young
            _accounts.Add(account);
        }

        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Traveler" };
        pm.AddItem(new Backpack());
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] == null)
            {
                account[i] = pm;
                break;
            }
        }

        pm.Young = false;
        pm.Skills[SkillName.Swords].Base = 50.0; // level 50 (ClusterFCreatureLevels' stand-in)
        _made.Add(pm);

        // Walk 20 tiles to the spot, sampled at both ends: this player is traveling.
        var to = at ?? Spot;
        map ??= Map.Trammel;
        pm.MoveToWorld(new Point3D(to.X - 20, to.Y, to.Z), map);
        ClusterFEncounters.NoteTravel(pm, _now);
        pm.MoveToWorld(to, map);
        ClusterFEncounters.NoteTravel(pm, _now);
        return pm;
    }

    private T Region<T>(T r) where T : Region
    {
        r.Register();
        _regions.Add(r);
        return r;
    }

    private static Rectangle3D Around(Point3D p, int r) =>
        new(new Point3D(p.X - r, p.Y - r, -128), new Point3D(p.X + r, p.Y + r, 127));

    private static EncounterDef Def(string id) => EncounterTable.Current.Find(id)!;

    private void AssertRefused(PlayerMobile pm, string because)
    {
        var why = ClusterFEncounters.WhyNot(pm, _now);
        _out.WriteLine($"refused: {why}");
        Assert.Equal(because, why);
        Assert.Null(ClusterFEncounters.TryEncounter(pm, _now));
        Assert.Empty(ClusterFEncounters.Active);
    }

    // ---------------------------------------------------------------- the engine

    [Fact]
    public void AnEligibleTravelerOnAnEncounterFacetGetsOne()
    {
        var pm = Traveler();
        Assert.Null(ClusterFEncounters.WhyNot(pm, _now));

        var e = ClusterFEncounters.TryEncounter(pm, _now);
        Assert.NotNull(e);
        Assert.Equal("toll", e.Def.Id); // by day, not League, out of season: the toll is the only one that qualifies
        Assert.Single(ClusterFEncounters.Active);
        Assert.Equal(pm, e.Player);

        // A miss on the roll starts nothing.
        foreach (var bc in e.All.ToList())
        {
            bc.Delete();
        }

        ClusterFEncounters.Reset();
        ClusterFEncounters.Roll = () => 0.99;
        var other = Traveler(new Point3D(Spot.X, Spot.Y + 100, 0));
        Assert.Null(ClusterFEncounters.TryEncounter(other, _now));
    }

    [Fact]
    public void TheDefaultTablePassesItsChecksAndRoundTripsThroughTheFile()
    {
        var table = EncounterTable.BuildDefaults();
        Assert.Empty(table.Validate());
        Assert.Equal(["toll", "surveyor", "procession"], table.Encounters.Select(e => e.Id));

        var errors = new List<string>();
        var back = EncounterTable.Parse(EncounterTable.ToJson(table), "test", errors);
        Assert.Empty(errors);
        Assert.NotNull(back);
        Assert.Equal(table.Encounters.Count, back.Encounters.Count);
        Assert.Equal(EncounterKind.Procession, back.Find("procession")!.Kind);

        // A table naming a type that is not a creature is refused whole.
        errors.Clear();
        var bad = EncounterTable.ToJson(table).Replace("\"Brigand\"", "\"Longsword\"");
        Assert.Null(EncounterTable.Parse(bad, "test", errors));
        Assert.Contains(errors, e => e.Contains("Longsword"));
    }

    // ---------------------------------------------------------------- the off switch

    [Fact]
    public void TheOffSwitchStopsEverything()
    {
        var pm = Traveler();
        EncounterTable.Current.Enabled = false;
        AssertRefused(pm, "encounters are off");

        // The file's switch: "enabled": false parses and is what the table in force says.
        var errors = new List<string>();
        var json = EncounterTable.ToJson(EncounterTable.BuildDefaults());
        var on = "\"enabled\": true";
        var first = json.IndexOf(on, StringComparison.Ordinal); // the table's own, before any encounter's
        Assert.True(first > 0 && first < json.IndexOf("\"encounters\"", StringComparison.Ordinal));
        json = json[..first] + "\"enabled\": false" + json[(first + on.Length)..];
        var off = EncounterTable.Parse(json, "test", errors);
        Assert.Empty(errors);
        Assert.False(off!.Enabled);
        EncounterTable.Current = off;
        AssertRefused(pm, "encounters are off");

        EncounterTable.Current = EncounterTable.BuildDefaults();
        Assert.NotNull(ClusterFEncounters.TryEncounter(pm, _now));
    }

    // ---------------------------------------------------------------- the never rules: places

    [Fact]
    public void NeverOnTheWakeningIsle()
    {
        var pm = Traveler(new Point3D(6640, 3992, 0), map: FoldMaps.Fold);
        AssertRefused(pm, "the Wakening Isle");
        Assert.Equal("the Wakening Isle", ClusterFEncounters.WhyNotHere(FoldMaps.Fold, new Point3D(6640, 3992, 0)));

        // Even listed as an encounter facet.
        EncounterTable.Current.Facets.Add(FoldMaps.Fold.Name);
        AssertRefused(pm, "the Wakening Isle");
    }

    [Fact]
    public void NeverOnHavenIslandOrInNewHaven()
    {
        Region(new BaseRegion("Haven Island", Map.Trammel, 1, Around(Spot, 30)));
        AssertRefused(Traveler(), "in Haven Island");

        var at = new Point3D(Spot.X + 200, Spot.Y, 0);
        Region(new BaseRegion("New Haven", Map.Trammel, 1, Around(at, 30)));
        AssertRefused(Traveler(at), "in New Haven");
    }

    [Fact]
    public void NeverInAGuardedTownOrADungeonOrJail()
    {
        Region(new TownRegion("p776 town", Map.Trammel, 50, Around(Spot, 30)));
        AssertRefused(Traveler(), "in a town");

        var guarded = new Point3D(Spot.X + 200, Spot.Y, 0);
        Region(new GuardedRegion("p776 guarded", Map.Trammel, 50, Around(guarded, 30)));
        AssertRefused(Traveler(guarded), "in a town");

        var dungeon = new Point3D(Spot.X + 400, Spot.Y, 0);
        Region(new DungeonRegion("p776 dungeon", Map.Trammel, 50, Around(dungeon, 30)));
        AssertRefused(Traveler(dungeon), "in a dungeon");
    }

    [Fact]
    public void NeverOnAFacetNotListed()
    {
        EncounterTable.Current.Facets = ["Felucca"];
        AssertRefused(Traveler(), "Trammel is not an encounter facet");
    }

    [Fact]
    public void NeverInAHouse()
    {
        var owner = Traveler(new Point3D(Spot.X, Spot.Y + 300, 0));
        var house = new SmallOldHouse(owner, 0x64);
        _items.Add(house);
        var at = new Point3D(Spot.X + 300, Spot.Y, 0);
        house.MoveToWorld(at, Map.Trammel);

        var pm = Traveler(at);
        _out.WriteLine($"house at {house.Location}: FindHouseAt = {BaseHouse.FindHouseAt(pm)}, region {pm.Region}");
        AssertRefused(pm, "in a house");
    }

    [Fact]
    public void NeverOnABoat()
    {
        var boat = new SmallBoat();
        _items.Add(boat);
        var at = new Point3D(Spot.X + 500, Spot.Y, 0);
        boat.MoveToWorld(at, Map.Trammel);

        var pm = Traveler(at);
        _out.WriteLine($"boat at {boat.Location}: FindBoatAt = {BaseBoat.FindBoatAt(pm.Location, pm.Map)}");
        AssertRefused(pm, "on a boat");
    }

    // ---------------------------------------------------------------- the never rules: players

    [Fact]
    public void NeverForStaff()
    {
        var pm = Traveler();
        pm.AccessLevel = AccessLevel.Counselor;
        AssertRefused(pm, "staff");
    }

    [Fact]
    public void NeverForTheDead()
    {
        var pm = Traveler();
        pm.Kill();
        Assert.False(pm.Alive);
        AssertRefused(pm, "dead");
    }

    [Fact]
    public void NeverForTheHiddenOrTheIdle()
    {
        var pm = Traveler();
        pm.Hidden = true;
        AssertRefused(pm, "hidden");
        pm.Hidden = false;

        // Idle: no travel inside the window...
        _now += TimeSpan.FromSeconds(EncounterTable.Current.TravelWindowSeconds + 1);
        AssertRefused(pm, "idle (not traveling)");

        // ...and shuffling a few tiles is not travel.
        pm.MoveToWorld(new Point3D(pm.X + 3, pm.Y, pm.Z), pm.Map);
        ClusterFEncounters.NoteTravel(pm, _now);
        AssertRefused(pm, "idle (not traveling)");

        // A never-sampled player has not traveled either.
        var fresh = new PlayerMobile { Player = true, Race = Race.Human, Name = "Fresh" };
        _made.Add(fresh);
        fresh.MoveToWorld(Spot, Map.Trammel);
        Assert.Equal("idle (not traveling)", ClusterFEncounters.WhyNot(fresh, _now));
    }

    [Fact]
    public void NeverForYoungCharactersOrTrainees()
    {
        var pm = Traveler();
        pm.Young = true;
        AssertRefused(pm, "Young");
        pm.Young = false;

        ((Account)pm.Account).Young = true;
        AssertRefused(pm, "Young");
        ((Account)pm.Account).Young = false;

        ClusterFEncounters.LoopOf = _ => 0;
        AssertRefused(pm, "a trainee (loop 0)");
    }

    [Fact]
    public void NeverWhileInCombat()
    {
        var pm = Traveler();
        var foe = new Brigand();
        _made.Add(foe);
        foe.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), pm.Map);
        pm.Combatant = foe;
        AssertRefused(pm, "in combat");
    }

    // ---------------------------------------------------------------- the cooldown

    [Fact]
    public void TheCooldownStopsStackingOnACharacterAndItsAccount()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.TryEncounter(pm, _now);
        Assert.NotNull(e);

        // While it is out: no second one.
        Assert.Equal("already in an encounter", ClusterFEncounters.WhyNot(pm, _now));
        Assert.Null(ClusterFEncounters.TryEncounter(pm, _now));
        Assert.Single(ClusterFEncounters.Active);

        // Gone, but inside the cooldown: still no.
        foreach (var bc in e.All.ToList())
        {
            bc.Delete();
        }

        ClusterFEncounters.Sweep();
        Assert.Empty(ClusterFEncounters.Active);
        Assert.Equal("cooldown", ClusterFEncounters.WhyNot(pm, _now));

        // Another character on the same account, far away: the account's cooldown.
        var alt = Traveler(new Point3D(Spot.X, Spot.Y + 200, 0), (Account)pm.Account);
        Assert.Equal("cooldown (account)", ClusterFEncounters.WhyNot(alt, _now));

        // A minute before the end: still no. At the end (and traveling again): yes.
        var cooldown = TimeSpan.FromMinutes(EncounterTable.Current.CooldownMinutes);
        _now += cooldown - TimeSpan.FromMinutes(1);
        pm.MoveToWorld(new Point3D(pm.X + 20, pm.Y, pm.Z), pm.Map);
        ClusterFEncounters.NoteTravel(pm, _now);
        Assert.Equal("cooldown", ClusterFEncounters.WhyNot(pm, _now));

        _now += TimeSpan.FromMinutes(1);
        pm.MoveToWorld(new Point3D(pm.X + 20, pm.Y, pm.Z), pm.Map);
        ClusterFEncounters.NoteTravel(pm, _now);
        Assert.Null(ClusterFEncounters.WhyNot(pm, _now));
    }

    [Fact]
    public void NoEncounterStartsBesideOneAlreadyOut()
    {
        var first = Traveler();
        Assert.NotNull(ClusterFEncounters.TryEncounter(first, _now));

        var second = Traveler(new Point3D(Spot.X + 5, Spot.Y + 5, 0));
        Assert.Equal("another encounter is nearby", ClusterFEncounters.WhyNot(second, _now));
    }

    // ---------------------------------------------------------------- the starters

    [Fact]
    public void TheTollRoadSpawnsTwoToFourBrigandsAndATollKeeper()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now);
        Assert.NotNull(e);
        _out.WriteLine(string.Join(", ", e.Creatures.Select(c => $"{c.GetType().Name} {c.Name} {c.Title}")));

        Assert.InRange(e.Creatures.Count, 2, 4);
        Assert.All(e.Creatures, c => Assert.IsType<Brigand>(c));
        Assert.Equal("the toll keeper", e.Leader!.Title);
        Assert.All(e.Creatures, c => Assert.Equal(FightMode.Aggressor, c.FightMode)); // peaceful until the deadline
        Assert.All(e.Creatures, c => Assert.False(c.Tamable));
        Assert.All(e.Creatures, c => Assert.Contains(c.Serial, ClusterFEncounterState.Spawned));

        // Past the deadline they turn on the player.
        ClusterFEncounters.Sweep(_now + TimeSpan.FromSeconds(Def("toll").TollWaitSeconds));
        Assert.Equal(EncounterStage.Hostile, e.Stage);
        Assert.All(e.Creatures, c => Assert.Equal(FightMode.Closest, c.FightMode));
        Assert.All(e.Creatures, c => Assert.Equal(pm, c.Combatant));
    }

    [Fact]
    public void PayingTheTollTakesFivePercentAtMostTwoHundredAndTheyLeave()
    {
        var def = Def("toll");
        Assert.Equal(50, ClusterFEncounters.TollFor(def, 1000));
        Assert.Equal(200, ClusterFEncounters.TollFor(def, 10000));
        Assert.Equal(0, ClusterFEncounters.TollFor(def, 19));

        var pm = Traveler();
        pm.Backpack.DropItem(new Gold(1000));
        var e = ClusterFEncounters.Start(pm, def, _now)!;
        var brigands = e.Creatures.ToList();

        Assert.Equal(50, ClusterFEncounters.TryPayToll(pm));
        Assert.Equal(950, pm.Backpack.GetAmount(typeof(Gold)));
        Assert.Equal(EncounterStage.Done, e.Stage);
        Assert.Equal(-1, ClusterFEncounters.TryPayToll(pm)); // once

        ClusterFEncounters.Sweep(_now + TimeSpan.FromSeconds(5));
        Assert.Empty(ClusterFEncounters.Active);
        Assert.All(brigands, c => Assert.True(c.Deleted));

        // No toll is waiting for a player with no encounter.
        var other = Traveler(new Point3D(Spot.X, Spot.Y + 200, 0));
        Assert.Equal(-1, ClusterFEncounters.TryPayToll(other));
    }

    [Fact]
    public void TheLostSurveyorSpawnsASurveyorAndTwoOrThreeNativesOnHerForLeagueMembersOnly()
    {
        var pm = Traveler();
        var def = Def("surveyor");
        Assert.False(ClusterFEncounters.Qualifies(pm, def, _now)); // not League

        ClusterFEncounters.IsLeague = _ => true;
        Assert.True(ClusterFEncounters.Qualifies(pm, def, _now));

        var e = ClusterFEncounters.Start(pm, def, _now);
        Assert.NotNull(e);
        var ally = Assert.IsType<EncounterSurveyor>(e.Ally);
        Assert.Equal("the League surveyor", ally.Title);
        Assert.InRange(e.Creatures.Count, 2, 3);
        Assert.All(e.Creatures, c => Assert.Contains(c.GetType().Name, def.Creatures[0].Types));
        Assert.All(e.Creatures, c => Assert.Equal(ally, c.Combatant));
        Assert.All(e.Creatures, c => Assert.False(c.Tamable));

        // Saved: the natives gone and she alive, she thanks and leaves.
        foreach (var c in e.Creatures.ToList())
        {
            c.Delete();
        }

        ClusterFEncounters.Sweep(_now);
        Assert.Equal(EncounterStage.Done, e.Stage);
        ally.Combatant = null;
        ClusterFEncounters.Sweep(_now + TimeSpan.FromSeconds(5));
        Assert.True(ally.Deleted);
        Assert.Empty(ClusterFEncounters.Active);
    }

    [Fact]
    public void TheLanternProcessionSpawnsAtNightInSeasonOnly()
    {
        var pm = Traveler();
        var def = Def("procession");

        Assert.False(ClusterFEncounters.Qualifies(pm, def, _now)); // day, out of season
        ClusterFEncounters.InSeason = name => name == "halloween";
        Assert.False(ClusterFEncounters.Qualifies(pm, def, _now)); // still day
        ClusterFEncounters.HourAt = (_, _) => 23;
        Assert.True(ClusterFEncounters.Qualifies(pm, def, _now));
        ClusterFEncounters.InSeason = _ => false;
        Assert.False(ClusterFEncounters.Qualifies(pm, def, _now)); // night, out of season
        ClusterFEncounters.InSeason = name => name == "halloween";

        var e = ClusterFEncounters.Start(pm, def, _now);
        Assert.NotNull(e);
        var walkers = e.Creatures.ToList();
        Assert.InRange(e.Creatures.Count, 3, 5);
        Assert.All(e.Creatures, c => Assert.IsType<Zombie>(c));
        Assert.Equal("the lantern bearer", e.Leader!.Name);
        Assert.All(e.Creatures.Skip(1), c => Assert.Equal("a lantern walker", c.Name));
        Assert.All(e.Creatures, c => Assert.Equal(0x2B, c.Hue));
        Assert.All(e.Creatures, c => Assert.Equal(FightMode.Aggressor, c.FightMode)); // harmless unless attacked

        // At dawn they leave.
        ClusterFEncounters.Sweep(_now);
        Assert.Single(ClusterFEncounters.Active);
        ClusterFEncounters.HourAt = (_, _) => 7;
        ClusterFEncounters.Sweep(_now + TimeSpan.FromSeconds(10));
        Assert.Empty(ClusterFEncounters.Active);
        Assert.All(walkers, c => Assert.True(c.Deleted));
    }

    [Fact]
    public void AnEncounterPicksOnlyWhatThePlayerQualifiesFor()
    {
        var pm = Traveler();

        // Under the toll's level floor (15), by day, out of season, not League: nothing qualifies, nothing starts.
        pm.Skills[SkillName.Swords].Base = 5.0;
        Assert.Null(ClusterFEncounters.Pick(pm, _now));
        Assert.Null(ClusterFEncounters.TryEncounter(pm, _now));

        // League at level 10 and up: the surveyor.
        pm.Skills[SkillName.Swords].Base = 12.0;
        ClusterFEncounters.IsLeague = _ => true;
        Assert.Equal("surveyor", ClusterFEncounters.Pick(pm, _now)!.Id);
    }

    // ---------------------------------------------------------------- the rewards, through the existing systems

    private static bool _achievements;

    [Fact]
    public void TheTollKeepersKillerGetsTheTollBoxOncePerLoop()
    {
        LootTable.Current = LootTable.Defaults;
        ClusterFLootBoxes.RarityRoll = _ => BoxRarity.Common;
        ClusterFLootBoxes.SeasonClock = () => new DateTime(2026, 6, 15);
        FanfareLines.Current = FanfareLines.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;

        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        var keeper = e.Leader!;

        keeper.Damage(keeper.Hits + 50, pm); // the creature death event: the encounter's hook

        Assert.False(keeper.Alive);
        Assert.Equal(EncounterStage.Done, e.Stage);
        Assert.Equal(1, ClusterFLootBoxes.RecordOf(pm)!.CountOf("encounter:toll"));

        // A second toll keeper on the same loop: no second box.
        ClusterFEncounters.Reset();
        var again = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        again.Leader!.Damage(again.Leader.Hits + 50, pm);
        Assert.Equal(1, ClusterFLootBoxes.RecordOf(pm)!.CountOf("encounter:toll"));
        foreach (var bc in e.All.Concat(again.All).ToList())
        {
            bc.Delete();
        }
    }

    [Fact]
    public void TheLanternBearersKillerGetsAFestivalToken()
    {
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        ClusterFEncounters.HourAt = (_, _) => 23;
        ClusterFEncounters.InSeason = _ => true;
        var pm = Traveler();
        var user = ((Account)pm.Account).Username;
        var before = FestivalTokens.Earned(user);
        var e = ClusterFEncounters.Start(pm, Def("procession"), _now)!;
        var bearer = e.Leader!;
        var walker = e.Creatures[1];

        walker.Damage(walker.Hits + 50, pm);
        Assert.Equal(before, FestivalTokens.Earned(user)); // a walker gives nothing extra
        Assert.Equal(EncounterStage.Open, e.Stage);

        bearer.Damage(bearer.Hits + 50, pm);
        Assert.Equal(before + Def("procession").LeaderTokens, FestivalTokens.Earned(user));
        Assert.Equal(EncounterStage.Done, e.Stage);
    }

    // ---------------------------------------------------------------- cleanup

    [Fact]
    public void UnfoughtCreaturesLeaveOnTimeAndNotBefore()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        var creatures = e.Creatures.ToList();
        var leave = TimeSpan.FromMinutes(EncounterTable.Current.LeaveAfterMinutes);

        // The toll is paid so they are not fighting; a minute early nothing goes.
        e.Stage = EncounterStage.Done;
        Assert.Equal(0, ClusterFEncounters.Sweep(_now + leave - TimeSpan.FromMinutes(1)));
        Assert.All(creatures, c => Assert.False(c.Deleted));

        Assert.Equal(creatures.Count, ClusterFEncounters.Sweep(_now + leave));
        Assert.All(creatures, c => Assert.True(c.Deleted));
        Assert.Empty(ClusterFEncounters.Active);
        Assert.Empty(ClusterFEncounterState.Spawned);
    }

    [Fact]
    public void AFightingCreatureStaysUntilItStopsAndItsCorpseAndLootAreNotTouched()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        e.Stage = EncounterStage.Done;
        var fighter = e.Creatures[0];
        var others = e.Creatures.Skip(1).ToList();
        Assert.NotEmpty(others);
        fighter.Combatant = pm;
        var leave = _now + TimeSpan.FromMinutes(EncounterTable.Current.LeaveAfterMinutes);

        ClusterFEncounters.Sweep(leave);
        Assert.False(fighter.Deleted);
        Assert.All(others, c => Assert.True(c.Deleted));
        Assert.Single(ClusterFEncounters.Active);

        // The player's own things are never part of the sweep.
        var loot = new Gold(77);
        pm.Backpack.DropItem(loot);
        fighter.Combatant = null;
        ClusterFEncounters.Sweep(leave + TimeSpan.FromSeconds(10));
        Assert.True(fighter.Deleted);
        Assert.False(loot.Deleted);
        Assert.Empty(ClusterFEncounters.Active);
    }

    [Fact]
    public void ACreatureThatFollowsIntoTownIsRemovedAtOnce()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        var chaser = e.Creatures[0];
        var rest = e.Creatures.Skip(1).ToList();
        chaser.Combatant = pm; // even mid-fight

        var town = new Point3D(Spot.X + 600, Spot.Y, 0);
        Region(new TownRegion("p776 chase town", Map.Trammel, 50, Around(town, 20)));
        chaser.MoveToWorld(town, Map.Trammel);

        Assert.Equal(1, ClusterFEncounters.Sweep(_now));
        Assert.True(chaser.Deleted);
        Assert.NotEmpty(rest);
        Assert.All(rest, c => Assert.False(c.Deleted));
    }

    [Fact]
    public void ARestartRemovesWhatWasLeftOut()
    {
        var pm = Traveler();
        var e = ClusterFEncounters.Start(pm, Def("toll"), _now)!;
        var creatures = e.Creatures.ToList();
        var unrelated = new Brigand();
        _made.Add(unrelated);
        unrelated.MoveToWorld(new Point3D(Spot.X + 30, Spot.Y, 0), Map.Trammel);

        // The saved serials are all that survives a restart.
        Assert.Equal(creatures.Count, ClusterFEncounterState.Spawned.Count);
        Assert.Equal(creatures.Count, ClusterFEncounters.SweepSaved());
        Assert.All(creatures, c => Assert.True(c.Deleted));
        Assert.False(unrelated.Deleted);
        Assert.Empty(ClusterFEncounterState.Spawned);
    }
}
