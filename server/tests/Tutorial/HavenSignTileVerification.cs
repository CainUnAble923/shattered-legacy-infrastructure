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
        (3465, 2620, 3473, 2628), (3516, 2564, 3524, 2572), (3467, 2584, 3475, 2592), (3477, 2618, 3485, 2626),
    ];

    /// <summary>Every tile in those boxes that holds a static on this branch's client map.</summary>
    private static readonly (int X, int Y)[] StaticTiles =
    [
        (3466, 2621), (3466, 2623), (3466, 2624), (3467, 2621), (3467, 2623), (3467, 2624), (3468, 2584),
        (3468, 2588), (3468, 2589), (3468, 2590), (3468, 2621), (3468, 2623), (3469, 2587), (3469, 2588),
        (3469, 2589), (3469, 2590), (3469, 2620), (3470, 2586), (3470, 2587), (3471, 2586), (3471, 2587),
        (3472, 2585), (3472, 2586), (3472, 2587), (3473, 2585), (3473, 2586), (3473, 2587), (3477, 2622),
        (3478, 2624), (3479, 2618), (3479, 2621), (3479, 2623), (3479, 2626), (3485, 2624), (3517, 2566),
        (3518, 2565), (3518, 2566), (3518, 2567), (3519, 2564), (3519, 2565), (3519, 2566), (3519, 2567),
        (3520, 2564), (3521, 2564), (3521, 2565), (3522, 2564), (3522, 2565), (3523, 2564), (3523, 2565),
        (3524, 2564),
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
