// PostValoriteHarvestVerification.cs
//
// cc-P57 Part C (Chase 2026-10-05: "just like OSI's metals"). Stock lines up the Mining to dig a colored ore and to smelt
// it: pinned Engines/Harvest/Mining.cs:115-187 gives Dull Copper .. Valorite reqSkill 65 .. 99 with a window of
// reqSkill - 40 to reqSkill + 40, and Items/Resources/Blacksmithing/Ore.cs:168-179 smelts them at the same 65 .. 99.
// Post-Valorite ores now follow ClusterFMetalTiers the same way (Platinum 112.5 .. Celestial 200, the smelt difficulty
// since cc-P56 Part B); they all needed 100 before. Notes: shard-migration notes/cc-P57-batch-6.md, Part C.
//
// Facts:
//   C1. Each post-Valorite ore: required = its tier, window tier - 40 to tier + 40.
//   C2. A miner a tenth under the tier never digs that ore: the vein gives its fallback, iron, at every roll. At the tier,
//       on a roll that does not fall back, the ore itself.
//   C3. Stock ores unchanged: iron and Dull Copper .. Valorite keep pinned's numbers, and the same under/at behaviour.
//   C4. Vein weights unchanged (1036 in all, Platinum 8 .. Celestial 1), each falling back to iron.

using System;
using System.Linq;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PostValoriteHarvestVerification
{
    private readonly ITestOutputHelper _out;

    public PostValoriteHarvestVerification(ITestOutputHelper output) => _out = output;

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

    private static (Mining System, HarvestDefinition Def) Fresh()
    {
        var mining = (Mining)Activator.CreateInstance(typeof(Mining), true)!;
        ClusterFMiningExtension.ApplyExtendedVeins(mining.OreAndStone);
        return (mining, mining.OreAndStone);
    }

    private static readonly (Type Ore, double Tier)[] PostValorite =
    [
        (typeof(PlatinumOre), 112.5), (typeof(ToxicOre), 125.0), (typeof(BlazeOre), 137.5), (typeof(FrostOre), 150.0),
        (typeof(ObsidianOre), 162.5), (typeof(MythrilOre), 175.0), (typeof(AdamantiumOre), 187.5),
        (typeof(CelestialOre), 200.0)
    ];

    // Pinned Mining.cs:115-187: (ore, reqSkill, minSkill, maxSkill).
    private static readonly (Type Ore, double Req, double Min, double Max)[] Stock =
    [
        (typeof(IronOre), 0.0, 0.0, 100.0), (typeof(DullCopperOre), 65.0, 25.0, 105.0),
        (typeof(ShadowIronOre), 70.0, 30.0, 110.0), (typeof(CopperOre), 75.0, 35.0, 115.0),
        (typeof(BronzeOre), 80.0, 40.0, 120.0), (typeof(GoldOre), 85.0, 45.0, 125.0),
        (typeof(AgapiteOre), 90.0, 50.0, 130.0), (typeof(VeriteOre), 95.0, 55.0, 135.0),
        (typeof(ValoriteOre), 99.0, 59.0, 139.0)
    ];

    private static HarvestVein VeinOf(HarvestDefinition def, Type ore) => def.Veins.Single(v => v.PrimaryResource.Types[0] == ore);

    // What the vein gives a miner at this Mining, on this roll (HarvestSystem.MutateResource), and whether the harvest's
    // own gate (HarvestSystem.cs:147, Base >= ReqSkill) lets them try it.
    private static (Type Got, bool MayTry) Dig(Mining system, HarvestDefinition def, HarvestVein vein, double mining, double roll)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.Skills.Mining.Cap = 200.0;
        pm.Skills.Mining.Base = mining;
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            var res = system.MutateResource(pm, null, def, Map.Trammel, Point3D.Zero, vein, vein.PrimaryResource,
                vein.FallbackResource);
            return (res.Types[0], pm.Skills.Mining.Base >= res.ReqSkill);
        }
        finally
        {
            BuiltInRng.Reset();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- C1

    [Fact]
    public void EachPostValoriteOreNeedsItsTierWithStocksWindow()
    {
        var (_, def) = Fresh();
        foreach (var (ore, tier) in PostValorite)
        {
            var r = VeinOf(def, ore).PrimaryResource;
            _out.WriteLine($"{ore.Name}: required {r.ReqSkill}, window {r.MinSkill} to {r.MaxSkill}");
            Assert.Equal((tier, tier - 40.0, tier + 40.0), (r.ReqSkill, r.MinSkill, r.MaxSkill));
        }
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public void ATenthUnderTheTierDigsIronAndAtTheTierTheOre()
    {
        var (system, def) = Fresh();
        foreach (var (ore, tier) in PostValorite)
        {
            var vein = VeinOf(def, ore);
            foreach (var roll in new[] { 0.0, 0.5, 0.99 })
            {
                var under = Dig(system, def, vein, tier - 0.1, roll);
                Assert.Equal(typeof(IronOre), under.Got);
                Assert.True(under.MayTry); // iron is diggable at any skill
            }

            var at = Dig(system, def, vein, tier, 0.99);
            _out.WriteLine($"{ore.Name}: at {tier - 0.1} iron; at {tier} {at.Got.Name}");
            Assert.Equal(ore, at.Got);
            Assert.True(at.MayTry);
        }
    }

    // ---------------------------------------------------------------- C3

    [Fact]
    public void StockOresAreUnchanged()
    {
        var (system, def) = Fresh();
        for (var i = 0; i < Stock.Length; i++)
        {
            var (ore, req, min, max) = Stock[i];
            var r = def.Resources[i];
            Assert.Equal(ore, r.Types[0]);
            Assert.Equal((req, min, max), (r.ReqSkill, r.MinSkill, r.MaxSkill));

            if (req > 0)
            {
                var vein = VeinOf(def, ore);
                Assert.Equal(typeof(IronOre), Dig(system, def, vein, req - 0.1, 0.99).Got);
                Assert.Equal(ore, Dig(system, def, vein, req, 0.99).Got);
            }
        }
    }

    // ---------------------------------------------------------------- C4

    [Fact]
    public void VeinWeightsAreUnchanged()
    {
        var (_, def) = Fresh();
        Assert.Equal(1036u, def.VeinWeights);
        var weights = PostValorite.Select(p => VeinOf(def, p.Ore).VeinChance).ToArray();
        Assert.Equal(new uint[] { 8, 7, 6, 5, 4, 3, 2, 1 }, weights);
        Assert.All(PostValorite, p => Assert.Equal(typeof(IronOre), VeinOf(def, p.Ore).FallbackResource.Types[0]));
    }
}
