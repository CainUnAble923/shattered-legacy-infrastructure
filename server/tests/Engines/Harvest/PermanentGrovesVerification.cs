// PermanentGrovesVerification.cs
//
// cc-P37 (Chase, 2026-10-02): groves are permanent. ClusterFLumberjackingExtension.ApplyExtendedVeins appends the
// eight extended woods and then sets RandomizeVeins = false, as ClusterFMiningExtension.cs:131 does for ore, so a
// tree bank's wood is a stable function of x, y and map (pinned HarvestDefinition.cs:141-143) instead of a roll on
// first touch and on every respawn (HarvestDefinition.cs:136-139, HarvestBank.cs:60-63). The logging book's
// groves (P13) depend on a recorded spot keeping its wood. Notes in shard-migration notes/cc-P37-upgrade-prep.md.
//
// Facts:
//   1. Stock pinned randomizes lumber on this ML-era shard; after ApplyExtendedVeins it does not, and VeinWeights
//      covers the combined list (1000 stock + 36 extended), as the Veins setter computes (HarvestDefinition.cs:66-77).
//   2. The same x, y and map give the same wood across two fresh definitions, and again after the bank cache is
//      cleared, over a grid on six facets; and the grid meets the extended woods, so the fact is not vacuous.
//   3. Every wood occurs over the real bank cells of six facets at about its weight, Starwood included, and each
//      vein's exact share of GetVeinFrom's values IS its weight. cc-P37 measured Starwood at 0 of 1,036: pinned's
//      GetVeinFrom tested `randomValue <= VeinChance` (HarvestDefinition.cs:155, the same line at upstream d4531cd9),
//      so the first vein got VeinChance + 1 values and the last VeinChance - 1, and Starwood, last with weight 1, got
//      none. cc-P38 (D49, Chase 2026-10-02) patches it to `<` (server/patches/HarvestDefinition-vein-boundary.patch).
//   4. The same for mining: every ore, Celestial included (last, weight 1, ClusterFMiningExtension), occurs over the
//      real 8x8 bank cells of six facets at about its weight. cc-P37 deduced Celestial had the same defect; this
//      measures it.


