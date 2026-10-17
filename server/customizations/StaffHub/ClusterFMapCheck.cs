// ClusterFMapCheck.cs
//
// cc-P135. Read-only map checks for staff over ModernUO's own loaded map, statics and tiledata; nothing here reads a
// client file. Adapted from vseUOAware by ghostbyte420, GPL-3.0 (vseUtilities commit e4b1d243, D:\UO\third-party\
// code\vseutilities, vseProjects\vseUOAware\vseUOAware_09272026_Latest\vseUOAware\Tools\MapPlacementTools.cs,
// MapStaticsTools.cs, WaterTools.cs and SnippetTools.cs). The rules are his; the readers are ModernUO's. This file is
// GPL-3.0, as ModernUO is. Notes: shard-migration/notes/cc-P135-vseuoaware-staff-commands.md.
//
// Pinned ModernUO (d4531cd94) behind IClusterFTileSource's one real implementation, ClusterFMapTileSource:
//   Map.Tiles                                Projects/Server/Maps/Map.cs:105 (the TileMatrix)
//   TileMatrix.GetLandTile(x, y)             Projects/Server/TileMatrix/TileMatrix.cs:341; LandTile ID and Z, :527-551
//   TileMatrix.GetStaticTiles(x, y)          TileMatrix.cs:271: the map file's statics, no multi tiles, as the original
//                                            read staticsN.mul; StaticTile ID, Z and Hue, :554-600
//   TileData.LandTable[id & MaxLandValue]    Projects/Server/TileData.cs:288, read as Map.CanFit reads it (Map.cs:944)
//   TileData.ItemTable[id & MaxItemValue]    TileData.cs:289, as Map.CanFit reads it (Map.cs:960); ItemData.Name,
//                                            Flags and Height, :59-105
//   TileFlag.Impassable, Wet, Surface        TileData.cs:225-228
//   Map.Width and Map.Height                 Map.cs:111-113
// The original indexed tiledata items at 0x4000 + id (the file's layout, TileDataReader.GetItemDetail); ModernUO's
// ItemTable is indexed by the item id itself (TileData.cs:369), so the checks use the id as it is.
//
// What each check says (the original's rules, kept):
//   blocked tile     land Wet -> water; land Impassable -> impassable land; otherwise a static whose Z is within
//                    StandHeight (16) above the land (or above the spot's own Z when one is given) and whose tiledata
//                    is Impassable and not Surface -> blocked by that static. A heuristic over map data, not
//                    Map.CanFit: items, mobiles and multis are not read, which is what a placement check wants.
//   footprint        every tile on the map, none blocked, and the land Z spread (max minus min) within maxZSpread.
//   static problems  exact duplicates (the same id and Z at one x,y), statics 10 or more Z below the land, and ids with
//                    no tiledata (an empty entry: no name, no flags, no height; ModernUO's table has a slot for every
//                    16-bit id, TileData.cs:289 and :381-382, so "past the table" cannot happen here).
//   walkable         the blocked rule for every tile, counted by cause.
//   shore            water (Wet land, or a non-Wet land tile under a Wet static: a shallow bed) that touches non-water
//                    land among its 8 neighbours; coast heights in buckets; the only fault is a water surface outside
//                    -120..120.
//   export           ModernUO's own JSON for a point list or a rectangle: "location": [x, y, z] and "map": "Trammel" as
//                    the spawn files carry them (UOContent/Engines/Spawners/Json/Point3DArrayConverter.cs:18-27,
//                    Server/Json/Converters/MapConverter.cs:32-33), and "spawnBounds": {x1, y1, x2, y2} as pinned
//                    writes a Rectangle3D (Server/Json/Converters/Rectangle3DConverter.cs:176-194; x2 and y2 are
//                    exclusive, Rectangle3D's End).
//
// The facts (server/tests/Misc/MapCheckVerification.cs) run every check over a hand-built IClusterFTileSource, because
// the test gate's host loads no client files (pinned UOContent.Tests/Fixtures/TestServerInitializer.cs:34-39); the
// real adapter is checked against known New Haven tiles when tile data is present (MapCheckRealDataVerification.cs).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ModernUO.Serialization;
using Server.Items;

namespace Server;

/// <summary>Land, statics and tiledata flags for one map, as the checks read them.</summary>
public interface IClusterFTileSource
{
    string Name { get; }
    int Width { get; }
    int Height { get; }
    LandTile GetLandTile(int x, int y);

