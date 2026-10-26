// cc-P177 (Chase 2026-10-18 20:30: "all guild menus need to be uniform in UI and style, we may need to add some art? if
// possible to help differentiate while in menu"). One base for every guild menu, guild book and League page. Proposed
// here with mockups for Chase (shard-migration notes/cc-P177-guild-menus-template-mockups.md); no menu uses it yet.
// cc-P178 moves the menus onto it once Chase approves the look.
//
// Layout (560 wide; 440 tall unless a menu asks for more):
//
//   +--------------------------------------------------------------------+
//   | [crest]  Society of Smiths  (accent hue)          Smithing Seals   |
//   |  60x60   Guildmaster Aldric, Minoc (gray)          1,240           |
//   |          Rank: Journeyman   Standing 6,200 / 15,000 to Master      |
//   |====================================================================|
//   | > Overview  |  Section heading (ice white)                          |
//   |   Orders    |  ---------------------------------------------------  |
//   |   Catalog   |  content: text, rows, item rows, requirement lines    |
//   |   Hammer    |  (RequirementList, cc-P169: met green + check, not    |
//   |   ...       |  met red + X, info gray), the action under them       |
//   |====================================================================|
//   | <- Back            < Page 2 of 3 >                        Close [x] |
//   +--------------------------------------------------------------------+
//
// Per guild only: the crest, the accent hue (GuildMenuThemes) and the content. Everything else is here: the frame
// (9270) with cc-P169's solid panel (2624), the rules (9304), the button art (4005 sections and rows, 4014 back and
// previous, 4017 close), the heading, info and requirement hues (RequirementStyle), and paging.
//
// Crests are the client's gold guild medallions (gumpartLegacyMUL.uop 5545-5592, 60 x 60, one per classic guild type).
// A guild without one (the Custodians, the Keepers of the Last Door) draws a placeholder: the 9270 frame at crest size
// with the guild's item art in it, until the art lane (cc-P152) delivers a crest.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps;

/// <summary>A guild's look in its menus: its name, crest and accent hue. Everything else is shared.</summary>
public sealed record GuildMenuTheme(
    string Key,
    string Name,
    int AccentHue,
    int CrestGump,
    int PlaceholderItem,
    int PlaceholderItemHue = 0
)
{
    /// <summary>False until the guild has crest art; the menu then draws the placeholder (frame and item art).</summary>
    public bool HasCrest => CrestGump > 0;
}

public static class GuildMenuThemes
{
    public const string LeagueKey = "league";

    // Accent hues are label hues (the client draws entry N + 1 of hues.mul). None is the met green (0x44), the not met
    // rose (0x22) or the heading white (1153), so a guild's colour never reads as a requirement's. Each comment names
    // the crest's picture and the accent; the item is the placeholder's art, drawn only while the crest is 0.
    private static readonly GuildMenuTheme[] _themes =
    [
        new("smithing", "Society of Smiths", 0x2B, 5555, 0xFAF),             // anvil; forge orange
        new("mining", "Miners' Compact", 0x8A5, 5567, 0xE86),                // pickaxe; gold
        new("artificers", "Artificers' Order", 0x76, 5585, 0x2B70),          // a glowing blade (stand-in); violet
        new("custodians", "The Custodians", 0x58, 0, 0xB025),                // no crest yet: a broom; cyan
        new("arcane", "Arcane Society", 0x05, 5569, 0xEFA),                  // wizard's hat; periwinkle
        new("tailoring", "Tailors' Circle", 0x80, 5579, 0xF9F),              // scissors; pink
        new("tinkers", "Tinkers' Union", 0x96D, 5581, 0x1053),               // gears; brass
        new("healers", "Healers' Covenant", 0x5A, 5563, 0xE21),              // ankh; pale blue
        new("bards", "Bards' Consortium", 0x14, 5553, 0xEB3),                // lute; orchid
        new("thieves", "Thieves' Den", 0x8FD, 5575, 0xF52),                  // hooded figure; smoke gray
        new("warriors", "Warriors' Brotherhood", 0x972, 5577, 0x13B9),       // crossed swords; bronze
        new("rangers", "Rangers' League", 0x92, 5551, 0x13B2),               // bow; dark gold
        new("maritime", "Maritime Brotherhood", 0x501, 5565, 0x9CC),         // fish; sea blue
        new("merchants", "Merchants' Exchange", 0x35, 5571, 0xEED),          // coins; yellow
        new("foresters", "Foresters' Union", 0xA0, 5557, 0xF43),             // tree; pale lime
        new("dojo", "Twin Paths Dojo", 0x94, 5589, 0x27AC),                  // shuriken; amber
        new("keepers", "Keepers of the Last Door", 0x10, 0, 0x1853),         // no crest yet: a skull; purple
        new("alembic", "Order of the Alembic", 0x494, 5583, 0xF0E),          // a provisioner's table (stand-in); teal
        new(LeagueKey, "League of Extraordinary Citizens", 0x1F4, 5545, 0x14F0) // Britannia's sigil (stand-in); steel blue
    ];

