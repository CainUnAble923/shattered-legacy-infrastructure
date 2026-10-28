// cc-P289: server-observed core trials. In-flight evidence resets on disconnect/restart;
// completed stations remain in TutorialRecord. Only one trial and one timer per character.
// cc-P663: the lessons are on the Wakening Isle; "nearby", arrows, effects and the death lesson use the trainee's lesson
// map (TutorialWorld.MapOf). core.farm is Wren's three steps (TutorialFarm), finished by FarmFinished. A reconnect cancels
// the trial (ClusterFTutorial.OnLogin) and starting the lesson again makes its practice item fresh (P605 risk 3).
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Commands;
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
        public int Ticks;
        public Point3D ArrowAt; // cc-P720: where the arrow points now (the bank lesson moves it)
        public string? Now; // cc-P728: the step the trainee is on now, at the top of the lesson window
    }

    // cc-P728 item 6: a finished lesson names the next and points at its signpost; a closed lesson window comes back.
    public const string NextLessonLine = "Next: {0}. Click Next lesson in the lesson window, or follow the arrow to its signpost and double-click it.";
    public const string ReopenLine = "If you closed the lesson window, type [lesson and press Enter to open it again.";
    public const string NoLessonLine = "You have no tutorial lesson running.";

    public static void Initialize() => EnsureHooks();
    public static void EnsureHooks()
    {
        foreach (var station in TutorialStations.Core) { TutorialStations.Handlers[station.Key] = Start; }
        if (_hooked) { return; }
        _hooked = true;
        EventSink.Speech += OnSpeech;
        EventSink.PaperdollRequest += OnPaperdollRequest; // cc-P718 item 4
        CommandSystem.Register("Lesson", AccessLevel.Player, OnLessonCommand); // cc-P728 item 6
    }

    [Usage("Lesson")]
    [Description("Opens your tutorial lesson's window again.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnLessonCommand(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm) { Reopen(pm); }
    }

    /// <summary>cc-P728 item 6: [lesson: the running lesson's window again (with its step now), or the next lesson's.</summary>
    public static bool Reopen(PlayerMobile pm)
    {
        if (!ClusterFTutorial.IsParticipant(pm)) { pm.SendMessage(0x3B2, NoLessonLine); return false; }
        if (Current(pm) is { } trial)
        {
            pm.CloseGump<TutorialTrialGump>();
            pm.SendGump(new TutorialTrialGump(trial.Station, false, trial.Now));
            return true;
        }
        if (ClusterFTutorial.RecordOf(pm) is { } record && ClusterFTutorial.CoreLeft(record).FirstOrDefault() is { } next)
        {
            pm.CloseGump<TutorialTrialGump>();
            pm.SendGump(new TutorialTrialGump(next, false));
            return true;
        }
        ClusterFTutorial.SystemLine(pm, "You finished the main lessons. Double-click Rook on the New Haven plaza to graduate or try an optional lesson.");
        return false;
    }

    /// <summary>
    /// cc-P728 item 6: the step the trainee is on now, said in the system line and over their head, at the top of the lesson
    /// window (sent again, so a closed one comes back), with the arrow on where to go.
    /// </summary>
    public static void NowStep(PlayerMobile pm, string line, Point3D spot, string label)
    {
        ClusterFTutorial.SystemLine(pm, line);
        Overhead(pm, line);
        Point(pm, spot, label);
        if (Current(pm) is { } trial)
        {
            trial.Now = line;
            pm.CloseGump<TutorialTrialGump>();
            pm.SendGump(new TutorialTrialGump(trial.Station, false, line));
        }
    }

    /// <summary>The step-now line is spent once the trainee acts on it (Wren gives the next herself).</summary>
    public static void ClearNow(PlayerMobile pm)
    {
        if (Current(pm) is { } trial) { trial.Now = null; }
    }

    /// <summary>A line over the trainee's own head, for them alone.</summary>
    public static void Overhead(PlayerMobile pm, string line)
    {
        if (pm.NetState is { } ns) { pm.PrivateOverheadMessage(MessageType.Regular, 0x3B2, false, line, ns); }
    }

    /// <summary>
    /// cc-P728 item 6: after a finished lesson, the next one is named in the system line and over the trainee's head, and
    /// the arrow points at its signpost (double-click it to start). Nothing when no core lesson is left: CompleteStation
    /// has sent them to Rook.
    /// </summary>
    public static void PointToNext(PlayerMobile pm)
    {
        if (ClusterFTutorial.RecordOf(pm) is not { } record || ClusterFTutorial.CoreLeft(record).FirstOrDefault() is not { } next) { return; }
        var line = string.Format(NextLessonLine, next.Title);
        ClusterFTutorial.SystemLine(pm, line);
        ClusterFTutorial.SystemLine(pm, ReopenLine);
        Overhead(pm, $"Next: {next.Title}");
        Point(pm, TutorialCorePlacement.SignSpot(next.Key), TutorialCorePlacement.SignName(next.Key));
    }

    // cc-P718 item 4: the trainee opened their own character window (a double-click on themselves, or the client's
    // paperdoll button: both reach Mobile.DisplayPaperdollTo, which raises PaperdollRequest).
    private static void OnPaperdollRequest(Mobile beholder, Mobile beheld)
    {
        if (beholder == beheld && beholder is PlayerMobile { Alive: true } pm && IsDoing(pm, "core.backpack") &&
            Current(pm)!.Progress.Step == 0)
        {
            Observe(pm, TutorialAction.OpenedPaperdoll);
            ClusterFTutorial.SystemLine(pm, "That is your character window. Now double-click the backpack picture in it, then double-click the blue practice bag inside your backpack.");
        }
    }

    public static Trial? Current(PlayerMobile pm) => Active.GetValueOrDefault(pm.Serial);
    public static bool IsDoing(PlayerMobile pm, string key) =>
        ClusterFTutorial.IsParticipant(pm) && Current(pm)?.Station.Key == key;

    // cc-P480: each page says what to find, what it looks like and where (Chase 10-23: "a mark" meant nothing to someone
    // who has never played), in two short sentences where it can.
    public static string Hint(string key) => key switch
    {
        "core.movement" => "Hold the right mouse button toward the glowing blue floor tile in the road, up from the beach, to walk onto it, then walk onto the gold tile. Point farther from your character while holding the button to run.",
        "core.backpack" => "Follow the arrow to the camp beside the road. First open your character window: double-click your own character (two quick left clicks on yourself), or click the paperdoll button. Then double-click the backpack picture in that window, and inside your backpack double-click the blue practice bag.",
        "core.paperdoll" => "Follow the arrow to the table by the clothes rack: a chest appears there for you, with your blue cloak of the Wakening Isle inside. Double-click the chest to open it. To move a thing, click it, hold the left mouse button down, move the mouse, and let go: that is drag and drop. Drag the cloak onto your character's picture in the character window to wear it, or into your backpack and double-click it there. If you already wear a cloak, drag it into your backpack first. The cloak is yours to keep.",
        "core.chat" => $"Follow the arrow to the signpost. Press Enter, type '{Phrase}', then press Enter again to speak. The Journal records what you say.",
        "core.farm" => "Follow the arrow east along the road to the farm gate and double-click Wren the Farmhand, who wears a straw hat. Choose your pet, hold the pitchfork she gives you and switch to war mode (press Tab), teach your pet its commands, clear the pests in the cabbage rows, then heal Pip with a bandage.",
        "core.death" => "Follow the arrow into the training yard to the pale gray rat, double-click it, and click Ready. After you die, double-click Sera beside it and click Resurrect to return to life.",
        "core.banking" => "Follow the arrow through the practice bank's door in the trades square to Merrin the banker and say 'bank' to open your bank box. Drag the blue practice token, a small blue pile of coins in your backpack, into that box.",
        "core.league" => "Follow the arrow to the League lodge and double-click the door on its north side to enter. Double-click Elara Voss to finish this lesson; you can read about the League without joining.",
        "core.loop" => "Follow the arrow up the knoll to the standing stones and double-click the lesson's signpost there. Watch the vision; it lasts about a minute, holds you still, and will not change your character.",
        "core.travel" => "Follow the arrow to the end of the dock and walk into the shimmering blue moongate, or double-click it. It takes you, and your pet, to Rook on the New Haven plaza.",
        _ => "Double-click Tamsin on the Wakening Isle's beach to choose this lesson."
    };

    /// <summary>A lesson's practice item, made fresh (bright and named for what it is: cc-P480).</summary>
    public static Item? MakeSupply(string key) => key switch
    {
        "core.backpack" => new TutorialPracticeBag { Name = "blue practice bag", Hue = TutorialStyle.SupplyHue, LootType = LootType.Blessed },
        "core.banking" => new TutorialBankToken(),
        _ => null
    };

    /// <summary>
    /// cc-P480: gives the lesson's practice item again when it went missing (deleted, dropped, traded): not on the player,
    /// not in the bank. True when it gave one. A full backpack keeps the trial and says so.
    /// </summary>
    public static bool Resupply(PlayerMobile pm, Trial trial)
    {
        var supply = trial.Supply;
        if (supply == null || !supply.Deleted && (supply.RootParent == pm || supply.IsChildOf(pm.FindBankNoCreate())))
        {
            return false;
        }

        supply.Delete();
        trial.Supply = MakeSupply(trial.Station.Key);
        if (trial.Supply != null && !pm.AddToBackpack(trial.Supply))
        {
            ClusterFTutorial.SystemLine(pm, "Make room in your backpack. Click Hint and arrow to get another practice item.");
            return false;
        }

        ClusterFTutorial.SystemLine(pm, "A replacement practice item is in your backpack.");
        return true;
    }

    public static void Start(PlayerMobile pm, TutorialStation station)
    {
        if (!ClusterFTutorial.IsParticipant(pm) || !station.IsCore) { return; }
        if (!pm.Alive)
        {
            ClusterFTutorial.SystemLine(pm, "Follow the arrow to Sera the healer and double-click Sera. Click Resurrect to return to life before starting a lesson.");
            Point(pm, TutorialCorePlacement.HealerSpot, "Sera the healer");
            return;
        }
        var record = ClusterFTutorial.RecordOf(pm)!;
        var next = ClusterFTutorial.CoreLeft(record).FirstOrDefault();
        if (!record.StationsDone.Contains(station.Key) && next?.Key != station.Key)
        {
            ClusterFTutorial.SystemLine(pm, $"Finish {next?.Title} before starting this lesson.");
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
        trial.Supply = MakeSupply(station.Key);
        if (trial.Supply != null && !pm.AddToBackpack(trial.Supply))
        {
            Cancel(pm);
            ClusterFTutorial.SystemLine(pm, "Make room in your backpack, then choose this lesson again in Tamsin's window.");
            return;
        }
        if (station.Key == "core.paperdoll") { TutorialCloakChest.EnsureFor(pm); } // cc-P728: a chest appears with the cloak
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), () => Tick(trial), out trial.Timer);
        Show(pm, trial);
        // cc-P663: farm steps finished before the farm was a core station (or before this start) count at once.
        if (station.Key == "core.farm" && TutorialFarm.StepOf(pm) == 4) { Observe(pm, TutorialAction.FarmDone); }
    }

    private static void Show(PlayerMobile pm, Trial trial)
    {
        Resupply(pm, trial);
        if (trial.Station.Key == "core.paperdoll") { TutorialCloakChest.EnsureFor(pm); } // cc-P718: a lost cloak comes back (cc-P728: in its chest)
        ClusterFTutorial.SystemLine(pm, Hint(trial.Station.Key));
        Point(pm, Destination(trial), ArrowLabel(trial));
        trial.ArrowAt = Destination(trial);
        pm.CloseGump<TutorialTrialGump>();
        pm.SendGump(new TutorialTrialGump(trial.Station, false, trial.Now));
    }

    // cc-P720 item 4: what the arrow is pointing at, named.
    private static string ArrowLabel(Trial t) => t.Station.Key == "core.banking"
        ? TutorialCorePlacement.InBank(t.Player.Location) ? "Merrin the banker" : "the practice bank's door"
        : t.Station.Title;

    private static Point3D Destination(Trial t) => t.Station.Key switch
    {
        "core.movement" => t.Progress.Step == 0 ? TutorialCorePlacement.FirstMark : TutorialCorePlacement.SecondMark,
        "core.death" => t.Progress.Step == 0 ? TutorialCorePlacement.DeathSpot : TutorialCorePlacement.HealerSpot,
        // cc-P720 item 4: the bank's door until the trainee is inside, then Merrin
        "core.banking" when !TutorialCorePlacement.InBank(t.Player.Location) => TutorialWorld.Ground(TutorialWorld.MapOf(t.Player) ?? TutorialWorld.Isle, TutorialWorld.BankDoor),
        _ => TutorialCorePlacement.Spot(t.Station.Key)
    };

    /// <summary>An arrow to a spot on <paramref name="map"/> (the lesson map unless given), shown while the trainee is on it.</summary>
    public static void Point(PlayerMobile pm, Point3D spot, string label, Map? map = null)
    {
        map ??= TutorialWorld.MapOf(pm);
        pm.QuestArrow?.Stop();
        if (pm.Map == map) { pm.QuestArrow = new GuildDirectionArrow(pm, pm, spot, map, label); }
    }

    /// <summary>Near a spot on the trainee's lesson map (or the given map).</summary>
    public static bool Nearby(PlayerMobile pm, Point3D spot, int range = 3, Map? map = null) =>
        pm.Map == (map ?? TutorialWorld.MapOf(pm)) && pm.InRange(spot, range) && Math.Abs(pm.Z - spot.Z) <= 16;

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
        PointToNext(pm); // cc-P728 item 6
        return true;
    }

    public static void Cancel(PlayerMobile pm)
    {
        if (!Active.Remove(pm.Serial, out var trial)) { return; }
        trial.Timer.Cancel();
        TutorialVisionScene.End(pm, false); // cc-P665: a cancelled lesson ends its vision (back at the stones, unfrozen)
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
        trial.Ticks++;
        switch (trial.Station.Key)
        {
            case "core.movement" when pm.Alive && Nearby(pm, Destination(trial), 0):
                var first = trial.Progress.Step == 0;
                Observe(pm, first ? TutorialAction.FirstMark : TutorialAction.SecondMark);
                if (first) { ClusterFTutorial.SystemLine(pm, "Now walk onto the glowing gold floor tile."); Point(pm, Destination(trial), "the glowing gold floor tile"); }
                break;
            case "core.movement" when trial.Ticks % 8 == 1:
                // cc-P480: the tile to walk onto sparkles every two seconds.
                Effects.SendLocationEffect(Destination(trial), TutorialWorld.MapOf(pm), 0x376A, 16, 10,
                    trial.Progress.Step == 0 ? TutorialStyle.BlueTileHue : TutorialStyle.GoldTileHue, 0);
                break;
            case "core.paperdoll" when pm.Alive && pm.FindItemOnLayer(Layer.Cloak) is WakeningIsleCloak:
                Observe(pm, TutorialAction.Equipped);
                break;
            case "core.banking" when pm.Alive && trial.Supply?.IsChildOf(pm.FindBankNoCreate()) == true:
                Observe(pm, TutorialAction.Deposited);
                break;
            case "core.banking" when Destination(trial) != trial.ArrowAt:
                // cc-P720 item 4: through the door, the arrow turns to Merrin (and back to the door on leaving)
                Point(pm, Destination(trial), ArrowLabel(trial));
                trial.ArrowAt = Destination(trial);
                break;
            case "core.death" when pm.Alive && trial.Progress.Step == 2 && Nearby(pm, TutorialCorePlacement.HealerSpot, 4):
                Observe(pm, TutorialAction.Resurrected);
                break;
            // cc-P665: the scene (TutorialVisionScene) runs the vision and reports its end; a started vision with no scene
            // playing (it ended early) can be started again from the stone.
            case "core.loop" when trial.Progress.Step == 1 && !TutorialVisionScene.IsPlaying(pm):
                trial.Progress.Reset();
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
        // Return this lesson's ghost to the yard (Young's death teleport may have moved it), without changing Young.
        pm.MoveToWorld(TutorialCorePlacement.GhostSpot, TutorialWorld.MapOf(pm));
        ClusterFTutorial.SystemLine(pm, "You are a ghost. Double-click Sera beside you and click Resurrect to return to life.");
        Point(pm, TutorialCorePlacement.HealerSpot, "Sera the healer");
    }

    /// <summary>cc-P663: Wren's farm steps are all done (TutorialFarm.StepOf is 4): core.farm is finished.</summary>
    public static void FarmFinished(PlayerMobile pm)
    {
        if (IsDoing(pm, "core.farm")) { Observe(pm, TutorialAction.FarmDone); }
        else { ClusterFTutorial.CompleteStation(pm, "core.farm"); }
    }

    public static void WatchVision(PlayerMobile pm)
    {
        if (!pm.Alive || !IsDoing(pm, "core.loop") || !Nearby(pm, TutorialCorePlacement.Spot("core.loop"))) { return; }
        var trial = Current(pm)!;
        if (trial.Progress.Step != 0) { return; }
        if (TutorialVisionScene.IsPlaying(pm)) { return; }
        Observe(pm, TutorialAction.VisionStarted);
        // cc-P665: the scene (61 s since cc-P721); its last beat finishes the lesson, an early end lets the stone start it again.
        if (!TutorialVisionScene.Play(pm, p => Observe(p, TutorialAction.VisionFinished), VisionEnded)) { trial.Progress.Reset(); }
    }

    private static void VisionEnded(PlayerMobile pm)
    {
        if (IsDoing(pm, "core.loop")) { Current(pm)!.Progress.Reset(); }
    }
}

