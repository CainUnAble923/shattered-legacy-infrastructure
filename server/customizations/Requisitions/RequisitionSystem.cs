// cc-P150 (F-47): the requisitions board's rules. Notes in shard-migration notes/cc-P150-requisitions-and-leads.md.
//
//   The roll     Refresh: once per refresh day (DayIndex: UTC days counted from RefreshHourUtc, so the set rolls over at
//                a fixed server time), lazily when the board is next read. Every open request without a live Lead is
//                replaced; a request with a live Lead is kept (F-48: a Lead always pays off). Each guild with theme rows
//                gets PerGuild requests at the character's tier: min(rank index in that guild, the League rank's gate).
//                A character outside the guild rolls at tier 0.
//   Turn-in      TurnIn: the item must be in the backpack and fill the request (RequisitionMatch); it is consumed, and
//                the character is paid the tier's Renown (GrantRenown, LeagueJob: play-earned, counts toward League
//                rank), the guild's standing (CharacterGuildData.AddReputation, paid to a non-member too: nothing banks
//                for non-members yet, cc-P47 4.2), and gold equal to the Appraiser's price (AppraiserPricing). The slot
//                stays empty until the next refresh.
//   Quiet expiry An unfilled request simply rolls over: no Renown loss, no standing loss, no Infamy (which does not
//                exist in code: nothing here writes anything but the new set).
//   Hunts        HuntFamily is the seam for League hunts and culls (F-24, not built): a slayer row marked "any" asks it
//                for the family first.
//   Commands     [requisitions (player): the open requests as "[Requisition]" lines, for the combat script's loot rater
//                (cc-P145, section 4 of the prompt). [RequisitionsStaff reload|reroll|show (GM).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class ClusterFRequisitions
{
    public const string Prefix = "[Requisition]";

    /// <summary>Days are counted from here; the number itself means nothing, it only has to be fixed.</summary>
    public static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The roll used for Lead chances and Lead drops; a fact swaps it for a fixed one.</summary>
    public static Func<double> Chance { get; set; } = Utility.RandomDouble;

    /// <summary>Now, as the board reads it; a fact swaps it to move a day or three.</summary>
    public static Func<DateTime> Clock { get; set; } = () => Core.Now;

    /// <summary>
    /// The seam for League hunts and culls (F-24): given the character and the guild, the slayer family an active hunt
    /// wants, or null. A slayer theme row marked "any" asks here before drawing from the table's families.
    /// </summary>
    public static Func<PlayerMobile, string, SlayerName?> HuntFamily { get; set; }

    // cc-P95's rule: after the guilds and the Artificers' catalogue (ClusterFImbueKnowledge is 100), which the table's
    // validation reads.
    [CallPriority(110)]
    public static void Configure()
    {
        foreach (var line in RequisitionTable.LoadOrWriteDefaults(RequisitionTable.FilePath))
            Console.WriteLine($"[Requisitions] {line}");

        CommandSystem.Register("requisitions", AccessLevel.Player, OnPlayerCommand);
        CommandSystem.Register("RequisitionsStaff", AccessLevel.GameMaster, OnStaffCommand);
    }

    // -- Days -------------------------------------------------------------------------------------------

    /// <summary>The refresh day of a moment: UTC days since Epoch, each starting at <paramref name="refreshHourUtc"/>.</summary>
    public static int DayIndex(DateTime utcNow, int refreshHourUtc) =>
        (int)Math.Floor((utcNow.AddHours(-refreshHourUtc) - Epoch).TotalDays);

    public static int TodayIndex() => DayIndex(Clock(), RequisitionTable.Current.RefreshHourUtc);

    /// <summary>When the open set next rolls over.</summary>
    public static DateTime NextRefresh()
    {
        var hour = RequisitionTable.Current.RefreshHourUtc;
        var day = TodayIndex() + 1;
        return Epoch.AddDays(day).AddHours(hour);
    }

    // -- Data -------------------------------------------------------------------------------------------

    public static CharacterRequisitionData DataOf(PlayerMobile pm, bool create)
    {
        if (pm?.Account is not IAccount acct) return null;
        return create
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateRequisitionData(pm.Serial)
            : ClusterFAccountPersistence.Get(acct)?.GetRequisitionData(pm.Serial);
    }

    /// <summary>This character's open requests, rolled or refreshed first if the day has turned.</summary>
    public static CharacterRequisitionData EnsureCurrent(PlayerMobile pm)
    {
        var data = DataOf(pm, true);
        if (data == null) return null;
        Refresh(pm, data, Clock(), false);
        return data;
    }

    public static List<RequisitionEntry> Open(PlayerMobile pm, string guildKey = null)
    {
        var data = EnsureCurrent(pm);
        if (data == null) return new List<RequisitionEntry>();
        return guildKey == null
            ? data.Requests.ToList()
            : data.Requests.Where(e => e.GuildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // -- Tiers ------------------------------------------------------------------------------------------

    /// <summary>The tier this character rolls in a guild: its rank index there (0 outside the guild), capped by the League gate.</summary>
    public static int TierFor(PlayerMobile pm, string guildKey, CharacterGuildData guild = null)
    {
        guild ??= ClusterFAccountPersistence.GetGuild(pm);
        var gate = RequisitionTable.Current.GateFor(ClusterFLeagueRanks.GetRank(pm));
        var rank = guild != null && guild.JoinedGuilds.Contains(guildKey) ? ClusterFGuildSystem.GuildRankIndex(guildKey, guild) : 0;
        return Math.Clamp(Math.Min(rank, gate), 0, 5);
    }

    // -- The roll ---------------------------------------------------------------------------------------

    /// <summary>
    /// Rolls or refreshes the set for <paramref name="now"/>'s refresh day. Nothing happens when the day is the one the
    /// set was rolled on, unless <paramref name="force"/>. A request with a live Lead is kept; every other one goes.
    /// </summary>
    public static void Refresh(PlayerMobile pm, CharacterRequisitionData data, DateTime now, bool force)
    {
        var table = RequisitionTable.Current;
        var day = DayIndex(now, table.RefreshHourUtc);
        if (!force && data.RolledDay == day)
            return;

        ClusterFGuildSystem.EnsureRegistered();
        var guild = ClusterFAccountPersistence.GetGuild(pm);
        var leagueRank = ClusterFLeagueRanks.GetRank(pm);

        data.Requests.RemoveAll(e => force || !e.LeadLive(now));

        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            var themes = table.ThemesFor(def.Key);
            if (themes.Count == 0)
                continue;

            var tier = TierFor(pm, def.Key, guild);
            var guard = 0;
            while (data.CountFor(def.Key) < table.PerGuild && guard++ < 20)
                data.Requests.Add(Make(pm, def.Key, PickTheme(themes), tier, leagueRank, now, table));
        }

        data.RolledDay = day;
    }

    private static RequisitionThemeRow PickTheme(List<RequisitionThemeRow> themes)
    {
        var total = themes.Sum(t => Math.Max(1, t.Weight));
        var roll = Utility.Random(total);
        foreach (var t in themes)
        {
            roll -= Math.Max(1, t.Weight);
            if (roll < 0) return t;
        }

        return themes[^1];
    }

    public static RequisitionEntry Make(PlayerMobile pm, string guildKey, RequisitionThemeRow row, int tier, int leagueRank, DateTime now, RequisitionTable table = null)
    {
        table ??= RequisitionTable.Current;
        var t = table.TierFor(tier);
        var e = new RequisitionEntry
        {
            GuildKey = guildKey,
            Category = row.Category,
            Tier = tier,
            Renown = t.Renown,
            Reputation = t.Reputation,
            MinIntensity = t.MinIntensity,
            MinProperties = Math.Max(1, t.MinProperties),
            RolledAt = now,
        };

        switch (row.Kind)
        {
            case RequisitionKind.Slayer:
                e.Kind = RequisitionKind.Slayer;
                e.Slayer = ResolveFamily(pm, guildKey, row.Slayer, table);
                e.MinProperties = 1;
                break;
            case RequisitionKind.AnyMagic:
                e.Kind = RequisitionKind.AnyMagic;
                break;
            default:
                e.Kind = RequisitionKind.Property;
                e.Property = row.Property;
                break;
        }

        RequisitionLeads.TryRoll(pm, e, leagueRank, now);
        return e;
    }

    private static SlayerName ResolveFamily(PlayerMobile pm, string guildKey, string wanted, RequisitionTable table)
    {
        if (!wanted.Equals("any", StringComparison.OrdinalIgnoreCase))
            return Enum.TryParse<SlayerName>(wanted, true, out var named) ? named : SlayerName.Silver;

        if (HuntFamily?.Invoke(pm, guildKey) is { } hunt && hunt != SlayerName.None)
            return hunt;

        var families = table.SlayerFamilyNames();
        return families.Count == 0 ? SlayerName.Silver : families[Utility.Random(families.Count)];
    }

    // -- Turn-in ----------------------------------------------------------------------------------------

    /// <summary>
    /// Fills a request with an item from the character's backpack: consumes the item, pays Renown, the guild's standing
    /// and the Appraiser's price in gold, and takes the request down until the next refresh.
    /// </summary>
    public static bool TurnIn(PlayerMobile pm, RequisitionEntry e, Item item, out string message)
    {
        if (pm?.Account is not IAccount acct)
        {
            message = "No account.";
            return false;
        }

        var data = EnsureCurrent(pm);
        if (data == null || e == null || data.Find(e.Id) == null)
        {
            message = "That request has been taken down; the board has refreshed.";
            return false;
        }

        if (item == null || item.Deleted || pm.Backpack == null || !item.IsChildOf(pm.Backpack))
        {
            message = "The item must be in your backpack.";
            return false;
        }

        if (!RequisitionMatch.Check(e, item, out var why))
        {
            message = why;
            return false;
        }

        var gold = AppraiserPricing.Price(item);
        var guildName = ClusterFGuildSystem.GetDef(e.GuildKey)?.Name ?? e.GuildKey;

        ClusterFAccountPersistence.GetOrCreate(acct).GrantRenown(e.Renown, RenownSource.LeagueJob);
        ClusterFAccountPersistence.GetOrCreateGuild(pm).AddReputation(e.GuildKey, e.Reputation);
        GiveGold(pm, gold);

        data.Requests.Remove(e);
        item.Delete();

        message = $"Requisition filled: {Describe(e)}. Paid {e.Renown} Renown, {e.Reputation} {guildName} standing and {gold} gold.";
        return true;
    }

    /// <summary>Gold into the backpack, in piles a pile can hold, as pinned's shopkeepers pay (BaseVendor.OnSellItems).</summary>
    public static void GiveGold(Mobile m, int amount)
    {
        while (amount > 60000)
        {
            m.AddToBackpack(new Gold(60000));
            amount -= 60000;
        }

        if (amount > 0)
            m.AddToBackpack(new Gold(amount));
    }

    /// <summary>The first open request this item would fill, or null. The server side of the loot rater's "Requisition" tier.</summary>
    public static RequisitionEntry FirstMatch(PlayerMobile pm, Item item)
    {
        var data = EnsureCurrent(pm);
        if (data == null) return null;
        foreach (var e in data.Requests)
            if (RequisitionMatch.Check(e, item, out _))
                return e;
        return null;
    }

    // -- Words ------------------------------------------------------------------------------------------

    public static string Describe(RequisitionEntry e)
    {
        var cat = RequisitionCategories.Get(e.Category);
        var what = cat?.Singular ?? "an item";
        switch (e.Kind)
        {
            case RequisitionKind.Slayer:
                return $"{what} that slays {RequisitionLeads.FamilyNoun(e.Slayer)}";
            case RequisitionKind.AnyMagic:
                return $"{what}, magic, with {e.MinProperties} {(e.MinProperties == 1 ? "property" : "properties")} or more";
            default:
                var more = e.MinProperties > 1 ? $", {e.MinProperties} properties or more" : "";
                return $"{what} with {e.Property} at {e.MinIntensity}% or better{more}";
        }
    }

    public static string PayText(RequisitionEntry e)
    {
        var guildName = ClusterFGuildSystem.GetDef(e.GuildKey)?.Name ?? e.GuildKey;
        return $"{e.Renown} Renown, {e.Reputation} {guildName} standing, and gold at the Appraiser's price";
    }

    public static string TierName(int tier) => tier switch
    {
        <= 0 => "Initiate",
        1    => "Apprentice",
        2    => "Journeyman",
        3    => "Master",
        4    => "Grandmaster",
        _    => "top rank",
    };

    /// <summary>The lines [requisitions says: a header, one line per open request, then "end".</summary>
    public static List<string> Lines(PlayerMobile pm)
    {
        var data = EnsureCurrent(pm);
        var lines = new List<string>();
        var table = RequisitionTable.Current;
        var count = data?.Requests.Count ?? 0;
        lines.Add($"{Prefix} {count} open; day {data?.RolledDay ?? -1}; refresh {table.RefreshHourUtc:00}:00 UTC");

        if (data != null)
        {
            var now = Clock();
            foreach (var e in data.Requests)
            {
                var what = e.Kind switch
                {
                    RequisitionKind.Slayer => $"slayer:{e.Slayer}",
                    RequisitionKind.AnyMagic => "-",
                    _ => e.Property,
                };
                var lead = e.LeadLive(now) ? $"{e.LeadArea}:{e.LeadGroup}" : "-";
                lines.Add($"{Prefix} {e.GuildKey}|{e.Kind}|{e.Category}|{what}|{e.MinIntensity}|{e.MinProperties}|{e.Tier}|{e.Renown}|{e.Reputation}|{lead}");
            }
        }

        lines.Add($"{Prefix} end");
        return lines;
    }

    // -- Commands ---------------------------------------------------------------------------------------

    [Usage("requisitions")]
    [Description("Says your open requisitions as [Requisition] lines (the SL_Combat loot rater reads them).")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnPlayerCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
            return;

        foreach (var line in Lines(pm))
            pm.SendMessage(0x3B2, line);
    }

    [Usage("RequisitionsStaff [reload|reroll|show]")]
    [Description("reload reads Configuration/requisitions.json again (a bad file is refused whole); reroll rolls your own board again now; show lists your open requests.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnStaffCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "show";
        switch (mode)
        {
            case "reload":
                foreach (var line in RequisitionTable.Reload(RequisitionTable.FilePath))
                    e.Mobile.SendMessage(line);
                return;

            case "reroll" when e.Mobile is PlayerMobile pm:
            {
                var data = DataOf(pm, true);
                Refresh(pm, data, Clock(), true);
                e.Mobile.SendMessage($"Rolled {data.Requests.Count} requests again.");
                return;
            }

            case "show" when e.Mobile is PlayerMobile pm:
                foreach (var line in Lines(pm))
                    e.Mobile.SendMessage(line);
                return;

            default:
                e.Mobile.SendMessage("Usage: [RequisitionsStaff [reload|reroll|show]");
                return;
        }
    }
}
