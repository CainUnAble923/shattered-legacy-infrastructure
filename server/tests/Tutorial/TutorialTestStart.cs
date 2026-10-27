// cc-P663: the tests' way into the tutorial. Entry is the start choice now (TutorialStart): a character with none, as the
// older facts' fresh characters were, is never entered at login. Begin picks "Tutorial Island" for the character and
// logs it in, which enters it and wakes it on the isle (the Fold, map 32).

using System.Linq;
using Server;
using Server.Mobiles;
using Server.Tutorial;

namespace ShatteredLegacy.Tests;

internal static class TutorialTestStart
{
    public static void Begin(PlayerMobile pm, TutorialStart.Choice choice = TutorialStart.Choice.Isle)
    {
        FoldMaps.Configure();
        // cc-P705: the test world's map is void at the isle's site (no P662 patch in the host); it stands in for the land.
        TutorialWorld.IsleReady = () => true;
        ReleaseIdleCopies();
        TutorialStart.Record(pm, choice);
        ClusterFTutorial.OnLogin(pm);
    }

    /// <summary>
    /// cc-P664: facts across the suite enter trainees and seldom free their copies; a copy with nobody online on it is
    /// handed back to the pool (its seeding kept), so the 16 never run out between facts.
    /// </summary>
    public static void ReleaseIdleCopies()
    {
        foreach (var slot in TutorialCopies.Slots)
        {
            if (slot.InUse && !TutorialCopies.Occupants(slot).Any(o => o.NetState != null))
            {
                slot.InUse = false;
            }
        }
    }

    /// <summary>cc-P664: every copy emptied (what it holds removed), unused and unseeded; nobody assigned or waiting.</summary>
    public static void FreeAllCopies()
    {
        foreach (var slot in TutorialCopies.Slots)
        {
            TutorialCopies.Reset(slot);
            slot.InUse = false;
        }

        TutorialCopies.Assigned.Clear();
        TutorialCopies.Waiting.Clear();
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