public sealed class TutorialTrialGump : Gump
{
    public const string FinishedText = "Lesson finished. Click Next lesson / graduate to continue. If you close this window, type [lesson and press Enter to open it again.";
    private readonly TutorialStation _station;
    private readonly bool _done;
    /// <summary>cc-P728 item 6: the step the trainee is on now, shown above the hint; null for none.</summary>
    public string? Now { get; }
    public TutorialTrialGump(TutorialStation station, bool done, string? now = null) : base(80, 80)
    {
        _station = station; _done = done; Now = now;
        var extra = now != null ? 56 : 0;
        TutorialStyle.Panel(this, 480, 240 + extra);
        AddLabel(24, 18, TutorialStyle.HeadingHue, station.Title);
        if (now != null) { AddHtml(24, 46, 432, 50, TutorialStyle.HeadingHtml("Now: " + now), false, false); }
        AddHtml(24, 50 + extra, 432, 100, TutorialStyle.Html(done ? FinishedText : TutorialCoreTrials.Hint(station.Key)), false, false);
        AddButton(24, 166 + extra, 4005, 4007, 1); AddLabel(60, 168 + extra, done ? TutorialStyle.DoneHue : TutorialStyle.ActionHue, done ? "Next lesson / graduate" : "Hint and arrow");
        AddButton(24, 204 + extra, 4017, 4019, 2); AddLabel(60, 206 + extra, TutorialStyle.TextHue, "Restart this lesson");
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
                    ClusterFTutorial.SystemLine(pm, "You finished the main lessons. Follow the arrow to Rook on the New Haven plaza and choose Graduate, or try an optional lesson there.");
                    TutorialRecruiter.PointTo(pm);
                }
            }
            else { ClusterFTutorial.RunStation(pm, _station); }
        }
    }
}
