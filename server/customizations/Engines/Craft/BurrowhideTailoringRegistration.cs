// cc-P36 Part D.3: Burrowhide, the New Haven dungeon's starter leather, as a Tailoring material at 45 (decisions file
// item 9). Pinned registers the stock leathers at 0 / 65 / 80 / 99 (DefTailoring.cs:658-665: SetSubRes(Leather),
// AddSubRes Leather, Spined, Horned, Barbed, generic name 1044462 "leather", message 1049311); Burrowhide is appended
// after Barbed so the craft menu's remembered material index (CraftContext.LastResourceIndex) does not shift. Leather
// recipes name typeof(Leather) and ConsumeRes swaps in the picked sub-resource, as for Spined. BurrowhideLeather is a
// BaseLeather, not a Leather, so nothing that asks for plain Leather by type (work orders) takes it.
//
// ServerStarted, after DefTailoring.Initialize; the test host calls Register directly (ServerStarted never fires there).

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class BurrowhideTailoringRegistration
{
    public const double RequiredSkill = 45.0;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>Idempotent.</summary>
    public static void Register()
    {
        var tailoring = DefTailoring.CraftSystem;
        if (tailoring == null)
        {
            Console.WriteLine("[BurrowhideTailoringRegistration] WARNING: DefTailoring.CraftSystem is null; Burrowhide is not a tailoring material.");
            return;
        }

        if (tailoring.CraftSubRes.SearchFor(typeof(BurrowhideLeather)) == null)
        {
            tailoring.AddSubRes(typeof(BurrowhideLeather), "Burrowhide", RequiredSkill, 1044462, 1049311);
        }
    }
}
