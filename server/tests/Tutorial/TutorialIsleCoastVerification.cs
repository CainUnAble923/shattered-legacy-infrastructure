// cc-P741: the isle's coast round (Chase's 10-30 notes): the whole wreck, the farm turned round (field west, farmhouse
// east), the signs north of their paths. Notes: shard-migration notes/cc-P741-isle-coast-and-wreck.md.
//
// Facts, each seen failing first:
//   1. The wreck is the whole ship (both halves, OSI's composition), and a world seeded with P724's half has it swapped
//      for the whole ship on the next seeding, once.
//   2. The farmhouse stands east of the field, by its gate, so the lesson runs one way; the crops are sparse short rows
//      with lanes between (no corn or wheat to hide the rats).
//   3. A world laid before the farm moved is moved in place: its old field gate, the fence piece by it, Pip's old bench
//      and blood and the old pest and pen spawners go; Wren, Tobin and Pip walk to their new spots; nothing is doubled.
//   4. The chat lesson's farm signpost, laid on the road's edge before, is moved north of the road, not doubled.
//   5. A lesson sign standing where P720 put it is moved north of its path by the next seeding, not doubled.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialIsleCoastVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleCoastVerification()
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

    private static List<T> Mobiles<T>(Map map) where T : Mobile =>
        World.Mobiles.Values.OfType<T>().Where(m => !m.Deleted && m.Map == map).ToList();

    private static Map SeededCopy()
    {
        var map = TutorialCopies.MapOf(LastSlot);
        TutorialCopies.Reset(LastSlot);
        TutorialCorePlacement.SeedOn(map, false, false);
        return map;
    }

    [Fact]
    public void TheWreckIsTheWholeShipAndP724sHalfIsSwapped()
    {
        // both halves: the 30 "West" pieces (0x47B8-0x47D5) and the 30 "East" pieces (0x48A4-0x490F) of the art
        var pieces = WakeningIsleShipwreckAddon.Pieces;
        Assert.Equal(30, pieces.Count(p => p.Id is >= 0x47B8 and <= 0x47D5));
        Assert.Equal(30, pieces.Count(p => p.Id is >= 0x48A4 and <= 0x490F));
        // the halves lie either side of the break, the West half west of it
        Assert.True(pieces.Where(p => p.Id is >= 0x47B8 and <= 0x47D5).Max(p => p.X) < 0);
        Assert.True(pieces.Where(p => p.Id is >= 0x48A4 and <= 0x490F).Min(p => p.X) > 0);

        var map = SeededCopy();
        try
        {
            foreach (var w in Items<WakeningIsleShipwreckAddon>(map)) { w.Delete(); }
            var half = new WakeningIsleShipwreckAddon();
            half.MoveToWorld(TutorialIsleAddons.OldWreckSpot, map); // a world seeded before: the wreck at P724's spot

            var line = TutorialIsleAddons.Seed(map, false);
            Assert.Contains("replaced 1 old wreck", line);
            Assert.True(half.Deleted);
            var wreck = Assert.Single(Items<WakeningIsleShipwreckAddon>(map));
            Assert.Equal(TutorialIsleAddons.WreckSpot, wreck.Location);
            Assert.Equal(pieces.Length, wreck.Components.Count);

            Assert.Contains("placed 0", TutorialIsleAddons.Seed(map, false));
            Assert.Single(Items<WakeningIsleShipwreckAddon>(map));
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }

    [Fact]
    public void TheFarmhouseStandsEastOfTheFieldAndTheCropsAreSparse()
    {
        int[] fence = [0x0835, 0x0836, 0x0837];
        var fieldEast = TutorialIsleFarmLayout.Pieces.Where(p => fence.Contains(p.Id)).Max(p => p.X);
        // the cottage's foundation and walls: all of it east of the field's east fence, beside the gate
        var house = TutorialIsleFarmLayout.Pieces.Where(p => p.Id is 0x0063 or 0x0064 or 0x0065).ToArray();
        Assert.True(house.Min(p => p.X) > fieldEast, $"the farmhouse starts at x {house.Min(p => p.X)}");
        Assert.True(house.Min(p => p.X) - TutorialFarm.FieldGateSpots[0].X <= 5);
        // one way along the road: the farm gate, then the farmhouse door, then the yard, each further east
        Assert.True(TutorialWorld.FarmGate.X < TutorialIsleFarmLayout.DoorSpot.X);
        Assert.True(TutorialIsleFarmLayout.DoorSpot.X < TutorialWorld.Yard.X);
        Assert.True(TutorialCorePlacement.MarkerSpot("core.healing").X > TutorialWorld.FarmGate.X);
        // Pip's bench is in the farmhouse
        Assert.InRange(TutorialFarm.PipBench.X, house.Min(p => p.X), house.Max(p => p.X));

        // sparse: no corn or wheat; no two crops side by side in a row; the rows two or more apart
        int[] crops = [0x0C6A, 0x0C7B, 0x0C76, 0x0C55, 0x0C7D, 0x0C70, 0x0C6D];
        var planted = TutorialIsleFarmLayout.Pieces.Where(p => crops.Contains(p.Id)).Select(p => (p.X, p.Y)).ToHashSet();
        Assert.NotEmpty(planted);
        Assert.DoesNotContain(TutorialIsleFarmLayout.Pieces, p => p.Id is 0x0C55 or 0x0C7D);
        Assert.DoesNotContain(planted, c => planted.Contains((c.X + 1, c.Y)));
        Assert.DoesNotContain(planted, c => planted.Contains((c.X, c.Y + 1)));
        Assert.All(planted, c => Assert.True(TutorialFarm.PestField.Contains(new Point2D(c.X, c.Y)), $"a crop outside the field at {c}"));
    }

    [Fact]
    public void AFarmLaidBeforeTheMoveIsMovedInPlace()
    {
        var map = SeededCopy();
        try
        {
            var g = (Point2D p) => TutorialWorld.Ground(map, p);
            // the world as P726 left it: the gate and its fence piece in the old east fence, Pip's bench and blood in the
            // old farmhouse, the old spawners, and Wren, Tobin and Pip at their old spots
            var oldGate = new TutorialPestGate(DoorFacing.NorthCCW);
            oldGate.MoveToWorld(g(TutorialFarm.OldFieldGateSpot), map);
            var oldFence = new Static(TutorialFarm.FieldFenceId) { Name = "wooden fence" };
            oldFence.MoveToWorld(g(TutorialFarm.OldFieldFenceSpot), map);
            var oldProps = TutorialFarm.OldPipProps.Select(p =>
            {
                var s = new Static(p.Id) { Name = p.Name };
                s.MoveToWorld(p.At, map);
                return s;
            }).ToList();
            var oldPests = new Spawner(3, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), 0, default, TutorialFarm.PestTypes);
            oldPests.MoveToWorld(new Point3D(TutorialFarm.OldPestSpot.X, TutorialFarm.OldPestSpot.Y, 0), map);
            var oldPen = new Spawner(4, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), 0, default, TutorialFarm.PenTypes);
            oldPen.MoveToWorld(new Point3D(TutorialFarm.OldPenSpot.X, TutorialFarm.OldPenSpot.Y, 0), map);
            var wren = Assert.Single(Mobiles<TutorialFarmhand>(map));
            wren.MoveToWorld(g(new Point2D(6714, 3978)), map);
            var tobin = Assert.Single(Mobiles<TutorialVeterinarian>(map));
            tobin.MoveToWorld(g(new Point2D(6714, 3976)), map);
            var pip = Assert.Single(Mobiles<TutorialWoundedHelper>(map));
            pip.MoveToWorld(TutorialFarm.OldPipBench, map);

            TutorialCorePlacement.SeedOn(map, false, false);

            Assert.True(oldGate.Deleted);
            Assert.True(oldFence.Deleted);
            Assert.All(oldProps, s => Assert.True(s.Deleted, $"{s.Name} left at {s.Location}"));
            Assert.True(oldPests.Deleted);
            Assert.True(oldPen.Deleted);
            Assert.Same(wren, Assert.Single(Mobiles<TutorialFarmhand>(map)));
            Assert.Equal((TutorialFarm.FarmhandSpot.X, TutorialFarm.FarmhandSpot.Y), (wren.X, wren.Y));
            Assert.Same(tobin, Assert.Single(Mobiles<TutorialVeterinarian>(map)));
            Assert.Equal((TutorialFarm.FarmVetSpot.X, TutorialFarm.FarmVetSpot.Y), (tobin.X, tobin.Y));
            Assert.Same(pip, Assert.Single(Mobiles<TutorialWoundedHelper>(map)));
            Assert.Equal(TutorialFarm.PipBench, pip.Location);
            // the new farm's own pieces are all there, once
            Assert.Single(TutorialFarm.FieldGates(map));
            Assert.NotNull(TutorialFarm.FindFieldFence(map));
            Assert.NotNull(TutorialFarm.FindSpawner(map, TutorialFarm.PestSpot, TutorialFarm.PestTypes[0]));
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }

    [Fact]
    public void ALessonSignFromBeforeIsMovedNorthOfItsPathNotDoubled()
    {
        var map = SeededCopy();
        try
        {
            // P720's spots: the movement lesson's south of the road, the death lesson's inside the yard's gate
            var before = new Dictionary<string, Point2D> { ["core.movement"] = new(6646, 3994), ["core.death"] = new(6736, 3996) };
            foreach (var (key, at) in before)
            {
                var sign = TutorialCorePlacement.FindMarker(key, map)!;
                sign.MoveToWorld(TutorialWorld.Ground(map, at), map);
            }

            TutorialCorePlacement.SeedOn(map, false, false);

            var signs = Items<TutorialStationMarker>(map);
            Assert.Equal(TutorialStations.Core.Count(), signs.Count);
            foreach (var key in before.Keys)
            {
                var one = Assert.Single(signs, m => m.StationKey == key);
                Assert.Equal(TutorialCorePlacement.SignSpot(key).X, one.X);
                Assert.Equal(TutorialCorePlacement.SignSpot(key).Y, one.Y);
            }
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }

    [Fact]
    public void TheChatSignpostIsMovedNorthOfTheRoadNotDoubled()
    {
        var map = SeededCopy();
        try
        {
            bool IsPost(Item i) => i.ItemID == TutorialCorePlacement.SignpostId && i.Name == TutorialCorePlacement.ChatSignpostName;
            var post = Assert.Single(Items<Static>(map), IsPost);
            Assert.True(TutorialCorePlacement.ChatSignpost.Y < TutorialWorld.Signpost.Y - 2, "north of the road");
            post.MoveToWorld(TutorialWorld.Ground(map, TutorialCorePlacement.OldChatSignpostSpot), map); // laid before

            TutorialCorePlacement.SeedOn(map, false, false);

            var now = Assert.Single(Items<Static>(map), IsPost);
            Assert.Same(post, now);
            Assert.Equal(TutorialWorld.Ground(map, TutorialCorePlacement.ChatSignpost), now.Location);
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }
}
