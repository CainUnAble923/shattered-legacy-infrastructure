// ShardDecorAddon.cs
//
// cc-P855 (P852 "port first" item 1): one data-driven house addon and its deed, so later furniture and decor are data
// files, not classes. Notes: shard-migration notes/cc-P855-decor-addon-housing-furniture.md.
//
// The data: server/data/Decoration/Addons/<design>.cfg (the image ships it as Data/ShatteredLegacy/Decoration/Addons;
// [Decorate reads only its facet folders, never this one). Each file is pinned's own [Decorate text, the format of the
// facet decoration, P734's builds and P812's [VBuild capture: a type line "Static 0x0519 (Hue=29; Light=Circle300;
// Name=...)", then one "x y z" line per piece, a blank line between lists. Offsets are from the addon's own tile, the
// tile the deed is aimed at. Only Static lists are accepted (a piece of a house addon is a plain AddonComponent). Header
// lines pinned's reader skips as comments carry what the addon needs:
//
//   #! name: a small grey stone fireplace (east)   the piece's name (the deed drops the "(facing)" part)
//   #! credit: Diesel                               the original author, shown on the deed and on every piece
//   #! group: grey-fireplace-small                  one deed per group
//   #! facing: east                                 the choice the deed offers when its group has more than one
//
// A deed names its group. Used with more than one facing in the group, it asks which (a small window), then targets
// as every addon deed does (pinned BaseAddonDeed: in your own house, fit checked). Chopping the addon (a house owner's
// axe, pinned BaseAddon.OnChop) gives the group's deed back. Nothing here has item properties or does anything on use.
//
// Staff: [DecorAddon lists the groups (and any file refused), [DecorAddon reload reads the files again, [DecorAddon
// <group> puts that deed in your pack, [DecorAddon place <design> [x y z] stands one design at a point (or where you
// stand) with no house rules, for town dressing, and [DecorAddon clear [x y z] removes the data addons within 6 tiles.
//
// The download pieces were turned into these files by shard-migration notes/cc-P855-tools/convert.py from the YAAAG
// component lists in D:\UO\third-party\code (reference only); no code was taken, and every piece is stock client art.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Network;

namespace Server.Items;

/// <summary>One piece of a design: its art, offset from the addon's tile, hue, light and name.</summary>
public sealed record ShardDecorPart(int ItemID, int X, int Y, int Z, int Hue, LightType? Light, string? Name);

/// <summary>One design file: a piece in one facing.</summary>
public sealed class ShardDecorDesign
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Credit { get; init; }
    public required string Group { get; init; }
    public required string Facing { get; init; }
    public required IReadOnlyList<ShardDecorPart> Parts { get; init; }

    /// <summary>The name without its "(facing)" tail: "a small grey stone fireplace".</summary>
    public string GroupName
    {
        get
        {
            var i = Name.LastIndexOf(" (", StringComparison.Ordinal);
            return i > 0 && Name.EndsWith(')') ? Name[..i] : Name;
        }
    }
}

public static class ShardDecorDesigns
{
    public const string DefaultFolder = "Data/ShatteredLegacy/Decoration/Addons";

    /// <summary>Where the files are read from; null for Data/ShatteredLegacy/Decoration/Addons under the server (tests set it).</summary>
    public static string? FolderOverride { get; set; }

    public static string Folder => FolderOverride ?? Path.Combine(Core.BaseDirectory, DefaultFolder);

    private static Dictionary<string, ShardDecorDesign>? _designs;
    private static List<string> _refused = [];

    /// <summary>The files refused on the last read, each with its reason.</summary>
    public static IReadOnlyList<string> Refused
    {
        get
        {
            EnsureLoaded();
            return _refused;
        }
    }

    public static IReadOnlyDictionary<string, ShardDecorDesign> All
    {
        get
        {
            EnsureLoaded();
            return _designs!;
        }
    }

    public static ShardDecorDesign? Get(string? key) => key != null && All.TryGetValue(key, out var d) ? d : null;

    /// <summary>A group's designs, in file-name order (its facings); empty for an unknown group.</summary>
    public static List<ShardDecorDesign> Facings(string? group) =>
        group == null
            ? []
            : All.Values.Where(d => d.Group == group).OrderBy(d => d.Key, StringComparer.Ordinal).ToList();

    public static IEnumerable<string> Groups() => All.Values.Select(d => d.Group).Distinct().OrderBy(g => g, StringComparer.Ordinal);