    private static readonly Dictionary<string, GuildMenuTheme> _byKey =
        _themes.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<GuildMenuTheme> All => _themes;

    /// <summary>The guild's theme, or a plain one (no crest, white accent) for a key without one.</summary>
    public static GuildMenuTheme Get(string key) =>
        _byKey.TryGetValue(key ?? "", out var theme)
            ? theme
            : new GuildMenuTheme(key ?? "", ClusterFGuildSystem.GetDef(key ?? "")?.Name ?? key ?? "", 1153, 0, 0x14F0);
}

/// <summary>The header's rank line: rank, standing toward the next rank, and the guild's currency.</summary>
public sealed record GuildMenuRank(
    string RankName,
    long Standing,
    long NextAt,
    string NextName,
    string CurrencyName,
    long Currency
)
{
    /// <summary>"Standing 6,200 / 15,000 to Master", or "Standing 120,000 (top rank)".</summary>
    public string StandingText => NextAt > 0
        ? $"Standing {Standing:N0} / {NextAt:N0} to {NextName}"
        : $"Standing {Standing:N0} (top rank)";

    /// <summary>A guild member's line from their guild data, with the guild's own rank names and currency.</summary>
    public static GuildMenuRank For(string key, CharacterGuildData data)
    {
        var standing = data.GetReputation(key);
        var thresholds = ClusterFGuildSystem.RankThresholds(key);
        var next = thresholds.FirstOrDefault(t => t > standing);

        return new GuildMenuRank(
            ClusterFGuildSystem.GetRankName(key, data),
            standing,
            next,
            next > 0 ? ClusterFGuildSystem.GetRankName(key, next) : "",
            MuleUpgrades.CurrencyName(key),
            data.GetCurrency(key)
        );
    }
}

/// <summary>
/// The shared guild menu. A menu sets its theme, subtitle, rank line and sections, then calls <see cref="Build"/> at
/// the end of its constructor; Build draws the frame and calls <see cref="DrawContent"/> for the open section.
/// Button ids: 0 close, 1 back, 2 and 3 previous and next page, 10 + n section n, 100 and up the content's own.
/// </summary>
public abstract class GuildMenuGump : GuildStyledGump
{
    public const int Width = 560;
    public const int DefaultHeight = 440;

    public const int FrameArt = 9270;
    public const int RuleTile = 9304;
    public const int RowArt = 4005;      // the arrow button: sections, rows, next page
    public const int RowArtPressed = 4007;
    public const int RowArtSelected = 4006;
    public const int BackArt = 4014;     // back and previous page
    public const int BackArtPressed = 4016;
    public const int CloseArt = 4017;
    public const int CloseArtPressed = 4019;

    public const int CrestX = 18;
    public const int CrestY = 16;
    public const int CrestSize = 60;
    public const int HeaderHeight = 92;
    public const int MenuWidth = 150;
    public const int MenuRowHeight = 26;
    public const int FooterHeight = 44;
    public const int ContentX = MenuWidth + 16;
    public const int ContentY = HeaderHeight + 12;
    public const int ContentWidth = Width - ContentX - 18;
    public const int RowHeight = 26;
    public const int ItemRowHeight = 46;

    public const int BtnClose = 0;
    public const int BtnBack = 1;
    public const int BtnPrevious = 2;
    public const int BtnNext = 3;
    public const int BtnSectionBase = 10;
    public const int BtnContentBase = 100;

    protected readonly GuildMenuTheme Theme;
    protected readonly int Height;

    protected string Subtitle { get; set; } = "";
    protected GuildMenuRank? Rank { get; set; }
    protected IReadOnlyList<string> Sections { get; set; } = [];
    protected int Section { get; set; }
    protected int Page { get; set; }
    protected int PageCount { get; set; } = 1;
    protected bool HasBack { get; set; }

