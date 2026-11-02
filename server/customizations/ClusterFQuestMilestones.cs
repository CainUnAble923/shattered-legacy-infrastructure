// cc-P769 item 8 (the first outside playtester, 11-01: "And then for completing a first quest beyond the tutorial"):
// MLQuestEntry-quest-milestones.patch calls OnRewardClaimed at the end of MLQuestInstance.ClaimRewards, after the quest is
// recorded done. The first quest a character's account claims grants "Gainfully Employed" (quest.first), and the New
// Haven trainer quest count is refreshed at once, so "The Tutorial Has a Checkbox" is earned when the quest is turned in
// instead of at the next login (it was only counted at login). The Wakening Isle's and New Haven's tutorial lessons are
// not quests (TutorialStations), so they never count. Notes: shard-migration notes/cc-P769-playtester-ux-notes-1.md.

#nullable enable

using Server.Engines.MLQuests;
using Server.Mobiles;

namespace Server;

public static class ClusterFQuestMilestones
{
    public const string FirstQuestKey = "quest.first";

    public static void OnRewardClaimed(PlayerMobile? pm, MLQuest? quest)
    {
        if (pm?.Account is not { } acct || quest == null)
        {
            return;
        }

        ClusterFAchievementSystem.TryGrant(acct, FirstQuestKey);
        ClusterFAchievementSystem.RefreshNewHavenQuests(pm, acct);
    }
}
