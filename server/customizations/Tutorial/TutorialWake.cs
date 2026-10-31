// cc-P721 (Chase 10-28 09:29): a new character's first arrival on the Wakening Isle is a body washed ashore getting up.
// The character falls into the death pose (the human death animation, played forward), lies still in a dark, quiet
// light while the waves sound and "You wake on cold sand..." shows, the light comes up over a few seconds, and they get up
// (the same animation played backward) before the first lesson's lines and the arrow to Tamsin. Movement is blocked
// only while lying down (Frozen). Once per character: TutorialStart.ConsumeAtLogin calls it for the first entry only, so
// remedial trips, copy resets and [Tutorial reset (which leaves the choice consumed) never play it again.
//
// The client draws no screen fade (TazUO ignores the server's screen-effect packet, cc-P665), so the dark is the
// player's own light level (FoldRegion.AlterLightLevel asks LightOf), as the vision does. A client can hold a pose only
// for the animation's length, so the fallen pose is the death animation's last frames, held by the freeze; if a client
// shows the character standing between the two animations, the dark and the lines still carry the moment. Notes:
// shard-migration notes/cc-P721-isle-bandage-wait.md.
//
// cc-P728 (Chase 10-28 15:22): the trainee stays down until they try to move. Their first move request, a step or a turn
// (PlayerMobile-tutorial-wake-move.patch, OnMoveRequest), is spent on getting up. "Move to get up." comes after
// HintSeconds if they have not moved, the waves keep washing, and after SafetySeconds they get up anyway. Disconnecting
// while down ends the lie at once. The washed-ashore moment comes first: the first login's other windows and welcome
// lines (the patch notes, the first achievement and its fanfare, the welcome lines) wait in Hold until the trainee has
// stood up, and come HeldDelay later. Notes: shard-migration notes/cc-P728-isle-walkthrough-fixes-4.md.
//
// cc-P732 (P731 in the real client): TazUO sends no step or turn while the server marks the player Frozen (its
// PlayerMobile.Walk checks IsParalyzed), so the freeze made "Move to get up." do nothing until the safety. The trainee is
// no longer frozen: the move patch refuses their moves while they lie (the client snaps back) and the first one, a step
// or a turn, gets them up. TazUO also ends a server animation on the standing frame, so the death animation played once
// showed them standing; the lying pose is now the death animation played backward from its last frames with the longest
// frame delay (HoldPose: TazUO draws frame 5, then holds frames 4 and 3, all on the ground, about 20 s each), sent again
// every HoldEverySeconds. The dark stays until they stand (it came up after 4 s, and the first light packet went out
// before the client was in the world), then a short dawn. Notes: shard-migration notes/cc-P732-isle-wake-pose.md.
//
// cc-P740 (Chase 10-30, a new character: "its dark, and you can not tell you are laying down.. more time to see player
// get up"): the dusk is lighter (DarkLight), so the trainee and the sand show from the first frame. "You lie on the wet
// sand..." shows over their head a second in, and "Move to get up." comes sooner (HintSeconds) and again over the head
// every HintEverySeconds. The first move starts the get-up, drawn slowly (GetUpDelay, about 2.5 s); moves are refused
// while it plays (GetUpSeconds; the trainee still counts as lying), then the dawn, and only after the dawn the first
// lesson's lines and, HeldDelay later, the held windows. Notes: shard-migration notes/cc-P740-isle-wake-visible.md.
// In ClassicUO the refused move's reject, which goes out after the get-up, ends the animation at once (the character
// stood up in a blink in the P740 run), so the get-up is sent again GetUpResend later, once the reject is through.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialWake
{
    public const int HintSeconds = 4; // cc-P740: was 8
    public const int HintEverySeconds = 12; // cc-P740: "Move to get up." over the head again
    public const int SafetySeconds = 60;
    public const int WaveEverySeconds = 6;
    public static readonly TimeSpan HeldDelay = TimeSpan.FromSeconds(2);
    public const int DieAnimation = 21;   // the human death animation (fall)
    public const int DieFrames = 6;
    // cc-P732: the held pose. Backward from frame HoldFrames (5, flat on the ground) at the longest delay (TazUO: 80 ms x
    // (delay + 1) a frame, so 20.5 s on each of frames 4 and 3), sent again every HoldEverySeconds.
    public const int HoldFrames = 5;
    public const int HoldDelay = 255;
    public const int HoldEverySeconds = 20;
    // cc-P740: a dusk the trainee and the sand show in (P732's 24 was near night). Picked from TazUO shots of a new
    // character (notes/cc-P740-isle-wake-visible.md).
    public const int DarkLight = 16;     // 0 day, 30 near black
    public static readonly int[] DawnLight = [10, 5]; // after the get-up, one a second
    // cc-P740: the get-up, the same animation backward from the ground at a slow pace (TazUO: 80 ms x (delay + 1) a
    // frame, five frames, so about 2.4 s), sent again GetUpResend later (ClassicUO), and the seconds it is given before
    // the dawn (moves refused meanwhile; the second-long tick makes it GetUpSeconds - 1 to GetUpSeconds).
    public const int GetUpDelay = 5;
    public const int GetUpSeconds = 4;
    public static readonly TimeSpan GetUpResend = TimeSpan.FromMilliseconds(100); // after the reject; P740 tried 300 (a 0.3 s stand flash)
    public const int WaveSound = 0x12;    // "wave01_l"
    public const string LyingLine = "You wake on cold sand, soaked and aching. Waves wash over your boots...";
    public const string MoveHintLine = "Move to get up.";
    public const string LieOverheadLine = "You lie on the wet sand...";

    private sealed class Lie
    {
        public int Light = DarkLight;
        public int Seconds;
        public int Poses; // cc-P732: how many times the held pose was sent
        public int Rising = -1; // cc-P740: seconds into the get-up, or -1 while still lying
        public readonly List<string> Said = new(); // cc-P740: the lines shown over their head, in order
        public Action? Up;
        public TimerExecutionToken Timer;
    }

    private static readonly Dictionary<Serial, Lie> _lying = new();

    // cc-P732: the dawn after standing, the light level until it is back to the isle's own.
    private static readonly Dictionary<Serial, int> _dawn = new();

    // Each trainee's first-login windows and lines, held until they have stood up (Hold, Release).
    private static readonly Dictionary<Serial, List<Action>> _held = new();

    private static bool _hooked;

    public static void Initialize() => EnsureHooks();

    public static void EnsureHooks()
    {
        if (!_hooked)
        {
            _hooked = true;
            EventSink.Disconnected += OnDisconnected;
        }
    }

    /// <summary>Lying on the sand, or (cc-P740) still getting up: moves are refused and the first login's windows wait.</summary>
    public static bool IsLying(Mobile m) => _lying.ContainsKey(m.Serial);

    /// <summary>cc-P740: getting up (the slow animation plays; moves are refused until the dawn).</summary>
    public static bool IsRising(Mobile m) => _lying.TryGetValue(m.Serial, out var l) && l.Rising >= 0;

    public static int? LightOf(Mobile m) =>
        _lying.TryGetValue(m.Serial, out var l) ? l.Light : _dawn.TryGetValue(m.Serial, out var d) ? d : null;

    /// <summary>cc-P732 (tests): how many times the lying pose has been sent to <paramref name="m"/>, or -1 when not lying.</summary>
    public static int PosesSent(Mobile m) => _lying.TryGetValue(m.Serial, out var l) ? l.Poses : -1;

    /// <summary>cc-P740 (tests): the lines shown over <paramref name="m"/>'s head while lying, in order.</summary>
    public static IReadOnlyList<string> OverheadSaid(Mobile m) => _lying.TryGetValue(m.Serial, out var l) ? l.Said : [];

    /// <summary>FoldRegion.AlterLightLevel: the waking light, for this trainee alone.</summary>
    public static void AlterLight(Mobile m, ref int global, ref int personal)
    {
        if (LightOf(m) is { } level)
        {
            global = level;
        }
    }

    /// <summary>
    /// Plays the wake-up for <paramref name="pm"/>: lie until they move (or SafetySeconds), the light coming up, then stand
    /// and <paramref name="up"/> (the first lesson's lines). False when it cannot (no client, dead, already lying): then
    /// <paramref name="up"/> runs at once and anything held is released.
    /// </summary>
    public static bool Play(PlayerMobile pm, Action? up = null)
    {
        if (pm.Deleted || !pm.Alive || pm.NetState == null || IsLying(pm))
        {
            up?.Invoke();
            Release(pm);
            return false;
        }

        EnsureHooks();
        var lie = new Lie { Up = up };
        _lying[pm.Serial] = lie;
        _dawn.Remove(pm.Serial);
        pm.CheckLightLevels(true);
        HoldPose(pm, lie);
        pm.NetState.SendSoundEffect(WaveSound, pm.Location);
        ClusterFTutorial.SystemLine(pm, LyingLine);
        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(pm, lie), out lie.Timer);
        return true;
    }

    /// <summary>Seconds of lying at once (tests): the light comes up, the waves wash, the hint, and the safety stand.</summary>
    public static void Advance(PlayerMobile pm, int seconds)
    {
        for (var i = 0; i < seconds && _lying.TryGetValue(pm.Serial, out var lie); i++)
        {
            Tick(pm, lie);
        }
    }

    /// <summary>cc-P732: the death animation backward from the ground, frame by frame at the longest delay.</summary>
    private static void HoldPose(PlayerMobile pm, Lie lie)
    {
        pm.Animate(DieAnimation, HoldFrames, 1, false, false, HoldDelay);
        lie.Poses++;
    }

    private static void Tick(PlayerMobile pm, Lie lie)
    {
        if (!_lying.TryGetValue(pm.Serial, out var current) || current != lie)
        {
            lie.Timer.Cancel();
            return;
        }

        // cc-P740: getting up; the dawn and the lines after GetUpSeconds.
        if (lie.Rising >= 0)
        {
            if (++lie.Rising >= GetUpSeconds)
            {
                Risen(pm, lie);
            }

            return;
        }

        var s = ++lie.Seconds;
        // cc-P732: the dark and the pose again a second in (the login's went out before the client was in the world),
        // then the pose every HoldEverySeconds; the light stays dark until they stand.
        if (s == 1)
        {
            pm.CheckLightLevels(true);
            Overhead(pm, lie, LieOverheadLine); // cc-P740: said where the eye is, once the client is in the world
        }

        if (s % HoldEverySeconds == 1)
        {
            HoldPose(pm, lie);
        }

        if (s % WaveEverySeconds == 2)
        {
            pm.NetState?.SendSoundEffect(WaveSound, pm.Location);
        }

        if (s == HintSeconds)
        {
            ClusterFTutorial.SystemLine(pm, MoveHintLine);
            Overhead(pm, lie, MoveHintLine);
        }
        else if (s > HintSeconds && (s - HintSeconds) % HintEverySeconds == 0)
        {
            Overhead(pm, lie, MoveHintLine); // cc-P740: again, over the head only
        }

        if (s >= SafetySeconds)
        {
            Stand(pm);
        }
    }

    /// <summary>
    /// cc-P728: PlayerMobile.Move's first call (PlayerMobile-tutorial-wake-move.patch). A trainee lying on the beach gets
    /// up, and the request is refused (true). cc-P740: so is every request while they are getting up. False for anyone
    /// else: the move goes on as pinned's.
    /// </summary>
    public static bool OnMoveRequest(PlayerMobile pm)
    {
        if (!IsLying(pm))
        {
            return false;
        }

        Stand(pm);
        return true;
    }

    /// <summary>
    /// Gets up: cc-P740: the death animation backward, slowly, for GetUpSeconds (the trainee still counts as lying), then the
    /// light coming back (Dawn), then the lie's lines and, HeldDelay later, the windows and lines that were held.
    /// <paramref name="now"/> skips the slow get-up (the vision scene starting).
    /// </summary>
    public static void Stand(PlayerMobile pm, bool now = false)
    {
        if (!_lying.TryGetValue(pm.Serial, out var lie))
        {
            return;
        }

        if (now || pm.Deleted || pm.NetState == null || !pm.Alive)
        {
            Risen(pm, lie);
            return;
        }

        if (lie.Rising >= 0)
        {
            return; // already getting up
        }

        lie.Rising = 0;
        GetUp(pm);
        // cc-P740: again once the refused move's reject is through (ClassicUO ends the animation on it).
        Timer.StartTimer(GetUpResend, () =>
        {
            if (_lying.TryGetValue(pm.Serial, out var current) && current == lie && lie.Rising >= 0 && pm.NetState != null)
            {
                GetUp(pm);
            }
        });
    }

    // cc-P732: from frame HoldFrames (on the ground) up; from DieFrames TazUO drew the standing frame first. cc-P740: at
    // GetUpDelay, so the player sees it.
    private static void GetUp(PlayerMobile pm) => pm.Animate(DieAnimation, HoldFrames, 1, false, false, GetUpDelay);

    // cc-P740: the get-up is over: the dawn, then the lie's lines and the held windows.
    private static void Risen(PlayerMobile pm, Lie lie)
    {
        if (!_lying.Remove(pm.Serial, out var current) || current != lie)
        {
            return;
        }

        lie.Timer.Cancel();
        if (pm.Deleted || pm.NetState == null)
        {
            pm.CheckLightLevels(true);
            _held.Remove(pm.Serial);
            return;
        }

        Dawn(pm, 0, () => AfterDawn(pm, lie));
    }

    private static void AfterDawn(PlayerMobile pm, Lie lie)
    {
        if (pm.Deleted || pm.NetState == null)
        {
            _held.Remove(pm.Serial);
            return;
        }

        lie.Up?.Invoke();
        if (_held.ContainsKey(pm.Serial))
        {
            Timer.StartTimer(HeldDelay, () => Release(pm));
        }
    }

    private static void Overhead(PlayerMobile pm, Lie lie, string line)
    {
        lie.Said.Add(line);
        if (pm.NetState is { } ns)
        {
            pm.PrivateOverheadMessage(MessageType.Regular, 0x3B2, false, line, ns);
        }
    }

    // -- cc-P728 item 1: the first login's other windows and lines wait for the wake-up --

    /// <summary>
    /// True while <paramref name="pm"/>'s first-login windows wait: they lie on the beach, they are about to (their first
    /// login will wake them, TutorialStart.WillWake), or they stood up and the held ones have not come yet.
    /// </summary>
    public static bool IsHolding(PlayerMobile pm) =>
        IsLying(pm) || _dawn.ContainsKey(pm.Serial) || _held.ContainsKey(pm.Serial) || TutorialStart.WillWake(pm);

    /// <summary>Runs <paramref name="action"/> now, or after the wake-up when <paramref name="pm"/> is holding.</summary>
    public static void Hold(PlayerMobile pm, Action action)
    {
        if (!IsHolding(pm))
        {
            action();
            return;
        }

        if (!_held.TryGetValue(pm.Serial, out var list))
        {
            _held[pm.Serial] = list = new List<Action>();
        }

        list.Add(action);
    }

    public static int HeldCount(Mobile m) => _held.TryGetValue(m.Serial, out var list) ? list.Count : 0;

    /// <summary>Runs what was held for <paramref name="pm"/>, in order, unless they are still lying. Returns how many.</summary>
    public static int Release(PlayerMobile pm)
    {
        if (IsLying(pm) || !_held.Remove(pm.Serial, out var list))
        {
            return 0;
        }

        if (pm.Deleted || pm.NetState == null)
        {
            return 0; // their next login runs the login handlers again
        }

        foreach (var action in list)
        {
            action();
        }

        return list.Count;
    }

    // A trainee who disconnects while down: the lie ends at once (unfrozen, no lines), and what was held is dropped; their
    // next login is not a first login, so it plays no wake-up and its handlers run as usual.
    private static void OnDisconnected(Mobile m)
    {
        if (m is PlayerMobile pm)
        {
            if (_lying.Remove(pm.Serial, out var lie))
            {
                lie.Timer.Cancel();
            }

            _dawn.Remove(pm.Serial);

            _held.Remove(pm.Serial);
        }
    }

    // cc-P732: the light comes up after they stand, one DawnLight step a second, then the isle's own. cc-P740: then
    // <paramref name="after"/> (the lines and the windows); a disconnect or a new lie meanwhile ends it.
    private static void Dawn(PlayerMobile pm, int step, Action? after = null)
    {
        if (IsLying(pm) || pm.Deleted || pm.NetState == null)
        {
            _dawn.Remove(pm.Serial);
            return;
        }

        if (step < DawnLight.Length)
        {
            _dawn[pm.Serial] = DawnLight[step];
            pm.CheckLightLevels(true);
            Timer.StartTimer(TimeSpan.FromSeconds(1), () => Dawn(pm, step + 1, after));
            return;
        }

        _dawn.Remove(pm.Serial);
        pm.CheckLightLevels(true);
        after?.Invoke();
    }
}
