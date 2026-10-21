// cc-P169 (Chase 2026-10-18, the Hammer of Hephaestus "Upgrade to T2" screenshot): one look for every upgrade, restore
// and rank-up menu's requirements, so a met line looks met everywhere and nothing else does.
//
//   Met:     green text and the client's ticked check box, with the numbers ("Blacksmithy 119.0 / 80").
//   Not met: red text and an X box, with how much more is needed ("Smithing Seals 10 / 25 (need 15 more)").
//   Info:    light gray, indented under the art column, never a met / not met colour (where the ingots are).
//   Heading: ice white (1153), so green only ever means met. The menus' old 1154 headings drew green: TazUO draws a
//            label of hue N with hues.mul entry N + 1, and entry 1155 is "green highlight".
//
// The art carries the meaning, not only the colour, for a red-green colour-blind player. Hues are label hues (the
// client adds 1); the colours drawn are hues.mul's brightest of each (classic-client hues.mul, read 2026-10-20):
//   0x44 (68)   met       entry 69   #4AEE31 green
//   0x22 (34)   not met   entry 35   #F66283 rose red
//   999         info      entry 1000 #ACACAC light gray
//   1153        heading   entry 1154 #DEF6F6 ice white
//   0x3B2 (946) disabled  entry 947  #5A5A5A dim gray
// Art (EA gumpartLegacyMUL.uop): 211, the client's ticked check box (19 x 20; SmithBulkOrderChoiceGump's CheckOn);
// 5231, a stone box with an X (19 x 16). The window's inside is tile 2624, as cc-P57 Part E made the bulk order window
// opaque (the alpha region let the world and other gumps show through).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Items;
using Server.Mobiles;

namespace Server.Gumps;

public enum RequirementState
{
    Met,
    NotMet,
    Info
}

public sealed record RequirementLine(RequirementState State, string Text)
{
    public bool Blocks => State == RequirementState.NotMet;
}

public static class RequirementStyle
{
    public const int MetHue = 0x44;
    public const int NotMetHue = 0x22;
    public const int InfoHue = 999;
    public const int HeadingHue = 1153;
    public const int DisabledHue = 0x3B2;

    public const int CheckArt = 211;
    public const int CrossArt = 5231;

    /// <summary>cc-P57 Part E's solid panel tile (the Staff Hub's, cc-P52 Part F), under the whole inside of the frame.</summary>
    public const int PanelTile = 2624;

    /// <summary>The action button's art (4011 / 4012); drawn alone in the disabled hue when the action is not open.</summary>
    public const int ActionArt = 4011;
    public const int ActionArtPressed = 4012;

    /// <summary>Text starts this far right of the line's x, past the 19-pixel art.</summary>
    public const int TextIndent = 24;

    public const int LineHeight = 22;
    public const int InfoLineHeight = 18;

    /// <summary>The frame's inside, opaque: replaces AddAlphaRegion(6, 6, w - 12, h - 12) after AddBackground(0, 0, w, h, ...).</summary>
    public static void AddSolidPanel(Gump g, int w, int h) => g.AddImageTiled(8, 8, w - 16, h - 16, PanelTile);

    /// <summary>
    /// The menu's action: a live button when it is open, otherwise the same art and words in the disabled gray with no
    /// button behind them (the handler checks everything again on the press, so a stale page cannot act either).
    /// </summary>
    public static void AddAction(Gump g, int x, int y, int buttonId, string text, bool enabled)
    {
        if (enabled)
        {
            g.AddButton(x, y, ActionArt, ActionArtPressed, buttonId);
            g.AddLabel(x + 34, y + 2, HeadingHue, text);
        }
        else
        {
            g.AddImage(x, y, ActionArt, DisabledHue);
            g.AddLabel(x + 34, y + 2, DisabledHue, text);
        }
    }
}

/// <summary>A menu's requirements, built line by line, drawn with one look (RequirementStyle).</summary>
public sealed class RequirementList
{
    private readonly List<RequirementLine> _lines = new();

    public IReadOnlyList<RequirementLine> Lines => _lines;

    public int NotMetCount => _lines.Count(l => l.Blocks);

    public bool AllMet => NotMetCount == 0;

    /// <summary>"All requirements met", or "3 requirements not met".</summary>
    public string Summary => Summarize(NotMetCount);

    public static string Summarize(int notMet) =>
        notMet == 0 ? "All requirements met" : $"{notMet} requirement{(notMet == 1 ? "" : "s")} not met";

    /// <summary>A count: "Smithing Seals 10 / 25 (need 15 more)", or "Smithing Seals 30 / 25" when met.</summary>
    public RequirementList Amount(string label, long have, long need) =>
        Add(have >= need, have >= need
            ? $"{label} {have:N0} / {need:N0}"
            : $"{label} {have:N0} / {need:N0} (need {need - have:N0} more)");

    /// <summary>A skill: "Blacksmithy 119.0 / 80", or "Mining 62.5 / 75 (need 12.5 more)".</summary>
    public RequirementList Skill(string label, double have, double need) =>
        Add(have >= need, have >= need
            ? $"{label} {have:F1} / {need:0.#}"
            : $"{label} {have:F1} / {need:0.#} (need {need - have:0.0} more)");

