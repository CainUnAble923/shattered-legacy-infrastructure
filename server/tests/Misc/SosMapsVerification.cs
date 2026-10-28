// SosMapsVerification.cs
//
// cc-P675 (SoS swap S2): the server reads the Siege of Sosaria map for Trammel, Felucca and the Fold
// (server/customizations/Maps/SosMaps.cs; the map itself is the overlay docker/uo/Build-MapOverlay.ps1 builds). Notes:
// shard-migration notes/cc-P675-sos-server-map.md.
//
// Facts:
//   1. Felucca reads file index 1 (Trammel's) and keeps everything else: index 0, client id 0, its name, size, season
//      and FeluccaRules, one entry in Map.AllMaps, found by name. A second Configure changes nothing. Trammel is as it was.
//   2. (Real client data only, with the server's map overlay mounted over it; skips without tile data, run by hand as
//      cc-P135 section 5 does.) At Valunstead, a player on Trammel, on Felucca and on map 32 stands on SoS's land tile
//      and Z (grass 0x0003 at Z 15 at 504,384, where stock Trammel is sea at -5), and the hall at 479,388 has SoS's
//      floors and roof on all three (read independently by shard-migration notes/cc-P675-tools/spots.py).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Mobiles;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosMapsVerification
{
    private readonly ITestOutputHelper _out;

    public SosMapsVerification(ITestOutputHelper output)
    {
        _out = output;
        FoldMaps.Configure();
        SosMaps.Configure();
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

    [Fact]
    public void FeluccaReadsTrammelsFileIndexAndKeepsItsOwnRules()
    {
        var felucca = Map.Felucca;
        Assert.NotNull(felucca);
        Assert.Same(Map.Maps[0], felucca);
        Assert.Equal(1, FileIndexOf(felucca));
        Assert.Equal(0, felucca.MapIndex);
        Assert.Equal(0, felucca.MapID);
        Assert.Equal("Felucca", felucca.Name);
        Assert.Equal(7168, felucca.Width);
        Assert.Equal(4096, felucca.Height);
        Assert.Equal(4, felucca.Season); // pinned's map-definitions.json and the test host's TestMapDefinitions
        Assert.Equal(MapRules.FeluccaRules, felucca.Rules);
        Assert.Single(Map.AllMaps, m => m.MapIndex == 0);
        Assert.Contains(felucca, Map.AllMaps);
        Assert.Same(felucca, Map.Parse("Felucca"));
        Assert.True(SosMaps.FeluccaOnSos);

        Assert.Equal(1, FileIndexOf(Map.Trammel));
        Assert.Equal(1, Map.Trammel.MapID);
        Assert.Equal(MapRules.TrammelRules, Map.Trammel.Rules);

        var count = Map.AllMaps.Count;
        SosMaps.Configure();
        Assert.Same(felucca, Map.Felucca);
        Assert.Equal(count, Map.AllMaps.Count);
    }

    [SkippableFact]
    public void APlayerOnTrammelFeluccaAndTheFoldAtValunsteadStandsOnSosTileAndZ()
    {
        TileDataRequirement.SkipIfMissing();

        foreach (var map in new[] { Map.Trammel, Map.Felucca, Map.Maps[32] })
        {
            var z = map.GetAverageZ(504, 384);
            var pm = new PlayerMobile { Player = true, AccessLevel = AccessLevel.Player, Name = "Walker" };
            pm.MoveToWorld(new Point3D(504, 384, z), map);
            try
            {
                var land = map.Tiles.GetLandTile(504, 384);
                var hall = map.Tiles.GetLandTile(479, 388);
                var floors = StaticsAt(map, 479, 388);
                _out.WriteLine($"{map.Name}: 504,384 land 0x{land.ID:X4} Z {land.Z}, stands at Z {pm.Z}; 479,388 land 0x{hall.ID:X4} Z {hall.Z}, statics {string.Join(" ", floors)}");

                Assert.Same(map, pm.Map);
                Assert.Equal(0x0003, land.ID);
                Assert.Equal(15, land.Z);
                Assert.Equal(15, pm.Z);
                Assert.Empty(StaticsAt(map, 504, 384));
                Assert.Equal(0x0006, hall.ID);
                Assert.Equal(15, hall.Z);
                Assert.Equal(new[] { "0x04AB@20", "0x04AB@40", "0x0596@49" }, floors);
            }
            finally
            {
                pm.Delete();
            }
        }
    }
}
