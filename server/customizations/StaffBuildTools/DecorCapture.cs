// DecorCapture.cs
//
// cc-P812 (P798 rank 1): the Visual Builder's capture. A staff member decorates in game, runs [VBuild capture <name> [build|world],
// targets two corners, and the pieces standing in that box are written as the shard's decoration data: pinned's [Decorate
// text, the format of server/data/Decoration and of P734's builds. A Golem (or staff) can then seed it:
//
//   build (the default)  coordinates relative to the box's north-west corner at the lower of the two targeted heights, so
//                        the file goes in server/data/Decoration/Builds and [PlaceBuild <name> puts it anywhere;
//   world                the world's own coordinates, so the file's lists can go into server/data/Decoration/<Facet>/
//                        and [Decorate (or a seeder) places them where they were.
//
// Each piece keeps its ID, place, hue and name. A door keeps its facing ("DarkWoodDoor (Facing=WestCCW)"), found by trying
// each facing on a probe of the same door type, and is captured closed. A plain item is written as its own type when it
// can be made without arguments; anything else, and every addon part, is written as a plain Static of the same ID (the
// look kept, the behaviour not), and the count of those is reported. The map's own statics are left out unless the
// config says otherwise. Nothing is placed or deleted by a capture.
//
// Ideas from Hammerhand's AddonGenerator through High Seas (after CEO's YAAAG) and Addon2Static (third-party, reference
// only): capture a box into something that can be placed again, and break addons into statics. No code was taken, and
// this writes data, not generated C#. It sits beside P98's [CaptureLayout, which writes the layout JSON the design
// generators read; this one writes what the seeders and [PlaceBuild read.
//
// Files go to <world folder>/decor-captures/<name>.cfg, beside Saves, never into a repo. Staff only (GameMaster).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Server.Items;
using Server.Targeting;

namespace Server;

public sealed record DecorPiece(string Header, int X, int Y, int Z);

public static class DecorCapture
{
    private static StaffBuildToolsSettings S => StaffBuildToolsSettings.Current;

    public static string Folder =>
        Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", S.CaptureFolder);

    public static string PathOf(string name) => Path.Combine(Folder, name + ".cfg");

