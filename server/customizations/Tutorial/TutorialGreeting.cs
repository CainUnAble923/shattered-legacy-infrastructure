// cc-P718 item 1 (Chase's isle walk, 10-28): a newcomer does not know to double-click an NPC. The isle's lesson NPCs a
// trainee must double-click speak first: when the trainee comes within Range, and again every Interval while they stay
// near, an overhead line to that one player (SayTo) tells them to double-click. Each NPC stops once the trainee has used
// it (the caller's own test: Tamsin once she was double-clicked or a lesson is done, Wren once the pet is chosen, Elara
// once the League lesson is done). Notes: shard-migration notes/cc-P718-isle-walkthrough-fixes-1.md.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialGreeting
{
    public const int Range = 8;
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public const string TamsinLine = "Welcome, traveler! Double-click me (two quick left clicks on me) to begin your lessons.";
    public const string WrenLine = "Hello there! Double-click me (two quick left clicks on me) to choose your pet.";
    public const string ElaraLine = "Welcome to the lodge! Double-click me (two quick left clicks on me) to finish your League lesson.";

    // When each NPC last greeted each player, in Core.TickCount milliseconds (in memory: a restart greets again, which is
    // harmless). The tick count, not Core.Now: a test host's NetState resets Core.Now to the wall clock as it sends (D16).
    private static readonly Dictionary<(Serial Npc, Serial Player), long> _said = new();

    /// <summary>
    /// Says <paramref name="line"/> from <paramref name="npc"/> to <paramref name="pm"/> when they are within Range on the
    /// same map, alive, and not greeted by this NPC within Interval. True when it spoke.
    /// </summary>
    public static bool TryGreet(Mobile npc, PlayerMobile pm, string line)
    {
        if (npc.Deleted || pm.Deleted || !pm.Alive || pm.NetState == null || npc.Map != pm.Map || !npc.InRange(pm, Range))
        {
            return false;
        }

        var key = (npc.Serial, pm.Serial);
        if (_said.TryGetValue(key, out var at) && Core.TickCount - at < (long)Interval.TotalMilliseconds)
        {
            return false;
        }

        _said[key] = Core.TickCount;
        npc.SayTo(pm, line);
        Greeted?.Invoke(npc, pm);
        return true;
    }

    /// <summary>
    /// Greets every player within Range of <paramref name="npc"/> who <paramref name="needs"/> it (each within its own
    /// Interval). The NPCs call it from OnThink, so a trainee who stands still is prompted again. Returns how many it greeted.
    /// </summary>
    public static int GreetAround(Mobile npc, Func<PlayerMobile, bool> needs, string line)
    {
        if (npc.Deleted || npc.Map == null || npc.Map == Map.Internal)
        {
            return 0;
        }

        var n = 0;
        foreach (var m in npc.GetMobilesInRange(Range))
        {
            if (m is PlayerMobile pm && needs(pm) && TryGreet(npc, pm, line))
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Raised after each greeting (tests count them, whichever path spoke: approach, think or a call).</summary>
    public static event Action<Mobile, PlayerMobile>? Greeted;

    /// <summary>Forgets every greeting by <paramref name="npc"/> to <paramref name="pm"/> (tests).</summary>
    public static void Forget(Mobile npc, PlayerMobile pm) => _said.Remove((npc.Serial, pm.Serial));

    // -- Who still needs the prompt --

    // Characters who double-clicked Tamsin since the last restart.
    private static readonly HashSet<Serial> _usedGuide = new();

    public static void UsedGuide(PlayerMobile pm) => _usedGuide.Add(pm.Serial);

    /// <summary>A trainee who has not double-clicked Tamsin yet and has no lesson done or running.</summary>
    public static bool NeedsGuide(PlayerMobile pm) =>
        ClusterFTutorial.IsParticipant(pm) && !_usedGuide.Contains(pm.Serial) &&
        ClusterFTutorial.RecordOf(pm) is { StationsDone.Count: 0 } && TutorialCoreTrials.Current(pm) == null;

    /// <summary>A trainee whose next lesson is the farm and whom Wren has not started on it (cc-P726: the pitchfork).</summary>
    public static bool NeedsWren(PlayerMobile pm) =>
        !TutorialFarm.CombatStarted(pm) && ClusterFTutorial.RecordOf(pm) is { } record &&
        System.Linq.Enumerable.FirstOrDefault(ClusterFTutorial.CoreLeft(record))?.Key == TutorialFarm.CoreStation;

    /// <summary>A trainee on the League lesson.</summary>
    public static bool NeedsElara(PlayerMobile pm) => TutorialCoreTrials.IsDoing(pm, "core.league");
}
