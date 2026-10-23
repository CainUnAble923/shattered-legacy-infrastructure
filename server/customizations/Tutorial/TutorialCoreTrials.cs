// cc-P289: server-observed core trials. In-flight evidence resets on disconnect/restart;
// completed stations remain in TutorialRecord. Only one trial and one timer per character.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialCoreTrials
{
    public const string Phrase = "I am ready to learn";
    private static readonly Dictionary<Serial, Trial> Active = new();
    private static bool _hooked;

    public sealed class Trial(PlayerMobile player, TutorialStation station)
    {
        public PlayerMobile Player { get; } = player;
        public TutorialStation Station { get; } = station;
        public TutorialTrialProgress Progress { get; } = new(station.Key);
        public Item? Supply;
        public TimerExecutionToken Timer;
        public DateTime VisionAt;
        public int VisionLine;
    }

    public static void Initialize() => EnsureHooks();
    public static void EnsureHooks()
    {
        foreach (var station in TutorialStations.Core) { TutorialStations.Handlers[station.Key] = Start; }
        if (_hooked) { return; }
        _hooked = true;
        EventSink.Speech += OnSpeech;
    }

    public static Trial? Current(PlayerMobile pm) => Active.GetValueOrDefault(pm.Serial);
    public static bool IsDoing(PlayerMobile pm, string key) =>
        ClusterFTutorial.IsParticipant(pm) && Current(pm)?.Station.Key == key;

    public static string Hint(string key) => key switch
    {
        "core.movement" => "Walk onto the blue mark, then the gold mark. Hold the right mouse button to walk; move the pointer farther to run.",
        "core.paperdoll" => "Open your paperdoll by double-clicking yourself. Drag the practice cloak from your backpack onto it. Remove your old cloak first if needed.",
        "core.backpack" => "Open your backpack from your paperdoll. Double-click the blue practice bag inside it.",
        "core.chat" => $"Press Enter, say '{Phrase}', then Enter again. Your Journal keeps the words.",
        "core.death" => "At the farm gate, double-click the pale echo and accept the death lesson. As a ghost, double-click the tutorial healer and accept resurrection. While you are Young, your things come back to your backpack.",
        "core.banking" => "Follow the arrow to the banker. Say 'bank', then drag the practice token from your backpack into your bank. The practice token disappears when done; real valuables stay safe there.",
        "core.travel" => "At the farm gate, double-click the blue tutorial moongate. It brings you back to the New Haven plaza.",
        "core.league" => "Follow the arrow to Elara Voss. Double-click her to read about the League. Joining is your choice.",
        "core.loop" => "Follow the arrow to the vision marker at the farm gate. Double-click it and stay nearby until the vision ends. It changes nothing about you.",
        _ => "Coming soon. This wing is optional."
    };

    public static void Start(PlayerMobile pm, TutorialStation station)
    {
        if (!ClusterFTutorial.IsParticipant(pm) || !station.IsCore) { return; }
        if (!pm.Alive)
        {
            ClusterFTutorial.SystemLine(pm, "Visit the tutorial healer first. A ghost can still double-click the healer.");
            Point(pm, TutorialCorePlacement.HealerSpot, "the tutorial healer");
            return;
        }
        var record = ClusterFTutorial.RecordOf(pm)!;
        var next = ClusterFTutorial.CoreLeft(record).FirstOrDefault();
        if (!record.StationsDone.Contains(station.Key) && next?.Key != station.Key)
        {
            ClusterFTutorial.SystemLine(pm, $"First finish: {next?.Title}.");
            return;
        }
        if (Current(pm) is { } current && current.Station.Key == station.Key)
        {
            Show(pm, current);
            return;
        }
        Cancel(pm);
        if (record.StationsDone.Contains(station.Key))
        {
            pm.SendGump(new TutorialTrialGump(station, true));
            return;
        }
        var trial = new Trial(pm, station);
        Active[pm.Serial] = trial;
        switch (station.Key)
        {
            case "core.paperdoll": trial.Supply = new TutorialPracticeCloak(); break;
            case "core.backpack": trial.Supply = new TutorialPracticeBag { Name = "blue practice bag", Hue = 0x482, LootType = LootType.Blessed }; break;
            case "core.banking": trial.Supply = new TutorialBankToken(); break;
        }
        if (trial.Supply != null && !pm.AddToBackpack(trial.Supply))
        {
            Cancel(pm);
            ClusterFTutorial.SystemLine(pm, "Make room in your backpack, then try this lesson again.");
            return;
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), () => Tick(trial), out trial.Timer);
        Show(pm, trial);
    }

    private static void Show(PlayerMobile pm, Trial trial)
    {
        ClusterFTutorial.SystemLine(pm, Hint(trial.Station.Key));
        Point(pm, Destination(trial), trial.Station.Title);
        pm.CloseGump<TutorialTrialGump>();
        pm.SendGump(new TutorialTrialGump(trial.Station, false));
    }

    private static Point3D Destination(Trial t) => t.Station.Key switch
    {
        "core.movement" => t.Progress.Step == 0 ? TutorialCorePlacement.FirstMark : TutorialCorePlacement.SecondMark,
        "core.death" => t.Progress.Step == 0 ? TutorialCorePlacement.DeathSpot : TutorialCorePlacement.HealerSpot,
        _ => TutorialCorePlacement.Spot(t.Station.Key)
    };

    public static void Point(PlayerMobile pm, Point3D spot, string label)
    {
        pm.QuestArrow?.Stop();
        if (pm.Map == Map.Trammel) { pm.QuestArrow = new GuildDirectionArrow(pm, pm, spot, Map.Trammel, label); }
    }

    public static bool Nearby(PlayerMobile pm, Point3D spot, int range = 3) =>
        pm.Map == Map.Trammel && pm.InRange(spot, range) && Math.Abs(pm.Z - spot.Z) <= 16;

    // Shared by real hooks and fakes. Callers supply evidence, never a generic "done" button.
    public static bool Observe(PlayerMobile pm, TutorialAction action)
    {
        var trial = Current(pm);
        if (trial == null || !ClusterFTutorial.IsParticipant(pm)) { return false; }
        if (!trial.Progress.Observe(action)) { return false; }
        Cancel(pm);
        ClusterFTutorial.CompleteStation(pm, trial.Station.Key);
        pm.CloseGump<TutorialTrialGump>();
        pm.SendGump(new TutorialTrialGump(trial.Station, true));
        return true;
    }

    public static void Cancel(PlayerMobile pm)
    {
        if (!Active.Remove(pm.Serial, out var trial)) { return; }
        trial.Timer.Cancel();
        trial.Supply?.Delete(); // exact supplied instance only, even if moved to the bank
        pm.QuestArrow?.Stop();
    }

    public static void Reset(PlayerMobile pm, TutorialStation station)
    {
        if (!ClusterFTutorial.IsParticipant(pm) || !pm.Alive) { return; }
        Cancel(pm);
        var record = ClusterFTutorial.RecordOf(pm)!;
        record.StationsDone.Remove(station.Key);
        if (record.State == TutorialState.InWings) { record.State = TutorialState.InCore; }
        Start(pm, station);
    }

    private static void Tick(Trial trial)
    {
        var pm = trial.Player;
        if (Current(pm) != trial) { trial.Timer.Cancel(); return; }
        if (pm.Deleted || pm.NetState == null || !ClusterFTutorial.IsParticipant(pm)) { Cancel(pm); return; }
        switch (trial.Station.Key)
        {
            case "core.movement" when pm.Alive && Nearby(pm, Destination(trial), 0):
                var first = trial.Progress.Step == 0;
                Observe(pm, first ? TutorialAction.FirstMark : TutorialAction.SecondMark);
                if (first) { ClusterFTutorial.SystemLine(pm, "Now the gold mark."); Point(pm, Destination(trial), "the gold mark"); }
                break;
            case "core.paperdoll" when pm.Alive && trial.Supply?.Parent == pm:
                Observe(pm, TutorialAction.Equipped);
                break;
            case "core.banking" when pm.Alive && trial.Supply?.IsChildOf(pm.FindBankNoCreate()) == true:
                Observe(pm, TutorialAction.Deposited);
                break;
            case "core.death" when pm.Alive && trial.Progress.Step == 2 && Nearby(pm, TutorialCorePlacement.HealerSpot, 4):
                Observe(pm, TutorialAction.Resurrected);
                break;
            case "core.loop" when trial.Progress.Step == 1:
                if (!pm.Alive || !Nearby(pm, TutorialCorePlacement.Spot("core.loop"), 4))
                {
                    trial.Progress.Reset(); trial.VisionLine = 0;
                    ClusterFTutorial.SystemLine(pm, "Stay beside the vision marker. Double-click it to begin again.");
                    break;
                }
                if (Core.Now < trial.VisionAt) { break; }
                if (trial.VisionLine < TutorialVision.Lines.Length)
                {
                    pm.SendMessage(0x3B2, TutorialVision.Lines[trial.VisionLine++]);
                    trial.VisionAt = Core.Now + TimeSpan.FromSeconds(2);
                }
                else { Observe(pm, TutorialAction.VisionFinished); }
                break;
        }
    }

    private static void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Blocked && e.Type == MessageType.Regular && e.Mobile is PlayerMobile { Alive: true } pm &&
            IsDoing(pm, "core.chat") && string.Equals(e.Speech.Trim(), Phrase, StringComparison.OrdinalIgnoreCase))
        { Observe(pm, TutorialAction.SaidPhrase); }
    }

    public static void OpenBag(PlayerMobile pm, Item bag)
    {
        if (pm.Alive && IsDoing(pm, "core.backpack") && Current(pm)?.Supply == bag && bag.IsChildOf(pm.Backpack))
        { Observe(pm, TutorialAction.OpenedBag); }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnDeath(PlayerMobile pm)
    {
        if (!IsDoing(pm, "core.death") || pm.FindMostRecentDamager(true) is not TutorialDeathEcho) { return; }
        Observe(pm, TutorialAction.EchoDeath);
        // PlayerDeathEvent is after YoungDeathTeleport (pinned PlayerMobile.OnDeath).
        // Return this lesson's ghost from Old Haven to the farm, without changing Young.
        pm.MoveToWorld(TutorialCorePlacement.GhostSpot, Map.Trammel);
        ClusterFTutorial.SystemLine(pm, "You are a ghost. Double-click the tutorial healer and accept resurrection.");
        Point(pm, TutorialCorePlacement.HealerSpot, "the tutorial healer");
    }

    public static void WatchVision(PlayerMobile pm)
    {
        if (!pm.Alive || !IsDoing(pm, "core.loop") || !Nearby(pm, TutorialCorePlacement.Spot("core.loop"))) { return; }
        var trial = Current(pm)!;
        if (trial.Progress.Step != 0) { return; }
        Observe(pm, TutorialAction.VisionStarted);
        trial.VisionAt = Core.Now;
        pm.FixedParticles(0x376A, 9, 32, 5030, 0x8AF, 0, EffectLayer.Waist);
        pm.PlaySound(0x1F7);
    }
}

