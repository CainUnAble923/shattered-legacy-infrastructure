using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Server;

/// <summary>
/// cc-P100: the Visual Builder, an art palette for staff. [VBuild opens it: search tile data by name or ID, then paint,
/// fill a box, erase, wipe a box, hue and nudge, all as plain Static items.
///
/// The idea and the shape of it (a palette of every item ID from tile data, paint, fill box, erase, wipe box, nudge,
/// hue, a protected mode that only touches what the session placed, and a world mode that does not) are Kamras's
/// ServUO Visual Builder, Architect Edition (servuo.dev; kept for reference at D:\UO\third-party\code\visual-builder).
/// This is a rewrite for ModernUO, not a copy, and it changes what that tool did in the ways the cc-P93 assessment
/// asked for:
///   - erase and wipe RECORD every item they delete (type, ID, hue, name, location, map) before deleting any, so undo
///     puts them back; the original's undo only removed what it had placed, so a wipe could not be undone;
///   - world mode (erase and wipe of Static items the tool did not place) is off by default, Administrator only, and
///     off again after a restart; it only ever touches items whose type is exactly Static, the one type a record of
///     ID, hue, name and place restores in full;
///   - every action, placing, removing, moving and re-hueing alike, is one undo step, and the undo history is saved
///     with the world (ClusterFVisualBuilderStore), so it survives a restart and always agrees with the save;
///   - the fill box is capped at 20 x 20, the wipe box at 50 x 50, both refused before anything is touched.
/// It sits beside pinned's [Tile, [Wipe and [Add, and P98's [StampLayout; it adds the visual palette.
/// </summary>
public static class ClusterFVisualBuilder
{
    public const int MaxFillSide = 20;
    public const int MaxWipeSide = 50;
    public const int MaxUndo = 100;
    public const AccessLevel WorldModeAccess = AccessLevel.Administrator;

    private static readonly Dictionary<Serial, ClusterFBuilderSession> _sessions = new();

    internal static Dictionary<Serial, ClusterFBuilderSession> Sessions => _sessions;

    public static void Configure()
    {
        CommandSystem.Register("VBuild", AccessLevel.GameMaster, OnCommand);
    }

    public static ClusterFBuilderSession Get(Mobile by)
    {
        if (!_sessions.TryGetValue(by.Serial, out var s))
        {
            _sessions[by.Serial] = s = new ClusterFBuilderSession(by.Serial);
        }

        return s;
    }

    /// <summary>Forgets every session (the facts start clean). Items stay where they are.</summary>
    internal static void ResetAll() => _sessions.Clear();

    // -- the rules -------------------------------------------------------------------------------------------------

    /// <summary>Why world mode cannot be turned on for <paramref name="by"/>, or null.</summary>
    public static string SetWorldMode(Mobile by, bool on)
    {
        if (on && by.AccessLevel < WorldModeAccess)
        {
            return $"world mode is {WorldModeAccess} only";
        }

        Get(by).WorldMode = on;
        return null;
    }

    /// <summary>
    /// Whether erase, wipe or hue may touch <paramref name="item"/>: in the default protected mode only a Static this
    /// session placed; in world mode also any item whose type is exactly Static. Never an addon, door, multi, container
    /// or anything with properties a record could not restore.
    /// </summary>
    public static bool CanModify(Mobile by, Item item)
    {
        if (item == null || item.Deleted || item.Parent != null || item.GetType() != typeof(Static))
        {
            return false;
        }

        var s = Get(by);
        if (s.Placed.Contains(item.Serial))
        {
            return true;
        }

        return s.WorldMode && by.AccessLevel >= WorldModeAccess;
    }

    public static string CheckBox(Point2D a, Point2D b, int maxSide, string what)
    {
        var w = Math.Abs(b.X - a.X) + 1;
        var h = Math.Abs(b.Y - a.Y) + 1;
        return w > maxSide || h > maxSide ? $"the box is {w} x {h} tiles; the most {what} takes is {maxSide} x {maxSide}" : null;
    }

    // The highest item graphic: TileData's once it has loaded, else the full 16 bits (the facts' host loads no tile data).
    public static int MaxItem => TileData.MaxItemValue > 0 ? TileData.MaxItemValue : 0xFFFF;

    public static string CheckArt(int itemId, int hue) =>
        itemId <= 0 || itemId > MaxItem ? $"item ID {itemId} is not one this tile data has (1 to {MaxItem})"
        : hue < 0 || hue > 0xFFFF ? $"hue {hue} is not 0 to 65535"
        : null;

    // -- the tools -------------------------------------------------------------------------------------------------

