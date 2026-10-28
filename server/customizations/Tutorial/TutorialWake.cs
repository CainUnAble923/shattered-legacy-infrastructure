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

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialWake
{
    public const int HintSeconds = 8;
    public const int SafetySeconds = 60;
    public const int WaveEverySeconds = 6;
    public static readonly TimeSpan HeldDelay = TimeSpan.FromSeconds(2);
    public const int DieAnimation = 21;   // the human death animation (fall)
    public const int DieFrames = 6;
    public const int WaveSound = 0x12;    // "wave01_l"
    public const string LyingLine = "You wake on cold sand, soaked and aching. Waves wash over your boots...";
    public const string MoveHintLine = "Move to get up.";

    private sealed class Lie
    {
        public int Light = 24; // 0 day, 30 near black
        public int Seconds;
        public Action? Up;
        public TimerExecutionToken Timer;
    }

    private static readonly Dictionary<Serial, Lie> _lying = new();

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

    public static bool IsLying(Mobile m) => _lying.ContainsKey(m.Serial);

    public static int? LightOf(Mobile m) => _lying.TryGetValue(m.Serial, out var l) ? l.Light : null;

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
        pm.Frozen = true;
        pm.CheckLightLevels(true);
        pm.Animate(DieAnimation, DieFrames, 1, true, false, 0);
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

    private static void Tick(PlayerMobile pm, Lie lie)
    {
        if (!_lying.TryGetValue(pm.Serial, out var current) || current != lie)
        {
            lie.Timer.Cancel();
            return;
        }

        var s = ++lie.Seconds;
        if (s == 2)
        {
            SetLight(pm, lie, 14);
        }
        else if (s == 4)
        {
            SetLight(pm, lie, 6);
        }

        if (s % WaveEverySeconds == 2)
        {
            pm.NetState?.SendSoundEffect(WaveSound, pm.Location);
        }

        if (s == HintSeconds)
        {
            ClusterFTutorial.SystemLine(pm, MoveHintLine);
            if (pm.NetState is { } ns)
            {
                pm.PrivateOverheadMessage(MessageType.Regular, 0x3B2, false, MoveHintLine, ns);
            }
        }

        if (s >= SafetySeconds)
        {
            Stand(pm);
        }
    }

    /// <summary>
    /// cc-P728: PlayerMobile.Move's first call (PlayerMobile-tutorial-wake-move.patch). A trainee lying on the beach gets
    /// up, and the request is refused (true). False for anyone else: the move goes on as pinned's.
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
    /// Gets up (the death animation backward), unfrozen, the light back, then the lie's lines and, HeldDelay later, the
    /// windows and lines that were held.
    /// </summary>
    public static void Stand(PlayerMobile pm)
    {
        if (!_lying.Remove(pm.Serial, out var lie))
        {
            return;
        }

        lie.Timer.Cancel();
        pm.Frozen = false;
        pm.CheckLightLevels(true);
        if (pm.Deleted || pm.NetState == null)
        {
            _held.Remove(pm.Serial);
            return;
        }

        if (pm.Alive)
        {
            pm.Animate(DieAnimation, DieFrames, 1, false, false, 0);
        }

        lie.Up?.Invoke();
        if (_held.ContainsKey(pm.Serial))
        {
            Timer.StartTimer(HeldDelay, () => Release(pm));
        }
    }

    // -- cc-P728 item 1: the first login's other windows and lines wait for the wake-up --

    /// <summary>
    /// True while <paramref name="pm"/>'s first-login windows wait: they lie on the beach, they are about to (their first
    /// login will wake them, TutorialStart.WillWake), or they stood up and the held ones have not come yet.
    /// </summary>
    public static bool IsHolding(PlayerMobile pm) =>
        IsLying(pm) || _held.ContainsKey(pm.Serial) || TutorialStart.WillWake(pm);

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
                pm.Frozen = false;
            }

            _held.Remove(pm.Serial);
        }
    }

    private static void SetLight(PlayerMobile pm, Lie lie, int level)
    {
        lie.Light = level;
        pm.CheckLightLevels(true);
    }
}
