// cc-P726 item 6 (Chase 10-28 12:25): the isle's lessons in the quest log. The paperdoll's Quests button (encoded packet
// 0x32) opens pinned's QuestLogGump (MLQuestSystem.QuestGumpRequest), which lists only MLQuest instances and has no hook
// for anything else (QuestLogGump has a private constructor and reads MLQuestContext.QuestInstances alone). So the button
// is answered here: a trainee in the tutorial gets the quest log's own frame (BaseMLQuestGump, the "Quest Log" header and
// buttons) listing the core lessons, each done, now, next or later, with a page per lesson that ticks its steps from the
// same progress the lesson window reads (TutorialRecord.StationsDone, the running trial's evidence, Wren's farm keys).
// "Other quests" opens pinned's log as before; everyone not in the tutorial gets pinned's log straight away. Nothing is
// tracked twice: the log only reads. Re-registered in Initialize, after pinned's IncomingPlayerPackets.Configure.

#nullable enable

using System.Linq;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Gumps;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialQuestLog
{
    public const int QuestButtonPacket = 0x32;

    public static unsafe void Initialize() => IncomingPackets.RegisterEncoded(QuestButtonPacket, true, &OnQuestButton);

    public static void OnQuestButton(NetState state, IEntity e, EncodedReader reader) => Open(state.Mobile);

    /// <summary>The Quests button: the isle's lessons for a trainee, pinned's quest log for anyone else.</summary>
    public static bool Open(Mobile? m)
    {
        if (m is PlayerMobile pm && ClusterFTutorial.IsParticipant(pm))
        {
            pm.CloseGump<TutorialQuestLogGump>();
            pm.CloseGump<TutorialQuestStepsGump>();
            pm.SendGump(new TutorialQuestLogGump(pm));
            return true;
        }

        MLQuestSystem.QuestGumpRequest(m);
        return false;
    }

    public enum LessonState { Done, Now, Next, Later }

    public static LessonState StateOf(PlayerMobile pm, TutorialStation station)
    {
        var record = ClusterFTutorial.RecordOf(pm);
        if (record == null)
        {
            return LessonState.Later;
        }

        if (ClusterFTutorial.IsDone(record, station.Key))
        {
            return LessonState.Done;
        }

        if (TutorialCoreTrials.Current(pm)?.Station.Key == station.Key)
        {
            return LessonState.Now;
        }

        return ClusterFTutorial.CoreLeft(record).FirstOrDefault()?.Key == station.Key ? LessonState.Next : LessonState.Later;
    }

    /// <summary>A lesson's steps in plain words, each ticked when done.</summary>
    public static (string Text, bool Done)[] Steps(PlayerMobile pm, TutorialStation station)
    {
        var record = ClusterFTutorial.RecordOf(pm);
        var all = record != null && ClusterFTutorial.IsDone(record, station.Key);
        if (station.Key == TutorialFarm.CoreStation)
        {
            return
            [
                ("Hold the pitchfork Wren gives you", all || TutorialFarm.Has(pm, TutorialFarm.WeaponKey)),
                ("Switch to war mode (press Tab)", all || TutorialFarm.Has(pm, TutorialFarm.WarKey)),
                ("Clear the pests in the cabbage rows", all || TutorialFarm.Has(pm, TutorialFarm.PestsKey)),
                ("Take your pet from Wren", all || record?.PetClaimed == true),
                ("Say 'all follow me'", all || TutorialFarm.Has(pm, TutorialFarm.FollowKey)),
                ("Say 'all guard me'", all || TutorialFarm.Has(pm, TutorialFarm.GuardKey))
            ];
        }

        var expected = TutorialTrialProgress.Expected(station.Key);
        var trial = TutorialCoreTrials.Current(pm);
        var done = all ? expected.Length : trial?.Station.Key == station.Key ? trial.Progress.Step : 0;
        var steps = new (string, bool)[expected.Length];
        for (var i = 0; i < expected.Length; i++)
        {
            steps[i] = (StepText(expected[i]), i < done);
        }

        return steps;
    }

    public static string StepText(TutorialAction a) => a switch
    {
        TutorialAction.FirstMark => "Walk onto the glowing blue tile",
        TutorialAction.SecondMark => "Walk onto the glowing gold tile",
        TutorialAction.OpenedPaperdoll => "Open your character window",
        TutorialAction.OpenedBag => "Open the blue practice bag in your backpack",
        TutorialAction.Equipped => "Wear your cloak of the Wakening Isle",
        TutorialAction.SaidPhrase => "Say 'I am ready to learn' at the signpost",
        TutorialAction.EchoDeath => "Face the pale rat in the training yard",
        TutorialAction.HealerOffer => "As a ghost, double-click Sera",
        TutorialAction.Resurrected => "Come back to life",
        TutorialAction.Deposited => "Put the practice token in your bank box",
        TutorialAction.TalkedToLeague => "Double-click Elara Voss in the lodge",
        TutorialAction.VisionStarted => "Start the vision at the standing stones",
        TutorialAction.VisionFinished => "Watch the vision to its end",
        TutorialAction.UsedGate => "Walk through the moongate on the dock",
        TutorialAction.Healed => "Bandage Pip until a bandage heals him",
        TutorialAction.FarmDone => "Finish Wren's farm lesson",
        _ => a.ToString()
    };

    public static string Mark(LessonState s) => s switch
    {
        LessonState.Done => "(done)",
        LessonState.Now => "(now)",
        LessonState.Next => "(next)",
        _ => ""
    };
}

