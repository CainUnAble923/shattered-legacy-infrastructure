// ExtendedOreStoneMiningVerification.cs
//
// cc-P36 Part A (cc-P24 F1). Pinned Mining.GetResourceType gives a GM miner with stone mining toggled on resource.Types[1]
// on 10% of rolls (patches/Mining.cs:284-288, unchanged from pinned). Every stock colored ore has two types, ore and granite
// (pinned Mining.cs:115-187); our eight post-Valorite ores had one, so that roll threw IndexOutOfRangeException inside the
// harvest timer. The fix gives each its own granite, as stock, and Masonry takes it at the metal's tier. Dawnstone (Part D)
// has two types from the start. Notes: shard-migration notes/cc-P36-dungeon-build-1.md, Part A.
//
// Facts:
//   A1. Forced onto the stone branch (roll 0.0 < 0.1), each post-Valorite ore and Dawnstone yields its own granite: a
//       BaseGranite whose resource is that metal. Red before the fix: IndexOutOfRangeException for all eight.
//   A2. Off the branch (roll 0.5) each still yields its ore; a miner without stone mining never gets granite.
//   A3. Each new granite is registered to its metal (CraftResources.GetFromType) and is a Masonry material at the metal's
//       requirement, appended after stock's Valorite Granite.

using System;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ExtendedOreStoneMiningVerification
{
    private readonly ITestOutputHelper _out;

    public ExtendedOreStoneMiningVerification(ITestOutputHelper output) => _out = output;

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

    private static readonly (Type Ore, Type Granite, CraftResource Res, double Tier)[] Metals =
    [
        (typeof(PlatinumOre), typeof(PlatinumGranite), CraftResource.Platinum, 112.5),
        (typeof(ToxicOre), typeof(ToxicGranite), CraftResource.Toxic, 125.0),
        (typeof(BlazeOre), typeof(BlazeGranite), CraftResource.Blaze, 137.5),
        (typeof(FrostOre), typeof(FrostGranite), CraftResource.Frost, 150.0),
        (typeof(ObsidianOre), typeof(ObsidianGranite), CraftResource.Obsidian, 162.5),
        (typeof(MythrilOre), typeof(MythrilGranite), CraftResource.Mythril, 175.0),
        (typeof(AdamantiumOre), typeof(AdamantiumGranite), CraftResource.Adamantium, 187.5),
        (typeof(CelestialOre), typeof(CelestialGranite), CraftResource.Celestial, 200.0)
    ];

    private static (Mining System, HarvestDefinition Def) Fresh()
    {
        var mining = (Mining)Activator.CreateInstance(typeof(Mining), true)!;
        ClusterFMiningExtension.ApplyExtendedVeins(mining.OreAndStone);
        return (mining, mining.OreAndStone);
    }

    // Every resource the test cares about: the eight veins' primaries, and Dawnstone (the Burrows' own resource).
    private static (HarvestResource Res, Type Ore, Type Granite, CraftResource Metal)[] Resources(HarvestDefinition def) =>
        Metals.Select(m => (def.Veins.Single(v => v.PrimaryResource.Types[0] == m.Ore).PrimaryResource, m.Ore, m.Granite, m.Res))
            .Append((Mining.Dawnstone, typeof(DawnstoneOre), typeof(DawnstoneGranite), CraftResource.Dawnstone))
            .ToArray();

    private static PlayerMobile Miner(bool stone)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.Skills.Mining.Cap = 200.0;
        pm.Skills.Mining.Base = 100.0;
        pm.StoneMining = stone;
        pm.ToggleMiningStone = stone;
        return pm;
    }

    private static Type Roll(Mining system, HarvestDefinition def, PlayerMobile pm, HarvestResource res, double roll)
    {
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            return system.GetResourceType(pm, null, def, Map.Trammel, Point3D.Zero, res);
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    // ---------------------------------------------------------------- A1

    [Fact]
    public void TheStoneBranchGivesEachOresOwnGranite()
    {
        var (system, def) = Fresh();
        var pm = Miner(true);
        try
        {
            foreach (var (res, _, granite, metal) in Resources(def))
            {
                var got = Roll(system, def, pm, res, 0.0);
                _out.WriteLine($"{metal}: stone branch -> {got.Name}");
                Assert.Equal(granite, got);

                var item = (BaseGranite)Activator.CreateInstance(got)!;
                try
                {
                    Assert.Equal(metal, item.Resource);
                    Assert.Equal(CraftResources.GetHue(metal), item.Hue);
                }
                finally
                {
                    item.Delete();
                }
            }
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A2

    [Fact]
    public void OffTheBranchItIsStillOre()
    {
        var (system, def) = Fresh();
        var stone = Miner(true);
        var plain = Miner(false);
        try
        {
            foreach (var (res, ore, _, _) in Resources(def))
            {
                Assert.Equal(ore, Roll(system, def, stone, res, 0.5));
                Assert.Equal(ore, Roll(system, def, plain, res, 0.0));
            }
        }
        finally
        {
            stone.Delete();
            plain.Delete();
        }
    }

    // ---------------------------------------------------------------- A3

    [Fact]
    public void EachGraniteIsItsMetalAndAMasonryMaterialAtTheTier()
    {
        if (DefMasonry.CraftSystem == null)
        {
            DefMasonry.Initialize();
        }

        ClusterFMiningExtension.ExtendMasonrySubResources();
        var masonry = DefMasonry.CraftSystem;

        var expected = Metals.Select(m => (m.Granite, m.Res, m.Tier))
            .Append((typeof(DawnstoneGranite), CraftResource.Dawnstone, 45.0)).ToArray();

        var valoriteIndex = -1;
        for (var i = 0; i < masonry.CraftSubRes.Count; i++)
        {
            if (masonry.CraftSubRes.GetAt(i).ItemType == typeof(ValoriteGranite))
            {
                valoriteIndex = i;
            }
        }

        for (var i = 0; i < expected.Length; i++)
        {
            var (granite, metal, tier) = expected[i];
            Assert.Equal(metal, CraftResources.GetFromType(granite));

            var sub = masonry.CraftSubRes.GetAt(valoriteIndex + 1 + i);
            _out.WriteLine($"masonry[{valoriteIndex + 1 + i}] {sub.ItemType.Name} at {sub.RequiredSkill}");
            Assert.Equal(granite, sub.ItemType);
            Assert.Equal(tier, sub.RequiredSkill);
        }
    }
}