    /// <summary>Reads every file again: the count read.</summary>
    public static int Reload()
    {
        _designs = null;
        EnsureLoaded();
        return _designs!.Count;
    }

    private static void EnsureLoaded()
    {
        if (_designs != null)
        {
            return;
        }

        var designs = new Dictionary<string, ShardDecorDesign>(StringComparer.OrdinalIgnoreCase);
        var refused = new List<string>();
        if (Directory.Exists(Folder))
        {
            foreach (var path in Directory.GetFiles(Folder, "*.cfg").OrderBy(p => p, StringComparer.Ordinal))
            {
                var key = Path.GetFileNameWithoutExtension(path);
                try
                {
                    designs[key] = Parse(key, File.ReadAllText(path));
                }
                catch (FormatException e)
                {
                    refused.Add($"{key}: {e.Message}");
                }
            }
        }

        _refused = refused;
        _designs = designs;
    }

    /// <summary>Reads one design's text. Throws FormatException on anything it does not understand.</summary>
    public static ShardDecorDesign Parse(string key, string text)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<ShardDecorPart>();
        (int ItemID, int Hue, LightType? Light, string? Name)? list = null;
        var lineNo = 0;

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.StartsWith("#!", StringComparison.Ordinal))
            {
                var colon = line.IndexOf(':');
                if (colon < 0)
                {
                    throw new FormatException($"line {lineNo}: a #! line needs \"key: value\"");
                }

                meta[line[2..colon].Trim()] = line[(colon + 1)..].Trim();
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.Length == 0)
            {
                list = null;
                continue;
            }

            if (list == null)
            {
                list = ReadHeader(line, lineNo);
                continue;
            }

            var xyz = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (xyz.Length != 3 || !int.TryParse(xyz[0], out var x) || !int.TryParse(xyz[1], out var y) ||
                !int.TryParse(xyz[2], out var z))
            {
                throw new FormatException($"line {lineNo}: expected \"x y z\", got \"{line}\"");
            }

            var (id, hue, light, name) = list.Value;
            parts.Add(new ShardDecorPart(id, x, y, z, hue, light, name));
        }

        string Need(string k) =>
            meta.TryGetValue(k, out var v) && v.Length > 0 ? v : throw new FormatException($"no \"#! {k}:\" line");

        if (parts.Count == 0)
        {
            throw new FormatException("no pieces");
        }

        return new ShardDecorDesign
        {
            Key = key,
            Name = Need("name"),
            Credit = Need("credit"),
            Group = meta.TryGetValue("group", out var g) && g.Length > 0 ? g : key,
            Facing = meta.TryGetValue("facing", out var f) && f.Length > 0 ? f : "as made",
            Parts = parts
        };
    }

    // "Static 0x0519 (Hue=29; Light=Circle300; Name=Garland)", as pinned's DecorationList.Read splits it.
    private static (int, int, LightType?, string?) ReadHeader(string line, int lineNo)
    {
        var space = line.IndexOf(' ');
        var type = space < 0 ? line : line[..space];
        if (!type.Equals("Static", StringComparison.Ordinal))
        {
            throw new FormatException($"line {lineNo}: only Static lists make addon pieces, not {type}");
        }

        var rest = space < 0 ? "" : line[(space + 1)..].Trim();
        var open = rest.IndexOf('(');
        var idText = (open < 0 ? rest : rest[..open]).Trim();
        if (!TryId(idText, out var id))
        {
            throw new FormatException($"line {lineNo}: bad item ID \"{idText}\"");
        }

        int hue = 0;
        LightType? light = null;
        string? name = null;
        if (open >= 0)
        {
            var args = rest[(open + 1)..].TrimEnd(')');
            foreach (var arg in args.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = arg.IndexOf('=');
                var k = eq < 0 ? arg : arg[..eq].Trim();
                var v = eq < 0 ? "" : arg[(eq + 1)..].Trim();
                switch (k)
                {
                    case "Hue" when TryId(v, out var h):
                        hue = h;
                        break;
                    case "Light" when Enum.TryParse<LightType>(v, true, out var l) && Enum.IsDefined(l):
                        light = l;
                        break;
                    case "Name":
                        name = v;
                        break;
                    default:
                        throw new FormatException($"line {lineNo}: unknown or bad parameter \"{arg}\"");
                }
            }
        }

        return (id, hue, light, name);
    }

    /// <summary>Stands a design at a point with no house rules (staff town dressing).</summary>
    public static ShardDecorAddon Place(string design, Map map, Point3D at)
    {
        var addon = new ShardDecorAddon(design);
        addon.MoveToWorld(at, map);
        return addon;
    }

    /// <summary>Removes the data addons whose tile is within <paramref name="range"/> of a point: the number removed.</summary>
    public static int Clear(Map map, Point3D at, int range)
    {
        var doomed = new List<ShardDecorAddon>();
        foreach (var item in map.GetItemsInRange(at, range))
        {
            if (item is ShardDecorAddon addon)
            {
                doomed.Add(addon);
            }
        }

        foreach (var addon in doomed)
        {
            addon.Delete();
        }

        return doomed.Count;
    }

    private static bool TryId(string s, out int value) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) && value >= 0
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;

    public static void Configure()
    {
        CommandSystem.Register("DecorAddon", AccessLevel.GameMaster, OnCommand);
        EventSink.ServerStarted += () =>
        {
            Console.WriteLine($"[DecorAddon] {All.Count} designs in {Groups().Count()} deeds from {Folder}");
            foreach (var r in Refused)
            {
                Console.WriteLine($"[DecorAddon] refused {r}");
            }
        };
    }

    [Usage("DecorAddon [reload | <group> | place <design> [x y z] | clear [x y z]]")]
    [Description("The data-driven house addons (cc-P855). With no argument, lists the deeds (groups) and their facings, and any design file refused. reload reads Data/ShatteredLegacy/Decoration/Addons again. A group's name puts its deed in your pack. place stands one design (a file name, such as grey-fireplace-small-east) at x y z, or where you stand, with no house rules. clear removes the data addons within 6 tiles of x y z, or of you.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.Any, Summary = "Lists the data addons, rereads their files, or gives staff one deed (cc-P855).")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var arg = e.Length > 0 ? e.GetString(0) : "";
        if (arg.Length == 0)
        {
            foreach (var group in Groups())
            {
                var facings = Facings(group);
                from.SendMessage($"{group}: {facings[0].GroupName} by {facings[0].Credit}; " +
                                 string.Join(", ", facings.Select(d => $"{d.Facing} ({d.Parts.Count} pieces)")));
            }

            foreach (var r in Refused)
            {
                from.SendMessage(0x22, $"Refused: {r}");
            }

            from.SendMessage($"{All.Count} designs, {Groups().Count()} deeds. [DecorAddon <group> for a deed, [DecorAddon reload to reread.");
            return;
        }

        if (arg.Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            var n = Reload();
            from.SendMessage($"Read {n} designs from {Folder}; {Refused.Count} refused.");
            return;
        }

        if (arg.Equals("place", StringComparison.OrdinalIgnoreCase) || arg.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            var place = arg.Equals("place", StringComparison.OrdinalIgnoreCase);
            var first = place ? 2 : 1;
            if (place && (e.Length < 2 || Get(e.GetString(1)) == null))
            {
                from.SendMessage(0x22, "Usage: [DecorAddon place <design> [x y z]; [DecorAddon lists the designs by group.");
                return;
            }

            var at = e.Length >= first + 3
                ? new Point3D(e.GetInt32(first), e.GetInt32(first + 1), e.GetInt32(first + 2))
                : from.Location;
            if (place)
            {
                var addon = Place(e.GetString(1), from.Map, at);
                from.SendMessage($"Placed {addon.Name} ({addon.Components.Count} pieces) at {at}.");
            }
            else
            {
                from.SendMessage($"Removed {Clear(from.Map, at, 6)} data addons within 6 tiles of {at}.");
            }

            return;
        }

        if (Facings(arg).Count == 0)
        {
            from.SendMessage(0x22, $"No decor group \"{arg}\". [DecorAddon lists them.");
            return;
        }

        from.AddToBackpack(new ShardDecorDeed(Facings(arg)[0].Group));
        from.SendMessage($"A deed for {Facings(arg)[0].GroupName} is in your pack.");
    }
}

