// cc-P61 Part A (bug-list D83, Chase 2026-10-05): a post-Valorite metal raises the Blacksmithy gain ceiling; it changes
// nothing else about the craft.
//
// Pinned rolls a craft's gain once per craft skill inside CraftItem.GetSuccessChance(gainSkills: true): it calls
// from.CheckSkill(skill, item min, item max) (CraftItem.cs:891-894 at d4531cd9), and SkillCheck.CheckLocation gains
// nothing below min ("too difficult") or at or above max ("no challenge"), and between them rolls with
// chance = (value - min) / (max - min) (SkillCheck.cs:56-75). The metal never enters that roll: it only gates use
// (CraftItem.ConsumeRes, `Skills[MainSkill].Base < RequiredSkill`, CraftItem.cs:630). Every Blacksmithy item is 50 points
// wide and the highest tops out at 140 (the kabutos), while Platinum to Celestial need 112.5 to 200 (ClusterFMetalTiers),
// so no craft could take Blacksmithy past 140.
//
// The rule: a Blacksmithy craft in a post-Valorite metal can gain Blacksmithy up to the metal's gain ceiling, the next
// metal's requirement (ClusterFMetalTiers.GainCeiling). Below the item's own maximum the window is stock's, untouched.
// From the item's maximum to the ceiling the window is the item's own width slid up so that its top is the ceiling:
// (ceiling - (max - min), ceiling). So the roll there is exactly stock's roll for that item at the same distance below
// its maximum (chance 1 - distance / width; with every width 50 and the band at most 12.5 deep, 0.75 to 1). The gain
// itself is still pinned's Gain, so the skill cap, the lock and the total cap all apply. Success chance, failure,
// material loss and the exceptional chance read the item's own range in GetSuccessChance and are not touched.
//
// The one call site is a line in our CraftItem-shard-hooks.patch (GetSuccessChance's gain call now reads Window first).
// "Orders that still teach me" reads the same Window (ClusterFSmithTeaching.Teaches), so the two cannot disagree.

using System;
using Server.Engines.Craft;
using Server.Items;

namespace Server;

public static class ClusterFCraftGain
{
    /// <summary>
    /// The gain window for a Blacksmithy craft at <paramref name="value"/>: the item's own (min, max) below max, or with
    /// no higher ceiling; from max up, the item's width slid up to end at <paramref name="ceiling"/>.
    /// </summary>
    public static (double Min, double Max) Window(double value, double min, double max, double ceiling)
    {
        if (value < max || ceiling <= max)
        {
            return (min, max);
        }

        return (ceiling - (max - min), ceiling);
    }

    /// <summary>The metal a craft uses, for its ceiling: typeRes, or the craft's default when the player picked none.</summary>
    public static double CeilingFor(CraftSystem system, CraftItem item, Type typeRes)
    {
        if (system is not DefBlacksmithy || item.UseSubRes2)
        {
            return 0.0;
        }

        var res = HammerMetal.CraftedResource(typeRes, system, item);
        return res == null ? 0.0 : ClusterFMetalTiers.GainCeiling(CraftResources.GetFromType(res));
    }

    /// <summary>
    /// The craft's gain roll for one of its skills: pinned's own CheckSkill, with the window above for Blacksmithy in a
    /// post-Valorite metal. Called from CraftItem.GetSuccessChance in place of its from.CheckSkill(skill, min, max).
    /// </summary>
    public static void CheckSkill(Mobile from, CraftItem item, CraftSystem system, Type typeRes, CraftSkill craftSkill)
    {
        var min = craftSkill.MinSkill;
        var max = craftSkill.MaxSkill;

        if (craftSkill.SkillToMake == system.MainSkill)
        {
            (min, max) = Window(from.Skills[craftSkill.SkillToMake].Value, min, max, CeilingFor(system, item, typeRes));
        }

        from.CheckSkill(craftSkill.SkillToMake, min, max);
    }
}