    private static bool Skipped(Item item)
    {
        for (var t = item.GetType(); t != null && t != typeof(Item); t = t.BaseType)
        {
            if (S.CaptureSkipTypes.Contains(t.Name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A door's facing, found by trying each on a probe of its own type; null when its type takes none.</summary>
    public static DoorFacing? FacingOf(BaseDoor door)
    {
        foreach (var facing in Enum.GetValues<DoorFacing>())
        {
            BaseDoor probe = null;
            try
            {
                probe = Activator.CreateInstance(door.GetType(), facing) as BaseDoor;
                if (probe?.ClosedId == door.ClosedId)
                {
                    return facing;
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                probe?.Delete();
            }
        }

        return null;
    }

    private static string Clean(string text) => text?.Replace(";", ",").Replace("(", "").Replace(")", "").Trim();

    /// <summary>The [Decorate type line for one item; flattened is true when it is kept only as a Static of its ID.</summary>
    public static string Header(Item item, out bool flattened)
    {
        flattened = false;
        var props = new List<string>();
        string head;
        var type = item.GetType();

        if (item is BaseDoor door && FacingOf(door) is { } facing)
        {
            head = type.Name;
            props.Add($"Facing={facing}");
        }
        else if (type == typeof(Static))
        {
            head = $"Static 0x{item.ItemID:X4}";
        }
        else if (item is AddonComponent || type.GetConstructor(Type.EmptyTypes) == null)
        {
            head = $"Static 0x{item.ItemID:X4}";
            flattened = true;
        }
        else
        {
            head = $"{type.Name} 0x{item.ItemID:X4}";
        }

        if (item.Hue != 0)
        {
            props.Add($"Hue=0x{item.Hue:X}");
        }

        if (!string.IsNullOrWhiteSpace(item.Name) && Clean(item.Name).Length > 0)
        {
            props.Add($"Name={Clean(item.Name)}");
        }

        return props.Count == 0 ? head : $"{head} ({string.Join(";", props)})";
    }

    /// <summary>The pieces standing in a box; refused (with nothing read) when the box is too big.</summary>
    public static List<DecorPiece> Collect(Map map, Point2D a, Point2D b, out string refused, out int flattened)
    {
        flattened = 0;
        refused = null;
        int x1 = Math.Min(a.X, b.X), x2 = Math.Max(a.X, b.X), y1 = Math.Min(a.Y, b.Y), y2 = Math.Max(a.Y, b.Y);

        if (map == null || map == Map.Internal)
        {
            refused = "there is no map to capture from";
            return null;
        }

        if (x2 - x1 + 1 > S.CaptureMaxSide || y2 - y1 + 1 > S.CaptureMaxSide)
        {
            refused = $"the box is {x2 - x1 + 1} x {y2 - y1 + 1} tiles; the most this captures is {S.CaptureMaxSide} x {S.CaptureMaxSide}";
            return null;
        }

        var pieces = new List<DecorPiece>();
        var items = new List<Item>();
        foreach (var item in map.GetItemsInBounds(new Rectangle2D(x1, y1, x2 - x1 + 1, y2 - y1 + 1)))
        {
            items.Add(item);
        }

        foreach (var item in items)
        {
            if (item.Deleted || item.Parent != null || item.ItemID <= 0 || Skipped(item))
            {
                continue;
            }

            var at = item.Location;
            if (item is BaseDoor { Open: true } d)
            {
                at = new Point3D(at.X - d.Offset.X, at.Y - d.Offset.Y, at.Z - d.Offset.Z);
            }

            pieces.Add(new DecorPiece(Header(item, out var flat), at.X, at.Y, at.Z));
            if (flat)
            {
                flattened++;
            }
        }

        if (S.CaptureMapStatics)
        {
            for (var x = x1; x <= x2; x++)
            {
                for (var y = y1; y <= y2; y++)
                {
                    foreach (var st in map.Tiles.GetStaticTiles(x, y))
                    {
                        var head = st.Hue != 0 ? $"Static 0x{st.ID:X4} (Hue=0x{st.Hue:X})" : $"Static 0x{st.ID:X4}";
                        pieces.Add(new DecorPiece(head, x, y, st.Z));
                    }
                }
            }
        }

        if (pieces.Count > S.CaptureMaxPieces)
        {
            refused = $"the box holds {pieces.Count} pieces; a capture holds at most {S.CaptureMaxPieces}";
            return null;
        }

        return pieces;
    }

    /// <summary>[Decorate text: one list per type line, its pieces sorted, minus <paramref name="origin"/> when given.</summary>
    public static string ToDecorate(IReadOnlyList<DecorPiece> pieces, Point3D? origin, IEnumerable<string> comments)
    {
        var o = origin ?? Point3D.Zero;
        var sb = new StringBuilder();
        foreach (var c in comments ?? [])
        {
            sb.Append("# ").Append(c.Replace("\n", " ")).Append('\n');
        }

        foreach (var group in pieces.GroupBy(p => p.Header).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append('\n').Append(group.Key).Append('\n');
            foreach (var p in group.OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z))
            {
                sb.Append(p.X - o.X).Append(' ').Append(p.Y - o.Y).Append(' ').Append(p.Z - o.Z).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Captures a box and writes the file. Returns the message for the staff member; ok when a file was written.</summary>
    public static string Capture(Mobile from, string name, bool relative, Map map, Point3D a, Point3D b, out bool ok)
    {
        ok = false;
        var bad = ClusterFLayoutCapture.CheckName(name);
        if (bad != null)
        {
            return "Refused: " + bad + ".";
        }

        var pieces = Collect(map, new Point2D(a.X, a.Y), new Point2D(b.X, b.Y), out var refused, out var flattened);
        if (refused != null)
        {
            return "Refused: " + refused + ".";
        }

        if (pieces.Count == 0)
        {
            return "Nothing to capture in that box.";
        }

        var origin = new Point3D(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        var text = ToDecorate(pieces, relative ? origin : null,
        [
            $"{name}: captured by [VBuild capture on {map.Name} from {a.X},{a.Y} to {b.X},{b.Y}" +
            $" by {from?.Name ?? "staff"} at {Core.Now:yyyy-MM-dd HH:mm} UTC.",
            relative
                ? $"Build form: 0 0 0 is {origin.X} {origin.Y} {origin.Z}. Put it in server/data/Decoration/Builds and use [PlaceBuild {name}."
                : $"World form ({map.Name}): its lists can go into server/data/Decoration/{map.Name}/ for [Decorate.",
            $"{pieces.Count} piece(s); {flattened} kept only as a Static of their ID.",
        ]);

        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathOf(name), text);
        ok = true;
        return $"Captured {pieces.Count} piece(s) ({flattened} as plain statics) to {PathOf(name)}.";
    }

    /// <summary>[VBuild capture: two targets, then the file.</summary>
    public static void Begin(Mobile from, string name, bool relative)
    {
        var bad = ClusterFLayoutCapture.CheckName(name);
        if (bad != null)
        {
            from.SendMessage(0x22, "Refused: " + bad + ".");
            return;
        }

        from.SendMessage(0x43, $"Capture '{name}' ({(relative ? "build" : "world")} form): target the first corner.");
        from.BeginTarget(-1, true, TargetFlags.None, (f, t1) =>
        {
            if (t1 is not IPoint3D p1)
            {
                return;
            }

            f.SendMessage(0x43, "Now target the opposite corner.");
            f.BeginTarget(-1, true, TargetFlags.None, (g, t2) =>
            {
                if (t2 is IPoint3D p2)
                {
                    var msg = Capture(g, name, relative, g.Map, new Point3D(p1), new Point3D(p2), out var ok);
                    g.SendMessage(ok ? 0x43 : 0x22, msg);
                }
            });
        });
    }
}