    /// <summary>Places one Static. One undo step.</summary>
    public static Static Paint(Mobile by, int itemId, int hue, Map map, Point3D at, out string refused)
    {
        refused = CheckArt(itemId, hue) ?? CheckZ(at.Z);
        if (refused != null)
        {
            return null;
        }

        var s = Get(by);
        var step = new ClusterFBuilderStep("paint");
        s.Push(step);
        var piece = new Static(itemId) { Hue = hue };
        step.Placed.Add(piece.Serial);
        s.Placed.Add(piece.Serial);
        piece.MoveToWorld(at, map);
        return piece;
    }

    /// <summary>Fills a box (both corners included) at one Z. Refused over 20 x 20 before anything is placed. One undo step.</summary>
    public static int Fill(Mobile by, int itemId, int hue, Map map, Point2D a, Point2D b, int z, out string refused)
    {
        refused = CheckBox(a, b, MaxFillSide, "a fill") ?? CheckArt(itemId, hue) ?? CheckZ(z);
        if (refused != null)
        {
            return 0;
        }

        var s = Get(by);
        var step = new ClusterFBuilderStep("fill");
        s.Push(step);
        var n = 0;
        for (var y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
        {
            for (var x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
            {
                // Each piece is on the undo step before it is placed, so a fill that stops part way still undoes.
                var piece = new Static(itemId) { Hue = hue };
                step.Placed.Add(piece.Serial);
                s.Placed.Add(piece.Serial);
                piece.MoveToWorld(new Point3D(x, y, z), map);
                n++;
            }
        }

        return n;
    }

    /// <summary>Records then deletes one item. One undo step. Returns false (with why) when the rules refuse it.</summary>
    public static bool Erase(Mobile by, Item item, out string refused)
    {
        refused = CanModify(by, item) ? null : WhyNot(by, item);
        if (refused != null)
        {
            return false;
        }

        Remove(by, new List<Item> { item }, "erase");
        return true;
    }

    /// <summary>
    /// Records then deletes every item in the box (both corners included, minZ to maxZ) the rules allow. Refused over
    /// 50 x 50 before anything is read. Every record is made before the first item is deleted. One undo step.
    /// </summary>
    public static int Wipe(Mobile by, Map map, Point2D a, Point2D b, int minZ, int maxZ, out string refused)
    {
        refused = CheckBox(a, b, MaxWipeSide, "a wipe");
        if (refused != null)
        {
            return 0;
        }

        int x1 = Math.Min(a.X, b.X), x2 = Math.Max(a.X, b.X), y1 = Math.Min(a.Y, b.Y), y2 = Math.Max(a.Y, b.Y);
        var hit = new List<Item>();
        foreach (var item in map.GetItemsInBounds<Item>(new Rectangle2D(x1, y1, x2 - x1 + 1, y2 - y1 + 1), true))
        {
            if (item.X >= x1 && item.X <= x2 && item.Y >= y1 && item.Y <= y2 && item.Z >= minZ && item.Z <= maxZ && CanModify(by, item))
            {
                hit.Add(item);
            }
        }

        if (hit.Count > 0)
        {
            Remove(by, hit, "wipe");
        }

        return hit.Count;
    }

    /// <summary>Re-hues one item the rules allow. One undo step.</summary>
    public static bool SetHue(Mobile by, Item item, int hue, out string refused)
    {
        refused = (CanModify(by, item) ? null : WhyNot(by, item)) ?? CheckArt(item.ItemID, hue);
        if (refused != null)
        {
            return false;
        }

        var step = new ClusterFBuilderStep("hue");
        step.Rehued.Add(new ClusterFBuilderHue(item.Serial, item.Hue));
        Get(by).Push(step);
        item.Hue = hue;
        return true;
    }

    /// <summary>Moves what the newest placing step (paint or fill) placed by dx, dy, dz. One undo step. -1: nothing to move.</summary>
    public static int Nudge(Mobile by, int dx, int dy, int dz)
    {
        var s = Get(by);
        var last = s.Steps.LastOrDefault(t => t.Placed.Count > 0);
        if (last == null)
        {
            return -1;
        }

        var step = new ClusterFBuilderStep("nudge");
        foreach (var serial in last.Placed)
        {
            if (World.FindItem(serial) is { Deleted: false } item && item.Map != null && item.Map != Map.Internal)
            {
                step.Moved.Add(new ClusterFBuilderMove(serial, item.Location));
                item.Location = new Point3D(item.X + dx, item.Y + dy, Math.Clamp(item.Z + dz, sbyte.MinValue, sbyte.MaxValue));
            }
        }

        if (step.Moved.Count == 0)
        {
            return -1;
        }

        s.Push(step);
        return step.Moved.Count;
    }

    /// <summary>
    /// Reverses the newest step: removes what it placed, puts back what it removed (as new Statics, each as it was),
    /// moves back what it moved and re-hues what it re-hued. Returns how many items it touched, or -1 with nothing to undo.
    /// </summary>
    public static int Undo(Mobile by)
    {
        var s = Get(by);
        if (s.Steps.Count == 0)
        {
            return -1;
        }

        var step = s.Steps[^1];
        s.Steps.RemoveAt(s.Steps.Count - 1);
        var n = 0;

        foreach (var serial in step.Placed)
        {
            s.Placed.Remove(serial);
            if (World.FindItem(serial) is { Deleted: false } item)
            {
                item.Delete();
                n++;
            }
        }

        foreach (var r in step.Removed)
        {
            var map = Map.Maps[r.MapIndex];
            if (map == null)
            {
                continue;
            }

            var back = new Static(r.ItemID) { Hue = r.Hue, Name = r.Name };
            back.MoveToWorld(new Point3D(r.X, r.Y, r.Z), map);
            if (r.WasSession)
            {
                s.Placed.Add(back.Serial);
            }

            // Older steps that name the deleted item now mean the one put back.
            s.Rename(r.Serial, back.Serial);
            n++;
        }

        foreach (var m in step.Moved)
        {
            if (World.FindItem(m.Serial) is { Deleted: false } item)
            {
                item.Location = m.From;
                n++;
            }
        }

        foreach (var h in step.Rehued)
        {
            if (World.FindItem(h.Serial) is { Deleted: false } item)
            {
                item.Hue = h.Hue;
                n++;
            }
        }

        return n;
    }

    private static void Remove(Mobile by, List<Item> items, string kind)
    {
        var s = Get(by);
        var step = new ClusterFBuilderStep(kind);
        foreach (var item in items)
        {
            step.Removed.Add(new ClusterFBuilderRecord(
                item.Serial, item.GetType().FullName, item.ItemID, item.Hue, item.Name, item.X, item.Y, item.Z,
                item.Map.MapIndex, s.Placed.Contains(item.Serial)));
        }

        // Every record is on the undo step before the first delete.
        s.Push(step);
        foreach (var item in items)
        {
            s.Placed.Remove(item.Serial);
            item.Delete();
        }
    }

    private static string WhyNot(Mobile by, Item item)
    {
        if (item == null || item.Deleted || item.Parent != null)
        {
            return "that is not an item in the world";
        }

        if (item.GetType() != typeof(Static))
        {
            return $"only plain Static items can be changed here, not a {item.GetType().Name}";
        }

        return Get(by).WorldMode && by.AccessLevel >= WorldModeAccess
            ? "that cannot be changed"
            : "that was not placed by this builder session (world mode, Administrator only, changes others)";
    }

    private static string CheckZ(int z) => z < sbyte.MinValue || z > sbyte.MaxValue ? $"z {z} is outside -128 to 127" : null;

    // -- the palette's catalog ---------------------------------------------------------------------------------------

    private static int[] _defined;

    /// <summary>
    /// The item IDs whose tile data names them, from 1 to <paramref name="max"/>, that match <paramref name="filter"/>:
    /// empty is all of them; a number (decimal, or hex with 0x) matches that ID exactly; any other text matches names
    /// containing it, case ignored. Pure, so the facts can hand it a table (the test host loads no tile data).
    /// </summary>
    public static List<int> Search(ItemData[] table, int max, string filter)
    {
        var f = (filter ?? "").Trim();
        var list = new List<int>();
        int? exact = TryParseId(f, out var id) ? id : null;
        for (var i = 1; i <= Math.Min(max, table.Length - 1); i++)
        {
            var name = table[i].Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (f.Length == 0 || exact == i || (exact == null && name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(i);
            }
        }

        return list;
    }

    public static IReadOnlyList<int> Palette(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return _defined ??= Search(TileData.ItemTable, TileData.MaxItemValue, "").ToArray();
        }

        return Search(TileData.ItemTable, TileData.MaxItemValue, filter);
    }

    public static bool TryParseId(string text, out int id)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id) && id > 0;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }

