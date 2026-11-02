// cc-P769 item 8 (the first outside playtester, 11-01: "I love the achievement boxes ... more for like each tutorial step"):
// an achievement for each Wakening Isle lesson, granted when ClusterFTutorial.CompleteStation records the lesson finished.
// Every achievement is once per account, so a second character's lessons, a remedial run or a restarted lesson grant
// nothing new. The titles and lines are in ClusterFAchievements (tutorial.lesson.*), in the System's voice. Each earn
// brings the usual fanfare and loot box (ClusterFLootBoxes.OnAchievementEarned); no skill or stat reward. Notes:
// shard-migration notes/cc-P769-playtester-ux-notes-1.md.

#nullable enable

using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialLessonAchievements
{
    public const string Prefix = "tutorial.lesson.";

    /// <summary>The achievement for core lesson <paramref name="stationKey"/> ("core.farm" is "tutorial.lesson.farm"), or null.</summary>
    public static string? KeyFor(string stationKey) =>
        TutorialStations.Find(stationKey) is { IsCore: true } && stationKey.StartsWith("core.")
            ? Prefix + stationKey["core.".Length..]
            : null;

    /// <summary>Grants the lesson's achievement to <paramref name="pm"/>'s account. True on a new earn.</summary>
    public static bool Grant(PlayerMobile pm, string stationKey) =>
        KeyFor(stationKey) is { } key && pm.Account is { } acct && ClusterFAchievementSystem.TryGrant(acct, key);
}
