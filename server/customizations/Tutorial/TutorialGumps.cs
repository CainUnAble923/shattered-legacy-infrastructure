// cc-P80: the tutorial's pages. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// TazUO (our client) takes ordinary server gumps only: no command a server can send opens one of its own windows or
// highlights a control (Part A item 5). So each station teaches its TazUO features in words, names the key, menu or
// "-command" to use, and links the tazuo.org wiki page in the gump text (an htmlgump link opens only when clicked).
// cc-P103: our patched TazUO also runs a whitelisted client command the server sends (TazUOClientCommand). A station
// whose TazUO features name the World Map, the Journal or the options window gets a button that opens it, for a client
// that answered that handshake; every other client keeps the words only.

#nullable enable

using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;

namespace Server.Tutorial;

/// <summary>The recruiter's page: the core steps left, the wings, skip, remedial, graduate.</summary>
public class TutorialRecruiterGump : Gump
{
    public const int BtnSkip = 1;
    public const int BtnRemedial = 2;
    public const int BtnGraduate = 3;
    public const int BtnStationBase = 100;

    private readonly PlayerMobile _pm;

    public TutorialRecruiterGump(PlayerMobile pm) : base(60, 60)
    {
        _pm = pm;
        var record = ClusterFTutorial.RecordOf(pm);
        var stations = TutorialStations.All;
        var h = 140 + stations.Length * 20;

        AddPage(0);
        AddBackground(0, 0, 520, h, 9270);
        AddAlphaRegion(10, 10, 500, h - 20);
        AddLabel(24, 18, 1154, "League Recruiter: New Haven orientation");
        AddLabel(24, 40, 999, $"Status: {record?.State.ToString() ?? "no account"}");

        var y = 64;
        if (record is { IsActive: true })
        {
            for (var i = 0; i < stations.Length; i++, y += 20)
            {
                var s = stations[i];
                var done = record.StationsDone.Contains(s.Key);
                AddButton(24, y, 4005, 4007, BtnStationBase + i);
                AddLabel(60, y + 2, done ? 68 : s.IsCore ? 1154 : 999,
                    $"{(s.IsCore ? "Core" : s.Wing.ToString())}: {s.Title}{(done ? " (done)" : "")}");
            }

            AddButton(24, y + 10, 4017, 4019, BtnSkip);
            AddLabel(60, y + 12, 37, "Skip the tutorial");
            if (ClusterFTutorial.CoreDone(record))
            {
                AddButton(260, y + 10, 4023, 4025, BtnGraduate);
                AddLabel(296, y + 12, 68, "Graduate");
            }
        }
        else if (record is { State: TutorialState.Skipped })
        {
            AddButton(24, y, 4005, 4007, BtnRemedial);
            AddLabel(60, y + 2, 1154, "Remedial training (the full tutorial, with its rewards)");
        }
        else
        {
            AddLabel(24, y, 999, "Nothing more to teach you here. Go and be extraordinary.");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case BtnSkip:
                _pm.SendGump(new TutorialSkipGump(1));
                return;
            case BtnRemedial:
                ClusterFTutorial.StartRemedial(_pm);
                _pm.SendGump(new TutorialRecruiterGump(_pm));
                return;
            case BtnGraduate:
                if (ClusterFTutorial.Graduate(_pm) is { } why)
                {
                    _pm.SendMessage(0x22, why);
                }

                return;
        }

        var index = info.ButtonID - BtnStationBase;
        if (index >= 0 && index < TutorialStations.All.Length)
        {
            ClusterFTutorial.RunStation(_pm, TutorialStations.All[index]);
        }
    }
}

/// <summary>The skip, confirmed twice in the System voice.</summary>
public class TutorialSkipGump : Gump
{
    public const int BtnYes = 1;
    public readonly int Step;

    public static readonly string[] Prompts =
    [
        "System: You want to skip the tutorial. Everyone who skips it says they know what they are doing. Skip?",
        "System: Really? There is an achievement for this, and it is not a compliment. Last chance. Skip?"
    ];

    public TutorialSkipGump(int step) : base(160, 160)
    {
        Step = Math.Clamp(step, 1, 2);
        AddPage(0);
        AddBackground(0, 0, 420, 150, 9270);
        AddAlphaRegion(10, 10, 400, 130);
        AddHtml(24, 20, 372, 70, $"<BASEFONT COLOR=#DDDDDD>{Prompts[Step - 1]}</BASEFONT>", false, false);
        AddButton(24, 108, 4023, 4025, BtnYes);
        AddLabel(60, 110, 37, Step == 1 ? "Yes, skip" : "Yes. Really.");
        AddButton(240, 108, 4017, 4019, 0);
        AddLabel(276, 110, 999, "No, teach me");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || info.ButtonID != BtnYes)
        {
            return;
        }

