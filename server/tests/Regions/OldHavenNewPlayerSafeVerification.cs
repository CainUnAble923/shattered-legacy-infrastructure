// OldHavenNewPlayerSafeVerification.cs
//
// cc-P189 (Chase, 2026-10-19: the custom Old Haven mages and boss are "too hard for new players", who now walk through
// Old Haven to the Rootdeep hole; and "we should have the young player skill boost for old haven area").
// Notes: shard-migration notes/cc-P189-cont-old-haven-new-player-safe.md.
//
// Facts:
//   S1. The seeder places ServUO's Drelgor at 3640,2418 and no OldHavenMage or Vorlak spawner.
//
// cc-P678: on the Siege of Sosaria map Old Haven is the author's Haven town, New Haven now, so Old Haven's undead field,
// Drelgor and "Old Haven Training" moved to the north woods between the town and the shore (x 3590-3710, y 2378-2459;
// ClusterFHavenTownSpawns.TrainingField). The facts below are the same facts on the new ground.
//   S2. [ClusterFOldHavenRetire removes exactly the mage and Vorlak spawners in Old Haven (one still under Vorlak's old
//       name), their creatures, and a stray mage; it keeps Drelgor's spawner, the stock skeleton spawner, a spawner
//       staff mixed, and a mage spawner outside Old Haven. The dry run changes nothing and reports the same counts.
//   A1. "Old Haven Training" is registered at start over the north woods, and not over New Haven.
//   A2. Drelgor's broadcast test holds for him at his spawn and a player at the woods' east edge (Drelgor.cs
//       BroadcastMessage).
//   A3. A player holding a New Haven apprentice quest gains x2 to x4 on that skill inside the region (ServUO
//       SkillCheck.cs:404-407), through the shard's real gain handler; the same player outside gains x1.
//   A4. Nobody else gets it: no quest, another skill's quest, the quest's skill at 50, a quest whose region is not
//       registered, or while pinned's own acceleration runs. Young does not matter either way (ServUO checks the quest,
//       never Young).
//   A5. Entering with the quest shows the buff, leaving removes it; without the quest nothing shows.
//   A6. The table is pinned's 26 accelerated New Haven quests, each on its own skill.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Objectives;
using Server.Engines.Spawners;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class OldHavenNewPlayerSafeVerification
{
    private static readonly Point3D DrelgorHome = new(3640, 2418, 34);
    // inside Old Haven Training: the north woods' east side
    private static readonly Point3D WoodsEdge = new(3700, 2440, 0);
    // P371: outside Old Haven Training, while remaining on the tutorial island before graduation (cc-P678: the start).
    private static readonly Point3D FarAway = new(3634, 2586, 0);

    private readonly ITestOutputHelper _out;

    public OldHavenNewPlayerSafeVerification(ITestOutputHelper output)
    {
        _out = output;
        ApprenticeTrainingRegion.Initialize();
        ShardTestHost.EnsureSkillChecks();
    }

    private static List<Spawner> OldHavenSpawners() =>
        World.Items.Values.OfType<Spawner>()
            .Where(s => !s.Deleted && s.Map == Map.Trammel && s.X is >= 3590 and <= 3710 && s.Y is >= 2378 and <= 2459)
            .ToList();

    // ---------------------------------------------------------------- S1

    [Fact]
    public void S1_TheSeederPlacesDrelgorAndNoMageOrVorlak()
    {
        var before = OldHavenSpawners().ToHashSet();
        var result = ClusterFOldHavenSeeder.Seed(false, false);
        _out.WriteLine(result);

        var made = OldHavenSpawners().Where(s => !before.Contains(s)).ToList();

        try
        {
            var names = made.SelectMany(s => s.Entries.Select(e => $"{e.SpawnedName} at {s.X},{s.Y}")).Order().ToList();
            _out.WriteLine(string.Join(" | ", names));

            Assert.DoesNotContain(made, s => s.Entries.Any(e => ClusterFOldHavenSeeder.RetiredTypes.Contains(e.SpawnedName)));
            Assert.Equal(["Drelgor at 3640,2418"], names);
        }
        finally
        {
            foreach (var s in made)
            {
                s.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- S2

    private sealed class Scene : IDisposable
    {
        public readonly List<IEntity> Made = [];
        public Spawner Mages, OldVorlak, Drelgor, Skeletons, Mixed, MagesElsewhere;
        public OldHavenMage Stray;

        public Spawner Place(Point3D at, params string[] names)
        {
            var spawner = new Spawner();
            Made.Add(spawner);
            spawner.MoveToWorld(at, Map.Trammel);
            spawner.SpawnBounds = default;

            foreach (var name in names)
            {
                var entry = spawner.AddEntry(name, 100, 1, dotimer: false);
                Assert.True(spawner.Spawn(entry, out var flags), $"{name}: {flags}");
            }

            spawner.Stop();
            return spawner;
        }

        public Scene()
        {
            Mages = Place(new Point3D(3600, 2400, 0), "OldHavenMage");
            OldVorlak = Place(new Point3D(3622, 2420, 0), "DrelgorTheImpaler"); // placed before cc-P129's rename
            Drelgor = Place(DrelgorHome, "Drelgor");
            Skeletons = Place(new Point3D(3660, 2430, 0), "Skeleton");          // an undead spawner in the woods
            Mixed = Place(new Point3D(3618, 2440, 0), "OldHavenMage", "Skeleton");
            MagesElsewhere = Place(new Point3D(1500, 1500, 0), "OldHavenMage");  // a new home outside Old Haven

            Stray = new OldHavenMage();
            Made.Add(Stray);
            Stray.MoveToWorld(new Point3D(3595, 2410, 0), Map.Trammel);
        }

        public IEnumerable<IEntity> Removed =>
            [Mages, OldVorlak, Stray, ..Mages.Spawned.Keys, ..OldVorlak.Spawned.Keys];

        public IEnumerable<IEntity> Kept =>
        [
            Drelgor, Skeletons, Mixed, MagesElsewhere, ..Drelgor.Spawned.Keys, ..Skeletons.Spawned.Keys,
            ..Mixed.Spawned.Keys, ..MagesElsewhere.Spawned.Keys
        ];

        public void Dispose()
        {
            foreach (var e in Made)
            {
                e.Delete();
            }
        }
    }

    [Fact]
    public void S2_TheRetireCommandRemovesTheMagesAndVorlakAndNothingElse()
    {
        using var w = new Scene();
        var removed = w.Removed.ToList();
        var kept = w.Kept.ToList();
        Assert.IsType<VorlakTheImpaler>(Assert.Single(w.OldVorlak.Spawned.Keys));
        Assert.Equal(5, removed.Count); // 2 spawners, 2 spawned, 1 stray
        Assert.Equal(9, kept.Count);    // 4 spawners, 5 spawned

        var dry = ClusterFOldHavenSeeder.Retire(true);
        dry.ForEach(l => _out.WriteLine($"dry: {l}"));
        Assert.All(removed.Concat(kept), e => Assert.False(e.Deleted));

        var real = ClusterFOldHavenSeeder.Retire(false);
        real.ForEach(l => _out.WriteLine($"real: {l}"));

        Assert.All(removed, e => Assert.True(e.Deleted, $"{e} should be gone"));
        Assert.All(kept, e => Assert.False(e.Deleted, $"{e} should stay"));

        Assert.Equal("ClusterF Old Haven retire dry run: would delete 2 spawners with 2 creatures, and 1 creatures with no spawner.", dry[^1]);
        Assert.Equal("ClusterF Old Haven retire complete: deleted 2 spawners with 2 creatures, and 1 creatures with no spawner.", real[^1]);
        Assert.Single(real, l => l.StartsWith("[MIXED, KEPT] Spawner at (3618,2440,", StringComparison.Ordinal));

        // A second run finds nothing more.
        Assert.Equal("ClusterF Old Haven retire complete: deleted 0 spawners with 0 creatures, and 0 creatures with no spawner.", ClusterFOldHavenSeeder.Retire(false)[^1]);
    }

    // ---------------------------------------------------------------- A1, A2

    [Fact]
    public void A1_OldHavenTrainingCoversTheNorthWoods()
    {
        Region At(int x, int y) => Region.Find(new Point3D(x, y, 0), Map.Trammel);

        foreach (var (x, y) in new[] { (3590, 2378), (3710, 2378), (3590, 2459), (3710, 2459), (3640, 2418), (3700, 2440) })
        {
            var r = At(x, y);
            Assert.True(r is ApprenticeRegion && r.Name == "Old Haven Training", $"{x},{y}: {r?.Name} ({r?.GetType().Name})");
        }

        // outside: one past each edge, the town's start, the bank (and the cellar hatch beside it), the old ruin's field
        foreach (var (x, y) in new[] { (3589, 2418), (3711, 2418), (3640, 2377), (3640, 2460), (3634, 2586), (3618, 2620), (3651, 2509) })
        {
            Assert.False(At(x, y).IsPartOf("Old Haven Training"), $"{x},{y} is inside");
        }

        Assert.Same(ApprenticeTrainingRegion.OldHaven, Region.Find("Old Haven Training", Map.Trammel));
    }

    [Fact]
    public void A2_DrelgorsBroadcastTestHoldsBetweenHisHomeAndTheWoodsEdge()
    {
        var drelgor = new Drelgor();
        drelgor.MoveToWorld(DrelgorHome, Map.Trammel);
        var pm = new PlayerMobile { Player = true };
        pm.MoveToWorld(WoodsEdge, Map.Trammel);

        try
        {
            // Drelgor.cs BroadcastMessage: Region.Name == "Old Haven Training" && m.Region.Name == "Old Haven Training".
            Assert.Equal("Old Haven Training", drelgor.Region.Name);
            Assert.Equal("Old Haven Training", pm.Region.Name);
        }
        finally
        {
            drelgor.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A3 to A5

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    // The shard's gain hook over pinned's handlers, one attempt and a 1x amount (so every gain is pinned's own),
    // with every roll held at the given value.
    private sealed class Hook : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
        private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
        private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
        private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
        private readonly bool _enabled = ClusterFSkillGain.Enabled;
        private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
        private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

        public Hook(double roll)
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            ClusterFSkillGain.Enabled = true;
            ClusterFSkillGain.ChanceAttempts = 1;
            ClusterFSkillGain.AmountMultiplier = 1;
            BuiltInRng.Generator = new ConstantRandom { Value = roll };
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = _location;
            Mobile.SkillCheckDirectLocationHandler = _directLocation;
            Mobile.SkillCheckTargetHandler = _target;
            Mobile.SkillCheckDirectTargetHandler = _directTarget;
            ClusterFSkillGain.Enabled = _enabled;
            ClusterFSkillGain.ChanceAttempts = _attempts;
            ClusterFSkillGain.AmountMultiplier = _amount;
        }
    }

    // A fresh character: stats locked so no stat roll runs, every skill capped at 200, nothing down-locked.
    private static PlayerMobile Player(Point3D at, bool young = false)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Young = young };
        pm.StrLock = StatLockType.Locked;
        pm.DexLock = StatLockType.Locked;
        pm.IntLock = StatLockType.Locked;
        pm.Skills.Cap = 58 * 2000;

        for (var i = 0; i < pm.Skills.Length; i++)
        {
            pm.Skills[i].Cap = 200.0;
        }

        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static MLQuestInstance Take<T>(PlayerMobile pm) where T : MLQuest, new() => new(new T(), null, pm);

    private static void Forget(PlayerMobile pm)
    {
        MLQuestSystem.Contexts.Remove(pm);
        ApprenticeTraining.RemoveBuff(pm);
        pm.Delete();
    }

    // One use of Magic Resist at 30.0 (window 0 to 100) with every roll held at `roll`: the gain in tenths.
    private static int OneUse(PlayerMobile pm, double roll, SkillName skill = SkillName.MagicResist, double at = 30.0)
    {
        using var hook = new Hook(roll);
        pm.Skills[skill].Base = at;
        var before = pm.Skills[skill].BaseFixedPoint;
        pm.CheckSkill(skill, 0.0, 100.0);
        return pm.Skills[skill].BaseFixedPoint - before;
    }

    [Fact]
    public void A3_AQuestHolderGainsTwoToFourTimesInsideAndOnceOutside()
    {
        var pm = Player(WoodsEdge);

        try
        {
            Take<DefyingTheArcane>(pm); // Alefian's quest: Magic Resist in Old Haven Training

            // RandomMinMax(2, 4) is 2 + Next(3) (pinned Utility.cs:860-872): a held 0 reads 2, a held 0.99 reads 4.
            // A held 0.99 fails the gain roll at 30.0, so the x4 case is below 10.0, where pinned always gains
            // (SkillCheck.cs:120-123) and its own amount is Random(4) + 1, 4 at 0.99 (SkillCheck.cs:252-255).
            var low = OneUse(pm, 0.0);
            var high = OneUse(pm, 0.99, at: 5.0);

            pm.MoveToWorld(FarAway, Map.Trammel);
            var outside = OneUse(pm, 0.0);
            var outsideHigh = OneUse(pm, 0.99, at: 5.0);

            _out.WriteLine($"Magic Resist, one use, in tenths: inside {low} at 30.0 and {high} at 5.0; outside {outside} and {outsideHigh}");
            Assert.Equal(1, outside);
            Assert.Equal(2, low);         // x2
            Assert.Equal(4, outsideHigh);
            Assert.Equal(16, high);       // x4
        }
        finally
        {
            Forget(pm);
        }
    }

    [Fact]
    public void A4_NobodyElseGetsIt()
    {
        var none = Player(WoodsEdge);
        var young = Player(WoodsEdge, young: true);
        var otherSkill = Player(WoodsEdge);
        var atFifty = Player(WoodsEdge);
        var unregistered = Player(WoodsEdge);
        var accelerated = Player(WoodsEdge);
        var notYoung = Player(WoodsEdge);

        try
        {
            Take<TheArtOfWar>(otherSkill);              // Tactics, not Magic Resist
            Take<DefyingTheArcane>(atFifty);
            Take<ScribingArcaneKnowledge>(unregistered); // Inscription in "Haven Library", not registered
            Take<DefyingTheArcane>(accelerated);
            accelerated.AcceleratedSkill = SkillName.MagicResist;
            accelerated.AcceleratedStart = Core.Now + TimeSpan.FromHours(1.0);
            Take<DefyingTheArcane>(notYoung);

            Assert.Equal(1, OneUse(none, 0.0));
            Assert.Equal(1, OneUse(young, 0.0));                                          // Young alone: nothing
            Assert.Equal(1, OneUse(otherSkill, 0.0));
            Assert.Equal(1, OneUse(atFifty, 0.0, at: 50.0));                              // the quest is done at 50
            Assert.Equal(1, OneUse(unregistered, 0.0, SkillName.Inscribe));
            Assert.Equal(2, OneUse(accelerated, 0.0));                                    // pinned's x2-5 alone, not both
            Assert.Equal(2, OneUse(notYoung, 0.0));                                       // the quest, not Young
            Assert.False(notYoung.Young);

            // The gain that reaches 50 is still boosted, as ServUO judges it before the objective completes.
            Assert.Equal(2, OneUse(atFifty, 0.0, at: 49.9));
        }
        finally
        {
            foreach (var pm in new[] { none, young, otherSkill, atFifty, unregistered, accelerated, notYoung })
            {
                Forget(pm);
            }
        }
    }

    [Fact]
    public void A5_TheBuffShowsInsideWithTheQuestOnly()
    {
        var holder = Player(FarAway);
        var other = Player(FarAway);

        try
        {
            Take<DefyingTheArcane>(holder);

            holder.MoveToWorld(WoodsEdge, Map.Trammel);
            other.MoveToWorld(WoodsEdge, Map.Trammel);
            Assert.True(ApprenticeTraining.HasBuff(holder));
            Assert.False(ApprenticeTraining.HasBuff(other));

            holder.MoveToWorld(FarAway, Map.Trammel);
            Assert.False(ApprenticeTraining.HasBuff(holder));
        }
        finally
        {
            Forget(holder);
            Forget(other);
        }
    }

    // ---------------------------------------------------------------- A6

    [Fact]
    public void A6_TheTableIsPinnedsTwentySixAcceleratedQuests()
    {
        var pinned = typeof(DefyingTheArcane).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(DefyingTheArcane).Namespace && typeof(MLQuest).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Type: t, Quest: SafeCreate(t)))
            .Where(q => q.Quest?.Objectives.OfType<GainSkillObjective>().Any(o => o.Accelerate) == true)
            .ToList();

        Assert.Equal(26, pinned.Count);
        Assert.Equal(26, ApprenticeTraining.Areas.Length);
        Assert.Equal(26, ApprenticeTraining.Areas.Select(a => a.Skill).Distinct().Count());

        foreach (var (type, quest) in pinned)
        {
            var area = Assert.Single(ApprenticeTraining.Areas, a => a.Quest == type);
            Assert.Equal(area.Skill, quest!.Objectives.OfType<GainSkillObjective>().Single().Skill);
        }

        Assert.Equal(20, ApprenticeTraining.Areas.Count(a => a.Region == "Old Haven Training"));
    }

    private static MLQuest SafeCreate(Type t)
    {
        try
        {
            return t.GetConstructor(Type.EmptyTypes)?.Invoke(null) as MLQuest;
        }
        catch
        {
            return null;
        }
    }
}
