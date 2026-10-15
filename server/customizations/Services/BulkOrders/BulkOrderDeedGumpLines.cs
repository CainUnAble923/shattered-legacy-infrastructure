// cc-P122: the two lines OSI's 2016 deed gumps add, from ServUO SmallBODGump.cs:82-96 and LargeBODGump.cs (the same
// lines): what the deed is worth ("~1_POINTS~ turn in points and ~2_POINTS~ bank points", 1157301) and the button that
// cycles the player's points mode (Banking Points Enabled, Disabled or Automatic: 1157302, 1157303, 1157309). It is the
// only place ServUO lets a player change the mode. Pinned's SmallBODGump and LargeBODGump call in here
// (BOD-gumps-points-mode.patch) and grow by Height rows; for a deed no 2016 vendor takes, or with the system off, they
// draw as before.

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

    /// <summary>The extra height the two lines take: two rows, or none.</summary>
    public static int Height(BaseBOD deed) => Shows(deed) ? Row * 2 : 0;

    public static void Add(ref DynamicGumpBuilder builder, BaseBOD deed, int y)
    {
        if (!Shows(deed))
        {
            return;
        }

        BulkOrderSystem.ComputePoints(deed, out var points, out var banked);

        // Worth ~1_POINTS~ turn in points and ~2_POINTS~ bank points.
        builder.AddHtmlLocalized(75, y, 300, 20, 1157301, $"{points}\t{banked:0.000000}", 0x7FFF);

        var mode = BulkOrderSystem.GetContext(Viewer(deed)).PointsMode;

        builder.AddButton(125, y + Row, 4005, 4007, ToggleButton);
        builder.AddHtmlLocalized(
            160,
            y + Row,
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
