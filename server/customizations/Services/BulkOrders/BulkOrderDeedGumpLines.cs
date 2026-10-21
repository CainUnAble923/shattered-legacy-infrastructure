// cc-P122: the two lines OSI's 2016 deed gumps add, from ServUO SmallBODGump.cs:82-96 and LargeBODGump.cs (the same
// lines): what the deed is worth ("~1_POINTS~ turn in points and ~2_POINTS~ bank points", 1157301) and the button that
// cycles the player's points mode (Banking Points Enabled, Disabled or Automatic: 1157302, 1157303, 1157309). It is the
// only place ServUO lets a player change the mode. Pinned's SmallBODGump and LargeBODGump call in here
// (BOD-gumps-points-mode.patch) and grow by Height rows; for a deed no 2016 vendor takes, or with the system off, they
// draw as before.
//
// cc-P168: for a member of the deed's guild the lines say what the turn-in pays them (BulkOrderGuildPay): a Society of
// Smiths member sees the Seals or cash-out gold and standing, and the Completed orders setting, in place of the points and
// the points-mode button (the Society pays no points); a member of one of the 2016 guilds keeps both and gets a gold line
// between them. A non-member sees the two lines as before.

using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Engines.BulkOrders;

public static class BulkOrderDeedGumpLines
{
    /// <summary>The toggle's button id. 1 is EXIT, 2 combine, 3 combine contained (BODCombineContained).</summary>
    public const int ToggleButton = 5;

    private const int Row = 24;

    private static PlayerMobile Viewer(BaseBOD deed) => deed.RootParent as PlayerMobile;

    public static bool Shows(BaseBOD deed) =>
        BulkOrderSystem.NewSystemEnabled && BulkOrderSystem.GetBODType(deed) != null && Viewer(deed) != null;

    /// <summary>The extra height the lines take: two rows (three for a member of a 2016 guild), or none.</summary>
    public static int Height(BaseBOD deed) => !Shows(deed) ? 0 : OtherGuildMember(deed) ? Row * 3 : Row * 2;

    private static bool OtherGuildMember(BaseBOD deed) =>
        BulkOrderGuildPay.IsMember(Viewer(deed), deed) && !BulkOrderGuildPay.IsSmithMember(Viewer(deed), deed);

    /// <summary>The text lines drawn, in order (the button's label is a cliloc and not among them).</summary>
    public static List<string> TextLines(BaseBOD deed)
    {
        var lines = new List<string>();
        if (!Shows(deed))
        {
            return lines;
        }

        if (BulkOrderGuildPay.IsSmithMember(Viewer(deed), deed))
        {
            var (pay, setting) = BulkOrderGuildPay.SmithLines(Viewer(deed), deed);
            lines.Add(pay);
            lines.Add(setting);
            return lines;
        }

        BulkOrderSystem.ComputePoints(deed, out var points, out var banked);
        lines.Add($"Worth {points} turn in points and {banked:0.000000} bank points"); // 1157301's words
        if (OtherGuildMember(deed))
        {
            lines.Add(BulkOrderGuildPay.OtherGuildLine(deed));
        }

        return lines;
    }

    public static void Add(ref DynamicGumpBuilder builder, BaseBOD deed, int y)
    {
        if (!Shows(deed))
        {
            return;
        }

        // cc-P168: a Society member's deed: what the Society pays, and the setting it pays by.
        if (BulkOrderGuildPay.IsSmithMember(Viewer(deed), deed))
        {
            var (pay, setting) = BulkOrderGuildPay.SmithLines(Viewer(deed), deed);
            builder.AddLabel(75, y, 1152, pay);
            builder.AddLabel(75, y + Row, 1152, setting);
            return;
        }

        if (OtherGuildMember(deed))
        {
            builder.AddLabel(75, y + Row, 1152, BulkOrderGuildPay.OtherGuildLine(deed));
            AddPointsLines(ref builder, deed, y, Row * 2);
            return;
        }

        AddPointsLines(ref builder, deed, y, Row);
    }

    // The 2016 lines: points at y, the points-mode button `toggleAt` below.
    private static void AddPointsLines(ref DynamicGumpBuilder builder, BaseBOD deed, int y, int toggleAt)
    {
        BulkOrderSystem.ComputePoints(deed, out var points, out var banked);

        // Worth ~1_POINTS~ turn in points and ~2_POINTS~ bank points.
        builder.AddHtmlLocalized(75, y, 300, 20, 1157301, $"{points}\t{banked:0.000000}", 0x7FFF);

        var mode = BulkOrderSystem.GetContext(Viewer(deed)).PointsMode;

        builder.AddButton(125, y + toggleAt, 4005, 4007, ToggleButton);
        builder.AddHtmlLocalized(
            160,
            y + toggleAt,
            300,
            20,
            mode switch
            {
                PointsMode.Enabled  => 1157302, // Banking Points Enabled
                PointsMode.Disabled => 1157303, // Banking Points Disabled
                _                   => 1157309  // Banking Points Automatic
            },
            0x7FFF
        );
    }

    /// <summary>ServUO's button 3: Enabled, then Disabled, then Automatic, then Enabled.</summary>
    public static void Toggle(Mobile from)
    {
        var context = BulkOrderSystem.GetContext(from);

        if (context != null)
        {
            context.PointsMode = context.PointsMode switch
            {
                PointsMode.Enabled  => PointsMode.Disabled,
                PointsMode.Disabled => PointsMode.Automatic,
                _                   => PointsMode.Enabled
            };
        }
    }
}
