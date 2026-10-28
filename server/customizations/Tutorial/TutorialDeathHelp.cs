// cc-P721 (Chase 10-28 09:19): dying on the Wakening Isle, in plain words. Outside the death lesson (which has its own
// lines), a trainee who dies on the isle is told they are a ghost and given an arrow to Sera, the isle's healer; once
// alive again, if their body holds anything, an arrow back to it and how to take their things (double-click it: your
// own body gives your things straight back, pinned Corpse.Open's self-loot). A Young character keeps their belongings
// in the backpack (pinned PlayerMobile's death rules), so they are told there is nothing to fetch. The full words the
// first time per character (an account flag value), a short reminder after. Notes: shard-migration
// notes/cc-P721-isle-bandage-wait.md.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialDeathHelp
{
    public static readonly TimeSpan WatchFor = TimeSpan.FromMinutes(20);

    public static readonly string[] FirstLines =
    [
        "You have died. You are a ghost now: you can walk about, but you cannot pick things up or fight.",
        "Follow the arrow to Sera the healer and double-click her, then click Resurrect to come back to life."
    ];

    public const string YoungLine = "While you are Young, your belongings stay in your backpack when you die, so there is nothing to fetch.";
    public const string BodyLine = "Your belongings stay on your body for a while. Once you are alive again, walk back to it and double-click it to take them back.";
    public const string ReminderLine = "You died. Follow the arrow to Sera the healer and double-click her to come back to life.";
    public const string BackToBodyLine = "You are alive again. Follow the arrow to your body and double-click it: your things go straight back to you. It crumbles after a while.";

    private static string Key(Mobile m) => $"character:{(uint)m.Serial}:isleDeath";

    // Ghosts being watched for their resurrection.
    private static readonly Dictionary<Serial, DateTime> _watching = new();
    private static TimerExecutionToken _timer;

    /// <summary>Whether this character has had the full words once.</summary>
    public static bool Told(PlayerMobile pm) =>
        pm.Account is IAccount acct && ClusterFAccountPersistence.Get(acct)?.FlagValues.ContainsKey(Key(pm)) == true;

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnDeath(PlayerMobile pm) => Help(pm);

    /// <summary>The words and the arrow to Sera for a trainee who died on the isle. True when it helped.</summary>
    public static bool Help(PlayerMobile pm)
    {
        if (pm.Alive || !TutorialWorld.IsOnIsle(pm) || TutorialCoreTrials.IsDoing(pm, "core.death") || pm.Account is not IAccount acct)
        {
            return false;
        }

        if (!Told(pm))
        {
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[Key(pm)] = "1";
            foreach (var line in FirstLines)
            {
                ClusterFTutorial.SystemLine(pm, line);
            }

            ClusterFTutorial.SystemLine(pm, pm.Young ? YoungLine : BodyLine);
        }
        else
        {
            ClusterFTutorial.SystemLine(pm, ReminderLine);
        }

        TutorialCoreTrials.Point(pm, TutorialCorePlacement.HealerSpot, "Sera the healer");
        _watching[pm.Serial] = Core.Now + WatchFor;
        if (!_timer.Running)
        {
            Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), WatchAll, out _timer);
        }

        return true;
    }

    private static void WatchAll()
    {
        foreach (var (serial, until) in new List<KeyValuePair<Serial, DateTime>>(_watching))
        {
            if (World.FindMobile(serial) is not PlayerMobile { Deleted: false } pm || Core.Now > until)
            {
                _watching.Remove(serial);
            }
            else if (pm.Alive)
            {
                _watching.Remove(serial);
                Revived(pm);
            }
        }

        if (_watching.Count == 0)
        {
            _timer.Cancel();
        }
    }

    /// <summary>Alive again: an arrow to the body when it holds anything.</summary>
    public static bool Revived(PlayerMobile pm)
    {
        if (pm.Corpse is not Corpse { Deleted: false } body || body.Items.Count == 0 || body.Map != pm.Map)
        {
            pm.QuestArrow?.Stop();
            return false;
        }

        ClusterFTutorial.SystemLine(pm, BackToBodyLine);
        TutorialCoreTrials.Point(pm, body.Location, "your body", body.Map);
        return true;
    }
}