    protected GuildMenuGump(GuildMenuTheme theme, int height = DefaultHeight) : base(100, 80)
    {
        Theme = theme;
        Height = Math.Max(DefaultHeight, height);
    }

    /// <summary>The y the content must stay above (the footer's rule is under it).</summary>
    protected int ContentBottom => Height - FooterHeight - 6;

    /// <summary>Draws the frame, header, section menu and footer, then the open section's content.</summary>
    protected void Build()
    {
        Section = Sections.Count == 0 ? 0 : Math.Clamp(Section, 0, Sections.Count - 1);
        PageCount = Math.Max(1, PageCount);
        Page = Math.Clamp(Page, 0, PageCount - 1);

        AddPage(0);
        AddBackground(0, 0, Width, Height, FrameArt);
        RequirementStyle.AddSolidPanel(this, Width, Height);

        DrawHeader();
        DrawMenu();
        DrawFooter();
        DrawContent(ContentY);
    }

    /// <summary>The open section's content, from <paramref name="y"/> down to <see cref="ContentBottom"/>.</summary>
    protected abstract void DrawContent(int y);

    private void DrawHeader()
    {
        if (Theme.HasCrest)
        {
            AddImage(CrestX, CrestY, Theme.CrestGump);
        }
        else
        {
            AddBackground(CrestX, CrestY, CrestSize, CrestSize, FrameArt);
            AddItem(CrestX + 12, CrestY + 12, Theme.PlaceholderItem, Theme.PlaceholderItemHue);
        }

        const int textX = CrestX + CrestSize + 16;
        AddLabel(textX, 18, Theme.AccentHue, Theme.Name);

        if (!string.IsNullOrEmpty(Subtitle))
        {
            AddLabel(textX, 38, RequirementStyle.InfoHue, Subtitle);
        }

        if (Rank != null)
        {
            AddLabel(textX, 60, RequirementStyle.HeadingHue, $"Rank: {Rank.RankName}");
            AddLabel(textX + 150, 60, RequirementStyle.InfoHue, Rank.StandingText);

            if (!string.IsNullOrEmpty(Rank.CurrencyName))
            {
                AddLabel(Width - 150, 18, RequirementStyle.InfoHue, Rank.CurrencyName);
                AddLabel(Width - 150, 38, RequirementStyle.HeadingHue, $"{Rank.Currency:N0}");
            }
        }

        AddImageTiled(10, HeaderHeight, Width - 20, 2, RuleTile);
    }

    private void DrawMenu()
    {
        var y = ContentY;

        for (var i = 0; i < Sections.Count; i++, y += MenuRowHeight)
        {
            if (i == Section)
            {
                AddImage(16, y, RowArtSelected);
                AddLabel(50, y + 2, Theme.AccentHue, Sections[i]);
            }
            else
            {
                AddButton(16, y, RowArt, RowArtPressed, BtnSectionBase + i);
                AddLabel(50, y + 2, RequirementStyle.InfoHue, Sections[i]);
            }
        }

        AddImageTiled(MenuWidth, HeaderHeight + 8, 2, Height - HeaderHeight - FooterHeight - 14, RuleTile);
    }

    private void DrawFooter()
    {
        var y = Height - FooterHeight;
        AddImageTiled(10, y, Width - 20, 2, RuleTile);
        y += 12;

        if (HasBack)
        {
            AddButton(18, y, BackArt, BackArtPressed, BtnBack);
            AddLabel(52, y + 2, RequirementStyle.InfoHue, "Back");
        }

        if (PageCount > 1)
        {
            const int x = Width / 2 - 70;
            NavButton(x, y, BackArt, BackArtPressed, BtnPrevious, Page > 0);
            AddLabel(x + 40, y + 2, RequirementStyle.InfoHue, $"Page {Page + 1} of {PageCount}");
            NavButton(x + 124, y, RowArt, RowArtPressed, BtnNext, Page < PageCount - 1);
        }

        AddButton(Width - 92, y, CloseArt, CloseArtPressed, BtnClose);
        AddLabel(Width - 58, y + 2, RequirementStyle.InfoHue, "Close");
    }

    // A page button with nowhere to go is its art in the disabled gray, no button (cc-P169's rule for actions).
    private void NavButton(int x, int y, int art, int pressed, int id, bool enabled)
    {
        if (enabled)
        {
            AddButton(x, y, art, pressed, id);
        }
        else
        {
            AddImage(x, y, art, RequirementStyle.DisabledHue);
        }
    }

    // -- Content helpers ---------------------------------------------------------------------------------------------