/// <summary>A piece of a ShardDecorAddon: plain stock art, with the design's author on it.</summary>
[SerializationGenerator(0, false)]
public partial class ShardDecorComponent : AddonComponent
{
    public ShardDecorComponent(int itemID) : base(itemID)
    {
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Addon is ShardDecorAddon { Credit: { Length: > 0 } } addon)
        {
            list.Add($"Design by {addon.Credit}");
        }
    }
}

/// <summary>A house addon built from one design file.</summary>
[SerializationGenerator(0, false)]
public partial class ShardDecorAddon : BaseAddon
{
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string? _design;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string? _group;

    [SerializableField(2, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string? _credit;

    [Constructible(AccessLevel.GameMaster)]
    public ShardDecorAddon(string design)
    {
        var d = ShardDecorDesigns.Get(design);
        _design = d?.Key ?? design;
        _group = d?.Group ?? design;
        _credit = d?.Credit ?? "";
        Name = d?.Name;

        if (d == null)
        {
            return;
        }

        foreach (var part in d.Parts)
        {
            var c = new ShardDecorComponent(part.ItemID);
            if (part.Hue != 0)
            {
                c.Hue = part.Hue;
            }

            if (part.Light is { } light)
            {
                c.Light = light;
            }

            if (part.Name != null)
            {
                c.Name = part.Name;
            }

            AddComponent(c, part.X, part.Y, part.Z);
        }
    }

    // Each piece keeps the hue its design gives it.
    public override bool ShareHue => false;

    public override BaseAddonDeed Deed => new ShardDecorDeed(_group ?? _design ?? "");
}

/// <summary>The deed for one group of designs: asks the facing when there is a choice, then places it.</summary>
[SerializationGenerator(0, false)]
public partial class ShardDecorDeed : BaseAddonDeed
{
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string? _group;

    [Constructible(AccessLevel.GameMaster)]
    public ShardDecorDeed(string group)
    {
        _group = group;
        var first = ShardDecorDesigns.Facings(group).FirstOrDefault();
        Name = $"a deed for {first?.GroupName ?? "a piece of house decor"}";
    }

    /// <summary>The design the next placement makes: the facing chosen, else the group's only (or first) one.</summary>
    public string? Design { get; set; }

    public override BaseAddon Addon => new ShardDecorAddon(Design ?? ShardDecorDesigns.Facings(_group).First().Key);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var facings = ShardDecorDesigns.Facings(_group);
        if (facings.Count > 0)
        {
            list.Add($"Design by {facings[0].Credit}");
            if (facings.Count > 1)
            {
                list.Add($"Facings: {string.Join(", ", facings.Select(d => d.Facing))}");
            }
        }

        list.Add("House decor");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var facings = ShardDecorDesigns.Facings(_group);
        if (facings.Count == 0)
        {
            from.SendMessage(0x22, "This deed's design cannot be found. Please page a staff member.");
            return;
        }

        if (facings.Count == 1)
        {
            ChooseFacing(from, 0);
            return;
        }

        from.CloseGump<ShardDecorFacingGump>();
        from.SendGump(new ShardDecorFacingGump(this, facings));
    }

    /// <summary>Picks the group's facing at <paramref name="index"/> and starts the placement target (pinned's).</summary>
    public bool ChooseFacing(Mobile from, int index)
    {
        var facings = ShardDecorDesigns.Facings(_group);
        if (Deleted || index < 0 || index >= facings.Count || !IsChildOf(from.Backpack))
        {
            return false;
        }

        Design = facings[index].Key;
        base.OnDoubleClick(from);
        return true;
    }
}

/// <summary>Which way the piece faces: one button per facing in the deed's group.</summary>
public sealed class ShardDecorFacingGump : DynamicGump
{
    private readonly ShardDecorDeed _deed;
    private readonly List<ShardDecorDesign> _facings;

    public ShardDecorFacingGump(ShardDecorDeed deed, List<ShardDecorDesign> facings) : base(60, 60)
    {
        _deed = deed;
        _facings = facings;
    }

    /// <summary>The facings offered, in button order (button i + 1 is facing i).</summary>
    public IReadOnlyList<ShardDecorDesign> Choices => _facings;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var height = 70 + _facings.Count * 26;
        builder.AddPage();
        builder.AddBackground(0, 0, 300, height, 5054);
        builder.AddBackground(10, 10, 280, height - 20, 3000);
        builder.AddHtml(20, 18, 260, 20, $"Place {_facings[0].GroupName}");
        builder.AddHtml(20, 38, 260, 20, "Which way should it face?");
        for (var i = 0; i < _facings.Count; i++)
        {
            builder.AddButton(20, 62 + i * 26, 4005, 4007, i + 1);
            builder.AddHtml(55, 63 + i * 26, 220, 20, _facings[i].Facing);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } from && info.ButtonID > 0)
        {
            _deed.ChooseFacing(from, info.ButtonID - 1);
        }
    }
}
