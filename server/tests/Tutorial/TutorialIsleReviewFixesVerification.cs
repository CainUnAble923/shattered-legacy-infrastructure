// cc-P751: the fixes from P748's build review of the isle round (P741/P746). Notes: shard-migration
// notes/cc-P751-isle-round-review-fixes.md; the guide is notes/guides/building-and-decorating.md.
//
// Facts, each seen failing first on a mutant that undoes its fix:
//   1. The field fence closes by the guide's rule: the post 0x0838 at the north-west, one L piece 0x0835 at the
//      south-east, one piece per tile (no X of stubs, no two pieces on one tile).
//   2. The farmhouse hearth stands on the north wall under the roof's high middle, so its chimney stays under the
//      thatch, and no furniture stands in front of a window (guide 11.3, 11.4).
//   3. The farmhouse door is hung WestCCW, so open it folds along the south wall; a door hung WestCW before is rehung
//      in place on an existing world, once (guide 2.4).
//   4. The League lodge has its own name sign on its door's side, north of the road beside the League lesson sign, where
//      the camera shows it, off the door's approach; an existing world gets it once (guide 1.1, 2.5, 6). cc-P818: the
//      door moved to the east wall; the sign stands where the walk to it leaves the road.

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
public class TutorialIsleReviewFixesVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleReviewFixesVerification()
    {
        ShardTestClock.Arm();
        FoldMaps.Configure();
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialCorePlacement.IsTestShard = () => true;
        TutorialFarm.IsTestShard = () => true;
        TutorialTestStart.FreeAllCopies();
    }

    public void Dispose()
    {
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
    }

    private static TutorialCopies.Slot LastSlot => TutorialCopies.Slots[^1];

    private static List<T> Items<T>(Map map) where T : Item =>
        World.Items.Values.OfType<T>().Where(i => !i.Deleted && i.Map == map && i.Parent == null).ToList();

    private static Map SeededCopy()
    {
        var map = TutorialCopies.MapOf(LastSlot);
        TutorialCopies.Reset(LastSlot);
        TutorialCorePlacement.SeedOn(map, false, false);
        return map;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheFieldFenceClosesWithAPostAtTheNorthWestAndOneLAtTheSouthEast()
    {
        int[] fence = [0x0835, 0x0836, 0x0837, 0x0838];
        var pieces = TutorialIsleFarmLayout.Pieces.Where(p => fence.Contains(p.Id)).ToArray();
        var at = (int x, int y) => pieces.Where(p => p.X == x && p.Y == y).Select(p => p.Id).ToArray();

        Assert.Equal(new[] { 0x0838 }, at(6677, 3967));  // the post; the L 0x0835 there drew an X of stubs (P748 item 2)
        Assert.Equal(new[] { 0x0835 }, at(6695, 3983));  // one L piece, not 0x0836 + 0x0837 on one tile
        Assert.Single(pieces, p => p.Id == 0x0838);
        // one piece per tile, everywhere in the ring and the pen
        var doubled = pieces.GroupBy(p => (p.X, p.Y)).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.Empty(doubled);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheHearthStandsOnTheNorthWallUnderTheRidgeAndNothingStandsBeforeAWindow()
    {
        var pieces = TutorialIsleFarmLayout.Pieces;
        // pinned's StoneFireplaceSouthAddon on the floor row in front of the north wall (y 3972), facing the room
        Assert.Contains((0x0967, 6702, 3973, 7), pieces);
        Assert.Contains((0x0961, 6703, 3973, 7), pieces);
        Assert.DoesNotContain(pieces, p => p.Id is 0x0959 or 0x0953); // the east-facing hearth on the west wall

        // the chimney stays under the thatch: a stone fireplace is 24 high, and the roof over its tiles (the course on
        // its own column and the next one east, which the client draws over it) is higher than its top
        const int fireplaceTop = 7 + 24;
        int[] thatch = [0x05A4, 0x05A5, 0x05A6];
        foreach (var hearth in pieces.Where(p => p.Id is 0x0967 or 0x0961))
        {
            var roof = pieces.Where(p => thatch.Contains(p.Id) && (p.X == hearth.X || p.X == hearth.X + 1)).Min(p => p.Z);
            Assert.True(roof > fireplaceTop, $"the hearth at x {hearth.X} reaches Z {fireplaceTop} under a roof at Z {roof}");
        }

        // no furniture on the floor tile inside any window of the cottage (light wood windows 0x00B9 in the west and
        // east walls, x 6699 and 6705)
        int[] structure = [0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AB, 0x00AD, 0x00B9, 0x00C1, 0x0063, 0x0064, 0x0065];
        var furniture = pieces.Where(p => p.Z == 7 && !structure.Contains(p.Id) && p.Id is not (>= 0x04A9 and <= 0x04B4))
            .Select(p => (p.X, p.Y)).ToHashSet();
        var windows = pieces.Where(p => p.Id == 0x00B9).ToArray();
        Assert.NotEmpty(windows);
        foreach (var w in windows)
        {
            var inside = w.X == 6699 ? (w.X + 1, w.Y) : (w.X - 1, w.Y);
            Assert.DoesNotContain(inside, furniture);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheFarmhouseDoorFoldsAlongTheWallAndAnOldDoorIsRehungOnce()
    {
        var door = TutorialFarm.MakeIsleDoor();
        try
        {
            Assert.Equal(0x06A5 + 2 * (int)DoorFacing.WestCCW, door.ClosedId); // DarkWoodDoor: closed art 0x06A5 + 2 x facing
            Assert.Equal(TutorialFarm.IsleDoorClosedId, door.ClosedId);
            Assert.Equal(new Point3D(-1, 0, 0), door.Offset); // open, it folds west along the south wall line
        }
        finally
        {
            door.Delete();
        }

        var map = SeededCopy();
        try
        {
            // the world as P741 left it: the farmhouse door hung WestCW (it swung out past the wall)
            TutorialFarm.FindDoor(map)?.Delete();
            var old = new DarkWoodDoor(DoorFacing.WestCW);
            old.MoveToWorld(TutorialIsleFarmLayout.DoorSpot, map);

            TutorialCorePlacement.SeedOn(map, false, false);

            Assert.True(old.Deleted);
            var hung = TutorialFarm.FindDoor(map)!;
            Assert.Equal(TutorialFarm.IsleDoorClosedId, hung.ClosedId);
            Assert.Single(Items<BaseDoor>(map), d => d.Location == TutorialIsleFarmLayout.DoorSpot);

            TutorialCorePlacement.SeedOn(map, false, false);
            Assert.Same(hung, TutorialFarm.FindDoor(map));
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheLodgeHasANameSignBesideItsDoorOnce()
    {
        var door = TutorialWorld.LodgeDoor;
        var sign = TutorialCorePlacement.LodgeSign;
        // cc-P818: the door moved to the east wall; the sign stands north of the road where the walk to it leaves the road
        // (x 6789), out of the eave's shadow from the camera, beside the League sign
        Assert.True(sign.Y < door.Y);
        Assert.InRange(sign.X - TutorialWorld.LodgeStop.X, -2, 2);
        var league = TutorialCorePlacement.SignPoint("core.league");
        Assert.Equal(1, Math.Max(Math.Abs(league.X - sign.X), Math.Abs(league.Y - sign.Y)));
        Assert.Contains(TutorialCorePlacement.Props, p => p.At == sign && p.Id == TutorialCorePlacement.SignpostId &&
                                                          p.Name == TutorialCorePlacement.LodgeSignName);
        // the door's two-tile approach is clear of signs (P722: a signpost in front of a door made TazUO slide)
        Assert.DoesNotContain(TutorialCorePlacement.Props, p => p.At.Y == door.Y && p.At.X > door.X && p.At.X <= door.X + 2);

        bool IsSign(Item i) => i.ItemID == TutorialCorePlacement.SignpostId && i.Name == TutorialCorePlacement.LodgeSignName;
        var map = SeededCopy();
        try
        {
            var placed = Assert.Single(Items<Static>(map), IsSign);
            Assert.Equal(TutorialWorld.Ground(map, sign), placed.Location);

            // an existing world without it gets it once
            placed.Delete();
            TutorialCorePlacement.SeedOn(map, false, false);
            var again = Assert.Single(Items<Static>(map), IsSign);
            TutorialCorePlacement.SeedOn(map, false, false);
            Assert.Same(again, Assert.Single(Items<Static>(map), IsSign));
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }
}
