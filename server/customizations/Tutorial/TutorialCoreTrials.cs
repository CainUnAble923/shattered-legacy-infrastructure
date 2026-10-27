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
        public int Ticks;
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

    // cc-P480: each page says what to find, what it looks like and where (Chase 10-23: "a mark" meant nothing to someone
    // who has never played), in two short sentences where it can.
    public static string Hint(string key) => key switch
    {
        "core.movement" => "Hold the right mouse button toward the glowing blue floor tile in the road, up from the beach, to walk onto it, then walk onto the gold tile. Point farther from your character while holding the button to run.",
        "core.backpack" => "Follow the arrow to the camp beside the road. Double-click your character to open the character window, then double-click its backpack picture. Inside your backpack, double-click the blue practice bag.",
        "core.paperdoll" => "Follow the arrow to the clothes rack. Double-click your character to open the character window, then drag the blue practice cloak from your backpack onto your character's picture. If you already wear a cloak, drag it into your backpack first.",
        "core.chat" => $"Follow the arrow to the signpost. Press Enter, type '{Phrase}', then press Enter again to speak. The Journal records what you say.",
        "core.farm" => "Follow the arrow east along the road to the farm gate and double-click Wren the Farmhand, who wears a straw hat. Choose your pet, clear the pests in the cabbage rows, then heal with a bandage.",
        "core.death" => "Follow the arrow into the training yard to the pale gray rat, double-click it, and click Ready. After you die, double-click Sera beside it and click Resurrect to return to life.",
        "core.banking" => "Follow the arrow through the practice bank's door in the trades square to Merrin the banker and say 'bank' to open your bank box. Drag the blue practice token, a small blue pile of coins in your backpack, into that box.",
        "core.league" => "Follow the arrow to the League lodge and double-click the door on its north side to enter. Double-click Elara Voss to finish this lesson; you can read about the League without joining.",
        "core.loop" => "Follow the arrow up the knoll to the standing stones and double-click the bright blue rune stone. Stay nearby until the vision ends; it will not change your character.",
        "core.travel" => "Follow the arrow to the end of the dock and walk into the shimmering blue moongate, or double-click it. It takes you, and your pet, to Rook on the New Haven plaza.",
        _ => "Double-click Tamsin on the Wakening Isle's beach to choose this lesson."
    };

    /// <summary>A lesson's practice item, made fresh (bright and named for what it is: cc-P480).</summary>
    public static Item? MakeSupply(string key) => key switch
    {
        "core.paperdoll" => new TutorialPracticeCloak(),
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
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), () => Tick(trial), out trial.Timer);
        Show(pm, trial);
        // cc-P663: farm steps finished before the farm was a core station (or before this start) count at once.
        if (station.Key == "core.farm" && TutorialFarm.StepOf(pm) == 4) { Observe(pm, TutorialAction.FarmDone); }
    }

    private static void Show(PlayerMobile pm, Trial trial)
    {
        Resupply(pm, trial);
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
                    ClusterFTutorial.SystemLine(pm, "Stay beside the bright blue rune stone until the vision ends. Double-click it to restart the vision.");
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
        TutorialStyle.Panel(this, 480, 240);
        AddLabel(24, 18, TutorialStyle.HeadingHue, station.Title);
        AddHtml(24, 50, 432, 100, TutorialStyle.Html(done ? "Lesson finished. Click Next lesson / graduate to continue." : TutorialCoreTrials.Hint(station.Key)), false, false);
        AddButton(24, 166, 4005, 4007, 1); AddLabel(60, 168, done ? TutorialStyle.DoneHue : TutorialStyle.ActionHue, done ? "Next lesson / graduate" : "Hint and arrow");
        AddButton(24, 204, 4017, 4019, 2); AddLabel(60, 206, TutorialStyle.TextHue, "Restart this lesson");
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
