using System;
using System.Linq;
using Server.Accounting;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

/// <summary>
/// Shattered Legacy - [GuildStanding, a test-shard command to inspect or set any guild's standing (cc-P29 Part E).
///
///   [GuildStanding &lt;guildKey&gt; [amount]   -- target a player. With an amount, sets that character's
///                                          GuildReputation[guildKey]; without, reports standing and rank.
///
/// The guild key is one of ClusterFGuildSystem's definitions (smithing, artificers, mining, ...); an unknown key
/// is refused before the target cursor, with the valid keys listed. Rank names are each guild's own ladder where
/// it has one. [CompactStanding (mining only, ClusterFCompactAdminTools.cs) is unchanged.
/// </summary>
public static class ClusterFGuildStandingCommand
{
    public static void Configure()
    {
        CommandSystem.Register("GuildStanding", AccessLevel.GameMaster, GuildStanding_OnCommand);
    }

    [Usage("GuildStanding <guildKey> [amount]")]
    [Description("Inspect or set a targeted player's standing (GuildReputation) in one guild. Test shard only.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.TestOnly)]
    private static void GuildStanding_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length < 1)
        {
            from.SendMessage("Usage: [GuildStanding <guildKey> [amount]");
            from.SendMessage($"Guild keys: {ValidKeys()}");
            return;
        }

        if (!TryResolveKey(e.GetString(0), out var key))
        {
            from.SendMessage(UnknownKeyMessage(e.GetString(0)));
            return;
        }

        int? amount = null;
        if (e.Length >= 2)
        {
            if (!int.TryParse(e.GetString(1), out var parsed))
            {
                from.SendMessage($"'{e.GetString(1)}' is not a whole number. Usage: [GuildStanding <guildKey> [amount]");
                return;
            }

            amount = parsed;
        }

        from.BeginTarget(-1, false, TargetFlags.None, (m, targeted) =>
        {
            if (targeted is not PlayerMobile pm)
            {
                m.SendMessage("Target a player.");
                return;
            }

            m.SendMessage(Apply(pm, key, amount));
        });
    }

    // Guild keys are matched without regard to case and returned as ClusterFGuildSystem defines them.
    public static bool TryResolveKey(string text, out string key)
    {
        ClusterFGuildSystem.EnsureRegistered();

        if (!string.IsNullOrWhiteSpace(text) && ClusterFGuildSystem.AllGuilds.TryGetValue(text.Trim(), out var def))
        {
            key = def.Key;
            return true;
        }

        key = null;
        return false;
    }

    public static string UnknownKeyMessage(string text) => $"Unknown guild key '{text}'. Guild keys: {ValidKeys()}";

    public static string ValidKeys()
    {
        ClusterFGuildSystem.EnsureRegistered();
        return string.Join(", ", ClusterFGuildSystem.AllGuilds.Values.Select(d => d.Key).OrderBy(k => k, StringComparer.Ordinal));
    }

    // The guild's own rank ladder. Mining's is the Miners' Compact's, which [CompactStanding also uses; the
    // Directory's GetRankName has no mining case and would show the generic ladder.
    public static string RankName(string key, int standing) =>
        key == "mining"
            ? MinersCompactLiaisonGump.GetRankName(standing)
            : ClusterFGuildSystem.GetRankName(key, standing);

    // Sets (amount given) or reports one guild's standing on one character. Returns the line for the staff member.
    public static string Apply(PlayerMobile pm, string key, int? amount)
    {
        if (pm.Account is not IAccount acct)
        {
            return $"{pm.Name} has no account.";
        }

        var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial);
        var name = ClusterFGuildSystem.AllGuilds[key].Name;

        if (amount.HasValue)
        {
            guild.GuildReputation[key] = amount.Value;
            return $"Set {pm.Name}'s {name} standing to {amount.Value:N0} ({RankName(key, amount.Value)}).";
        }

        var standing = guild.GetReputation(key);
        return $"{pm.Name}: {name} standing={standing:N0}, Rank={RankName(key, standing)}";
    }
}
