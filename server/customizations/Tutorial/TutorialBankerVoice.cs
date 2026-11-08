// cc-P817 (Chase 11-07: "banker on tutorial island facing wrong way", "he should also be speaking telling player what to
// do"). Merrin, the isle's practice banker, stands behind the counter (TutorialWorld.Banker 6762,3976; the counter runs
// west to east at y 3977, isle-sets.json interiors.bank) and faces its customer side, south, toward the carpet and the
// door in the bank's south wall (6762,3980), the way a trainee walks up (guide 3 and 11.2: a counter's serving side
// toward the door, the keeper behind it). He was placed with no facing, so he stood facing north, at the vault chests on
// the back wall. Seeding turns an already placed Merrin too (TutorialCorePlacement.SeedOn, every world load).
//
// His voice: while a trainee is on the bank lesson and within VoiceRange of him, he greets them over his head and says the
// lesson's next step in a few words, as Wren and Tamsin do (SayTo: that trainee alone), once and then again only after
// VoicePause: open the box by saying "bank", then put the blue practice token in it. The full steps stay in the lesson
// window. Notes: shard-migration notes/cc-P817-creation-and-wake.md.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialBankerVoice
{
    public const Direction Facing = Direction.South;
    public const int VoiceRange = 5;
    public static readonly TimeSpan VoicePause = TimeSpan.FromSeconds(25);

    public const string OpenLine = "Welcome to the bank! Say 'bank' and I will open your bank box.";
    public const string DepositLine = "Your bank box is open. Drag the blue practice token from your backpack into it.";

    // When each trainee last heard him (not saved: a restart greets them again).
    private static readonly Dictionary<Serial, DateTime> _lastSaid = new();

    /// <summary>The isle's banker on <paramref name="map"/> (beside his spot), or null.</summary>
    public static Banker? Find(Map? map)
    {
        if (map == null)
        {
            return null;
        }

        foreach (var m in map.GetMobilesInRange(TutorialWorld.Ground(map, TutorialWorld.Banker), 2))
        {
            if (m is Banker b && !b.Deleted)
            {
                return b;
            }
        }

        return null;
    }

    /// <summary>Turns the isle's banker on <paramref name="map"/> to his counter's customer side. True when he was turned.</summary>
    public static bool FaceCounter(Map? map)
    {
        if (Find(map) is not { } banker || banker.Direction == Facing)
        {
            return false;
        }

        banker.Direction = Facing;
        return true;
    }

    /// <summary>The next step for this trainee, in his words: open the box, then put the token in it.</summary>
    public static string LineFor(PlayerMobile pm) =>
        pm.FindBankNoCreate() is { Opened: true } ? DepositLine : OpenLine;

    /// <summary>
    /// The bank lesson's tick: near Merrin, he says the next step, then not again for VoicePause. Returns what he said,
    /// or null.
    /// </summary>
    public static string? Speak(PlayerMobile pm)
    {
        if (!pm.Alive || Find(pm.Map) is not { } banker || !pm.InRange(banker, VoiceRange) || Math.Abs(pm.Z - banker.Z) > 16)
        {
            return null;
        }

        if (_lastSaid.TryGetValue(pm.Serial, out var at) && Core.Now - at < VoicePause)
        {
            return null;
        }

        var line = LineFor(pm);
        _lastSaid[pm.Serial] = Core.Now;
        banker.SayTo(pm, line);
        return line;
    }

    /// <summary>Forgets when a trainee last heard him (a new lesson start greets them at once).</summary>
    public static void Forget(PlayerMobile pm) => _lastSaid.Remove(pm.Serial);
}