/// <summary>cc-P726: the quest log's list page for a trainee: every core lesson, done, now, next or later.</summary>
public sealed class TutorialQuestLogGump : BaseMLQuestGump
{
    public const int BtnOtherQuests = 2;
    public const int BtnOkay = 3;
    public const int BtnLessonBase = 10;

    private readonly PlayerMobile _owner;

    public override bool Singleton => true;

    public TutorialQuestLogGump(PlayerMobile pm) : base(1046026) // Quest Log
    {
        _owner = pm;
        RegisterButton(ButtonPosition.Left, ButtonGraphic.Continue, BtnOtherQuests);
        RegisterButton(ButtonPosition.Right, ButtonGraphic.Okay, BtnOkay);
        SetPageCount(1);
    }

    protected override void BuildContent(ref DynamicGumpBuilder builder)
    {
        BuildPage(ref builder);
        var heading = "The Wakening Isle: your lessons";
        builder.AddHtml(98, 112, 312, 20, heading, "#FFE14A");
        var core = TutorialStations.Core.ToArray();
        for (var i = 0; i < core.Length; i++)
        {
            var state = TutorialQuestLog.StateOf(_owner, core[i]);
            var color = state switch
            {
                TutorialQuestLog.LessonState.Done => "#7FD07F",
                TutorialQuestLog.LessonState.Now or TutorialQuestLog.LessonState.Next => "#FFFFFF",
                _ => "#A0A0A0"
            };
            var row = $"{i + 1}. {core[i].Title} {TutorialQuestLog.Mark(state)}";
            builder.AddHtml(98, 140 + 21 * i, 270, 21, row, color);
            builder.AddButton(368, 140 + 21 * i, 0x26B0, 0x26B1, BtnLessonBase + i);
        }

        var other = "Other quests: the button below";
        builder.AddHtml(98, 140 + 21 * core.Length + 4, 312, 20, other, "#A0A0A0");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnOtherQuests)
        {
            MLQuestSystem.QuestGumpRequest(sender.Mobile);
            return;
        }

        var core = TutorialStations.Core.ToArray();
        var index = info.ButtonID - BtnLessonBase;
        if (index >= 0 && index < core.Length)
        {
            _owner.SendGump(new TutorialQuestStepsGump(_owner, core[index]));
        }
    }
}

/// <summary>cc-P726: one lesson's page in the quest log: its steps, ticked as they are done, and its hint.</summary>
public sealed class TutorialQuestStepsGump : BaseMLQuestGump
{
    public const int BtnBack = 3;

    private readonly PlayerMobile _owner;

    public TutorialStation Station { get; }

    public TutorialQuestStepsGump(PlayerMobile pm, TutorialStation station) : base(1046026) // Quest Log
    {
        _owner = pm;
        Station = station;
        RegisterButton(ButtonPosition.Right, ButtonGraphic.Okay, BtnBack);
        SetPageCount(1);
    }

    protected override void BuildContent(ref DynamicGumpBuilder builder)
    {
        BuildPage(ref builder);
        var title = $"{Station.Title} {TutorialQuestLog.Mark(TutorialQuestLog.StateOf(_owner, Station))}";
        builder.AddHtml(98, 112, 312, 20, title, "#FFE14A");
        var steps = TutorialQuestLog.Steps(_owner, Station);
        var y = 140;
        foreach (var (text, done) in steps)
        {
            var line = done ? $"[x] {text}" : $"[ ] {text}";
            builder.AddHtml(98, y, 312, 20, line, done ? "#7FD07F" : "#FFFFFF");
            y += 21;
        }

        var hint = TutorialCoreTrials.Hint(Station.Key);
        builder.AddHtml(98, y + 8, 312, 420 - y - 8, hint, "#D0D0D0", scrollbar: true);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnBack)
        {
            TutorialQuestLog.Open(_owner);
        }
    }
}
