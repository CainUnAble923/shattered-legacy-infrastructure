// RequisitionsVerification.cs
//
// cc-P150 (F-47, F-48): the requisitions board, the Appraiser and Leads. Notes in shard-migration
// notes/cc-P150-requisitions-and-leads.md. Each fact was seen red first against a deliberate break (the notes' red table).
//
// Facts:
//   1. The default table validates, writes as JSON and reads back the same; a file with an unknown property is refused
//      whole, the error names it, and the table in force is kept.
//   2. A first read rolls three requests for every guild that posts and none for the Custodians; every request names a
//      known kind, a catalogue property or a slayer family, and pays its tier's numbers.
//   3. The tier is the character's rank index in the guild, capped by the League gate; a non-member rolls at tier 0.
//   4. The set rolls over once a day at the refresh hour: the same ids an hour later and just before the hour, all new
//      ones just after, with the slots back to three.
//   5. A turn-in consumes a matching item and pays the tier's Renown (and lifetime Renown), the guild's standing and
//      gold equal to the Appraiser's price; the slot stays empty until the refresh.
//   6. A turn-in refuses the wrong kind, a property under the floor and too few properties, and pays nothing.
//   7. A slayer request takes the family or its group's super slayer and nothing else; a row marked "any" asks the hunt
//      seam first.
//   8. The Appraiser pays by count and intensity near shop levels: nothing for a plain item, a floor for one property,
//      more for five, capped; selling to the NPC pays exactly that and deletes the item; the board's gold pay is the same.
//   9. A Lead: a qualifying kill gets the fame band's extra roll (the 2x), the pity makes the 40th kill certain, a drop
//      fills the request and lands in the corpse through the real death event; kills outside the area, of another group,
//      under the fame floor, or by a pet count nothing; without a Lead nothing drops.
//  10. Quiet expiry: a lapsed Lead and an unfilled request leave Renown, lifetime Renown and every guild's standing
//      exactly as they were; a request with a live Lead survives the daily refresh and goes once the Lead has lapsed;
//      nothing in the account record is called Infamy.
//  11. The record survives a write and a read with every field compared, and a version 19 save loads with no board and
//      is written again as version 20.
//  12. The board gump lists the guild's requests with a Turn in button each; turning in needs a counter within reach;
//      [requisitions says one line per request between a header and "end".

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RequisitionsVerification
{
    private readonly ITestOutputHelper _out;

    public RequisitionsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        RequisitionTable.Current = RequisitionTable.Defaults;
        ClusterFRequisitions.HuntFamily = null;
    }

    // ---------------------------------------------------------------- helpers

    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Scope : IDisposable
    {
        private readonly Func<DateTime> _clock = ClusterFRequisitions.Clock;
        private readonly Func<double> _chance = ClusterFRequisitions.Chance;

        public Scope(DateTime? now = null, double? chance = null)
        {
            if (now != null) At(now.Value);
            if (chance != null) Roll(chance.Value);
        }

        public static void At(DateTime now) => ClusterFRequisitions.Clock = () => now;

        public static void Roll(double chance) => ClusterFRequisitions.Chance = () => chance;

        public void Dispose()
        {
            ClusterFRequisitions.Clock = _clock;
            ClusterFRequisitions.Chance = _chance;
            ClusterFRequisitions.HuntFamily = null;
            RequisitionTable.Current = RequisitionTable.Defaults;
        }
    }

    private static Account NewAccount() => new($"p150{Guid.NewGuid():N}"[..16], "p150-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0, Point3D? at = null)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(at ?? new Point3D(3460, 2604, 18), Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account, params IEntity[] extras)
    {
        foreach (var e in extras)
            e?.Delete();

        for (var i = 0; i < account.Length; i++)
            if (account[i] is PlayerMobile pm)
            {
                pm.Backpack?.Delete();
                pm.Delete();
            }

        var data = ClusterFAccountPersistence.Get(account);
        data?.ClearGuildData();
        data?.ClearRequisitionData();
        data?.ClearLeagueData();
        Accounts.Remove(account);
    }

    private static CharacterGuildData Join(PlayerMobile pm, string guild, int standing)
    {
        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        data.JoinedGuilds.Add(guild);
        data.AddReputation(guild, standing);
        return data;
    }

    private static RequisitionEntry Plant(PlayerMobile pm, RequisitionEntry e)
    {
        var data = ClusterFRequisitions.EnsureCurrent(pm);
        data.Requests.RemoveAll(x => x.GuildKey == e.GuildKey);
        data.Requests.Add(e);
        return e;
    }

    private static RequisitionEntry SwordWithHci(int tier = 1, int minIntensity = 30, int minProperties = 1) => new()
    {
        GuildKey = "warriors", Kind = RequisitionKind.Property, Category = "sword", Property = "Hit Chance Increase",
        MinIntensity = minIntensity, MinProperties = minProperties, Tier = tier, Renown = 20, Reputation = 60, RolledAt = Noon,
    };

    private static Longsword Sword(PlayerMobile pm, int hci, int dci = 0, int luck = 0)
    {
        var w = new Longsword();
        w.Attributes.AttackChance = hci;
        w.Attributes.DefendChance = dci;
        w.Attributes.Luck = luck;
        pm.Backpack.DropItem(w);
        return w;
    }

    // FindItemsByType is a pooled enumerator (no LINQ on it).
    private static int GoldIn(PlayerMobile pm)
    {
        var total = 0;
        foreach (var g in pm.Backpack.FindItemsByType<Gold>())
        {
            total += g.Amount;
        }

        return total;
    }

    private static int Guilds() => RequisitionsBoard.Guilds().Count;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheDefaultTableValidatesRoundTripsAndRefusesABadFileWhole()
    {
        using var scope = new Scope();
        var errors = new List<string>();
        RequisitionTable.Validate(RequisitionTable.Defaults, errors);
        Assert.Empty(errors);

        var json = RequisitionTable.ToJson(RequisitionTable.Defaults);
        var copy = RequisitionTable.Parse(json, "test", errors);
        Assert.Empty(errors);
        Assert.NotNull(copy);
        Assert.Equal(RequisitionTable.Defaults.Themes.Count, copy.Themes.Count);
        Assert.Equal(RequisitionTable.Defaults.Areas.Count, copy.Areas.Count);
        Assert.Equal(RequisitionTable.Defaults.Tiers.Count, copy.Tiers.Count);
        Assert.Equal(RequisitionTable.Defaults.Leads.PityKills, copy.Leads.PityKills);
        Assert.Equal(RequisitionTable.Defaults.Appraiser.PerProperty, copy.Appraiser.PerProperty);
        Assert.Empty(RequisitionTable.Defaults.ThemesFor("custodians"));
        Assert.Equal(17, RequisitionTable.Defaults.Themes.Select(t => t.Guild).Distinct().Count());

        var before = RequisitionTable.Current;
        var bad = json.Replace("\"Hit Chance Increase\"", "\"Hit Chance Incrase\"");
        Assert.NotEqual(json, bad);
        var refused = RequisitionTable.Parse(bad, "bad", errors);
        Assert.Null(refused);
        Assert.Contains(errors, e => e.Contains("Hit Chance Incrase"));
        Assert.Same(before, RequisitionTable.Current);
        _out.WriteLine(string.Join("\n", errors.Take(3)));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AFirstReadRollsThreeRequestsPerPostingGuildAndNoneForTheCustodians()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Equal(ClusterFRequisitions.DayIndex(Noon, 5), data.RolledDay);
            Assert.Equal(3 * Guilds(), data.Requests.Count);
            Assert.Equal(0, data.CountFor("custodians"));

            foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
                Assert.Equal(RequisitionTable.Current.ThemesFor(def.Key).Count > 0 ? 3 : 0, data.CountFor(def.Key));

            var tier0 = RequisitionTable.Current.TierFor(0);
            foreach (var e in data.Requests)
            {
                Assert.True(RequisitionCategories.Known(e.Category), e.Category);
                Assert.Equal(0, e.Tier); // unregistered, outside every guild
                Assert.Equal(tier0.Renown, e.Renown);
                Assert.Equal(tier0.Reputation, e.Reputation);
                switch (e.Kind)
                {
                    case RequisitionKind.Property:
                        Assert.True(RequisitionProperties.IsKnownProperty(e.Property), e.Property);
                        Assert.Equal(tier0.MinIntensity, e.MinIntensity);
                        break;
                    case RequisitionKind.Slayer:
                        Assert.NotEqual(SlayerName.None, e.Slayer);
                        break;
                    default:
                        Assert.True(e.MinProperties >= 1);
                        break;
                }
            }

            _out.WriteLine($"{data.Requests.Count} requests over {Guilds()} guilds; first: {ClusterFRequisitions.Describe(data.Requests[0])}");
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheTierIsTheGuildRankCappedByTheLeagueGate()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var guild = Join(pm, "warriors", 5_000); // Journeyman: rank index 2
            Assert.Equal(2, ClusterFGuildSystem.GuildRankIndex("warriors", guild));

            Assert.Equal(0, ClusterFRequisitions.TierFor(pm, "warriors", guild)); // unregistered: gate 0
            ClusterFLeagueRanks.SetRank(pm, 2);                                       // Dull Copper: gate 1
            Assert.Equal(1, ClusterFRequisitions.TierFor(pm, "warriors", guild));
            ClusterFLeagueRanks.SetRank(pm, 8);                                       // Verite: gate 3
            Assert.Equal(2, ClusterFRequisitions.TierFor(pm, "warriors", guild));   // the guild rank caps
            Assert.Equal(0, ClusterFRequisitions.TierFor(pm, "arcane", guild));     // not a member
            guild.AddReputation("warriors", 10_000);                                  // 15,000: Master, rank index 3
            Assert.Equal(3, ClusterFRequisitions.TierFor(pm, "warriors", guild));
            ClusterFLeagueRanks.SetRank(pm, 17);                                      // Celestial: gate 5
            Assert.Equal(3, ClusterFRequisitions.TierFor(pm, "warriors", guild));

            var data = ClusterFRequisitions.DataOf(pm, true);
            ClusterFRequisitions.Refresh(pm, data, Noon, true);
            var tier3 = RequisitionTable.Current.TierFor(3);
            foreach (var e in data.Requests.Where(e => e.GuildKey == "warriors"))
            {
                Assert.Equal(3, e.Tier);
                Assert.Equal(tier3.Renown, e.Renown);
                Assert.Equal(tier3.Reputation, e.Reputation);
            }

            foreach (var e in data.Requests.Where(e => e.GuildKey == "arcane"))
                Assert.Equal(0, e.Tier);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheSetRollsOverOnceADayAtTheRefreshHourAndNotBefore()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = ClusterFRequisitions.EnsureCurrent(pm);
            var day = data.RolledDay;
            var ids = data.Requests.Select(e => e.Id).ToHashSet();

            Scope.At(Noon.AddHours(10));
            ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Equal(ids, data.Requests.Select(e => e.Id).ToHashSet());

            Scope.At(new DateTime(2026, 10, 21, 4, 59, 0, DateTimeKind.Utc)); // before 05:00 UTC
            ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Equal(day, data.RolledDay);
            Assert.Equal(ids, data.Requests.Select(e => e.Id).ToHashSet());

            Scope.At(new DateTime(2026, 10, 21, 5, 1, 0, DateTimeKind.Utc));
            ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Equal(day + 1, data.RolledDay);
            Assert.Equal(3 * Guilds(), data.Requests.Count);
            Assert.Empty(ids.Intersect(data.Requests.Select(e => e.Id)));
            _out.WriteLine($"day {day} -> {data.RolledDay}, next refresh {ClusterFRequisitions.NextRefresh():u}");
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ATurnInConsumesTheItemAndPaysRenownStandingAndTheAppraiserPrice()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500);
            var acct = ClusterFAccountPersistence.GetOrCreate(account);
            var e = Plant(pm, SwordWithHci());
            var sword = Sword(pm, 15); // 15 of 45: 33%
            var price = AppraiserPricing.Price(sword);
            Assert.True(price > 0);

            var renown = acct.Renown;
            var lifetime = acct.LifetimeRenown;
            var standing = ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors");

            Assert.True(ClusterFRequisitions.TurnIn(pm, e, sword, out var message), message);
            _out.WriteLine(message);

            Assert.True(sword.Deleted);
            Assert.Equal(renown + 20, acct.Renown);
            Assert.Equal(lifetime + 20, acct.LifetimeRenown);
            Assert.Equal(standing + 60, ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors"));
            Assert.Equal(price, GoldIn(pm));

            var data = ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Null(data.Find(e.Id));
            Assert.Equal(0, data.CountFor("warriors")); // Plant left only this one; the slot stays empty today

            Assert.False(ClusterFRequisitions.TurnIn(pm, e, Sword(pm, 15), out var again));
            Assert.Contains("taken down", again);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ATurnInRefusesTheWrongKindALowIntensityAndTooFewPropertiesAndPaysNothing()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500);
            var acct = ClusterFAccountPersistence.GetOrCreate(account);
            var renown = acct.Renown;
            var standing = ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors");

            var e = Plant(pm, SwordWithHci(minIntensity: 30, minProperties: 2));

            var mace = new WarMace();
            mace.Attributes.AttackChance = 45;
            pm.Backpack.DropItem(mace);
            Assert.False(ClusterFRequisitions.TurnIn(pm, e, mace, out var kind));
            Assert.Contains("a sword", kind);

            var weak = Sword(pm, 5, dci: 10); // 11%
            Assert.False(ClusterFRequisitions.TurnIn(pm, e, weak, out var low));
            Assert.Contains("11%", low);

            var lone = Sword(pm, 45); // one property only
            Assert.False(ClusterFRequisitions.TurnIn(pm, e, lone, out var few));
            Assert.Contains("1 property", few);

            var plain = new Longsword();
            pm.Backpack.DropItem(plain);
            Assert.False(ClusterFRequisitions.TurnIn(pm, e, plain, out var none));
            Assert.Contains("no Hit Chance Increase", none);

            var onFloor = new Longsword();
            onFloor.Attributes.AttackChance = 45;
            onFloor.Attributes.DefendChance = 10;
            onFloor.MoveToWorld(pm.Location, pm.Map);
            Assert.False(ClusterFRequisitions.TurnIn(pm, e, onFloor, out var where));
            Assert.Contains("backpack", where);

            Assert.False(mace.Deleted);
            Assert.False(weak.Deleted);
            Assert.False(lone.Deleted);
            Assert.Equal(renown, acct.Renown);
            Assert.Equal(standing, ClusterFAccountPersistence.GetGuild(pm).GetReputation("warriors"));
            Assert.Equal(0, GoldIn(pm));
            Assert.NotNull(ClusterFRequisitions.EnsureCurrent(pm).Find(e.Id));

            var good = Sword(pm, 45, dci: 10);
            Assert.True(ClusterFRequisitions.TurnIn(pm, e, good, out var ok), ok);
            onFloor.Delete();
            _out.WriteLine($"{kind} | {low} | {few} | {none} | {where}");
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ASlayerRequestTakesTheFamilyOrItsSuperSlayerAndTheHuntSeamNamesTheFamily()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var e = new RequisitionEntry
            {
                GuildKey = "warriors", Kind = RequisitionKind.Slayer, Category = "weapon", Slayer = SlayerName.OrcSlaying,
                MinProperties = 1, Tier = 0, Renown = 10, Reputation = 40, RolledAt = Noon,
            };

            var orc = new Longsword { Slayer = SlayerName.OrcSlaying };
            var repond = new Longsword { Slayer2 = SlayerName.Repond };
            var silver = new Longsword { Slayer = SlayerName.Silver };
            Assert.True(RequisitionMatch.Check(e, orc, out _));
            Assert.True(RequisitionMatch.Check(e, repond, out _), "the humanoid super slayer covers orcs");
            Assert.False(RequisitionMatch.Check(e, silver, out var why));
            Assert.Contains("orcs", why);
            Assert.False(RequisitionMatch.Check(e, new Longsword(), out _));

            var lute = new Lute { Slayer = SlayerName.Silver };
            var bardRow = RequisitionTable.Current.ThemesFor("bards").First(t => t.Category == "instrument");
            ClusterFRequisitions.HuntFamily = (_, _) => SlayerName.Silver;
            var bard = ClusterFRequisitions.Make(pm, "bards", bardRow, 0, 0, Noon);
            Assert.Equal(RequisitionKind.Slayer, bard.Kind);
            Assert.Equal(SlayerName.Silver, bard.Slayer);
            Assert.True(RequisitionMatch.Check(bard, lute, out _));
            Assert.False(RequisitionMatch.Check(bard, orc, out _), "an instrument is asked for");

            ClusterFRequisitions.HuntFamily = (_, _) => SlayerName.Fey;
            Assert.Equal(SlayerName.Fey, ClusterFRequisitions.Make(pm, "bards", bardRow, 0, 0, Noon).Slayer);
            ClusterFRequisitions.HuntFamily = null;
            var families = RequisitionTable.Current.SlayerFamilyNames();
            for (var i = 0; i < 20; i++)
                Assert.Contains(ClusterFRequisitions.Make(pm, "bards", bardRow, 0, 0, Noon).Slayer, families);

            orc.Delete(); repond.Delete(); silver.Delete(); lute.Delete();
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheAppraiserPaysByCountAndIntensityNearShopLevelsAndSellingPaysExactlyThat()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        var npc = new Appraiser();
        try
        {
            var rules = RequisitionTable.Current.Appraiser;
            Assert.Equal(0, AppraiserPricing.Price(new Longsword()));

            var one = Sword(pm, 15); // 33% of 45
            Assert.Equal(rules.BaseFor("weapon") + rules.PerProperty + (int)Math.Round(rules.PerIntensityPoint * 33), AppraiserPricing.Price(one));

            var five = new Longsword();
            five.Attributes.AttackChance = 45;
            five.Attributes.DefendChance = 45;
            five.Attributes.WeaponDamage = 50;
            five.Attributes.WeaponSpeed = 60;
            five.Attributes.Luck = 100;
            pm.Backpack.DropItem(five);
            Assert.Equal(5, RequisitionProperties.PropertyCount(five));
            Assert.Equal(500, RequisitionProperties.TotalIntensity(five));
            Assert.Equal(400, AppraiserPricing.Price(five));

            five.Attributes.Luck = 300;
            Assert.Equal(rules.Max, AppraiserPricing.Price(five));

            var book = new Spellbook();
            book.Attributes.LowerManaCost = 8; // 20% of 40
            pm.Backpack.DropItem(book);
            Assert.Equal(1, RequisitionProperties.PropertyCount(book));
            Assert.Equal(rules.BaseFor("spellbook") + rules.PerProperty + 10, AppraiserPricing.Price(book));
            _out.WriteLine($"one: {AppraiserPricing.Explain(one)}; book: {AppraiserPricing.Explain(book)}");

            npc.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), pm.Map);
            var price = AppraiserPricing.Price(one);
            Assert.True(Appraiser.Sell(pm, npc, one, out var sold), sold);
            Assert.True(one.Deleted);
            Assert.Equal(price, GoldIn(pm));

            var plain = new Longsword();
            pm.Backpack.DropItem(plain);
            Assert.False(Appraiser.Sell(pm, npc, plain, out var refused));
            Assert.Contains("shopkeeper", refused);
            Assert.False(plain.Deleted);

            npc.MoveToWorld(new Point3D(pm.X + 10, pm.Y, pm.Z), pm.Map);
            Assert.False(Appraiser.Sell(pm, npc, book, out var far));
            Assert.Contains("reach", far);
        }
        finally
        {
            Cleanup(account, npc);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void ALeadGivesTheExtraRollAndThePityMakesTheFortiethKillCertain()
    {
        using var scope = new Scope(Noon, 0.999);
        var account = NewAccount();
        var spot = new Point3D(5610, 1010, 0);
        var region = new Region("Despise", Map.Trammel, 60, new Rectangle3D(new Point3D(5600, 1000, -128), new Point3D(5620, 1020, 127)));
        region.Register();
        var pm = NewCharacter(account, at: spot);
        var orc = new Orc { Fame = 2500 };
        try
        {
            Assert.True(Region.Find(spot, Map.Trammel).IsPartOf("Despise"));
            var rules = RequisitionTable.Current.Leads;
            Assert.Equal(40, rules.PityKills);
            var extra = RequisitionTable.Current.ExtraRollPercent(2500);
            Assert.Equal(5, extra);
            Assert.Equal(extra, RequisitionLeads.DropChancePercent(2500, 1));     // the extra roll: the fame band's chance again
            Assert.Equal(100, RequisitionLeads.DropChancePercent(2500, 40));     // the pity: certain by the 40th kill
            Assert.True(RequisitionLeads.DropChancePercent(2500, 20) > extra);

            var e = Plant(pm, SwordWithHci());
            e.LeadArea = "despise";
            e.LeadGroup = "Repond";
            e.LeadExpires = Noon.AddDays(3);

            orc.MoveToWorld(spot, Map.Trammel);
            Assert.True(RequisitionLeads.Qualifies(e, orc, out var why), why);

            // Kills 1 to 39 at a 99.9% roll: counted, nothing drops.
            for (var k = 1; k < 40; k++)
                Assert.Empty(RequisitionLeads.HandleKill(pm, orc));
            Assert.Equal(39, e.LeadKills);

            // The 40th: certain, through the real death event, into the corpse.
            orc.LastKiller = pm;
            orc.Kill();
            var corpse = Assert.IsAssignableFrom<Corpse>(orc.Corpse);
            // cc-P203: a stock Orc packs a Scimitar or a Katana 2 times in 20 (pinned Orc.cs:42-52) and its loot pack
            // (LootPack.Meager) can roll a magic weapon, so the corpse can hold a plain sword too; the drop is the sword
            // that fills the request, not the first sword found (the first run of cc-P203 failed on the orc's own blade).
            var swords = corpse.Items.Where(i => RequisitionCategories.Get("sword").Matches(i)).ToList();
            Assert.NotEmpty(swords);
            var drop = swords.FirstOrDefault(i => RequisitionMatch.Check(e, i, out _));
            Assert.True(drop != null, "no sword in the corpse fills the request: " + string.Join("; ", swords.Select(s =>
            {
                RequisitionMatch.Check(e, s, out var why);
                return $"{s.GetType().Name}: {why}";
            })));
            Assert.True(RequisitionMatch.Check(e, drop, out var fills), fills);
            Assert.Equal(0, e.LeadKills);
            Assert.True(RequisitionProperties.IntensityOf(drop, "Hit Chance Increase") >= 30);
            _out.WriteLine($"the 40th kill dropped {drop.GetType().Name}: HCI {RequisitionProperties.IntensityOf(drop, "Hit Chance Increase")}%, {RequisitionProperties.PropertyCount(drop)} properties");
            corpse.Delete();

            // The extra roll alone: a 4% roll under the 5% band drops on the first kill.
            Scope.Roll(0.04);
            var second = new Orc { Fame = 2500 };
            second.MoveToWorld(spot, Map.Trammel);
            var dropped = RequisitionLeads.HandleKill(pm, second);
            Assert.Single(dropped);
            Assert.True(RequisitionMatch.Check(e, dropped[0], out _));
            Assert.True(dropped[0].IsChildOf(pm.Backpack), "no corpse: to the backpack");

            // Nothing counts: outside the area, another group, under the fame floor, a pet; and no Lead, no drop.
            Scope.Roll(0.0);
            e.LeadKills = 0;
            second.MoveToWorld(new Point3D(5650, 1050, 0), Map.Trammel);
            Assert.Empty(RequisitionLeads.HandleKill(pm, second));
            second.MoveToWorld(spot, Map.Trammel);
            var zombie = new Zombie { Fame = 2500 };
            zombie.MoveToWorld(spot, Map.Trammel);
            Assert.Empty(RequisitionLeads.HandleKill(pm, zombie));
            second.Fame = 100;
            Assert.Empty(RequisitionLeads.HandleKill(pm, second));
            second.Fame = 2500;
            second.Controlled = true;
            Assert.Empty(RequisitionLeads.HandleKill(pm, second));
            second.Controlled = false;
            Assert.Equal(0, e.LeadKills);

            e.ClearLead();
            Assert.Empty(RequisitionLeads.HandleKill(pm, second));

            second.Delete();
            zombie.Delete();
        }
        finally
        {
            region.Unregister();
            Cleanup(account, orc);
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void ALapsedLeadAndAnUnfilledRequestChangeNoRenownOrStandingAndTheLeadRequestOutlivesTheDay()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            Join(pm, "warriors", 1_500);
            Join(pm, "arcane", 7_000);
            var acct = ClusterFAccountPersistence.GetOrCreate(account);
            acct.GrantRenown(300, RenownSource.Achievement);
            var guild = ClusterFAccountPersistence.GetGuild(pm);
            var renown = acct.Renown;
            var lifetime = acct.LifetimeRenown;
            var standings = guild.GuildReputation.ToDictionary(kv => kv.Key, kv => kv.Value);

            var data = ClusterFRequisitions.EnsureCurrent(pm);
            var lead = data.Requests.First(r => r.Kind == RequisitionKind.Property);
            Assert.True(RequisitionLeads.Attach(pm, lead, 0, Noon));
            Assert.True(lead.LeadLive(Noon));
            var others = data.Requests.Where(r => r != lead).Select(r => r.Id).ToHashSet();

            Scope.At(Noon.AddDays(1));
            ClusterFRequisitions.EnsureCurrent(pm);
            Assert.NotNull(data.Find(lead.Id));                                   // the Lead's request stays
            Assert.Empty(others.Intersect(data.Requests.Select(r => r.Id)));     // every other one rolled over
            Assert.Equal(3 * Guilds(), data.Requests.Count);

            Scope.At(Noon.AddDays(3).AddHours(1));
            Assert.False(lead.LeadLive(ClusterFRequisitions.Clock()));
            Assert.Equal(1, RequisitionLeads.Sweep(data, ClusterFRequisitions.Clock()));
            Assert.False(lead.HasLead);
            ClusterFRequisitions.EnsureCurrent(pm);
            Assert.Null(data.Find(lead.Id));                                      // gone with the day, quietly

            Assert.Equal(renown, acct.Renown);
            Assert.Equal(lifetime, acct.LifetimeRenown);
            Assert.Equal(standings.Count, guild.GuildReputation.Count);
            foreach (var (key, value) in standings)
                Assert.Equal(value, guild.GetReputation(key));
            Assert.Equal(0, GoldIn(pm));

            // Infamy does not exist (F-37 says to build it later): nothing in the account record carries the name.
            foreach (var p in typeof(ClusterFAccountData).GetProperties().Concat(typeof(CharacterGuildData).GetProperties()))
                Assert.DoesNotContain("Infamy", p.Name, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void TheRecordSurvivesAWriteAndAReadAndAVersion19SaveLoadsWithNoBoard()
    {
        var original = new ClusterFAccountData();
        var first = original.GetOrCreateRequisitionData((Serial)0x1234u);
        first.RolledDay = 297;
        first.Requests.Add(new RequisitionEntry
        {
            GuildKey = "warriors", Kind = RequisitionKind.Property, Category = "sword", Property = "Hit Chance Increase",
            MinIntensity = 30, MinProperties = 2, Tier = 1, Renown = 20, Reputation = 60, RolledAt = Noon,
            LeadArea = "despise", LeadGroup = "Repond", LeadExpires = Noon.AddDays(3), LeadKills = 7,
        });
        first.Requests.Add(new RequisitionEntry
        {
            GuildKey = "bards", Kind = RequisitionKind.Slayer, Category = "instrument", Slayer = SlayerName.Fey,
            MinProperties = 1, Tier = 3, Renown = 55, Reputation = 130, RolledAt = Noon.AddHours(-2),
        });
        var second = original.GetOrCreateRequisitionData((Serial)0x5678u);
        second.RolledDay = 296;
        second.Requests.Add(new RequisitionEntry { GuildKey = "thieves", Kind = RequisitionKind.AnyMagic, Category = "any", MinProperties = 3, Tier = 2, Renown = 35, Reputation = 90, RolledAt = Noon });

        var writer = new BufferWriter(true);
        original.Serialize(writer);
        var reader = new BufferReader(writer.Buffer);
        var copy = new ClusterFAccountData(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(2, copy.RequisitionDataCount);

        foreach (var serial in new[] { (Serial)0x1234u, (Serial)0x5678u })
        {
            var o = original.GetRequisitionData(serial);
            var c = copy.GetRequisitionData(serial);
            Assert.NotNull(c);
            Assert.Equal(o.RolledDay, c.RolledDay);
            Assert.Equal(o.Requests.Count, c.Requests.Count);
            for (var i = 0; i < o.Requests.Count; i++)
            {
                var a = o.Requests[i];
                var b = c.Requests[i];
                Assert.Equal(
                    (a.Id, a.GuildKey, a.Kind, a.Category, a.Property, a.MinIntensity, a.MinProperties, a.Slayer, a.Tier, a.Renown, a.Reputation, a.RolledAt, a.LeadArea, a.LeadGroup, a.LeadExpires, a.LeadKills),
                    (b.Id, b.GuildKey, b.Kind, b.Category, b.Property, b.MinIntensity, b.MinProperties, b.Slayer, b.Tier, b.Renown, b.Reputation, b.RolledAt, b.LeadArea, b.LeadGroup, b.LeadExpires, b.LeadKills));
            }
        }

        // A version 19 save: the same bytes without the v20 tail (an empty record set is one int) and, since cc-P229, the
        // v21 tail after it (an empty LeagueTitleRecord: its version and two empty counts), and since cc-P238 the v22 tail
        // (an empty MinedBlockRecord: its version and an empty count), and since cc-P630 the v23 tail (the Echo time, 8
        // bytes), the version patched.
        var bare = new BufferWriter(true);
        new ClusterFAccountData().Serialize(bare);
        var v20 = bare.Buffer.AsSpan(0, (int)bare.Position).ToArray();
        Assert.Equal(23, BitConverter.ToInt32(v20, 0));
        var v19 = v20.AsSpan(0, v20.Length - 4 - 12 - 8 - 8).ToArray();
        BitConverter.GetBytes(19).CopyTo(v19, 0);

        var oldReader = new BufferReader(v19);
        var loaded = new ClusterFAccountData(oldReader);
        Assert.Equal(v19.Length, oldReader.Position);
        Assert.Equal(0, loaded.RequisitionDataCount);

        var again = new BufferWriter(true);
        loaded.Serialize(again);
        Assert.Equal(23, BitConverter.ToInt32(again.Buffer, 0));
        Assert.Equal(v20.Length, again.Position);
        _out.WriteLine($"v20 {v20.Length} bytes, v19 {v19.Length} bytes, full record {writer.Position} bytes");
    }

    // ---------------------------------------------------------------- 12

    [Fact]
    public void TheBoardListsRequestsWithTurnInButtonsNeedsACounterAndTheCommandSaysOneLinePerRequest()
    {
        using var scope = new Scope(Noon, 1.0);
        var account = NewAccount();
        var pm = NewCharacter(account);
        var board = new RequisitionsBoard();
        try
        {
            Join(pm, "warriors", 1_500);
            ClusterFRequisitions.EnsureCurrent(pm);

            var gump = new RequisitionsBoardGump(pm, "warriors", 0);
            var labels = gump.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
            Assert.Equal(3, labels.FindAll(t => t == "Turn in").Count);
            Assert.Contains(labels, t => t.StartsWith("Warriors' Brotherhood"));

            var league = new RequisitionsBoardGump(pm, null, 0);
            Assert.Contains(league.Entries.OfType<GumpLabel>(), l => l.Text == "Next guild");

            Assert.False(RequisitionsBoard.CanTurnInHere(pm, "warriors", out _));
            board.MoveToWorld(new Point3D(pm.X + 2, pm.Y, pm.Z), pm.Map);
            Assert.True(RequisitionsBoard.CanTurnInHere(pm, "warriors", out var where));
            Assert.Equal("the board", where);
            board.GuildKey = "arcane";
            Assert.False(RequisitionsBoard.CanTurnInHere(pm, "warriors", out _));
            Assert.True(RequisitionsBoard.CanTurnInHere(pm, "arcane", out _));

            var lines = ClusterFRequisitions.Lines(pm);
            var data = ClusterFRequisitions.DataOf(pm, false);
            Assert.Equal(data.Requests.Count + 2, lines.Count);
            Assert.StartsWith($"{ClusterFRequisitions.Prefix} {data.Requests.Count} open", lines[0]);
            Assert.Equal($"{ClusterFRequisitions.Prefix} end", lines[^1]);
            Assert.All(lines.Skip(1).SkipLast(1), l => Assert.Equal(10, l.Split('|').Length));
            _out.WriteLine(string.Join("\n", lines.Take(4)));
        }
        finally
        {
            Cleanup(account, board);
        }
    }
}
