// ScoringArcheryButteArtVerification.cs
//
// cc-P632 (P571 design, stage 1): the scoring archery butte's hit pictures on the server. Notes in shard-migration
// notes/cc-P632-build-scoring-buttes.md. The players' copy (records.json, registry, PNGs) is checked against this table by
// player-package\tests\Build-UoOverrides.Tests.ps1.
//
// Facts:
//   1. The display states keep their saved numbers, 0 (Empty) to 10 (BoltOuter).
//   2. Every shot state has its own picture in each facing: 20 different IDs, exactly 46690-46709, east ones in the east
//      blocks and south ones in the south blocks; Empty (and a value no state has) is the stock target of that facing.
//   2b. cc-P647: every state's exact east and south picture, from a literal table.
//   3. After ShatteredLegacyArt.Apply every picture has the stock butte's record: "archery butte", ArticleAn |
//      Impassable, weight 255, height 10, so each blocks its tile as the stock target does.

using System;
using System.Linq;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ScoringArcheryButteArtVerification
{
    private static readonly ButteDisplay[] Shots = Enum.GetValues<ButteDisplay>().Where(d => d != ButteDisplay.Empty).ToArray();

    [Fact]
    public void TheDisplayStatesKeepTheirSavedNumbers()
    {
        Assert.Equal(
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            new[]
            {
                ButteDisplay.Empty, ButteDisplay.ArrowMiss, ButteDisplay.ArrowBullseye, ButteDisplay.ArrowInner,
                ButteDisplay.ArrowMiddle, ButteDisplay.ArrowOuter, ButteDisplay.BoltMiss, ButteDisplay.BoltBullseye,
                ButteDisplay.BoltInner, ButteDisplay.BoltMiddle, ButteDisplay.BoltOuter
            }.Select(d => (int)d).ToArray()
        );
        Assert.Equal(10, Shots.Length);
    }

    [Fact]
    public void EveryShotHasItsOwnPictureInEachFacing()
    {
        var east = Shots.Select(d => ScoringArcheryButteArt.ItemId(d, true)).ToArray();
        var south = Shots.Select(d => ScoringArcheryButteArt.ItemId(d, false)).ToArray();
        var all = east.Concat(south).ToArray();

        Assert.Equal(20, all.Distinct().Count());
        Assert.Equal(Enumerable.Range(46690, 20), all.Order());
        Assert.All(east, id => Assert.True(id is >= 46690 and <= 46694 or >= 46700 and <= 46704, $"east {id}"));
        Assert.All(south, id => Assert.True(id is >= 46695 and <= 46699 or >= 46705 and <= 46709, $"south {id}"));

        // Arrows in the arrow blocks, bolts in the bolt blocks; a miss is each block's last picture.
        Assert.Equal(46690, ScoringArcheryButteArt.ItemId(ButteDisplay.ArrowBullseye, true));
        Assert.Equal(46694, ScoringArcheryButteArt.ItemId(ButteDisplay.ArrowMiss, true));
        Assert.Equal(46699, ScoringArcheryButteArt.ItemId(ButteDisplay.ArrowMiss, false));
        Assert.Equal(46700, ScoringArcheryButteArt.ItemId(ButteDisplay.BoltBullseye, true));
        Assert.Equal(46708, ScoringArcheryButteArt.ItemId(ButteDisplay.BoltOuter, false));

        Assert.Equal(0x100A, ScoringArcheryButteArt.ItemId(ButteDisplay.Empty, true));
        Assert.Equal(0x100B, ScoringArcheryButteArt.ItemId(ButteDisplay.Empty, false));
        Assert.Equal(0x100A, ScoringArcheryButteArt.ItemId((ButteDisplay)99, true));
        Assert.Equal(0x100B, ScoringArcheryButteArt.ItemId((ButteDisplay)99, false));
    }

    // cc-P647 (P644 review): each state's exact picture, written out from the registered PNG names
    // (46690 "Arrow East Bullseye" ... 46709 "Bolt South Total Miss") rather than from the block constants, so
    // two swapped rings or ammo blocks fail here.
    [Theory]
    [InlineData(ButteDisplay.ArrowBullseye, 46690, 46695)]
    [InlineData(ButteDisplay.ArrowInner, 46691, 46696)]
    [InlineData(ButteDisplay.ArrowMiddle, 46692, 46697)]
    [InlineData(ButteDisplay.ArrowOuter, 46693, 46698)]
    [InlineData(ButteDisplay.ArrowMiss, 46694, 46699)]
    [InlineData(ButteDisplay.BoltBullseye, 46700, 46705)]
    [InlineData(ButteDisplay.BoltInner, 46701, 46706)]
    [InlineData(ButteDisplay.BoltMiddle, 46702, 46707)]
    [InlineData(ButteDisplay.BoltOuter, 46703, 46708)]
    [InlineData(ButteDisplay.BoltMiss, 46704, 46709)]
    public void EachShotShowsItsOwnRingAndAmmoPicture(ButteDisplay display, int east, int south)
    {
        Assert.Equal(east, ScoringArcheryButteArt.ItemId(display, true));
        Assert.Equal(south, ScoringArcheryButteArt.ItemId(display, false));
    }

    [Fact]
    public void EveryPictureHasTheStockButtesRecord()
    {
        ShatteredLegacyArt.Apply();
        for (var id = 46690; id <= 46709; id++)
        {
            var data = TileData.ItemTable[id];
            Assert.Equal("archery butte", data.Name);
            Assert.Equal(TileFlag.ArticleAn | TileFlag.Impassable, data.Flags);
            Assert.True(data.Impassable, $"{id} blocks");
            Assert.Equal(255, data.Weight);
            Assert.Equal(10, data.Height);
            // cc-P647: the rest of the stock record (layer, count, animation, value) is 0, as in 0x100A.
            Assert.Equal(0, data.Quality);
            Assert.Equal(0, data.Quantity);
            Assert.Equal(0, data.Animation);
            Assert.Equal(0, data.Value);
        }

        Assert.Equal(20, ScoringArcheryButteArt.Records.Length);
    }
}
