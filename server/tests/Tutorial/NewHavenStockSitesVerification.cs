// NewHavenStockSitesVerification.cs
//
// cc-P799 (isle release only): Chase's 11-05 notes on the New Haven testers play, the stock town, with P743's lessons brought over.
//
// Facts:
//   1. The music lessons are in the theater: the lesson arms there, its echoes stand on the stage, and every line that names
//      the place says so.
//   2. The mining lesson sends the trainee to the mine in the mountains, not a spot in town, and tells them to click the rock.
//   3. Animals stays open when the trainee reaches New Haven, and neither the healing nor the isle's pet finishes it.
//   4. Each place is on the stock town, on Haven island, far from the trades yard where these lessons used to arm.

using System.Linq;
using Server;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenStockSitesVerification
{
    private static readonly Point2D OldYard = TutorialWingTrials.TradesYard;

    // cc-P806: the stall's east wall (x 3536) stands in front of x 3535 from the default camera, so a piece there cannot be seen.
    [Fact]
    public void TheStallsBucketStandsWhereTheCameraSeesIt()
    {
        var bucket = TutorialWingTrials.HavenPieces.Single(x => x.Id == 0x0FFA);
        Assert.Equal((3534, 2574), (bucket.At.X, bucket.At.Y));
    }

    [Fact]
    public void TheMusicLessonsAreInTheTheater()
    {
        var t = TutorialWingTrials.Trade("bard.trial");
        Assert.NotNull(t);
        Assert.Equal((HavenSpots.Theater.X, HavenSpots.Theater.Y), (t.Place.X, t.Place.Y));
        Assert.Contains("theater", t.Where);
        Assert.Contains("theater", TutorialStations.Find("bard.trial")!.Where);
        Assert.Contains("theater", TutorialWingTrials.FollowLine("bard.trial"));

        // The stage is Z 60, the audience floor before it Z 55; the canopy and curtains cover x 3410-3411 (cc-P806).
        Assert.All(new[] { TutorialWingTrials.BardEchoA, TutorialWingTrials.BardEchoB }, p => Assert.InRange(p.X, 3412, 3413));
        Assert.Equal(60, TutorialWingTrials.BardEchoA.Z);
        Assert.Equal(60, TutorialWingTrials.BardEchoB.Z);
        Assert.Equal(55, HavenSpots.Theater.Z);
        Assert.InRange(HavenSpots.Theater.X, 3410, 3421);
        Assert.InRange(HavenSpots.Theater.Y, 2598, 2607);
    }

    [Fact]
    public void TheMiningLessonGoesToTheMineInTheMountains()
    {
        var t = TutorialWingTrials.Trade("gathering.mining");
        Assert.NotNull(t);
        Assert.Equal((HavenSpots.MiningCave.X, HavenSpots.MiningCave.Y), (t.Place.X, t.Place.Y));
        Assert.True(HavenSpots.MiningCave.Y > 2740, "south of the town, in the mountains");
        Assert.Contains("mine in the mountains", t.Where);
        Assert.Contains("mine in the mountains", TutorialWingTrials.FollowLine("gathering.mining"));
        Assert.Contains("mine in the mountains", TutorialStations.Find("gathering.mining")!.Where);
        Assert.Contains("rock wall", t.Hint);
        Assert.DoesNotContain("cave floor", t.Hint);

        // Its banks: the arm spot is one of the lesson's tiles.
        Assert.Contains(TutorialWingTrials.MiningTiles, p => p.X == HavenSpots.MiningCave.X && p.Y == HavenSpots.MiningCave.Y);
    }

    [Fact]
    public void AnimalsIsAtTheStables()
    {
        var t = TutorialWingTrials.Trade("animals.taming");
        Assert.NotNull(t);
        Assert.Equal((HavenSpots.Stables.X, HavenSpots.Stables.Y), (t.Place.X, t.Place.Y));
        Assert.Contains("stables", t.Where);
        Assert.Contains("stables", TutorialStations.Find("animals.taming")!.Where);
        Assert.True(t.Steps.Length >= 2, "heal, then tame");
    }

    [Fact]
    public void EachPlaceIsOnHavenIslandAwayFromTheOldYard()
    {
        foreach (var p in new[]
                 {
                     HavenSpots.Rook, HavenSpots.MoongateExit, HavenSpots.ProvingGrounds, HavenSpots.MageryHall, HavenSpots.Theater,
                     HavenSpots.MiningCave, HavenSpots.Stables, HavenSpots.SickStall
                 })
        {
            Assert.True(HavenSpots.InIslandBounds(p), p.ToString());
            Assert.True(p.X < 3600, $"{p} is on the stock town, not main's"); // main's town is 3600 to 3760
            Assert.True(new Point2D(p.X, p.Y) != OldYard);
        }
    }

    [Fact]
    public void EverySignedPieceStandsInTown()
    {
        Assert.All(TutorialWingTrials.HavenPieces, p => Assert.True(p.At.X < 3600, p.Name));
        Assert.Contains(TutorialWingTrials.HavenPieces, p => p.Name.Contains("mine"));
        Assert.Contains(TutorialWingTrials.HavenPieces, p => p.Name.Contains("stables"));
    }
}