    /// <summary>The map file's statics at x,y (no multis, no items).</summary>
    StaticTile[] GetStaticTiles(int x, int y);

    TileFlag LandFlags(int landId);
    TileFlag ItemFlags(int itemId);

    /// <summary>Whether tiledata has anything for this item id (a name, a flag or a height).</summary>
    bool HasItemData(int itemId);
}

/// <summary>The real thing: a Map's TileMatrix and the TileData tables (file:line in the header).</summary>
public sealed class ClusterFMapTileSource : IClusterFTileSource
{
    public ClusterFMapTileSource(Map map) => Map = map ?? throw new ArgumentNullException(nameof(map));

    public Map Map { get; }
    public string Name => Map.Name;
    public int Width => Map.Width;
    public int Height => Map.Height;

    public LandTile GetLandTile(int x, int y) => Map.Tiles.GetLandTile(x, y);

    public StaticTile[] GetStaticTiles(int x, int y)
    {
        var list = new List<StaticTile>();
        foreach (var t in Map.Tiles.GetStaticTiles(x, y))
        {
            list.Add(t);
        }

        return list.ToArray();
    }

    public TileFlag LandFlags(int landId) => TileData.LandTable[landId & TileData.MaxLandValue].Flags;

    public TileFlag ItemFlags(int itemId) => TileData.ItemTable[itemId & TileData.MaxItemValue].Flags;

    public bool HasItemData(int itemId)
    {
        if (itemId < 0 || itemId > TileData.MaxItemValue)
        {
            return false;
        }

        var d = TileData.ItemTable[itemId];
        return !string.IsNullOrEmpty(d.Name) || d.Flags != 0 || d.Height != 0;
    }

    /// <summary>A source for a map by its name or number, null for an unknown one or Internal.</summary>
    public static IClusterFTileSource ForMapName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Map.TryParse(name, null, out var map) || map == null || map == Map.Internal)
        {
            return null;
        }

        return new ClusterFMapTileSource(map);
    }
}

public sealed record ClusterFTileProblem(int X, int Y, string Problem)
{
    public override string ToString() => $"({X},{Y}): {Problem}";
}

public sealed class ClusterFFootprintResult
{
    public string Refused { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int MaxZSpread { get; init; }
    public int MinZ { get; set; }
    public int MaxZ { get; set; }
    public int Spread => MaxZ - MinZ;
    public bool ZOk => Spread <= MaxZSpread;
    public List<ClusterFTileProblem> Blocked { get; } = [];
    public bool CanPlace => Refused == null && Blocked.Count == 0 && ZOk;

    public IEnumerable<string> Lines(int maxResults = 20)
    {
        if (Refused != null)
        {
            yield return Refused;
            yield break;
        }

        yield return $"Footprint ({X},{Y}) {Width}x{Height}: {(CanPlace ? "CAN be placed" : "CANNOT be placed")}. " +
                     $"{Blocked.Count} blocked tiles, land Z {MinZ}..{MaxZ} (spread {Spread}, allowed {MaxZSpread}).";
        foreach (var l in Blocked.Take(maxResults))
        {
            yield return "  " + l;
        }

        if (Blocked.Count > maxResults)
        {
            yield return $"  ... {Blocked.Count - maxResults} more not listed";
        }
    }
}

/// <summary>One spot to validate: where it came from (a line, a spawner, a decoration entry) and what is wrong, if anything.</summary>
public sealed record ClusterFSpot(int X, int Y, int? Z, string MapName, string Source)
{
    public string Problem { get; set; }
}

public sealed class ClusterFSpotsResult
{
    public List<ClusterFSpot> Spots { get; } = [];
    public List<string> BadLines { get; } = [];
    public int Dropped { get; set; }
    public int Ok => Spots.Count(s => s.Problem == null);
    public int Problems => Spots.Count - Ok;

