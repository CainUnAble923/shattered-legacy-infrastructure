using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Commands;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Shattered Legacy - [artificerrank (cc-P145). A player command that says the character's Artificers' Order rank and
/// the imbue properties the account knows, in plain lines a Legion script reads from the journal: the combat script's
/// loot rater (SL_Combat, shard-migration notes/design-combat-script.md section 8.6). On this shard unravelling is the
/// Artificers' table extracting a property into an Essence of that property, which is only worth keeping when the
/// account can imbue the property, and that is what the rank's formula books (and the 140+ path) decide.
///
///   [Artificer] rank 2 of 5 (Artificer), standing 5,200, 31 properties known
///   [Artificer] Basic: Balanced, Bonus Dexterity, ...        (a tier that needs more than 180 chars repeats its header)
///   [Artificer] Refined: ...
///   [Artificer] Full: ...
///   [Artificer] end
///
/// A character that is not a member still gets the free properties at Basic (that is what the account can imbue) and
/// the first line says "not a member". Nothing here changes any state. Test shard first (ArtificerRankCommandVerification).
/// </summary>
public static class ArtificerRankCommand
{
    public const string Prefix = "[Artificer]";
    public const int MaxLine = 180;
    public const string GuildKey = "artificers";
    public const int TopRank = ClusterFGuildSystem.RankTop;

    private static readonly ImbueTier[] TierOrder = [ImbueTier.Basic, ImbueTier.Refined, ImbueTier.Full, ImbueTier.Overcharged];

    public static void Configure()
    {
        CommandSystem.Register("artificerrank", AccessLevel.Player, OnCommand);
    }

    [Usage("artificerrank")]
    [Description("Says your Artificers' Order rank and the imbue properties your account knows (the SL_Combat loot helper reads it).")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        foreach (var line in Lines(pm))
        {
            pm.SendMessage(0x3B2, line);
        }
    }

    /// <summary>The lines the command sends, in order: the rank line, the known properties by tier, then "end".</summary>
    public static List<string> Lines(PlayerMobile pm)
    {
        ClusterFGuildSystem.EnsureRegistered();

        var guild = ClusterFAccountPersistence.GetGuild(pm);
        var standing = guild?.GetReputation(GuildKey) ?? 0;
        var member = guild?.JoinedGuilds.Contains(GuildKey) == true;
        var index = guild != null
            ? ClusterFGuildSystem.GuildRankIndex(GuildKey, guild)
            : ClusterFGuildSystem.GuildRankIndex(GuildKey, standing);
        var rankName = guild != null
            ? ClusterFGuildSystem.GetRankName(GuildKey, guild)
            : ClusterFGuildSystem.GetRankName(GuildKey, standing);

        var data = ClusterFRecipeStore.DataOf(pm);
        var byTier = new Dictionary<ImbueTier, List<string>>();
        var count = 0;
        foreach (var key in ImbueFamilies.AllKeys())
        {
            var tier = ClusterFImbueKnowledge.KnownTier(data, key);
            if (tier < ImbueTier.Basic)
            {
                continue;
            }

            count++;
            if (!byTier.TryGetValue(tier, out var names))
            {
                byTier[tier] = names = [];
            }

            names.Add(key);
        }

        var lines = new List<string>
        {
            $"{Prefix} rank {index} of {TopRank} ({rankName}), standing {standing:N0}, {count} properties known" +
            (member ? "" : "; not a member of the Artificers' Order")
        };

        foreach (var tier in TierOrder)
        {
            if (byTier.TryGetValue(tier, out var names))
            {
                lines.AddRange(Chunk(tier.ToString(), names.OrderBy(n => n, StringComparer.Ordinal)));
            }
        }

        lines.Add($"{Prefix} end");
        return lines;
    }

    /// <summary>
    /// "[Artificer] &lt;tier&gt;: a, b, c" lines of at most MaxLine characters, the header repeated on each; every name once,
    /// in the order given. No names, no lines.
    /// </summary>
    public static List<string> Chunk(string tier, IEnumerable<string> names)
    {
        var header = $"{Prefix} {tier}: ";
        var lines = new List<string>();
        var sb = new StringBuilder();

        foreach (var raw in names)
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (sb.Length > 0 && header.Length + sb.Length + 2 + name.Length > MaxLine)
            {
                lines.Add(header + sb);
                sb.Clear();
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(name);
        }

        if (sb.Length > 0)
        {
            lines.Add(header + sb);
        }

        return lines;
    }
}
