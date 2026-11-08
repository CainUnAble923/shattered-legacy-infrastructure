// cc-P848 (P842's build review of P838): on the release's stock New Haven, P806 put the housing lesson's stop 2 and its
// signpost on the lawn at 3474,2603 and 3474,2601. P801's jail was then built over that lawn, so the sign stood in the
// jail hall in front of the middle cell's gate. Stop 2 is now on the open lawn south of the town wall, and a world seeded
// before keeps no sign in the jail: the seeder takes a named Haven piece away from any spot but its own.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HavenHousingStopVerification
{
    /// <summary>Where P806 stood stop 2's sign: in the release jail's hall since P801.</summary>
    private static readonly Point3D OldStop2Sign = new(3474, 2601, 11);

    private static TutorialWingTrials.HavenPiece Stop2Sign =>
        TutorialWingTrials.HavenPieces.Single(p => p.Name.StartsWith("Housing lesson, stop 2", StringComparison.Ordinal));

    [Fact]
    public void Stop2SignInTheJailHallIsTakenAwayOnLoadOnce()
    {
        var map = HavenSpots.Map;
        var sign = Stop2Sign;
        var old = new Static(sign.Id) { Name = sign.Name };
        old.MoveToWorld(OldStop2Sign, map);
        try
        {
            TutorialWingTrials.SeedHaven(false);
            Assert.True(old.Deleted);
            Assert.Single(ItemsAt(map, sign.At), i => i.ItemID == sign.Id && i.Name == sign.Name);
            Assert.DoesNotContain(ItemsAt(map, OldStop2Sign), i => i.Name == sign.Name);

            var again = TutorialWingTrials.SeedHaven(false); // idempotent: nothing more to take away or place
            Assert.Contains("took away 0 from old spots", again);
            Assert.Single(ItemsAt(map, sign.At), i => i.ItemID == sign.Id && i.Name == sign.Name);
        }
        finally
        {
            foreach (var piece in TutorialWingTrials.HavenPieces)
            {
                foreach (var item in ItemsAt(map, piece.At).Where(i => i.Name == piece.Name)) { item.Delete(); }
            }

            old.Delete();
        }
    }

    [Fact]
    public void Stop2IsOutsideTheJailOnTheOpenLawn()
    {
        var stop = TutorialWingTrials.HousingStop(1);
        Assert.Equal(new Point3D(3482, 2623, stop.Z), stop);
        Assert.Equal(new Point3D(3481, 2622, 0), Stop2Sign.At);
        Assert.False(stop.X >= 3469 && stop.X <= 3479 && stop.Y >= 2597 && stop.Y <= 2606);
    }

    private static List<Item> ItemsAt(Map map, Point3D at)
    {
        var list = new List<Item>();
        foreach (var item in map.GetItemsAt(at.X, at.Y)) { list.Add(item); }
        return list;
    }
}