public sealed class TutorialTrialGump : Gump
{
    private readonly TutorialStation _station;
    private readonly bool _done;
    public TutorialTrialGump(TutorialStation station, bool done) : base(80, 80)
    {
        _station = station; _done = done;
        AddBackground(0, 0, 480, 240, 9270);
        AddLabel(24, 18, 1154, station.Title);
        AddHtml(24, 50, 432, 100, $"<BASEFONT COLOR=#DDDDDD>System: {(done ? "Done. Your next lesson is ready." : TutorialCoreTrials.Hint(station.Key))}</BASEFONT>", false, false);
        AddButton(24, 166, 4005, 4007, 1); AddLabel(60, 168, 68, done ? "Next lesson / graduate" : "Hint and arrow");
        AddButton(24, 204, 4017, 4019, 2); AddLabel(60, 206, 999, "Reset this lesson");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || !ClusterFTutorial.IsParticipant(pm)) { return; }
        if (info.ButtonID == 2) { TutorialCoreTrials.Reset(pm, _station); }
        else if (info.ButtonID == 1)
        {
            if (_done && ClusterFTutorial.RecordOf(pm) is { } record)
            {
                if (ClusterFTutorial.CoreLeft(record).FirstOrDefault() is { } next) { ClusterFTutorial.RunStation(pm, next); }
                else
                {
                    ClusterFTutorial.SystemLine(pm, "Core finished. Return to Rook on the New Haven plaza to graduate. The wings are optional.");
                    TutorialRecruiter.PointTo(pm);
                }
            }
            else { ClusterFTutorial.RunStation(pm, _station); }
        }
    }
}