    public static string NameOf(int itemId) =>
        itemId > 0 && itemId < TileData.ItemTable.Length && !string.IsNullOrWhiteSpace(TileData.ItemTable[itemId].Name)
            ? TileData.ItemTable[itemId].Name : "unnamed";

    // -- the command -----------------------------------------------------------------------------------------------

    [Usage("VBuild [status | undo | nudge <dx> <dy> [dz] | world on | world off | forget]")]
    [Description("cc-P100: opens the Visual Builder palette (after Kamras): search tile data, then paint, fill a box (20 x 20 at most), erase, wipe a box (50 x 50), hue and nudge, as plain statics. Erase and wipe record what they delete, so undo puts it back; the undo history is saved with the world. World mode, which lets erase and wipe touch statics the builder did not place, is Administrator only and off by default.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "status",
        Summary = "Opens the staff art palette. Opening it places nothing; what you paint, fill or wipe with it is one undo step each.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var first = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        var s = Get(from);
        switch (first)
        {
            case "":
                {
                    Open(from);
                    return;
                }
            case "status":
                {
                    from.SendMessage(0x43, $"Visual Builder: {s.Steps.Count} step(s) to undo, {s.Placed.Count} placed item(s) tracked, world mode {(s.WorldMode ? "ON" : "off")}. Nothing was changed.");
                    return;
                }
            case "undo":
                {
                    var n = Undo(from);
                    from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing of yours to undo." : $"Undone: {n} item(s). {s.Steps.Count} step(s) left.");
                    return;
                }
            case "nudge":
                {
                    if (e.Length < 3)
                    {
                        from.SendMessage(0x22, "Usage: [VBuild nudge <dx> <dy> [dz]");
                        return;
                    }

                    var n = Nudge(from, e.GetInt32(1), e.GetInt32(2), e.Length > 3 ? e.GetInt32(3) : 0);
                    from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing placed to nudge." : $"Moved {n} item(s). [VBuild undo moves them back.");
                    return;
                }
            case "world":
                {
                    var on = e.Length > 1 && e.GetString(1).Equals("on", StringComparison.OrdinalIgnoreCase);
                    var why = SetWorldMode(from, on);
                    if (why != null)
                    {
                        from.SendMessage(0x22, "Refused: " + why + ".");
                        return;
                    }

                    if (on)
                    {
                        CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} turned Visual Builder world mode on");
                    }

                    from.SendMessage(on ? 0x22 : 0x43, on
                        ? "World mode ON: erase and wipe now also remove Static items the builder did not place. Each is recorded, and [VBuild undo puts it back. [VBuild world off ends it."
                        : "World mode off: erase and wipe only touch what this session placed.");
                    return;
                }
            case "forget":
                {
                    _sessions.Remove(from.Serial);
                    from.SendMessage(0x43, "Forgot your builder session: nothing left to undo. What you placed stays in the world.");
                    return;
                }
            default:
                {
                    from.SendMessage(0x22, "Usage: [VBuild [status | undo | nudge <dx> <dy> [dz] | world on | world off | forget]");
                    return;
                }
        }
    }

    public static void Open(Mobile from)
    {
        from.CloseGump<ClusterFVisualBuilderGump>();
        from.SendGump(new ClusterFVisualBuilderGump(from));
    }

    // Placement on a target: the ground, a map static or an item (on top of it), plus the session's Z offset.
    internal static bool TryPlacement(object targeted, int zOffset, out Point3D at)
    {
        at = Point3D.Zero;
        if (targeted is not IPoint3D p)
        {
            return false;
        }

        var z = p.Z;
        if (targeted is Item item)
        {
            z = item.Z + item.ItemData.CalcHeight;
        }

        at = new Point3D(p.X, p.Y, Math.Clamp(z + zOffset, sbyte.MinValue, sbyte.MaxValue));
        return true;
    }
}

public enum ClusterFBuilderTool
{
    Paint,
    Fill,
    Erase,
    Wipe,
    Hue
}

/// <summary>One staff member's builder state. Only Placed, the steps and the art choice are saved; world mode is not.</summary>
public sealed class ClusterFBuilderSession
{
    public ClusterFBuilderSession(Serial owner) => Owner = owner;

