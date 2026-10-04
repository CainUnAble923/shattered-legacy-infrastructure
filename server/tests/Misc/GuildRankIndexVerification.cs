// GuildRankIndexVerification.cs
//
// cc-P48 Part C: ClusterFGuildSystem.GuildRankIndex, 0 (Initiate) to 5 (top rank) for any guild, honouring each guild's
// own ladder and the Apprentice-task bump; and the Mining rank-name gap (GetRankName had no "mining" case, so the
// Directory showed the generic ladder for the Miners' Compact, cc-P47). Notes in shard-migration
// notes/cc-P48-league-batch-1.md.
//
// Facts:
//   1. Every ladder's thresholds (RankThresholds) are exactly where its rank name changes, so the index and the name
//      cannot drift apart; and every guild's index at standing 0 is 0.
//   2. One fact per custom ladder: Smithing (6 ranks), Outriders, Foresters, Mining (7), Artificers, Custodians (5),
//      each read at the bottom of every rung and one below it.
//   3. The Mining rank name is the Miners' Compact's ladder.
//   4. The Apprentice task lifts a character with no standing to index 1 and never lowers a higher one.

using System;
using System.Linq;
using Server;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildRankIndexVerification
{
    private readonly ITestOutputHelper _out;

    public GuildRankIndexVerification(ITestOutputHelper output)
    {
        _out = output;
        ClusterFGuildSystem.EnsureRegistered();
    }

    // Index at the bottom of each rung, and one standing below each rung's bottom.
    private void AssertLadder(string key, int[] thresholds, int[] indexes)
    {
        Assert.Equal(thresholds, ClusterFGuildSystem.RankThresholds(key));

        for (var i = 0; i < thresholds.Length; i++)
        {
            var at = ClusterFGuildSystem.GuildRankIndex(key, thresholds[i]);
            _out.WriteLine($"{key} {thresholds[i],7:N0} {ClusterFGuildSystem.GetRankName(key, thresholds[i]),-22} index {at}");
            Assert.Equal(indexes[i], at);

            if (i > 0)
            {
                Assert.Equal(indexes[i - 1], ClusterFGuildSystem.GuildRankIndex(key, thresholds[i] - 1));
            }
        }

        Assert.Equal(indexes[^1], ClusterFGuildSystem.GuildRankIndex(key, int.MaxValue));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EachLaddersThresholdsAreWhereItsRankNameChanges()
    {
        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            var thresholds = ClusterFGuildSystem.RankThresholds(def.Key);
            Assert.Equal(0, thresholds[0]);
            Assert.Equal(0, ClusterFGuildSystem.GuildRankIndex(def.Key, 0));

            for (var i = 1; i < thresholds.Length; i++)
            {
                var below = ClusterFGuildSystem.GetRankName(def.Key, thresholds[i] - 1);
                var at = ClusterFGuildSystem.GetRankName(def.Key, thresholds[i]);
                Assert.True(below != at, $"{def.Key}: the name does not change at {thresholds[i]}");
                Assert.Equal(ClusterFGuildSystem.GetRankName(def.Key, thresholds[i - 1]), below);
            }

            // And nothing changes above the last threshold.
            var top = ClusterFGuildSystem.GetRankName(def.Key, thresholds[^1]);
            Assert.Equal(top, ClusterFGuildSystem.GetRankName(def.Key, 10_000_000));
            _out.WriteLine($"{def.Key}: {thresholds.Length} ranks, top {top}");
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SmithingSixRanks() =>
        AssertLadder("smithing", [0, 1_000, 5_000, 15_000, 50_000, 100_000], [0, 1, 2, 3, 4, 5]);

    [Fact]
    public void OutridersSevenRanks() =>
        AssertLadder("rangers", [0, 1_000, 5_000, 15_000, 40_000, 80_000, 150_000], [0, 1, 2, 3, 4, 4, 5]);

    [Fact]
    public void ForestersSevenRanks() =>
        AssertLadder("foresters", [0, 1_000, 5_000, 15_000, 40_000, 80_000, 150_000], [0, 1, 2, 3, 4, 4, 5]);

    [Fact]
    public void MiningSevenRanks() =>
        AssertLadder("mining", [0, 1_000, 5_000, 15_000, 40_000, 80_000, 150_000], [0, 1, 2, 3, 4, 4, 5]);

    [Fact]
    public void ArtificersFiveRanks() =>
        AssertLadder("artificers", [0, 1_000, 5_000, 15_000, 40_000], [0, 1, 2, 3, 5]);

    [Fact]
    public void CustodiansFiveRanks() =>
        AssertLadder("custodians", [0, 25, 125, 500, 1_250], [0, 1, 2, 3, 5]);

    [Fact]
    public void AGenericGuildSixRanks() =>
        AssertLadder("healers", [0, 1_000, 5_000, 15_000, 50_000, 100_000], [0, 1, 2, 3, 4, 5]);

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheMiningRankNameIsTheMinersCompactsLadder()
    {
        foreach (var standing in new[] { 0, 1_000, 5_000, 15_000, 40_000, 80_000, 150_000 })
        {
            Assert.Equal(MinersCompactLiaisonGump.GetRankName(standing), ClusterFGuildSystem.GetRankName("mining", standing));
        }

        Assert.Equal("Surveyor", ClusterFGuildSystem.GetRankName("mining", 15_000));
        Assert.Equal("Legendary Prospector", ClusterFGuildSystem.GetRankName("mining", 150_000));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheApprenticeTaskLiftsTheIndexToApprentice()
    {
        foreach (var key in new[] { "smithing", "rangers", "foresters", "mining", "artificers", "custodians", "healers" })
        {
            var data = new CharacterGuildData();
            data.JoinedGuilds.Add(key);
            Assert.Equal(0, ClusterFGuildSystem.GuildRankIndex(key, data));

            data.ApprenticeGuilds.Add(key);
            Assert.Equal(ClusterFGuildSystem.RankApprentice, ClusterFGuildSystem.GuildRankIndex(key, data));

            // A higher standing keeps its own index.
            data.GuildReputation[key] = ClusterFGuildSystem.RankThresholds(key)[3];
            Assert.Equal(ClusterFGuildSystem.RankMaster, ClusterFGuildSystem.GuildRankIndex(key, data));
        }
    }
}
