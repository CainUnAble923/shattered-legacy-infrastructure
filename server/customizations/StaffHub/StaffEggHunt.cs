// StaffEggHunt.cs
//
// cc-P810 (P797 rank 1): a world egg hunt a GM starts from the Staff Hub's Events tab (StaffEventsGump) or [StaffEvents egg.
// Our own build of the idea in Bittiez's World Egg Hunt (third-party, reference only; no code taken): its power scroll
// prizes are gone, and its claim has a real range check.
//
// - The GM hides an egg on a spot (here, or a target). The shard is told which facet.
// - Every player on that facet gets a quest arrow to the egg, and anyone arriving there later gets one within
//   EggArrowSeconds. Staff get none.
// - The first living player to double-click it within EggClaimRange wins EggTokens Festival Tokens, and the shard is told.
// - It ends when found, when the GM ends it, or after EggMinutes. A restart removes the egg (EventEgg deletes itself after
//   load), so no hunt outlives the server. Off unless a GM starts one; one at a time.
//
// Saved type: EventEgg (generator, version 0, D40). Numbers: Configuration/staff-events.json.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.RewardStores;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>The arrow a hunt gives: it points at the egg and is stopped when the hunt ends.</summary>
public class EggHuntArrow : QuestArrow
{
    public EggHuntArrow(PlayerMobile pm, Point3D egg) : base(pm, pm, egg.X, egg.Y)
    {
    }
}

public static class StaffEggHunt
{
    public const int BroadcastHue = 0x35;

    private static TimerExecutionToken _timer;

    private static StaffEventsSettings S => StaffEventsSettings.Current;

    public static EventEgg Egg { get; private set; }
    public static DateTime EndsAt { get; private set; }
    public static string LastResult { get; private set; }

    public static bool Running => Egg is { Deleted: false };

    public static bool Start(Mobile from, Map map, Point3D spot, out string message)
    {
        if (Running)
        {
            message = "An egg hunt is already running.";
            return false;
        }

        if (map == null || map == Map.Internal)
        {
            message = "Pick a spot on a facet.";
            return false;
        }

        if (map == Tutorial.TutorialWorld.Isle)
        {
            message = "Not on the Wakening Isle.";
            return false;
        }

        Egg = new EventEgg();
        Egg.MoveToWorld(spot, map);
        EndsAt = Core.Now + TimeSpan.FromMinutes(S.EggMinutes);
        LastResult = null;

        World.Broadcast(BroadcastHue, false,
            $"An egg hunt! An egg is hidden somewhere in {map.Name}. Follow your arrow; the first to find it wins {S.EggTokens} Festival Tokens.");

        PointEveryone();
        _timer.Cancel();
        Timer.StartTimer(TimeSpan.FromSeconds(S.EggArrowSeconds), TimeSpan.FromSeconds(S.EggArrowSeconds), Tick, out _timer);

        message = $"Egg hidden at {spot.X}, {spot.Y}, {spot.Z} in {map.Name}; it lasts {S.EggMinutes} minutes.";
        return true;
    }

    /// <summary>Gives an arrow to every player on the egg's facet who has none. Returns how many got one.</summary>
    public static int PointEveryone()
    {
        if (!Running)
        {
            return 0;
        }

        var given = 0;

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && Point(pm))
            {
                given++;
            }
        }

        return given;
    }

    /// <summary>One player's arrow: given when they are a player on the egg's facet and have no running hunt arrow.</summary>
    public static bool Point(PlayerMobile pm)
    {
        if (!Running || pm.AccessLevel > AccessLevel.Player || pm.Map != Egg.Map)
        {
            return false;
        }

        if (pm.QuestArrow is EggHuntArrow { Running: true })
        {
            return false;
        }

        pm.QuestArrow?.Stop();
        pm.QuestArrow = new EggHuntArrow(pm, Egg.Location);
        return true;
    }

    public static void Tick()
    {
        if (!Running)
        {
            _timer.Cancel();
            return;
        }

        if (Core.Now >= EndsAt)
        {
            Finish("Nobody found the egg in time.");
            World.Broadcast(BroadcastHue, false, "The egg hunt is over: nobody found the egg.");
            return;
        }

        PointEveryone();
    }

    /// <summary>A double-click on the egg: the first living player in range wins.</summary>
    public static bool TryClaim(Mobile from, EventEgg egg)
    {
        if (egg != Egg || !Running)
        {
            return false;
        }

        if (from is not PlayerMobile pm || pm.AccessLevel > AccessLevel.Player)
        {
            from.SendMessage("Staff cannot win the egg hunt.");
            return false;
        }

        if (!pm.Alive)
        {
            pm.SendMessage("You must be alive to claim the egg.");
            return false;
        }

        if (pm.Map != egg.Map || !pm.InRange(egg.GetWorldLocation(), S.EggClaimRange))
        {
            pm.SendMessage("You are too far away to reach the egg.");
            return false;
        }

        FestivalTokens.Grant(pm, S.EggTokens, "the egg hunt");
        Finish($"{pm.Name} found the egg.");
        World.Broadcast(BroadcastHue, false, $"{pm.Name} has found the egg and wins the egg hunt!");
        return true;
    }

    public static bool End(Mobile from, out string message)
    {
        if (!Running)
        {
            message = "No egg hunt is running.";
            return false;
        }

        Finish($"Ended by {from?.Name ?? "staff"}.");
        World.Broadcast(BroadcastHue, false, "The egg hunt has been called off.");
        message = "Egg hunt ended.";
        return true;
    }

    private static void Finish(string result)
    {
        _timer.Cancel();
        LastResult = result;

        var egg = Egg;
        Egg = null;
        egg?.Delete();

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile { QuestArrow: EggHuntArrow arrow })
            {
                arrow.Stop();
            }
        }
    }

    public static List<string> Status()
    {
        if (!Running)
        {
            return [LastResult == null ? "Egg hunt: none running." : $"Egg hunt: none running. Last: {LastResult}"];
        }

        var left = EndsAt - Core.Now;
        return
        [
            $"Egg hunt: at {Egg.X}, {Egg.Y}, {Egg.Z} in {Egg.Map.Name}",
            $"Time left {(int)left.TotalMinutes}:{Math.Max(0, left.Seconds):00}; prize {S.EggTokens} Festival Tokens"
        ];
    }

    /// <summary>For the facts: forgets the hunt without broadcasting.</summary>
    public static void ForgetForTests()
    {
        _timer.Cancel();
        Egg?.Delete();
        Egg = null;
        LastResult = null;
    }

    internal static void ExpireNowForTests() => EndsAt = Core.Now.AddSeconds(-1);
}

[SerializationGenerator(0)]
public partial class EventEgg : Item
{
    private static readonly int[] _hues = [0x21, 0x35, 0x48, 0x59, 0x5F, 0x8A, 0x96, 0x481];

    [Constructible]
    public EventEgg() : base(StaffEventsSettings.Current.EggItemID)
    {
        Movable = false;
        Hue = Utility.RandomList(_hues);
    }

    public override string DefaultName => "a hidden egg";

    public override void OnDoubleClick(Mobile from) => StaffEggHunt.TryClaim(from, this);

    // A hunt never outlives a restart: an egg left in a save removes itself once the world is loaded.
    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(TimeSpan.Zero, Delete);
}
