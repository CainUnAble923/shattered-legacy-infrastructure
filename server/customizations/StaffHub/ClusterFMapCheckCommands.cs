// ClusterFMapCheckCommands.cs
//
// cc-P135. The in-game side of ClusterFMapCheck.cs: six GameMaster commands that read the loaded map and report.
// Adapted from vseUOAware by ghostbyte420, GPL-3.0 (vseUtilities commit e4b1d243; the tools named in
// ClusterFMapCheck.cs). This file is GPL-3.0, as ModernUO is. Notes: shard-migration/notes/cc-P135-vseuoaware-staff-commands.md.
//
//   [CheckFootprint <width> <height> [maxZSpread=5]     target the top-left corner: CAN / CANNOT, the blocked tiles, Z spread
//   [ValidateSpots <file> [map]                         x,y[,z][,map] lines, a spawner .json or a decoration .cfg under
//                                                       Data (or the map-checks folder); each bad spot is named
//   [FindStaticProblems                                 two corners: duplicate, buried and unknown-id statics (max 512x512)
//   [Walkable [list|overlay [seconds]|clear]            two corners: blocked tiles listed, or marked with self-deleting
//                                                       markers (test shard only), or every marker cleared now
//   [ShoreCheck [stepThreshold]                         two corners: water touching non-water land, coast heights
//   [ExportPoints <name> [rect|point|clear]             a rectangle (two corners) or points (one target at a time) to
//                                                       <world folder>\map-checks\<name>.json in the spawn files' shape
//
// None of them changes a world: they read and report. The one thing that places anything is [Walkable overlay, and
// what it places is a ClusterFWalkMarker: not movable, named for its cause, deleted by its own timer (1 to 600 s,
// default 60), never kept across a restart, and refused on the live shard (TestCenter.Enabled is the switch both
// shards' configs set, as ClusterFStaffHub.IsTestShard reads it). Reports longer than the in-game cap are written in
// full to the map-checks folder beside the saves, and the message names the file.
//
// Declared Diagnostic: nothing here is a world generation, setup or removal, and the hub lists them under
// Diagnostics. Every argument is a plain literal for the console's source reader.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Commands;
using Server.Misc;
using Server.Targeting;

namespace Server;

public static class ClusterFMapCheckCommands
{
    private const int Red = 0x22;
    private const int Green = 0x43;
    private const int Gray = 0x3B2;

    // [ExportPoints point: the points gathered so far, per staff member, by export name.
    private static readonly Dictionary<Serial, (string Name, List<Point3D> Points, Map Map)> _points = new();

    public static void Configure()
    {
        CommandSystem.Register("CheckFootprint", AccessLevel.GameMaster, OnCheckFootprint);
        CommandSystem.Register("ValidateSpots", AccessLevel.GameMaster, OnValidateSpots);
        CommandSystem.Register("FindStaticProblems", AccessLevel.GameMaster, OnFindStaticProblems);
        CommandSystem.Register("Walkable", AccessLevel.GameMaster, OnWalkable);
        CommandSystem.Register("ShoreCheck", AccessLevel.GameMaster, OnShoreCheck);
        CommandSystem.Register("ExportPoints", AccessLevel.GameMaster, OnExportPoints);
    }

    public static bool IsTestShard => TestCenter.Enabled;

    private static IClusterFTileSource SourceOf(Mobile from) =>
        from.Map == null || from.Map == Map.Internal ? null : new ClusterFMapTileSource(from.Map);

    private static void Say(Mobile from, IEnumerable<string> lines, int hue = Gray)
    {
        foreach (var l in lines)
        {
            from.SendMessage(hue, l);
        }
    }