    public IEnumerable<string> Lines(int maxResults = 50)
    {
        yield return $"{Spots.Count} spots checked: {Ok} ok, {Problems} with problems" +
                     (BadLines.Count > 0 ? $", {BadLines.Count} lines not understood" : "") +
                     (Dropped > 0 ? $", {Dropped} past the first {ClusterFMapCheck.MaxSpots} not read" : "") + ".";
        var shown = 0;
        foreach (var s in Spots.Where(s => s.Problem != null))
        {
            if (shown++ >= maxResults)
            {
                yield return $"  ... {Problems - maxResults} more not listed";
                break;
            }

            yield return $"  {s.Source}: ({s.X},{s.Y}{(s.Z.HasValue ? "," + s.Z.Value : "")}) on {s.MapName}: {s.Problem}";
        }

        foreach (var b in BadLines.Take(maxResults))
        {
            yield return "  " + b;
        }
    }
}

public sealed class ClusterFStaticProblemsResult
{
    public string Refused { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long Tiles { get; set; }
    public long Statics { get; set; }
    public int Duplicates { get; set; }
    public int Buried { get; set; }
    public int Unknown { get; set; }
    public List<ClusterFTileProblem> Problems { get; } = [];
    public int Total => Duplicates + Buried + Unknown;

    public IEnumerable<string> Lines(int maxResults = 50)
    {
        if (Refused != null)
        {
            yield return Refused;
            yield break;
        }

        yield return $"Checked {Tiles} tiles, {Statics} statics in ({X},{Y}) {Width}x{Height}: {Duplicates} duplicates, {Buried} buried, {Unknown} unknown IDs.";
        foreach (var l in Problems.Take(maxResults))
        {
            yield return "  " + l;
        }

        if (Problems.Count > maxResults)
        {
            yield return $"  ... {Problems.Count - maxResults} more not listed";
        }
    }
}

public sealed record ClusterFBlockedTile(int X, int Y, int LandZ, string Why);

public sealed class ClusterFWalkResult
{
    public string Refused { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long Tiles { get; set; }
    public long Walkable { get; set; }
    public long Water { get; set; }
    public long LandBlocked { get; set; }
    public long StaticBlocked { get; set; }
    public List<ClusterFBlockedTile> Blocked { get; } = [];

    public IEnumerable<string> Lines(int maxResults = 30)
    {
        if (Refused != null)
        {
            yield return Refused;
            yield break;
        }

        yield return $"{Tiles} tiles in ({X},{Y}) {Width}x{Height}: {Walkable} walkable, {Water} water, {LandBlocked} impassable land, {StaticBlocked} blocked by statics.";
        foreach (var b in Blocked.Take(maxResults))
        {
            yield return $"  ({b.X},{b.Y}): blocked by {b.Why}";
        }

        if (Blocked.Count > maxResults)
        {
            yield return $"  ... {Blocked.Count - maxResults} more not listed";
        }
    }
}

public sealed record ClusterFShoreHit(int X, int Y, int WaterId, int WaterZ, int[] ShoreIds, int MaxStep, string Problem);

public sealed class ClusterFShoreResult
{
    public static readonly string[] BucketNames =
        ["below water", "level (0)", "1-4 above", "5 above", "6-9 above", "10-24 above", "25+ above (cliff)"];

    public string Refused { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int WaterTiles { get; set; }
    public int ShoreHits => Hits.Count;
    public int Problems => Hits.Count(h => h.Problem != null);
    public int[] HeightBuckets { get; } = new int[7];
    public SortedDictionary<int, int> DeepWaterIds { get; } = new();
    public SortedDictionary<int, int> ShallowBedIds { get; } = new();
    public SortedDictionary<int, int> ShoreIds { get; } = new();
    public List<ClusterFShoreHit> Hits { get; } = [];

    public IEnumerable<string> Lines(int maxResults = 60, int stepThreshold = 0)
    {
        if (Refused != null)
        {
            yield return Refused;
            yield break;
        }

        yield return $"Region ({X},{Y}) {Width}x{Height}: {WaterTiles} water tiles (deep land + shallow beds), {ShoreHits} touch non-water land, {Problems} with a water surface outside -120..120.";
        yield return "Coast height (shore Z minus water Z) per shore-touching water tile: " +
                     string.Join(", ", BucketNames.Select((n, k) => $"{n} x{HeightBuckets[k]}"));
        yield return "Deep water land tile IDs (Wet flag, no static): " + string.Join(", ", DeepWaterIds.Select(k => $"0x{k.Key:X4}x{k.Value}"));
        yield return "Shallow-water beds (land tile under a water static): " + string.Join(", ", ShallowBedIds.Select(k => $"0x{k.Key:X4}x{k.Value}"));
        yield return "Shore tile IDs touched: " + string.Join(", ", ShoreIds.Select(k => $"0x{k.Key:X4}x{k.Value}"));
        var shown = 0;
        foreach (var h in Hits)
        {
            if (stepThreshold > 0 && h.MaxStep < stepThreshold)
            {
                continue;
            }

            if (shown++ >= maxResults)
            {
                break;
            }

            yield return $"  ({h.X},{h.Y}) water 0x{h.WaterId:X4} z={h.WaterZ} touches shore tiles [{string.Join("/", h.ShoreIds.Select(i => $"0x{i:X4}"))}]" +
                         (h.Problem != null ? "  <-- " + h.Problem : "");
        }

        if (ShoreHits > shown)
        {
            yield return $"  ... {ShoreHits - shown} more not shown.";
        }
    }
}

public static class ClusterFMapCheck
{
    public const int MaxFootprint = 64;
    public const int MaxArea = 512 * 512;
    public const int MaxSpots = 500;
    public const int StandHeight = 16;
    public const int BuriedThreshold = 10;
    public const int WaterSurfaceLimit = 120;
    public const string FolderName = "map-checks";

