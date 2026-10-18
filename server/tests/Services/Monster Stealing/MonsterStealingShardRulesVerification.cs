// cc-P142: the shard's monster-stealing rules on top of cc-P127's port (Chase's decisions of 2026-10-16). Notes:
// shard-migration notes/cc-P142-monster-stealing-shard-rules.md.
//
// Facts:
//   1. A non-member and a too-low rank are refused with the line; outside every covered place nothing is said.
//   2. Each rank opens exactly its places (the ladder table; the Burrows at Initiate as the tutorial dungeon, Chase
//      2026-10-18 05:51), a boss needs the boss rank wherever it stands.
//   3. The excluded creatures refuse: pet, summon, tutorial creature, the pack mule, anything in the Vault.
//   4. The chance curve at 60, 100, 120, 160 and 200 matches the table in the notes, fame on top.
//   5. The minor table below 100 (never an OSI special), the special table from 100; the pick boundaries.
//   6. Fragments, recipe scrolls, the five components and a Burrows family part can drop.
//   7. The three Den recipes are own recipes (never a guild's Band V rotation), learned from a scroll, and their results
//      carry the thief's bonuses (the Thief's Smoke Bomb hides on Hiding, no Ninjitsu).
//   8. A miss can be retried after the cooldown, reveals the thief and provokes the creature.
//   9. After one success the creature refuses, and the flag and the gem timer survive a save (the store's round trip).
//  10. The Life Shield Lotion halves or better a pet's Life Drain on a player (decision 6: ServUO's one call site we have,
//      SpecialAbility.cs:2582-2585; cc-P127 ported the lotion without it). The Balm of Protection has no site in pinned.
//
// Host notes as cc-P127's: no regions.json, so a DungeonRegion is registered where a fact needs one; the Burrows and the
// Vault come from their own Initialize (as their facts do); a champion spawn is laid down with its own UpdateRegion.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.CannedEvil;
using Server.Engines.Craft;
using Server.Engines.CreatureStealing;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MonsterStealingShardRulesVerification
{
    private readonly ITestOutputHelper _out;

    public MonsterStealingShardRulesVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        MonsterStealingStore.Configure();
    }

    private const int Initiate = 0;
    private const int Apprentice = 1_000;
    private const int Journeyman = 5_000;
    private const int Master = 15_000;

    private static readonly Point3D DenSpot = new(4410, 1210, 0);
    private static readonly Rectangle3D DenArea = new(new Point3D(4400, 1200, -128), new Point3D(4440, 1240, 127));

    /// <summary>Inside the Burrows, outside the Vault (x 5142-5178, y 1132-1159).</summary>
    private static readonly Point3D BurrowsSpot = new(5200, 1200, 0);

    private static readonly Point3D VaultSpot = new(5150, 1140, 0);

    private sealed class Thief : IDisposable
    {
        public readonly Account Account = new($"p142{Guid.NewGuid():N}"[..16], "p142-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Marks = new();
        private readonly Region _den;

        public Thief(double stealing, Map map, bool den = true, Point3D? at = null)
        {
            ShardTestClock.Arm();
            if (den)
            {
                _den = new DungeonRegion("P142 den", map, 50, DenArea);
                _den.Register();
            }

            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills[SkillName.Stealing].Cap = 200.0;
            Pm.Skills[SkillName.Stealing].Base = stealing;
            Account[0] = Pm;
            Pm.MoveToWorld(at ?? DenSpot, map);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        /// <summary>Joins the Den at a standing (0 Initiate, 1,000 Apprentice, 5,000 Journeyman, 15,000 Master).</summary>
        public Thief Join(int standing)
        {
            var data = ClusterFAccountPersistence.GetOrCreateGuild(Pm);
            data.JoinedGuilds.Add(ThievesDenStealRules.GuildKey);
            data.GuildReputation[ThievesDenStealRules.GuildKey] = standing;
            return this;
        }

        public int Scrip => ClusterFAccountPersistence.GetGuild(Pm)?.GetCurrency(ThievesDenStealRules.GuildKey) ?? 0;

        public BaseCreature Mark(int fame, Point3D? at = null)
        {
            var bc = new Skeleton { Fame = fame };
            var spot = at ?? Pm.Location;
            bc.MoveToWorld(new Point3D(spot.X + 1, spot.Y, spot.Z), Pm.Map);
            Marks.Add(bc);
            return bc;
        }

        public T Keep<T>(T m) where T : Mobile
        {
            Marks.Add(m);
            return m;
        }

        public StealingHandler.Outcome Steal(BaseCreature bc)
        {
            StealingHandler.HandleSteal(bc, Pm);
            return StealingHandler.LastOutcome;
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Marks.ForEach(m => m.Delete());
            BaseThieveConsumable.EndEffect(Pm);
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            _den?.Unregister();
        }
    }

    private static bool Proceeded(StealingHandler.Outcome o) =>
        o is StealingHandler.Outcome.Missed or StealingHandler.Outcome.StoleMinor or StealingHandler.Outcome.StoleSpecial
            or StealingHandler.Outcome.TooLittleSkill or StealingHandler.Outcome.Wary;

    [Fact]
    public void ANonMemberAndATooLowRankAreRefusedWithTheLine()
    {
        // A non-member in a Felucca dungeon: the line. Outside any covered place: silence (OSI's), member or not.
        using (var t = new Thief(120.0, Map.Felucca))
        {
            Assert.Equal(StealingHandler.Outcome.Refused, t.Steal(t.Mark(1)));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(t.Pm, t.Mark(1), out var place, out var line));
            Assert.Equal(StealPlace.FeluccaDungeon, place);
            Assert.Equal(ThievesDenStealRules.NotAMemberLine, line);
        }

        using (var open = new Thief(120.0, Map.Felucca, den: false))
        {
            Assert.Equal(StealingHandler.Outcome.NotHere, open.Steal(open.Mark(1)));
            open.Join(Master);
            Assert.Equal(StealingHandler.Outcome.NotHere, open.Steal(open.Mark(1)));
        }

        // An Apprentice in a Trammel dungeon: the line names the rank held and the rank needed. A Journeyman proceeds.
        using (var t = new Thief(120.0, Map.Trammel))
        {
            t.Join(Apprentice);
            Assert.Equal(StealingHandler.Outcome.Refused, t.Steal(t.Mark(1)));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(t.Pm, t.Mark(1), out var place, out var line));
            Assert.Equal(StealPlace.FacetDungeon, place);
            Assert.Equal(ThievesDenStealRules.TooLowLine("Apprentice", "Journeyman", ThievesDenStealRules.PlaceName(StealPlace.FacetDungeon)), line);
            Assert.Contains("Journeyman", line);
            _out.WriteLine(line);

            ClusterFAccountPersistence.GetOrCreateGuild(t.Pm).GuildReputation[ThievesDenStealRules.GuildKey] = Journeyman;
            Assert.True(Proceeded(t.Steal(t.Mark(1))));
        }

        // The Burrows, the tutorial dungeon (Chase, 2026-10-18 05:51): an Initiate works it like OSI's own places; its
        // boss (The Burrow Mother, a boss to the Den though not to the loot boxes) still asks for a Master.
        OldHavenBurrowsRegion.Initialize();
        using (var t = new Thief(120.0, Map.Trammel, den: false, at: BurrowsSpot))
        {
            Assert.True(ThievesDenStealRules.IsTutorialDungeon(t.Pm.Region), $"the thief stands in {t.Pm.Region?.Name}");
            Assert.False(ThievesDenStealRules.IsShardDungeon(t.Pm.Region), "the Burrows is the tutorial set, not a Master's shard dungeon");
            Assert.Equal(StealPlace.TutorialDungeon, ThievesDenStealRules.PlaceOf(t.Pm));
            Assert.Equal(ClusterFGuildSystem.RankInitiate, ThievesDenStealRules.RequiredRank(ThievesDenStealRules.PlaceOf(t.Pm), false));
            t.Join(Initiate);
            Assert.True(Proceeded(t.Steal(t.Mark(1))), "an Initiate works the Burrows: it is the tutorial dungeon");

            var mother = t.Keep(new TheBurrowMother());
            mother.MoveToWorld(new Point3D(BurrowsSpot.X + 1, BurrowsSpot.Y + 1, 0), Map.Trammel);
            Assert.True(ThievesDenStealRules.IsBoss(mother), "the Burrows' named bosses are bosses to the Den");
            Assert.Equal(StealingHandler.Outcome.Refused, t.Steal(mother));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(t.Pm, mother, out _, out var line));
            Assert.Contains("Master", line);
            _out.WriteLine(line);
            ClusterFAccountPersistence.GetOrCreateGuild(t.Pm).GuildReputation[ThievesDenStealRules.GuildKey] = Master;
            Assert.True(Proceeded(t.Steal(mother)));
        }

        // A boss (a champion; the shard's boss test, ClusterFLootBoxes.IsBoss, which leaves paragons out) in a Felucca
        // dungeon: an Initiate is refused, a Master proceeds.
        using (var t = new Thief(120.0, Map.Felucca))
        {
            t.Join(Initiate);
            var boss = t.Keep(new Barracoon());
            boss.MoveToWorld(new Point3D(DenSpot.X + 1, DenSpot.Y + 1, 0), Map.Felucca);
            Assert.True(ThievesDenStealRules.IsBoss(boss));
            var paragon = t.Mark(30000);
            paragon.IsParagon = true;
            Assert.False(ThievesDenStealRules.IsBoss(paragon), "a paragon is not a boss (ClusterFLootBoxes.IsBoss)");
            Assert.Equal(StealingHandler.Outcome.Refused, t.Steal(boss));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(t.Pm, boss, out _, out var line));
            Assert.Contains("Master", line);
            _out.WriteLine(line);
            ClusterFAccountPersistence.GetOrCreateGuild(t.Pm).GuildReputation[ThievesDenStealRules.GuildKey] = Master;
            Assert.True(Proceeded(t.Steal(boss)));
        }
    }

    [Fact]
    public void EachRankOpensExactlyItsPlaces()
    {
        Assert.Equal(ClusterFGuildSystem.RankInitiate, ThievesDenStealRules.RequiredRank(StealPlace.FeluccaDungeon, false));
        Assert.Equal(ClusterFGuildSystem.RankInitiate, ThievesDenStealRules.RequiredRank(StealPlace.ChampionSpawn, false));
        Assert.Equal(ClusterFGuildSystem.RankJourneyman, ThievesDenStealRules.RequiredRank(StealPlace.FacetDungeon, false));
        Assert.Equal(ClusterFGuildSystem.RankMaster, ThievesDenStealRules.RequiredRank(StealPlace.ShardDungeon, false));
        Assert.Equal(ClusterFGuildSystem.RankInitiate, ThievesDenStealRules.RequiredRank(StealPlace.TutorialDungeon, false));
        Assert.Equal(int.MaxValue, ThievesDenStealRules.RequiredRank(StealPlace.None, false));

        foreach (var place in new[] { StealPlace.FeluccaDungeon, StealPlace.ChampionSpawn, StealPlace.FacetDungeon, StealPlace.ShardDungeon, StealPlace.TutorialDungeon })
        {
            Assert.Equal(ClusterFGuildSystem.RankMaster, ThievesDenStealRules.RequiredRank(place, true));
        }

        // The ladder's names are the Den's own (the generic ladder).
        Assert.Equal("Initiate", ThievesDenStealRules.RankName(ClusterFGuildSystem.RankInitiate));
        Assert.Equal("Journeyman", ThievesDenStealRules.RankName(ClusterFGuildSystem.RankJourneyman));
        Assert.Equal("Master", ThievesDenStealRules.RankName(ClusterFGuildSystem.RankMaster));

        // Places: a Felucca dungeon, the same dungeon on Trammel, a champion spawn on Trammel, the Burrows, open land.
        using (var fel = new Thief(120.0, Map.Felucca))
        {
            Assert.Equal(StealPlace.FeluccaDungeon, ThievesDenStealRules.PlaceOf(fel.Pm));
        }

        using (var tram = new Thief(120.0, Map.Trammel))
        {
            Assert.Equal(StealPlace.FacetDungeon, ThievesDenStealRules.PlaceOf(tram.Pm));
        }

        using (var open = new Thief(120.0, Map.Trammel, den: false))
        {
            Assert.Equal(StealPlace.None, ThievesDenStealRules.PlaceOf(open.Pm));

            var spawn = new ChampionSpawn();
            try
            {
                spawn.MoveToWorld(new Point3D(DenSpot.X + 5, DenSpot.Y + 5, 0), Map.Trammel);
                spawn.SetInitialSpawnArea();
                spawn.UpdateRegion();
                Assert.True(open.Pm.Region.IsPartOf<ChampionSpawnRegion>(), $"the thief stands in {open.Pm.Region?.Name}");
                Assert.Equal(StealPlace.ChampionSpawn, ThievesDenStealRules.PlaceOf(open.Pm));
                open.Join(Initiate);
                Assert.True(Proceeded(open.Steal(open.Mark(1))), "a member works a champion spawn on any facet");
            }
            finally
            {
                spawn.Delete();
            }

            Assert.Equal(StealPlace.None, ThievesDenStealRules.PlaceOf(open.Pm));
        }

        // The Burrows: a covered place at the Initiate's rank (the tutorial dungeon), not a Master's shard dungeon.
        OldHavenBurrowsRegion.Initialize();
        using (var burrows = new Thief(120.0, Map.Trammel, den: false, at: BurrowsSpot))
        {
            var place = ThievesDenStealRules.PlaceOf(burrows.Pm);
            Assert.Equal(StealPlace.TutorialDungeon, place);
            Assert.NotEqual(StealPlace.ShardDungeon, place);
            Assert.Equal(ClusterFGuildSystem.RankInitiate, ThievesDenStealRules.RequiredRank(place, false));
            Assert.Empty(ThievesDenStealRules.ShardDungeonRegionTypes);
        }
    }

    [Fact]
    public void TheExcludedCreaturesRefuse()
    {
        using var t = new Thief(200.0, Map.Felucca);
        t.Join(Master);

        var pet = t.Mark(30000);
        pet.Controlled = true;
        Assert.Equal(StealingHandler.Outcome.Never, t.Steal(pet));
        Assert.False(StealingHandler.HasBeenStolen(pet));

        var summon = t.Mark(30000);
        summon.Summoned = true;
        Assert.Equal(StealingHandler.Outcome.Never, t.Steal(summon));

        var gourdling = t.Keep(new Gourdling());
        gourdling.MoveToWorld(new Point3D(DenSpot.X + 1, DenSpot.Y + 1, 0), Map.Felucca);
        Assert.Equal(StealingHandler.Outcome.Never, t.Steal(gourdling));

        var mule = t.Keep(new PackMule());
        mule.MoveToWorld(new Point3D(DenSpot.X + 2, DenSpot.Y + 1, 0), Map.Felucca);
        Assert.Equal(StealingHandler.Outcome.Never, t.Steal(mule));

        // The Vault: a creature standing in it refuses even a Master.
        OldHavenBurrowsRegion.Initialize();
        RootdeepVaultRegion.Initialize();
        var vaulted = t.Mark(30000, VaultSpot);
        vaulted.MoveToWorld(VaultSpot, Map.Trammel);
        Assert.True(vaulted.Region.IsPartOf<RootdeepVaultRegion>(), $"the creature stands in {vaulted.Region?.Name}");
        Assert.True(ThievesDenStealRules.IsNeverStolen(vaulted));
        Assert.Equal(StealingHandler.Outcome.Never, t.Steal(vaulted));

        // One already stolen from: OSI's refusal.
        var done = t.Mark(30000);
        StealingHandler.SetStolen(done, true);
        Assert.Equal(StealingHandler.Outcome.AlreadyStolen, t.Steal(done));
        StealingHandler.SetStolen(done, false);
    }

    [Fact]
    public void TheChanceCurveMatchesTheTable()
    {
        var weak = new Skeleton { Fame = 1 };
        var famous = new Skeleton { Fame = 30000 };

        try
        {
            // The table in the notes: weak creature (no fame bonus).
            Assert.Equal(0.0, StealingHandler.GetStealingChance(null, weak, 59.9));
            Assert.Equal(2.0, StealingHandler.GetStealingChance(null, weak, 60.0), 3);
            Assert.Equal(3.5, StealingHandler.GetStealingChance(null, weak, 80.0), 3);
            Assert.Equal(5.0, StealingHandler.GetStealingChance(null, weak, 100.0), 3);
            Assert.Equal(11.0, StealingHandler.GetStealingChance(null, weak, 120.0), 3);
            Assert.Equal(23.0, StealingHandler.GetStealingChance(null, weak, 160.0), 3);
            Assert.Equal(35.0, StealingHandler.GetStealingChance(null, weak, 200.0), 3);
            Assert.Equal(35.0, StealingHandler.GetStealingChance(null, weak, 250.0), 3); // past the cap, nothing more

            // The most famous (fame level 40): OSI's +5 on top.
            Assert.Equal(5, StealingHandler.FameBonus(famous));
            Assert.Equal(7.0, StealingHandler.GetStealingChance(null, famous, 60.0), 3);
            Assert.Equal(10.0, StealingHandler.GetStealingChance(null, famous, 100.0), 3);
            Assert.Equal(16.0, StealingHandler.GetStealingChance(null, famous, 120.0), 3);
            Assert.Equal(28.0, StealingHandler.GetStealingChance(null, famous, 160.0), 3);
            Assert.Equal(40.0, StealingHandler.GetStealingChance(null, famous, 200.0), 3);

            // The minor tier meets the special tier at 100: no step down.
            Assert.True(StealingHandler.GetStealingChance(null, weak, 99.9) < StealingHandler.GetStealingChance(null, weak, 100.0));

            // OSI's table is kept beside ours (the register).
            Assert.Equal(15, StealingHandler.GetServUOStealingChance(null, famous, 120.0));
            Assert.Equal(13, StealingHandler.GetServUOStealingChance(null, famous, 130.0)); // OSI's quirk: only exactly 120 is 10; 130 falls to the 110.1 step (8), plus 5
        }
        finally
        {
            weak.Delete();
            famous.Delete();
        }
    }

    [Fact]
    public void TheMinorTableBelow100AndTheSpecialTableFrom100()
    {
        // The pick boundaries.
        Assert.Equal(StealLootTables.MinorKind.Gold, StealLootTables.MinorPick(0));
        Assert.Equal(StealLootTables.MinorKind.Gold, StealLootTables.MinorPick(34));
        Assert.Equal(StealLootTables.MinorKind.Scrip, StealLootTables.MinorPick(35));
        Assert.Equal(StealLootTables.MinorKind.Scrip, StealLootTables.MinorPick(59));
        Assert.Equal(StealLootTables.MinorKind.Lockpicks, StealLootTables.MinorPick(60));
        Assert.Equal(StealLootTables.MinorKind.Lockpicks, StealLootTables.MinorPick(74));
        Assert.Equal(StealLootTables.MinorKind.Potion, StealLootTables.MinorPick(75));
        Assert.Equal(StealLootTables.MinorKind.Potion, StealLootTables.MinorPick(89));
        Assert.Equal(StealLootTables.MinorKind.Bandages, StealLootTables.MinorPick(90));
        Assert.Equal(StealLootTables.MinorKind.Bandages, StealLootTables.MinorPick(99));

        Assert.Equal(StealLootTables.SpecialKind.Fragment, StealLootTables.SpecialPick(0.0));
        Assert.Equal(StealLootTables.SpecialKind.Fragment, StealLootTables.SpecialPick(0.0099));
        Assert.Equal(StealLootTables.SpecialKind.Recipe, StealLootTables.SpecialPick(0.01));
        Assert.Equal(StealLootTables.SpecialKind.Recipe, StealLootTables.SpecialPick(0.0499));
        Assert.Equal(StealLootTables.SpecialKind.Component, StealLootTables.SpecialPick(0.05));
        Assert.Equal(StealLootTables.SpecialKind.Component, StealLootTables.SpecialPick(0.1999));
        Assert.Equal(StealLootTables.SpecialKind.OsiSpecial, StealLootTables.SpecialPick(0.2));
        Assert.Equal(StealLootTables.SpecialKind.OsiSpecial, StealLootTables.SpecialPick(0.999));

        Assert.Equal(20, StealLootTables.GoldFor(1));
        Assert.Equal(770, StealLootTables.GoldFor(30000));

        // Each minor prize lands where it should.
        using (var t = new Thief(80.0, Map.Felucca))
        {
            t.Join(Initiate);
            var mark = t.Mark(1);
            Assert.IsType<Gold>(StealLootTables.GiveMinor(t.Pm, mark, StealLootTables.MinorKind.Gold));
            Assert.IsType<Lockpick>(StealLootTables.GiveMinor(t.Pm, mark, StealLootTables.MinorKind.Lockpicks));
            Assert.IsAssignableFrom<BasePotion>(StealLootTables.GiveMinor(t.Pm, mark, StealLootTables.MinorKind.Potion));
            Assert.IsType<Bandage>(StealLootTables.GiveMinor(t.Pm, mark, StealLootTables.MinorKind.Bandages));
            var scrip = t.Scrip;
            Assert.Null(StealLootTables.GiveMinor(t.Pm, mark, StealLootTables.MinorKind.Scrip));
            Assert.InRange(t.Scrip - scrip, 1, 3);
            Assert.Equal(4, t.Pm.Backpack.Items.Count);

            // Below 100 the live roll never pays an OSI special: at 80 against fame 1 (3.5%), 600 fresh creatures.
            var minors = 0;
            for (var i = 0; i < 600 && minors < 3; i++)
            {
                var o = t.Steal(t.Mark(1));
                Assert.NotEqual(StealingHandler.Outcome.StoleSpecial, o);
                if (o == StealingHandler.Outcome.StoleMinor)
                {
                    minors++;
                }
            }

            Assert.True(minors >= 3, $"only {minors} minor steals in 600 tries at 3.5%");
            Assert.DoesNotContain(t.Pm.Backpack.Items, i => StealingHandler.SpecialItemList.Contains(i.GetType()));
        }

        // From 100 the special table: an OSI special or the new loot, never the minor things.
        using (var t = new Thief(200.0, Map.Felucca))
        {
            t.Join(Master);
            var specials = 0;
            for (var i = 0; i < 300 && specials < 5; i++)
            {
                var o = t.Steal(t.Mark(30000)); // 40% a try
                Assert.NotEqual(StealingHandler.Outcome.StoleMinor, o);
                if (o == StealingHandler.Outcome.StoleSpecial)
                {
                    specials++;
                }
            }

            Assert.Equal(5, specials);
            Assert.Equal(5, t.Pm.Backpack.Items.Sum(i => i.Amount)); // stackable prizes (seeds, components) merge: count amounts
            Assert.All(t.Pm.Backpack.Items, i => Assert.True(
                StealingHandler.SpecialItemList.Contains(i.GetType()) || i is StolenComponent or LoopScrollFragment or AccountRecipeScroll,
                $"{i.GetType().Name} is not a special-tier prize"));
            _out.WriteLine(string.Join(", ", t.Pm.Backpack.Items.Select(i => i.GetType().Name)));
        }
    }

    [Fact]
    public void FragmentsRecipesAndTheNewComponentsCanDrop()
    {
        EnsureDenRecipes();

        var weak = new Skeleton { Fame = 1 };
        var famous = new Skeleton { Fame = 30000 };
        var made = new List<Item>();

        try
        {
            made.Add(Assert.IsType<LoopScrollFragment>(StealLootTables.MakeSpecial(weak, StealLootTables.SpecialKind.Fragment)));

            var den = Assert.IsType<AccountRecipeScroll>(StealLootTables.RecipeScroll(0.0));
            made.Add(den);
            Assert.Contains(den.RecipeKey, StealLootTables.DenRecipeKeys);

            var wild = StealLootTables.RecipeScroll(0.9);
            Assert.NotNull(wild);
            made.Add(wild);

            Assert.Equal(2, StealLootTables.ComponentsFor(0).Count);
            Assert.Equal(3, StealLootTables.ComponentsFor(10).Count);
            Assert.Equal(4, StealLootTables.ComponentsFor(20).Count);
            Assert.Equal(5, StealLootTables.ComponentsFor(30).Count);
            Assert.Equal(new[] { typeof(ShadowsilkThread), typeof(EmberAsh), typeof(Whisperleaf), typeof(ThiefsWire), typeof(LuckyFang) }, StealLootTables.ComponentsFor(40));

            for (var i = 0; i < 20; i++)
            {
                var c = StealLootTables.MakeSpecial(weak, StealLootTables.SpecialKind.Component);
                made.Add(c);
                Assert.True(c is ShadowsilkThread or EmberAsh, $"a weak creature gave {c.GetType().Name}");
            }

            var seen = new HashSet<Type>();
            for (var i = 0; i < 200 && seen.Count < 5; i++)
            {
                var c = StealLootTables.MakeSpecial(famous, StealLootTables.SpecialKind.Component);
                made.Add(c);
                Assert.IsAssignableFrom<StolenComponent>(c);
                seen.Add(c.GetType());
            }

            Assert.Equal(5, seen.Count);

            made.Add(Assert.IsAssignableFrom<Item>(StealLootTables.MakeSpecial(famous, StealLootTables.SpecialKind.OsiSpecial)));
            Assert.Contains(made[^1].GetType(), StealingHandler.SpecialItemList.Take(9));
        }
        finally
        {
            weak.Delete();
            famous.Delete();
            made.ForEach(i => i.Delete());
        }

        // Family flavour: a Burrows creature's part at the low rate; a creature with no family part never.
        using var t = new Thief(120.0, Map.Felucca);
        var grub = t.Keep(new BurrowGrub());
        grub.MoveToWorld(new Point3D(DenSpot.X + 1, DenSpot.Y, 0), Map.Felucca);
        Assert.Equal(typeof(BurrowerClaws), StealLootTables.FamilyPartOf(grub));
        Assert.Null(StealLootTables.TryFamilyPart(t.Pm, grub, 0.5));
        Assert.IsType<BurrowerClaws>(StealLootTables.TryFamilyPart(t.Pm, grub, 0.0));
        Assert.Null(StealLootTables.FamilyPartOf(t.Mark(1)));
        Assert.Null(StealLootTables.TryFamilyPart(t.Pm, t.Mark(1), 0.0));
    }

    private static void EnsureDenRecipes()
    {
        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        if (DefAlchemy.CraftSystem == null)
        {
            DefAlchemy.Initialize();
        }

        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        ThievesDenCraftRegistrations.Register();
    }

    [Fact]
    public void TheDenRecipesAreOwnRecipesLearnedFromAScroll()
    {
        EnsureDenRecipes();

        var cloak = DefTailoring.CraftSystem.CraftItems.SearchFor(typeof(ShadowsilkCloak));
        var bomb = DefAlchemy.CraftSystem.CraftItems.SearchFor(typeof(ThiefsSmokeBomb));
        var charm = DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(PickpocketsCharm));

        Assert.NotNull(cloak);
        Assert.NotNull(bomb);
        Assert.NotNull(charm);
        Assert.Equal(ThievesDenCraftRegistrations.ShadowsilkCloakRecipe, cloak.Recipe.ID);
        Assert.Equal(ThievesDenCraftRegistrations.ThiefsSmokeBombRecipe, bomb.Recipe.ID);
        Assert.Equal(ThievesDenCraftRegistrations.PickpocketsCharmRecipe, charm.Recipe.ID);

        // Own recipes: never in a guild's Band V rotation.
        foreach (var (system, item) in new[] { (DefTailoring.CraftSystem, cloak), (DefAlchemy.CraftSystem, bomb), (DefTinkering.CraftSystem, charm) })
        {
            var c = ClusterFRecipeGate.Classify(system, item);
            Assert.Equal(RecipeKind.OwnRecipe, c.Kind);
            Assert.Equal(ClusterFRecipeStore.RecipeKey(item.Recipe.ID), c.Key);
            Assert.DoesNotContain(c.Key, ClusterFRecipeRewards.BandVKeys(system.MainSkill));
            Assert.Contains(c.Key, StealLootTables.DenRecipeKeys);
        }

        // The inputs: the stolen-only components.
        Assert.Contains(cloak.Resources.Select(r => r.ItemType), t => t == typeof(ShadowsilkThread));
        Assert.Contains(bomb.Resources.Select(r => r.ItemType), t => t == typeof(EmberAsh));
        Assert.Contains(bomb.Resources.Select(r => r.ItemType), t => t == typeof(Whisperleaf));
        Assert.Contains(charm.Resources.Select(r => r.ItemType), t => t == typeof(LuckyFang));

        using var t = new Thief(100.0, Map.Felucca);
        Assert.False(ClusterFRecipeGate.IsKnown(t.Pm, DefTailoring.CraftSystem, cloak));
        Assert.True(ClusterFRecipeStore.Learn(t.Pm, ClusterFRecipeStore.RecipeKey(ThievesDenCraftRegistrations.ShadowsilkCloakRecipe)));
        Assert.True(ClusterFRecipeGate.IsKnown(t.Pm, DefTailoring.CraftSystem, cloak));
        Assert.False(ClusterFRecipeGate.IsKnown(t.Pm, DefAlchemy.CraftSystem, bomb));

        // The results.
        var made = new ShadowsilkCloak();
        var ring = new PickpocketsCharm();
        try
        {
            Assert.Equal(5.0, made.SkillBonuses.GetBonus(0));
            Assert.Equal(SkillName.Stealth, made.SkillBonuses.GetSkill(0));
            Assert.Equal(SkillName.Hiding, made.SkillBonuses.GetSkill(1));
            Assert.Equal(SkillName.Stealing, ring.SkillBonuses.GetSkill(0));
            Assert.Equal(SkillName.Snooping, ring.SkillBonuses.GetSkill(1));
            Assert.Equal(5.0, ring.SkillBonuses.GetBonus(1));
        }
        finally
        {
            made.Delete();
            ring.Delete();
        }

        // The Thief's Smoke Bomb hides on Hiding (50 is the gate; 100 here so the Hiding roll cannot miss) with no
        // Ninjitsu; the stock one refuses the same thief. The host installs no skill callbacks, so Hiding's goes in first.
        Server.SkillHandlers.Hiding.Initialize();
        t.Pm.Skills[SkillName.Hiding].Base = 100.0;
        t.Pm.Skills[SkillName.Ninjitsu].Base = 0.0;
        t.Pm.NextSkillTime = Core.TickCount;
        t.Pm.RawInt = 50;
        t.Pm.Mana = 50;
        var stock = new SmokeBomb();
        var thiefs = new ThiefsSmokeBomb();
        t.Pm.Backpack.DropItem(stock);
        t.Pm.Backpack.DropItem(thiefs);
        stock.OnDoubleClick(t.Pm);
        Assert.False(stock.Deleted, "the stock bomb wants Ninjitsu 50");
        Assert.False(t.Pm.Hidden);
        thiefs.OnDoubleClick(t.Pm);
        Assert.True(thiefs.Deleted, "the thief's bomb is used on Hiding alone");
        Assert.True(t.Pm.Hidden);
        Assert.Equal(40, t.Pm.Mana);
    }

    [Fact]
    public void TheLifeShieldLotionHalvesALifeDrainOnAPlayer()
    {
        using var t = new Thief(100.0, Map.Felucca);
        t.Pm.RawStr = 1000;
        t.Pm.Hits = t.Pm.HitsMax;
        var drainer = t.Mark(1);
        var ability = new LifeDrain();

        int Drained(int rounds)
        {
            var total = 0;
            for (var i = 0; i < rounds; i++)
            {
                t.Pm.Hits = t.Pm.HitsMax;
                var damage = 0;
                ability.DoEffects(drainer, t.Pm, ref damage);
                total += t.Pm.HitsMax - t.Pm.Hits;
            }

            return total;
        }

        var bare = Drained(30);
        Assert.InRange(bare, 300, 1200); // 30 drains of 10 to 40 reach the player at all

        var lotion = new LifeShieldLotion();
        t.Pm.Backpack.DropItem(lotion);
        lotion.OnDoubleClick(t.Pm);
        Assert.True(lotion.Deleted);
        Assert.True(BaseThieveConsumable.IsUnderThieveConsumableEffect(t.Pm, ThieveConsumableEffect.LifeShieldLotionEffect));

        var shielded = Drained(30);
        _out.WriteLine($"30 drains: {bare} bare, {shielded} under the lotion");
        Assert.True(shielded * 2 < bare, $"the lotion took {bare} to {shielded}; it halves a drain at the least");

        // The hook itself: 50 to 99 percent off, never nothing, never all.
        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(LifeShieldLotion.HandleLifeDrain(t.Pm, 100.0), 1.0, 50.0);
        }

        BaseThieveConsumable.EndEffect(t.Pm);
        Assert.Equal(100.0, LifeShieldLotion.HandleLifeDrain(t.Pm, 100.0));
    }

    [Fact]
    public void AMissCanBeRetriedAfterTheCooldownAndProvokesTheCreature()
    {
        using var t = new Thief(60.0, Map.Felucca); // 2% a try: a miss comes at once
        t.Join(Initiate);

        BaseCreature mark = null;
        StealingHandler.Outcome o = StealingHandler.Outcome.Nothing;
        for (var i = 0; i < 50 && o != StealingHandler.Outcome.Missed; i++)
        {
            mark = t.Mark(1);
            t.Pm.Hidden = true;
            o = t.Steal(mark);
        }

        Assert.Equal(StealingHandler.Outcome.Missed, o);
        Assert.False(StealingHandler.HasBeenStolen(mark), "a miss does not use the creature up");
        Assert.False(t.Pm.Hidden, "a miss reveals the thief");
        Assert.Same(t.Pm, mark.Combatant);
        Assert.True(StealingHandler.NextRetry(mark) > Core.Now);
        Assert.True(StealingHandler.NextRetry(mark) <= Core.Now + StealingHandler.RetryCooldown);

        // Too soon: wary, no roll.
        Assert.Equal(StealingHandler.Outcome.Wary, t.Steal(mark));
        Assert.False(StealingHandler.HasBeenStolen(mark));

        // The cooldown over: a real try again.
        StealingHandler.SetNextRetry(mark, Core.Now - TimeSpan.FromSeconds(1));
        o = t.Steal(mark);
        Assert.True(o is StealingHandler.Outcome.Missed or StealingHandler.Outcome.StoleMinor, o.ToString());
    }

    [Fact]
    public void AfterOneSuccessTheCreatureRefusesAndTheFlagSurvivesASave()
    {
        using var t = new Thief(200.0, Map.Felucca);
        t.Join(Master);

        BaseCreature mark = null;
        StealingHandler.Outcome o = StealingHandler.Outcome.Nothing;
        for (var i = 0; i < 200 && o != StealingHandler.Outcome.StoleSpecial; i++)
        {
            mark = t.Mark(30000); // 40% a try
            StealingHandler.SetNextRetry(mark, DateTime.MinValue);
            o = t.Steal(mark);
            if (o == StealingHandler.Outcome.Missed)
            {
                StealingHandler.SetNextRetry(mark, DateTime.MinValue);
                o = t.Steal(mark); // a miss leaves it open: the same creature again
            }
        }

        Assert.Equal(StealingHandler.Outcome.StoleSpecial, o);
        Assert.True(StealingHandler.HasBeenStolen(mark));
        Assert.Equal(StealingHandler.Outcome.AlreadyStolen, t.Steal(mark));
        var count = t.Pm.Backpack.Items.Count;
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(StealingHandler.Outcome.AlreadyStolen, t.Steal(mark));
        }

        Assert.Equal(count, t.Pm.Backpack.Items.Count);

        // The store's round trip: the flag and a gem timer come back; a creature deleted in between does not.
        var gone = t.Mark(1);
        StealingHandler.SetStolen(gone, true);
        var gemAt = Core.Now + TimeSpan.FromHours(3);
        GemOfSalvation.SetNextUse(t.Pm, gemAt);

        var store = MonsterStealingStore.Instance;
        Assert.NotNull(store);

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        store.Serialize(writer);
        writer.Flush();

        MonsterStealingStore.ClearForTests();
        Assert.False(StealingHandler.HasBeenStolen(mark));
        Assert.Equal(DateTime.MinValue, GemOfSalvation.NextUse(t.Pm));

        gone.Delete();
        store.Deserialize(new BufferReader(buffer));

        Assert.True(StealingHandler.HasBeenStolen(mark), "the flag outlives a save and load");
        Assert.False(StealingHandler.HasBeenStolen(gone));
        Assert.Equal(gemAt, GemOfSalvation.NextUse(t.Pm));
        Assert.Equal(StealingHandler.Outcome.AlreadyStolen, t.Steal(mark));

        MonsterStealingStore.ClearForTests();
    }
}
