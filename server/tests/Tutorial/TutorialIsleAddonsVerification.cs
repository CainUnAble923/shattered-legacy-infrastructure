// cc-P724: the isle's addons from the new downloads (the wreck, the lighthouse, the moongate frame, the fountain). Notes:
// shard-migration notes/cc-P724-isle-addons.md.
//
// Facts, each seen failing first:
//   1. Seeding a copy places each addon once, at its spot, and stands the gate on the frame's dais; seeding again places
//      nothing more (P728's load top-up runs the same seeding every start).
//   2. No addon covers a lesson's stop, sign, NPC, door or prop, the walk tiles of the bot route, or the road to the dock;
//      only the frame covers the gate's own tile, by design.
//   3. A copy's reset removes them with the rest of its seeding.

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
public class TutorialIsleAddonsVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;

    public TutorialIsleAddonsVerification()
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

    private static List<T> On<T>(Map map) where T : Item =>
        World.Items.Values.OfType<T>().Where(i => !i.Deleted && i.Map == map && i.Parent == null).ToList();

    [Fact]
    public void SeedingPlacesEachAddonOnceAndStandsTheGateOnTheFrame()
    {
        var map = TutorialCopies.MapOf(LastSlot);
        TutorialCopies.Reset(LastSlot);
        try
        {
            TutorialCorePlacement.SeedOn(map, false, false);

            var wreck = Assert.Single(On<WakeningIsleShipwreckAddon>(map));
            Assert.Equal(TutorialIsleAddons.WreckSpot, wreck.Location);
            Assert.Equal(TutorialIsleAddons.LighthouseSpot, Assert.Single(On<WakeningIsleLighthouseAddon>(map)).Location);
            Assert.Equal(TutorialIsleAddons.FountainSpot, Assert.Single(On<WakeningIsleBrassFountain>(map)).Location);
            var frame = Assert.Single(On<WakeningIsleMoongateFrameAddon>(map));
            Assert.Equal(TutorialIsleAddons.FrameSpot(map), frame.Location);
            Assert.Equal(WakeningIsleShipwreckAddon.Pieces.Length, wreck.Components.Count);
            Assert.Equal(
                WakeningIsleMoongateFrameAddon.Pieces.Length + WakeningIsleMoongateFrameAddon.Lights.Length,
                frame.Components.Count
            );

            var gate = Assert.Single(On<TutorialTravelGate>(map));
            Assert.Equal(frame.Z + TutorialIsleAddons.DaisHeight, gate.Z);
            Assert.Equal(TutorialWorld.Moongate.X, gate.X);
            Assert.Equal(TutorialWorld.Moongate.Y, gate.Y);

            // Again (every start's top-up): nothing more.
            var count = World.Items.Values.Count(i => !i.Deleted && i.Map == map);
            var line = TutorialCorePlacement.SeedOn(map, false, false);
            Assert.Contains("Isle addons: placed 0", line);
            Assert.Equal(count, World.Items.Values.Count(i => !i.Deleted && i.Map == map));
            Assert.Equal(frame.Z + TutorialIsleAddons.DaisHeight, gate.Z);
        }
        finally
        {
            TutorialCopies.Reset(LastSlot);
        }
    }

    [Fact]
    public void NoAddonCoversALessonOrThePathsBetweenThem()
    {
        var kept = new HashSet<Point2D>
        {
            TutorialWorld.Beach, TutorialWorld.Guide, TutorialWorld.BlueTile, TutorialWorld.GoldTile, TutorialWorld.Camp,
            TutorialWorld.Rack, TutorialWorld.Signpost, TutorialWorld.FarmGate, TutorialWorld.Yard, TutorialWorld.Echo,
            TutorialWorld.Healer, TutorialWorld.GhostSpot, TutorialWorld.BankStop, TutorialWorld.BankDoor,
            TutorialWorld.Banker, TutorialWorld.BankBox, TutorialWorld.LodgeStop, TutorialWorld.LodgeDoor,
            TutorialWorld.Elara, TutorialWorld.Stones, TutorialWorld.DockEnd, TutorialCorePlacement.ClothesTable,
            TutorialCorePlacement.ChatSignpost, TutorialCorePlacement.LeagueBoard, TutorialCorePlacement.HealingSign,
            new(TutorialFarm.PipBench.X, TutorialFarm.PipBench.Y)
        };
        foreach (var station in TutorialStations.Core)
        {
            kept.Add(TutorialCorePlacement.SignPoint(station.Key));
            var stop = TutorialCorePlacement.MarkerSpot(station.Key);
            kept.Add(new Point2D(stop.X, stop.Y));
        }

        foreach (var (_, at, _) in TutorialCorePlacement.Props)
        {
            kept.Add(at);
        }

        // The bot route's walk: the bank's doorstep round its sign, and the road from the stones out along the dock.
        kept.Add(new Point2D(6763, 3981));
        for (var x = 6800; x <= 6831; x++)
        {
            kept.Add(new Point2D(x, 3991));
        }

        var addons = new (string Name, Point3D Spot, (int Id, int X, int Y, int Z)[] Components)[]
        {
            ("wreck", TutorialIsleAddons.WreckSpot, WakeningIsleShipwreckAddon.Pieces),
            ("lighthouse", TutorialIsleAddons.LighthouseSpot, WakeningIsleLighthouseAddon.Pieces),
            ("fountain", TutorialIsleAddons.FountainSpot, [(0xA62B, 0, 0, 0)]),
            ("frame", new Point3D(TutorialWorld.Moongate.X, TutorialWorld.Moongate.Y, 0), WakeningIsleMoongateFrameAddon.Pieces)
        };
        var gate = TutorialWorld.Moongate;
        foreach (var (name, spot, components) in addons)
        {
            foreach (var tile in TutorialIsleAddons.Footprint(spot, components))
            {
                // The frame stands round the gate and over the landing's last tiles, as Chase asked (the dock leads into it).
                if (name == "frame" && Math.Abs(tile.X - gate.X) <= 3 && Math.Abs(tile.Y - gate.Y) <= 3)
                {
                    continue;
                }

                Assert.False(kept.Contains(tile), $"the {name} covers {tile}");
            }

            // And nothing of it on the bank's floor or the bank door's line north of the road.
            Assert.False(TutorialCorePlacement.BankFloor.Contains(new Point2D(spot.X, spot.Y)), $"the {name} is in the bank");
        }

        Assert.NotEqual(TutorialWorld.BankDoor.X, TutorialIsleAddons.FountainSpot.X);
    }

    [Fact]
    public void ACopysResetRemovesTheAddons()
    {
        var map = TutorialCopies.MapOf(LastSlot);
        TutorialCopies.Reset(LastSlot);
        TutorialCorePlacement.SeedOn(map, false, false);
        Assert.NotEmpty(On<WakeningIsleMoongateFrameAddon>(map));

        TutorialCopies.Reset(LastSlot);

        Assert.Empty(On<WakeningIsleShipwreckAddon>(map));
        Assert.Empty(On<WakeningIsleLighthouseAddon>(map));
        Assert.Empty(On<WakeningIsleMoongateFrameAddon>(map));
        Assert.Empty(On<WakeningIsleBrassFountain>(map));
        Assert.Empty(On<AddonComponent>(map));
    }
}