using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PermanentGrovesVerification
{
    private readonly ITestOutputHelper _out;

    public PermanentGrovesVerification(ITestOutputHelper output) => _out = output;

    // Facet sizes in tiles. A lumber bank is 4x3 tiles (pinned Lumberjacking.cs:43-44).
    private static (Map Map, int Width, int Height)[] Facets =>
    [
        (Map.Felucca, 7168, 4096),
        (Map.Trammel, 7168, 4096),
        (Map.Ilshenar, 2304, 1600),
        (Map.Malas, 2560, 2048),
        (Map.Tokuno, 1448, 1448),
        (Map.TerMur, 1280, 4096)
    ];

    private static readonly Type[] ExtendedLogs =
    [
        typeof(IronwoodLog), typeof(GhostwoodLog), typeof(EmberbarkLog), typeof(FrostbarkLog),
        typeof(ShadowbarkLog), typeof(RunewoodLog), typeof(VoidwoodLog), typeof(StarwoodLog)
    ];

    private static HarvestDefinition FreshStockDefinition() =>
        ((Lumberjacking)Activator.CreateInstance(typeof(Lumberjacking), true)!).Definitions[0];

    private static HarvestDefinition FreshDefinition()
    {
        var def = FreshStockDefinition();
        Assert.Equal(8, ClusterFLumberjackingExtension.ApplyExtendedVeins(def));
        return def;
    }

    private static Type Wood(HarvestVein vein) => vein.PrimaryResource.Types[0];

    // ---- 1 ----------------------------------------------------------------------------------------

    [Fact]
    public void ExtendedLumberIsNotRandomizedAndWeighsTheCombinedList()
    {
        var stock = FreshStockDefinition();
        Assert.True(Core.ML);
        Assert.True(stock.RandomizeVeins); // pinned Lumberjacking.cs:118, RandomizeVeins = Core.ML
        Assert.Equal(7, stock.Veins.Length);
        Assert.Equal(1000u, stock.VeinWeights);

        var def = FreshDefinition();
        Assert.False(def.RandomizeVeins);
        Assert.Equal(15, def.Veins.Length);
        Assert.Equal(1036u, def.VeinWeights);
        Assert.Equal((uint)def.Veins.Sum(v => (long)v.VeinChance), def.VeinWeights);
        Assert.Equal(ExtendedLogs, def.Veins.Skip(7).Select(Wood).ToArray());
    }

    // ---- 2 ----------------------------------------------------------------------------------------

    [Fact]
    public void TheSameSpotKeepsItsWoodAcrossDefinitionsAndAfterTheBankCacheIsCleared()
    {
        var a = FreshDefinition();
        var b = FreshDefinition();

        var spots = 0;
        var mismatches = new List<string>();
        var seen = new HashSet<Type>();

        foreach (var (map, width, height) in Facets)
        {
            for (var x = 3; x < width; x += 53)
            {
                for (var y = 5; y < height; y += 47)
                {
                    spots++;
                    var first = Wood(a.GetBank(map, x, y).Vein);
                    seen.Add(first);

                    var other = Wood(b.GetBank(map, x, y).Vein);
                    if (first != other && mismatches.Count < 10)
                    {
                        mismatches.Add($"{map} {x},{y}: {first.Name} vs fresh definition {other.Name}");
                    }
                }
            }
        }

        a.Banks.Clear();

        foreach (var (map, width, height) in Facets)
        {
            for (var x = 3; x < width; x += 53)
            {
                for (var y = 5; y < height; y += 47)
                {
                    var before = Wood(b.GetBank(map, x, y).Vein);
                    var after = Wood(a.GetBank(map, x, y).Vein);
                    if (before != after && mismatches.Count < 20)
                    {
                        mismatches.Add($"{map} {x},{y}: {before.Name} vs after clearing banks {after.Name}");
                    }
                }
            }
        }

        _out.WriteLine($"{spots} spots, {seen.Count} woods seen: {string.Join(", ", seen.Select(t => t.Name).OrderBy(n => n))}");
        mismatches.ForEach(_out.WriteLine);

        Assert.Empty(mismatches);
        Assert.Contains(typeof(IronwoodLog), seen);
        Assert.True(seen.Count(ExtendedLogs.Contains) >= 4, "the grid should meet several extended woods");
    }

    // ---- 3 ----------------------------------------------------------------------------------------

    [Fact]
    public void EveryWoodOccursAtItsWeightStarwoodIncluded()
    {
        var def = FreshDefinition();
        var counts = CountBanks(def, 4, 3); // a lumber bank is 4x3 tiles (pinned Lumberjacking.cs:43-44)

        Assert.Equal(typeof(StarwoodLog), Wood(def.Veins[^1]));
        AssertEveryVeinAtItsWeight(def, counts);
        Assert.All(ExtendedLogs, t => Assert.True(counts[t] > 0, t.Name));
    }

    // ---- 4 ----------------------------------------------------------------------------------------

    [Fact]
    public void EveryOreOccursAtItsWeightCelestialIncluded()
    {
        var stock = ((Mining)Activator.CreateInstance(typeof(Mining), true)!).OreAndStone;
        Assert.Equal(1000u, stock.VeinWeights);
        Assert.Equal(8, ClusterFMiningExtension.ApplyExtendedVeins(stock));
        Assert.False(stock.RandomizeVeins);
        Assert.Equal(1036u, stock.VeinWeights);

        var counts = CountBanks(stock, stock.BankWidth, stock.BankHeight);

        Assert.Equal(typeof(CelestialOre), Wood(stock.Veins[^1]));
        AssertEveryVeinAtItsWeight(stock, counts);
        Assert.True(counts[typeof(CelestialOre)] > 0, "CelestialOre never occurs");
    }

    private static Dictionary<Type, int> CountBanks(HarvestDefinition def, int bankWidth, int bankHeight)
    {
        var counts = def.Veins.ToDictionary(Wood, _ => 0);

        foreach (var (map, width, height) in Facets)
        {
            for (var bx = 0; bx < width / bankWidth; bx++)
            {
                for (var by = 0; by < height / bankHeight; by++)
                {
                    counts[Wood(def.GetVeinAt(map, bx, by))]++;
                }
            }
        }

        return counts;
    }

    private void AssertEveryVeinAtItsWeight(HarvestDefinition def, Dictionary<Type, int> counts)
    {
        var banks = counts.Values.Sum();

        // The exact share each vein receives from GetVeinFrom over every value StableRandom can return
        // (StableRandom.First returns 0..VeinWeights-1). With `<` it is the vein's weight, and nothing falls through.
        var exact = def.Veins.ToDictionary(Wood, _ => 0);
        for (var v = 0u; v < def.VeinWeights; v++)
        {
            var vein = def.GetVeinFrom(v);
            Assert.True(vein != null, $"GetVeinFrom({v}) returned no vein");
            exact[Wood(vein)]++;
        }

        var bad = new List<string>();
        foreach (var vein in def.Veins)
        {
            var type = Wood(vein);
            var expected = (double)banks * vein.VeinChance / def.VeinWeights;
            var got = counts[type];
            _out.WriteLine($"{type.Name,-14} weight {vein.VeinChance,4}  exact share {exact[type],4}/{def.VeinWeights}  banks {got,7}  expected {expected,9:F0}  ratio {(expected > 0 ? got / expected : 0):F2}");

            if (exact[type] != vein.VeinChance)
            {
                bad.Add($"{type.Name}: exact share {exact[type]}, weight {vein.VeinChance}");
            }

            if (got < expected * 0.5 || got > expected * 1.5)
            {
                bad.Add($"{type.Name}: {got} banks, expected about {expected:F0}");
            }
        }

        _out.WriteLine($"{banks} banks over six facets");
        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }
}
