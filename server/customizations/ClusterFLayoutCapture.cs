using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Server.Commands;
using Server.Items;
using Server.Multis;
using Server.Targeting;

namespace Server;

/// <summary>
/// cc-P98: capture a box of the world as the shard's layout JSON, and stamp a capture back, with nudge and undo.
///
/// The idea and the shape of it (box capture of items and map statics, offsets, nudge, undo) are Kamras's Building
/// Duplicator for ServUO (servuo.dev; kept for reference at D:\UO\third-party\code\building-duplicator). This is a
/// rewrite for ModernUO, not a copy, and it changes what that tool did in four ways the cc-P93 assessment asked for:
///   - a 64 x 64 cap on the box, refused before a single tile is read (the original looped on any box);
///   - statics read with GetStaticTiles, which leaves multi (house and boat) tiles out;
///   - the whole file is checked before anything is placed, so a bad line places nothing;
///   - every item goes in the undo list before it is placed, so undo also covers a stamp that stopped part way.
/// Its AddOn .cs export is left out: pinned ModernUO has [AddonGen.
///
/// The file is the design layout shape the generators read (design/registrar-office/layout.json, read by
/// shard-migration notes/cc-P31-tools/gen_office_layout.py): "map" and "components", each component "area", "id"
/// ("0x0A99"), "name", "x", "y", "z" in world coordinates. It adds "name", "source", "origin" and each component's
/// "hue", which the generator does not read. Only the item ID and hue are ever kept, never an item's type or
/// properties, so a stamp makes plain Static items and cannot duplicate anything of value.
///
/// Captures go in the world folder, beside Saves: on the test shard D:\ShatteredLegacy\server\lib\uo\modernuo-test\
/// layout-captures\ (server/lib/ is git-ignored), never in a repo. Staff only (GameMaster).
/// </summary>
public static class ClusterFLayoutCapture
{
    public const int MaxSide = 64;
    public const int MaxPieces = 4096;
    public const string FolderName = "layout-captures";

    private static readonly Regex NameRe = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$");
    private static readonly Regex HexRe = new("^0x[0-9A-Fa-f]{1,4}$");

    // Undo, per staff member: each stamp is the list of what it placed, newest on top. Memory only (see the notes).
    private static readonly Dictionary<Serial, Stack<List<Item>>> _undo = new();

    /// <summary>How many items and static tiles the last Capture looked at (the facts check a refused box reads none).</summary>
    public static int TilesRead { get; private set; }

    public static string Folder => Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", FolderName);

    public static string PathOf(string name) => Path.Combine(Folder, name + ".json");

    // The highest item graphic: TileData's once it has loaded, else the full 16 bits (the facts' host loads no tile data).
    private static int MaxItem => TileData.MaxItemValue > 0 ? TileData.MaxItemValue : 0xFFFF;

    public static void Configure()
    {
        CommandSystem.Register("CaptureLayout", AccessLevel.GameMaster, OnCapture);
        CommandSystem.Register("StampLayout", AccessLevel.GameMaster, OnStamp);
    }

    // -- capture ---------------------------------------------------------------------------------------------------

    public static string CheckName(string name) =>
        name != null && NameRe.IsMatch(name) ? null : "a layout name is 1 to 40 letters, digits, - or _, starting with a letter or digit";

    /// <summary>Why a box is refused, or null. Called before anything is read.</summary>
    public static string CheckBox(Point2D a, Point2D b, int minZ, int maxZ)
    {
        var w = Math.Abs(b.X - a.X) + 1;
        var h = Math.Abs(b.Y - a.Y) + 1;
        if (w > MaxSide || h > MaxSide)
        {
            return $"the box is {w} x {h} tiles; the most this captures is {MaxSide} x {MaxSide}";
        }

        if (minZ > maxZ || minZ < sbyte.MinValue || maxZ > sbyte.MaxValue)
        {
            return $"the Z range {minZ} to {maxZ} is not a range from -128 to 127";
        }

        return null;
    }

