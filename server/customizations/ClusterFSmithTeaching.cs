using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Craft;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P42 Part G1, Chase 2026-10-03). The Society of Smiths' orders (small and large bulk orders
/// from the guildmaster, the Bulk Order choice and the guild book, and commissions) ask for items that can still
/// raise the smith's Blacksmithy. Stock generation accepts any item with a success chance above 0
/// (SmallSmithBOD.cs:139-157), so a Grandmaster is sent ringmail that teaches nothing.
///
/// The engine's own gain rule, not a guess: crafting rolls Mobile.CheckSkill(skill, item min, item max) for each of
/// the item's craft skills (CraftItem.GetSuccessChance, CraftItem.cs:874-894), and SkillCheck.CheckLocation gains
/// nothing below the minimum ("too difficult") or at or above the maximum ("no challenge") (SkillCheck.cs:56-74).
/// So an item teaches while min &lt;= Blacksmithy &lt; max. Material and exceptional quality do not enter that roll; they
/// count where the engine counts them, in whether the smith can make the item at all (the stock checks: every
/// required skill met, success chance, and for an exceptional order a chance of exceptional above 0), and the
/// material rolls stay stock.
///
/// When nothing teaches (Blacksmithy at or above the top of every item on offer), the order falls back to the
/// hardest items the smith can make: the highest maximum, the ones closest to their skill.
/// Regular NPC smiths are not changed (PT-11: they are the OSI path).
/// </summary>
public static class ClusterFSmithTeaching
{
    public static CraftSystem Smithing => DefBlacksmithy.CraftSystem;

    private static CraftItem Find(Type type) => type == null ? null : Smithing.CraftItems.SearchFor(type);

    private static CraftSkill MainSkill(CraftItem item) =>
        item?.Skills.FirstOrDefault(s => s.SkillToMake == Smithing.MainSkill);

    /// <summary>Making <paramref name="type"/> can raise <paramref name="m"/>'s Blacksmithy: min &lt;= value &lt; max.</summary>
    public static bool Teaches(Mobile m, Type type)
    {
        var skill = MainSkill(Find(type));
        if (skill == null)
        {
            return false;
        }

        var value = m.Skills[Smithing.MainSkill].Value;
        return value >= skill.MinSkill && value < skill.MaxSkill;
    }

    /// <summary>The item's Blacksmithy maximum (where gains stop), or -infinity for an item Blacksmithy cannot make.</summary>
    public static double MaxSkill(Type type) => MainSkill(Find(type))?.MaxSkill ?? double.NegativeInfinity;

    /// <summary>The stock "can make it" test of SmallSmithBOD.CreateRandomFor (SmallSmithBOD.cs:139-157).</summary>
    public static bool CanMake(Mobile m, Type type, bool exceptional)
    {
        var item = Find(type);
        if (item == null)
        {
            return false;
        }

        var chance = item.GetSuccessChance(m, null, Smithing, false, out var allRequiredSkills);
        if (!allRequiredSkills || chance < 0.0)
        {
            return false;
        }

        if (exceptional)
        {
            chance = item.GetExceptionalChance(Smithing, chance, m);
        }

        return chance > 0.0;
    }

    /// <summary>
    /// Of the candidates the smith can make, the ones that teach; if none teaches, the hardest (highest maximum,
    /// ties kept). Candidates the smith cannot make are never returned; with none makeable, the list comes back
    /// as it was (stock behaviour).
    /// </summary>
    public static List<T> PickItems<T>(Mobile m, IEnumerable<T> candidates, Func<T, Type> typeOf, bool exceptional)
    {
        var all = candidates.ToList();
        var makeable = all.Where(c => CanMake(m, typeOf(c), exceptional)).ToList();
        if (makeable.Count == 0)
        {
            return all;
        }

        var teaching = makeable.Where(c => Teaches(m, typeOf(c))).ToList();
        if (teaching.Count > 0)
        {
            return teaching;
        }

        var hardest = makeable.Max(c => MaxSkill(typeOf(c)));
        return makeable.Where(c => MaxSkill(typeOf(c)) == hardest).ToList();
    }

    /// <summary>
    /// For orders of several items (a large deed's entries, a large commission's pieces): the sets the smith can make
    /// every piece of and every piece teaches; if there is none, the makeable sets with the most teaching pieces, then
    /// the highest lowest maximum (ties kept). With no makeable set, every set (stock behaviour).
    /// </summary>
    public static List<T> PickSets<T>(Mobile m, IEnumerable<T> sets, Func<T, Type[]> typesOf, bool exceptional)
    {
        var all = sets.ToList();
        var makeable = all.Where(s => typesOf(s).All(t => CanMake(m, t, exceptional))).ToList();
        if (makeable.Count == 0)
        {
            return all;
        }

        var full = makeable.Where(s => typesOf(s).All(t => Teaches(m, t))).ToList();
        if (full.Count > 0)
        {
            return full;
        }

        static (int, double) Score(Mobile m, Type[] types) =>
            (types.Count(t => Teaches(m, t)), types.Min(MaxSkill));

        var best = makeable.Select(s => Score(m, typesOf(s))).Max();
        return makeable.Where(s => Score(m, typesOf(s)) == best).ToList();
    }

    /// <summary>
    /// A small order's list: stock flips a coin between armor (with materials) and weapons (SmallSmithBOD.cs:76).
    /// When only one list has an item that teaches, that list; when neither does, the list with the hardest item the
    /// smith can make; otherwise the coin.
    /// </summary>
    public static bool UseArmorList(Mobile m, Type[] armor, Type[] weapons)
    {
        var armorTeaches = armor.Any(t => CanMake(m, t, false) && Teaches(m, t));
        var weaponsTeach = weapons.Any(t => CanMake(m, t, false) && Teaches(m, t));

        if (armorTeaches != weaponsTeach)
        {
            return armorTeaches;
        }

        if (!armorTeaches)
        {
            var hardestArmor = armor.Where(t => CanMake(m, t, false)).Select(MaxSkill).DefaultIfEmpty(double.NegativeInfinity).Max();
            var hardestWeapon = weapons.Where(t => CanMake(m, t, false)).Select(MaxSkill).DefaultIfEmpty(double.NegativeInfinity).Max();
            if (hardestArmor != hardestWeapon)
            {
                return hardestArmor > hardestWeapon;
            }
        }

        return Utility.RandomBool();
    }
}