    /// <summary>The corner-to-corner rectangle of a bounding box pick, both corners included.</summary>
    public static (int X, int Y, int Width, int Height) Box(Point3D a, Point3D b) =>
        (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

    // ---------------------------------------------------------------- [CheckFootprint

    [Usage("CheckFootprint <width> <height> [maxZSpread=5]")]
    [Description("Targets the top-left corner of a footprint and says whether a building of that size can stand there: CAN or CANNOT, the blocked tiles (water, impassable land, an impassable non-surface static within 16 Z) and the land Z spread. Max 64x64. Reads only.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Can a w x h footprint stand where you target? Blocked tiles and land Z spread.")]
    private static void OnCheckFootprint(CommandEventArgs e)
    {
        if (e.Length < 2 || e.GetInt32(0) <= 0 || e.GetInt32(1) <= 0)
        {
            e.Mobile.SendMessage(Red, "Usage: [CheckFootprint <width> <height> [maxZSpread=5], then target the top-left corner.");
            return;
        }

        var w = e.GetInt32(0);
        var h = e.GetInt32(1);
        var spread = e.Length >= 3 ? e.GetInt32(2) : 5;
        if (w > ClusterFMapCheck.MaxFootprint || h > ClusterFMapCheck.MaxFootprint)
        {
            e.Mobile.SendMessage(Red, $"A footprint is at most {ClusterFMapCheck.MaxFootprint}x{ClusterFMapCheck.MaxFootprint} tiles.");
            return;
        }

        e.Mobile.SendMessage(Gray, $"Target the top-left corner of the {w}x{h} footprint (land Z spread allowed: {spread}).");
        e.Mobile.Target = new PointTarget((from, p) =>
        {
            var src = SourceOf(from);
            if (src == null)
            {
                from.SendMessage(Red, "You are not on a facet.");
                return;
            }

            var r = ClusterFMapCheck.CheckFootprint(src, p.X, p.Y, w, h, spread);
            Say(from, r.Lines(), r.Refused != null ? Red : r.CanPlace ? Green : Red);
        });
    }

    // ---------------------------------------------------------------- [ValidateSpots

    [Usage("ValidateSpots <file under Data> [map]")]
    [Description("Reads spots from a file under the server's Data folder (or the map-checks folder) and names each bad one: outside the map, water, impassable land, or blocked by a static. The file is x,y[,z][,map] lines (the map after the file name is the default), a spawner .json (location and map per spawner), or a decoration .cfg (x y z lines; give the map, or Britannia for both Trammel and Felucca). At most 500 spots a file. Reads only; the full report is written to the map-checks folder.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Checks every spot in a spawn, decoration or x,y list file and names the bad ones.")]
    private static void OnValidateSpots(CommandEventArgs e)
    {
        var from = e.Mobile;
        if (e.Length < 1)
        {
            from.SendMessage(Red, "Usage: [ValidateSpots <file under Data> [map]. E.g. [ValidateSpots Spawns/post-uoml/trammel/Outdoors.json");
            return;
        }

        var path = ClusterFMapCheck.ResolveReadable(e.GetString(0), [ClusterFMapCheck.DataFolder, ClusterFMapCheck.Folder], out var error);
        if (path == null)
        {
            from.SendMessage(Red, error);
            return;
        }

        var mapArg = e.Length >= 2 ? e.GetString(1) : null;
        var defaultMap = mapArg ?? (from.Map != null && from.Map != Map.Internal ? from.Map.Name : "Trammel");
        ClusterFSpotsResult spots;
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".json")
            {
                spots = ClusterFMapCheck.ParseSpawnerJson(File.ReadAllText(path), defaultMap);
            }
            else if (ext == ".cfg")
            {
                var maps = string.Equals(mapArg, "Britannia", StringComparison.OrdinalIgnoreCase)
                    ? new[] { "Trammel", "Felucca" }
                    : new[] { defaultMap };
                spots = ClusterFMapCheck.ParseDecorationCfg(File.ReadAllLines(path), maps);
            }
            else
            {
                spots = ClusterFMapCheck.ParseSpotLines(File.ReadAllLines(path), defaultMap);
            }
        }
        catch (Exception ex)
        {
            from.SendMessage(Red, $"Could not read {Path.GetFileName(path)}: {ex.Message}");
            return;
        }

