// cc-P780 (F-24): League hunts on the requisitions board. Notes in shard-migration notes/cc-P780-daily-league-hunts.md.
// The idea (a daily and a weekly kill goal) is from DragnMaw's DailyQuests (servuo.dev #1539, assessed in cc-P777); none
// of its code, rewards or NPCs is used.
//
//   The roll       Each refresh day (ClusterFRequisitions.DayIndex, the requisitions board's day) a character gets
//                  dailyHunts hunts; each refresh week (WeekIndex: the same hour, on weeklyResetDay) one great hunt. A
//                  hunt names a League Bestiary family (league-hunts.json families), is posted by a guild (one the
//                  character has joined, else any guild that posts requisitions) and is rolled at the requisitions tier
//                  for that guild (ClusterFRequisitions.TierFor). Only families whose lowest bestiary level is within
//                  the tier's maxLevel (great: plus levelBonus) and, with onlyWithSpawner, whose spawner stands in the
//                  world, are offered. Rolled lazily when the board or a kill next reads the record, so an existing
//                  world needs no seeding: the file is written on first start and every character rolls on first read.
//   Counting       A death of a counted creature (not a pet, not a summon; CreatureDeletedEvent, see there) credits the player with the most damage
//                  (pinned's looting rights, a pet's damage going to its master), else the last killer; each member of
//                  that player's party within shareRange of the corpse is credited too. Each credited character's open
//                  hunts of that family count one kill each.
//   Pay            When the last kill lands: the hunt's Renown (LeagueJob: counts toward League rank), the guild's
//                  standing and, for a member, the guild's points (GuildPointEarning.OnRequisition, as a requisition),
//                  and gold into the backpack; the great hunt adds a League shop loot box (great.lootBox, Silver: the
//                  shop's own goods, no magic gear). Nothing grants skill or stat points, power scrolls, bless deeds or
//                  artifacts. A finished hunt stays on the board, marked done, until its refresh.
//   Quiet expiry   An unfinished hunt simply rolls over. No penalty of any kind.
//   HuntFamily     ClusterFRequisitions.HuntFamily is filled: a slayer requisition row marked "any" asks for the slayer
//                  family of an open hunt (the family's slayer in the file, else the pinned slayer group that slays its
//                  creature), when there is one.
//   Commands       [hunts (player): the hunts as "[Hunt]" lines. [LeagueHuntsStaff reload|reroll|show (GM).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Engines.LeagueRewards;
using Server.Engines.PartySystem;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class LeagueHunts
{
    public const string Prefix = "[Hunt]";

    /// <summary>Whether a family can be found in the world now; a fact swaps it.</summary>
    public static Func<LeagueHuntFamily, bool> HasHome { get; set; } = SpawnerStands;

    // After the requisitions table (110), which the tier and the guild list read.
    [CallPriority(111)]
    public static void Configure()
    {
        foreach (var line in LeagueHuntTable.LoadOrWriteDefaults(LeagueHuntTable.FilePath))
            Console.WriteLine($"[LeagueHunts] {line}");

        ClusterFRequisitions.HuntFamily = HuntFamilyFor;

        CommandSystem.Register("hunts", AccessLevel.Player, OnPlayerCommand);
        CommandSystem.Register("LeagueHuntsStaff", AccessLevel.GameMaster, OnStaffCommand);
    }

    // -- Weeks ------------------------------------------------------------------------------------------

    /// <summary>The hunt week of a moment: refresh days counted in sevens, each week starting on <paramref name="resetDay"/>.</summary>
    public static int WeekIndex(DateTime utcNow, int refreshHourUtc, DayOfWeek resetDay)
    {
        var day = ClusterFRequisitions.DayIndex(utcNow, refreshHourUtc);
        var offset = ((int)ClusterFRequisitions.Epoch.DayOfWeek - (int)resetDay + 7) % 7;
        return (int)Math.Floor((day + offset) / 7.0);
    }

    public static int ThisWeek(DateTime now) =>
        WeekIndex(now, RequisitionTable.Current.RefreshHourUtc, LeagueHuntTable.Current.ResetDay);

    /// <summary>When the great hunt next rolls over.</summary>
    public static DateTime NextWeek(DateTime now)
    {
        var hour = RequisitionTable.Current.RefreshHourUtc;
        var week = ThisWeek(now);
        var t = ClusterFRequisitions.Epoch.AddDays(ClusterFRequisitions.DayIndex(now, hour) + 1).AddHours(hour);
        while (WeekIndex(t, hour, LeagueHuntTable.Current.ResetDay) == week)
            t = t.AddDays(1);
        return t;
    }

    // -- The roll ---------------------------------------------------------------------------------------

    /// <summary>
    /// Rolls the daily hunts when the refresh day has turned and the great hunt when the week has (or both, when
    /// <paramref name="force"/>). Returns whether anything was rolled.
    /// </summary>
    public static bool Refresh(PlayerMobile pm, CharacterRequisitionData data, DateTime now, bool force)
    {
        var table = LeagueHuntTable.Current;
        if (pm == null || data == null || !table.Enabled)
            return false;

        var day = ClusterFRequisitions.DayIndex(now, RequisitionTable.Current.RefreshHourUtc);
        var week = ThisWeek(now);
        var rolled = false;

        if (force || data.HuntDay != day)
        {
            data.Hunts.Clear();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < table.DailyHunts; i++)
            {
                var h = Roll(pm, false, now, used);
                if (h == null)
                    break;
                used.Add(h.FamilyKey);
                data.Hunts.Add(h);
            }

            data.HuntDay = day;
            rolled = true;
        }

        if (force || data.GreatHuntWeek != week)
        {
            data.GreatHunt = Roll(pm, true, now, null);
            data.GreatHuntWeek = week;
            rolled = true;
        }

        return rolled;
    }

    /// <summary>The character's hunts, rolled first if the day or week has turned; null without an account.</summary>
    public static CharacterRequisitionData EnsureCurrent(PlayerMobile pm, bool create = true)
    {
        var data = ClusterFRequisitions.DataOf(pm, create);
        if (data != null)
            Refresh(pm, data, ClusterFRequisitions.Clock(), false);
        return data;
    }

    /// <summary>The guilds a hunt can be posted by for this character: those it has joined that post requisitions, else all that do.</summary>
    public static List<string> GuildsFor(PlayerMobile pm)
    {
        ClusterFGuildSystem.EnsureRegistered();
        var req = RequisitionTable.Current;
        var posting = ClusterFGuildSystem.AllGuilds.Values.Where(d => req.ThemesFor(d.Key).Count > 0).Select(d => d.Key).ToList();
        var joined = ClusterFAccountPersistence.GetGuild(pm)?.JoinedGuilds;
        var mine = joined == null ? new List<string>() : posting.Where(k => joined.Contains(k)).ToList();
        return mine.Count > 0 ? mine : posting;
    }

    /// <summary>The families open to a tier: within its level reach (great: plus the bonus) and with a home in the world.</summary>
    public static List<LeagueHuntFamily> FamiliesFor(int tier, bool great)
    {
        var table = LeagueHuntTable.Current;
        var reach = table.TierFor(tier).MaxLevel + (great ? Math.Max(0, table.Great.LevelBonus) : 0);
        return table.Families.Where(f => f.Entries.Count > 0 && f.LevelFrom <= reach && HasHome(f)).ToList();
    }

    private static HuntEntry Roll(PlayerMobile pm, bool great, DateTime now, HashSet<string> exclude)
    {
        var guilds = GuildsFor(pm);
        if (guilds.Count == 0)
            return null;

        var guildKey = guilds[Utility.Random(guilds.Count)];
        var tier = ClusterFRequisitions.TierFor(pm, guildKey);
        var families = FamiliesFor(tier, great);
        if (exclude != null && families.Count > exclude.Count)
            families = families.Where(f => !exclude.Contains(f.Key)).ToList();
        if (families.Count == 0)
            return null;

        return Make(families[Utility.Random(families.Count)], guildKey, tier, great, now);
    }

    /// <summary>A hunt of a family for a guild at a tier, with the table's count and pay fixed now.</summary>
    public static HuntEntry Make(LeagueHuntFamily family, string guildKey, int tier, bool great, DateTime now, LeagueHuntTable table = null)
    {
        table ??= LeagueHuntTable.Current;
        var t = table.TierFor(tier);
        var count = great ? table.Great.CountMultiplier : 1;
        var pay = great ? table.Great.PayMultiplier : 1;
        return new HuntEntry
        {
            FamilyKey = family.Key,
            GuildKey = guildKey,
            Tier = tier,
            Great = great,
            Needed = Math.Max(1, table.CountFor(family) * count),
            Renown = t.Renown * pay,
            Reputation = t.Reputation * pay,
            Gold = Math.Min(60000, t.Gold * pay),
            LootBox = great && table.GreatBox is { } box ? box.ToString() : "",
            RolledAt = now,
        };
    }

    /// <summary>A family has a home while one of its bestiary spawner rows has its spawner standing on Trammel.</summary>
    public static bool SpawnerStands(LeagueHuntFamily family)
    {
        if (!LeagueHuntTable.Current.OnlyWithSpawner)
            return true;

        var map = Map.Trammel;
        if (map == null)
            return false;

        foreach (var row in ClusterFLeagueBestiary.Spawners)
            if (row.Types.Any(n => family.Entries.Any(e => e.Type.Name == n)) && ClusterFLeagueBestiary.FindSpawner(map, row) != null)
                return true;

        return false;
    }

    // -- Kills ------------------------------------------------------------------------------------------

    /// <summary>
    /// Who a death counts for: the player with the most damage (pinned's looting rights: a pet's damage is its master's),
    /// else the last killer as the bestiary reads it; then each member of that player's party within shareRange of the
    /// corpse. Empty for a pet or a summon.
    /// </summary>
    public static List<PlayerMobile> Credited(BaseCreature bc)
    {
        var list = new List<PlayerMobile>();
        if (bc == null || bc.Controlled || bc.Summoned)
            return list;

        PlayerMobile top = null;
        var best = 0;
        foreach (var ds in BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax))
        {
            if (ds.m_Mobile is PlayerMobile p && ds.m_Damage > best)
            {
                best = ds.m_Damage;
                top = p;
            }
        }

        top ??= ClusterFLeagueBestiary.KillerOf(bc) as PlayerMobile;
        if (top == null)
            return list;

        list.Add(top);

        var map = bc.Map;
        var location = bc.Location;
        if (bc.Corpse is { Deleted: false } corpse)
        {
            map = corpse.Map;
            location = corpse.Location;
        }

        if (Party.Get(top) is { } party && map != null && map != Map.Internal)
        {
            var range = LeagueHuntTable.Current.ShareRange;
            foreach (var info in party.Members)
            {
                if (info.Mobile is PlayerMobile member && member != top && !member.Deleted && member.Alive &&
                    member.Map == map && member.InRange(location, range) && !list.Contains(member))
                {
                    list.Add(member);
                }
            }
        }

        return list;
    }

    // Which event: CreatureDeletedEvent, as the loot boxes' boss kills (ClusterFLootBoxes.OnCreatureDeleted, cc-P455).
    // Pinned deletes a dead non-bonded creature inside Mobile.OnDeath, before CreatureDeathEvent, and BaseCreature.OnDelete
    // clears the master after raising CreatureDeletedEvent (BaseCreature.cs:3672-3688): only here is a dead pet still a
    // pet (the first run of this fact, on the death event, counted a pet's death). A creature deleted with hits left is
    // not dying (Mobile.Kill sets Hits to 0); a bonded pet's death deletes nothing.
    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc)
    {
        if (bc.Hits > 0 || bc.IsDeadPet || bc.Controlled || bc.Summoned)
            return;

        if (!LeagueHuntTable.Current.Enabled || LeagueHuntTable.Current.Families.All(f => !f.Counts(bc.GetType())))
            return;

        foreach (var pm in Credited(bc))
            CountKill(pm, bc.GetType());
    }

    /// <summary>A counted kill of this type for this character: every open hunt of its family goes up by one. Returns how many did.</summary>
    public static int CountKill(PlayerMobile pm, Type type)
    {
        var data = EnsureCurrent(pm, false);
        if (data == null)
            return 0;

        var table = LeagueHuntTable.Current;
        var counted = 0;
        foreach (var h in data.Hunts.Append(data.GreatHunt))
        {
            if (h == null || h.Done || table.Family(h.FamilyKey) is not { } family || !family.Counts(type))
                continue;

            h.Kills++;
            counted++;
            if (h.Done)
                Pay(pm, h);
            else
                pm.SendMessage(0x3B2, $"League hunt: {h.Kills} of {h.Needed} {family.Name}.");
        }

        return counted;
    }

    /// <summary>Pays a finished hunt once: Renown, the guild's standing and points, gold, and the great hunt's box.</summary>
    public static bool Pay(PlayerMobile pm, HuntEntry h)
    {
        if (h.Paid || !h.Done || pm?.Account is not IAccount acct)
            return false;

        h.Paid = true;
        LeagueSashSystem.Earn(ClusterFAccountPersistence.GetOrCreate(acct), pm, h.Renown, RenownSource.LeagueJob); // cc-P800: the worn sash's bonus
        ClusterFAccountPersistence.GetOrCreateGuild(pm).AddReputation(h.GuildKey, h.Reputation);
        var guildPoints = Server.Engines.RewardStores.GuildPointEarning.OnRequisition(pm, h.GuildKey, h.Reputation);
        ClusterFRequisitions.GiveGold(pm, h.Gold);

        var boxText = "";
        if (!string.IsNullOrEmpty(h.LootBox) && Enum.TryParse<LeagueShop.BoxTier>(h.LootBox, true, out var tier))
        {
            var box = LeagueShop.MakeBox(pm, tier);
            ClusterFGuildPromotion.Deliver(pm, box);
            boxText = $" and a {box.DefaultName}";
        }

        var guildName = ClusterFGuildSystem.GetDef(h.GuildKey)?.Name ?? h.GuildKey;
        pm.SendMessage(0x44, $"{(h.Great ? "Great hunt" : "League hunt")} done: {Describe(h)}. Paid {h.Renown} Renown, " +
                             $"{h.Reputation} {guildName} standing" +
                             (guildPoints > 0 ? $", {guildPoints} {MuleUpgrades.CurrencyName(h.GuildKey)}" : "") +
                             $", {h.Gold} gold{boxText}.");
        return true;
    }

    // -- The requisitions seam --------------------------------------------------------------------------

    /// <summary>
    /// ClusterFRequisitions.HuntFamily: the slayer family of one of the character's open hunts (that guild's first), or
    /// null when none of them has one.
    /// </summary>
    public static SlayerName? HuntFamilyFor(PlayerMobile pm, string guildKey)
    {
        var data = ClusterFRequisitions.DataOf(pm, false);
        if (data == null || !LeagueHuntTable.Current.Enabled)
            return null;

        var open = data.Hunts.Append(data.GreatHunt).Where(h => h is { Done: false })
            .OrderByDescending(h => h.GuildKey.Equals(guildKey ?? "", StringComparison.OrdinalIgnoreCase));
        foreach (var h in open)
            if (SlayerOf(LeagueHuntTable.Current.Family(h.FamilyKey)) is { } s)
                return s;

        return null;
    }

    /// <summary>The family's slayer: the file's, else the first pinned slayer entry whose types cover one of its creatures.</summary>
    public static SlayerName? SlayerOf(LeagueHuntFamily family)
    {
        if (family == null)
            return null;

        if (!string.IsNullOrEmpty(family.Slayer) && Enum.TryParse<SlayerName>(family.Slayer, true, out var named) && named != SlayerName.None)
            return named;

        foreach (var group in SlayerGroup.Groups)
            foreach (var entry in group.Entries.Append(group.Super))
                if (entry != null && family.Entries.Any(e => entry.Types.Any(t => t.IsAssignableFrom(e.Type))))
                    return entry.Name;

        return null;
    }

    // -- Words ------------------------------------------------------------------------------------------

    public static string Describe(HuntEntry h)
    {
        var family = LeagueHuntTable.Current.Family(h.FamilyKey);
        var name = family?.Name ?? h.FamilyKey;
        return (family?.Use ?? "cull").ToLowerInvariant() switch
        {
            "boss" => h.Needed == 1 ? $"slay {name}" : $"slay {name} {h.Needed} times",
            "hunt" => $"hunt down {h.Needed} {name}",
            _      => $"cull {h.Needed} {name}",
        };
    }

    public static string HomeOf(HuntEntry h) =>
        LeagueHuntTable.Current.Family(h.FamilyKey)?.Entries.FirstOrDefault()?.Home ?? "";

    public static string Progress(HuntEntry h) => h.Done ? "done" : $"{h.Kills} of {h.Needed}";

    public static string PayText(HuntEntry h)
    {
        var guildName = ClusterFGuildSystem.GetDef(h.GuildKey)?.Name ?? h.GuildKey;
        var box = string.IsNullOrEmpty(h.LootBox) ? "" : $" and a System's {h.LootBox} Loot Box";
        return $"{h.Renown} Renown, {h.Reputation} {guildName} standing, {h.Gold} gold{box}";
    }

    /// <summary>The lines [hunts says: a header, one line per hunt (the great hunt last), then "end".</summary>
    public static List<string> Lines(PlayerMobile pm)
    {
        var data = EnsureCurrent(pm);
        var lines = new List<string>();
        var hunts = data == null ? new List<HuntEntry>() : data.Hunts.Append(data.GreatHunt).Where(h => h != null).ToList();
        lines.Add($"{Prefix} {hunts.Count} hunts; day {data?.HuntDay ?? -1}; week {data?.GreatHuntWeek ?? -1}");
        foreach (var h in hunts)
            lines.Add($"{Prefix} {(h.Great ? "great" : "daily")}|{h.FamilyKey}|{h.GuildKey}|{h.Tier}|{h.Kills}|{h.Needed}|{h.Renown}|{h.Reputation}|{h.Gold}|{(h.LootBox == "" ? "-" : h.LootBox)}|{(h.Paid ? "paid" : "open")}");
        lines.Add($"{Prefix} end");
        return lines;
    }

    // -- Commands ---------------------------------------------------------------------------------------

    [Usage("hunts")]
    [Description("Says your League hunts (today's and this week's great hunt) as [Hunt] lines.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnPlayerCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
            return;

        foreach (var line in Lines(pm))
            pm.SendMessage(0x3B2, line);
    }

    [Usage("LeagueHuntsStaff [reload|reroll|show]")]
    [Description("reload reads Configuration/league-hunts.json again (a bad file is refused whole); reroll rolls your own hunts again now; show lists them.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnStaffCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "show";
        switch (mode)
        {
            case "reload":
                foreach (var line in LeagueHuntTable.Reload(LeagueHuntTable.FilePath))
                    e.Mobile.SendMessage(line);
                return;

            case "reroll" when e.Mobile is PlayerMobile pm:
            {
                var data = ClusterFRequisitions.DataOf(pm, true);
                Refresh(pm, data, ClusterFRequisitions.Clock(), true);
                e.Mobile.SendMessage($"Rolled {data.Hunts.Count} hunts and {(data.GreatHunt == null ? "no" : "a")} great hunt.");
                return;
            }

            case "show" when e.Mobile is PlayerMobile pm:
                foreach (var line in Lines(pm))
                    e.Mobile.SendMessage(line);
                return;

            default:
                e.Mobile.SendMessage("Usage: [LeagueHuntsStaff [reload|reroll|show]");
                return;
        }
    }
}
