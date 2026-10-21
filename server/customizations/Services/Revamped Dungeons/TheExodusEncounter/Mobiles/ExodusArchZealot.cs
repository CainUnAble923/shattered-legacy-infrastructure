// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusArchZealot.cs (cc-P195): Hunter the Arch
// Zealot, the unmoving, blessed preacher at 946, 641, -79 who explains the ritual (a ten-page gump from his context
// menu, 6146 "Talk") and swaps a gargoyle's human dagger for a gargish one when handed it within two tiles. A plain
// Mobile, as ServUO's; the beard is pinned's facial hair rather than ServUO's item on the facial-hair layer.
// ServUO's IsInvulnerable virtual has no pinned reader on a Mobile; Blessed is what keeps him safe on both.

using ModernUO.Serialization;
using Server.Collections;
using Server.Commands;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusArchZealot : Mobile
{
    [Constructible]
    public ExodusArchZealot()
    {
        Name = "Hunter";
        Title = "the Arch Zealot";
        Body = 0x190;
        CantWalk = true;
        Hue = 0;
        Blessed = true;

        AddItem(new HoodedShroudOfShadows(0xA91));
        AddItem(new ThighBoots());

        FacialHairItemID = 0x2040;
        FacialHairHue = 902;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from.InRange(Location, 2) && from.Race == Race.Gargoyle && dropped.GetType() == typeof(ExodusSacrificalDagger))
        {
            dropped.Delete();
            from.AddToBackpack(new ExodusSacrificalGargishDagger());
            return true;
        }

        return base.OnDragDrop(from, dropped);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        list.Add(new TalkEntry());
    }

    private class TalkEntry : ContextMenuEntry
    {
        public TalkEntry() : base(6146, 3)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile pm && !pm.HasGump<ExodusArchZealotGump>())
            {
                pm.SendGump(new ExodusArchZealotGump());
            }
        }
    }
}

/// <summary>ServUO ExodusArchZealotGump: ten pages of the Arch Zealot's lore. [ExodusArchZealotGump shows it to staff.</summary>
public class ExodusArchZealotGump : Gump
{
    public const int White = 0x7FFF;

    public static void Initialize()
    {
        CommandSystem.Register("ExodusArchZealotGump", AccessLevel.GameMaster, ExodusArchZealotGump_OnCommand);
    }

    [Usage("ExodusArchZealotGump")]
    [Description("Shows Hunter the Arch Zealot's ten-page lore gump (ServUO's debug command).")]
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Duplicates, Shard = CommandShard.Any)]
    public static void ExodusArchZealotGump_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendGump(new ExodusArchZealotGump());
    }

    public ExodusArchZealotGump() : base(50, 50)
    {
        AddPage(0);
        AddImageTiled(54, 33, 369, 400, 2624);
        AddAlphaRegion(54, 33, 369, 400);

        AddImageTiled(416, 39, 44, 389, 203);

        AddImage(97, 49, 9005);
        AddImageTiled(58, 39, 29, 390, 10460);
        AddImageTiled(412, 37, 31, 389, 10460);
        AddHtmlLocalized(140, 60, 250, 24, 1153670, White, false, false); // Exodus Arch Zealot
        AddImage(430, 9, 10441);
        AddImageTiled(40, 38, 17, 391, 9263);
        AddImage(6, 25, 10421);
        AddImage(34, 12, 10420);
        AddImageTiled(94, 25, 342, 15, 10304);
        AddImageTiled(40, 427, 415, 16, 10304);
        AddImage(-10, 314, 10402);
        AddImage(56, 150, 10411);
        AddImage(136, 84, 96);

        // Page 1: the greeting, and the six questions.
        AddPage(1);
        AddHtmlLocalized(107, 110, 300, 230, 1153671, White, false, true);
        AddQuestion(260, 1153672, 2); // Ritual?
        AddQuestion(280, 1153674, 3); // Summoning Altar?
        AddQuestion(300, 1153676, 4); // Robe of Rite?
        AddQuestion(320, 1153678, 5); // Summoning Rite?
        AddQuestion(340, 1153680, 6); // Sacrificial Dagger?
        AddQuestion(360, 1153682, 7); // The Keys?
        AddOk();

        // Page 2: the Ritual.
        AddPage(2);
        AddHtmlLocalized(107, 110, 300, 230, 1153673, White, false, true);
        AddQuestion(280, 1153674, 3);
        AddQuestion(300, 1153676, 4);
        AddQuestion(320, 1153678, 5);
        AddQuestion(340, 1153680, 6);
        AddQuestion(360, 1153682, 7);
        AddOk();

        // Page 3: the Summoning Altar.
        AddPage(3);
        AddHtmlLocalized(107, 110, 300, 230, 1153675, White, false, true);
        AddQuestion(300, 1153676, 4);
        AddQuestion(320, 1153678, 5);
        AddQuestion(340, 1153680, 6);
        AddQuestion(360, 1153682, 7);
        AddOk();

        // Page 4: the Robe of Rite.
        AddPage(4);
        AddHtmlLocalized(107, 110, 300, 230, 1153677, White, false, true);
        AddQuestion(320, 1153678, 5);
        AddQuestion(340, 1153680, 6);
        AddQuestion(360, 1153682, 7);
        AddOk();

        // Page 5: the Summoning Rite.
        AddPage(5);
        AddHtmlLocalized(107, 110, 300, 230, 1153679, White, false, true);
        AddQuestion(340, 1153680, 6);
        AddQuestion(360, 1153682, 7);
        AddOk();

        // Page 6: the Sacrificial Dagger.
        AddPage(6);
        AddHtmlLocalized(107, 110, 300, 230, 1153681, White, false, true);
        AddQuestion(360, 1153682, 7);
        AddOk();

        // Page 7: the Keys, and the three paths.
        AddPage(7);
        AddHtmlLocalized(107, 110, 300, 230, 1153683, White, false, true);
        AddQuestion(320, 1153684, 8); // The Warrior?
        AddQuestion(340, 1153686, 9); // The Rogue?
        AddQuestion(360, 1153688, 10); // The Craftsman?
        AddOk();

        // Page 8: the Warrior.
        AddPage(8);
        AddHtmlLocalized(107, 110, 300, 230, 1153685, White, false, true);
        AddQuestion(340, 1153686, 9);
        AddQuestion(360, 1153688, 10);
        AddOk();

        // Page 9: the Rogue.
        AddPage(9);
        AddHtmlLocalized(107, 110, 300, 230, 1153687, White, false, true);
        AddQuestion(360, 1153688, 10);
        AddOk();

        // Page 10: the Craftsman.
        AddPage(10);
        AddHtmlLocalized(107, 110, 300, 230, 1153689, White, false, true);
        AddOk();
    }

    private void AddQuestion(int y, int number, int page)
    {
        AddHtmlLocalized(155, y, 250, 24, number, White, false, false);
        AddButton(125, y, 0x26B0, 0x26B1, 0, GumpButtonType.Page, page);
    }

    private void AddOk() => AddButton(325, 390, 0xF7, 0xF8, 0, GumpButtonType.Reply, 0);

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (info.ButtonID == 0)
        {
            state.Mobile?.SendMessage("Good Luck!");
        }
    }
}
