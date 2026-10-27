// cc-P663: the tests' way into the tutorial. Entry is the start choice now (TutorialStart): a character with none, as the
// older facts' fresh characters were, is never entered at login. Begin picks "Tutorial Island" for the character and
// logs it in, which enters it and wakes it on the isle (the Fold, map 32).

using Server;
using Server.Mobiles;
using Server.Tutorial;

namespace ShatteredLegacy.Tests;

internal static class TutorialTestStart
{
    public static void Begin(PlayerMobile pm, TutorialStart.Choice choice = TutorialStart.Choice.Isle)
    {
        FoldMaps.Configure();
        TutorialStart.Record(pm, choice);
        ClusterFTutorial.OnLogin(pm);
    }

    /// <summary>Marks the four lessons before the farm done: Wren answers a trainee only at core.farm or later.</summary>
    public static void ToTheFarm(PlayerMobile pm)
    {
        var record = ClusterFTutorial.RecordOf(pm)!;
        foreach (var key in new[] { "core.movement", "core.backpack", "core.paperdoll", "core.chat" })
        {
            record.StationsDone.Add(key);
        }
    }
}
