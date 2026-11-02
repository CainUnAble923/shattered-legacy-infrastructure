// cc-P763: the loot boxes' per-account counters (the League Dive's work orders), kept with the achievement counters so
// they are saved with them.

namespace Server;

public static partial class ClusterFAchievementSystem
{
    /// <summary>Adds one to a loot box counter of the account and returns the new count.</summary>
    public static int BumpLootBoxCounter(string username, string counter) => Increment(username, counter);
}
