// HalloweenWitchVerification.cs
//
// cc-P786: the Halloween witch field boss. Notes in shard-migration notes/cc-P786-halloween-witch.md. Each fact was
// seen red first (the notes' red table).
//
// Facts:
//   1. The table: the defaults validate (about 900 hits, Magery 110, three drops, the wild spots, tokens) and round-trip
//      as JSON; a drop that is a power scroll, a bless deed, a weapon, a robe or a container, and an unknown setting, are
//      each refused whole. A made witch has the table's hits and Magery and carries no robe or broom to loot.
//   2. The season gate, wild spots: out of season and in winter no witch stands at any spot; in Halloween one witch at
//      each spot, once however often the check runs; a killed spot's witch comes back only after respawnMinutes; when
//      the season ends every one is taken away.
//   3. The season gate, encounter: the witch's encounter joins any encounter table that comes into force (and is not
//      added twice), as a night Boss in the halloween season: a player qualifies at night in Halloween, not out of it
//      and not by day; started, it brings one hostile witch at the player.
//   4. The drop table: one prize drawn by weight (the Ouija board, the torture block deed, the gore rack deed), none
//      past the drop chance; a killed witch's corpse holds the drawn prize and none of her clothes.
//   5. The token award: her top damager gets the table's Festival Tokens in a festival season; nothing out of season
//      and nothing with no player to give them to; a killed witch pays the player who hurt her most.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Encounters;
using Server.Engines.RewardStores;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HalloweenWitchVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];
    private DateTime _now = new(2026, 10, 25, 23, 0, 0);
    private readonly Func<Map, Point3D, int> _hourAt = ClusterFEncounters.HourAt;
    private readonly Func<string, bool> _inSeason = ClusterFEncounters.InSeason;

    private static bool _achievements;

    // A wild test spot clear of other facts' spots and of the SOS fences.
    private static readonly Point3D Field = new(3420, 420, 0);

    public HalloweenWitchVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        HalloweenWitchTable.Current = HalloweenWitchTable.Defaults;
        HalloweenWitchSpawns.HookEncounters();
        HalloweenWitchSpawns.Clock = () => _now;
        HalloweenWitchSpawns.FitSpot = (_, p) => p;
        HalloweenWitchSpawns.Clear();
        FestivalSeasons.Forced = "none";
        HalloweenWitch.ChanceRoll = () => 0.0;
        HalloweenWitch.DropRoll = () => 0.0;
    }

    public void Dispose()
    {
        HalloweenWitchSpawns.Clear();
        FestivalSeasons.Forced = null;
        HalloweenWitchTable.Current = HalloweenWitchTable.Defaults;
        HalloweenWitchSpawns.Clock = () => Core.Now;
        HalloweenWitch.ChanceRoll = Utility.RandomDouble;
        HalloweenWitch.DropRoll = Utility.RandomDouble;

        foreach (var e in ClusterFEncounters.Active.ToList())
        {
            foreach (var bc in e.All.ToList())
            {
                bc.Delete();
            }
        }

        ClusterFEncounters.Reset();
        ClusterFEncounters.HourAt = _hourAt;
        ClusterFEncounters.InSeason = _inSeason;
        ClusterFEncounters.Clock = () => Core.Now;
        ClusterFEncounters.FindSpot = ClusterFEncounters.DefaultFindSpot;
        EncounterTable.Current = EncounterTable.BuildDefaults();

        foreach (var e in _made)
        {
            e?.Delete();
        }

        foreach (var a in _accounts)
        {
            for (var i = 0; i < a.Length; i++)
            {
                a[i]?.Delete();
            }

            Accounts.Remove(a);
        }
    }

    // ---------------------------------------------------------------- helpers

    private PlayerMobile Player(Point3D? at = null)
    {
        var account = new Account($"p786{Guid.NewGuid():N}"[..16], "p786-test-only") { Young = false };
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Hunter" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.Young = false;
        pm.Skills[SkillName.Swords].Base = 80.0;
        pm.MoveToWorld(at ?? Field, Map.Trammel);
        return pm;
    }

    private HalloweenWitch Witch(Point3D? at = null)
    {
        var w = new HalloweenWitch();
        w.MoveToWorld(at ?? new Point3D(Field.X + 1, Field.Y, Field.Z), Map.Trammel);
        _made.Add(w);
        return w;
    }

    private static int Tokens(PlayerMobile pm) => FestivalTokens.Instance.Balance(pm);

    // ---------------------------------------------------------------- 1. the table

    [Fact]
    public void TheTableValidatesAndRefusesAnythingButDecorAndToys()
    {
        var t = HalloweenWitchTable.Defaults;
        var errors = new List<string>();
        HalloweenWitchTable.Validate(t, errors);
        Assert.Empty(errors);
        Assert.InRange(t.HitsMin, 800, 900);
        Assert.InRange(t.HitsMax, 900, 1000);
        Assert.Equal(110.0, t.Magery);
        Assert.Equal(new[] { typeof(WitchOuijaBoard), typeof(TortureBlockDeed), typeof(GoreRackDeed) }, t.Drops.Select(d => d.ItemType));
        Assert.True(t.Tokens > 0);
        Assert.NotEmpty(t.Spots);
        Assert.All(t.Spots, s => Assert.NotNull(s.Facet));

        var back = HalloweenWitchTable.Parse(HalloweenWitchTable.ToJson(t), "round-trip", errors);
        Assert.Empty(errors);
        Assert.NotNull(back);
        Assert.Equal(t.Drops.Select(d => (d.Type, d.Weight)), back!.Drops.Select(d => (d.Type, d.Weight)));
        Assert.Equal(t.Spots.Select(s => (s.X, s.Y, s.Z)), back.Spots.Select(s => (s.X, s.Y, s.Z)));

        foreach (var bad in new[] { "PowerScroll", "ClothingBlessDeed", "Longsword", "Robe", "Bag" })
        {
            var table = HalloweenWitchTable.BuildDefaults();
            table.Drops.Add(new WitchDropRow { Type = bad, Weight = 1 });
            var errs = new List<string>();
            Assert.Null(HalloweenWitchTable.Parse(HalloweenWitchTable.ToJson(table), "bad", errs));
            Assert.Contains(errs, e => e.Contains(bad));
            _out.WriteLine(string.Join("; ", errs));
        }

        Assert.Null(HalloweenWitchTable.Parse("{\"robe\": true}", "unknown", new List<string>()));

        // A made witch: the table's numbers, and nothing of her costume to loot.
        var w = Witch();
        Assert.InRange(w.HitsMax, t.HitsMin, t.HitsMax);
        Assert.Equal(t.Magery, w.Skills.Magery.Base);
        Assert.All(w.Items.Where(i => i.Layer != Layer.Backpack), i => Assert.False(i.Movable));
        Assert.DoesNotContain(w.Items, i => i is BaseWeapon);
    }

    // ---------------------------------------------------------------- 2. the season gate: wild spots

    [Fact]
    public void WitchesStandAtTheWildSpotsOnlyInTheHalloweenWindowOnceEach()
    {
        var spots = HalloweenWitchTable.Current.Spots.Count;

        FestivalSeasons.Forced = "none";
        HalloweenWitchSpawns.Sync();
        Assert.Empty(HalloweenWitchSpawns.Witches());

        FestivalSeasons.Forced = FestivalSeasons.Winter;
        HalloweenWitchSpawns.Sync();
        Assert.Empty(HalloweenWitchSpawns.Witches());

        // Halloween: one at each spot, at that spot; the check again, any number of times, places nothing more.
        FestivalSeasons.Forced = FestivalSeasons.Halloween;
        HalloweenWitchSpawns.Sync();
        HalloweenWitchSpawns.Sync();
        HalloweenWitchSpawns.Sync();
        var witches = HalloweenWitchSpawns.Witches();
        Assert.Equal(spots, witches.Count);
        for (var i = 0; i < spots; i++)
        {
            var w = HalloweenWitchSpawns.WitchAt(i);
            Assert.NotNull(w);
            Assert.Equal(HalloweenWitchTable.Current.Spots[i].Location, w!.Location);
            Assert.Equal(HalloweenWitchTable.Current.Spots[i].Location, w.Home);
        }

        Assert.Equal(spots, World.Mobiles.Values.OfType<HalloweenWitch>().Count(w => !w.Deleted && w.Spot >= 0));

        // A spot's witch killed: that spot waits respawnMinutes, then gets a new one.
        var first = HalloweenWitchSpawns.WitchAt(0)!;
        first.Kill();
        HalloweenWitchSpawns.Sync();
        Assert.Null(HalloweenWitchSpawns.WitchAt(0));
        Assert.Equal(spots - 1, HalloweenWitchSpawns.Witches().Count);

        _now = _now.AddMinutes(HalloweenWitchTable.Current.RespawnMinutes - 1);
        HalloweenWitchSpawns.Sync();
        Assert.Null(HalloweenWitchSpawns.WitchAt(0));

        _now = _now.AddMinutes(2);
        HalloweenWitchSpawns.Sync();
        Assert.NotNull(HalloweenWitchSpawns.WitchAt(0));
        Assert.Equal(spots, HalloweenWitchSpawns.Witches().Count);

        // The season over: every one taken away.
        var serials = HalloweenWitchSpawns.Witches().Select(w => w.Serial).ToList();
        FestivalSeasons.Forced = "none";
        HalloweenWitchSpawns.Sync();
        Assert.Empty(HalloweenWitchSpawns.Witches());
        Assert.Empty(HalloweenWitchSpawns.Placed());
        Assert.All(serials, s => Assert.True(World.FindMobile(s) is null or { Deleted: true }));

        // Off by the table: none even in Halloween.
        var off = HalloweenWitchTable.BuildDefaults();
        off.Enabled = false;
        HalloweenWitchTable.Validate(off, new List<string>());
        HalloweenWitchTable.Current = off;
        FestivalSeasons.Forced = FestivalSeasons.Halloween;
        HalloweenWitchSpawns.Sync();
        Assert.Empty(HalloweenWitchSpawns.Witches());
    }

    // ---------------------------------------------------------------- 3. the season gate: the encounter

    [Fact]
    public void TheWitchEncounterJoinsTheTableAndComesOnlyAtNightInHalloween()
    {
        // Any table coming into force gets it, once; a table that already has a "witch" keeps its own.
        EncounterTable.Current = EncounterTable.BuildDefaults();
        var def = EncounterTable.Current.Find(HalloweenWitchTable.EncounterId);
        Assert.NotNull(def);
        Assert.Equal(EncounterKind.Boss, def!.Kind);
        Assert.Equal("halloween", def.Season);
        Assert.Equal(EncounterTime.Night, def.Time);
        Assert.Equal(new[] { nameof(HalloweenWitch) }, def.Creatures.Single().Types);
        EncounterTable.Current = EncounterTable.Current;
        Assert.Single(EncounterTable.Current.Encounters, e => e.Id == HalloweenWitchTable.EncounterId);
        Assert.Empty(EncounterTable.Current.Validate());

        var pm = Player();
        ClusterFEncounters.Reset();
        ClusterFEncounters.Clock = () => _now;
        ClusterFEncounters.FindSpot = (p, _) => new Point3D(p.X + 6, p.Y, p.Z);

        try
        {
            ClusterFEncounters.HourAt = (_, _) => 23;
            ClusterFEncounters.InSeason = name => name == "halloween";
            Assert.True(ClusterFEncounters.Qualifies(pm, def, _now));

            ClusterFEncounters.InSeason = _ => false;
            Assert.False(ClusterFEncounters.Qualifies(pm, def, _now));

            ClusterFEncounters.InSeason = name => name == "halloween";
            ClusterFEncounters.HourAt = (_, _) => 12;
            Assert.False(ClusterFEncounters.Qualifies(pm, def, _now));

            // Started: one hostile witch, coming for the player.
            ClusterFEncounters.HourAt = (_, _) => 23;
            var e = ClusterFEncounters.Start(pm, def, _now);
            Assert.NotNull(e);
            var witch = Assert.IsType<HalloweenWitch>(Assert.Single(e!.Creatures));
            Assert.Equal(-1, witch.Spot);
            Assert.Equal(FightMode.Closest, witch.FightMode);
            Assert.Same(pm, witch.Combatant);
        }
        finally
        {
            ClusterFEncounters.HourAt = _hourAt;
            ClusterFEncounters.InSeason = _inSeason;
        }

        // Off by the witch's table: a new table gets no witch.
        var off = HalloweenWitchTable.BuildDefaults();
        off.Encounter.Enabled = false;
        HalloweenWitchTable.Validate(off, new List<string>());
        HalloweenWitchTable.Current = off;
        EncounterTable.Current = EncounterTable.BuildDefaults();
        Assert.Null(EncounterTable.Current.Find(HalloweenWitchTable.EncounterId));
    }

    // ---------------------------------------------------------------- 4. the drop table

    [Fact]
    public void TheCorpseHoldsOnePrizeDrawnByWeight()
    {
        var t = HalloweenWitchTable.Current;
        var total = (double)t.TotalWeight;
        var at = 0.0;
        foreach (var row in t.Drops)
        {
            var low = HalloweenWitch.MakeDrop(0.0, at / total);
            var high = HalloweenWitch.MakeDrop(0.0, (at + row.Weight - 0.001) / total);
            Assert.IsType(row.ItemType!, low);
            Assert.IsType(row.ItemType!, high);
            _made.Add(low!);
            _made.Add(high!);
            at += row.Weight;
        }

        // Past the chance: nothing.
        var half = HalloweenWitchTable.BuildDefaults();
        half.DropChancePercent = 50;
        HalloweenWitchTable.Validate(half, new List<string>());
        HalloweenWitchTable.Current = half;
        Assert.Null(HalloweenWitch.MakeDrop(0.6, 0.0));
        Assert.NotNull(HalloweenWitch.MakeDrop(0.4, 0.0));
        HalloweenWitchTable.Current = HalloweenWitchTable.Defaults;

        // A killed witch: the drawn prize in her corpse, and none of her clothes.
        HalloweenWitch.DropRoll = () => 0.99;
        var w = Witch();
        w.Kill();
        var corpse = w.Corpse;
        Assert.NotNull(corpse);
        _made.Add(corpse!);
        Assert.Single(corpse!.Items, i => i is GoreRackDeed);
        Assert.DoesNotContain(corpse.Items, i => i is BaseClothing or BaseWeapon or WitchOuijaBoard or TortureBlockDeed);

        // The deeds make house decor.
        var block = new TortureBlockDeed();
        var rack = new GoreRackDeed();
        _made.Add(block);
        _made.Add(rack);
        Assert.IsType<TortureBlockAddon>(block.Addon);
        Assert.IsType<GoreRackAddon>(rack.Addon);
    }

    // ---------------------------------------------------------------- 5. the token award

    [Fact]
    public void HerTopDamagerGetsTheTokensInAFestivalSeasonOnly()
    {
        var tokens = HalloweenWitchTable.Current.Tokens;
        var pm = Player();

        FestivalSeasons.Forced = "none";
        Assert.Equal(0, HalloweenWitch.AwardTokens(pm));
        Assert.Equal(0, Tokens(pm));

        FestivalSeasons.Forced = FestivalSeasons.Halloween;
        Assert.Equal(0, HalloweenWitch.AwardTokens(null));
        Assert.Equal(tokens, HalloweenWitch.AwardTokens(pm));
        Assert.Equal(tokens, Tokens(pm));

        // A real kill: the player who hurt her most is paid; the one who barely scratched her is not.
        var top = Player();
        var other = Player(new Point3D(Field.X, Field.Y + 2, Field.Z));
        var w = Witch();
        w.Damage(5, other);
        w.Damage(200, top);
        Assert.Same(top, FestivalTokens.TopDamager(w));
        w.Kill();
        if (w.Corpse != null)
        {
            _made.Add(w.Corpse);
        }

        // The witch's tokens; FestivalTokens' own creature drop (any 1,000+ fame kill in season) may add one more.
        Assert.InRange(Tokens(top), tokens, tokens + 1);
        Assert.Equal(0, Tokens(other));
    }
}
