// HerbalismVerification.cs
//
// cc-P804 (F-51, phase 1). Notes in shard-migration notes/cc-P804-herbalism-phase-1.md.
//
// Facts: the skill and its gain windows; a patch picks, hides and respawns; patches by tier; the world seed is idempotent; a bowl grows
// and yields without end; a bed is one unit; a cross stays in the lane, mutates up, and obeys the skill cap; processing amounts and the
// starter farm's daily reagents; neglect's windows; recipe gating; save and load of every new type; the settings file.

using System;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Plants;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HerbalismVerification : IDisposable
{
    private static bool _achievements;
    private readonly ITestOutputHelper _out;
    private readonly Account _account;
    private readonly PlayerMobile _pm;
    private static readonly Point3D Spot = new(3460, 2604, 18);

    public HerbalismVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        HerbalismSettings.Current = new HerbalismSettings();

        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        _account = new Account($"p804{Guid.NewGuid():N}"[..16], "p804-test-only");
        _pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        _pm.AddItem(new Backpack());
        _account[0] = _pm;
        _pm.MoveToWorld(Spot, Map.Trammel);
    }

    public void Dispose()
    {
        HerbalismSettings.Current = new HerbalismSettings();
        _pm.Delete();
        Accounts.Remove(_account);
    }

    private void SetSkill(double value) => _pm.Skills[SkillName.Herbalism].Base = value;

    private PlantItem Bowl(PlantType type, int tier = 0, PlantStatus status = PlantStatus.Stage9, Mobile tender = null)
    {
        var bowl = new PlantItem { PlantType = type, PlantHue = PlantHue.Plain, PlantStatus = status };
        _pm.Backpack.DropItem(bowl);
        HerbalLane.OnPlanted(tender, bowl, tier);
        return bowl;
    }

    private static void Day(PlantItem plant)
    {
        plant.PlantSystem.NextGrowth = Core.Now.AddMinutes(-1);
        plant.PlantSystem.Water = 2;
        plant.PlantSystem.Hits = plant.PlantSystem.MaxHits;
        plant.PlantSystem.Infestation = plant.PlantSystem.Fungus = 0; // a well-kept plant: maladies cut the yield
        plant.PlantSystem.DoGrowthCheck();
    }

    private static void Idle(PlantItem plant, double days) => HerbalLane.Record(plant).LastTended = Core.Now.AddDays(-days);

    private static T RoundTrip<T>(T original) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();
        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private System.Collections.Generic.List<T> InPack<T>() where T : Item
    {
        var list = new System.Collections.Generic.List<T>();

        foreach (var item in _pm.Backpack.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    private int RawInPack(string key)
    {
        var total = 0;

        foreach (var raw in _pm.Backpack.FindItemsByType<RawHerb>())
        {
            if (raw.HerbKey == key)
            {
                total += raw.Amount;
            }
        }

        return total;
    }

    // ---------------------------------------------------------------- the skill

    [Fact]
    public void TheSkillIsRow58AndTheAlembicOwnsIt()
    {
        Assert.Equal(59, SkillInfo.Table.Length);
        Assert.Equal("Herbalism", SkillInfo.Table[58].Name);
        Assert.Equal(58, (int)SkillName.Herbalism);
        Assert.Contains(SkillName.Herbalism, GuildSkillTable.For(ClusterFAlembicGuild.Key));
    }

    [Theory]
    [InlineData("Garlic", 0, 0, 50)]
    [InlineData("Mandrake", 0, 50, 100)]
    [InlineData("GraveMoss", 0, 100, 150)]
    [InlineData("Garlic", 8, 40, 90)]
    [InlineData("Mandrake", 16, 130, 180)]
    public void GainWindowsClimbByHerbTierAndStrain(string herb, int strain, double min, double max)
    {
        var (low, high) = HerbalLane.GainWindow(HerbalismSettings.Current.Herb(herb), strain);
        Assert.Equal(min, low);
        Assert.Equal(max, high);
    }

    [Fact]
    public void TopStrainsKeepTheSkillRisingTo200()
    {
        var (_, high) = HerbalLane.GainWindow(HerbalismSettings.Current.Herb("GraveMoss"), 16);
        Assert.Equal(200, high);
    }

    // ---------------------------------------------------------------- foraging

    private HerbPatch Patch(string herb)
    {
        var patch = new HerbPatch(herb, $"test:{Guid.NewGuid():N}");
        patch.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return patch;
    }

    [Fact]
    public void APickedPatchHidesLogsTheFindAndRespawns()
    {
        var patch = Patch("Garlic");

        try
        {
            Assert.True(patch.Pick(_pm, true));
            Assert.False(patch.Ripe);
            var got = RawInPack("Garlic");
            Assert.InRange(got, HerbalismSettings.Current.ForageMin, HerbalismSettings.Current.ForageMax);
            Assert.Equal(got, HerbalLane.Found(_pm, "Garlic"));

            // Hidden: nothing to pick until it regrows.
            Assert.False(patch.Pick(_pm, true));
            Assert.Equal(0, HerbPatch.RespawnDue());
            Assert.False(patch.Ripe);

            patch.RipeAt = Core.Now.AddSeconds(-1);
            Assert.Equal(1, HerbPatch.RespawnDue());
            Assert.True(patch.Ripe);
        }
        finally
        {
            patch.Delete();
        }
    }

    [Theory]
    [InlineData("Garlic", 0, true)]
    [InlineData("Mandrake", 49.9, false)]
    [InlineData("Mandrake", 50, true)]
    [InlineData("GraveMoss", 99.9, false)]
    [InlineData("GraveMoss", 100, true)]
    public void PatchesAskForTheirTier(string herb, double skill, bool picked)
    {
        SetSkill(skill);
        var patch = Patch(herb);

        try
        {
            Assert.Equal(picked, patch.Pick(_pm, true));
        }
        finally
        {
            patch.Delete();
        }
    }

    [Fact]
    public void ExoticsAreAStubWithNoWildPatch()
    {
        var s = HerbalismSettings.Current;
        Assert.True(s.Herb("Exotic").Stub);
        Assert.DoesNotContain(s.Patches, p => p.Herb == "Exotic");
    }

    [Fact]
    public void TheWorldSeedPlacesOnlyWhatIsMissing()
    {
        HerbalismSettings.Current.Patches = [new HerbSpot { Herb = "Garlic", X = Spot.X + 3, Y = Spot.Y, Map = "Trammel" }];
        var before = HerbPatch.Patches.ToList();
        var first = HerbalismSystem.SeedWorld();
        _out.WriteLine(first);
        var second = HerbalismSystem.SeedWorld();
        _out.WriteLine(second);

        try
        {
            Assert.Contains("0 placed", second);
            Assert.Contains("already there; herb table", second.Replace(", 0 with no open ground ()", ""));
            Assert.Single(HerbPatch.Patches.Except(before));
            Assert.Single(World.Items.Values.OfType<AlembicHerbTable>());
        }
        finally
        {
            foreach (var p in HerbPatch.Patches.Except(before).ToList())
            {
                p.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- bowls

    [Fact]
    public void ABowlGrowsAtOsiPaceThenYieldsWithoutEnd()
    {
        var bowl = Bowl(PlantType.Garlic, 0, PlantStatus.Seed);

        try
        {
            Assert.Equal(PlantSystem.CheckDelay, HerbalLane.GrowthDelay(bowl.PlantSystem));

            for (var day = 0; day < 8; day++)
            {
                HerbalLane.Tended(_pm, bowl);
                Day(bowl);
            }

            Assert.Equal(PlantStatus.Stage9, bowl.PlantStatus);
            Assert.Equal(0, bowl.PlantSystem.AvailableResources);

            // Past OSI's eight, it keeps giving, and the stock count never moves.
            for (var day = 0; day < 20; day++)
            {
                HerbalLane.Tended(_pm, bowl);
                Day(bowl);
                Assert.True(HerbalLane.TryHarvest(_pm, bowl));
            }

            Assert.Equal(20 * HerbalismSettings.Current.BowlHerbsPerDay, RawInPack("Garlic"));
            Assert.Equal(8, bowl.PlantSystem.LeftResources);
            Assert.Equal(StrainHueOf(0), bowl.Hue);
        }
        finally
        {
            bowl.Delete();
        }
    }

    private static int StrainHueOf(int tier) => ClusterFLeagueRanks.Ladder[tier].Hue;

    [Fact]
    public void AStockPlantKeepsOsisEightAndPassesEveryHook()
    {
        var plant = new PlantItem { PlantType = PlantType.Poppies, PlantHue = PlantHue.BrightOrange, PlantStatus = PlantStatus.Stage9 };
        _pm.Backpack.DropItem(plant);

        try
        {
            Assert.False(HerbalLane.IsReagent(plant));
            Assert.False(HerbalLane.TryHarvest(_pm, plant));
            Assert.False(HerbalLane.SparesDamage(plant.PlantSystem));
            Day(plant);
            Assert.Equal(1, plant.PlantSystem.AvailableResources);
            Assert.Equal(7, plant.PlantSystem.LeftResources);
            Assert.False(HerbalismSeedHeld(plant));
        }
        finally
        {
            plant.Delete();
        }
    }

    private static bool HerbalismSeedHeld(PlantItem plant) => HerbalStore.Rows.ContainsKey(plant.Serial);

    [Fact]
    public void TheHarvestStoresOnlySoManyDays()
    {
        var bowl = Bowl(PlantType.Ginseng);

        try
        {
            for (var day = 0; day < 30; day++)
            {
                HerbalLane.Tended(_pm, bowl);
                Day(bowl);
            }

            var s = HerbalismSettings.Current;
            Assert.Equal(s.BowlHerbsPerDay * s.MaxStoredDays, bowl.PlantSystem.AvailableResources);
        }
        finally
        {
            bowl.Delete();
        }
    }

    [Fact]
    public void TierSkillAndMaladiesMoveTheYield()
    {
        var bowl = Bowl(PlantType.Garlic, 4, tender: _pm);

        try
        {
            Assert.Equal(10, HerbalLane.DailyYield(bowl)); // 6 + 4 tiers
            SetSkill(200);
            HerbalLane.Tended(_pm, bowl);
            Assert.Equal(20, HerbalLane.DailyYield(bowl));
            bowl.PlantSystem.Infestation = 2;
            Assert.Equal(14, HerbalLane.DailyYield(bowl)); // 30% off
        }
        finally
        {
            bowl.Delete();
        }
    }

    [Fact]
    public void TheFirstHarvestEarnsGreenThumbOnce()
    {
        var bowl = Bowl(PlantType.Garlic);

        try
        {
            Assert.False(ClusterFAchievementSystem.HasEarned(_account, HerbalLane.FirstHarvestAchievement));
            bowl.PlantSystem.AvailableResources = 3;
            HerbalLane.TryHarvest(_pm, bowl);
            Assert.True(ClusterFAchievementSystem.HasEarned(_account, HerbalLane.FirstHarvestAchievement));
            Assert.Equal(3, HerbalLane.Found(_pm, "Garlic"));
        }
        finally
        {
            bowl.Delete();
        }
    }

    // ---------------------------------------------------------------- beds

    private RaisedGardenPlantItem[] Bed(PlantType type, out RaisedGardenAddon addon, int tier = 0)
    {
        addon = new RaisedGardenAddon(RaisedGardenDirection.Small);
        addon.MoveToWorld(new Point3D(Spot.X + 4, Spot.Y + 4, Spot.Z), Map.Trammel);
        var mounds = new System.Collections.Generic.List<RaisedGardenPlantItem>();

        foreach (var part in addon.Components.OfType<GardenAddonComponent>())
        {
            var mound = new RaisedGardenPlantItem();
            mound.MoveToWorld(new Point3D(part.X, part.Y, part.Z + 5), part.Map);
            mound.Component = part;
            part.Plant = mound;
            mound.PlantType = type;
            mound.PlantHue = PlantHue.Plain;
            mound.PlantStatus = PlantStatus.Stage9;
            HerbalLane.OnPlanted(_pm, mound, tier);
            mounds.Add(mound);
        }

        return mounds.ToArray();
    }

    [Fact]
    public void ABedIsOneUnit()
    {
        var mounds = Bed(PlantType.Mandrake, out var addon);

        try
        {
            Assert.Equal(4, mounds.Length);
            Assert.All(mounds, m => Assert.True(m.RequiresUpkeep));
            Assert.All(mounds, m => Assert.Equal(2, m.PlantSystem.Water));

            // One watering reaches them all.
            mounds[0].PlantSystem.Water = 4;
            Idle(mounds[1], 3);
            HerbalLane.Tended(_pm, mounds[0]);
            Assert.All(mounds, m => Assert.Equal(4, m.PlantSystem.Water));
            Assert.All(mounds, m => Assert.True((Core.Now - HerbalLane.Record(m).LastTended).TotalMinutes < 1));

            // Grows and yields: one harvest takes the whole bed.
            foreach (var m in mounds)
            {
                Day(m);
            }

            Assert.True(HerbalLane.TryHarvest(_pm, mounds[2]));
            Assert.Equal(4 * HerbalismSettings.Current.BedHerbsPerDay, RawInPack("Mandrake"));
            Assert.All(mounds, m => Assert.Equal(0, m.PlantSystem.AvailableResources));
        }
        finally
        {
            addon.Delete();

            foreach (var m in mounds)
            {
                m.Delete();
            }
        }
    }

    [Fact]
    public void AStockMoundStaysCarefreeAndBedsTakeFewerMaladies()
    {
        var stock = new RaisedGardenPlantItem();
        stock.MoveToWorld(Spot, Map.Trammel);
        var mounds = Bed(PlantType.Garlic, out var addon);

        try
        {
            Assert.False(stock.RequiresUpkeep);
            Assert.True(HerbalLane.SkipsMaladies(mounds[0].PlantSystem, 0.5));
            Assert.False(HerbalLane.SkipsMaladies(mounds[0].PlantSystem, 0.1));
        }
        finally
        {
            stock.Delete();
            addon.Delete();

            foreach (var m in mounds)
            {
                m.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- crossing

    [Fact]
    public void ReagentCrossesStayInTheLaneAndRefuseStock()
    {
        for (var i = 0; i < 50; i++)
        {
            var type = PlantTypeInfo.Cross(PlantType.Garlic, PlantType.BloodMoss);
            Assert.True(HerbalLane.IsReagent(type));
            Assert.Equal(PlantType.Mandrake, type);
        }

        var bowl = Bowl(PlantType.Garlic);
        var stock = new PlantItem { PlantType = PlantType.Fern, PlantHue = PlantHue.Plain, PlantStatus = PlantStatus.Stage9 };

        try
        {
            Assert.True(HerbalLane.RefusesPollen(bowl, stock));
            Assert.False(HerbalLane.RefusesPollen(bowl, bowl));
        }
        finally
        {
            bowl.Delete();
            stock.Delete();
        }
    }

    [Fact]
    public void ASameTierCrossCanMutateUpAndHerbalismRaisesTheChance()
    {
        SetSkill(200);
        var a = Bowl(PlantType.Garlic, 3);
        var b = Bowl(PlantType.Ginseng, 3);

        try
        {
            Assert.True(HerbalLane.OnPollinated(_pm, a, b, 0.0));
            Assert.Equal(4, HerbalLane.Record(b).SeedTier);

            var seed = Assert.IsType<ReagentSeed>(HerbalLane.NewSeed(b, b.PlantSystem.Also(PlantType.Garlic)));
            Assert.Equal(4, seed.StrainTier);
            seed.Delete();

            // No luck: the parents' strain.
            Assert.False(HerbalLane.OnPollinated(_pm, a, b, 0.99));
            Assert.Equal(3, HerbalLane.Record(b).SeedTier);

            Assert.True(HerbalLane.MutationChance(200) > HerbalLane.MutationChance(0));
        }
        finally
        {
            a.Delete();
            b.Delete();
        }
    }

    [Fact]
    public void CrossesOfDifferentTiersTakeTheBetterAndNeverMutate()
    {
        SetSkill(200);
        var a = Bowl(PlantType.Garlic, 2);
        var b = Bowl(PlantType.Ginseng, 5);

        try
        {
            Assert.False(HerbalLane.OnPollinated(_pm, a, b, 0.0));
            Assert.Equal(5, HerbalLane.Record(b).SeedTier);
        }
        finally
        {
            a.Delete();
            b.Delete();
        }
    }

    [Fact]
    public void TheBreedersSkillCapsTheStrain()
    {
        SetSkill(50); // cap tier 4
        var a = Bowl(PlantType.Garlic, 4);
        var b = Bowl(PlantType.Ginseng, 4);

        try
        {
            Assert.Equal(4, HerbalLane.StrainCap(50));
            Assert.False(HerbalLane.OnPollinated(_pm, a, b, 0.0));
            Assert.Equal(4, HerbalLane.Record(b).SeedTier);
        }
        finally
        {
            a.Delete();
            b.Delete();
        }
    }

    [Fact]
    public void AStrainShowsItsMetalHueOnThePlantAndTheSeed()
    {
        var bowl = Bowl(PlantType.Garlic, 7);
        var seed = new ReagentSeed(PlantType.Garlic, 7);

        try
        {
            bowl.PlantStatus = PlantStatus.Stage8;
            bowl.PlantStatus = PlantStatus.Stage9;
            Assert.Equal(StrainHueOf(7), bowl.Hue);
            Assert.Equal(StrainHueOf(7), seed.Hue);
            Assert.Contains("garlic seed", seed.Words);
        }
        finally
        {
            bowl.Delete();
            seed.Delete();
        }
    }

    // ---------------------------------------------------------------- processing

    [Theory]
    [InlineData(0, 12, 2, 2, 10)]
    [InlineData(200, 10, 2, 0, 15)]
    [InlineData(100, 5, 1, 0, 12)]
    public void HerbsProcessIntoBundlesOfReagents(double skill, int herbs, int bundles, int left, int each)
    {
        SetSkill(skill);
        var raw = new RawHerb("Garlic", herbs);
        _pm.Backpack.DropItem(raw);

        Assert.Equal(bundles, raw.Process(_pm));
        Assert.Equal(left, RawInPack("Garlic"));

        var bundle = Assert.Single(InPack<ReagentBundle>());
        Assert.Equal(bundles, bundle.Amount);
        Assert.Equal(each, bundle.Reagents);

        bundle.OnDoubleClick(_pm);
        Assert.Equal(bundles * each, _pm.Backpack.GetAmount(typeof(Garlic)));
        Assert.True(bundle.Deleted);
    }

    [Fact]
    public void AHerbAboveYourSkillWillNotProcess()
    {
        var raw = new RawHerb("GraveMoss", 10);
        _pm.Backpack.DropItem(raw);
        Assert.Equal(0, raw.Process(_pm));
        Assert.Equal(10, raw.Amount);
    }

    [Fact]
    public void TooFewHerbsMakeNoBundle()
    {
        var raw = new RawHerb("Garlic", 4);
        _pm.Backpack.DropItem(raw);
        Assert.Equal(0, raw.Process(_pm));
        Assert.Equal(4, raw.Amount);
        Assert.Empty(InPack<ReagentBundle>());
    }

    [Fact]
    public void AStarterFarmMakesFiftyToAHundredReagentsADay()
    {
        var bowls = new[] { Bowl(PlantType.Garlic), Bowl(PlantType.Ginseng) };
        var mounds = Bed(PlantType.Garlic, out var addon);

        try
        {
            // Two bowls and a regular six-plot bed at no skill; a small bed has four plots.
            var s = HerbalismSettings.Current;
            var herbs = bowls.Sum(HerbalLane.DailyYield) + 6 * HerbalLane.DailyYield(mounds[0]);
            var regs = herbs / s.HerbsPerBundle * HerbalLane.ReagentsInBundle(0);
            _out.WriteLine($"{herbs} herbs, {regs} reagents a day");
            Assert.InRange(regs, 50, 100);
        }
        finally
        {
            addon.Delete();

            foreach (var p in bowls.Cast<PlantItem>().Concat(mounds))
            {
                p.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- neglect

    [Theory]
    [InlineData(0, 2)]
    [InlineData(200, 7)]
    [InlineData(100, 4.5)]
    public void GraceIsTwoDaysPlusUpToFiveAt200(double skill, double days) => Assert.Equal(days, HerbalLane.GraceDays(skill));

    [Fact]
    public void InsideGraceNothingIsLost()
    {
        var bowl = Bowl(PlantType.Garlic, 5);

        try
        {
            var hits = bowl.PlantSystem.Hits;
            Idle(bowl, 1.9);
            bowl.PlantSystem.Water = 0; // bone dry, which OSI would punish
            bowl.PlantSystem.NextGrowth = Core.Now.AddMinutes(-1);
            bowl.PlantSystem.DoGrowthCheck();
            Assert.True(bowl.PlantSystem.Hits >= hits);
            Assert.Equal(5, HerbalLane.Record(bowl).Tier);
        }
        finally
        {
            bowl.Delete();
        }
    }

    [Fact]
    public void PastGraceHealthFallsFirstThenAWindowLaterATier()
    {
        var bowl = Bowl(PlantType.Garlic, 5);

        try
        {
            var hits = bowl.PlantSystem.Hits;

            // Day 3: one day past a two-day grace. Health, no tier.
            Idle(bowl, 3);
            HerbalLane.OnGrowthCheck(bowl.PlantSystem);
            Assert.Equal(hits - HerbalismSettings.Current.NeglectHitsPerCheck, bowl.PlantSystem.Hits);
            Assert.Equal(5, HerbalLane.Record(bowl).Tier);

            // Day 4: a full window past grace. One tier, once.
            Idle(bowl, 4);
            HerbalLane.OnGrowthCheck(bowl.PlantSystem);
            Assert.Equal(4, HerbalLane.Record(bowl).Tier);
            HerbalLane.OnGrowthCheck(bowl.PlantSystem);
            Assert.Equal(4, HerbalLane.Record(bowl).Tier);

            // Day 6: the next window.
            Idle(bowl, 6);
            HerbalLane.OnGrowthCheck(bowl.PlantSystem);
            Assert.Equal(3, HerbalLane.Record(bowl).Tier);

            // Tended, the clock and the windows start over.
            HerbalLane.Tended(_pm, bowl);
            Assert.Equal(0, HerbalLane.Record(bowl).TierDrops);
            Idle(bowl, 1);
            HerbalLane.OnGrowthCheck(bowl.PlantSystem);
            Assert.Equal(3, HerbalLane.Record(bowl).Tier);
        }
        finally
        {
            bowl.Delete();
        }
    }

    [Fact]
    public void NeglectNeverKillsAPlant()
    {
        var bowl = Bowl(PlantType.Garlic);

        try
        {
            Idle(bowl, 500);

            for (var i = 0; i < 40; i++)
            {
                bowl.PlantSystem.NextGrowth = Core.Now.AddMinutes(-1);
                bowl.PlantSystem.DoGrowthCheck();
            }

            Assert.True(bowl.PlantSystem.Hits >= 1);
            Assert.Equal(PlantStatus.Stage9, bowl.PlantStatus);
            Assert.Equal(0, HerbalLane.Record(bowl).Tier);
        }
        finally
        {
            bowl.Delete();
        }
    }

    // ---------------------------------------------------------------- gates

    [Theory]
    [InlineData(PlantType.Garlic, 0, 0, true)]
    [InlineData(PlantType.Mandrake, 0, 0, false)]
    [InlineData(PlantType.Mandrake, 0, 1, true)]
    [InlineData(PlantType.Garlic, 3, 0, false)]
    [InlineData(PlantType.Garlic, 3, 1, true)]
    [InlineData(PlantType.Garlic, 6, 1, false)]
    public void TypesAndStrainsAskForAnAlembicRank(PlantType type, int tier, int rank, bool expected)
    {
        Assert.Equal(expected, HerbalLane.RankNeeded(type, tier) <= rank);
    }

    [Fact]
    public void AnUnrankedCharacterCannotPlantARankedSeed()
    {
        var seed = new ReagentSeed(PlantType.Mandrake, 0);
        var starter = new ReagentSeed(PlantType.Garlic, 0);
        var stock = new Seed(PlantType.Poppies, PlantHue.Plain);

        try
        {
            Assert.False(HerbalLane.MayPlant(_pm, seed));
            Assert.True(HerbalLane.MayPlant(_pm, starter));
            Assert.True(HerbalLane.MayPlant(_pm, stock));
        }
        finally
        {
            seed.Delete();
            starter.Delete();
            stock.Delete();
        }
    }

    [Fact]
    public void TheAlembicCountsTheHigherOfItsTwoSkills()
    {
        var def = ClusterFGuildSystem.GetDef(ClusterFAlembicGuild.Key);
        _pm.Skills[SkillName.Alchemy].Base = 10;
        SetSkill(40);
        Assert.Equal(40, ClusterFGuildSystem.TaskSkillValue(_pm, def));
        _pm.Skills[SkillName.Alchemy].Base = 60;
        Assert.Equal(60, ClusterFGuildSystem.TaskSkillValue(_pm, def));
    }

    [Fact]
    public void TheStarterIsGivenOnce()
    {
        Assert.False(HerbalismSystem.HadStarter(_pm));
        Assert.True(HerbalismSystem.GiveStarter(_pm, out _));
        Assert.True(HerbalismSystem.HadStarter(_pm));
        Assert.Single(InPack<PlantBowl>());
        Assert.Equal(2, InPack<ReagentSeed>().Count);
        Assert.False(HerbalismSystem.GiveStarter(_pm, out _));
        Assert.Single(InPack<PlantBowl>());
    }

    // ---------------------------------------------------------------- saves

    [Fact]
    public void EveryNewItemSurvivesASave()
    {
        var seed = new ReagentSeed(PlantType.Nightshade, 9);
        var raw = new RawHerb("BloodMoss", 7);
        var bundle = new ReagentBundle("Ginseng", 13);
        var patch = new HerbPatch("Mandrake", "Trammel:1:2") { RipeAt = new DateTime(2026, 11, 5, 1, 2, 3, DateTimeKind.Utc) };
        var table = new AlembicHerbTable();

        try
        {
            var seed2 = RoundTrip(seed);
            Assert.Equal(9, seed2.StrainTier);
            Assert.Equal(PlantType.Nightshade, seed2.PlantType);
            Assert.Equal("BloodMoss", RoundTrip(raw).HerbKey);
            var bundle2 = RoundTrip(bundle);
            Assert.Equal("Ginseng", bundle2.HerbKey);
            Assert.Equal(13, bundle2.Reagents);
            var patch2 = RoundTrip(patch);
            Assert.Equal("Mandrake", patch2.HerbKey);
            Assert.Equal("Trammel:1:2", patch2.SpotKey);
            Assert.Equal(patch.RipeAt, patch2.RipeAt);
            HerbPatch.Patches.Remove(patch2);
            Assert.NotNull(RoundTrip(table));
        }
        finally
        {
            seed.Delete();
            raw.Delete();
            bundle.Delete();
            patch.Delete();
            table.Delete();
        }
    }

    [Fact]
    public void TheLaneRecordsSurviveASaveAndTheDeadArePruned()
    {
        var bowl = Bowl(PlantType.Garlic, 6);
        var gone = Bowl(PlantType.Ginseng, 2);
        var record = HerbalLane.Record(bowl);
        record.SeedTier = 7;
        record.TenderSkill = 123.5;
        record.TierDrops = 2;
        var when = record.LastTended;
        gone.Delete();

        try
        {
            var store = new HerbalStore();
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            store.Serialize(writer);
            writer.Flush();
            Assert.False(HerbalStore.Rows.ContainsKey(gone.Serial));

            HerbalStore.Rows.Clear();
            store.Deserialize(new BufferReader(buffer));

            var back = HerbalStore.Rows[bowl.Serial];
            Assert.Equal(6, back.Tier);
            Assert.Equal(7, back.SeedTier);
            Assert.Equal(123.5, back.TenderSkill);
            Assert.Equal(2, back.TierDrops);
            Assert.Equal(when, back.LastTended);
        }
        finally
        {
            bowl.Delete();
        }
    }

    [Fact]
    public void AnUnknownStoreVersionFailsLoudly()
    {
        var buffer = new byte[16];
        var writer = new BufferWriter(buffer, true);
        writer.WriteEncodedInt(HerbalStore.CurrentVersion + 1);
        writer.Flush();
        Assert.Throws<InvalidDataException>(() => new HerbalStore().Deserialize(new BufferReader(buffer)));
    }

    // ---------------------------------------------------------------- the file

    [Fact]
    public void TheFileRoundTripsAndABadOneIsRefusedWhole()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"p804{Guid.NewGuid():N}");
        var path = Path.Combine(dir, HerbalismSettings.FileName);

        try
        {
            var lines = HerbalismSettings.LoadOrWriteDefaults(path);
            Assert.Contains(lines, l => l.Contains("wrote the built-in defaults"));

            var errors = new System.Collections.Generic.List<string>();
            var read = HerbalismSettings.Parse(File.ReadAllText(path), errors);
            _out.WriteLine(string.Join(" | ", errors));
            Assert.True(read != null, string.Join(" | ", errors));
            Assert.Equal(HerbalismSettings.ToJson(HerbalismSettings.Current), HerbalismSettings.ToJson(read));

            File.WriteAllText(path, File.ReadAllText(path).Replace("\"BowlHerbsPerDay\": 6", "\"BowlHerbsPerDay\": 9"));
            HerbalismSettings.Reload(path);
            Assert.Equal(9, HerbalismSettings.Current.BowlHerbsPerDay);

            var inForce = HerbalismSettings.Current;
            var good = File.ReadAllText(path);

            foreach (var bad in new[]
                     {
                         "not json",
                         good.Replace("\"BowlHerbsPerDay\": 9", "\"BowlHerbsPerDay\": -1"),
                         good.Replace("\"MaxStoredDays\": 7", "\"MaxStoredDays\": 0"),
                         good.Replace("\"Reagent\": \"Garlic\"", "\"Reagent\": \"NotAnItem\""),
                         good.Replace("\"Herb\": \"Mandrake\"", "\"Herb\": \"Exotic\""),
                         good.Replace("\"Map\": \"Trammel\"", "\"Map\": \"Nowhere\""),
                     })
            {
                File.WriteAllText(path, bad);
                var report = HerbalismSettings.Reload(path);
                _out.WriteLine(string.Join(" | ", report).Length > 200 ? report[0] + " " + report[1] : string.Join(" | ", report));
                Assert.Contains("REFUSED", report[0]);
                Assert.Same(inForce, HerbalismSettings.Current);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

internal static class HerbalismTestExtensions
{
    /// <summary>The plant system with a reagent seed type set, for NewSeed.</summary>
    public static PlantSystem Also(this PlantSystem system, PlantType type)
    {
        system.SeedType = type;
        return system;
    }
}