    public Serial Owner { get; }
    public HashSet<Serial> Placed { get; } = new();
    public List<ClusterFBuilderStep> Steps { get; } = new();
    public bool WorldMode { get; set; }
    public int ItemID { get; set; } = 0x0519; // a cobblestone floor tile, so a first paint shows something
    public int Hue { get; set; }
    public int ZOffset { get; set; }
    public string Search { get; set; } = "";
    public int Page { get; set; }

    public void Push(ClusterFBuilderStep step)
    {
        Steps.Add(step);
        if (Steps.Count > ClusterFVisualBuilder.MaxUndo)
        {
            Steps.RemoveAt(0);
        }
    }

    // An undo put back a deleted item under a new serial: older steps mean the new one now.
    internal void Rename(Serial from, Serial to)
    {
        foreach (var t in Steps)
        {
            for (var i = 0; i < t.Placed.Count; i++)
            {
                if (t.Placed[i] == from)
                {
                    t.Placed[i] = to;
                }
            }

            for (var i = 0; i < t.Moved.Count; i++)
            {
                if (t.Moved[i].Serial == from)
                {
                    t.Moved[i] = t.Moved[i] with { Serial = to };
                }
            }

            for (var i = 0; i < t.Rehued.Count; i++)
            {
                if (t.Rehued[i].Serial == from)
                {
                    t.Rehued[i] = t.Rehued[i] with { Serial = to };
                }
            }

            for (var i = 0; i < t.Removed.Count; i++)
            {
                if (t.Removed[i].Serial == from)
                {
                    t.Removed[i] = t.Removed[i] with { Serial = to };
                }
            }
        }
    }
}

/// <summary>One undo step: what it placed (removed on undo), removed (put back), moved (moved back) and re-hued.</summary>
public sealed class ClusterFBuilderStep
{
    public ClusterFBuilderStep(string kind) => Kind = kind;

