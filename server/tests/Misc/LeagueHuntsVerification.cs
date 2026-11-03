// LeagueHuntsVerification.cs
//
// cc-P780 (F-24): League hunts on the requisitions board. Notes in shard-migration notes/cc-P780-daily-league-hunts.md.
// Each fact was seen red first (the notes' red table).
//
// Facts:
//   1. The default table validates, names every League Bestiary creature once, writes as JSON and reads back the same;
//      a file naming a creature outside the bestiary, or with an unknown setting, is refused whole and the table in
//      force is kept.
//   2. A first read rolls the daily hunts (different families, within the tier's level reach, posted by a guild the
//      character has joined, paying the tier's numbers) and one great hunt (counts and pay multiplied, a Silver box).
//   3. The daily refresh: the same hunts and progress an hour later and just before the refresh hour, new ones just
//      after; a finished hunt stays, marked done, until then; the great hunt is not touched by a daily refresh.
//   4. The weekly reset: weeks start on the reset day at the refresh hour; the great hunt stays all week, kills and
//      all, and is new just after the reset.
//   5. Counting: a kill of the family counts one on every open hunt of it and nothing on others; the last kill pays
//      Renown (and lifetime Renown), the guild's standing and gold once; later kills pay nothing; the great hunt's
//      last kill also delivers a System's Silver Loot Box.
//   6. Credit through the real death event: the player with the most damage counts, not the last hitter; a party
//      member in range counts too, one out of range or outside the party does not; a pet's death counts for nobody.
//   7. HuntFamily is filled: a slayer requisition row marked "any" asks for an open hunt's family slayer; with none it
//      draws from the table as before.
//   8. The record survives a write and a read with every hunt field compared; a version 0 record reads with no hunts.
//   9. The board shows each hunt with its progress and pay; [hunts says one line per hunt between a header and "end".
//  10. The rules: a hunt pays only Renown, standing, guild points, gold and a League shop box; no Silver box holds a
//      power scroll, a stat or skill scroll, a bless deed or a magic item; the numbers come from the file.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.PartySystem;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueHuntsVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueHuntsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        RequisitionTable.Current = RequisitionTable.Defaults;
        LeagueHuntTable.Current = LeagueHuntTable.Defaults;
    }

    // ---------------------------------------------------------------- helpers

    // Tuesday 2026-10-20 12:00 UTC: the refresh hour is 05:00, the great hunt resets on Mondays.
    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Point3D Spot = new(3460, 2604, 18);

    private sealed class Scope : IDisposable
    {
        private readonly Func<DateTime> _clock = ClusterFRequisitions.Clock;
        private readonly Func<LeagueHuntFamily, bool> _home = LeagueHunts.HasHome;
        private readonly Func<PlayerMobile, string, SlayerName?> _seam = ClusterFRequisitions.HuntFamily;

        public Scope(DateTime now)
        {
            At(now);
            LeagueHunts.HasHome = _ => true;
            ClusterFRequisitions.HuntFamily = LeagueHunts.HuntFamilyFor;
        }

        public static void At(DateTime now) => ClusterFRequisitions.Clock = () => now;

        public void Dispose()
        {
            ClusterFRequisitions.Clock = _clock;
            LeagueHunts.HasHome = _home;
            ClusterFRequisitions.HuntFamily = _seam;
            RequisitionTable.Current = RequisitionTable.Defaults;
            LeagueHuntTable.Current = LeagueHuntTable.Defaults;
        }
    }

    private static Account NewAccount() => new($"p780{Guid.NewGuid():N}"[..16], "p780-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0, Point3D? at = null)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(at ?? Spot, Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account, params IEntity[] extras)
    {
        foreach (var e in extras)
            e?.Delete();

        for (var i = 0; i < account.Length; i++)
            if (account[i] is PlayerMobile pm)
            {
                pm.Party = null;
                pm.Backpack?.Delete();
                pm.Delete();
            }

        var data = ClusterFAccountPersistence.Get(account);
        data?.ClearGuildData();
        data?.ClearRequisitionData();
        data?.ClearLeagueData();
        Accounts.Remove(account);
    }

    private static void Join(PlayerMobile pm, string guild, int standing)
    {
        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        data.JoinedGuilds.Add(guild);
        data.AddReputation(guild, standing);
    }

    private static int GoldIn(PlayerMobile pm) => pm.Backpack.Items.OfType<Gold>().Sum(g => g.Amount);

    private static LeagueHuntFamily Family(string key) => LeagueHuntTable.Current.Family(key);

    /// <summary>Sets the character's hunts to exactly these (rolled first, so the day and week are current).</summary>
    private static CharacterRequisitionData Plant(PlayerMobile pm, HuntEntry great, params HuntEntry[] daily)
    {
        var data = LeagueHunts.EnsureCurrent(pm);
        data.Hunts.Clear();
        data.Hunts.AddRange(daily);
        data.GreatHunt = great;
        return data;
    }

    private static HuntEntry Hunt(string family, int tier = 0, bool great = false) =>
        LeagueHunts.Make(Family(family), "warriors", tier, great, Noon);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheDefaultTableValidatesNamesTheBestiaryAndRoundTripsAndABadFileIsRefusedWhole()
    {
        using var scope = new Scope(Noon);
        var defaults = LeagueHuntTable.Defaults;
        var errors = new List<string>();
        LeagueHuntTable.Validate(defaults, errors);
        Assert.Empty(errors);

        var named = defaults.Families.SelectMany(f => f.Entries).Select(e => e.Type).ToList();
        Assert.Equal(named.Count, named.Distinct().Count());
        foreach (var entry in ClusterFLeagueBestiary.Entries)
            Assert.Contains(entry.Type, named);

        var json = LeagueHuntTable.ToJson(defaults);
        var copy = LeagueHuntTable.Parse(json, "round trip", errors);
        Assert.Empty(errors);
        Assert.NotNull(copy);
        Assert.Equal(LeagueHuntTable.ToJson(copy), json);
        Assert.Equal(defaults.Families.Count, copy.Families.Count);
        Assert.All(copy.Families, f => Assert.NotEmpty(f.Entries));

        var bad = json.Replace("\"HedgeSnatcher\"", "\"Orc\"");
        Assert.Null(LeagueHuntTable.Parse(bad, "bad", errors));
        Assert.Contains(errors, e => e.Contains("\"Orc\" is not a League Bestiary creature"));

        errors.Clear();
        var unknown = json.Replace("\"dailyHunts\"", "\"dailyHuntz\": 9, \"dailyHunts\"");
        Assert.Null(LeagueHuntTable.Parse(unknown, "unknown", errors));
        Assert.NotEmpty(errors);
        _out.WriteLine(string.Join("\n", errors));

        Assert.Same(defaults, LeagueHuntTable.Current);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AFirstReadRollsTheDailyHuntsAndOneGreatHuntAtTheTiersNumbers()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500); // rank index 1, capped by the League gate (unregistered: tier 0)
            var table = LeagueHuntTable.Current;

            for (var round = 0; round < 10; round++)
            {
                var data = ClusterFRequisitions.DataOf(pm, true);
                LeagueHunts.Refresh(pm, data, Noon, true);

                Assert.Equal(table.DailyHunts, data.Hunts.Count);
                Assert.Equal(data.Hunts.Count, data.Hunts.Select(h => h.FamilyKey).Distinct().Count());
                foreach (var h in data.Hunts)
                {
                    var family = Family(h.FamilyKey);
                    var tier = table.TierFor(h.Tier);
                    Assert.Equal("warriors", h.GuildKey);
                    Assert.Equal(ClusterFRequisitions.TierFor(pm, "warriors"), h.Tier);
                    Assert.True(family.LevelFrom <= tier.MaxLevel, $"{family.Key} starts at {family.LevelFrom}, over {tier.MaxLevel}");
                    Assert.False(h.Great);
                    Assert.Equal(table.CountFor(family), h.Needed);
                    Assert.Equal((tier.Renown, tier.Reputation, tier.Gold), (h.Renown, h.Reputation, h.Gold));
                    Assert.Equal("", h.LootBox);
                    Assert.Equal(0, h.Kills);
                }

                var g = data.GreatHunt;
                Assert.NotNull(g);
                Assert.True(g.Great);
                var gt = table.TierFor(g.Tier);
                var gf = Family(g.FamilyKey);
                Assert.True(gf.LevelFrom <= gt.MaxLevel + table.Great.LevelBonus);
                Assert.Equal(table.CountFor(gf) * table.Great.CountMultiplier, g.Needed);
                Assert.Equal(gt.Renown * table.Great.PayMultiplier, g.Renown);
                Assert.Equal(gt.Reputation * table.Great.PayMultiplier, g.Reputation);
                Assert.Equal(gt.Gold * table.Great.PayMultiplier, g.Gold);
                Assert.Equal("Silver", g.LootBox);
            }

            // Nothing open to the tier: no hunts, no great hunt, and nothing breaks.
            LeagueHunts.HasHome = _ => false;
            var empty = ClusterFRequisitions.DataOf(pm, true);
            LeagueHunts.Refresh(pm, empty, Noon, true);
            Assert.Empty(empty.Hunts);
            Assert.Null(empty.GreatHunt);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheDailyHuntsRollOverAtTheRefreshHourAndAFinishedOneStaysUntilThen()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = LeagueHunts.EnsureCurrent(pm);
            var ids = data.Hunts.Select(h => h.Id).ToList();
            var great = data.GreatHunt.Id;
            Assert.NotEmpty(ids);

            data.Hunts[0].Kills = data.Hunts[0].Needed; // finished
            data.Hunts[0].Paid = true;
            if (data.Hunts.Count > 1) data.Hunts[1].Kills = 1;

            Scope.At(Noon.AddHours(1));
            LeagueHunts.EnsureCurrent(pm);
            Assert.Equal(ids, data.Hunts.Select(h => h.Id).ToList());
            Assert.True(data.Hunts[0].Done);

            Scope.At(new DateTime(2026, 10, 21, 4, 59, 0, DateTimeKind.Utc)); // just before the refresh
            LeagueHunts.EnsureCurrent(pm);
            Assert.Equal(ids, data.Hunts.Select(h => h.Id).ToList());
            if (data.Hunts.Count > 1) Assert.Equal(1, data.Hunts[1].Kills);

            Scope.At(new DateTime(2026, 10, 21, 5, 0, 0, DateTimeKind.Utc)); // the refresh
            LeagueHunts.EnsureCurrent(pm);
            Assert.Equal(LeagueHuntTable.Current.DailyHunts, data.Hunts.Count);
            Assert.DoesNotContain(data.Hunts, h => ids.Contains(h.Id));
            Assert.All(data.Hunts, h => Assert.Equal(0, h.Kills));
            Assert.Equal(great, data.GreatHunt.Id); // Wednesday: the week has not turned
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheGreatHuntStaysAllWeekAndRollsOverOnTheResetDayAtTheRefreshHour()
    {
        var monday = new DateTime(2026, 10, 19, 5, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        var w = LeagueHunts.WeekIndex(monday, 5, DayOfWeek.Monday);
        Assert.Equal(w - 1, LeagueHunts.WeekIndex(monday.AddMinutes(-1), 5, DayOfWeek.Monday));
        Assert.Equal(w, LeagueHunts.WeekIndex(monday.AddDays(7).AddMinutes(-1), 5, DayOfWeek.Monday));
        Assert.Equal(w + 1, LeagueHunts.WeekIndex(monday.AddDays(7), 5, DayOfWeek.Monday));
        Assert.Equal(w, LeagueHunts.WeekIndex(Noon, 5, DayOfWeek.Monday));
        // Another reset day moves the boundary: a Thursday week turns on Thursday at the hour.
        var thursday = new DateTime(2026, 10, 22, 5, 0, 0, DateTimeKind.Utc);
        Assert.Equal(LeagueHunts.WeekIndex(thursday, 5, DayOfWeek.Thursday) - 1,
            LeagueHunts.WeekIndex(thursday.AddMinutes(-1), 5, DayOfWeek.Thursday));

        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = LeagueHunts.EnsureCurrent(pm);
            var great = data.GreatHunt;
            great.Kills = 5;
            Assert.Equal(new DateTime(2026, 10, 26, 5, 0, 0, DateTimeKind.Utc), LeagueHunts.NextWeek(Noon));

            foreach (var t in new[] { Noon.AddDays(2), new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 26, 4, 59, 0, DateTimeKind.Utc) })
            {
                Scope.At(t);
                LeagueHunts.EnsureCurrent(pm);
                Assert.Same(great, data.GreatHunt);
                Assert.Equal(5, data.GreatHunt.Kills);
            }

            Scope.At(new DateTime(2026, 10, 26, 5, 0, 0, DateTimeKind.Utc));
            LeagueHunts.EnsureCurrent(pm);
            Assert.NotEqual(great.Id, data.GreatHunt.Id);
            Assert.Equal(0, data.GreatHunt.Kills);
            Assert.Equal(w + 1, data.GreatHuntWeek);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AKillCountsOnItsFamilyAndTheLastKillPaysOnceAndTheGreatHuntPaysASilverBox()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500);
            var acct = ClusterFAccountPersistence.GetOrCreate(account);
            var snatchers = Hunt("hedge-snatchers");
            var crawlers = Hunt("ditch-crawlers");
            var great = Hunt("hedge-snatchers", great: true);
            Plant(pm, great, snatchers, crawlers);
            Assert.Equal(12, snatchers.Needed);
            Assert.Equal(36, great.Needed);

            var renown = acct.Renown;
            var lifetime = acct.LifetimeRenown;
            var standing = ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors");

            Assert.Equal(2, LeagueHunts.CountKill(pm, typeof(HedgeSnatcher)));
            Assert.Equal((1, 0, 1), (snatchers.Kills, crawlers.Kills, great.Kills));
            Assert.Equal(0, LeagueHunts.CountKill(pm, typeof(Moonmaw)));
            Assert.Equal(0, LeagueHunts.CountKill(pm, typeof(Orc)));
            Assert.Equal(1, LeagueHunts.CountKill(pm, typeof(DitchCrawler)));
            Assert.Equal(renown, acct.Renown);
            Assert.Equal(0, GoldIn(pm));

            for (var k = 2; k <= 12; k++)
                LeagueHunts.CountKill(pm, typeof(HedgeSnatcher));

            Assert.True(snatchers.Done);
            Assert.True(snatchers.Paid);
            Assert.Equal(renown + snatchers.Renown, acct.Renown);
            Assert.Equal(lifetime + snatchers.Renown, acct.LifetimeRenown);
            Assert.Equal(standing + snatchers.Reputation, ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors"));
            Assert.Equal(snatchers.Gold, GoldIn(pm));

            // A finished hunt counts and pays nothing more.
            Assert.Equal(1, LeagueHunts.CountKill(pm, typeof(HedgeSnatcher))); // the great hunt only
            Assert.Equal(12, snatchers.Kills);
            Assert.Equal(renown + snatchers.Renown, acct.Renown);
            Assert.False(LeagueHunts.Pay(pm, snatchers));
            Assert.Equal(snatchers.Gold, GoldIn(pm));

            Assert.Empty(pm.Backpack.Items.OfType<LootBox>());
            while (!great.Done)
                LeagueHunts.CountKill(pm, typeof(HedgeSnatcher));

            Assert.True(great.Paid);
            Assert.Equal(renown + snatchers.Renown + great.Renown, acct.Renown);
            Assert.Equal(snatchers.Gold + great.Gold, GoldIn(pm));
            var box = Assert.Single(pm.Backpack.Items.OfType<LootBox>());
            Assert.Contains("Silver", box.DefaultName);
            _out.WriteLine($"great hunt box: {box.DefaultName}");
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheMostDamageCountsAndThePartyNearbySharesItThroughTheRealDeathEvent()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var hitter = NewCharacter(account, 0);
        var heavy = NewCharacter(account, 1, new Point3D(Spot.X + 1, Spot.Y, Spot.Z));
        var near = NewCharacter(account, 2, new Point3D(Spot.X + 4, Spot.Y, Spot.Z));
        var far = NewCharacter(account, 3, new Point3D(Spot.X + 40, Spot.Y, Spot.Z));
        var stranger = NewCharacter(account, 4, new Point3D(Spot.X + 2, Spot.Y, Spot.Z));
        var all = new[] { hitter, heavy, near, far, stranger };
        var hunts = all.ToDictionary(p => p, p => Hunt("hedge-snatchers"));
        foreach (var p in all)
            Plant(p, null, hunts[p]);

        var party = new Party(heavy);
        heavy.Party = party;
        foreach (var p in new[] { near, far })
        {
            party.Members.Add(new PartyMemberInfo(p));
            p.Party = party;
        }

        HedgeSnatcher snatcher = null, pet = null;
        try
        {
            snatcher = new HedgeSnatcher();
            snatcher.MoveToWorld(Spot, Map.Trammel);
            snatcher.RegisterDamage(10, hitter);
            snatcher.RegisterDamage(30, heavy);

            var credited = LeagueHunts.Credited(snatcher);
            Assert.Equal(new[] { heavy, near }, credited.ToArray());

            snatcher.LastKiller = hitter; // the last blow is the hitter's: the count still goes by damage
            snatcher.Kill();

            Assert.Equal(0, hunts[hitter].Kills);
            Assert.Equal(1, hunts[heavy].Kills);
            Assert.Equal(1, hunts[near].Kills);
            Assert.Equal(0, hunts[far].Kills);
            Assert.Equal(0, hunts[stranger].Kills);

            // With no damage on record, the killer the bestiary reads counts.
            var second = new HedgeSnatcher();
            second.MoveToWorld(Spot, Map.Trammel);
            second.LastKiller = stranger;
            second.Kill();
            Assert.Equal(1, hunts[stranger].Kills);

            // A pet's death counts for nobody.
            pet = new HedgeSnatcher { Controlled = true, ControlMaster = hitter };
            pet.MoveToWorld(Spot, Map.Trammel);
            pet.RegisterDamage(30, heavy);
            Assert.Empty(LeagueHunts.Credited(pet));
            pet.LastKiller = heavy;
            pet.Kill();
            Assert.Equal(1, hunts[heavy].Kills);
        }
        finally
        {
            snatcher?.Corpse?.Delete();
            pet?.Corpse?.Delete();
            Cleanup(account, snatcher, pet);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void HuntFamilyAsksForAnOpenHuntsSlayerAndOtherwiseTheTableDraws()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        var original = LeagueHuntTable.Current;
        try
        {
            var table = LeagueHuntTable.Parse(LeagueHuntTable.ToJson(original), "copy", new List<string>());
            table.Family("pale-drifters").Slayer = "Silver";
            LeagueHuntTable.Current = table;

            var bardRow = RequisitionTable.Current.ThemesFor("bards").First(t => t.Slayer == "any");

            // No open hunt with a slayer: the table's families.
            Plant(pm, null, Hunt("hedge-snatchers"));
            Assert.Null(LeagueHunts.HuntFamilyFor(pm, "bards"));
            var families = RequisitionTable.Current.SlayerFamilyNames();
            for (var i = 0; i < 10; i++)
                Assert.Contains(ClusterFRequisitions.Make(pm, "bards", bardRow, 0, 0, Noon).Slayer, families);

            // An open pale drifter hunt: the row asks for its slayer.
            var drifters = Hunt("pale-drifters");
            Plant(pm, null, Hunt("hedge-snatchers"), drifters);
            Assert.Equal(SlayerName.Silver, LeagueHunts.HuntFamilyFor(pm, "bards"));
            for (var i = 0; i < 5; i++)
                Assert.Equal(SlayerName.Silver, ClusterFRequisitions.Make(pm, "bards", bardRow, 0, 0, Noon).Slayer);

            // Once that hunt is done it asks no more.
            drifters.Kills = drifters.Needed;
            Assert.Null(LeagueHunts.HuntFamilyFor(pm, "bards"));

            // The family's slayer comes from the file; a family no pinned slayer covers has none.
            Assert.NotNull(LeagueHunts.SlayerOf(table.Family("pale-drifters")));
            Assert.Null(LeagueHunts.SlayerOf(table.Family("moonmaws")));
        }
        finally
        {
            LeagueHuntTable.Current = original;
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheRecordSurvivesAWriteAndAReadAndAVersion0RecordLoadsWithNoHunts()
    {
        var data = new CharacterRequisitionData { RolledDay = 292, HuntDay = 292, GreatHuntWeek = 41 };
        data.Requests.Add(new RequisitionEntry { GuildKey = "warriors", Kind = RequisitionKind.AnyMagic, Category = "any", MinProperties = 2, RolledAt = Noon });
        var a = LeagueHunts.Make(Family("moonmaws"), "warriors", 2, false, Noon);
        a.Kills = 7;
        var b = LeagueHunts.Make(Family("hedge-snatchers"), "arcane", 0, false, Noon.AddHours(-3));
        b.Kills = b.Needed;
        b.Paid = true;
        data.Hunts.Add(a);
        data.Hunts.Add(b);
        data.GreatHunt = LeagueHunts.Make(Family("the-siegebreaker"), "thieves", 4, true, Noon);
        data.GreatHunt.Kills = 2;

        var writer = new BufferWriter(true);
        data.Serialize(writer);
        var reader = new BufferReader(writer.Buffer);
        var copy = new CharacterRequisitionData(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal((data.RolledDay, data.HuntDay, data.GreatHuntWeek), (copy.RolledDay, copy.HuntDay, copy.GreatHuntWeek));
        Assert.Single(copy.Requests);

        static object Fields(HuntEntry h) =>
            (h.Id, h.FamilyKey, h.GuildKey, h.Tier, h.Great, h.Needed, h.Kills, h.Renown, h.Reputation, h.Gold, h.LootBox, h.Paid, h.RolledAt);

        Assert.Equal(data.Hunts.Select(Fields), copy.Hunts.Select(Fields));
        Assert.Equal(Fields(data.GreatHunt), Fields(copy.GreatHunt));

        // No great hunt is written as one flag.
        var none = new CharacterRequisitionData { HuntDay = 5 };
        var w2 = new BufferWriter(true);
        none.Serialize(w2);
        var none2 = new CharacterRequisitionData(new BufferReader(w2.Buffer));
        Assert.Null(none2.GreatHunt);
        Assert.Equal(5, none2.HuntDay);

        // A version 0 record (cc-P150): version, rolled day, request count. Reads with no hunts, writes as version 1.
        var v0 = new BufferWriter(true);
        v0.Write(0);
        v0.Write(290);
        v0.Write(0);
        var bytes = v0.Buffer.AsSpan(0, (int)v0.Position).ToArray();
        var oldReader = new BufferReader(bytes);
        var old = new CharacterRequisitionData(oldReader);
        Assert.Equal(bytes.Length, oldReader.Position);
        Assert.Equal(290, old.RolledDay);
        Assert.Equal((-1, -1), (old.HuntDay, old.GreatHuntWeek));
        Assert.Empty(old.Hunts);
        Assert.Null(old.GreatHunt);

        var again = new BufferWriter(true);
        old.Serialize(again);
        Assert.Equal(1, BitConverter.ToInt32(again.Buffer, 0));
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheBoardShowsEachHuntsProgressAndTheCommandSaysOneLinePerHunt()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500);
            var snatchers = Hunt("hedge-snatchers");
            snatchers.Kills = 4;
            var great = Hunt("the-hollow-oak", great: true);
            Plant(pm, great, snatchers);

            var gump = new RequisitionsBoardGump(pm, "warriors", 0);
            var text = string.Join("\n", GumpText(gump));
            _out.WriteLine(text);
            Assert.Contains("League hunts", text);
            Assert.Contains("Cull 12 hedge snatchers: 4 of 12", text);
            Assert.Contains("Slay the Hollow Oak 3 times: 0 of 3", text);
            Assert.Contains("Great hunt:", text);
            Assert.Contains("System's Silver Loot Box", text);
            Assert.Contains($"{snatchers.Renown} Renown", text);

            var lines = LeagueHunts.Lines(pm);
            _out.WriteLine(string.Join("\n", lines));
            Assert.Equal(4, lines.Count);
            Assert.StartsWith("[Hunt] 2 hunts", lines[0]);
            Assert.Equal($"[Hunt] daily|hedge-snatchers|warriors|{snatchers.Tier}|4|12|{snatchers.Renown}|{snatchers.Reputation}|{snatchers.Gold}|-|open", lines[1]);
            Assert.StartsWith("[Hunt] great|the-hollow-oak|warriors|", lines[2]);
            Assert.EndsWith("|Silver|open", lines[2]);
            Assert.Equal("[Hunt] end", lines[3]);

            // Turned off: the board shows no hunts section.
            var off = LeagueHuntTable.Parse(LeagueHuntTable.ToJson(LeagueHuntTable.Current), "off", new List<string>());
            off.Enabled = false;
            LeagueHuntTable.Current = off;
            Assert.DoesNotContain("League hunts", string.Join("\n", GumpText(new RequisitionsBoardGump(pm, "warriors", 0))));
        }
        finally
        {
            Cleanup(account);
        }
    }

    private static IEnumerable<string> GumpText(Server.Gumps.Gump gump)
    {
        foreach (var e in gump.Entries)
        {
            switch (e)
            {
                case Server.Gumps.GumpLabel l: yield return l.Text; break;
                case Server.Gumps.GumpHtml h: yield return h.Text; break;
            }
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void AHuntPaysOnlyRenownStandingGoldAndALeagueShopBoxAndTheNumbersComeFromTheFile()
    {
        using var scope = new Scope(Noon);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            // The pay comes from the file: tier 0's gold, the cull count and the great hunt's box, edited.
            var table = LeagueHuntTable.Parse(LeagueHuntTable.ToJson(LeagueHuntTable.Current), "edited", new List<string>());
            table.Tiers.First(t => t.Tier == 0).Gold = 777;
            table.Counts.Cull = 3;
            table.Great.LootBox = "";
            LeagueHuntTable.Current = table;
            var h = Hunt("hedge-snatchers");
            Assert.Equal((777, 3), (h.Gold, h.Needed));
            Assert.Equal("", Hunt("hedge-snatchers", great: true).LootBox);

            // Everything a finished hunt puts in the pack: gold, and a League shop box for the great hunt.
            LeagueHuntTable.Current = LeagueHuntTable.Defaults;
            var great = Hunt("hedge-snatchers", great: true);
            Plant(pm, great, Hunt("ditch-crawlers"));
            var before = pm.Backpack.Items.ToList();
            while (!great.Done)
                LeagueHunts.CountKill(pm, typeof(HedgeSnatcher));
            var added = pm.Backpack.Items.Except(before).ToList();
            Assert.All(added, i => Assert.True(i is Gold or LootBox, $"a hunt paid a {i.GetType().Name}"));

            // No Silver box ever holds a power scroll, a stat or skill scroll, a bless deed or a magic item.
            string[] forbidden = ["PowerScroll", "StatCapScroll", "ScrollOfTranscendence", "ScrollOfAlacrity", "SkillBall", "BlessDeed", "ClothingBlessDeed"];
            for (var i = 0; i < 40; i++)
            {
                var box = LeagueShop.MakeBox(pm, LeagueShop.BoxTier.Silver);
                foreach (var item in box.Contents.ToList())
                {
                    var name = item.GetType().Name;
                    Assert.DoesNotContain(name, forbidden);
                    Assert.False(item is BaseWeapon or BaseArmor or BaseJewel, $"a Silver box held {name}");
                }

                foreach (var item in box.Contents.ToList())
                    item.Delete();
                box.Delete();
            }
        }
        finally
        {
            Cleanup(account);
        }
    }
}
