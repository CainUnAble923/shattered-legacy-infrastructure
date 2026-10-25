// cc-P80: the loop signal (F-12 is not built) and the tutorial's loop vision. Notes:
// shard-migration notes/cc-P80-new-player-tutorial.md.
//
// P371 (Chase 2026-10-21): graduation records the first real loop. Young is time-only; no loop removes it.
// F-12's actual levels, skill resets and caps are not built yet. ResetLevelAndSkillsForF12 is their integration hook.
//
// The tutorial's loop demo is a VISION: TutorialVision.RunLoopDemo shows a loop happening, and never touches the real
// character's level, skills, loop count or Young status (a real low cap would crush starting skills). It does not call
// CompleteLoop.

#nullable enable

using System.Globalization;
using Server.Accounting;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Tutorial
{
    public static class ClusterFLoops
    {
        // Existing account FlagValues persist this per-character counter without changing the account save schema.
        private static string LoopKey(PlayerMobile pm) => $"character:{(uint)pm.Serial}:loops";

        public static int LoopOf(PlayerMobile pm) =>
            pm.Account is IAccount acct && ClusterFAccountPersistence.Get(acct) is { } data &&
                data.FlagValues.TryGetValue(LoopKey(pm), out var value)
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var loop) ? loop : 0;

        /// <summary>F-12 ladder only. Does not enforce a level or skill cap.</summary>
        public static int MaxLevelFor(int loop) => loop <= 0 ? 50 : 100 + 5 * (System.Math.Min(loop, 21) - 1);

        /// <summary>Once per character: tutorial graduation (or the configured skip waiver) is loop 1.</summary>
        public static AwardResultOrNone Graduate(PlayerMobile pm) => CompleteLoop(pm, 1);

        /// <summary>
        /// F-12 integration point: implement level-to-1, Keeper/Breaker skill reset/peak memory and cap application here
        /// when that subsystem exists. Today no skill, level, stat or XP is changed by recording a loop.
        /// </summary>
        private static void ResetLevelAndSkillsForF12(PlayerMobile pm, int loop)
        {
        }

        /// <summary>
        /// Records a real loop and plays its fanfare and box once. Young continues to its hours limit.
        /// </summary>
        public static AwardResultOrNone CompleteLoop(PlayerMobile pm, int loop)
        {
            if (pm.Account is not IAccount acct)
            {
                return new AwardResultOrNone(ClusterFLootBoxes.AwardResult.NoRecord);
            }

            if (loop <= LoopOf(pm))
            {
                return new AwardResultOrNone(ClusterFLootBoxes.AwardResult.AlreadyThisLoop);
            }

            ResetLevelAndSkillsForF12(pm, loop);
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[LoopKey(pm)] = loop.ToString(CultureInfo.InvariantCulture);
            return new AwardResultOrNone(ClusterFLootBoxes.OnLoopCompleted(pm, loop));
        }
    }

    public readonly record struct AwardResultOrNone(ClusterFLootBoxes.AwardResult Result);

    public static class TutorialVision
    {
        public static readonly string[] Lines =
        [
            "You see a vision of a sword striking a gem. The gem breaks, and the world fades away.",
            "In the vision, you wake at the beginning with your level and skills reset. This is a preview of a future loop system.",
            "In this vision, your League progress, recipes, achievements, and guild ranks remain. Your companion stays with you too.",
            "The vision is over. Your level, skills, and belongings have not changed."
        ];

        /// <summary>Compatibility entry to the timed vision. Real character progression remains untouched.</summary>
        public static void RunLoopDemo(PlayerMobile pm)
        {
            ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.loop")!);
            TutorialCoreTrials.WatchVision(pm);
        }
    }
}
