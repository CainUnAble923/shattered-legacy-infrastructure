// GumpTextWidth.cs
//
// cc-P57 Parts E and F: how wide a server gump's text is in the client, for the layout facts. TazUO draws a gump label
// and an HTML block in unicode font 1 (TazUO 3212623f: RenderedText.Font maps 0xFF to font 1 for clients from 3.0.5d,
// ClassicUO.Client/Game/UI/Controls/RenderedText.cs:50-60; HtmlControl's default font is 1, HtmlControl.cs:63), and a
// line is as wide as the sum of OffsetX + Width + 1 for each character, a space 8 (FontsLoader.GetWidthUnicode,
// ClassicUO.Assets/FontsLoader.cs:1230-1255). The table is those sums for ASCII 32 to 126, read from EA's unifont1.mul
// (D:\UO\UltimaOnlineVanilla, 1,433,134 bytes) on 2026-10-05 by the script in the cc-P57 notes. Only numbers, no EA data
// file: this is a test helper and never ships. A label is drawn with a black border, so it gets 2 pixels more.
//
// Non-ASCII text has no entry and throws: the facts that use this also keep our gump text ASCII, which every client
// font draws (unifont1.mul has no glyph for U+2591 or U+2713, which two of the achievements gump's lines used).

using System;

namespace ShatteredLegacy.Tests;

internal static class GumpTextWidth
{
    private static readonly int[] Ascii =
    [
        8, 3, 4, 12, 9, 10, 11, 3, 4, 4, 10, 7, 3, 6, 3, 9, 8, 4, 8, 8, 8, 8, 8, 8, 8, 8, 3, 3, 8, 6, 8, 7, 12, 8, 8, 8, 8,
        7, 7, 8, 8, 3, 8, 8, 7, 10, 8, 8, 8, 9, 8, 8, 7, 8, 8, 12, 8, 9, 8, 4, 9, 5, 10, 8, 3, 6, 6, 6, 6, 6, 6, 6, 6, 3, 6,
        6, 3, 9, 6, 6, 6, 6, 6, 6, 6, 6, 6, 8, 6, 6, 6, 5, 2, 5, 6
    ];

    /// <summary>The line height of unicode font 1 (the tallest ASCII glyph, OffsetY + Height = 16) plus the border.</summary>
    public const int LineHeight = 18;

    public static int Of(string text)
    {
        var w = 0;
        foreach (var c in text)
        {
            if (c < 32 || c > 126)
            {
                throw new ArgumentException($"not ASCII: U+{(int)c:X4} in \"{text}\"");
            }

            w += Ascii[c - 32];
        }

        return w;
    }

    /// <summary>A label's drawn width: the text and its black border.</summary>
    public static int Label(string text) => Of(text) + 2;

    /// <summary>
    /// Lines the client needs for this text wrapped at <paramref name="width"/> pixels, word by word as the client wraps
    /// (a word that does not fit starts the next line; a word wider than the line is broken). Callers pass the HTML's
    /// visible text with each &lt;BR&gt; as '\n'.
    /// </summary>
    public static int Lines(string text, int width)
    {
        var lines = 0;
        foreach (var para in text.Split('\n'))
        {
            lines++;
            var x = 0;
            foreach (var word in para.Split(' '))
            {
                var w = Of(word);
                if (x > 0 && x + Of(" ") + w <= width)
                {
                    x += Of(" ") + w;
                    continue;
                }

                if (x == 0 && w <= width)
                {
                    x = w;
                    continue;
                }

                if (x > 0)
                {
                    lines++;
                }

                x = w;
                while (x > width)
                {
                    lines++;
                    x -= width;
                }
            }
        }

        return lines;
    }
}