    public string Kind { get; }
    public List<Serial> Placed { get; } = new();
    public List<ClusterFBuilderRecord> Removed { get; } = new();
    public List<ClusterFBuilderMove> Moved { get; } = new();
    public List<ClusterFBuilderHue> Rehued { get; } = new();
}

/// <summary>What erase or wipe deleted, recorded before the delete: enough to put a Static back exactly.</summary>
public sealed record ClusterFBuilderRecord(
    Serial Serial, string Type, int ItemID, int Hue, string Name, int X, int Y, int Z, int MapIndex, bool WasSession);

public sealed record ClusterFBuilderMove(Serial Serial, Point3D From);

public sealed record ClusterFBuilderHue(Serial Serial, int Hue);

/// <summary>
/// cc-P100: the builder's sessions, saved with the world through pinned's GenericPersistence
/// (Server/Serialization/GenericPersistence.cs), so undo survives a restart and always matches the save it was loaded
/// with: an item a wipe deleted after the last save is still in that save, and so is the record of deleting it, or
/// neither is. World mode is not saved: it is off after every restart. Versioned; an unknown version fails loudly, as
/// ClusterFSeasonalState does.
/// </summary>
public class ClusterFVisualBuilderStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static ClusterFVisualBuilderStore _instance;

    public ClusterFVisualBuilderStore() : base("VisualBuilder", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ClusterFVisualBuilderStore();
    }

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    // Static, so a fact can round-trip the sessions without a second persistence (constructing one registers it).
    public static void WriteTo(IGenericWriter w)
    {
        w.WriteEncodedInt(CurrentVersion);
        var sessions = ClusterFVisualBuilder.Sessions.Values.Where(s => s.Steps.Count > 0 || s.Placed.Count > 0).ToList();
        w.WriteEncodedInt(sessions.Count);
        foreach (var s in sessions)
        {
            w.Write(s.Owner);
            w.WriteEncodedInt(s.ItemID);
            w.WriteEncodedInt(s.Hue);
            w.WriteEncodedInt(s.Placed.Count);
            foreach (var p in s.Placed)
            {
                w.Write(p);
            }

            w.WriteEncodedInt(s.Steps.Count);
            foreach (var t in s.Steps)
            {
                w.Write(t.Kind);
                w.WriteEncodedInt(t.Placed.Count);
                foreach (var p in t.Placed)
                {
                    w.Write(p);
                }

                w.WriteEncodedInt(t.Removed.Count);
                foreach (var r in t.Removed)
                {
                    w.Write(r.Serial);
                    w.Write(r.Type);
                    w.WriteEncodedInt(r.ItemID);
                    w.WriteEncodedInt(r.Hue);
                    w.Write(r.Name);
                    w.Write(r.X);
                    w.Write(r.Y);
                    w.Write(r.Z);
                    w.WriteEncodedInt(r.MapIndex);
                    w.Write(r.WasSession);
                }

                w.WriteEncodedInt(t.Moved.Count);
                foreach (var m in t.Moved)
                {
                    w.Write(m.Serial);
                    w.Write(m.From);
                }

                w.WriteEncodedInt(t.Rehued.Count);
                foreach (var h in t.Rehued)
                {
                    w.Write(h.Serial);
                    w.WriteEncodedInt(h.Hue);
                }
            }
        }
    }

