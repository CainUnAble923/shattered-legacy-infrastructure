// cc-P480: the tutorial's look in one place. Chase's first play (10-23): the lesson text was dark gray on the dark gump
// background (label hue 999 and unstyled HTML are #5A5A5A-#949494 on 9270), and the marks were hues 0x482 and 0x8AF,
// which hues.mul draws as pale gray (#8B94A4) and dark slate (#394A5A). Every tutorial gump and lesson prop takes its
// colors from here. Hue values are checked against the client's hues.mul (top of each ramp, notes cc-P480).

#nullable enable

using Server.Gumps;

namespace Server.Tutorial;

public static class TutorialStyle
{
    // -- Gumps: light text on the dark background --

    public const int Background = 9270;
    public const int HeadingHue = 53;   // yellow, #EEEE00 at the top of its ramp
    public const int TextHue = 1153;    // near white, #F6F6F6
    public const int ActionHue = 90;    // light cyan, #62E6F6: what a button does
    public const int DoneHue = 68;      // green, #18EE00
    public const int WarnHue = 34;      // light red, #EE315A: skip and refusals
    public const string TextColor = "#F0F0F0";
    public const string HeadingColor = "#FFE14A";

    // -- Lesson props: bright, smooth hue ramps (hues.mul) --

    public const int BlueTileHue = 0x4F1;    // strong blue
    public const int GoldTileHue = 0xAA1;    // bright gold
    public const int SupplyHue = 0x4F2;      // bright cyan: the practice cloak, bag and token
    public const int FloorTileId = 0x050E;   // "white marble": a full flat floor tile (Background, Surface), takes a hue cleanly

    /// <summary>A dark panel: the stone background with a darker alpha region inside.</summary>
    public static void Panel(Gump g, int width, int height)
    {
        g.AddBackground(0, 0, width, height, Background);
        g.AddAlphaRegion(10, 10, width - 20, height - 20);
    }

    public static string Html(string text) => $"<BASEFONT COLOR={TextColor}>{text}</BASEFONT>";

    public static string HeadingHtml(string text) => $"<BASEFONT COLOR={HeadingColor}>{text}</BASEFONT>";
}
