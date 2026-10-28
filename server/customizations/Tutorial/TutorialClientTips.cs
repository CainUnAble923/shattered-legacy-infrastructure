// cc-P718 item 3 (Chase's isle walk, 10-28): an optional lesson, at any time and never needed to graduate, that teaches
// useful client settings in plain words. The first tip is always run (P719 makes new clients start with it off). Each tip
// is one row of Tips: more (auto-open doors, the journal, macros) are added there. The page offers TazUO's options window
// itself when the client answered our command handshake (TazUOClientCommand, cc-P103), and words alone otherwise.
// "Done" finishes the lesson: the server cannot see a client setting, and an optional page needs no evidence. Notes:
// shard-migration notes/cc-P718-isle-walkthrough-fixes-1.md.

#nullable enable

using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;

namespace Server.Tutorial;

public static class TutorialClientTips
{
    public const string Key = "client.tips";

    /// <summary>The tips, in order: a title and what to do. TazUO's menu path, read from its options window (Gameplay, Movement).</summary>
    public static readonly (string Title, string Text)[] Tips =
    [
        ("Always run",
            "Your character can run wherever you point, not only when you point far away. To turn it on: click Options on " +
            "your character window (double-click yourself to open it), choose Gameplay, then Movement, and tick Always run. " +
            "Close the options window when you are done.")
    ];

    public static void Start(PlayerMobile pm, TutorialStation station)
    {
        pm.CloseGump<TutorialClientTipsGump>();
        pm.SendGump(new TutorialClientTipsGump(TazUOClientCommand.CanRunCommands(pm.NetState)));
    }

    public static void Done(PlayerMobile pm)
    {
        ClusterFTutorial.CompleteStation(pm, Key);
        ClusterFTutorial.SystemLine(pm, "Client tips done. You can open them again from Tamsin's or Rook's window at any time.");
    }
}

public sealed class TutorialClientTipsGump : Gump
{
    public const int BtnDone = 1;
    public const int BtnOptions = 2;

    public TutorialClientTipsGump(bool canOpenOptions) : base(80, 80)
    {
        var h = 110 + TutorialClientTips.Tips.Length * 120 + (canOpenOptions ? 36 : 0);
        TutorialStyle.Panel(this, 480, h);
        AddLabel(24, 18, TutorialStyle.HeadingHue, "Client tips (optional)");
        var y = 48;
        foreach (var (title, text) in TutorialClientTips.Tips)
        {
            AddLabel(24, y, TutorialStyle.ActionHue, title);
            AddHtml(24, y + 22, 432, 90, TutorialStyle.Html(text), false, false);
            y += 120;
        }

        if (canOpenOptions)
        {
            AddButton(24, y, 4005, 4007, BtnOptions);
            AddLabel(60, y + 2, TutorialStyle.ActionHue, "Open the options window");
            y += 36;
        }

        AddButton(24, y, 4023, 4025, BtnDone);
        AddLabel(60, y + 2, TutorialStyle.DoneHue, "Done");
        AddButton(240, y, 4017, 4019, 0);
        AddLabel(276, y + 2, TutorialStyle.TextHue, "Skip for now");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case BtnOptions:
                TazUOClientCommand.Send(pm, "options");
                pm.SendGump(new TutorialClientTipsGump(true));
                break;
            case BtnDone:
                TutorialClientTips.Done(pm);
                break;
        }
    }
}