    public static void ReadFrom(IGenericReader r)
    {
        var version = r.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"ClusterFVisualBuilderStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        ClusterFVisualBuilder.ResetAll();
        var n = r.ReadEncodedInt();
        for (var i = 0; i < n; i++)
        {
            var s = new ClusterFBuilderSession(r.ReadSerial())
            {
                ItemID = r.ReadEncodedInt(),
                Hue = r.ReadEncodedInt()
            };
            var placed = r.ReadEncodedInt();
            for (var j = 0; j < placed; j++)
            {
                s.Placed.Add(r.ReadSerial());
            }

            var steps = r.ReadEncodedInt();
            for (var j = 0; j < steps; j++)
            {
                var t = new ClusterFBuilderStep(r.ReadString());
                var c = r.ReadEncodedInt();
                for (var k = 0; k < c; k++)
                {
                    t.Placed.Add(r.ReadSerial());
                }

                c = r.ReadEncodedInt();
                for (var k = 0; k < c; k++)
                {
                    t.Removed.Add(new ClusterFBuilderRecord(r.ReadSerial(), r.ReadString(), r.ReadEncodedInt(), r.ReadEncodedInt(),
                        r.ReadString(), r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadEncodedInt(), r.ReadBool()));
                }

                c = r.ReadEncodedInt();
                for (var k = 0; k < c; k++)
                {
                    t.Moved.Add(new ClusterFBuilderMove(r.ReadSerial(), r.ReadPoint3D()));
                }

                c = r.ReadEncodedInt();
                for (var k = 0; k < c; k++)
                {
                    t.Rehued.Add(new ClusterFBuilderHue(r.ReadSerial(), r.ReadEncodedInt()));
                }

                s.Steps.Add(t);
            }

            ClusterFVisualBuilder.Sessions[s.Owner] = s;
        }
    }
}

/// <summary>The palette: search, a page of art, the chosen art, hue and Z offset, the tools, nudge and undo.</summary>
public class ClusterFVisualBuilderGump : DynamicGump
{
    private const int Cols = 6;
    private const int Rows = 4;
    private const int PerPage = Cols * Rows;
    private const int LabelHue = 0x480;
    private const int WarnHue = 0x22;

    private const int BSearch = 1, BPrev = 2, BNext = 3, BApply = 4;
    private const int BPaint = 10, BFill = 11, BErase = 12, BWipe = 13, BHue = 14;
    private const int BNudge = 20; // 20 to 25: x-, x+, y-, y+, z-, z+
    private const int BUndo = 30;
    private const int BPick = 1000;

    private readonly Mobile _from;

    public override bool Singleton => true;

    public ClusterFVisualBuilderGump(Mobile from) : base(40, 40) => _from = from;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var s = ClusterFVisualBuilder.Get(_from);
        var items = ClusterFVisualBuilder.Palette(s.Search);
        var pages = Math.Max(1, (items.Count + PerPage - 1) / PerPage);
        s.Page = Math.Clamp(s.Page, 0, pages - 1);

        builder.AddPage();
        builder.AddBackground(0, 0, 700, 470, 9270);
        builder.AddAlphaRegion(12, 12, 676, 446);
        builder.AddLabel(20, 18, LabelHue, "Visual Builder (after Kamras's Architect Edition)");

        builder.AddLabel(20, 46, LabelHue, "Search name or ID:");
        builder.AddBackground(140, 42, 200, 24, 9350);
        builder.AddTextEntry(144, 45, 192, 20, LabelHue, 0, s.Search);
        builder.AddButton(346, 44, 4005, 4007, BSearch);
        builder.AddLabel(380, 46, LabelHue, $"{items.Count} found, page {s.Page + 1} of {pages}");

        for (var i = 0; i < PerPage; i++)
        {
            var index = s.Page * PerPage + i;
            if (index >= items.Count)
            {
                break;
            }

            var id = items[index];
            int x = 20 + i % Cols * 78, y = 76 + i / Cols * 80;
            builder.AddButton(x, y, 2117, 2118, BPick + index);
            builder.AddItem(x + 16, y + 4, id);
            builder.AddLabel(x, y + 58, id == s.ItemID ? 0x35 : LabelHue, $"0x{id:X4}");
        }

        builder.AddButton(20, 404, 4014, 4016, BPrev);
        builder.AddLabel(54, 406, LabelHue, "Previous");
        builder.AddButton(380, 404, 4005, 4007, BNext);
        builder.AddLabel(414, 406, LabelHue, "Next");