    /// <summary>Beside the saves, next to layout-captures: where exports and full reports are written.</summary>
    public static string Folder =>
        Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", FolderName);

    /// <summary>The server's data folder, where the spawn and decoration files are.</summary>
    public static string DataFolder => Path.Combine(Core.BaseDirectory, "Data");

    // ---------------------------------------------------------------- the blocked rule

    /// <summary>
    /// Why a tile cannot be stood on, or null. Statics count when their Z is from baseZ to baseZ + StandHeight, where
    /// baseZ is the land Z, or the given Z. (MapPlacementTools.Blocked, MapStaticsTools.CheckWalkability.)
    /// </summary>
    public static string Blocked(IClusterFTileSource src, int x, int y, int? z = null)
    {
        var land = src.GetLandTile(x, y);
        var lf = src.LandFlags(land.ID);
        if ((lf & TileFlag.Wet) != 0)
        {
            return "water";
        }

        if ((lf & TileFlag.Impassable) != 0)
        {
            return $"impassable land 0x{land.ID:X4}";
        }

        var baseZ = z ?? land.Z;
        foreach (var s in src.GetStaticTiles(x, y))
        {
            if (s.Z < baseZ || s.Z > baseZ + StandHeight)
            {
                continue;
            }

            var f = src.ItemFlags(s.ID);
            if ((f & TileFlag.Impassable) != 0 && (f & TileFlag.Surface) == 0)
            {
                return $"blocked by static 0x{s.ID:X4} at Z {s.Z}";
            }
        }

        return null;
    }

    public static bool OnMap(IClusterFTileSource src, int x, int y) => x >= 0 && y >= 0 && x < src.Width && y < src.Height;

    // ---------------------------------------------------------------- footprint

    public static ClusterFFootprintResult CheckFootprint(IClusterFTileSource src, int x, int y, int width, int height, int maxZSpread = 5)
    {
        if (width <= 0 || height <= 0 || width > MaxFootprint || height > MaxFootprint)
        {
            return new ClusterFFootprintResult { Refused = $"Footprint must be positive and at most {MaxFootprint}x{MaxFootprint} tiles." };
        }

        if (x < 0 || y < 0 || x + width > src.Width || y + height > src.Height)
        {
            return new ClusterFFootprintResult { Refused = $"Footprint ({x},{y}) {width}x{height} is outside the {src.Width}x{src.Height} map {src.Name}." };
        }

        var r = new ClusterFFootprintResult { X = x, Y = y, Width = width, Height = height, MaxZSpread = Math.Max(0, maxZSpread), MinZ = int.MaxValue, MaxZ = int.MinValue };
        for (var ty = y; ty < y + height; ty++)
        {
            for (var tx = x; tx < x + width; tx++)
            {
                var z = src.GetLandTile(tx, ty).Z;
                r.MinZ = Math.Min(r.MinZ, z);
                r.MaxZ = Math.Max(r.MaxZ, z);
                var why = Blocked(src, tx, ty);
                if (why != null)
                {
                    r.Blocked.Add(new ClusterFTileProblem(tx, ty, why));
                }
            }
        }

        return r;
    }

    // ---------------------------------------------------------------- spots

