// cc-P53 Parts A and B (D64, Chase 2026-10-04): the per-skill cap is 200, and every number the post-Valorite metals
// were tuned to on the old 300 cap is rescaled to it.
//
// Part A: ClusterFSkillCaps defaults to 200, and a config file still holding the old default 300 is rewritten once; a
// hand-set value is not. A character already above 200 keeps its skill (pinned's Cap setter never touches Base,
// Skills.cs:205-219) and cannot gain past it.
// Part B: the eight Blacksmithy requirements are Chase's even steps, Platinum 112.5 .. Celestial 200 (ClusterFMetalTiers);
// a 200 smith can work Celestial and a 187.4 smith cannot, through pinned's own resource check (CraftItem.ConsumeRes,
// `Skills[MainSkill].Base < RequiredSkill`); and each table that read the old tiers reads the new ones: commission
// offers and ranges, BOD ranges, salvage difficulty, armored meditation.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh. Def*.Initialize() and ServerStarted do not run in the host, so the facts run them in the
// server's order, as CleanUpBritanniaPointsVerification does.

using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TwoHundredCapVerification
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    private readonly ITestOutputHelper _out;

    public TwoHundredCapVerification(ITestOutputHelper output) => _out = output;

    private static readonly (CraftResource Res, Type Ingot, double Req)[] Metals =
    {
        (CraftResource.Platinum, typeof(PlatinumIngot), 112.5),
        (CraftResource.Toxic, typeof(ToxicIngot), 125.0),
        (CraftResource.Blaze, typeof(BlazeIngot), 137.5),
        (CraftResource.Frost, typeof(FrostIngot), 150.0),
        (CraftResource.Obsidian, typeof(ObsidianIngot), 162.5),
        (CraftResource.Mythril, typeof(MythrilIngot), 175.0),
        (CraftResource.Adamantium, typeof(AdamantiumIngot), 187.5),
        (CraftResource.Celestial, typeof(CelestialIngot), 200.0)
    };

    internal static CraftSystem Blacksmithy()
    {
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register();

        var smith = DefBlacksmithy.CraftSystem;
        if (smith.CraftSubRes.SearchFor(typeof(PlatinumIngot)) == null)
        {
            typeof(ClusterFMiningExtension).GetMethod("OnServerStarted", Private)!.Invoke(null, null);
        }

        return smith;
    }

    private static PlayerMobile Player()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
        return pm;
    }

    // ---------------------------------------------------------------- A1

    [Fact]
    public void TheDefaultCapIs200AndAStoredOldDefaultIsRewrittenOnce()
    {
        const string key = ClusterFSkillCaps.IndividualCapKey;
        const string marker = ClusterFSkillCaps.RescaledMarkerKey;
        var keptValue = ServerConfiguration.GetSetting(key, ClusterFSkillCaps.DefaultIndividualSkillCap);
        var keptMarker = ServerConfiguration.GetSetting(marker, false);

        try
        {
            Assert.Equal(200.0, ClusterFSkillCaps.DefaultIndividualSkillCap);

            // Both shards' config files hold "300", written by the old default.
            ServerConfiguration.SetSetting(key, "300");
            ServerConfiguration.SetSetting(marker, false);
            Assert.True(ClusterFSkillCaps.RescaleStoredLegacyCap());
            Assert.Equal(200.0, ServerConfiguration.GetSetting(key, -1.0));
            Assert.True(ServerConfiguration.GetSetting(marker, false));

            // Once only: setting 300 again by hand afterwards sticks.
            ServerConfiguration.SetSetting(key, "300");
            Assert.False(ClusterFSkillCaps.RescaleStoredLegacyCap());
            Assert.Equal(300.0, ServerConfiguration.GetSetting(key, -1.0));

            // A value that was not the old default is left alone even on the first run.
            ServerConfiguration.SetSetting(key, "250");
            ServerConfiguration.SetSetting(marker, false);
            Assert.False(ClusterFSkillCaps.RescaleStoredLegacyCap());
            Assert.Equal(250.0, ServerConfiguration.GetSetting(key, -1.0));

            // Applied: every skill's cap 200, the total 58 x 200 (the rule is unchanged: skills x individual).
            ServerConfiguration.SetSetting(key, "200");
            ClusterFSkillCaps.ReloadForTests();
            var pm = Player();
            ClusterFSkillCaps.ApplyTo(pm);
            _out.WriteLine($"cap {pm.Skills.Blacksmith.Cap}, total {pm.Skills.Cap / 10.0} for {SkillInfo.Table.Length} skills");
            for (var i = 0; i < pm.Skills.Length; i++)
            {
                Assert.Equal(200.0, pm.Skills[i].Cap);
            }

            Assert.Equal(SkillInfo.Table.Length * 2000, pm.Skills.Cap);
            Assert.Equal(11600.0, ClusterFSkillCaps.TotalSkillCap);
            pm.Delete();
        }
        finally
        {
            ServerConfiguration.SetSetting(key, keptValue);
            ServerConfiguration.SetSetting(marker, keptMarker);
            ClusterFSkillCaps.ReloadForTests();
        }
    }

    // ---------------------------------------------------------------- A2

    [Fact]
    public void ACharacterAbove200KeepsItsSkillAndDoesNotGainPastIt()
    {
        var pm = Player();

        try
        {
            pm.Skills.Cap = 58 * 3000;
            pm.Skills.Blacksmith.Cap = 300.0;
            pm.Skills.Blacksmith.Base = 300.0;

            pm.Skills.Blacksmith.Cap = 200.0; // what ClusterFSkillCaps.Apply now writes at login
            Assert.Equal(300.0, pm.Skills.Blacksmith.Base);
            Assert.Equal(300.0, pm.Skills.Blacksmith.Value);

            SkillCheck.Gain(pm, pm.Skills.Blacksmith);
            Assert.Equal(300.0, pm.Skills.Blacksmith.Base);
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- B1

    [Fact]
    public void EachMetalsRequiredSkillIsTheApprovedStep()
    {
        var smith = Blacksmithy();

        foreach (var (res, ingot, req) in Metals)
        {
            var sub = smith.CraftSubRes.SearchFor(ingot);
            Assert.NotNull(sub);
            _out.WriteLine($"{res}: {sub.RequiredSkill}");
            Assert.Equal(req, sub.RequiredSkill);
            Assert.Equal(req, ClusterFMetalTiers.RequiredSkill(res));
        }
    }

    // ---------------------------------------------------------------- B2

    [Theory]
    [InlineData(200.0, true)]
    [InlineData(187.4, false)]
    public void ASmithAt200CanWorkCelestialAndOneAt187Point4CannotWithAdamantiumBelow(double skill, bool celestial)
    {
        var smith = Blacksmithy();
        var dagger = smith.CraftItems.SearchFor(typeof(Dagger));
        Assert.NotNull(dagger);

        var pm = Player();

        try
        {
            pm.Skills.Blacksmith.Cap = 200.0;
            pm.Skills.Blacksmith.Base = skill;
            pm.Backpack.DropItem(new CelestialIngot(20));
            pm.Backpack.DropItem(new AdamantiumIngot(20));

            var hue = 0;
            var max = 0;
            TextDefinition message = null;
            var ok = dagger.ConsumeRes(pm, typeof(CelestialIngot), smith, ref hue, ref max, ConsumeType.None, ref message);
            _out.WriteLine($"Blacksmithy {skill}: Celestial {ok} ({message?.Number})");
            Assert.Equal(celestial, ok);
            if (!celestial)
            {
                Assert.Equal(1044036, message.Number); // You cannot use that material without the proper skill.
            }

            message = null;
            Assert.True(dagger.ConsumeRes(pm, typeof(AdamantiumIngot), smith, ref hue, ref max, ConsumeType.None, ref message));
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- B3

    [Fact]
    public void EveryRescaledTableReadsTheNewTiers()
    {
        // Commissions: requirement-10 to requirement+20 (was Platinum 95-125 .. Celestial 290-325).
        foreach (var (res, _, req) in Metals)
        {
            Assert.Equal((req - 10.0, req + 20.0), SmithCommissionSystem.GetSkillRange(res, false));
            Assert.Equal((req, req + 30.0), SmithCommissionSystem.GetSkillRange(res, true));
        }

        // BOD completion: requirement-20 to requirement+20 (was Platinum 85-115 .. Celestial 280-340).
        var bodRange = typeof(BlacksmithGuildmaster).GetMethod("GetSkillRange", Private)!;
        foreach (var mat in new[]
                 {
                     BulkMaterialType.Platinum, BulkMaterialType.Toxic, BulkMaterialType.Blaze, BulkMaterialType.Frost,
                     BulkMaterialType.Obsidian, BulkMaterialType.Mythril, BulkMaterialType.Adamantium,
                     BulkMaterialType.Celestial
                 })
        {
            var req = ClusterFMetalTiers.PostValoriteRequiredSkill(mat);
            var deed = new SmallSmithBOD(0, 10, typeof(Dagger), 1023921, 0xF52, false, mat);
            var range = ((double, double))bodRange.Invoke(null, new object[] { deed })!;
            deed.Delete();
            _out.WriteLine($"BOD {mat}: {range}");
            Assert.Equal((req - 20.0, req + 20.0), range);
            Assert.True(range.Item1 < 200.0, "a BOD whose range starts above the cap can never gain");
        }

        // Salvage bag: Mining difficulty is the metal's requirement (was 105 .. 300).
        var resmelt = typeof(SmithGuildSalvageBag).GetMethod("ResmeltDifficulty", Private)!;
        foreach (var (res, _, req) in Metals)
        {
            Assert.Equal(req, (double)resmelt.Invoke(null, new object[] { res })!);
        }

        Assert.Equal(99.0, (double)resmelt.Invoke(null, new object[] { CraftResource.Valorite })!); // stock, unchanged

        // Armored meditation: 150 on the 200 cap (was 200 on the 300 cap, unreachable at 200).
        var meditation = typeof(BlacksmithGuildmaster).Assembly.GetType("Server.SkillHandlers.Meditation")!;
        Assert.Equal(150.0, (double)meditation.GetField("ArmoredMeditationSkill")!.GetRawConstantValue()!);
    }

    // ---------------------------------------------------------------- B4

    [Fact]
    public void CommissionsOfferOnlyMetalsTheSmithCanWorkAndCelestialAt200()
    {
        var pick = typeof(SmithCommissionSystem).GetMethod("GetMaterialForSkill", Private)!;

        foreach (var skill in new[] { 105.0, 112.4, 112.5, 130.0, 150.0, 170.0, 187.4, 187.5, 199.9, 200.0 })
        {
            var seen = new HashSet<CraftResource>();
            for (var i = 0; i < 400; i++)
            {
                seen.Add((CraftResource)pick.Invoke(null, new object[] { skill, false })!);
            }

            _out.WriteLine($"{skill}: {string.Join(", ", seen)}");
            foreach (var res in seen)
            {
                Assert.True(ClusterFMetalTiers.RequiredSkill(res) <= skill, $"{res} offered at {skill}");
            }

            if (skill >= 200.0)
            {
                Assert.Contains(CraftResource.Celestial, seen);
            }
        }
    }
}