    /// <summary>
    /// The items and map statics in the box (both corners included) between minZ and maxZ, in a fixed order (y, x, z,
    /// id, hue). Left out: multis (houses, boats), doors, addon bases, anything not visible (spawners, teleporters),
    /// and multi tiles. Returns null and a reason for a box over the cap, without reading anything.
    /// </summary>
    public static List<ClusterFLayoutPiece> Capture(Map map, Point2D a, Point2D b, int minZ, int maxZ, string area, out string refused)
    {
        TilesRead = 0;
        refused = CheckBox(a, b, minZ, maxZ);
        if (refused != null)
        {
            return null;
        }

        if (map == null || map == Map.Internal)
        {
            refused = "there is no map to capture from";
            return null;
        }

        int x1 = Math.Min(a.X, b.X), x2 = Math.Max(a.X, b.X), y1 = Math.Min(a.Y, b.Y), y2 = Math.Max(a.Y, b.Y);
        area = string.IsNullOrWhiteSpace(area) ? "main" : area.Trim();
        var pieces = new List<ClusterFLayoutPiece>();

        foreach (var item in map.GetItemsInBounds<Item>(new Rectangle2D(x1, y1, x2 - x1 + 1, y2 - y1 + 1), true))
        {
            TilesRead++;
            if (item.Deleted || item.Parent != null || !item.Visible || item is BaseMulti or BaseDoor or BaseAddon)
            {
                continue;
            }

            if (item.X < x1 || item.X > x2 || item.Y < y1 || item.Y > y2 || item.Z < minZ || item.Z > maxZ)
            {
                continue;
            }

            pieces.Add(Piece(area, item.ItemID, item.X, item.Y, item.Z, item.Hue));
        }

        for (var x = x1; x <= x2; x++)
        {
            for (var y = y1; y <= y2; y++)
            {
                foreach (var t in map.Tiles.GetStaticTiles(x, y))
                {
                    TilesRead++;
                    if (t.Z >= minZ && t.Z <= maxZ)
                    {
                        pieces.Add(Piece(area, t.ID, x, y, t.Z, t.Hue));
                    }
                }
            }
        }

        if (pieces.Count > MaxPieces)
        {
            refused = $"the box holds {pieces.Count} pieces; a layout holds at most {MaxPieces}";
            return null;
        }

        return pieces.OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Z).ThenBy(p => p.ItemID).ThenBy(p => p.Hue).ToList();
    }

    private static ClusterFLayoutPiece Piece(string area, int id, int x, int y, int z, int hue)
    {
        id &= MaxItem;
        var name = TileData.ItemTable[id].Name;
        return new ClusterFLayoutPiece(area, id, string.IsNullOrEmpty(name) ? "unnamed" : name.Trim(), x, y, z, hue);
    }

    /// <summary>The layout JSON: the design shape (map, components with area, id, name, x, y, z), plus name, source,
    /// origin and hue.</summary>
    public static string ToJson(string name, Map map, Point3D origin, IReadOnlyList<ClusterFLayoutPiece> pieces, string source)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("map", map.Name);
            w.WriteString("name", name);
            w.WriteString("source", source ?? "");
            w.WriteStartObject("origin");
            w.WriteNumber("x", origin.X);
            w.WriteNumber("y", origin.Y);
            w.WriteNumber("z", origin.Z);
            w.WriteEndObject();
            w.WriteStartArray("components");
            foreach (var p in pieces)
            {
                w.WriteStartObject();
                w.WriteString("area", p.Area);
                w.WriteString("id", $"0x{p.ItemID:X4}");
                w.WriteString("name", p.Name);
                w.WriteNumber("x", p.X);
                w.WriteNumber("y", p.Y);
                w.WriteNumber("z", p.Z);
                w.WriteNumber("hue", p.Hue);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    // -- parse and check -------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads a layout file and checks all of it: the map, the origin, and every component. Returns the layout, or null
    /// with every problem found (each names its component number), so a bad line stops the stamp before it starts.
    /// </summary>
    public static ClusterFLayout Parse(string json, out List<string> errors)
    {
        errors = new List<string>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json ?? "");
        }
        catch (JsonException e)
        {
            errors.Add("the file is not JSON: " + e.Message);
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("the file is not a JSON object");
                return null;
            }

            var mapName = Str(root, "map");
            var map = Map.Maps.FirstOrDefault(m => m != null && m != Map.Internal && m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
            if (map == null)
            {
                errors.Add($"map '{mapName}' is not a facet of this shard");
            }

            var origin = Point3D.Zero;
            if (!root.TryGetProperty("origin", out var o) || o.ValueKind != JsonValueKind.Object ||
                !Int(o, "x", out var ox) || !Int(o, "y", out var oy) || !Int(o, "z", out var oz))
            {
                errors.Add("origin needs whole-number x, y and z");
            }
            else
            {
                origin = new Point3D(ox, oy, oz);
            }

            var pieces = new List<ClusterFLayoutPiece>();
            if (!root.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array || comps.GetArrayLength() == 0)
            {
                errors.Add("components needs to be a list with at least one entry");
            }
            else if (comps.GetArrayLength() > MaxPieces)
            {
                errors.Add($"{comps.GetArrayLength()} components; a layout holds at most {MaxPieces}");
            }
            else
            {
                var n = 0;
                foreach (var c in comps.EnumerateArray())
                {
                    n++;
                    var bad = CheckComponent(c, out var piece);
                    if (bad != null)
                    {
                        errors.Add($"component {n}: {bad}");
                    }
                    else
                    {
                        pieces.Add(piece);
                    }
                }
            }

            return errors.Count > 0 ? null : new ClusterFLayout(Str(root, "name"), map, origin, pieces);
        }
    }

    private static string CheckComponent(JsonElement c, out ClusterFLayoutPiece piece)
    {
        piece = null;
        if (c.ValueKind != JsonValueKind.Object)
        {
            return "not an object";
        }

        var area = Str(c, "area");
        if (string.IsNullOrWhiteSpace(area))
        {
            return "no area";
        }

        var id = Str(c, "id");
        if (id == null || !HexRe.IsMatch(id))
        {
            return $"id '{id}' is not hex like 0x0A99";
        }

        var itemId = Convert.ToInt32(id[2..], 16);
        if (itemId < 1 || itemId > MaxItem)
        {
            return $"id {id} is not an item graphic";
        }

        if (!Int(c, "x", out var x) || !Int(c, "y", out var y) || !Int(c, "z", out var z))
        {
            return "x, y and z need to be whole numbers";
        }

        if (z < sbyte.MinValue || z > sbyte.MaxValue)
        {
            return $"z {z} is outside -128 to 127";
        }

        var hue = 0;
        if (c.TryGetProperty("hue", out var h) && (h.ValueKind != JsonValueKind.Number || !h.TryGetInt32(out hue) || hue < 0 || hue > 0xFFFF))
        {
            return "hue needs to be a whole number from 0 to 65535";
        }

        piece = new ClusterFLayoutPiece(area.Trim(), itemId, Str(c, "name") ?? "", x, y, z, hue);
        return null;
    }

    private static string Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Int(JsonElement e, string key, out int value)
    {
        value = 0;
        return e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out value);
    }

    // -- stamp, nudge, undo ----------------------------------------------------------------------------------------

    /// <summary>
    /// Places a checked layout so its origin lands on <paramref name="at"/>. Each piece is a new Static of its ID and
    /// hue. The list of placed items is on the undo stack before the first is made, and each item joins it before it is
    /// moved into the world, so if anything stops the stamp part way, undo still removes all of them.
    /// <paramref name="beforePlace"/> is for the facts: it runs before each placement and may throw.
    /// </summary>
    public static ClusterFStampResult Stamp(Mobile by, ClusterFLayout layout, Map map, Point3D at, Action<int> beforePlace = null)
    {
        var dx = at.X - layout.Origin.X;
        var dy = at.Y - layout.Origin.Y;
        var dz = at.Z - layout.Origin.Z;
        var off = layout.Pieces.Where(p => p.Z + dz < sbyte.MinValue || p.Z + dz > sbyte.MaxValue).ToList();
        if (off.Count > 0)
        {
            return new ClusterFStampResult(0, $"{off.Count} pieces would land outside Z -128 to 127 there; nothing was placed");
        }

        var placed = new List<Item>(layout.Pieces.Count);
        UndoStack(by).Push(placed);
        var i = 0;
        try
        {
            foreach (var p in layout.Pieces)
            {
                var s = new Static(p.ItemID) { Hue = p.Hue };
                placed.Add(s);
                beforePlace?.Invoke(i);
                s.MoveToWorld(new Point3D(p.X + dx, p.Y + dy, p.Z + dz), map);
                i++;
            }
        }
        catch (Exception e)
        {
            return new ClusterFStampResult(i, $"stopped after {i} of {layout.Pieces.Count} pieces ({e.Message}); [StampLayout undo removes what was placed");
        }

        return new ClusterFStampResult(i, null);
    }

    public static int UndoCount(Mobile by) => _undo.TryGetValue(by.Serial, out var s) ? s.Count : 0;

    /// <summary>Removes everything the newest stamp placed, including one that stopped part way. Returns how many.</summary>
    public static int Undo(Mobile by)
    {
        if (!_undo.TryGetValue(by.Serial, out var stack) || stack.Count == 0)
        {
            return -1;
        }

        var n = 0;
        foreach (var item in stack.Pop())
        {
            if (!item.Deleted)
            {
                item.Delete();
                n++;
            }
        }

        return n;
    }

    /// <summary>Moves everything the newest stamp placed by dx, dy, dz. Returns how many moved, or -1 with nothing to move.</summary>
    public static int Nudge(Mobile by, int dx, int dy, int dz)
    {
        if (!_undo.TryGetValue(by.Serial, out var stack) || stack.Count == 0)
        {
            return -1;
        }

        var n = 0;
        foreach (var item in stack.Peek())
        {
            if (!item.Deleted && item.Map != null && item.Map != Map.Internal)
            {
                item.Location = new Point3D(item.X + dx, item.Y + dy, Math.Clamp(item.Z + dz, sbyte.MinValue, sbyte.MaxValue));
                n++;
            }
        }

        return n;
    }

    private static Stack<List<Item>> UndoStack(Mobile by)
    {
        if (!_undo.TryGetValue(by.Serial, out var stack))
        {
            _undo[by.Serial] = stack = new Stack<List<Item>>();
        }

        return stack;
    }

    // -- the commands ----------------------------------------------------------------------------------------------

    [Usage("CaptureLayout <name> [area] [minZ maxZ]")]
    [Description("cc-P98: target two corners (at most 64 x 64) to save their items and map statics as layout JSON in the world folder's layout-captures. Skips houses, boats, doors, addon bases and hidden items.")]
    [ShardCommand(CommandCategory.DevTool, Summary = "Saves a box of the world as layout JSON (a file in the world folder); changes nothing in the world.")]
    private static void OnCapture(CommandEventArgs e)
    {
        var from = e.Mobile;
        var name = e.Length > 0 ? e.GetString(0) : null;
        var bad = CheckName(name);
        if (bad != null)
        {
            from.SendMessage(0x22, "Usage: [CaptureLayout <name> [area] [minZ maxZ]. " + bad + ".");
            return;
        }

        if (File.Exists(PathOf(name)))
        {
            from.SendMessage(0x22, $"A layout called {name} already exists ({PathOf(name)}). Choose another name.");
            return;
        }

        var area = e.Length > 1 ? e.GetString(1) : "main";
        int minZ = sbyte.MinValue, maxZ = sbyte.MaxValue;
        if (e.Length > 3)
        {
            minZ = e.GetInt32(2);
            maxZ = e.GetInt32(3);
        }

        from.SendMessage($"Capture {name}: target the two corners of the box (at most {MaxSide} x {MaxSide}).");
        BoundingBoxPicker.Begin(from, (map, start, end) =>
        {
            var pieces = Capture(map, new Point2D(start.X, start.Y), new Point2D(end.X, end.Y), minZ, maxZ, area, out var refused);
            if (pieces == null)
            {
                from.SendMessage(0x22, "Not captured: " + refused + ".");
                return;
            }

            if (pieces.Count == 0)
            {
                from.SendMessage(0x22, "Nothing to capture in that box.");
                return;
            }

            var origin = new Point3D(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), pieces.Min(p => p.Z));
            var source = $"[CaptureLayout by {from.Name} at {Core.Now:yyyy-MM-ddTHH:mm:ssZ}, box {start.X},{start.Y} to {end.X},{end.Y}, z {minZ} to {maxZ}";
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(name), ToJson(name, map, origin, pieces, source), new UTF8Encoding(false));
            from.SendMessage(0x43, $"Captured {pieces.Count} pieces as {name}: {PathOf(name)}. [StampLayout {name} places it.");
        });
    }

    [Usage("StampLayout <name> | check <name> | nudge <dx> <dy> [dz] | undo")]
    [Description("cc-P98: places a captured layout at a target (its origin lands there) as plain statics, after checking the whole file. check only checks it; nudge moves the last stamp; undo removes it, even a stamp that stopped part way.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Duplicates, Shard = CommandShard.TestFirst, DryRun = "check",
        Summary = "Places a captured layout as statics where you target. Each run places another copy; undo removes the last.")]
    private static void OnStamp(CommandEventArgs e)
    {
        var from = e.Mobile;
        var first = e.Length > 0 ? e.GetString(0) : "";

        if (first.Equals("undo", StringComparison.OrdinalIgnoreCase))
        {
            var n = Undo(from);
            from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing of yours to undo." : $"Undone: {n} pieces removed. {UndoCount(from)} earlier stamp(s) left to undo.");
            return;
        }

        if (first.Equals("nudge", StringComparison.OrdinalIgnoreCase))
        {
            if (e.Length < 3)
            {
                from.SendMessage(0x22, "Usage: [StampLayout nudge <dx> <dy> [dz]");
                return;
            }

            var n = Nudge(from, e.GetInt32(1), e.GetInt32(2), e.Length > 3 ? e.GetInt32(3) : 0);
            from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing of yours to nudge." : $"Moved {n} pieces.");
            return;
        }

        var checkOnly = first.Equals("check", StringComparison.OrdinalIgnoreCase);
        var name = checkOnly ? (e.Length > 1 ? e.GetString(1) : null) : first;
        var bad = CheckName(name);
        if (bad != null)
        {
            from.SendMessage(0x22, "Usage: [StampLayout <name> | check <name> | nudge <dx> <dy> [dz] | undo. " + bad + ".");
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(PathOf(name));
        }
        catch (IOException)
        {
            from.SendMessage(0x22, $"There is no layout {name} in {Folder}.");
            return;
        }

        var layout = Parse(json, out var errors);
        if (layout == null)
        {
            from.SendMessage(0x22, $"{name} is not placed: {errors.Count} problem(s). The first: {errors[0]}");
            foreach (var x in errors.Take(5))
            {
                from.SendMessage(0x22, x);
            }

            return;
        }

        if (checkOnly)
        {
            from.SendMessage(0x43, $"{name} checks out: {layout.Pieces.Count} pieces, captured on {layout.Map.Name}. Nothing was placed.");
            return;
        }

        from.SendMessage($"Target where {name}'s origin goes ({layout.Pieces.Count} pieces).");
        from.Target = new StampTarget(layout);
    }

    private sealed class StampTarget : Target
    {
        private readonly ClusterFLayout _layout;

        public StampTarget(ClusterFLayout layout) : base(-1, true, TargetFlags.None) => _layout = layout;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D p || from.Map == null || from.Map == Map.Internal)
            {
                return;
            }

            var r = Stamp(from, _layout, from.Map, new Point3D(p.X, p.Y, p.Z));
            if (r.Error != null)
            {
                from.SendMessage(0x22, r.Error);
                return;
            }

            var note = from.Map == _layout.Map ? "" : $" (captured on {_layout.Map.Name}, placed on {from.Map.Name})";
            from.SendMessage(0x43, $"Placed {r.Placed} pieces{note}. [StampLayout nudge <dx> <dy> [dz] moves them; [StampLayout undo removes them.");
        }
    }
}

public sealed record ClusterFLayoutPiece(string Area, int ItemID, string Name, int X, int Y, int Z, int Hue);

public sealed record ClusterFLayout(string Name, Map Map, Point3D Origin, IReadOnlyList<ClusterFLayoutPiece> Pieces);

public sealed record ClusterFStampResult(int Placed, string Error);
