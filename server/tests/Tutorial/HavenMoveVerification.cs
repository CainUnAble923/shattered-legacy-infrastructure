// cc-P678 (P673 S7): New Haven moved into the Siege of Sosaria map's Haven town. Notes: shard-migration
// notes/cc-P678-new-haven-moves.md. The test host has no map files, so these facts hold the code to the spots; the docs
// repo's tools/tests/test_haven_spots.py holds the spots to SoS's own files (open ground, floor Z, inside the town).
//
// Facts:
//   1. A new character's New Haven start is the square (3634,2586,0), the one value in HavenSpots, and character
//      creation's city list reads it.
//   2. Rook, the isle moongate's way out, the quest board, the reset stone, the Fountain of Youth and the travel tour's
//      centre are at their new spots, each read from HavenSpots.
//   3. The Old Haven achievement keeps its key and now names the way beneath Haven.

#nullable enable

using Server;
using Server.Engines.CharacterCreation;
using Server.Engines.FountainOfYouth;
using Server.Engines.MLQuests.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HavenMoveVerification
{
    [Fact]
    public void ANewCharactersNewHavenStartIsTheSquare()
    {
        var city = CharacterCreation.NewHavenStartingCities[0];
        Assert.Equal("New Haven", city.City);
        Assert.Equal(new Point3D(3634, 2586, 0), city.Location);
        Assert.Equal(HavenSpots.Start, city.Location);
    }

    [Fact]
    public void RookTheMoongateExitAndTheTownsOwnPiecesStandOnTheSquare()
    {
        Assert.Equal(new Point3D(3631, 2584, 0), TutorialRecruiter.Spot);
        Assert.Equal(new Point3D(3632, 2584, 0), HavenSpots.MoongateExit);
        Assert.Equal(new Point3D(3633, 2585, 0), NewHavenQuestBoard.HomeLocation);
        Assert.Equal(new Point3D(3632, 2587, 0), ClusterFResetStoneSeeder.Location);
        Assert.Equal(new Point3D(3646, 2596, 0), FountainOfYouthSystem.NewHavenSpot);
        Assert.Equal(new Point2D(3660, 2590), TutorialWingTrials.TourCentre);
        Assert.True(Utility.InRange(HavenSpots.VetSpot, HavenSpots.Centre, 60), "the vet counts as New Haven's");
        Assert.Equal(new Point3D(3696, 2570, 14), HavenSpots.PublicMoongate);
        Assert.Equal(new Point3D(3665, 2581, 0), HavenSpots.Healer);
        Assert.Equal(new Point3D(3712, 2580, 20), HavenSpots.VetSpot);
    }

    [Fact]
    public void TheOldHavenAchievementKeepsItsKeyAndNamesTheWayBeneathHaven()
    {
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        var def = ClusterFAchievementSystem.Definitions["discovery.old_haven"];
        Assert.Contains("way beneath Haven", def.Description);
        Assert.DoesNotContain("ruins of Old Haven", def.Description);
    }
}
