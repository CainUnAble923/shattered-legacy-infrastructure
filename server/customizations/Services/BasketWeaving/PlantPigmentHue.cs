// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/PlantPigmentHue.cs -> ours. The plant pigment colours: the
// IPlantHue and IPigmentHue interfaces the reeds, clippings, pigments and dyes carry, the flag enum of 34 pigment hues
// (primaries, their mixes, bright, dark and ice forms, black, white, the four rares), the table from each to a plant hue,
// an item hue and a name cliloc, and ServUO's Mix rules (its own comment says the non-primary mixes are a guess until
// confirmed; kept as it runs). FromItemHue is ours: the plant hue behind an item hue, for a crafted reed whose colour
// the craft engine passed as resHue.

using System;
using System.Collections.Generic;

namespace Server.Engines.Plants;

public interface IPlantHue
{
    PlantHue PlantHue { get; set; }
    void InvalidatePlantHue();
}

public interface IPigmentHue
{
    PlantPigmentHue PigmentHue { get; set; }
}

[Flags]
public enum PlantPigmentHue
{
    None = 0,

    Plain = 0x1,

    Red = 0x2,
    Blue = 0x4,
    Yellow = 0x8,

    Purple = Red | Blue,
    Green = Blue | Yellow,
    Orange = Red | Yellow,

    Black = 0x10,
    White = 0x20,

    Pink = 0x40,
    Magenta = 0x80,
    Aqua = 0x100,
    FireRed = 0x200,

    BrightRed = Red | Bright,
    BrightBlue = Blue | Bright,
    BrightYellow = Yellow | Bright,

    BrightPurple = Purple | Bright,
    BrightGreen = Green | Bright,
    BrightOrange = Orange | Bright,

    DarkRed = Red | Dark,
    DarkBlue = Blue | Dark,
    DarkYellow = Yellow | Dark,

    DarkPurple = Purple | Dark,
    DarkGreen = Green | Dark,
    DarkOrange = Orange | Dark,

    IceRed = Red | Ice,
    IceBlue = Blue | Ice,
    IceYellow = Yellow | Ice,

    IcePurple = Purple | Ice,
    IceGreen = Green | Ice,
    IceOrange = Orange | Ice,

    IceBlack = Black | Ice,
    OffWhite = White | Bright,
    Metal = Black | Bright,

    Ice = 0x2000000,
    Dark = 0x4000000,
    Bright = 0x8000000
}

