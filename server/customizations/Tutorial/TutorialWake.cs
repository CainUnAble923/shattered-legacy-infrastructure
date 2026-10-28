// cc-P721 (Chase 10-28 09:29): a new character's first arrival on the Wakening Isle is a body washed ashore getting up.
// The character falls into the death pose (the human death animation, played forward), lies still in a dark, quiet
// light while the waves sound and "You wake on cold sand..." shows, the light comes up over a few seconds, and they get up
// (the same animation played backward) before the first lesson's lines and the arrow to Tamsin. Movement is blocked
// only while lying down (Frozen, Seconds long). Once per character: TutorialStart.ConsumeAtLogin calls it for the first
// entry only, so remedial trips, copy resets and [Tutorial reset (which leaves the choice consumed) never play it again.
//
// The client draws no screen fade (TazUO ignores the server's screen-effect packet, cc-P665), so the dark is the
// player's own light level (FoldRegion.AlterLightLevel asks LightOf), as the vision does. A client can hold a pose only
// for the animation's length, so the fallen pose is the death animation's last frames, held by the freeze; if a client
// shows the character standing between the two animations, the dark and the lines still carry the moment. Notes:
// shard-migration notes/cc-P721-isle-bandage-wait.md.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialWake
{
    public const int Seconds = 5;
    public const int DieAnimation = 21;   // the human death animation (fall)
    public const int DieFrames = 6;
    public const int WaveSound = 0x12;    // "wave01_l"
    public const string LyingLine = "You wake on cold sand, soaked and aching. Waves wash over your boots...";

    // Each lying trainee's light level (0 day, 30 near black), and the end of their lie.
    private static readonly Dictionary<Serial, int> _light = new();

    public static bool IsLying(Mobile m) => _light.ContainsKey(m.Serial);

    public static int? LightOf(Mobile m) => _light.TryGetValue(m.Serial, out var l) ? l : null;

    /// <summary>FoldRegion.AlterLightLevel: the waking light, for this trainee alone.</summary>
    public static void AlterLight(Mobile m, ref int global, ref int personal)
    {
        if (LightOf(m) is { } level)
        {
            global = level;
        }
    }

    /// <summary>
    /// Plays the wake-up for <paramref name="pm"/>: lie, light coming up, stand, then <paramref name="up"/> (the first
    /// lesson's lines). False when it cannot (no client, dead, already lying): then <paramref name="up"/> runs at once.
    /// </summary>
    public static bool Play(PlayerMobile pm, Action? up = null)
    {
        if (pm.Deleted || !pm.Alive || pm.NetState == null || IsLying(pm))
        {
            up?.Invoke();
            return false;
        }

        pm.Frozen = true;
        SetLight(pm, 24);
        pm.Animate(DieAnimation, DieFrames, 1, true, false, 0);
        pm.NetState.SendSoundEffect(WaveSound, pm.Location);
        ClusterFTutorial.SystemLine(pm, LyingLine);

        Timer.StartTimer(TimeSpan.FromSeconds(2), () => { if (IsLying(pm)) { SetLight(pm, 14); pm.NetState?.SendSoundEffect(WaveSound, pm.Location); } });
        Timer.StartTimer(TimeSpan.FromSeconds(4), () => { if (IsLying(pm)) { SetLight(pm, 6); } });
        Timer.StartTimer(TimeSpan.FromSeconds(Seconds), () => Stand(pm, up));
        return true;
    }

    /// <summary>Gets up (the death animation backward), unfrozen, the light back, then <paramref name="up"/>.</summary>
    public static void Stand(PlayerMobile pm, Action? up = null)
    {
        if (!_light.Remove(pm.Serial))
        {
            return;
        }

        pm.Frozen = false;
        pm.CheckLightLevels(true);
        if (pm.Deleted || pm.NetState == null)
        {
            return;
        }

        if (pm.Alive)
        {
            pm.Animate(DieAnimation, DieFrames, 1, false, false, 0);
        }

        up?.Invoke();
    }

    private static void SetLight(PlayerMobile pm, int level)
    {
        _light[pm.Serial] = level;
        pm.CheckLightLevels(true);
    }
}
