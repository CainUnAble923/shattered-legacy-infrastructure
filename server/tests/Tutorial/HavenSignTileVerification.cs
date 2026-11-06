// cc-P814 (P807's build review of P806): no fixed New Haven signpost shares a tile with a static of the client's map. P806's stop 1 sign
// stood through a stock bench at 3469,2589, and no fact could see it: the gate's host loads no client files.
//
// So the map's own statics round each sign are a table here, read from this branch's statics by shard-migration
// notes/cc-P814-tools/sign_statics.py. A sign moved outside its box fails the first fact until the table is made again.

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
public class HavenSignTileVerification
{
    /// <summary>The boxes the table covers: west, north, east, south, inclusive.</summary>
    private static readonly (int X1, int Y1, int X2, int Y2)[] Boxes =
    [
        (3698, 2738, 3706, 2746), (3605, 2610, 3613, 2618), (3709, 2587, 3717, 2595), (3700, 2600, 3708, 2608),
    ];

    /// <summary>Every tile in those boxes that holds a static on this branch's client map.</summary>
    private static readonly (int X, int Y)[] StaticTiles =
    [
        (3605, 2610), (3605, 2611), (3605, 2612), (3605, 2613), (3605, 2614), (3605, 2615), (3605, 2616),
        (3605, 2617), (3605, 2618), (3606, 2610), (3606, 2611), (3606, 2612), (3606, 2613), (3606, 2614),
        (3606, 2615), (3606, 2616), (3606, 2617), (3606, 2618), (3607, 2610), (3607, 2611), (3607, 2612),
        (3607, 2613), (3607, 2614), (3607, 2615), (3607, 2616), (3607, 2617), (3607, 2618), (3608, 2611),
        (3608, 2612), (3608, 2613), (3608, 2614), (3608, 2615), (3608, 2616), (3608, 2617), (3608, 2618),
        (3698, 2744), (3699, 2738), (3700, 2601), (3700, 2602), (3700, 2740), (3701, 2600), (3701, 2601),
        (3701, 2603), (3702, 2601), (3702, 2602), (3702, 2603), (3704, 2601), (3704, 2739), (3705, 2601),
        (3706, 2603), (3706, 2738), (3708, 2601), (3708, 2603), (3709, 2587), (3709, 2588), (3709, 2589),
        (3709, 2590), (3709, 2591), (3709, 2592), (3710, 2587), (3710, 2588), (3710, 2589), (3710, 2590),
        (3710, 2591), (3710, 2592), (3711, 2587), (3711, 2588), (3711, 2589), (3711, 2590), (3711, 2591),
        (3711, 2592), (3712, 2587), (3712, 2588), (3712, 2589), (3712, 2590), (3712, 2591), (3712, 2592),
        (3713, 2589), (3714, 2589), (3714, 2590), (3715, 2589),
    ];

    private static IEnumerable<TutorialWingTrials.HavenPiece> Signs => TutorialWingTrials.HavenPieces.Where(p => p.Id == 0xA854);

    [Fact]
    public void TheTableCoversEveryFixedSign()
    {
        Assert.NotEmpty(Signs);
        foreach (var sign in Signs)
        {
            Assert.Contains(Boxes, b => sign.At.X >= b.X1 && sign.At.X <= b.X2 && sign.At.Y >= b.Y1 && sign.At.Y <= b.Y2);
        }
    }

    [Fact]
    public void NoFixedSignSharesATileWithAStaticOfTheMap()
    {
        foreach (var sign in Signs)
        {
            Assert.DoesNotContain((sign.At.X, sign.At.Y), StaticTiles);
        }
    }

    [Fact]
    public void AStop1SignLeftAtItsOldSpotIsTakenAwayOnASeededWorld()
    {
        var map = HavenSpots.Map;
        var sign = Signs.Single(x => x.Name.StartsWith("Housing lesson, stop 1", StringComparison.Ordinal));
        var old = new Static(sign.Id) { Name = sign.Name };
        old.MoveToWorld(new Point3D(sign.At.X - 2, sign.At.Y + 1, 10), map); // 3469,2589 on the release: where P806 put it
        try
        {
            TutorialWingTrials.SeedHaven(false);
            Assert.True(old.Deleted);
            var here = new List<Item>();
            foreach (var item in map.GetItemsAt(sign.At.X, sign.At.Y)) { here.Add(item); }
            Assert.Single(here, i => i.ItemID == sign.Id && i.Name == sign.Name);
        }
        finally
        {
            foreach (var piece in TutorialWingTrials.HavenPieces)
            {
                var gone = new List<Item>();
                foreach (var item in map.GetItemsAt(piece.At.X, piece.At.Y))
                {
                    if (item.Name == piece.Name) { gone.Add(item); }
                }

                foreach (var item in gone) { item.Delete(); }
            }

            old.Delete();
        }
    }
}