        // The chosen art and its settings.
        builder.AddLabel(500, 46, LabelHue, ClusterFVisualBuilder.NameOf(s.ItemID));
        builder.AddItem(560, 70, s.ItemID, s.Hue);
        Entry(ref builder, 500, 130, "Item ID", 1, $"0x{s.ItemID:X4}");
        Entry(ref builder, 500, 156, "Hue", 2, s.Hue.ToString(CultureInfo.InvariantCulture));
        Entry(ref builder, 500, 182, "Z offset", 3, s.ZOffset.ToString(CultureInfo.InvariantCulture));
        builder.AddButton(500, 208, 4005, 4007, BApply);
        builder.AddLabel(534, 210, LabelHue, "Apply");

        Tool(ref builder, 500, 238, BPaint, "Paint");
        Tool(ref builder, 590, 238, BFill, $"Fill box ({ClusterFVisualBuilder.MaxFillSide})");
        Tool(ref builder, 500, 264, BErase, "Erase");
        Tool(ref builder, 590, 264, BWipe, $"Wipe box ({ClusterFVisualBuilder.MaxWipeSide})");
        Tool(ref builder, 500, 290, BHue, "Hue an item");

        builder.AddLabel(500, 320, LabelHue, "Nudge the last placed:");
        string[] arrows = { "X-", "X+", "Y-", "Y+", "Z-", "Z+" };
        for (var i = 0; i < arrows.Length; i++)
        {
            builder.AddButton(500 + i % 3 * 60, 342 + i / 3 * 24, 4005, 4007, BNudge + i);
            builder.AddLabel(534 + i % 3 * 60, 344 + i / 3 * 24, LabelHue, arrows[i]);
        }

        builder.AddButton(500, 400, 4020, 4022, BUndo);
        builder.AddLabel(534, 402, LabelHue, $"Undo ({s.Steps.Count})");
        builder.AddLabel(20, 434, s.WorldMode ? WarnHue : LabelHue, s.WorldMode
            ? "WORLD MODE: erase and wipe also remove statics the builder did not place (recorded; undo puts them back)."
            : "Protected: erase and wipe only touch what this session placed.");
    }

    private static void Entry(ref DynamicGumpBuilder builder, int x, int y, string label, int entry, string text)
    {
        builder.AddLabel(x, y, LabelHue, label);
        builder.AddBackground(x + 70, y - 2, 100, 22, 9350);
        builder.AddTextEntry(x + 74, y, 92, 18, LabelHue, entry, text);
    }

    private static void Tool(ref DynamicGumpBuilder builder, int x, int y, int button, string label)
    {
        builder.AddButton(x, y, 4005, 4007, button);
        builder.AddLabel(x + 34, y + 2, LabelHue, label);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        if (from == null || from.AccessLevel < AccessLevel.GameMaster || info.ButtonID == 0)
        {
            return;
        }

        var s = ClusterFVisualBuilder.Get(from);
        if (ClusterFVisualBuilder.TryParseId(info.GetTextEntry(1), out var id) && id <= ClusterFVisualBuilder.MaxItem)
        {
            s.ItemID = id;
        }

        if (int.TryParse(info.GetTextEntry(2), NumberStyles.None, CultureInfo.InvariantCulture, out var hue) && hue <= 0xFFFF)
        {
            s.Hue = hue;
        }

        if (int.TryParse(info.GetTextEntry(3), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var z))
        {
            s.ZOffset = Math.Clamp(z, sbyte.MinValue, sbyte.MaxValue);
        }

        var b = info.ButtonID;
        switch (b)
        {
            case BSearch:
                {
                    s.Search = (info.GetTextEntry(0) ?? "").Trim();
                    s.Page = 0;
                    break;
                }
            case BPrev:
                {
                    s.Page--;
                    break;
                }
            case BNext:
                {
                    s.Page++;
                    break;
                }
            case >= BNudge and < BNudge + 6:
                {
                    var k = b - BNudge;
                    var d = k % 2 == 0 ? -1 : 1;
                    var n = ClusterFVisualBuilder.Nudge(from, k / 2 == 0 ? d : 0, k / 2 == 1 ? d : 0, k / 2 == 2 ? d : 0);
                    from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing placed to nudge." : $"Moved {n} item(s).");
                    break;
                }
            case BUndo:
                {
                    var n = ClusterFVisualBuilder.Undo(from);
                    from.SendMessage(n < 0 ? 0x22 : 0x43, n < 0 ? "Nothing of yours to undo." : $"Undone: {n} item(s).");
                    break;
                }
            case >= BPaint and <= BHue:
                {
                    ClusterFVisualBuilderTargets.Begin(from, (ClusterFBuilderTool)(b - BPaint));
                    break;
                }
            case >= BPick:
                {
                    var items = ClusterFVisualBuilder.Palette(s.Search);
                    if (b - BPick < items.Count)
                    {
                        s.ItemID = items[b - BPick];
                    }

                    break;
                }
        }

        ClusterFVisualBuilder.Open(from);
    }
}

