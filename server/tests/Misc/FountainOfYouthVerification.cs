// FountainOfYouthVerification.cs
//
// cc-P192: the Fountain of Youth (customizations FountainOfYouth/*, ClusterFYoungPlayer's Young clock, the patches
// Account-young-duration and BaseBoatDeed-fountain-sanctum). Notes: shard-migration notes/cc-P192-cont-fountain-of-youth.md.
//
// Facts:
//   Y1. Drinking makes the account Young again: the flag, every character, the tutorial (back to the start with the
//       drinker in it), the New Haven trainer quest done records; skills, items, gold and other quests are untouched.
//   Y2. The Young clock restarts: an account with 400 hours played stays Young after a drink, and loses it again two
//       weeks of play later; an account that never drank keeps pinned's rule (whole TotalGameTime).
//   S1. The test seeder (Fixed) places it in New Haven at 3497,2583,14, once.
//   R1. Roaming: it starts in a pool area and 200 moves each land on a valid tile of a different pool entry (never the
//       same twice in a row), never in the guarded part, never in a climbing-only entry.
//   N1. Around a roaming fountain Mark is refused (staff exempt), so is recall into it, houses and boats; after it moves
//       away, Mark works there again.
//   A1. The secret achievement: earned by a roaming drink, once; a fixed (test shard) drink does not earn it.
//   C1. A second drink within the misclick cooldown is refused; the account cooldown and the limits are off by default
//       and refuse when set.
//   T1. The fountain's mode, pool entry and placed time survive a save and load, and its sanctum comes back.
//   W1. Nothing reaches players: a Staff patch note about it is in neither the bulletin nor the wiki page, and no player
//       text of the fountain or its achievement names a pool place.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Engines.FountainOfYouth;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class FountainOfYouthVerification
{
    private readonly ITestOutputHelper _out;

    public FountainOfYouthVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();

        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private static readonly FieldInfo GameTime =
        typeof(Account).GetField("_totalGameTime", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Account NewAccount() => new($"p192{Guid.NewGuid():N}"[..16], "p192-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot, Point3D at)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.Young = account.Young;
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                MLQuestSystem.Contexts.Remove(pm);
                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static void ClearFountains()
    {
        foreach (var f in FountainOfYouthSystem.Fountains.ToList())
        {
            f.Delete();
        }
    }

    // A grown-up account: Young removed, 400 hours played, the tutorial graduated.
    private static Account GrownUp(out PlayerMobile drinker, out PlayerMobile alt, Point3D at)
    {
        var account = NewAccount();
        drinker = NewCharacter(account, 0, at);
        alt = NewCharacter(account, 1, at);
        GameTime.SetValue(account, TimeSpan.FromHours(400));
        account.RemoveYoungStatus(0);

        var record = ClusterFTutorial.RecordOf(drinker)!;
        record.State = TutorialState.Graduated;
        record.CharacterSerial = (uint)alt.Serial;
        record.StationsDone.Add("vault.cleared");
        return account;
    }

    private static MLQuest Registered<T>() where T : MLQuest, new()
    {
        if (!MLQuestSystem.Quests.TryGetValue(typeof(T), out var quest))
        {
            MLQuestSystem.Quests[typeof(T)] = quest = new T();
        }

        return quest;
    }

    // ---------------------------------------------------------------- Y1, Y2

    [Fact]
    public void Y1_DrinkingMakesTheAccountYoungAgainAndKeepsEverythingElse()
    {
        ClearFountains();
        var fountain = new FountainOfYouthAddon(false);
        fountain.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        var account = GrownUp(out var drinker, out var alt, new Point3D(1301, 1301, 0));

        try
        {
            var trainerQuest = Registered<DefyingTheArcane>();
            var otherQuest = Registered<TheArtOfWar>();
            MLQuestSystem.GetOrCreateContext(alt).SetDoneQuest(trainerQuest);
            MLQuestSystem.GetOrCreateContext(drinker).SetDoneQuest(otherQuest);

            drinker.Skills[SkillName.Tactics].Base = 77.7;
            var sword = new Longsword();
            drinker.Backpack.DropItem(sword);
            drinker.BankBox.DropItem(new Gold(1234));

            Assert.False(account.Young);
            Assert.False(drinker.Young);
            Assert.False(alt.Young);

            var reset = FountainOfYouthSystem.Drink(fountain, drinker);
            Assert.NotNull(reset);
            _out.WriteLine(string.Join(" | ", reset!));

            Assert.True(account.Young);
            Assert.True(drinker.Young);
            Assert.True(alt.Young);

            var record = ClusterFTutorial.RecordOf(drinker)!;
            Assert.Equal(ClusterFTutorial.Enabled ? TutorialState.InCore : TutorialState.NotStarted, record.State);
            Assert.Empty(record.StationsDone);
            if (ClusterFTutorial.Enabled)
            {
                Assert.Equal((uint)drinker.Serial, record.CharacterSerial);
            }

            Assert.False(MLQuestSystem.GetContext(alt)!.HasDoneQuest(trainerQuest));
            Assert.False(MLQuestSystem.GetContext(drinker)!.HasDoneQuest(otherQuest)); // TheArtOfWar is a trainer quest too

            // Kept.
            Assert.Equal(77.7, drinker.Skills[SkillName.Tactics].Base, 1);
            Assert.False(sword.Deleted);
            Assert.True(sword.IsChildOf(drinker.Backpack));
            Assert.Equal(1234, drinker.BankBox.Items.OfType<Gold>().Sum(g => g.Amount));
            Assert.Equal(TimeSpan.FromHours(400), account.TotalGameTime);
        }
        finally
        {
            fountain.Delete();
            Cleanup(account);
        }
    }

    [Fact]
    public void Y2_TheYoungClockRestartsAndAnAccountThatNeverDrankKeepsPinnedsRule()
    {
        ClearFountains();
        var fountain = new FountainOfYouthAddon(false);
        fountain.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        var account = GrownUp(out var drinker, out _, new Point3D(1301, 1301, 0));
        var never = NewAccount();
        NewCharacter(never, 0, new Point3D(1301, 1301, 0));

        try
        {
            // Never drank: the whole 400 hours count, as pinned (Account.CheckYoung).
            GameTime.SetValue(never, TimeSpan.FromHours(400));
            Assert.Equal(TimeSpan.FromHours(400), ClusterFYoungPlayer.YoungPlayed(never));
            never.CheckYoung();
            Assert.False(never.Young);

            Assert.NotNull(FountainOfYouthSystem.Drink(fountain, drinker));
            Assert.Equal(TimeSpan.Zero, ClusterFYoungPlayer.YoungPlayed(account));
            account.CheckYoung();
            Assert.True(account.Young);

            GameTime.SetValue(account, TimeSpan.FromHours(400) + ClusterFYoungPlayer.Duration - TimeSpan.FromMinutes(1));
            account.CheckYoung();
            Assert.True(account.Young);

            GameTime.SetValue(account, TimeSpan.FromHours(400) + ClusterFYoungPlayer.Duration);
            account.CheckYoung();
            Assert.False(account.Young);
            Assert.False(drinker.Young);
        }
        finally
        {
            fountain.Delete();
            Cleanup(account);
            Cleanup(never);
        }
    }

    // ---------------------------------------------------------------- S1

    [Fact]
    public void S1_TheTestSeederPlacesItInNewHavenOnce()
    {
        ClearFountains();

        try
        {
            _out.WriteLine(FountainOfYouthSystem.Seed(true, FountainMode.Fixed));
            Assert.Empty(FountainOfYouthSystem.Fountains);

            _out.WriteLine(FountainOfYouthSystem.Seed(false, FountainMode.Fixed));
            var f = Assert.Single(FountainOfYouthSystem.Fountains);
            Assert.Equal(new Point3D(3497, 2583, 14), f.Location);
            Assert.Equal(Map.Trammel, f.Map);
            Assert.Equal(FountainMode.Fixed, f.Mode);
            Assert.Null(f.Sanctum); // the test shard's fixed fountain has no no-mark zone
            Assert.Equal(16, f.Components.Count);
            Assert.All(f.Components, c => Assert.Equal(FountainOfYouthAddon.FountainHue, c.Hue));

            FountainOfYouthSystem.Seed(false, FountainMode.Fixed);
            Assert.Single(FountainOfYouthSystem.Fountains);
        }
        finally
        {
            ClearFountains();
        }
    }

    // ---------------------------------------------------------------- the test pool

    private sealed class TestPool : IDisposable
    {
        public readonly List<Region> Regions = [];
        public readonly GuardedRegion Guarded;
        public readonly Rectangle2D GuardedArea = new(100, 3000, 20, 40);

        public TestPool()
        {
            // Five areas on Trammel, 60 x 40 each; a guarded strip over half of the first.
            for (var i = 0; i < 5; i++)
            {
                var r = new BaseRegion($"P192 Pool {i}", Map.Trammel, Region.DefaultPriority,
                    new Rectangle3D(new Point3D(100 + i * 80, 3000, Region.MinZ), new Point3D(160 + i * 80, 3040, Region.MaxZ)));
                r.Register();
                Regions.Add(r);
            }

            Guarded = new GuardedRegion("P192 Guarded", Map.Trammel, Region.DefaultPriority + 5, GuardedArea);
            Guarded.Register();

            FountainPool.Override =
            [
                ..Enumerable.Range(0, 5).Select(i => new FountainPoolEntry { Key = $"t{i}", Map = "Trammel", Region = $"P192 Pool {i}" }),
                new FountainPoolEntry { Key = "peak", Map = "Trammel", Rects = [[600, 3000, 640, 3040]], Access = "climbing" },
                new FountainPoolEntry { Key = "nowhere", Map = "Trammel", Region = "P192 No Such Region" }
            ];
        }

        public void Dispose()
        {
            FountainPool.Override = null;
            Guarded.Unregister();
            foreach (var r in Regions)
            {
                r.Unregister();
            }

            ClearFountains();
        }
    }

    // ---------------------------------------------------------------- R1

    [Fact]
    public void R1_TwoHundredMovesEachLandOnAValidTileOfAnotherPoolEntry()
    {
        ClearFountains();
        using var pool = new TestPool();

        // cc-P238: climbing boots exist now, so "climbing" is available on the shard (FountainPool.AccessChecks). This fact
        // is about an UNAVAILABLE access kind never being used, so it pins climbing off for its own run.
        var climbing = FountainPool.AccessChecks["climbing"];
        FountainPool.AccessChecks["climbing"] = () => false;
        try
        {
            _out.WriteLine(FountainOfYouthSystem.Seed(false, FountainMode.Roaming));
            var f = Assert.Single(FountainOfYouthSystem.Fountains);
            Assert.Equal(FountainMode.Roaming, f.Mode);

            var used = new Dictionary<string, int>();
            var previous = f.PoolKey;

            for (var i = 0; i < 200; i++)
            {
                Assert.True(FountainOfYouthSystem.Move(f, "test"), $"move {i} found no spot");
                var entry = FountainPool.Override!.Single(e => e.Key == f.PoolKey);

                Assert.NotEqual(previous, f.PoolKey);
                Assert.Equal("none", entry.Access); // the climbing-only peak is never used
                Assert.NotEqual("nowhere", entry.Key);
                Assert.True(FountainPool.Valid(entry, f.Map!, f.X, f.Y, out var z) && z == f.Z, $"move {i}: {f.Location} is not valid");

                for (var dx = FountainPool.FootMin; dx <= FountainPool.FootMax; dx++)
                {
                    for (var dy = FountainPool.FootMin; dy <= FountainPool.FootMax; dy++)
                    {
                        var p = new Point3D(f.X + dx, f.Y + dy, f.Z);
                        Assert.False(pool.GuardedArea.Contains(p), $"move {i}: {p} is in the guarded strip");
                        Assert.True(Region.Find(p, f.Map).IsPartOf(entry.Region!), $"move {i}: {p} is outside {entry.Region}");
                    }
                }

                used[f.PoolKey!] = used.GetValueOrDefault(f.PoolKey!) + 1;
                previous = f.PoolKey;
            }

            _out.WriteLine(string.Join(", ", used.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Assert.Equal(5, used.Count);
        }
        finally
        {
            FountainPool.AccessChecks["climbing"] = climbing;
        }
    }

    // ---------------------------------------------------------------- N1

    [Fact]
    public void N1_MarkRecallHousesAndBoatsAreRefusedAroundItUntilItMoves()
    {
        ClearFountains();
        using var pool = new TestPool();

        FountainOfYouthSystem.Seed(false, FountainMode.Roaming);
        var f = Assert.Single(FountainOfYouthSystem.Fountains);
        var spot = new Point3D(f.X + 5, f.Y + 5, f.Z);
        var account = NewAccount();
        var pm = NewCharacter(account, 0, spot);
        var staff = new PlayerMobile { Player = true, AccessLevel = AccessLevel.GameMaster };
        staff.MoveToWorld(spot, f.Map);

        try
        {
            Assert.IsType<FountainSanctumRegion>(Region.Find(spot, f.Map));
            Assert.Null(Region.Find(spot, f.Map).Name);

            Assert.False(SpellHelper.CheckTravel(pm, TravelCheckType.Mark, out var message));
            _out.WriteLine($"Mark refused: {message?.Number} {message?.String}");
            Assert.True(SpellHelper.CheckTravel(staff, TravelCheckType.Mark, out _));

            var outside = new PlayerMobile { Player = true };
            outside.MoveToWorld(new Point3D(f.X + 200, f.Y, f.Z), f.Map);
            Assert.False(SpellHelper.CheckTravel(outside, f.Map, spot, TravelCheckType.RecallTo, out _));
            Assert.False(SpellHelper.CheckTravel(outside, f.Map, spot, TravelCheckType.GateTo, out _));
            outside.Delete();

            Assert.False(Region.Find(spot, f.Map).AllowHousing(pm, spot));
            Assert.True(FountainSanctumRegion.RefusesBoats(Region.Find(spot, f.Map)));

            var edge = new Point3D(f.X + FountainOfYouthSystem.NoMarkRadius + 1, f.Y, f.Z);
            Assert.IsNotType<FountainSanctumRegion>(Region.Find(edge, f.Map));

            // It moves: the old spot is markable again (and unless it landed close by, no sanctum covers it).
            var old = f.Location;
            Assert.True(FountainOfYouthSystem.Move(f, "test"));
            if (Utility.InRange(old, f.Location, 2 * FountainOfYouthSystem.NoMarkRadius + 2) == false)
            {
                Assert.IsNotType<FountainSanctumRegion>(Region.Find(spot, f.Map));
                Assert.True(SpellHelper.CheckTravel(pm, TravelCheckType.Mark, out _));
            }

            f.Delete();
            Assert.IsNotType<FountainSanctumRegion>(Region.Find(spot, Map.Trammel));
            Assert.True(SpellHelper.CheckTravel(pm, TravelCheckType.Mark, out _));
        }
        finally
        {
            staff.Delete();
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- A1, C1

    [Fact]
    public void A1_TheSecretAchievementIsEarnedByARoamingDrinkOnce()
    {
        ClearFountains();
        using var pool = new TestPool();
        var fixedOne = new FountainOfYouthAddon(false);
        fixedOne.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        var account = GrownUp(out var pm, out _, new Point3D(1301, 1301, 0));
        var was = FountainOfYouthSystem.MisclickCooldownMinutes;
        FountainOfYouthSystem.MisclickCooldownMinutes = 0;

        try
        {
            var def = ClusterFAchievementSystem.Definitions[FountainOfYouthSystem.AchievementKey];
            Assert.True(def.Hidden);

            Assert.NotNull(FountainOfYouthSystem.Drink(fixedOne, pm));
            Assert.False(ClusterFAchievementSystem.HasEarned(account, FountainOfYouthSystem.AchievementKey));
            fixedOne.Delete();

            FountainOfYouthSystem.Seed(false, FountainMode.Roaming);
            var roaming = Assert.Single(FountainOfYouthSystem.Fountains);
            pm.MoveToWorld(new Point3D(roaming.X + 2, roaming.Y + 2, roaming.Z), roaming.Map);
            var before = roaming.Location;

            Assert.NotNull(FountainOfYouthSystem.Drink(roaming, pm));
            Assert.True(ClusterFAchievementSystem.HasEarned(account, FountainOfYouthSystem.AchievementKey));
            Assert.NotEqual(before, roaming.Location); // it moved after the drink

            pm.MoveToWorld(new Point3D(roaming.X + 2, roaming.Y + 2, roaming.Z), roaming.Map);
            Assert.NotNull(FountainOfYouthSystem.Drink(roaming, pm));
            Assert.False(ClusterFAchievementSystem.TryGrant(account, FountainOfYouthSystem.AchievementKey)); // already earned
        }
        finally
        {
            FountainOfYouthSystem.MisclickCooldownMinutes = was;
            Cleanup(account);
        }
    }

    [Fact]
    public void C1_CooldownsAndLimitsRefuseOnlyWhenSet()
    {
        ClearFountains();
        var fountain = new FountainOfYouthAddon(false);
        fountain.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        var account = GrownUp(out var pm, out _, new Point3D(1301, 1301, 0));

        try
        {
            Assert.Equal(0.0, FountainOfYouthSystem.AccountCooldownDays);
            Assert.Equal(0, FountainOfYouthSystem.MaxSkillTotal);
            Assert.Equal(0.0, FountainOfYouthSystem.MaxAccountAgeDays);

            Assert.NotNull(FountainOfYouthSystem.Drink(fountain, pm));
            Assert.NotNull(FountainOfYouthSystem.Refusal(pm)); // the misclick cooldown
            Assert.Null(FountainOfYouthSystem.Drink(fountain, pm));

            account.SetTag(FountainOfYouthSystem.LastDrinkTag, (Core.Now - TimeSpan.FromHours(1)).Ticks.ToString());
            Assert.Null(FountainOfYouthSystem.Refusal(pm));

            FountainOfYouthSystem.AccountCooldownDays = 30;
            Assert.NotNull(FountainOfYouthSystem.Refusal(pm));
            FountainOfYouthSystem.AccountCooldownDays = 0;

            pm.Skills[SkillName.Tactics].Base = 100;
            FountainOfYouthSystem.MaxSkillTotal = 50;
            Assert.NotNull(FountainOfYouthSystem.Refusal(pm));
            FountainOfYouthSystem.MaxSkillTotal = 0;

            // Out of range: no drink.
            pm.MoveToWorld(new Point3D(1310, 1310, 0), Map.Trammel);
            Assert.False(fountain.InUseRange(pm));
            Assert.Null(FountainOfYouthSystem.Drink(fountain, pm));
        }
        finally
        {
            FountainOfYouthSystem.AccountCooldownDays = 0;
            FountainOfYouthSystem.MaxSkillTotal = 0;
            fountain.Delete();
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- T1

    [Fact]
    public void T1_ModeEntryAndTimeSurviveASaveAndLoad()
    {
        ClearFountains();
        using var pool = new TestPool();

        FountainOfYouthSystem.Seed(false, FountainMode.Roaming);
        var original = Assert.Single(FountainOfYouthSystem.Fountains);
        var key = original.PoolKey;
        var placed = original.PlacedAt;
        var at = original.Location;

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var serial = original.Serial;
        var components = original.Components.ToList();
        original.Delete();
        Assert.IsNotType<FountainSanctumRegion>(Region.Find(at, Map.Trammel));

        var copy = new FountainOfYouthAddon(serial);
        copy.Deserialize(new BufferReader(buffer));

        try
        {
            Assert.Equal(FountainMode.Roaming, copy.Mode);
            Assert.Equal(key, copy.PoolKey);
            Assert.Equal(placed, copy.PlacedAt);
            Assert.Equal(at, copy.Location);
            Assert.Contains(copy, FountainOfYouthSystem.Fountains);
            Assert.NotNull(copy.Sanctum);
            Assert.IsType<FountainSanctumRegion>(Region.Find(at, Map.Trammel));
        }
        finally
        {
            copy.Delete();
            components.ForEach(c => c.Delete());
        }
    }

    // ---------------------------------------------------------------- W1

    [Fact]
    public void W1_NothingAboutItReachesPlayers()
    {
        const string changelog = """
            ## 2026.10.20.9

            ### Added
            - Something players see.

            ### Staff
            - The Fountain of Youth: [WhereIsFountain, [GoFountain, [MoveFountain.
            """;

        var versions = PatchNotes.Parse(changelog);
        var wiki = PatchNotes.RenderDokuWiki(versions);
        var bulletin = string.Join("\n", PatchNotes.BulletinLines(versions[0]));

        Assert.Contains("Something players see.", wiki);
        Assert.DoesNotContain("Fountain", wiki);
        Assert.DoesNotContain("Fountain", bulletin);

        // No player text of the fountain or its achievement names a place from the built-in pool, or a coordinate.
        var def = ClusterFAchievementSystem.Definitions[FountainOfYouthSystem.AchievementKey];
        var account = NewAccount();
        var pm = NewCharacter(account, 0, new Point3D(1301, 1301, 0));
        var fountain = new FountainOfYouthAddon(true);

        try
        {
            var texts = new List<string> { def.Title, def.Description, def.FlavorText, def.EarnedLine, FountainOfYouthGump.Body };

            var places = FountainPool.Defaults.SelectMany(e => new[] { e.Region ?? "", e.Key, e.Why }).Where(s => s.Length > 0)
                .Concat(["Trammel", "Felucca", "Ilshenar", "Malas", "dungeon", "island", "Isle", "mine", "cave"]).ToList();

            foreach (var text in texts)
            {
                _out.WriteLine(text);
                foreach (var place in places)
                {
                    Assert.False(text.Contains(place, StringComparison.OrdinalIgnoreCase), $"\"{text}\" names \"{place}\"");
                }
            }
        }
        finally
        {
            fountain.Delete();
            Cleanup(account);
        }
    }
}