    /// <summary>
    /// "x,y", "x,y,z" or "x,y,z,map" lines (commas or spaces; # comments and blank lines skipped). A map name on a
    /// line wins over the default. Lines that do not parse are named; past MaxSpots the rest are counted, not read.
    /// </summary>
    public static ClusterFSpotsResult ParseSpotLines(IEnumerable<string> lines, string defaultMap, string source = "line", int max = MaxSpots)
    {
        var r = new ClusterFSpotsResult();
        var n = 0;
        foreach (var raw in lines)
        {
            n++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split([',', ' ', '\t', ';'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts.Length > 4 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                r.BadLines.Add($"{source} {n}: not x,y[,z][,map]: {line}");
                continue;
            }

            int? z = null;
            var map = defaultMap;
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[2], out var zz))
                {
                    z = zz;
                }
                else if (parts.Length == 3)
                {
                    map = parts[2];
                }
                else
                {
                    r.BadLines.Add($"{source} {n}: z is not a number: {line}");
                    continue;
                }
            }

            if (parts.Length == 4)
            {
                map = parts[3];
            }

            if (r.Spots.Count >= max)
            {
                r.Dropped++;
                continue;
            }

            r.Spots.Add(new ClusterFSpot(x, y, z, map, $"{source} {n}"));
        }

        return r;
    }

    /// <summary>
    /// Spots from a spawner JSON file as pinned writes them: an array of objects with "location" ([x, y, z], an
    /// object or "(x, y, z)") and "map". The spawner's name is kept as the source. Objects without a location are named.
    /// </summary>
    public static ClusterFSpotsResult ParseSpawnerJson(string json, string defaultMap = null, int max = MaxSpots)
    {
        var r = new ClusterFSpotsResult();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            r.BadLines.Add("the file is not a JSON array of spawners");
            return r;
        }

        var i = 0;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            i++;
            var name = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String ? nm.GetString() : null;
            var source = $"spawner {i}" + (name != null ? $" '{name}'" : "");
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("location", out var loc) || !TryReadPoint(loc, out var p))
            {
                r.BadLines.Add($"{source}: no location");
                continue;
            }

            var map = e.TryGetProperty("map", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : defaultMap;
            if (r.Spots.Count >= max)
            {
                r.Dropped++;
                continue;
            }

            r.Spots.Add(new ClusterFSpot(p.X, p.Y, p.Z, map, source));
        }

        return r;
    }

    private static bool TryReadPoint(JsonElement loc, out Point3D p)
    {
        p = Point3D.Zero;
        switch (loc.ValueKind)
        {
            case JsonValueKind.Array:
                {
                    var v = new int[3];
                    var k = 0;
                    foreach (var n in loc.EnumerateArray())
                    {
                        if (k >= 3 || n.ValueKind != JsonValueKind.Number)
                        {
                            return false;
                        }

                        v[k++] = n.GetInt32();
                    }

                    if (k < 2)
                    {
                        return false;
                    }

                    p = new Point3D(v[0], v[1], v[2]);
                    return true;
                }
            case JsonValueKind.Object:
                {
                    if (!loc.TryGetProperty("x", out var x) || !loc.TryGetProperty("y", out var y))
                    {
                        return false;
                    }

                    var z = loc.TryGetProperty("z", out var zz) && zz.ValueKind == JsonValueKind.Number ? zz.GetInt32() : 0;
                    p = new Point3D(x.GetInt32(), y.GetInt32(), z);
                    return true;
                }
            case JsonValueKind.String:
                return Point3D.TryParse(loc.GetString(), null, out p);
            default:
                return false;
        }
    }

    /// <summary>
    /// Spots from a decoration .cfg as pinned's Decorate reads it (UOContent/Commands/Object Creation/Decorate.cs:1308-
    /// 1380): a header line "Type 0xID(params)", then "x y z [extra]" lines until a blank one; # comments. One spot per
    /// map given, because a Britannia file decorates both Trammel and Felucca (Decorate.cs:37).
    /// </summary>
    public static ClusterFSpotsResult ParseDecorationCfg(IEnumerable<string> lines, string[] maps, int max = MaxSpots)
    {
        var r = new ClusterFSpotsResult();
        string header = null;
        var n = 0;
        foreach (var raw in lines)
        {
            n++;
            var line = raw.Trim();
            if (line.Length == 0)
            {
                header = null;
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (header == null)
            {
                header = line;
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) || !int.TryParse(parts[2], out var z))
            {
                r.BadLines.Add($"line {n}: not x y z under '{header}': {line}");
                continue;
            }

            foreach (var map in maps)
            {
                if (r.Spots.Count >= max)
                {
                    r.Dropped++;
                    continue;
                }

                r.Spots.Add(new ClusterFSpot(x, y, z, map, $"line {n} ({header})"));
            }
        }

        return r;
    }