        if (Step == 1)
        {
            pm.SendGump(new TutorialSkipGump(2));
        }
        else
        {
            ClusterFTutorial.Skip(pm);
        }
    }
}

/// <summary>A station's plan, while its trial is a stub (TODO: the trial prompts).</summary>
public class TutorialStationGump : Gump
{
    public const string WikiBase = "https://tazuo.org/";
    public const int BtnWindowBase = 1;

    /// <summary>A station's TazUO feature, the client command that opens its window, and the button's words (cc-P103).</summary>
    public static readonly (string Feature, string Command, string Label)[] Windows =
    [
        ("World Map", "worldmap", "Open the World Map"),
        ("Journal and filters", "journal", "Open the Journal"),
        ("Options", "options", "Open the options")
    ];

    private readonly TutorialStation _station;

    /// <summary>The commands this gump offers as buttons, in button order (empty for a client that cannot run them).</summary>
    public readonly string[] Offered;

    public TutorialStationGump(TutorialStation s, bool canOpenWindows = false) : base(100, 100)
    {
        _station = s;
        var offered = new System.Collections.Generic.List<(string Command, string Label)>();
        if (canOpenWindows)
        {
            foreach (var w in Windows)
            {
                if (Array.IndexOf(s.TazUO, w.Feature) >= 0)
                {
                    offered.Add((w.Command, w.Label));
                }
            }
        }

        Offered = offered.ConvertAll(o => o.Command).ToArray();
        var lines = new System.Collections.Generic.List<string>
        {
            $"Where: {s.Where}.",
            s.HandsOn.Length > 0 ? $"Hands-on: {string.Join(", ", s.HandsOn)}." : "",
            s.Covered.Length > 0 ? $"Also gains: {string.Join(", ", s.Covered)}." : "",
            s.Explained.Length > 0 ? $"Explained: {string.Join(", ", s.Explained)}." : "",
            s.TazUO.Length > 0 ? $"TazUO: {string.Join(", ", s.TazUO)}. Guides: <a href=\"{WikiBase}\">tazuo.org</a>" : "",
            s.Tool != null ? $"You will be given: {s.Tool}." : "",
            "(This station's trial is still being built. It counts as done for now.)"
        };
        lines.RemoveAll(string.IsNullOrEmpty);

        var buttons = offered.Count * 24;
        AddPage(0);
        AddBackground(0, 0, 460, 70 + lines.Count * 36 + buttons, 9270);
        AddAlphaRegion(10, 10, 440, 50 + lines.Count * 36 + buttons);
        AddLabel(24, 18, 1154, s.Title);
        for (var i = 0; i < lines.Count; i++)
        {
            AddHtml(24, 44 + i * 36, 412, 34, $"<BASEFONT COLOR=#DDDDDD>{lines[i]}</BASEFONT>", false, false);
        }

        var y = 44 + lines.Count * 36;
        for (var i = 0; i < offered.Count; i++, y += 24)
        {
            AddButton(24, y, 4005, 4007, BtnWindowBase + i);
            AddLabel(60, y + 2, 1154, offered[i].Label);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var i = info.ButtonID - BtnWindowBase;
        if (sender.Mobile is not PlayerMobile pm || i < 0 || i >= Offered.Length)
        {
            return;
        }

        TazUOClientCommand.Send(pm, Offered[i]);
        pm.SendGump(new TutorialStationGump(_station, TazUOClientCommand.CanRunCommands(pm.NetState)));
    }
}

/// <summary>Graduation's evo starter pet pick: dog, cat, fox or bird.</summary>
public class TutorialPetPickGump : Gump
{
    public TutorialPetPickGump() : base(140, 140)
    {
        AddPage(0);
        AddBackground(0, 0, 360, 190, 9270);
        AddAlphaRegion(10, 10, 340, 170);
        AddLabel(24, 18, 1154, "Choose your companion");
        for (var i = 0; i < ClusterFTutorial.StarterPetLines.Length; i++)
        {
            AddButton(24, 50 + i * 30, 4005, 4007, 1 + i);
            AddLabel(60, 52 + i * 30, 999, ClusterFTutorial.StarterPetLines[i]);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var i = info.ButtonID - 1;
        if (sender.Mobile is not PlayerMobile pm || i < 0 || i >= ClusterFTutorial.StarterPetLines.Length)
        {
            return;
        }

        if (ClusterFTutorial.GivePet(pm, ClusterFTutorial.StarterPetLines[i]) == null)
        {
            pm.SendMessage(0x22, "System: No room for a pet right now. Ask the recruiter again when you have a free follower slot.");
            pm.SendGump(new TutorialPetPickGump());
        }
    }
}
