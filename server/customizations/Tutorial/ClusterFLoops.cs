// cc-P80: the loop signal (F-12 is not built) and the tutorial's loop vision. Notes:
// shard-migration notes/cc-P80-new-player-tutorial.md.
//
// ClusterFLoops.CompleteLoop is the one place a REAL loop is signaled: the F-12 reset routine calls it once it has reset
// the character. A real loop ends Young status (F-5 addition, Chase 2026-10-11: Young also ends at the first real loop;
// pinned Account.RemoveYoungStatus, Account.cs:830-853, the single chokepoint, clears it for every character on the
// account), then plays the loop's fanfare and box (ClusterFLootBoxes.OnLoopCompleted).
//
// The tutorial's loop demo is a VISION: TutorialVision.RunLoopDemo shows a loop happening, and never touches the real
// character's level, skills, loop count or Young status (a real low cap would crush starting skills). It does not call
// CompleteLoop.

#nullable enable

using Server.Accounting;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Tutorial
{
    public static class ClusterFLoops
    {
        // RemoveYoungStatus sends pinned's own "no longer considered a young player" line (1019039) itself; 0 adds no
        // second cliloc, and the System line below says why.
        public const int YoungEndedMessage = 0;

        /// <summary>
        /// A real loop completed (the F-12 reset's call). Ends Young for the account, then the loop's fanfare and box.
        /// </summary>
        public static AwardResultOrNone CompleteLoop(PlayerMobile pm, int loop)
        {
            if (pm.Account is Account acct && acct.Young)
            {
                acct.RemoveYoungStatus(YoungEndedMessage);
                pm.SendMessage(0x3B2, "System: Your first real loop. You are not Young anymore. Nobody stays Young forever. Some just stay naive.");
            }

            return new AwardResultOrNone(ClusterFLootBoxes.OnLoopCompleted(pm, loop));
        }
    }

    public readonly record struct AwardResultOrNone(ClusterFLootBoxes.AwardResult Result);

    public static class TutorialVision
    {
        public static readonly string[] Lines =
        [
            "System: A vision. The sword strikes the Gem. The shards fly. You fall, and the world folds shut behind you.",
            "System: You wake where you started, remembering. That is a loop. Your level goes back to 1; your skills go with it.",
            "System: What you keep: the League, your recipes, your achievements, your guild ranks. And me. Lucky you.",
            "System: This was only a vision. Nothing about you has changed. Yet."
        ];

        /// <summary>Compatibility entry to the timed vision. Real character progression remains untouched.</summary>
        public static void RunLoopDemo(PlayerMobile pm)
        {
            ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.loop")!);
            TutorialCoreTrials.WatchVision(pm);
        }
    }
}