    /// <summary>Fills each spot's Problem: an unknown map, outside the map, or the blocked rule at its Z.</summary>
    public static ClusterFSpotsResult Validate(ClusterFSpotsResult spots, Func<string, IClusterFTileSource> sourceFor)
    {
        var cache = new Dictionary<string, IClusterFTileSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in spots.Spots)
        {
            var key = s.MapName ?? "";
            if (!cache.TryGetValue(key, out var src))
            {
                cache[key] = src = sourceFor(key);
            }

            if (src == null)
            {
                s.Problem = $"unknown map '{s.MapName}'";
            }
            else if (!OnMap(src, s.X, s.Y))
            {
                s.Problem = $"outside the {src.Width}x{src.Height} map";
            }
            else
            {
                s.Problem = Blocked(src, s.X, s.Y, s.Z);
            }
        }

        return spots;
    }

    // ---------------------------------------------------------------- statics

    private static string RefuseArea(int width, int height) =>
        width <= 0 || height <= 0 || (long)width * height > MaxArea ? $"Region must be positive and at most {MaxArea} tiles (512x512)." : null;

    public static ClusterFStaticProblemsResult FindStaticProblems(IClusterFTileSource src, int x, int y, int width, int height)
    {
        var refused = RefuseArea(width, height);
        if (refused != null)
        {
            return new ClusterFStaticProblemsResult { Refused = refused };
        }

        var r = new ClusterFStaticProblemsResult { X = x, Y = y, Width = width, Height = height };
        var seen = new HashSet<(int, int)>();
        for (var ty = Math.Max(0, y); ty < Math.Min(src.Height, y + height); ty++)
        {
            for (var tx = Math.Max(0, x); tx < Math.Min(src.Width, x + width); tx++)
            {
                r.Tiles++;
                var landZ = src.GetLandTile(tx, ty).Z;
                seen.Clear();
                foreach (var s in src.GetStaticTiles(tx, ty))
                {
                    r.Statics++;
                    if (!seen.Add((s.ID, s.Z)))
                    {
                        r.Duplicates++;
                        r.Problems.Add(new ClusterFTileProblem(tx, ty, $"duplicate static 0x{s.ID:X4} at Z {s.Z}"));
                    }

                    if (s.Z <= landZ - BuriedThreshold)
                    {
                        r.Buried++;
                        r.Problems.Add(new ClusterFTileProblem(tx, ty, $"static 0x{s.ID:X4} Z {s.Z} buried under land Z {landZ}"));
                    }

                    if (!src.HasItemData(s.ID))
                    {
                        r.Unknown++;
                        r.Problems.Add(new ClusterFTileProblem(tx, ty, $"static 0x{s.ID:X4} has no tiledata entry"));
                    }
                }
            }
        }

        return r;
    }

    // ---------------------------------------------------------------- walkable

    public static ClusterFWalkResult CheckWalkable(IClusterFTileSource src, int x, int y, int width, int height)
    {
        var refused = RefuseArea(width, height);
        if (refused != null)
        {
            return new ClusterFWalkResult { Refused = refused };
        }

        var r = new ClusterFWalkResult { X = x, Y = y, Width = width, Height = height };
        for (var ty = Math.Max(0, y); ty < Math.Min(src.Height, y + height); ty++)
        {
            for (var tx = Math.Max(0, x); tx < Math.Min(src.Width, x + width); tx++)
            {
                r.Tiles++;
                var land = src.GetLandTile(tx, ty);
                var why = Blocked(src, tx, ty);
                if (why == null)
                {
                    r.Walkable++;
                    continue;
                }

                if (why == "water")
                {
                    r.Water++;
                }
                else if (why.StartsWith("impassable land", StringComparison.Ordinal))
                {
                    r.LandBlocked++;
                }
                else
                {
                    r.StaticBlocked++;
                    why = why["blocked by ".Length..];
                }

                r.Blocked.Add(new ClusterFBlockedTile(tx, ty, land.Z, why));
            }
        }

        return r;
    }

    /// <summary>Why the overlay is refused, or null. Markers are placed on the test shard only.</summary>
    public static string OverlayRefusal(bool isTestShard, int seconds)
    {
        if (!isTestShard)
        {
            return "The overlay places temporary markers and runs on the test shard only. 'list' works here.";
        }

        if (seconds < 1 || seconds > ClusterFWalkMarker.MaxSeconds)
        {
            return $"The overlay timeout is 1 to {ClusterFWalkMarker.MaxSeconds} seconds.";
        }

        return null;
    }

    /// <summary>One self-deleting marker per blocked tile, at the land Z, up to the cap. Returns how many were placed.</summary>
    public static int PlaceMarkers(Map map, ClusterFWalkResult result, int seconds, int cap = ClusterFWalkMarker.MaxPerOverlay)
    {
        var placed = 0;
        foreach (var b in result.Blocked)
        {
            if (placed >= cap)
            {
                break;
            }

            var m = new ClusterFWalkMarker(b.Why, seconds);
            m.MoveToWorld(new Point3D(b.X, b.Y, b.LandZ), map);
            placed++;
        }

        return placed;
    }

    // ---------------------------------------------------------------- shore

    public static ClusterFShoreResult CheckShore(IClusterFTileSource src, int x, int y, int width, int height)
    {
        var refused = RefuseArea(width, height);
        if (refused != null)
        {
            return new ClusterFShoreResult { Refused = refused };
        }

        var r = new ClusterFShoreResult { X = x, Y = y, Width = width, Height = height };
        var wetStatic = new Dictionary<int, bool>();
        bool StaticIsWet(int id)
        {
            if (!wetStatic.TryGetValue(id, out var w))
            {
                wetStatic[id] = w = (src.ItemFlags(id) & TileFlag.Wet) != 0;
            }

            return w;
        }

        // Water: Wet land, or a shallow bed (non-Wet land under a Wet static). The bed's surface is its highest Wet static.
        (bool Water, bool Bed, int WaterZ, int LandId, int LandZ) Cell(int cx, int cy)
        {
            var land = src.GetLandTile(cx, cy);
            var wetLand = (src.LandFlags(land.ID) & TileFlag.Wet) != 0;
            if (wetLand)
            {
                return (true, false, land.Z, land.ID, land.Z);
            }

            var bedZ = int.MinValue;
            foreach (var s in src.GetStaticTiles(cx, cy))
            {
                if (StaticIsWet(s.ID))
                {
                    bedZ = Math.Max(bedZ, s.Z);
                }
            }

            return bedZ != int.MinValue ? (true, true, bedZ, land.ID, land.Z) : (false, false, land.Z, land.ID, land.Z);
        }

        static int Bucket(int d) => d < 0 ? 0 : d == 0 ? 1 : d < 5 ? 2 : d == 5 ? 3 : d < 10 ? 4 : d < 25 ? 5 : 6;

        for (var wy = Math.Max(0, y); wy < Math.Min(src.Height, y + height); wy++)
        {
            for (var wx = Math.Max(0, x); wx < Math.Min(src.Width, x + width); wx++)
            {
                var c = Cell(wx, wy);
                if (!c.Water)
                {
                    continue;
                }

                r.WaterTiles++;
                var ids = c.Bed ? r.ShallowBedIds : r.DeepWaterIds;
                ids[c.LandId] = ids.GetValueOrDefault(c.LandId) + 1;

                var shore = new SortedSet<int>();
                var maxStep = int.MinValue;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0 || !OnMap(src, wx + dx, wy + dy))
                        {
                            continue;
                        }

                        var n = Cell(wx + dx, wy + dy);
                        if (!n.Water)
                        {
                            shore.Add(n.LandId);
                            maxStep = Math.Max(maxStep, n.LandZ - c.WaterZ);
                        }
                    }
                }

                if (shore.Count == 0)
                {
                    continue;
                }

                foreach (var s in shore)
                {
                    r.ShoreIds[s] = r.ShoreIds.GetValueOrDefault(s) + 1;
                }

                var problem = Math.Abs(c.WaterZ) > WaterSurfaceLimit ? $"water surface Z {c.WaterZ} is outside -{WaterSurfaceLimit}..{WaterSurfaceLimit} (hard limit 127)" : null;
                r.HeightBuckets[Bucket(maxStep)]++;
                r.Hits.Add(new ClusterFShoreHit(wx, wy, c.LandId, c.WaterZ, shore.ToArray(), maxStep, problem));
            }
        }

        return r;
    }

    // ---------------------------------------------------------------- export

    /// <summary>[ { "location": [x, y, z], "map": "Trammel" }, ... ] as the spawn files carry a location and a map.</summary>
    public static string PointsJson(string mapName, IReadOnlyList<Point3D> points)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartArray();
            foreach (var p in points)
            {
                w.WriteStartObject();
                WriteLocation(w, p);
                w.WriteString("map", mapName);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// One object with the rectangle as "spawnBounds" (x1, y1 inclusive; x2, y2 exclusive, Rectangle3D's End, as pinned
    /// writes it) and its centre as "location", so it pastes into a spawner entry.
    /// </summary>
    public static string RectangleJson(string mapName, int x, int y, int width, int height, int z)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartArray();
            w.WriteStartObject();
            WriteLocation(w, new Point3D(x + width / 2, y + height / 2, z));
            w.WriteString("map", mapName);
            w.WritePropertyName("spawnBounds");
            w.WriteStartObject();
            w.WriteNumber("x1", x);
            w.WriteNumber("y1", y);
            w.WriteNumber("x2", x + width);
            w.WriteNumber("y2", y + height);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndArray();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteLocation(Utf8JsonWriter w, Point3D p)
    {
        w.WritePropertyName("location");
        w.WriteStartArray();
        w.WriteNumberValue(p.X);
        w.WriteNumberValue(p.Y);
        w.WriteNumberValue(p.Z);
        w.WriteEndArray();
    }

    // ---------------------------------------------------------------- files

    /// <summary>A name for an export or report file: 1 to 40 letters, digits, - or _, starting with a letter or digit.</summary>
    public static string CheckName(string name) =>
        name != null && System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$")
            ? null
            : "a name is 1 to 40 letters, digits, - or _, starting with a letter or digit";

    /// <summary>
    /// The file a relative path names, under one of the given roots (the first that has it), or null and why. Rooted
    /// paths and ".." are refused, so a command can only ever read under the data folder and the checks folder.
    /// </summary>
    public static string ResolveReadable(string relative, IReadOnlyList<string> roots, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(relative))
        {
            error = "give a file path relative to the server's Data folder, e.g. Spawns/post-uoml/trammel/Outdoors.json";
            return null;
        }

        var rel = relative.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(rel) || rel.StartsWith('/') || rel.Split('/').Any(p => p == ".."))
        {
            error = "only a path under the server's Data folder (or the map-checks folder) can be read; no rooted paths and no ..";
            return null;
        }

        foreach (var root in roots)
        {
            var full = Path.GetFullPath(Path.Combine(root, rel));
            var rootFull = Path.GetFullPath(root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
            {
                return full;
            }
        }

        error = $"no file '{rel}' under {string.Join(" or ", roots)}";
        return null;
    }

    /// <summary>Writes the full report beside the saves and returns its path. The in-game lines are capped; this is not.</summary>
    public static string WriteReport(string command, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"{command}-{Core.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return path;
    }
}

