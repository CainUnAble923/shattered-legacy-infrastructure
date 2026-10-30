// PlaceBuild.cs
//
// cc-P734: reusable builds. A build is a decoration file in pinned's own [Decorate format (a type line such as
// "Static 0x0007", "DarkWoodDoor (Facing=WestCCW)" or "Static 0xA854 (Name=...)", then one "x y z" line per piece,
// a blank line between lists), with coordinates RELATIVE to the build's origin: 0 0 0 is the north-west corner of its
// plot at ground level. They live in server/data/Decoration/Builds (the image copies server/data/Decoration to
// Data/ShatteredLegacy/Decoration; [Decorate reads only its facet folders there, never Builds), or in the folder named by
// the setting clusterf.builds.folder, and are placed anywhere with:
//
//   [PlaceBuild <name> [x y z] [dryrun]   at the given origin, or where you stand; dryrun lists what it would place
//   [ClearBuild <name> x y z [dryrun]     removes the immovable items standing exactly on the build's pieces
//   [ListBuilds                           the builds in the folder, with their piece counts
//
// Placing reuses DecorationList (Decorate.cs), so doors link, signs and names work as in [Decorate, and placing again
// skips every piece already there (DecorationList.Generate's FindItem). The builds are the P734 practice pieces, made
// by the rules of shard-migration notes/guides/building-and-decorating.md; a later prompt places one with a command.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Server.Commands;

namespace Server;

public static class PlaceBuild
{
    public static string Folder => ServerConfiguration.GetOrUpdateSetting("clusterf.builds.folder", "Data/ShatteredLegacy/Decoration/Builds");

    private static readonly Regex Coordinate = new(@"^(\s*)(-?\d+)\s+(-?\d+)\s+(-?\d+)(.*)$", RegexOptions.Compiled);

    public static void Configure()
    {
        CommandSystem.Register("PlaceBuild", AccessLevel.Administrator, OnPlace);
        CommandSystem.Register("ClearBuild", AccessLevel.Administrator, OnClear);
        CommandSystem.Register("ListBuilds", AccessLevel.GameMaster, OnList);
    }

    // The build's text with every coordinate line moved by (dx, dy, dz); type lines, comments and blank lines unchanged.
    public static string Shift(string text, int dx, int dy, int dz)
    {
        var sb = new StringBuilder();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var m = Coordinate.Match(raw);
            if (m.Success && !raw.TrimStart().StartsWith('#'))
            {
                sb.Append(m.Groups[1].Value)
                    .Append(int.Parse(m.Groups[2].Value) + dx).Append(' ')
                    .Append(int.Parse(m.Groups[3].Value) + dy).Append(' ')
                    .Append(int.Parse(m.Groups[4].Value) + dz)
                    .Append(m.Groups[5].Value);
            }
            else
            {
                sb.Append(raw);
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    // Every piece's location in the (already shifted) text.
    public static List<Point3D> Points(string text)
    {
        var list = new List<Point3D>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var m = Coordinate.Match(raw);
            if (m.Success && !raw.TrimStart().StartsWith('#'))
            {
                list.Add(new Point3D(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value)));
            }
        }

        return list;
    }

    public static List<DecorationList> Parse(string text)
    {
        var lists = new List<DecorationList>();
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        DecorationList list;
        while ((list = DecorationList.Read(reader)) != null)
        {
            lists.Add(list);
        }

        return lists;
    }

    // Places the build's text at the origin on the map: the number of items placed (pieces already there are skipped).
    public static int Place(string text, Map map, Point3D origin)
    {
        var count = 0;
        foreach (var list in Parse(Shift(text, origin.X, origin.Y, origin.Z)))
        {
            count += list.Generate(new[] { map });
        }

        return count;
    }

    // Removes the immovable items standing exactly on the build's pieces at the origin: the number removed.
    public static int Clear(string text, Map map, Point3D origin, bool dryRun)
    {
        var doomed = new HashSet<Item>();
        foreach (var p in Points(Shift(text, origin.X, origin.Y, origin.Z)))
        {
            foreach (var item in map.GetItemsInRange(p, 0))
            {
                if (item.X == p.X && item.Y == p.Y && item.Z == p.Z && !item.Movable && item.Parent == null)
                {
                    doomed.Add(item);
                }
            }
        }

        if (!dryRun)
        {
            foreach (var item in doomed)
            {
                item.Delete();
            }
        }

        return doomed.Count;
    }