/// <summary>The targets each tool asks for. The gump stays open; each result is a message and one undo step.</summary>
public static class ClusterFVisualBuilderTargets
{
    public static void Begin(Mobile from, ClusterFBuilderTool tool)
    {
        var s = ClusterFVisualBuilder.Get(from);
        switch (tool)
        {
            case ClusterFBuilderTool.Paint:
                {
                    from.SendMessage(0x35, $"Paint 0x{s.ItemID:X4}: target where it goes. Press Escape to stop.");
                    from.Target = new PaintTarget();
                    break;
                }
            case ClusterFBuilderTool.Erase:
                {
                    from.SendMessage(0x35, "Erase: target a static. It is recorded, and undo puts it back.");
                    from.Target = new ItemTarget(false);
                    break;
                }
            case ClusterFBuilderTool.Hue:
                {
                    from.SendMessage(0x35, $"Hue {s.Hue}: target a static to re-hue.");
                    from.Target = new ItemTarget(true);
                    break;
                }
            case ClusterFBuilderTool.Fill:
            case ClusterFBuilderTool.Wipe:
                {
                    var fill = tool == ClusterFBuilderTool.Fill;
                    var side = fill ? ClusterFVisualBuilder.MaxFillSide : ClusterFVisualBuilder.MaxWipeSide;
                    from.SendMessage(0x35, $"{(fill ? "Fill" : "Wipe")} box: target two corners (at most {side} x {side}).");
                    BoundingBoxPicker.Begin(from, (map, start, end) =>
                    {
                        string refused;
                        int n;
                        if (fill)
                        {
                            n = ClusterFVisualBuilder.Fill(from, s.ItemID, s.Hue, map, new Point2D(start.X, start.Y), new Point2D(end.X, end.Y),
                                Math.Clamp(start.Z + s.ZOffset, sbyte.MinValue, sbyte.MaxValue), out refused);
                        }
                        else
                        {
                            n = ClusterFVisualBuilder.Wipe(from, map, new Point2D(start.X, start.Y), new Point2D(end.X, end.Y),
                                sbyte.MinValue, sbyte.MaxValue, out refused);
                        }

                        from.SendMessage(refused != null ? 0x22 : 0x43, refused != null
                            ? "Refused: " + refused + "."
                            : $"{(fill ? "Filled" : "Wiped")} {n} item(s). Undo {(fill ? "removes" : "puts back")} them.");
                        ClusterFVisualBuilder.Open(from);
                    });
                    break;
                }
        }
    }

    private sealed class PaintTarget : Target
    {
        public PaintTarget() : base(-1, true, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            var s = ClusterFVisualBuilder.Get(from);
            if (!ClusterFVisualBuilder.TryPlacement(targeted, s.ZOffset, out var at) || from.Map == null || from.Map == Map.Internal)
            {
                return;
            }

            if (ClusterFVisualBuilder.Paint(from, s.ItemID, s.Hue, from.Map, at, out var refused) == null)
            {
                from.SendMessage(0x22, "Refused: " + refused + ".");
                return;
            }

            from.Target = new PaintTarget(); // keep painting until Escape
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType) => ClusterFVisualBuilder.Open(from);
    }

    private sealed class ItemTarget : Target
    {
        private readonly bool _hue;

        public ItemTarget(bool hue) : base(-1, false, TargetFlags.None) => _hue = hue;

        protected override void OnTarget(Mobile from, object targeted)
        {
            var item = targeted as Item;
            string refused;
            bool ok;
            if (item == null)
            {
                ok = false;
                refused = targeted is StaticTarget
                    ? "that is a map static, part of the map files; the builder only changes placed items"
                    : "that is not an item";
            }
            else if (_hue)
            {
                ok = ClusterFVisualBuilder.SetHue(from, item, ClusterFVisualBuilder.Get(from).Hue, out refused);
            }
            else
            {
                ok = ClusterFVisualBuilder.Erase(from, item, out refused);
            }

            from.SendMessage(ok ? 0x43 : 0x22, ok ? (_hue ? "Re-hued. Undo puts the old hue back." : "Erased. Undo puts it back.") : "Refused: " + refused + ".");
            ClusterFVisualBuilder.Open(from);
        }
    }
}
