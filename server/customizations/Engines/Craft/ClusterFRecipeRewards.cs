// cc-P78 Part F (F-43): recipe scrolls from other sources. Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
//   F-20 BODs           a large order turned in has LargeBodScrollChance at a Band V scroll for its craft
//                       (ClusterFSmithBODPayout.Pay calls TryLargeBodScroll; the only BOD payout that exists is the smith's).
//   F-37 commissions    a recurring client may reward a custom recipe: CommissionScrollChance over CommissionScrolls
//                       (empty until custom recipes exist; SmithCommissionSystem.CompleteLarge calls TryCommissionScroll).
//   F-40 boxes, bosses, quests    CreateFromTable(table, source): a scroll from a named RewardTables entry. Nothing calls it
//                       yet (no loot box code has landed); it is the reward type those systems call.
//   F-42 epic recipes   a table marked Epic pays only to Boss and LootBox sources, never BODs, commissions or quests.
//   F-6 Cartography     LearnRegionMap: a region mapped by exploring, stored as map:<region>. Nothing calls it yet.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server;

public enum RecipeRewardSource
{
    LootBox,
    Boss,
    Quest,
    Bod,
    Commission
}

public static class ClusterFRecipeRewards
{
    /// <summary>A skill's Band V scroll keys (the first scroll band), in craft list order.</summary>
    public static List<string> BandVKeys(SkillName skill) => ScrollKeys(skill, 5);

    /// <summary>A skill's scroll keys in one band.</summary>
    public static List<string> ScrollKeys(SkillName skill, int band)
    {
        var keys = new List<string>();
        var system = ClusterFRecipeGate.SystemFor(skill);
        if (system == null)
        {
            return keys;
        }

        foreach (var item in system.CraftItems)
        {
            var c = ClusterFRecipeGate.Classify(system, item);
            if (c.Kind == RecipeKind.Scroll && c.Band == band && !keys.Contains(c.Key!))
            {
                keys.Add(c.Key!);
            }
        }

        return keys;
    }

    public static Item CreateScroll(string key) => new AccountRecipeScroll(key);

    /// <summary>A random Band V scroll for the skill, or null when it has none.</summary>
    public static Item? RandomBandVScroll(SkillName skill)
    {
        var keys = BandVKeys(skill);
        return keys.Count > 0 ? CreateScroll(keys[Utility.Random(keys.Count)]) : null;
    }

    public static bool TryLargeBodScroll(PlayerMobile pm, SkillName skill, double? roll = null)
    {
        if ((roll ?? Utility.RandomDouble()) >= RecipeGatingTable.Current.LargeBodScrollChance)
        {
            return false;
        }

        var scroll = RandomBandVScroll(skill);
        if (scroll == null)
        {
            return false;
        }

        ClusterFGuildPromotion.Deliver(pm, scroll);
        return true;
    }

    public static bool TryCommissionScroll(PlayerMobile pm, double? roll = null)
    {
        var table = RecipeGatingTable.Current;
        if (table.CommissionScrolls.Length == 0 || (roll ?? Utility.RandomDouble()) >= table.CommissionScrollChance)
        {
            return false;
        }

        ClusterFGuildPromotion.Deliver(pm, CreateScroll(table.CommissionScrolls[Utility.Random(table.CommissionScrolls.Length)]));
        return true;
    }

    /// <summary>
    /// A scroll from a named table, or null (no such table, an empty one, or an epic table asked for by a source that may
    /// not pay epic recipes). A table entry is a key, or a pool: "bandV:any" or "bandV:Blacksmith".
    /// </summary>
    public static Item? CreateFromTable(string tableName, RecipeRewardSource source)
    {
        var table = RecipeGatingTable.Current.RewardTable(tableName);
        if (table == null || table.Epic && source is not (RecipeRewardSource.Boss or RecipeRewardSource.LootBox))
        {
            return null;
        }

        var keys = new List<string>();
        foreach (var entry in table.Keys)
        {
            if (entry.StartsWith("bandV:", StringComparison.OrdinalIgnoreCase))
            {
                var which = entry[6..];
                foreach (var system in ClusterFRecipeGate.GatedSystems())
                {
                    if (which.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                        which.Equals(system.MainSkill.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        keys.AddRange(BandVKeys(system.MainSkill));
                    }
                }
            }
            else
            {
                keys.Add(entry);
            }
        }

        return keys.Count > 0 ? CreateScroll(keys[Utility.Random(keys.Count)]) : null;
    }

    /// <summary>The F-6 hook: an explored region's map, learned by the account. True when it was new.</summary>
    public static bool LearnRegionMap(PlayerMobile pm, string region) =>
        ClusterFRecipeStore.Learn(pm, ClusterFRecipeStore.MapKey(region));
}