public class PlantPigmentHueInfo
{
    private static readonly Dictionary<PlantPigmentHue, PlantPigmentHueInfo> _table = new()
    {
        [PlantPigmentHue.Plain] = new PlantPigmentHueInfo(PlantHue.Plain, 2101, 1060813, PlantPigmentHue.Plain),
        [PlantPigmentHue.Red] = new PlantPigmentHueInfo(PlantHue.Red, 1652, 1060814, PlantPigmentHue.Red),
        [PlantPigmentHue.Blue] = new PlantPigmentHueInfo(PlantHue.Blue, 2122, 1060815, PlantPigmentHue.Blue),
        [PlantPigmentHue.Yellow] = new PlantPigmentHueInfo(PlantHue.Yellow, 2125, 1060818, PlantPigmentHue.Yellow),
        [PlantPigmentHue.BrightRed] = new PlantPigmentHueInfo(PlantHue.BrightRed, 1646, 1060814, PlantPigmentHue.BrightRed),
        [PlantPigmentHue.BrightBlue] = new PlantPigmentHueInfo(PlantHue.BrightBlue, 1310, 1060815, PlantPigmentHue.BrightBlue),
        [PlantPigmentHue.BrightYellow] = new PlantPigmentHueInfo(PlantHue.BrightYellow, 253, 1060818, PlantPigmentHue.BrightYellow),
        [PlantPigmentHue.DarkRed] = new PlantPigmentHueInfo(PlantHue.Plain, 1141, 1112162, PlantPigmentHue.DarkRed),
        [PlantPigmentHue.DarkBlue] = new PlantPigmentHueInfo(PlantHue.Plain, 1317, 1112164, PlantPigmentHue.DarkBlue),
        [PlantPigmentHue.DarkYellow] = new PlantPigmentHueInfo(PlantHue.Plain, 2217, 1112165, PlantPigmentHue.DarkYellow),
        [PlantPigmentHue.IceRed] = new PlantPigmentHueInfo(PlantHue.Plain, 335, 1112169, PlantPigmentHue.IceRed),
        [PlantPigmentHue.IceBlue] = new PlantPigmentHueInfo(PlantHue.Plain, 1154, 1112168, PlantPigmentHue.IceBlue),
        [PlantPigmentHue.IceYellow] = new PlantPigmentHueInfo(PlantHue.Plain, 56, 1112171, PlantPigmentHue.IceYellow),
        [PlantPigmentHue.Purple] = new PlantPigmentHueInfo(PlantHue.Purple, 15, 1060816, PlantPigmentHue.Purple),
        [PlantPigmentHue.Green] = new PlantPigmentHueInfo(PlantHue.Green, 2128, 1060819, PlantPigmentHue.Green),
        [PlantPigmentHue.Orange] = new PlantPigmentHueInfo(PlantHue.Orange, 1128, 1060817, PlantPigmentHue.Orange),
        [PlantPigmentHue.BrightPurple] = new PlantPigmentHueInfo(PlantHue.BrightPurple, 316, 1060816, PlantPigmentHue.BrightPurple),
        [PlantPigmentHue.BrightGreen] = new PlantPigmentHueInfo(PlantHue.BrightGreen, 671, 1060819, PlantPigmentHue.BrightGreen),
        [PlantPigmentHue.BrightOrange] = new PlantPigmentHueInfo(PlantHue.BrightOrange, 1501, 1060817, PlantPigmentHue.BrightOrange),
        [PlantPigmentHue.DarkPurple] = new PlantPigmentHueInfo(PlantHue.Plain, 1254, 1113166, PlantPigmentHue.DarkPurple),
        [PlantPigmentHue.DarkGreen] = new PlantPigmentHueInfo(PlantHue.Plain, 1425, 1112163, PlantPigmentHue.DarkGreen),
        [PlantPigmentHue.DarkOrange] = new PlantPigmentHueInfo(PlantHue.Plain, 1509, 1112161, PlantPigmentHue.DarkOrange),
        [PlantPigmentHue.IcePurple] = new PlantPigmentHueInfo(PlantHue.Plain, 511, 1112172, PlantPigmentHue.IcePurple),
        [PlantPigmentHue.IceGreen] = new PlantPigmentHueInfo(PlantHue.Plain, 261, 1112167, PlantPigmentHue.IceGreen),
        [PlantPigmentHue.IceOrange] = new PlantPigmentHueInfo(PlantHue.Plain, 346, 1112170, PlantPigmentHue.IceOrange),
        [PlantPigmentHue.Black] = new PlantPigmentHueInfo(PlantHue.Black, 1175, 1060820, PlantPigmentHue.Black),
        [PlantPigmentHue.White] = new PlantPigmentHueInfo(PlantHue.White, 1150, 1060821, PlantPigmentHue.White),
        [PlantPigmentHue.IceBlack] = new PlantPigmentHueInfo(PlantHue.Plain, 2422, 1112988, PlantPigmentHue.IceBlack),
        [PlantPigmentHue.OffWhite] = new PlantPigmentHueInfo(PlantHue.Plain, 746, 1112224, PlantPigmentHue.OffWhite),
        [PlantPigmentHue.Metal] = new PlantPigmentHueInfo(PlantHue.Plain, 1105, 1015046, PlantPigmentHue.Metal),
        [PlantPigmentHue.Pink] = new PlantPigmentHueInfo(PlantHue.Pink, 341, 1061854, PlantPigmentHue.Pink),
        [PlantPigmentHue.Magenta] = new PlantPigmentHueInfo(PlantHue.Magenta, 1163, 1061852, PlantPigmentHue.Magenta),
        [PlantPigmentHue.Aqua] = new PlantPigmentHueInfo(PlantHue.Aqua, 391, 1061853, PlantPigmentHue.Aqua),
        [PlantPigmentHue.FireRed] = new PlantPigmentHueInfo(PlantHue.FireRed, 1358, 1061855, PlantPigmentHue.FireRed)
    };

