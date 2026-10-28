// cc-P718 item 8 (Chase, 10-28 07:20): fighting a rat at the farm gate, Anatomy rose 0.1 at a time from 11.6 to 12.9 in a
// few seconds and the gain lines filled the screen. Why: every swing rolls Tactics and Anatomy (pinned BaseWeapon.cs:2524,
// :2527, on the window 0 to the skill's cap). At 11.6 against the cap of 200 (ClusterFSkillCaps, D64), pinned's gain
// chance is about 0.48 a roll (SkillCheck.cs:122-131: ((total room + 188.4/200) / 2 + 0) / 2 on a missed roll), and the
// shard rolls it twice (ClusterFSkillGain, P191's 2 attempts), so each skill gained on about three swings in four. Below
// 10.0 pinned gains on every use. No isle boost and no bug: the shard's rate, at the bottom of a 200 cap.
//
// On the Wakening Isle (the Fold's isle area, or a copy of it) a player gains each skill at most once per Interval
// (clusterf.tutorial.gainSecondsPerSkill, 10 by default): FoldRegion.AllowGain asks Allow, and ClusterFSkillGain notes each
// gain. Off the isle nothing changes. The 200 shown by the client is the real cap today: F-12's loop-0 cap of 50 is not
// built (ClusterFLoops.ResetLevelAndSkillsForF12), and this does not pretend to enforce it (P709). Notes:
// shard-migration notes/cc-P718-isle-walkthrough-fixes-1.md.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialSkillPace
{
    public const int DefaultSeconds = 10;

    public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(DefaultSeconds);

    // When each player last gained each skill on the isle, in Core.TickCount milliseconds (in memory).
    private static readonly Dictionary<(Serial Player, SkillName Skill), long> _last = new();

    public static void Configure() =>
        Interval = TimeSpan.FromSeconds(Math.Max(0, ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.gainSecondsPerSkill", DefaultSeconds)));

    /// <summary>A player on the Wakening Isle (or a copy of it).</summary>
    public static bool Applies(Mobile? m) => m is PlayerMobile pm && TutorialWorld.IsOnIsle(pm);

    /// <summary>False while <paramref name="m"/> gained <paramref name="skill"/> on the isle within Interval.</summary>
    public static bool Allow(Mobile m, Skill skill) =>
        !Applies(m) || !_last.TryGetValue((m.Serial, skill.SkillName), out var at) ||
        Core.TickCount - at >= (long)Interval.TotalMilliseconds;

    /// <summary>Called after every gain roll (ClusterFSkillGain.ObserveAttempt): notes a gain made on the isle.</summary>
    public static void Note(Mobile m, Skill skill, int before)
    {
        if (skill.BaseFixedPoint > before && Applies(m))
        {
            _last[(m.Serial, skill.SkillName)] = Core.TickCount;
        }
    }

    /// <summary>Forgets a player's gains (tests).</summary>
    public static void Forget(Mobile m)
    {
        foreach (var key in new List<(Serial, SkillName)>(_last.Keys))
        {
            if (key.Item1 == m.Serial)
            {
                _last.Remove(key);
            }
        }
    }
}