    /// <summary>A section heading in ice white with a rule under it; returns the y under the rule.</summary>
    protected int Heading(int y, string text)
    {
        AddLabel(ContentX, y, RequirementStyle.HeadingHue, text);
        AddImageTiled(ContentX, y + 20, ContentWidth, 1, RuleTile);
        return y + 28;
    }

    /// <summary>One line of gray text; returns the y under it.</summary>
    protected int Text(int y, string text, int hue = RequirementStyle.InfoHue)
    {
        AddLabel(ContentX, y, hue, text);
        return y + 20;
    }

    /// <summary>Prose that wraps, in the info gray; returns the y under its box.</summary>
    protected int Paragraph(int y, int height, string text)
    {
        AddHtml(ContentX, y, ContentWidth, height, $"<BASEFONT COLOR=#ACACAC>{text}</BASEFONT>");
        return y + height + 4;
    }

    /// <summary>A label and its value on one line ("Contracts done" ... "12").</summary>
    protected int Field(int y, string label, string value, int valueHue = RequirementStyle.HeadingHue)
    {
        AddLabel(ContentX, y, RequirementStyle.InfoHue, label);
        AddLabel(ContentX + 190, y, valueHue, value);
        return y + 20;
    }

    /// <summary>A row that opens something: the arrow, its words, and a gray note on the right.</summary>
    protected int ActionRow(int y, int buttonId, string text, string note = null)
    {
        AddButton(ContentX, y, RowArt, RowArtPressed, buttonId);
        AddLabel(ContentX + 34, y + 2, RequirementStyle.HeadingHue, text);

        if (!string.IsNullOrEmpty(note))
        {
            AddLabel(ContentX + 230, y + 2, RequirementStyle.InfoHue, note);
        }

        return y + RowHeight;
    }

    /// <summary>
    /// A store or book row: the item's art, its name and note, its price in the met or not met colour, and the action
    /// live only when <paramref name="canTake"/> (otherwise its art in the disabled gray, no button).
    /// </summary>
    protected int ItemRow(int y, int itemId, int itemHue, string name, string note, string price, bool canTake, int buttonId)
    {
        AddItem(ContentX, y + 2, itemId, itemHue);
        AddLabel(ContentX + 52, y + 2, RequirementStyle.HeadingHue, name);

        if (!string.IsNullOrEmpty(note))
        {
            AddLabel(ContentX + 52, y + 22, RequirementStyle.InfoHue, note);
        }

        AddLabel(ContentX + 250, y + 2, canTake ? RequirementStyle.MetHue : RequirementStyle.NotMetHue, price);
        NavButton(ContentX + ContentWidth - 22, y, RowArt, RowArtPressed, buttonId, canTake);

        AddImageTiled(ContentX, y + ItemRowHeight - 4, ContentWidth, 1, RuleTile);
        return y + ItemRowHeight;
    }

    /// <summary>Requirement lines (cc-P169), the summary and the action; returns the y under the action.</summary>
    protected int Requirements(int y, RequirementList list, int buttonId, string action, bool waived = false)
    {
        y = list.Draw(this, ContentX, y);
        return list.DrawFooter(this, ContentX, y + 4, buttonId, action, waived);
    }

    // -- Responses ---------------------------------------------------------------------------------------------------

    /// <summary>Opens this menu again at a section and page (a section press opens its first page).</summary>
    protected abstract void Reopen(PlayerMobile pm, int section, int page);

    /// <summary>The menu that opened this one, when <see cref="HasBack"/>.</summary>
    protected virtual void OnBack(PlayerMobile pm)
    {
    }

    /// <summary>A content button (100 and up).</summary>
    protected virtual void OnContentButton(PlayerMobile pm, int buttonId, in RelayInfo info)
    {
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var id = info.ButtonID;

        if (id == BtnClose)
        {
            return;
        }

        if (id == BtnBack)
        {
            if (HasBack)
            {
                OnBack(pm);
            }
        }
        else if (id == BtnPrevious || id == BtnNext)
        {
            var page = Page + (id == BtnNext ? 1 : -1);

            if (page >= 0 && page < PageCount)
            {
                Reopen(pm, Section, page);
            }
        }
        else if (id >= BtnSectionBase && id < BtnContentBase)
        {
            if (id - BtnSectionBase < Sections.Count)
            {
                Reopen(pm, id - BtnSectionBase, 0);
            }
        }
        else
        {
            OnContentButton(pm, id, info);
        }
    }
}
