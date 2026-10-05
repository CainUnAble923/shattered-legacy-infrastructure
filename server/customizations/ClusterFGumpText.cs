// cc-P57 Part F (bug-list D80): how much room a gump's text takes in the client, so a layout can give each block its
// full height instead of guessing. TazUO draws server gump labels and HTML in unicode font 1; a line is as wide as the
// sum of OffsetX + Width + 1 over its characters, a space 8 (TazUO 3212623f, ClassicUO.Assets/FontsLoader.cs:1230-1255).
// The table holds those sums for ASCII 32 to 126, read from EA's unifont1.mul on 2026-10-05 (numbers only; the script is
// in shard-migration notes/cc-P57-batch-6.md, Part F). Characters outside it count as the widest ASCII glyph, so a
// measure is never short. The tests check layouts with their own copy (server/tests/Misc/GumpTextWidth.cs).

using System;

namespace Server.Gumps;

public static class ClusterFGumpText
{
    private static readonly int[] Ascii =
    {
        8, 3, 4, 12, 9, 10, 11, 3, 4, 4, 10, 7, 3, 6, 3, 9, 8, 4, 8, 8, 8, 8, 8, 8, 8, 8, 3, 3, 8, 6, 8, 7, 12, 8, 8, 8, 8,
        7, 7, 8, 8, 3, 8, 8, 7, 10, 8, 8, 8, 9, 8, 8, 7, 8, 8, 12, 8, 9, 8, 4, 9, 5, 10, 8, 3, 6, 6, 6, 6, 6, 6, 6, 6, 3, 6,
        6, 3, 9, 6, 6, 6, 6, 6, 6, 6, 6, 6, 8, 6, 6, 6, 5, 2, 5, 6
    };

    private const int Widest = 12;

    /// <summary>One line of unicode font 1: its tallest ASCII glyph (16) and the black border.</summary>
    public const int LineHeight = 18;

    public static int Width(string text)
    {
        var w = 0;
        foreach (var c in text ?? "")
        {
            w += c is >= ' ' and <= '~' ? Ascii[c - 32] : Widest;
        }

        return w;
    }

    /// <summary>Lines for plain text (no tags; '\n' between lines) wrapped word by word at <paramref name="width"/>.</summary>
    public static int Lines(string text, int width)
    {
        width = Math.Max(1, width);
        var lines = 0;
        foreach (var para in (text ?? "").Split('\n'))
        {
            lines++;
            var x = 0;
            foreach (var word in para.Split(' '))
            {
                var w = Width(word);
                if (x > 0 && x + Width(" ") + w <= width)
                {
                    x += Width(" ") + w;
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
