// LeagueOfficeDecorVerification.cs
//
// cc-P803: the League field office in New Haven, after Chase's "haven league building bulletin board needs replacement, in
// fact have deco golem review that space" (11-05). Notes in shard-migration notes/cc-P803-league-building-deco.md, renders
// and shots in notes/cc-P803-review. Written by notes/cc-P803-tools/make_facts.py for main (SoS Haven's hall, walls x3658-3674, y2547-2562, floor z0).
//
// What the facts pin:
//   1. The League board hangs on the floor tile in front of a wall, with the art that hangs on that wall (0x1E5E on a west
//      wall, 0x1E5F on a north wall: both rendered against a wall, notes/cc-P803-review/board-facings.png), and seeding
//      places it so. It stood in a wall's own line before (the old house's front door; the SoS hall's north wall).
//   2. A League board left on an old spot moves to the wall on the next seed, the same item (serial), and a second seed
//      changes nothing. A second League board on an old spot, when the wall already has one, is removed, so the office
//      never shows two (the after run of 11-07 found two: the bot world is a main world, and the release had listed main's
//      old spot as its own, moved that board across town and left its own in the doorway). Only this town's own old spots
//      count: a board in another building is left alone.
//   3. No floor-standing prop of the office stands on a window's floor tile (building guide 11.4, 11.13).
//   4. Every bookcase stands against the wall its art faces away from: east-facing cases (0x0A99, 0x0A9A, 0x0A9C) on a
//      west wall's floor tile, south-facing ones (0x0A97, 0x0A98) on a north wall's (guide 3).
//   5. On load, an office decor that is not the current layout is swapped for it; a current one, or one standing in
//      another building, is left alone; and running it again changes nothing.
//
// The test host has no map files, so the walls and windows are a table here, read from the map by
// notes/cc-P803-tools/office_statics.py.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueOfficeDecorVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueOfficeDecorVerification(ITestOutputHelper output) => _out = output;

    private const int FloorZ = 0;

    // The board's tile and art, and the wall it hangs on (x of a west wall's pieces, or y of a north wall's).
    private static readonly Point3D BoardSpot = new(3662, 2548, 0);
    private const int BoardArt = 0x1E5F;

    // Floor tiles in front of a window (the window piece's tile, one step into the room).
    private static readonly Point2D[] WindowFloor = [new(3661, 2561), new(3662, 2561), new(3664, 2561), new(3665, 2561), new(3670, 2561), new(3673, 2561)];

    // Rooms: (west wall x, north wall y, x1, y1, x2, y2 of the floor).
    private static readonly (int WestX, int NorthY, int X1, int Y1, int X2, int Y2)[] Rooms = [(3658, 2547, 3659, 2548, 3673, 2561)];

    private static readonly int[] EastFacingCases = [0x0A99, 0x0A9A, 0x0A9C];
    private static readonly int[] SouthFacingCases = [0x0A97, 0x0A98];

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheLeagueBoardHangsInFrontOfItsWallWithThatWallsArt()
    {
        var post = ClusterFRequisitionSeeder.OfficeBoard;
        Assert.Equal(BoardSpot, new Point3D(post.X, post.Y, post.Z));
        Assert.Equal(BoardArt, ClusterFRequisitionSeeder.OfficeBoardItemID);

        // The spot is a floor tile of a room, next to the wall the art hangs on, and never an old spot in a wall's line.
        var room = Rooms.Single(r => post.X >= r.X1 && post.X <= r.X2 && post.Y >= r.Y1 && post.Y <= r.Y2);
        Assert.True(BoardArt == 0x1E5E ? post.X == room.WestX + 1 : post.Y == room.NorthY + 1);
        Assert.DoesNotContain(ClusterFRequisitionSeeder.OldOfficeBoardSpots, p => p.X == post.X && p.Y == post.Y);
        Assert.DoesNotContain(WindowFloor, w => w.X == post.X && w.Y == post.Y);

        var made = ClearBoards();
        var appraisers = Appraisers();
        try
        {
            ClusterFRequisitionSeeder.Seed(false);
            var board = Assert.Single(LeagueBoardsNear());
            Assert.Equal(BoardSpot, board.Location);
            Assert.Equal(BoardArt, board.ItemID);
            Assert.False(board.Movable);
        }
        finally
        {
            ClearBoards();
            Restore(made);
            DeleteNewAppraisers(appraisers);
        }
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData(3459, 2596, 18)]
    [InlineData(3662, 2547, 0)]
    public void ABoardOnAnOldSpotMovesToTheWallAndStaysTheSameItem(int x, int y, int z)
    {
        var made = ClearBoards(x, y);
        var appraisers = Appraisers();
        var old = new RequisitionsBoard();
        try
        {
            old.MoveToWorld(new Point3D(x, y, z), Map.Trammel);
            var serial = old.Serial;

            ClusterFRequisitionSeeder.Seed(false);
            Assert.False(old.Deleted);
            Assert.Equal(serial, old.Serial);
            Assert.Equal(BoardSpot, old.Location);
            Assert.Equal(BoardArt, old.ItemID);

            ClusterFRequisitionSeeder.Seed(false);
            Assert.Equal(BoardSpot, old.Location);
            Assert.Single(LeagueBoardsNear());
            Assert.Null(ClusterFRequisitionSeeder.MoveOldOfficeBoard(Map.Trammel));
        }
        finally
        {
            old.Delete();
            ClearBoards(x, y);
            Restore(made);
            DeleteNewAppraisers(appraisers);
        }
    }

    [Fact]
    public void ASecondBoardOnAnOldSpotIsRemovedSoTheOfficeShowsOne()
    {
        var spots = ClusterFRequisitionSeeder.OldOfficeBoardSpots;
        var made = ClearBoards(spots[^1].X, spots[^1].Y);
        var appraisers = Appraisers();
        var extra = new List<RequisitionsBoard>();
        try
        {
            ClusterFRequisitionSeeder.Seed(false);
            var hung = Assert.Single(LeagueBoardsNear());
            foreach (var p in spots)
            {
                var b = new RequisitionsBoard();
                b.MoveToWorld(p, Map.Trammel);
                extra.Add(b);
            }

            ClusterFRequisitionSeeder.Seed(false);
            Assert.All(extra, b => Assert.True(b.Deleted));
            Assert.False(hung.Deleted);
            Assert.Equal(BoardSpot, hung.Location);
            Assert.Single(LeagueBoardsNear());

            // With the wall empty, two old boards leave one board, on the wall.
            hung.Delete();
            extra.Clear();
            foreach (var p in spots.Concat(spots))
            {
                var b = new RequisitionsBoard();
                b.MoveToWorld(p, Map.Trammel);
                extra.Add(b);
            }

            ClusterFRequisitionSeeder.Seed(false);
            var one = Assert.Single(LeagueBoardsNear());
            Assert.Equal(BoardSpot, one.Location);
            Assert.Equal(extra.Count - 1, extra.Count(b => b.Deleted));
            Assert.Null(ClusterFRequisitionSeeder.MoveOldOfficeBoard(Map.Trammel));
        }
        finally
        {
            foreach (var b in extra)
            {
                b.Delete();
            }

            ClearBoards(spots[^1].X, spots[^1].Y);
            Restore(made);
            DeleteNewAppraisers(appraisers);
        }
    }


    // ---------------------------------------------------------------- 3

    [Fact]
    public void NoFloorPropStandsOnAWindowsFloorTile()
    {
        var onWindows = ClusterFRegistrarOfficeLayout.Components
            .Where(c => c.Z == FloorZ && WindowFloor.Any(w => w.X == c.X && w.Y == c.Y))
            .Select(c => $"0x{c.ItemID:X4} {c.Name} at {c.X},{c.Y}")
            .ToList();
        _out.WriteLine(string.Join("\n", onWindows));
        Assert.Empty(onWindows);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void EveryBookcaseStandsAgainstTheWallItsArtFacesAwayFrom()
    {
        var wrong = new List<string>();
        foreach (var c in ClusterFRegistrarOfficeLayout.Components.Where(c => c.Name == "bookcase"))
        {
            var room = Rooms.Single(r => c.X >= r.X1 && c.X <= r.X2 && c.Y >= r.Y1 && c.Y <= r.Y2);
            var ok = EastFacingCases.Contains(c.ItemID) ? c.X == room.WestX + 1
                : SouthFacingCases.Contains(c.ItemID) && c.Y == room.NorthY + 1;
            if (!ok)
            {
                wrong.Add($"0x{c.ItemID:X4} at {c.X},{c.Y}");
            }
        }

        _out.WriteLine(string.Join("\n", wrong));
        Assert.Empty(wrong);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AStaleOfficeIsSwappedOnLoadAndACurrentOrDistantOneIsLeftAlone()
    {
        ClearOffice();
        try
        {
            // No office decor: nothing placed (the load step never seeds a world from nothing).
            Assert.Empty(ClusterFRegistrarOfficeSeeder.UpgradeStale());
            Assert.Empty(ClusterFRegistrarOfficeSeeder.FindOfficeAddons());

            // Current: left alone.
            ClusterFRegistrarOfficeSeeder.Seed(false);
            var serials = ClusterFRegistrarOfficeSeeder.FindOfficeAddons().Select(a => a.Serial).OrderBy(s => s.Value).ToList();
            Assert.Empty(ClusterFRegistrarOfficeSeeder.UpgradeStale());
            Assert.Equal(serials, ClusterFRegistrarOfficeSeeder.FindOfficeAddons().Select(a => a.Serial).OrderBy(s => s.Value).ToList());

            // Stale (an older layout: one prop the current one has is missing, another stands where it used to): swapped.
            var office = ClusterFRegistrarOfficeSeeder.FindOfficeAddons().OfType<RegistrarOfficeAddon>().Single();
            var gone = office.Components[^1];
            office.Components.Remove(gone);
            gone.Delete();
            Assert.False(ClusterFRegistrarOfficeSeeder.IsCurrent(office));
            var lines = ClusterFRegistrarOfficeSeeder.UpgradeStale();
            _out.WriteLine(string.Join("\n", lines));
            Assert.NotEmpty(lines);
            Assert.True(office.Deleted);
            var now = ClusterFRegistrarOfficeSeeder.FindOfficeAddons();
            Assert.Equal(3, now.Count);
            Assert.All(now, a => Assert.True(ClusterFRegistrarOfficeSeeder.IsCurrent(a)));

            // Twice is once.
            Assert.Empty(ClusterFRegistrarOfficeSeeder.UpgradeStale());

            // An office standing in another building (another town's coordinates) is never touched.
            var quarters = now.OfType<RegistrarQuartersAddon>().Single();
            quarters.MoveToWorld(new Point3D(quarters.X - 200, quarters.Y, quarters.Z), Map.Trammel);
            var before = ClusterFRegistrarOfficeSeeder.FindOfficeAddons().Select(a => a.Serial).OrderBy(s => s.Value).ToList();
            var away = ClusterFRegistrarOfficeSeeder.UpgradeStale();
            Assert.Single(away);
            Assert.Contains("left alone", away[0]);
            Assert.Equal(before, ClusterFRegistrarOfficeSeeder.FindOfficeAddons().Select(a => a.Serial).OrderBy(s => s.Value).ToList());
        }
        finally
        {
            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- helpers

    private static List<RequisitionsBoard> LeagueBoardsNear() =>
        World.Items.Values.OfType<RequisitionsBoard>()
            .Where(b => !b.Deleted && b.IsLeagueBoard && b.Map == Map.Trammel &&
                        Math.Abs(b.X - BoardSpot.X) <= 8 && Math.Abs(b.Y - BoardSpot.Y) <= 8)
            .ToList();

    // Takes League boards near the office (and near an extra tile) off the map for the fact; Restore puts them back.
    private static List<(RequisitionsBoard Board, Point3D At)> ClearBoards(int x = 0, int y = 0)
    {
        var moved = new List<(RequisitionsBoard, Point3D)>();
        foreach (var b in World.Items.Values.OfType<RequisitionsBoard>().ToList())
        {
            if (!b.Deleted && b.Map == Map.Trammel &&
                (Math.Abs(b.X - BoardSpot.X) <= 8 && Math.Abs(b.Y - BoardSpot.Y) <= 8 || Math.Abs(b.X - x) <= 8 && Math.Abs(b.Y - y) <= 8))
            {
                moved.Add((b, b.Location));
                b.Internalize();
            }
        }

        return moved;
    }

    private static void Restore(List<(RequisitionsBoard Board, Point3D At)> moved)
    {
        foreach (var (b, at) in moved)
        {
            if (!b.Deleted)
            {
                b.MoveToWorld(at, Map.Trammel);
            }
        }
    }

    // Seed also places the League Appraisers where none stands; the facts take away the ones they made.
    private static HashSet<Serial> Appraisers() =>
        World.Mobiles.Values.OfType<Appraiser>().Select(a => a.Serial).ToHashSet();

    private static void DeleteNewAppraisers(HashSet<Serial> before)
    {
        foreach (var a in World.Mobiles.Values.OfType<Appraiser>().Where(a => !before.Contains(a.Serial)).ToList())
        {
            a.Delete();
        }
    }

    private static void ClearOffice()
    {
        foreach (var a in ClusterFRegistrarOfficeSeeder.FindOfficeAddons())
        {
            a.Delete();
        }
    }
}
