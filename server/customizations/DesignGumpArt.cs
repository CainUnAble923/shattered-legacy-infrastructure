// cc-P813: the Claude Design gump art Chase approved on 2026-11-05 (canvas "Shattered Legacy gump art", made 11-01 in
// the Shattered Legacy design system). Each window is cut into a text-free background and button pieces, shipped in the
// player package as gumps 23211-23222 (player-package\vendor\shattered-legacy-art, registry.csv); the server draws every
// word, price and button label on top. Used by PetChoiceGump (P762's pet choice scroll), AchievementEarnedGump (P762's
// "Didn't Read the Sign") and LeagueShopGump (P229, P760's bigger shop). Notes: shard-migration
// notes/cc-P813-gump-art-import.md; the cut is notes/cc-P813-tools/render_gump_art.py.
//
// A client without the package (no ExternalImages gumps) draws nothing for these IDs: the text and buttons still work,
// on a bare window. Every player plays from the package, so that is a staff-client case only.

#nullable enable

namespace Server.Gumps;

public static class DesignGumpArt
{
    // The pet choice scroll, 340 x 280; its two tiles 143 x 96 at (21, 143) and (176, 143).
    public const int PetScrollBackground = 23211;
    public const int PetDogTile = 23212;
    public const int PetDogTilePressed = 23213;
    public const int PetCatTile = 23214;
    public const int PetCatTilePressed = 23215;
    public const int PetScrollWidth = 340;
    public const int PetScrollHeight = 280;

    // The moongate skip achievement popup, 400 x 136.
    public const int SkipPopupBackground = 23216;
    public const int SkipPopupWidth = 400;
    public const int SkipPopupHeight = 136;

    // The reward store window, 560 x 420: the panel, an 8 x 1 row rule to tile, a 104 x 32 tab (inactive, active) and a
    // 72 x 28 Buy button (normal, pressed).
    public const int StoreBackground = 23217;
    public const int StoreRowRule = 23218;
    public const int StoreTab = 23219;
    public const int StoreTabActive = 23220;
    public const int StoreBuy = 23221;
    public const int StoreBuyPressed = 23222;
    public const int StoreWidth = 560;
    public const int StoreHeight = 420;
    public const int StoreTabWidth = 104;
    public const int StoreBuyWidth = 72;

    // The design's colours as UO hues for labels (labels draw over buttons without taking their clicks; html would).
    // A gump label's hue is one less than the hues.mul number (TazUO's Label adds 1). Nearest hues.mul colour, by its
    // palette entry 24, to the design's: notes/cc-P813-gump-art-import.md lists each.
    public const int HueDesignBrown = 0x5AD;  // #5c4110, the pet tiles' words
    public const int HueDesignCream = 0x490;  // #e3c98d, the store's tab and Buy words
    public const int HueDesignBody = 0xB8D;   // #cfcabd, the store's row names
    public const int HueDesignWhite = 0x481;  // #ffffff, the store's active tab word

    // The same colours for html text, which is drawn where nothing clickable lies beneath.
    public const string Gold = "#7a5718";
    public const string Ink = "#2b2519";
    public const string Muted = "#675d4b";
    public const string Tan = "#c8a96e";
    public const string Cream = "#e3c98d";
    public const string Body = "#cfcabd";
    public const string Grey = "#8a8578";
    public const string Alarm = "#e0674c";
    public const string Teal = "#72b89a";

    public static string Color(string color, string text) => $"<BASEFONT COLOR={color}>{text}</BASEFONT>";

    /// <summary>
    /// Where a label starts to sit near the middle of a box <paramref name="width"/> wide. Gump labels cannot be centred
    /// by the client, so this guesses 7 pixels a letter (the label font's usual width; close enough for short words).
    /// </summary>
    public static int CenterX(int x, int width, string text) => x + System.Math.Max(0, (width - text.Length * 7) / 2);
}
