// FoldVerification.cs
//
// cc-P661 (P481 F1): the Fold, server map 32 on Trammel's tiles, and its 16 tutorial copies, maps 33 to 48
// (server/customizations/Fold/). Notes: shard-migration notes/cc-P661-fold-map-32.md.
//
// Facts:
//   1. Maps 32 to 48 are registered in Map.Maps and Map.AllMaps as "TheFold" and "TheFold 1" to "TheFold 16": client id 1,
//      file index 1, Trammel's size, season 0, TrammelRules. A second Configure adds nothing.
//   2. (Real client data only; skips without it, run by hand with the client mounted.) A player moved to 32 and to 40
//      stands on Trammel's land tile and Z at that spot, and the same statics are there.
//   3. On 32 and on 40: housing, recall, mark and gate (in and out) are refused for a player and allowed for staff, the
//      region covers the whole map, and the light is always day.
//   4. A player found on Trammel over void C is sent to the New Haven start; staff are left there; travel into it is
//      refused for a player.
//   5. A save and reload keeps a character on map 40: back from the save, the way back is map 40 at the same spot, and
//      it puts them on 40 again inside the Fold's rules.
//   6. A save that names an unregistered index loads without an exception, and the check names the character (sending
//      their way back to New Haven) and the item left without a map.
//   7. [Fold goto: test shard only; n 0 to 16; the arrival beach by default, or x y.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.CharacterCreation;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class FoldVerification
{
    private readonly ITestOutputHelper _out;

    public FoldVerification(ITestOutputHelper output)
    {
        _out = output;
        FoldMaps.Configure();
        FoldMirrorRegion.Initialize();
    }

    private static readonly Point3D Spot = new(6214, 2001, 0);          // the arrival beach, in void C
    private static readonly Point3D Elsewhere = new(1220, 1220, 0);     // ordinary Trammel, as YoungPlayerVerification
    private static readonly Point3D InVoidC = new(6300, 2000, 0);

    private static Map Map40 => Map.Maps[40];

    private static PlayerMobile Player(Point3D at, Map map, AccessLevel level = AccessLevel.Player)
    {
        var pm = new PlayerMobile { Player = true, AccessLevel = level, Name = level == AccessLevel.Player ? "Trainee" : "Staff" };
        pm.MoveToWorld(at, map);
        return pm;
    }

    private static int FileIndexOf(Map map) =>
        (int)typeof(Map).GetField("m_FileIndex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(map)!;

    private static List<string> StaticsAt(Map map, int x, int y)
    {
        var list = new List<string>();
        foreach (var s in map.Tiles.GetStaticTiles(x, y))
        {
            list.Add($"0x{s.ID:X4}@{s.Z}");
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void MapsThirtyTwoToFortyEightAreRegisteredOnTrammelsFiles()
    {
        var trammel = Map.Trammel;
        for (var index = 32; index <= 48; index++)
        {
            var map = Map.Maps[index];
            Assert.NotNull(map);
            Assert.Equal(index == 32 ? "TheFold" : $"TheFold {index - 32}", map.Name);
            Assert.Equal(index, map.MapIndex);
            Assert.Equal(1, map.MapID);
            Assert.Equal(1, FileIndexOf(map));
            Assert.Equal(trammel.Width, map.Width);
            Assert.Equal(trammel.Height, map.Height);
            Assert.Equal(0, map.Season);
            Assert.Equal(MapRules.TrammelRules, map.Rules);
            Assert.Single(Map.AllMaps, m => m == map);
            Assert.True(FoldMaps.IsFold(map));
        }

        Assert.Same(Map.Maps[32], FoldMaps.Fold);
        Assert.Same(Map.Maps[40], FoldMaps.Copy(8));
        Assert.Null(Map.Maps[49]);
        Assert.False(FoldMaps.IsFold(Map.Trammel));
        Assert.Equal("TheFold 16", Map.Parse("TheFold 16")?.Name);

        var count = Map.AllMaps.Count;
        FoldMaps.Configure();
        Assert.Equal(count, Map.AllMaps.Count);
    }

    // ---------------------------------------------------------------- 2

    [SkippableFact]
    public void APlayerOn32And40StandsOnTrammelsTileAndZ()
    {
        TileDataRequirement.SkipIfMissing();

        // New Haven: land 0x03E9 at Z 14, statics 0x0B24 and 0x0D33 on the tile north (cc-P135 5.1, cc-P134 section 4).
        foreach (var (x, y) in new[] { (3503, 2574), (3503, 2573), (6214, 2001) })
        {
            var land = Map.Trammel.Tiles.GetLandTile(x, y);
            var statics = StaticsAt(Map.Trammel, x, y);
            var z = Map.Trammel.GetAverageZ(x, y);

            foreach (var map in new[] { Map.Maps[32], Map40 })
            {
                var pm = Player(new Point3D(x, y, map.GetAverageZ(x, y)), map);
                try
                {
                    var theirLand = map.Tiles.GetLandTile(x, y);
                    var theirStatics = StaticsAt(map, x, y);
                    _out.WriteLine($"{map.Name} {x},{y}: land 0x{theirLand.ID:X4} Z {theirLand.Z}, stands at Z {pm.Z}, statics {string.Join(" ", theirStatics)}");

                    Assert.Same(map, pm.Map);
                    Assert.Equal(land.ID, theirLand.ID);
                    Assert.Equal(land.Z, theirLand.Z);
                    Assert.Equal(z, pm.Z);
                    Assert.Equal(statics, theirStatics);
                }
                finally
                {
                    pm.Delete();
                }
            }
        }

        Assert.Equal(0x03E9, Map.Maps[32].Tiles.GetLandTile(3503, 2574).ID);
        Assert.Equal(14, Map40.Tiles.GetLandTile(3503, 2574).Z);
        Assert.Equal(0x0244, Map40.Tiles.GetLandTile(6214, 2001).ID); // void C is still void until F2 paints the isle
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void HousingRecallMarkAndGateAreRefusedOnTheFold()
    {
        foreach (var map in new[] { Map.Maps[32], Map40 })
        {
            var trainee = Player(Spot, map);
            var staff = Player(Spot, map, AccessLevel.GameMaster);
            var outside = Player(Elsewhere, Map.Trammel);
            try
            {
                var region = trainee.Region;
                Assert.IsType<FoldRegion>(region);
                Assert.IsType<FoldRegion>(Region.Find(new Point3D(0, 0, 0), map));
                Assert.IsType<FoldRegion>(Region.Find(new Point3D(map.Width - 1, map.Height - 1, 0), map));
                Assert.Same(region, FoldRegion.On(map));
                Assert.Same(map.DefaultRegion, region);

                // Housing: the region type HousePlacement refuses by; staff place anywhere.
                Assert.Equal(HousePlacementResult.BadRegion, HousePlacement.Check(trainee, 0x64, Spot, out _));
                Assert.Equal(HousePlacementResult.Valid, HousePlacement.Check(staff, 0x64, Spot, out _));

                var cases = new (string Name, Mobile Who, Map To, Point3D At, TravelCheckType Type)[]
                {
                    ("recall in", outside, map, Spot, TravelCheckType.RecallTo),
                    ("gate in", outside, map, Spot, TravelCheckType.GateTo),
                    ("teleport in", outside, map, Spot, TravelCheckType.TeleportTo),
                    ("recall out", trainee, Map.Trammel, Elsewhere, TravelCheckType.RecallFrom),
                    ("gate out", trainee, Map.Trammel, Elsewhere, TravelCheckType.GateFrom),
                    ("mark", trainee, map, Spot, TravelCheckType.Mark)
                };
                foreach (var (name, who, to, at, type) in cases)
                {
                    var ok = SpellHelper.CheckTravel(who, to, at, type, out var message);
                    _out.WriteLine($"{map.Name} {name}: {ok} \"{message?.String ?? message?.Number.ToString()}\"");
                    Assert.False(ok, $"{map.Name} {name}");
                }

                Assert.True(SpellHelper.CheckTravel(staff, Map.Trammel, Elsewhere, TravelCheckType.RecallFrom, out _));
                Assert.True(SpellHelper.CheckTravel(staff, map, Spot, TravelCheckType.Mark, out _));

                int global = 26, personal = 0;
                region.AlterLightLevel(trainee, ref global, ref personal);
                Assert.Equal(LightCycle.DayLevel, global);
            }
            finally
            {
                trainee.Delete();
                staff.Delete();
                outside.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void APlayerFoundOnTrammelsMirrorIsSentToNewHaven()
    {
        ShardTestClock.Arm();
        var outside = Player(Elsewhere, Map.Trammel);
        var trainee = Player(InVoidC, Map.Trammel);
        var staff = Player(InVoidC, Map.Trammel, AccessLevel.GameMaster);
        try
        {
            Assert.IsType<FoldMirrorRegion>(Region.Find(new Point3D(6160, 1832, 0), Map.Trammel));
            Assert.IsType<FoldMirrorRegion>(Region.Find(new Point3D(6639, 2279, 0), Map.Trammel));
            Assert.IsNotType<FoldMirrorRegion>(Region.Find(new Point3D(6159, 2000, 0), Map.Trammel));
            Assert.IsNotType<FoldMirrorRegion>(Region.Find(new Point3D(6640, 2000, 0), Map.Trammel));
            Assert.IsNotType<FoldMirrorRegion>(Region.Find(InVoidC, Map.Maps[32]));
            Assert.False(SpellHelper.CheckTravel(outside, Map.Trammel, InVoidC, TravelCheckType.RecallTo, out _));

            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));

            // cc-P663: the New Haven start (HavenSpots); the start list's first entry is the isle's now.
            Assert.Same(Map.Trammel, trainee.Map);
            Assert.Equal(Server.Tutorial.HavenSpots.Start, trainee.Location);
            Assert.Equal(InVoidC, staff.Location);
        }
        finally
        {
            outside.Delete();
            trainee.Delete();
            staff.Delete();
        }
    }

    // ---------------------------------------------------------------- 5, 6

    private static byte[] Save(ISerializable entity)
    {
        var writer = new BufferWriter(new byte[1 << 16], true);
        entity.Serialize(writer);
        writer.Flush();
        return writer.Buffer[..(int)writer.Position];
    }

    private static PlayerMobile LoadPlayer(byte[] saved)
    {
        var copy = new PlayerMobile(World.NewMobile);
        World.AddEntity(copy);
        copy.Deserialize(new BufferReader(saved));
        return copy;
    }

    [Fact]
    public void ASaveAndReloadKeepsACharacterOnMapForty()
    {
        var at = new Point3D(6220, 2000, 0);
        var trainee = Player(at, Map40);
        PlayerMobile copy = null;
        try
        {
            var saved = Save(trainee);
            copy = LoadPlayer(saved);

            // Loading parks every player on Internal with the way back saved (Mobile.cs:6511-6517); login takes it.
            Assert.Same(Map40, copy.LogoutMap);
            Assert.Equal(at, copy.LogoutLocation);

            copy.MoveToWorld(copy.LogoutLocation, copy.LogoutMap);
            Assert.Equal(40, copy.Map.MapIndex);
            Assert.Equal(at, copy.Location);
            Assert.IsType<FoldRegion>(copy.Region);
        }
        finally
        {
            trainee.Delete();
            copy?.Delete();
        }
    }

    [Fact]
    public void AnUnregisteredIndexInASaveIsReportedNotCrashedOn()
    {
        var at = new Point3D(6220, 2000, 0);
        var trainee = Player(at, Map40);
        var dropped = new Item(0x0EED);
        dropped.MoveToWorld(at, Map40);
        PlayerMobile copy = null;
        Item itemCopy = null;

        var savedPlayer = Save(trainee);
        var savedItem = Save(dropped);
        var map40 = Map40;
        var allIndex = Map.AllMaps.IndexOf(map40);
        try
        {
            // As if a later build no longer registered 40.
            Map.Maps[40] = null;
            Map.AllMaps.Remove(map40);

            copy = LoadPlayer(savedPlayer);
            itemCopy = new Item(World.NewItem);
            World.AddEntity(itemCopy);
            itemCopy.Deserialize(new BufferReader(savedItem));

            Assert.Same(Map.Internal, copy.Map);
            Assert.Null(copy.LogoutMap);
            Assert.Null(itemCopy.Map);

            var report = FoldSavedMapCheck.Run();
            foreach (var line in report)
            {
                _out.WriteLine(line);
            }

            var (home, homeMap) = FoldMirrorRegion.Home;
            Assert.Contains(report, l => l.Contains(copy.Serial.ToString()) && l.Contains("New Haven"));
            Assert.Contains(report, l => l.Contains(itemCopy.Serial.ToString()));
            Assert.Same(homeMap, copy.LogoutMap);
            Assert.Equal(home, copy.LogoutLocation);
            Assert.False(itemCopy.Deleted);
        }
        finally
        {
            Map.Maps[40] = map40;
            if (!Map.AllMaps.Contains(map40))
            {
                Map.AllMaps.Insert(Math.Max(0, Math.Min(allIndex, Map.AllMaps.Count)), map40);
            }
            trainee.Delete();
            dropped.Delete();
            copy?.Delete();
            itemCopy?.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void FoldGotoIsForStaffOnTheTestShardOnly()
    {
        var gm = Player(Elsewhere, Map.Trammel, AccessLevel.GameMaster);
        try
        {
            Assert.Equal(FoldCommands.LiveRefusal, FoldCommands.Goto(gm, ["goto", "1"], false));
            Assert.Same(Map.Trammel, gm.Map);

            Assert.Equal(FoldCommands.Usage, FoldCommands.Goto(gm, ["goto", "17"], true));
            Assert.Equal(FoldCommands.Usage, FoldCommands.Goto(gm, ["goto", "-1"], true));
            Assert.Equal(FoldCommands.Usage, FoldCommands.Goto(gm, ["go", "1"], true));
            Assert.Same(Map.Trammel, gm.Map);

            var line = FoldCommands.Goto(gm, ["goto", "1"], true);
            _out.WriteLine(line);
            Assert.Equal(33, gm.Map.MapIndex);
            Assert.Equal(new Point2D(6214, 2001), new Point2D(gm.X, gm.Y));

            FoldCommands.Goto(gm, ["goto", "0"], true);
            Assert.Equal(32, gm.Map.MapIndex);

            FoldCommands.Goto(gm, ["goto", "8", "6300", "1990"], true);
            Assert.Equal(40, gm.Map.MapIndex);
            Assert.Equal(new Point2D(6300, 1990), new Point2D(gm.X, gm.Y));

            Assert.Contains("outside", FoldCommands.Goto(gm, ["goto", "8", "9000", "10"], true));
            Assert.Equal(40, gm.Map.MapIndex);
        }
        finally
        {
            gm.Delete();
        }
    }
}
