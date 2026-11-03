// cc-P760: the reward stores' commands. Players: [apstore opens the Achievement store and [leagueshop the League shop, from
// anywhere; [guildstore opens the Guild Store of your guild whose guildmaster is beside you (the same windows the achievements window and the League Registrar open). Staff, for testing:
// [RewardStores grant <character name> <tokens|ap|guild:<key>> <amount> (a guild grant also joins the character and sets
// its standing to the amount, so its rows open).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Commands;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

public static class RewardStoreCommands
{
    public static void Configure()
    {
        CommandSystem.Register("apstore", AccessLevel.Player, OnApStore);
        CommandSystem.Register("leagueshop", AccessLevel.Player, OnLeagueShop);
        CommandSystem.Register("guildstore", AccessLevel.Player, OnGuildStore);
    }

    [Usage("guildstore")]
    [Description("Opens the Guild Store of a guild you belong to whose guildmaster is beside you (the same store as the guild window's Guild Store button).")]
    [ShardCommand(CommandCategory.Player, Shard = CommandShard.Any)]
    private static void OnGuildStore(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        ClusterFGuildSystem.EnsureRegistered();
        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            if (ClusterFGuildSystem.IsJoined(pm, def.Key) && ClusterFGuildSystem.FindGuildmasterNear(pm, def) is { } gm)
            {
                GuildStores.Open(pm, def.Key, gm);
                return;
            }
        }

        // Say what was found, so a player (or staff) can tell which half is missing.
        var joined = ClusterFAccountPersistence.GetGuild(pm)?.JoinedGuilds.ToList() ?? [];
        var near = ClusterFGuildSystem.AllGuilds.Values.Where(d => ClusterFGuildSystem.FindGuildmasterNear(pm, d) != null)
            .Select(d => d.Name).ToList();
        pm.SendMessage(0x22, "Stand beside the guildmaster of a guild you belong to. " +
            $"Your guilds: {(joined.Count > 0 ? string.Join(", ", joined.Select(k => ClusterFGuildSystem.GetDef(k)?.Name ?? k)) : "none")}. " +
            $"Guildmasters here: {(near.Count > 0 ? string.Join(", ", near) : "none")}.");
    }

    [Usage("apstore")]
    [Description("Opens the Achievement store: spend achievement points on titles, cosmetics and decor. Your AP total never goes down.")]
    [ShardCommand(CommandCategory.Player, Shard = CommandShard.Any)]
    private static void OnApStore(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
        {
            AchievementStoreDef.Open(pm);
        }
    }

    [Usage("leagueshop")]
    [Description("Opens the League shop: AP opens a row, Renown buys it.")]
    [ShardCommand(CommandCategory.Player, Shard = CommandShard.Any)]
    private static void OnLeagueShop(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
        {
            pm.SendGump(new LeagueShopGump(pm));
        }
    }

    public static List<string> Grant(string[] args)
    {
        if (args.Length < 4 || !int.TryParse(args[^1], out var amount))
        {
            return ["Usage: [RewardStores grant <character name> <tokens|ap|guild:<key>> <amount>"];
        }

        var what = args[^2].ToLowerInvariant();
        var name = string.Join(' ', args[1..^2]);
        var pm = World.Mobiles.Values.OfType<PlayerMobile>().FirstOrDefault(m => m.Name == name);
        if (pm?.Account is not IAccount acct)
        {
            return [$"No character called '{name}'."];
        }

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        switch (what)
        {
            case "tokens":
                ClusterFAchievementSystem.AddToCounter(acct.Username, FestivalTokens.EarnedCounter, amount);
                return [$"{name}: +{amount} Festival Tokens ({FestivalTokens.BalanceOf(acct.Username)})."];
            case "ap":
                data.AchievementPoints += amount;
                LeagueTitles.Sync(data);
                return [$"{name}: +{amount} AP ({data.AchievementPoints} total)."];
            case "renown":
                data.Renown += amount;
                return [$"{name}: +{amount} Renown (balance only)."];
        }

        if (what.StartsWith("guild:", StringComparison.Ordinal))
        {
            var key = what[6..];
            if (ClusterFGuildSystem.GetDef(key) == null)
            {
                return [$"No guild '{key}'."];
            }

            var guild = data.GetOrCreateGuildData(pm.Serial);
            guild.JoinedGuilds.Add(key);
            guild.GuildReputation[key] = Math.Max(guild.GetReputation(key), amount);
            guild.AddCurrency(key, amount);
            return [$"{name}: joined {key}, standing {guild.GetReputation(key)}, +{amount} {MuleUpgrades.CurrencyName(key)}."];
        }

        return [$"Unknown: '{what}'. tokens, ap, renown or guild:<key>."];
    }
}
