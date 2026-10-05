using System;
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
    public const int BtnBack = 4; // cc-P52 Part E (D65): back to the gump that opened this one
    public const int BtnTurnIn = 5; // cc-P55 Part H: bank, cash out or ask

    // The client's check box art: 210 unchecked, 211 checked.
    public const int CheckOff = 210;
    public const int CheckOn = 211;

    private const int W = 340;
    private const int H = 290; // cc-P55 Part H: 50 taller for the turn-in setting

    private readonly PlayerMobile _pm;
    private readonly Action<PlayerMobile>? _back;

    public SmithBulkOrderChoiceGump(PlayerMobile pm, Action<PlayerMobile>? back = null) : base(120, 100)
    {
        _pm = pm;
        _back = back;

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

        // cc-P55 Part H: the character's turn-in setting; each press moves it to the next of bank, cash out, ask.
        var mode = ClusterFSmithBODPayout.GetMode(pm);
        AddButton(18, 206, 4005, 4007, BtnTurnIn);
        AddLabel(54, 208, 1154, $"{ClusterFSmithBODPayout.ToggleLabel} {ClusterFSmithBODPayout.ModeLabel(mode)}");
        AddLabel(54, 228, 0x3B2, ClusterFSmithBODPayout.ToggleHint);

        if (back != null)
        {
            AddButton(18, H - 34, 4014, 4016, BtnBack);
            AddLabel(52, H - 32, 999, "Back");
        }

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
                pm.SendGump(new SmithBulkOrderChoiceGump(pm, _back));
                break;
            case BtnTurnIn:
                ClusterFSmithBODPayout.CycleMode(pm);
                pm.SendGump(new SmithBulkOrderChoiceGump(pm, _back));
                break;
            case BtnBack:
                _back?.Invoke(pm);
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