    /// <summary>
    /// A guild rank by standing: "Rank: Journeyman 6,200 / 5,000 Standing", or "Rank: Journeyman (need 4,975 more
    /// Standing)". Met only at or above the rank's standing (cc-P169 part 4).
    /// </summary>
    public RequirementList Rank(string rankName, long standing, long need, string unit = "Standing") =>
        Add(standing >= need, standing >= need
            ? $"Rank: {rankName} {standing:N0} / {need:N0} {unit}"
            : $"Rank: {rankName} (need {need - standing:N0} more {unit})");

    /// <summary>A thing to hold: "Hammer of Hephaestus in pack", or "... (missing)" when not.</summary>
    public RequirementList Has(string label, bool met, string missing = "missing") =>
        Add(met, met ? label : $"{label} ({missing})");

    /// <summary>Any other requirement, with its own words for each state.</summary>
    public RequirementList Check(bool met, string metText, string notMetText) => Add(met, met ? metText : notMetText);

    /// <summary>
    /// A GuildResources price, as GuildResources.CanPay checks it: the scrip, each material (pack and bank), the gold
    /// (pack and bank, CompactGoldHelper), then where each material is and that gold counts from both.
    /// </summary>
    public RequirementList Price(
        PlayerMobile pm, CharacterGuildData guild, string scripKey, string scripName, int scrip, int gold,
        params GuildCost[] materials
    )
    {
        if (scrip > 0)
        {
            guild.GuildCurrency.TryGetValue(scripKey, out var held);
            Amount(scripName, held, scrip);
        }

        foreach (var cost in materials)
        {
            Amount(TitleCase(cost.Label), GuildResources.Count(pm, cost).Total, cost.Amount);
        }

        if (gold > 0)
        {
            Amount("Gold", CompactGoldHelper.GetTotalGold(pm), gold);
        }

        foreach (var cost in materials)
        {
            Info(GuildResources.Describe(pm, cost));
        }

        return gold > 0 ? Info("Gold counts from your pack and bank.") : this;
    }

    // "iron ingots" -> "Iron Ingots" (GuildResources.LabelFor's lower case, for a line that starts with it).
    private static string TitleCase(string label) =>
        string.Join(' ', (label ?? "").Split(' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>Where things are and other notes: gray, indented, never a met / not met colour.</summary>
    public RequirementList Info(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _lines.Add(new RequirementLine(RequirementState.Info, text));
        }

        return this;
    }

    private RequirementList Add(bool met, string text)
    {
        _lines.Add(new RequirementLine(met ? RequirementState.Met : RequirementState.NotMet, text));
        return this;
    }

    public static int HueOf(RequirementState state) => state switch
    {
        RequirementState.Met    => RequirementStyle.MetHue,
        RequirementState.NotMet => RequirementStyle.NotMetHue,
        _                       => RequirementStyle.InfoHue
    };

    public static int ArtOf(RequirementState state) => state switch
    {
        RequirementState.Met    => RequirementStyle.CheckArt,
        RequirementState.NotMet => RequirementStyle.CrossArt,
        _                       => -1
    };

    /// <summary>The height Draw takes.</summary>
    public int Height => _lines.Sum(l => l.State == RequirementState.Info ? RequirementStyle.InfoLineHeight : RequirementStyle.LineHeight);

    /// <summary>Draws the lines from (x, y); returns the y under the last.</summary>
    public int Draw(Gump g, int x, int y)
    {
        foreach (var line in _lines)
        {
            if (line.State == RequirementState.Info)
            {
                g.AddLabel(x + RequirementStyle.TextIndent, y, RequirementStyle.InfoHue, line.Text);
                y += RequirementStyle.InfoLineHeight;
                continue;
            }

            // The art is 19 wide and 16 to 20 tall; the label's glyphs sit 2 below its y.
            var art = ArtOf(line.State);
            g.AddImage(x, y - (art == RequirementStyle.CheckArt ? 1 : -1), art);
            g.AddLabel(x + RequirementStyle.TextIndent, y + 1, HueOf(line.State), line.Text);
            y += RequirementStyle.LineHeight;
        }

        return y;
    }

    /// <summary>
    /// The summary line, its art and colour as a requirement's, then the action under it: live only when every line is
    /// met, or when <paramref name="waived"/> (the testing token) says so, which the summary then names.
    /// Returns the y under the action.
    /// </summary>
    public int DrawFooter(Gump g, int x, int y, int buttonId, string action, bool waived = false)
    {
        var state = AllMet ? RequirementState.Met : RequirementState.NotMet;
        g.AddImage(x, y - (AllMet ? 1 : -1), ArtOf(state));
        g.AddLabel(x + RequirementStyle.TextIndent, y + 1, HueOf(state),
            waived && !AllMet ? $"{Summary} (waived: Testing Token)" : Summary);
        y += RequirementStyle.LineHeight + 4;

        RequirementStyle.AddAction(g, x, y, buttonId, action, AllMet || waived);
        return y + 26;
    }
}
