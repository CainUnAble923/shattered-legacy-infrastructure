// cc-P78 Part E (F-43): craft guild promotion. Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
// A craft guild's rank (ClusterFGuildSystem.GuildRankIndex, 0 to 5) needs its standing, as before, and now also the
// guild's craft skill for that rank (RecipeGatingTable.PromotionSkill: 0, 30, 50, 70, 90, 100), and rank 5 a guild
// trial (waived while guilds.trialsWaivedUntilBuilt is true, the default, or for a guild in TrialWaivedGuilds).
//
// How it is kept (CharacterGuildData v3):
//   RankCap[key]       the highest rank the character's craft skill (and trial) has ever allowed. It only rises, so a
//                      loop that resets skills never lowers a rank. The rank is the standing rank held under this cap.
//   PromotedRank[key]  the highest rank whose grants were handed over. Ranks never drop below it.
// A craft guild is tracked from joining (Join calls StartTracking). A record from before v3 is seeded at the rank its
// standing gave, so nobody loses a rank. A guild with no RankCap entry (every non-craft guild, and records built by
// hand) ranks by standing alone, exactly as before cc-P78.
//
// When it is checked (Update): at login, at the guild hall page, opening the Guild Directory, finishing the
// Apprentice task, and whenever the craft menu of a guild's skill draws. Each rank reached hands over its grant:
// rank 1 to 4 the Band I to IV pattern book of each craft skill the guild owns, rank 5 a Band V starter set. A book
// the account already knows is not given again, and the player is told so.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class ClusterFGuildPromotion
{
    // guilds.trialsWaivedUntilBuilt (default true): the rank 5 guild trials do not exist yet.
    public static bool TrialsWaivedUntilBuilt { get; set; } = true;

    public static void Configure()
    {
        TrialsWaivedUntilBuilt = ServerConfiguration.GetOrUpdateSetting("guilds.trialsWaivedUntilBuilt", true);
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (ClusterFAccountPersistence.GetGuild(pm) is { } data)
        {
            foreach (var key in new List<string>(data.JoinedGuilds))
            {
                Update(pm, key);
            }
        }
    }

    public static bool IsTracked(CharacterGuildData data, string key) => data.RankCap.ContainsKey(key);

    /// <summary>The rank index to use: standing alone for an untracked guild, else standing under the cap, never below promoted.</summary>
    public static int Effective(string key, CharacterGuildData data, int standingIndex)
    {
        if (!data.RankCap.TryGetValue(key, out var cap))
        {
            return standingIndex;
        }

        data.PromotedRank.TryGetValue(key, out var promoted);
        return Math.Max(promoted, Math.Min(standingIndex, cap));
    }

    /// <summary>The craft skill promotion reads: the best of the guild's own craft skills (Base, so no item bonus).</summary>
    public static double CraftSkill(Mobile m, string key)
    {
        var best = 0.0;
        foreach (var skill in RecipeOwners.SkillsOf(key))
        {
            best = Math.Max(best, m.Skills[skill].Base);
        }

        return best;
    }

    public static bool TrialDone(CharacterGuildData data, string key) =>
        TrialsWaivedUntilBuilt ||
        Array.Exists(RecipeGatingTable.Current.TrialWaivedGuilds, g => g.Equals(key, StringComparison.OrdinalIgnoreCase)) ||
        data.TrialsPassed.Contains(key);

    /// <summary>The highest rank this skill (and the trial) allows.</summary>
    public static int CapFor(double skill, bool trialDone)
    {
        var need = RecipeGatingTable.Current.PromotionSkill;
        var cap = 0;
        for (var r = 1; r < need.Length && r <= ClusterFGuildSystem.RankTop; r++)
        {
            if (skill >= need[r])
            {
                cap = r;
            }
        }

        return !trialDone && cap >= ClusterFGuildSystem.RankTop ? ClusterFGuildSystem.RankTop - 1 : cap;
    }

    /// <summary>Called by Join: a craft guild starts tracked at rank 0, then is checked at once.</summary>
    public static void StartTracking(PlayerMobile pm, string key)
    {
        if (!RecipeOwners.IsCraftGuild(key))
        {
            return;
        }

        var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        data.RankCap.TryAdd(key, 0);
        data.PromotedRank.TryAdd(key, 0);
        Update(pm, key);
    }

    /// <summary>Before v3: each joined craft guild tracked at the rank its standing gave, so no rank drops.</summary>
    public static void SeedFromStanding(CharacterGuildData data)
    {
        foreach (var key in data.JoinedGuilds)
        {
            if (RecipeOwners.IsCraftGuild(key))
            {
                var index = ClusterFGuildSystem.StandingRankIndex(key, data);
                data.RankCap[key] = index;
                data.PromotedRank[key] = index;
            }
        }
    }

    /// <summary>Checks the guilds that own this craft skill (the craft menu's call).</summary>
    public static void UpdateForSkill(PlayerMobile pm, SkillName skill)
    {
        if (RecipeOwners.GuildFor(skill) is { } key && ClusterFGuildSystem.IsJoined(pm, key))
        {
            Update(pm, key);
        }
    }

    /// <summary>
    /// Raises the cap to what the skill allows now, then hands over each rank's grant from the last promoted rank up to the
    /// rank in force. Returns what it said. Nothing for an untracked guild.
    /// </summary>
    public static List<string> Update(PlayerMobile pm, string key)
    {
        var said = new List<string>();
        var data = ClusterFAccountPersistence.GetGuild(pm);
        if (data == null || !data.JoinedGuilds.Contains(key) || !data.RankCap.TryGetValue(key, out var cap))
        {
            return said;
        }

        data.RankCap[key] = Math.Max(cap, CapFor(CraftSkill(pm, key), TrialDone(data, key)));

        data.PromotedRank.TryGetValue(key, out var promoted);
        var rank = ClusterFGuildSystem.GuildRankIndex(key, data);
        if (rank <= promoted)
        {
            return said;
        }

        var name = ClusterFGuildSystem.GetDef(key)?.Name ?? key;
        for (var r = promoted + 1; r <= rank; r++)
        {
            data.PromotedRank[key] = r;
            var line = $"The {name} promotes you to {RankNameForIndex(key, r)}.";
            pm.SendMessage(0x44, line);
            said.Add(line);
            said.AddRange(Grant(pm, key, r));
        }

        return said;
    }

    /// <summary>What reaching a rank hands over (data: RecipeGatingTable). Returns what it said.</summary>
    public static List<string> Grant(PlayerMobile pm, string key, int rank)
    {
        var said = new List<string>();
        var table = RecipeGatingTable.Current;

        foreach (var skill in RecipeOwners.GatedSkillsOf(key))
        {
            var system = ClusterFRecipeGate.SystemFor(skill)!;

            if (rank >= 1 && rank <= table.LastBookBand)
            {
                var band = ClusterFRecipeGate.MergedBand(system, rank);
                if (!ClusterFRecipeGate.BooksFor(system).Contains(band))
                {
                    continue;
                }

                var bookKey = ClusterFRecipeStore.BookKey(skill, band);
                if (ClusterFRecipeStore.Knows(pm, bookKey))
                {
                    say($"Your account already knows {ClusterFRecipeStore.Describe(bookKey)}; no book this time.");
                    continue;
                }

                Deliver(pm, new PatternBook(skill, band), said);
            }
            else if (rank == ClusterFGuildSystem.RankTop)
            {
                var given = 0;
                foreach (var scrollKey in ClusterFRecipeRewards.BandVKeys(skill))
                {
                    if (given >= table.Rank5StarterScrolls)
                    {
                        break;
                    }

                    if (!ClusterFRecipeStore.Knows(pm, scrollKey))
                    {
                        Deliver(pm, new AccountRecipeScroll(scrollKey), said);
                        given++;
                    }
                }

                if (given == 0)
                {
                    say($"There are no Band V {ClusterFRecipeStore.SkillTitle(skill)} designs your account does not know yet.");
                }
            }
        }

        return said;

        void say(string line)
        {
            pm.SendMessage(0x3B2, line);
            said.Add(line);
        }
    }

    /// <summary>Into the pack, or the bank if the pack is full; and the player is told where.</summary>
    public static void Deliver(PlayerMobile pm, Item item, List<string>? said = null)
    {
        string line;
        if (pm.Backpack?.TryDropItem(pm, item, false) == true)
        {
            line = $"You receive {DescribeItem(item)}.";
        }
        else
        {
            pm.BankBox.DropItem(item);
            line = $"Your pack is full: {DescribeItem(item)} was placed in your bank box.";
        }

        pm.SendMessage(0x44, line);
        said?.Add(line);
    }

    private static string DescribeItem(Item item) => item.Name ?? item.DefaultName ?? item.GetType().Name;

    /// <summary>The guild's own name for a rank index (its first rung at that index, ClusterFGuildSystem.RankThresholds).</summary>
    public static string RankNameForIndex(string key, int index)
    {
        var thresholds = ClusterFGuildSystem.RankThresholds(key);
        for (var rung = 0; rung < thresholds.Length; rung++)
        {
            if (ClusterFGuildSystem.GuildRankIndex(key, thresholds[rung]) >= index)
            {
                return ClusterFGuildSystem.GetRankName(key, thresholds[rung]);
            }
        }

        return ClusterFGuildSystem.GetRankName(key, thresholds[^1]);
    }
}