/// <summary>
/// A temporary marker [Walkable's overlay drops on a blocked tile: a red rune named for the cause, not movable, and
/// deleted by its own timer. A marker never survives a restart: one read from a save deletes itself at once. Test
/// shard only (ClusterFMapCheck.OverlayRefusal); [Walkable clear deletes every live one.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ClusterFWalkMarker : Item
{
    public const int Graphic = 0x1F14; // the recall rune graphic: flat, small, easy to see
    public const int BlockedHue = 0x21; // red
    public const int DefaultSeconds = 60;
    public const int MaxSeconds = 600;
    public const int MaxPerOverlay = 2000;

    private static readonly HashSet<ClusterFWalkMarker> _live = [];

    public static int LiveCount => _live.Count;

    public DateTime Expires { get; private set; }

    public ClusterFWalkMarker(string reason, int seconds) : base(Graphic)
    {
        Movable = false;
        Hue = BlockedHue;
        Name = "blocked: " + reason;
        Expires = Core.Now + TimeSpan.FromSeconds(seconds);
        _live.Add(this);
        Timer.StartTimer(TimeSpan.FromSeconds(seconds), Delete);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Timer.StartTimer(TimeSpan.Zero, Delete);
    }

    public override void OnDelete()
    {
        _live.Remove(this);
        base.OnDelete();
    }

    /// <summary>Deletes every live marker now. Returns how many.</summary>
    public static int DeleteAll()
    {
        var all = _live.ToArray();
        foreach (var m in all)
        {
            m.Delete();
        }

        _live.Clear();
        return all.Length;
    }
}
