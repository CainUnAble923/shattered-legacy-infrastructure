// SosMaps.cs
//
// cc-P675 (SoS swap S2): Felucca reads Trammel's files. The Siege of Sosaria map by Zigholtul replaces both Trammel and
// Felucca (cc-P673, Chase 10-26), and the player package draws it for both (cc-P674: map0LegacyMUL.uop is the patched
// map 1 under map 0's names, and Felucca's statics lines name Trammel's two files). The server side is this one change:
// Felucca keeps its index 0, client id 0, season, size, name and FeluccaRules, and reads file index 1, so its land and
// statics are Trammel's own (the SoS files plus trammel.mapops, from the map overlay that docker/uo/Build-MapOverlay.ps1
// builds and Start-TestShard.ps1 puts first in dataDirectories). Its TileMatrix is linked with Trammel's
// (TileMatrix.cs:71-86 links matrices of one file index), as the Fold's maps 32 to 48 are (FoldMaps.cs).
//
// In code, not a map-definitions.json patch: pinned builds the stock maps from that file in MapLoader.Configure
// (priority 2, MapLoader.cs:47), and a Map takes its file index only in its constructor (Map.cs:74-78, a private
// readonly m_FileIndex). Configure at priority 3, after it and before TileMatrixLoader, RegionJsonSerializer.LoadRegions
// and World.Load (Main.cs:667-671), sets that field on the Felucca map MapLoader made and drops its TileMatrix if one was
// already made (Map.Tiles builds it again on the next read, Map.cs:105). The Map object itself is kept, so nothing that
// already holds Map.Felucca, or keys a table by it (ClusterFSosSpawnPlacements), sees another map. If pinned ever
// renames those fields, Configure throws and the shard does not start: a Felucca on stock land under an SoS client
// would let players walk through walls the client draws. A second call changes nothing.

using System;
using System.Reflection;
using Server.Logging;

namespace Server;

public static class SosMaps
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(SosMaps));

    /// <summary>Felucca's index in Map.Maps and its client id: unchanged.</summary>
    public const int FeluccaIndex = 0;

    /// <summary>The file index Felucca reads: Trammel's, the SoS map.</summary>
    public const int SosFileIndex = 1;

    private static readonly FieldInfo FileIndexField = typeof(Map).GetField("m_FileIndex", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo TilesField = typeof(Map).GetField("m_Tiles", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>The file index <paramref name="map"/> reads its land and statics from.</summary>
    public static int FileIndexOf(Map map) => (int)FileIndexField!.GetValue(map)!;

    /// <summary>True once Felucca reads file index 1.</summary>
    public static bool FeluccaOnSos => Map.Felucca is { } f && FileIndexField != null && FileIndexOf(f) == SosFileIndex;

    [CallPriority(3)]
    public static void Configure()
    {
        var felucca = Map.Maps[FeluccaIndex];
        if (felucca == null || FeluccaOnSos)
        {
            return;
        }

        if (FileIndexField?.FieldType != typeof(int) || TilesField?.FieldType != typeof(TileMatrix))
        {
            throw new InvalidOperationException(
                "SosMaps: pinned's Map no longer has m_FileIndex and m_Tiles (Map.cs); Felucca cannot be pointed at the Siege of Sosaria map. Not starting."
            );
        }

        FileIndexField.SetValue(felucca, SosFileIndex);
        TilesField.SetValue(felucca, null);
        logger.Information("Felucca reads file index {FileIndex}, Trammel's map (the Siege of Sosaria swap)", SosFileIndex);
    }
}