        ClusterFMapCheck.Validate(spots, ClusterFMapTileSource.ForMapName);
        var lines = spots.Lines().ToList();
        Say(from, lines, spots.Problems == 0 && spots.BadLines.Count == 0 ? Green : Red);
        var report = ClusterFMapCheck.WriteReport("validate-spots", new[] { $"[ValidateSpots {e.GetString(0)} {mapArg}: {path}" }.Concat(spots.Lines(int.MaxValue)));
        from.SendMessage(Gray, $"Full report: {report}");
    }

    // ---------------------------------------------------------------- [FindStaticProblems

    [Usage("FindStaticProblems")]
    [Description("Target two corners (max 512x512 tiles): exact duplicate statics (same id and Z on one tile), statics buried 10 or more Z below the land, and static ids with no tiledata entry. Reads the map's statics only, not items. Reads only; the full report is written to the map-checks folder.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Duplicate, buried and unknown-id map statics in a box you target.")]
    private static void OnFindStaticProblems(CommandEventArgs e)
    {
        BoundingBoxPicker.Begin(e.Mobile, (map, a, b) =>
        {
            var from = e.Mobile;
            var (x, y, w, h) = Box(a, b);
            var r = ClusterFMapCheck.FindStaticProblems(new ClusterFMapTileSource(map), x, y, w, h);
            Say(from, r.Lines(), r.Refused != null || r.Total > 0 ? Red : Green);
            if (r.Refused == null && r.Problems.Count > 50)
            {
                from.SendMessage(Gray, $"Full report: {ClusterFMapCheck.WriteReport("static-problems", r.Lines(int.MaxValue))}");
            }
        });
    }

    // ---------------------------------------------------------------- [Walkable

    [Usage("Walkable [list|overlay [seconds]|clear]")]
    [Description("Target two corners (max 512x512 tiles) and list the tiles a mobile cannot stand on (water, impassable land, an impassable non-surface static within 16 Z), with counts. 'overlay' marks each blocked tile with a red rune named for the cause that deletes itself after the seconds given (default 60, max 600): test shard only, at most 2000 markers. 'clear' deletes every marker now. Heuristic over map data, not the server's movement rules.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Blocked tiles in a box you target, listed or marked with temporary runes (test shard).")]
    private static void OnWalkable(CommandEventArgs e)
    {
        var from = e.Mobile;
        var mode = e.Length >= 1 ? e.GetString(0).ToLowerInvariant() : "list";
        if (mode == "clear")
        {
            from.SendMessage(Green, $"Deleted {ClusterFWalkMarker.DeleteAll()} walkability markers.");
            return;
        }

        if (mode != "list" && mode != "overlay")
        {
            from.SendMessage(Red, "Usage: [Walkable [list|overlay [seconds]|clear], then target two corners.");
            return;
        }

        var seconds = e.Length >= 2 ? e.GetInt32(1) : ClusterFWalkMarker.DefaultSeconds;
        if (mode == "overlay")
        {
            var refusal = ClusterFMapCheck.OverlayRefusal(IsTestShard, seconds);
            if (refusal != null)
            {
                from.SendMessage(Red, refusal);
                return;
            }
        }

        BoundingBoxPicker.Begin(from, (map, a, b) =>
        {
            var (x, y, w, h) = Box(a, b);
            var r = ClusterFMapCheck.CheckWalkable(new ClusterFMapTileSource(map), x, y, w, h);
            Say(from, r.Lines(), r.Refused != null ? Red : Gray);
            if (r.Refused != null)
            {
                return;
            }

            if (mode == "overlay")
            {
                var placed = ClusterFMapCheck.PlaceMarkers(map, r, seconds);
                from.SendMessage(Green, $"Placed {placed} markers on the blocked tiles" +
                                        (placed < r.Blocked.Count ? $" (the first {placed} of {r.Blocked.Count})" : "") +
                                        $"; they delete themselves in {seconds} s. [Walkable clear removes them now.");
            }

            if (r.Blocked.Count > 30)
            {
                from.SendMessage(Gray, $"Full report: {ClusterFMapCheck.WriteReport("walkable", r.Lines(int.MaxValue))}");
            }
        });
    }

    // ---------------------------------------------------------------- [ShoreCheck

    [Usage("ShoreCheck [stepThreshold]")]
    [Description("Target two corners (max 512x512 tiles): every water tile (Wet land, or a land tile under a Wet static) that touches non-water land, with the coast heights in buckets, the water and shore tile ids, and any water surface outside -120..120. The threshold lists only shores at least that many Z above the water. Reads only; the full report is written to the map-checks folder.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Water touching land in a box you target: coast heights and tile ids.")]
    private static void OnShoreCheck(CommandEventArgs e)
    {
        var threshold = e.Length >= 1 ? e.GetInt32(0) : 0;
        BoundingBoxPicker.Begin(e.Mobile, (map, a, b) =>
        {
            var from = e.Mobile;
            var (x, y, w, h) = Box(a, b);
            var r = ClusterFMapCheck.CheckShore(new ClusterFMapTileSource(map), x, y, w, h);
            Say(from, r.Lines(20, threshold), r.Refused != null || r.Problems > 0 ? Red : Gray);
            if (r.Refused == null)
            {
                from.SendMessage(Gray, $"Full report: {ClusterFMapCheck.WriteReport("shore", r.Lines(int.MaxValue, threshold))}");
            }
        });
    }

    // ---------------------------------------------------------------- [ExportPoints

    [Usage("ExportPoints <name> [rect|point|clear]")]
    [Description("Writes coordinates as ModernUO's own spawn-file JSON to <world folder>/map-checks/<name>.json: 'rect' (the default) targets two corners and writes the rectangle as spawnBounds with its centre as location; 'point' targets one spot and adds it to the list for that name, rewriting the file each time; 'clear' forgets the list. The file reads back with [ValidateSpots <name>.json. Writes a file, never the world.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Targeted rectangle or points to a JSON file in the spawn files' shape.")]
    private static void OnExportPoints(CommandEventArgs e)
    {
        var from = e.Mobile;
        var name = e.GetString(0);
        var bad = ClusterFMapCheck.CheckName(name);
        if (e.Length < 1 || bad != null)
        {
            from.SendMessage(Red, "Usage: [ExportPoints <name> [rect|point|clear]" + (bad != null && e.Length >= 1 ? $" ({bad})" : "") + ".");
            return;
        }

        var mode = e.Length >= 2 ? e.GetString(1).ToLowerInvariant() : "rect";
        switch (mode)
        {
            case "clear":
                _points.Remove(from.Serial);
                from.SendMessage(Green, $"Forgot the point list for {name}. The file, if any, stays.");
                return;
            case "point":
                from.SendMessage(Gray, $"Target a spot to add to {name}.");
                from.Target = new PointTarget((m, p) =>
                {
                    if (m.Map == null || m.Map == Map.Internal)
                    {
                        m.SendMessage(Red, "You are not on a facet.");
                        return;
                    }

                    if (!_points.TryGetValue(m.Serial, out var list) || list.Name != name || list.Map != m.Map)
                    {
                        _points[m.Serial] = list = (name, new List<Point3D>(), m.Map);
                    }

                    list.Points.Add(p);
                    var path = Write(name, ClusterFMapCheck.PointsJson(m.Map.Name, list.Points));
                    m.SendMessage(Green, $"{list.Points.Count} point(s) in {name}: {path}. [ExportPoints {name} point adds another; [ExportPoints {name} clear starts over.");
                });
                return;
            case "rect":
                BoundingBoxPicker.Begin(from, (map, a, b) =>
                {
                    var (x, y, w, h) = Box(a, b);
                    var path = Write(name, ClusterFMapCheck.RectangleJson(map.Name, x, y, w, h, Math.Min(a.Z, b.Z)));
                    from.SendMessage(Green, $"Rectangle ({x},{y}) {w}x{h} on {map.Name} written to {path} (spawnBounds x2,y2 are exclusive, as the spawn files have them).");
                });
                return;
            default:
                from.SendMessage(Red, "Usage: [ExportPoints <name> [rect|point|clear].");
                return;
        }
    }

    public static string PathOf(string name) => Path.Combine(ClusterFMapCheck.Folder, name + ".json");

    private static string Write(string name, string json)
    {
        Directory.CreateDirectory(ClusterFMapCheck.Folder);
        var path = PathOf(name);
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
        return path;
    }

    // ---------------------------------------------------------------- targets

    private sealed class PointTarget : Target
    {
        private readonly Action<Mobile, Point3D> _then;

        public PointTarget(Action<Mobile, Point3D> then) : base(-1, true, TargetFlags.None) => _then = then;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D p)
            {
                from.SendMessage(Red, "Target a spot on the ground or a static.");
                return;
            }

            _then(from, new Point3D(p.X, p.Y, p.Z));
        }
    }
}
