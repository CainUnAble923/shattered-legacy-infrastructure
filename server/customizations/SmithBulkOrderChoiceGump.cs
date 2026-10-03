using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P42 Part F, Chase 2026-10-03; D51). The Society of Smiths' Bulk Order button: the player picks
/// a small or a large order. Large is offered only at 70.1 Blacksmithy (skill only, no rank); below it the gump says
/// why. Either choice goes through BlacksmithGuildmaster.OfferBOD, which needs no NPC and shows pinned's own accept
/// gump, so the button works from the guild page opened anywhere (guild directory, Hammer of Hephaestus) as well as
/// from the guildmaster. Not OSI: OSI's smiths choose small or large at random; see the deviation register.
///
/// cc-P46 Part B (Chase, 2026-10-03): the character's "Orders that still teach me" setting, a check box that flips it
/// and redraws the gump (ClusterFSmithTeaching.SetWantsTeaching). The guild book's order page shows the same setting.
/// </summary>
public class SmithBulkOrderChoiceGump : Gump
{
    public const int BtnSmall = 1;
    public const int BtnLarge = 2;
    public const int BtnTeaching = 3;

    // The client's check box art: 210 unchecked, 211 checked.
    public const int CheckOff = 210;
    public const int CheckOn = 211;

    private const int W = 340;
    private const int H = 240;

    private readonly PlayerMobile _pm;

    public SmithBulkOrderChoiceGump(PlayerMobile pm) : base(120, 100)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        var (small, large) = BlacksmithGuildmaster.CountBODs(pm);
        var meetsLarge     = BlacksmithGuildmaster.MeetsLargeSkill(pm);

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        AddLabel(18, 16, 1153, "Society of Smiths - Bulk Order");
        AddImageTiled(10, 38, W - 20, 2, 9304);
        AddLabel(18, 48, 999, $"Active orders: {small} small, {large} large");

        AddButton(18, 78, 4005, 4007, BtnSmall);
        AddLabel(54, 80, 1154, "Small bulk order");

        if (meetsLarge)
        {
            AddButton(18, 108, 4005, 4007, BtnLarge);
            AddLabel(54, 110, 1154, "Large bulk order");
        }
        else
        {
            AddLabel(18, 110, 0x3B2, "Large bulk order: needs 70.1 Blacksmithy");
            AddLabel(18, 130, 0x3B2, $"(yours is {pm.Skills.Blacksmith.Base:F1})");
        }

        // cc-P46 Part B: the teaching-orders setting.
        var teaching = ClusterFSmithTeaching.WantsTeaching(pm);
        AddImageTiled(10, 152, W - 20, 2, 9304);
        AddButton(18, 162, teaching ? CheckOn : CheckOff, teaching ? CheckOff : CheckOn, BtnTeaching);
        AddLabel(44, 162, 1154, ClusterFSmithTeaching.ToggleLabel);
        AddLabel(44, 182, 0x3B2, ClusterFSmithTeaching.ToggleHint);

        AddButton(W - 50, H - 34, 4017, 4019, 0);
        AddLabel(W - 90, H - 32, 999, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || pm != _pm) return;

        switch (info.ButtonID)
        {
            case BtnSmall:
                BlacksmithGuildmaster.OfferBOD(pm, large: false);
                break;
            case BtnLarge:
                // The button is not drawn below the skill; a client can still send it, so the rule is checked again.
                BlacksmithGuildmaster.OfferBOD(pm, large: true);
                break;
            case BtnTeaching:
                ToggleTeaching(pm);
                pm.SendGump(new SmithBulkOrderChoiceGump(pm));
                break;
        }
    }

    /// <summary>Flips the character's teaching-orders setting and says what it is now. Both gumps use this.</summary>
    public static void ToggleTeaching(PlayerMobile pm)
    {
        var on = !ClusterFSmithTeaching.WantsTeaching(pm);
        ClusterFSmithTeaching.SetWantsTeaching(pm, on);
        pm.SendMessage(0x59, on
            ? "Society orders will ask for items that still teach you."
            : "Society orders will ask for any item you can make.");
    }
}