    public static string FileFor(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
        {
            return null;
        }

        var folder = Path.IsPathRooted(Folder) ? Folder : Path.Combine(Core.BaseDirectory, Folder);
        var path = Path.Combine(folder, name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) ? name : name + ".cfg");
        return File.Exists(path) ? path : null;
    }

    // "<name> [x y z] [dryrun]": the build's text, its origin and whether this is a dry run; null text with a reason.
    private static (string Text, Point3D Origin, bool DryRun, string Why) ReadArgs(CommandEventArgs e, bool needOrigin)
    {
        if (e.Length < 1)
        {
            return (null, default, false, "no build named");
        }

        var dryRun = e.Length > 1 && e.GetString(e.Length - 1).Equals("dryrun", StringComparison.OrdinalIgnoreCase);
        var n = e.Length - (dryRun ? 1 : 0);
        Point3D origin;
        if (n == 4)
        {
            origin = new Point3D(e.GetInt32(1), e.GetInt32(2), e.GetInt32(3));
        }
        else if (n == 1 && !needOrigin)
        {
            origin = e.Mobile.Location;
        }
        else
        {
            return (null, default, false, needOrigin ? "give the origin: x y z" : "give x y z, or nothing to build where you stand");
        }

        var path = FileFor(e.GetString(0));
        if (path == null)
        {
            return (null, default, false, $"no build {e.GetString(0)} in {Folder}");
        }

        return (File.ReadAllText(path), origin, dryRun, null);
    }

    [Usage("PlaceBuild <name> [x y z] [dryrun]")]
    [Description("Places a saved build (Data/ShatteredLegacy/Decoration/Builds/<name>.cfg) with its north-west corner at x y z, or where you stand.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places one saved build (cc-P734) at a point; pieces already there are skipped.")]
    private static void OnPlace(CommandEventArgs e)
    {
        var (text, origin, dryRun, why) = ReadArgs(e, false);
        if (text == null)
        {
            e.Mobile.SendMessage($"Usage: [PlaceBuild <name> [x y z] [dryrun] ({why})");
            return;
        }

        if (dryRun)
        {
            var pieces = Points(text).Count;
            e.Mobile.SendMessage($"Dry run: {e.GetString(0)} has {pieces} pieces; it would stand at {origin} on {e.Mobile.Map}.");
            return;
        }

        var placed = Place(text, e.Mobile.Map, origin);
        e.Mobile.SendMessage($"{e.GetString(0)}: placed {placed} pieces at {origin} on {e.Mobile.Map} (pieces already there are skipped).");
    }

    [Usage("ClearBuild <name> x y z [dryrun]")]
    [Description("Removes the immovable items standing exactly on a saved build's pieces placed at x y z.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Removes one placed build (cc-P734) so it can be placed again.")]
    private static void OnClear(CommandEventArgs e)
    {
        var (text, origin, dryRun, why) = ReadArgs(e, true);
        if (text == null)
        {
            e.Mobile.SendMessage($"Usage: [ClearBuild <name> x y z [dryrun] ({why})");
            return;
        }

        var n = Clear(text, e.Mobile.Map, origin, dryRun);
        e.Mobile.SendMessage(dryRun ? $"Dry run: {n} items would be removed." : $"{e.GetString(0)}: removed {n} items at {origin}.");
    }

    [Usage("ListBuilds")]
    [Description("Lists the saved builds and their piece counts.")]
    [ShardCommand(CommandCategory.Diagnostic, Summary = "Lists the saved builds (cc-P734).")]
    private static void OnList(CommandEventArgs e)
    {
        var folder = Path.IsPathRooted(Folder) ? Folder : Path.Combine(Core.BaseDirectory, Folder);
        if (!Directory.Exists(folder))
        {
            e.Mobile.SendMessage($"No builds folder at {folder}.");
            return;
        }

        foreach (var f in Directory.GetFiles(folder, "*.cfg"))
        {
            e.Mobile.SendMessage($"{Path.GetFileNameWithoutExtension(f)}: {Points(File.ReadAllText(f)).Count} pieces");
        }
    }
}
