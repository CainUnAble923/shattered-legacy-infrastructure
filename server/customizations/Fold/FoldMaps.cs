// FoldMaps.cs
//
// cc-P661 (P481 F1): the Fold, a separate server world on Trammel's tiles, and the pool of tutorial copies beside it.
// Design: shard-migration notes/cc-P481-new-player-start-area-design.md sections 3.1, 11 and 12; P605 risk 8.
// Notes: shard-migration notes/cc-P661-fold-map-32.md.
//
//   index 32       "TheFold"        the Fold itself (the Wakening Isle is painted into Trammel's void C, F2)
//   index 33-48    "TheFold 1-16"   the tutorial copies, one per trainee or party (F4 hands them out)
//
// Every one is client id 1 and file index 1: the client is told map 1 (OutgoingMapPackets.cs:62, (byte)map.MapID) and
// draws Trammel's own files, and the server reads the same three files (TileMatrix.cs:71-86 links matrices that share
// a file index so cached blocks are reused). Width, height and season 0 are Trammel's; the rules are TrammelRules.
//
// INDEXES 32 TO 48 ARE RESERVED FOR GOOD. A save stores a map as its index byte (BufferWriter.cs:468) and reads it back
// through Map.Maps[index] (IGenericReader.cs:120), so once a save can name one of these, giving the index to another
// world would move whatever stood there into that world. Never reuse, renumber or unregister them; grow the pool
// upward from 49 if it is ever full. FoldSavedMapCheck reports anything a save names that is not registered.
//
// When: Configure at priority 3, after pinned's MapLoader.Configure (priority 2, MapLoader.cs:47) has built the stock
// maps from map-definitions.json and before TileMatrixLoader, RegionJsonSerializer.LoadRegions and World.Load
// (Main.cs:667-671). CallPriorityComparer sorts ascending and unmarked methods are 50 (Attributes.cs:40-60), so an
// explicit 3 does not depend on equal-priority ordering. Pinned's reserved indexes are 0-31, 127 and 255
// (MapLoader.cs:31-35); this registers no index below 32. Registering is cheap: a map opens its files only when its
// tiles are first read (Map.Tiles is lazy, Map.cs:105).

using Server.Logging;
using Server.Regions;

namespace Server;

public static class FoldMaps
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(FoldMaps));

    /// <summary>The Fold itself. Reserved for good (see the file header).</summary>
    public const int FoldIndex = 32;

    /// <summary>The first tutorial copy, "TheFold 1".</summary>
    public const int FirstCopyIndex = 33;

    /// <summary>How many tutorial copies there are: 33 to 48.</summary>
    public const int CopyCount = 16;

    /// <summary>The last index in use, 48. Reserved for good, as are all below it from 32.</summary>
    public const int LastIndex = FirstCopyIndex + CopyCount - 1;

    /// <summary>The client map and the file index the Fold draws: Trammel's.</summary>
    public const int TrammelId = 1;

    public const string FoldName = "TheFold";

    /// <summary>The Fold, map 32, or null before Configure.</summary>
    public static Map Fold => Map.Maps[FoldIndex];

    /// <summary>"TheFold" for 32, "TheFold n" for copy n (33 is "TheFold 1").</summary>
    public static string NameOf(int index) => index == FoldIndex ? FoldName : $"{FoldName} {index - FoldIndex}";

    /// <summary>Copy <paramref name="n"/> (1 to 16), or the Fold for 0; null outside that.</summary>
    public static Map Copy(int n) => n is >= 0 and <= CopyCount ? Map.Maps[FoldIndex + n] : null;

    /// <summary>True for the Fold and every tutorial copy.</summary>
    public static bool IsFold(Map map) => map != null && map.MapIndex is >= FoldIndex and <= LastIndex;

    [CallPriority(3)]
    public static void Configure()
    {
        var trammel = Map.Trammel;
        var width = trammel?.Width ?? 7168;
        var height = trammel?.Height ?? 4096;
        var made = 0;

        for (var index = FoldIndex; index <= LastIndex; index++)
        {
            var name = NameOf(index);
            var existing = Map.Maps[index];

            if (existing != null)
            {
                if (existing.Name == name && existing.MapID == TrammelId)
                {
                    existing.DefaultRegion = FoldRegion.On(existing) ?? new FoldRegion(existing);
                    continue; // already registered (a second call, or a test that registered first)
                }

                // A map-definitions.json entry has taken a reserved index. Refuse rather than move its contents.
                logger.Error(
                    "The Fold: map index {Index} is already {Name}; {Wanted} was not registered. Indexes 32-48 are the Fold's",
                    index,
                    existing.Name,
                    name
                );
                continue;
            }

            var map = new Map(TrammelId, index, TrammelId, width, height, 0, name, MapRules.TrammelRules);
            map.DefaultRegion = new FoldRegion(map); // the Fold's rules everywhere on it (FoldRegions.cs)
            Map.Maps[index] = map;
            Map.AllMaps.Add(map);
            made++;
        }

        if (made > 0)
        {
            logger.Information("The Fold: registered {Count} maps ({First}-{Last}) on Trammel's files", made, FoldIndex, LastIndex);
        }
    }
}
