// cc-P726 item 5 (Chase 10-28 12:24): the isle's starter gear keeps the rat fight safe. The cloak of the Wakening Isle
// (worn) and Wren's pitchfork (held) each add hit point regeneration: IslePoints each on the Wakening Isle and its copies,
// ElsewherePoints each anywhere else (a small newbie help that stops mattering once real gear arrives). RegenRates adds
// it to a player's points before the player cap (18). Numbers: shard-migration notes/cc-P726-isle-pet-reward.md.

#nullable enable

using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialStarterGear
{
    public const int IslePoints = 3;
    public const int ElsewherePoints = 1;

    /// <summary>The hit point regeneration points the starter gear gives <paramref name="m"/> where they stand.</summary>
    public static int RegenHits(Mobile m)
    {
        if (m is not PlayerMobile pm)
        {
            return 0;
        }

        var each = TutorialWorld.IsOnIsle(pm) ? IslePoints : ElsewherePoints;
        var points = 0;
        if (pm.FindItemOnLayer(Layer.Cloak) is WakeningIsleCloak)
        {
            points += each;
        }

        if (TutorialFarm.HoldsPitchfork(pm))
        {
            points += each;
        }

        return points;
    }
}