    private PlantPigmentHueInfo(PlantHue plantHue, int hue, int name, PlantPigmentHue pigmentHue)
    {
        PlantHue = plantHue;
        Hue = hue;
        Name = name;
        PlantPigmentHue = pigmentHue;
    }

    public PlantHue PlantHue { get; }
    public int Hue { get; }
    public int Name { get; }
    public PlantPigmentHue PlantPigmentHue { get; }

    public static IEnumerable<PlantPigmentHue> AllHues => _table.Keys;

    public static PlantPigmentHue HueFromPlantHue(PlantHue hue)
    {
        if (hue is PlantHue.None or PlantHue.Plain)
        {
            return PlantPigmentHue.Plain;
        }

        foreach (var kvp in _table)
        {
            if (kvp.Value.PlantHue == hue)
            {
                return kvp.Key;
            }
        }

        return PlantPigmentHue.Plain;
    }

    public static PlantPigmentHueInfo GetInfo(PlantPigmentHue hue) =>
        _table.TryGetValue(hue, out var info) ? info : _table[PlantPigmentHue.Plain];

    public static bool IsMixable(PlantPigmentHue hue) => hue <= PlantPigmentHue.White && hue != PlantPigmentHue.None;

    public static bool IsBright(PlantPigmentHue hue) => (hue & PlantPigmentHue.Bright) != PlantPigmentHue.None;

    public static bool IsPrimary(PlantPigmentHue hue) =>
        hue is PlantPigmentHue.Red or PlantPigmentHue.Blue or PlantPigmentHue.Yellow;

    public static PlantPigmentHue Mix(PlantPigmentHue first, PlantPigmentHue second)
    {
        if (!IsMixable(first) || !IsMixable(second))
        {
            return PlantPigmentHue.None;
        }

        if (first == second && first is PlantPigmentHue.Plain or PlantPigmentHue.Black or PlantPigmentHue.White)
        {
            return PlantPigmentHue.None;
        }

        if (first == second)
        {
            return second | PlantPigmentHue.Bright;
        }

        if (first == PlantPigmentHue.Plain)
        {
            return second | PlantPigmentHue.Bright;
        }

        if (second == PlantPigmentHue.Plain)
        {
            return first | PlantPigmentHue.Bright;
        }

        if (first == PlantPigmentHue.White)
        {
            return second | PlantPigmentHue.Ice;
        }

        if (second == PlantPigmentHue.White)
        {
            return first | PlantPigmentHue.Ice;
        }

        if (first == PlantPigmentHue.Black)
        {
            return second | PlantPigmentHue.Dark;
        }

        if (second == PlantPigmentHue.Black)
        {
            return first | PlantPigmentHue.Dark;
        }

        var firstPrimary = IsPrimary(first);
        var secondPrimary = IsPrimary(second);

        if (firstPrimary && secondPrimary)
        {
            return first | second;
        }

        // ServUO: "not sure after this point ... temporary until proper mixed hues can be confirmed". Kept as it runs.
        if (firstPrimary && !secondPrimary)
        {
            return first;
        }

        if (!firstPrimary && secondPrimary)
        {
            return second;
        }

        return first & second;
    }

    public bool IsMixable() => IsMixable(PlantPigmentHue);

    public bool IsBright() => IsBright(PlantPigmentHue);

    public bool IsPrimary() => IsPrimary(PlantPigmentHue);
}

public static class PlantHueLookup
{
    private static Dictionary<int, PlantHue> _byItemHue;

    /// <summary>The plant hue whose item hue this is, or None. Built once from pinned's PlantHueInfo table.</summary>
    public static PlantHue FromItemHue(int itemHue)
    {
        if (_byItemHue == null)
        {
            var map = new Dictionary<int, PlantHue>();

            foreach (var hue in Enum.GetValues<PlantHue>())
            {
                if (hue is PlantHue.None or PlantHue.Reproduces or PlantHue.Crossable or PlantHue.Bright)
                {
                    continue;
                }

                var info = PlantHueInfo.GetInfo(hue);

                if (info.PlantHue == hue)
                {
                    map.TryAdd(info.Hue, hue);
                }
            }

            _byItemHue = map;
        }

        return _byItemHue.TryGetValue(itemHue, out var found) ? found : PlantHue.None;
    }
}
