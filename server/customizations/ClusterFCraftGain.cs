// cc-P61 Part A (bug-list D83, Chase 2026-10-05): a post-Valorite metal raises the Blacksmithy gain ceiling; it changes
// nothing else about the craft. cc-P67 (Chase 2026-10-05 and 2026-10-06): the same rule for Carpentry, Fletching and
// Tinkering, a band that favors harder items, and ceilings that follow worn bonuses.
//
// Pinned rolls a craft's gain once per craft skill inside CraftItem.GetSuccessChance(gainSkills: true): it calls
// from.CheckSkill(skill, item min, item max) (CraftItem.cs:891-894 at d4531cd9), and SkillCheck.CheckLocation gains
// nothing below min ("too difficult") or at or above max ("no challenge"), and between them rolls with
// chance = (value - min) / (max - min) (SkillCheck.cs:56-75). The material never enters that roll: it only gates use
// (CraftItem.ConsumeRes, `Skills[MainSkill].Base < RequiredSkill`, CraftItem.cs:630). Stock items top out at 140
// (Blacksmithy, the kabutos), 140.3 (Carpentry, the tetsubo), 130 (Fletching, the yumi; Tinkering, the wind chimes), while
// the shard's materials need up to 200, so no craft could climb past its stock items.
//
// The rule: a craft in a shard material can gain its craft skill up to the material's gain ceiling, the next material's
// requirement (ClusterFMetalTiers for Blacksmithy and Tinkering, ClusterFWoodTiers for Carpentry and Fletching).
// - Below the item's own maximum the window is stock's, untouched.
// - From the item's maximum up, the window is (item's own minimum, ceiling) (cc-P67 Part C 2; cc-P61 slid the item's
//   width up instead, so every item taught alike). Stock's roll over that band: chance = (value - min) / (ceiling - min),
//   so an item with a higher minimum has a lower chance and a larger gain bonus ((1 - chance) / 2 on a passed check,
//   SkillCheck.cs:130). Harder items teach better; no extra multiplier (Part C 3).
// - The craft must consume the material (cc-P54's consume check): one of its resources is the sub-resource list's base
//   type, which CraftItem.ConsumeRes swaps for the picked material (CraftItem.cs:615-627), and the item has a stock
//   window at all (min < max). A material cut such as logs to boards (DefCarpentry.cs:95, 0-0) or kindling
//   (DefBowFletching.cs:94, 0-0) never teaches, as in stock.
// - Worn bonuses (cc-P67 Part E, the rule of cc-P66's hook): the band's top is the ceiling plus the player's worn bonus
//   (Value - Base), so gains stop when Base reaches the ceiling, whatever is worn.
// The gain itself is still pinned's Gain, so the skill cap, the lock and the total cap apply. Success chance, failure,
// material loss and the exceptional chance read the item's own range in GetSuccessChance and are not touched.
//
// The one call site is a line in our CraftItem-shard-hooks.patch (GetSuccessChance's gain call). "Orders that still
// teach me" reads the same Window (ClusterFSmithTeaching.Teaches), so the two cannot disagree.

using System;
using System.Linq;
using Server.Engines.Craft;
using Server.Items;

namespace Server;

public static class ClusterFCraftGain
{
    /// <summary>
    /// The gain window for a craft at <paramref name="value"/>: the item's own (min, max) below max, or with no higher
    /// ceiling; from max up, (min, ceiling + worn bonus).
    /// </summary>
    public static (double Min, double Max) Window(double value, double min, double max, double ceiling, double wornBonus = 0.0)
    {
        var top = ceiling + wornBonus;
        if (ceiling <= 0.0 || value < max || top <= max)
        {
            return (min, max);
        }

        return (min, top);
    }

    /// <summary>The same for <paramref name="m"/>'s <paramref name="skill"/>, with its worn bonus.</summary>
    public static (double Min, double Max) Window(Mobile m, SkillName skill, double min, double max, double ceiling) =>
        Window(m.Skills[skill].Value, min, max, ceiling, ClusterFSkillGain.WornBonus(m, m.Skills[skill]));

    /// <summary>
    /// The gain ceiling of a craft in the material it uses (typeRes, or the craft's default when the player picked none):
    /// 0 when the craft has none (another craft, a stock material, a material cut, or a recipe that does not consume it).
    /// </summary>
    public static double CeilingFor(CraftSystem system, CraftItem item, Type typeRes)
    {
        if (item.UseSubRes2 || !Consumes(system, item) || !HasStockWindow(system, item))
        {
            return 0.0;
        }

        var resType = HammerMetal.CraftedResource(typeRes, system, item);
        return resType == null ? 0.0 : CeilingFor(system, CraftResources.GetFromType(resType));
    }

    /// <summary>The material table of each craft that has one; 0 for any other craft.</summary>
    public static double CeilingFor(CraftSystem system, CraftResource res) => system switch
    {
        DefBlacksmithy or DefTinkering  => ClusterFMetalTiers.GainCeiling(res),
        DefCarpentry or DefBowFletching => ClusterFWoodTiers.GainCeiling(res),
        _                               => 0.0
    };

    /// <summary>
    /// The craft consumes the material picked in the menu: one of its resources is the sub-resource list's base type (the
    /// test CraftItem.ConsumeRes makes before it swaps in the picked material, CraftItem.cs:615-627).
    /// </summary>
    public static bool Consumes(CraftSystem system, CraftItem item)
    {
        var baseType = (item.UseSubRes2 ? system.CraftSubRes2 : system.CraftSubRes).ResType;
        return baseType != null && item.Resources.Any(r => r.ItemType == baseType);
    }

    // A stock window with width: Board and Kindling (0, 0) are material cuts that never teach in stock.
    private static bool HasStockWindow(CraftSystem system, CraftItem item) =>
        item.Skills.FirstOrDefault(s => s.SkillToMake == system.MainSkill) is { } s && s.MaxSkill > s.MinSkill;

    /// <summary>
    /// The craft's gain roll for one of its skills: pinned's own CheckSkill, with the window above for the craft's main
    /// skill in a shard material. Called from CraftItem.GetSuccessChance in place of its from.CheckSkill(skill, min, max).
    /// </summary>
    public static void CheckSkill(Mobile from, CraftItem item, CraftSystem system, Type typeRes, CraftSkill craftSkill)
    {
        var min = craftSkill.MinSkill;
        var max = craftSkill.MaxSkill;

        if (craftSkill.SkillToMake == system.MainSkill)
        {
            var (bandMin, bandMax) = Window(from, craftSkill.SkillToMake, min, max, CeilingFor(system, item, typeRes));
            if (bandMax != max)
            {
                // The band's top already carries the worn bonus; cc-P66's cap rule must not add it again.
                ClusterFSkillGain.CheckSkillWornBonusIncluded(from, craftSkill.SkillToMake, bandMin, bandMax);
                return;
            }
        }

        from.CheckSkill(craftSkill.SkillToMake, min, max);
    }
}
