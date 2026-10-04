// ClusterFLeagueRanks.cs
//
// cc-P48 (F-24, League batch 1): the League's 17-rank metal ladder, Iron Citizen to Celestial Citizen, and promotion.
// Notes in shard-migration notes/cc-P48-league-batch-1.md; research in notes/cc-P47-league-research.md section 2.
//
// THE LADDER is generated, never written out: one rank per CraftResource metal, in enum order (ResourceInfo.cs), named
// "<Metal> Citizen" and hued with the metal's hue. A metal whose enum name is in LadderExcluded is skipped, so a new metal
// adds a rank only when it is not excluded. Dawnstone (cc-P36, a 45-skill region ore appended after Celestial) is
// excluded by name, which works whether or not P36 has added it to the enum yet.
//
// STORAGE (Chase, 2026-10-03): rank is per character (CharacterLeagueData in ClusterFAccountData v16); registration
// stays per account (the "league.joined" flag), so a registered account's characters are all at least Iron. Lifetime
// Renown is per account and only rises (ClusterFAccountData.GrantRenown).
//
// PROMOTION (decided): to rise one rank a character needs all four of
//   1. lifetime Renown at or above the next rank's threshold;
//   2. a rank (ClusterFGuildSystem.GuildRankIndex) at or above the next rank's guild rank, in at least N joined guilds;
//   3. the next rank's promotion job (batch 2 sets PromotionDone), waived while league.promotionJobsWaivedUntilBuilt;
//   4. at a milestone rank, that chapter's story trial (TrialsDone), waived while
//      league.trialsWaivedUntilBuilt.chapterN is true. Each chapter's waiver is its own setting.
// The numbers are placeholders in one table (LeagueLadderTable): Configuration/league-ladder.json, written with the
// defaults on first start if missing, read at start and by [LeagueLadder reload. A bad file is refused whole, every
// error is printed, and the table in force is kept.
//
// MILESTONES are the ranks whose row names a trial chapter (six: Dull Copper, Bronze, Verite, Blaze, Mythril and
// Celestial by default). Bonuses step only there (Chase, 2026-10-03): LeagueMilestonesReached feeds the bank
// (ClusterFBankLimit); batch 2's job cap and the F-25 BOD bonus will read the same helper.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    /// <summary>One rung of the ladder: its index (1 = Iron), the metal it is named for, and that metal's hue.</summary>
    public sealed record LeagueRankInfo(int Index, string MetalKey, string MetalName, int Hue)
    {
        public string Name => $"{MetalName} Citizen";
    }

    /// <summary>What rising TO a rank needs. Chapter 0 means no trial (not a milestone).</summary>
    public sealed record LeagueRankRequirement(int Renown, int GuildRank, int Guilds, int Chapter)
    {
        public bool IsMilestone => Chapter > 0;
    }

    public enum LeagueCheckState
    {
        Passed,
        Failed,
        Waived,       // shown as "not yet required"
        NotRequired,  // no trial at this rank
    }

    public sealed record LeagueCheck(string Label, LeagueCheckState State, string Detail)
    {
        public bool Blocks => State == LeagueCheckState.Failed;
    }

    /// <summary>The four checks for one character's next rank, as the Registrar's Rank page shows them.</summary>
    public sealed record LeaguePromotionReport(
        int From, int To, string Blocker, LeagueCheck Renown, LeagueCheck Guilds, LeagueCheck Job, LeagueCheck Trial)
    {
        public IEnumerable<LeagueCheck> Checks
        {
            get
            {
                yield return Renown;
                yield return Guilds;
                yield return Job;
                yield return Trial;
            }
        }

        public bool CanPromote => Blocker == null && !Renown.Blocks && !Guilds.Blocks && !Job.Blocks && !Trial.Blocks;
    }

    public static class ClusterFLeagueRanks
    {
        /// <summary>Metal enum names that never become a League rank. Dawnstone: cc-P36's region-only starter ore.</summary>
        public static readonly HashSet<string> LadderExcluded = new(StringComparer.OrdinalIgnoreCase) { "Dawnstone" };

        public const int ChapterCount = 6;

        public readonly record struct Metal(string Key, string Name, int Hue);

        // -- The ladder -------------------------------------------------------------------------------

        /// <summary>Every CraftResource the resource tables call a metal, in enum order.</summary>
        public static List<Metal> MetalsFromEnum()
        {
            var metals = new List<Metal>();

            foreach (var r in Enum.GetValues<CraftResource>())
            {
                if (CraftResources.GetType(r) == CraftResourceType.Metal)
                {
                    metals.Add(new Metal(r.ToString(), CraftResources.GetName(r), CraftResources.GetHue(r)));
                }
            }

            return metals;
        }

        /// <summary>One rank per metal not excluded, numbered from 1 in the order given.</summary>
        public static List<LeagueRankInfo> Generate(IEnumerable<Metal> metals, ICollection<string> excluded)
        {
            var ladder = new List<LeagueRankInfo>();

            foreach (var m in metals)
            {
                if (!excluded.Contains(m.Key))
                {
                    ladder.Add(new LeagueRankInfo(ladder.Count + 1, m.Key, m.Name, m.Hue));
                }
            }

            return ladder;
        }

        private static List<LeagueRankInfo> _ladder;

        public static IReadOnlyList<LeagueRankInfo> Ladder => _ladder ??= Generate(MetalsFromEnum(), LadderExcluded);

        public static int TopRank => Ladder.Count;

        public static LeagueRankInfo GetRankInfo(int index) => index >= 1 && index <= Ladder.Count ? Ladder[index - 1] : null;

        public static string RankName(int index) => index <= 0 ? "Unregistered" : GetRankInfo(index)?.Name ?? $"Rank {index}";

        /// <summary>A label hue for the rank: its metal's hue, or plain text for Iron (hue 0) and no rank.</summary>
        public static int LabelHue(int index)
        {
            var hue = GetRankInfo(index)?.Hue ?? 0;
            return hue == 0 ? 1153 : hue;
        }

        // -- Settings ---------------------------------------------------------------------------------

        // league.promotionJobsWaivedUntilBuilt (default true): the promotion job check passes as "not yet required"
        // until batch 2's jobs exist. Settable here so facts can clear it.
        public static bool PromotionJobsWaived { get; set; } = true;

        private static readonly bool[] _trialWaived = [true, true, true, true, true, true];

        // league.trialsWaivedUntilBuilt.chapterN (default true for all six): a chapter that is not built does not
        // block. Clear one when that chapter ships; from then on rising to its rank needs TrialsDone to hold it.
        public static bool IsTrialWaived(int chapter) => chapter is >= 1 and <= ChapterCount && _trialWaived[chapter - 1];

        public static void SetTrialWaived(int chapter, bool waived)
        {
            if (chapter is >= 1 and <= ChapterCount)
            {
                _trialWaived[chapter - 1] = waived;
            }
        }

        public static string TrialSettingKey(int chapter) => $"league.trialsWaivedUntilBuilt.chapter{chapter}";

        public const string PromotionJobsSettingKey = "league.promotionJobsWaivedUntilBuilt";

        public static void Configure()
        {
            PromotionJobsWaived = ServerConfiguration.GetOrUpdateSetting(PromotionJobsSettingKey, true);

            for (var ch = 1; ch <= ChapterCount; ch++)
            {
                SetTrialWaived(ch, ServerConfiguration.GetOrUpdateSetting(TrialSettingKey(ch), true));
            }

            foreach (var line in LeagueLadderTable.LoadOrWriteDefaults(LeagueLadderTable.FilePath))
            {
                Console.WriteLine($"[League] {line}");
            }

            CommandSystem.Register("LeagueLadder", AccessLevel.GameMaster, OnLadderCommand);
        }

        // -- A character's rank -----------------------------------------------------------------------

        private static ClusterFAccountData AccountData(Mobile m) =>
            m?.Account is IAccount acct ? ClusterFAccountPersistence.Get(acct) : null;

        /// <summary>
        /// This character's rank: 0 when the account is not registered, else at least 1 (Iron, which registration
        /// gives every character on the account), up to the top rank.
        /// </summary>
        public static int GetRank(Mobile m)
        {
            var data = AccountData(m);

            if (data == null || !ClusterFLeagueSystem.IsJoined(data))
            {
                return 0;
            }

            var stored = data.GetLeagueData(m.Serial)?.Rank ?? 0;
            return Math.Clamp(stored, 1, TopRank);
        }

        /// <summary>Staff only ([props LeagueRank): registers the account if needed and sets the rank, 1 to top.</summary>
        public static void SetRank(Mobile m, int rank)
        {
            if (m?.Account is not IAccount acct)
            {
                return;
            }

            var data = ClusterFAccountPersistence.GetOrCreate(acct);
            data.SetFlag(ClusterFLeagueSystem.FlagJoined);
            data.GetOrCreateLeagueData(m.Serial).Rank = Math.Clamp(rank, 1, TopRank);
            ClusterFBankLimit.Apply(m, false);
        }

        /// <summary>How many milestone ranks (a rank whose row names a trial chapter) this character has reached: 0 to 6.</summary>
        public static int LeagueMilestonesReached(Mobile m) => MilestonesAtOrBelow(GetRank(m));

        public static int MilestonesAtOrBelow(int rank)
        {
            var count = 0;

            for (var r = 2; r <= Math.Min(rank, TopRank); r++)
            {
                if (LeagueLadderTable.Current.For(r).IsMilestone)
                {
                    count++;
                }
            }

            return count;
        }

        // -- Promotion --------------------------------------------------------------------------------

        public static LeaguePromotionReport Evaluate(PlayerMobile pm)
        {
            var from = GetRank(pm);
            var to = from + 1;
            var data = AccountData(pm);

            if (from == 0)
            {
                return Blocked(from, "Register with the League first. Registration makes you an Iron Citizen.");
            }

            if (from >= TopRank)
            {
                return Blocked(from, $"You hold the League's highest rank, {RankName(TopRank)}.");
            }

            var req = LeagueLadderTable.Current.For(to);
            var league = data.GetLeagueData(pm.Serial);
            var guild = data.GetGuildData(pm.Serial);

            var lifetime = data.LifetimeRenown;
            var renown = new LeagueCheck(
                "Lifetime Renown",
                lifetime >= req.Renown ? LeagueCheckState.Passed : LeagueCheckState.Failed,
                $"{lifetime:N0} of {req.Renown:N0}"
            );

            var have = CountGuildsAtOrAbove(guild, req.GuildRank);
            var rankName = ClusterFGuildSystem.RankIndexName(req.GuildRank);
            var guilds = req.Guilds <= 0
                ? new LeagueCheck("Guild ranks", LeagueCheckState.Passed, "none needed")
                : new LeagueCheck(
                    "Guild ranks",
                    have >= req.Guilds ? LeagueCheckState.Passed : LeagueCheckState.Failed,
                    $"{rankName} or higher in {req.Guilds} guild{(req.Guilds == 1 ? "" : "s")} (you: {have})"
                );

            LeagueCheck job;
            if (league?.PromotionDone.Contains(to) == true)
            {
                job = new LeagueCheck("Promotion job", LeagueCheckState.Passed, "done");
            }
            else if (PromotionJobsWaived)
            {
                job = new LeagueCheck("Promotion job", LeagueCheckState.Waived, "not yet required");
            }
            else
            {
                job = new LeagueCheck("Promotion job", LeagueCheckState.Failed, $"turn in the {RankName(to)} promotion job");
            }

            LeagueCheck trial;
            if (!req.IsMilestone)
            {
                trial = new LeagueCheck("Story trial", LeagueCheckState.NotRequired, "none at this rank");
            }
            else if (league?.TrialsDone.Contains(req.Chapter) == true)
            {
                trial = new LeagueCheck($"Story trial, chapter {req.Chapter}", LeagueCheckState.Passed, "done");
            }
            else if (IsTrialWaived(req.Chapter))
            {
                trial = new LeagueCheck($"Story trial, chapter {req.Chapter}", LeagueCheckState.Waived, "not yet required");
            }
            else
            {
                trial = new LeagueCheck($"Story trial, chapter {req.Chapter}", LeagueCheckState.Failed, "finish the chapter");
            }

            return new LeaguePromotionReport(from, to, null, renown, guilds, job, trial);
        }

        private static LeaguePromotionReport Blocked(int from, string why)
        {
            var none = new LeagueCheck("", LeagueCheckState.NotRequired, "");
            return new LeaguePromotionReport(from, from, why, none, none, none, none);
        }

        /// <summary>Joined guilds where this character's rank index is at least minIndex.</summary>
        public static int CountGuildsAtOrAbove(CharacterGuildData guild, int minIndex)
        {
            if (guild == null)
            {
                return 0;
            }

            var count = 0;

            foreach (var key in guild.JoinedGuilds)
            {
                if (ClusterFGuildSystem.GuildRankIndex(key, guild) >= minIndex)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The Promote button. Checks everything again, server side, and only from the rank the page was drawn at:
        /// a stale page (rank changed since, or a check no longer met) promotes nobody. Promotes by exactly one rank.
        /// </summary>
        public static bool TryPromote(PlayerMobile pm, int expectedFrom, out string message)
        {
            var report = Evaluate(pm);

            if (report.From != expectedFrom)
            {
                message = "Your League rank has changed since that page was opened. Look at it again.";
                return false;
            }

            if (report.Blocker != null)
            {
                message = report.Blocker;
                return false;
            }

            if (!report.CanPromote)
            {
                var missing = new List<string>();
                foreach (var c in report.Checks)
                {
                    if (c.Blocks)
                    {
                        missing.Add($"{c.Label}: {c.Detail}");
                    }
                }

                message = $"Not yet. {string.Join("; ", missing)}.";
                return false;
            }

            var data = AccountData(pm);
            data.GetOrCreateLeagueData(pm.Serial).Rank = report.To;

            // Bonuses step only at milestones; Apply is a no-op when the limit has not changed.
            ClusterFBankLimit.Apply(pm, false);

            message = LeagueLadderTable.Current.For(report.To).IsMilestone
                ? $"The League names you {RankName(report.To)}, a milestone rank. Your bank box now holds {ClusterFBankLimit.GetMaxItems(pm):N0} items."
                : $"The League names you {RankName(report.To)}.";
            return true;
        }

        // -- [LeagueLadder ------------------------------------------------------------------------------

        [Usage("LeagueLadder [reload]")]
        [Description("Lists the League ladder: each rank's Renown, guild rank and trial, and the waivers in force. 'reload' reads Configuration/league-ladder.json again; a bad file is refused whole and the table in force is kept.")]
        [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any)]
        private static void OnLadderCommand(CommandEventArgs e)
        {
            if (e.Length >= 1 && e.GetString(0).Equals("reload", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in LeagueLadderTable.Reload(LeagueLadderTable.FilePath))
                {
                    e.Mobile.SendMessage(line);
                }
            }

            foreach (var line in Describe())
            {
                e.Mobile.SendMessage(line);
            }
        }

        public static List<string> Describe()
        {
            var lines = new List<string> { $"League ladder ({LeagueLadderTable.Current.Source}), {TopRank} ranks:" };

            foreach (var rank in Ladder)
            {
                var req = LeagueLadderTable.Current.For(rank.Index);
                lines.Add(
                    rank.Index == 1
                        ? $"{rank.Index,2} {rank.Name}: given on registration"
                        : $"{rank.Index,2} {rank.Name}: Renown {req.Renown:N0}, {ClusterFGuildSystem.RankIndexName(req.GuildRank)} in {req.Guilds}" +
                          (req.IsMilestone ? $", trial chapter {req.Chapter}{(IsTrialWaived(req.Chapter) ? " (waived)" : "")}" : "")
                );
            }

            lines.Add($"Promotion jobs {(PromotionJobsWaived ? "waived until built" : "required")}.");
            return lines;
        }
    }

    /// <summary>
    /// The placeholder numbers for each rank (cc-P48 Part D): one row per ladder metal. Loaded from
    /// Configuration/league-ladder.json so Chase can change them without a build; the defaults below are what a missing
    /// file is written with.
    /// </summary>
    public sealed class LeagueLadderTable
    {
        public const string FileName = "league-ladder.json";

        public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

        // Renown curve (proposal, cc-P48): today's 74 achievements pay 6,505 Renown in all (counted), so Bronze (5) at
        // 2,500 is reachable by achievements alone and Gold (6) at 7,000 is not: everything past Bronze needs batch 2's
        // jobs. Guild requirements are the brief's defaults. Chapters by the metal names Chase gave (Dull Copper,
        // Bronze, Verite, Blaze, Mythril, Celestial).
        private static readonly (string Metal, int Renown, int GuildRank, int Guilds, int Chapter)[] DefaultRows =
        [
            ("Iron",       0,       ClusterFGuildSystem.RankInitiate,   0, 0),
            ("DullCopper", 100,     ClusterFGuildSystem.RankApprentice, 1, 1),
            ("ShadowIron", 400,     ClusterFGuildSystem.RankApprentice, 1, 0),
            ("Copper",     1_000,   ClusterFGuildSystem.RankApprentice, 1, 0),
            ("Bronze",     2_500,   ClusterFGuildSystem.RankApprentice, 2, 2),
            ("Gold",       7_000,   ClusterFGuildSystem.RankApprentice, 2, 0),
            ("Agapite",    10_000,  ClusterFGuildSystem.RankApprentice, 2, 0),
            ("Verite",     14_000,  ClusterFGuildSystem.RankJourneyman, 3, 3),
            ("Valorite",   19_000,  ClusterFGuildSystem.RankJourneyman, 3, 0),
            ("Platinum",   25_000,  ClusterFGuildSystem.RankJourneyman, 3, 0),
            ("Toxic",      32_000,  ClusterFGuildSystem.RankJourneyman, 4, 0),
            ("Blaze",      40_000,  ClusterFGuildSystem.RankJourneyman, 4, 4),
            ("Frost",      50_000,  ClusterFGuildSystem.RankJourneyman, 4, 0),
            ("Obsidian",   62_000,  ClusterFGuildSystem.RankMaster,     4, 0),
            ("Mythril",    76_000,  ClusterFGuildSystem.RankMaster,     4, 5),
            ("Adamantium", 92_000,  ClusterFGuildSystem.RankMaster,     4, 0),
            ("Celestial",  110_000, ClusterFGuildSystem.RankMaster,     5, 6),
        ];

        private readonly Dictionary<string, LeagueRankRequirement> _byMetal;

        private LeagueLadderTable(Dictionary<string, LeagueRankRequirement> byMetal, string source)
        {
            _byMetal = byMetal;
            Source = source;
        }

        /// <summary>Where the table in force came from: "built-in defaults" or the file's path.</summary>
        public string Source { get; }

        public static LeagueLadderTable Defaults { get; } = BuildDefaults();

        public static LeagueLadderTable Current { get; set; } = Defaults;

        private static LeagueLadderTable BuildDefaults()
        {
            var rows = new Dictionary<string, LeagueRankRequirement>(StringComparer.OrdinalIgnoreCase);
            foreach (var (metal, renown, guildRank, guilds, chapter) in DefaultRows)
            {
                rows[metal] = new LeagueRankRequirement(renown, guildRank, guilds, chapter);
            }

            return new LeagueLadderTable(rows, "built-in defaults");
        }

        /// <summary>
        /// The requirement for rising TO this rank. A rank the table has no row for (a metal added to the ladder after
        /// the table was written) needs what the rank below it needs, never less, and has no trial.
        /// </summary>
        public LeagueRankRequirement For(int rankIndex)
        {
            var rank = ClusterFLeagueRanks.GetRankInfo(rankIndex);
            if (rank == null)
            {
                return new LeagueRankRequirement(int.MaxValue, ClusterFGuildSystem.RankTop, int.MaxValue, 0);
            }

            if (_byMetal.TryGetValue(rank.MetalKey, out var req))
            {
                return req;
            }

            var below = rankIndex > 1 ? For(rankIndex - 1) : new LeagueRankRequirement(0, 0, 0, 0);
            return below with { Chapter = 0 };
        }

        // -- The file ----------------------------------------------------------------------------------

        private static readonly string[] GuildRankWords = ["Initiate", "Apprentice", "Journeyman", "Master", "Grandmaster", "Top"];

        public static string ToJson(LeagueLadderTable table)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"about\": \"League ladder requirements (cc-P48). One row per metal rank: the lifetime Renown, the guild rank ");
            sb.Append("(Initiate, Apprentice, Journeyman, Master, Grandmaster, Top) and how many guilds at it, to rise TO that rank, ");
            sb.Append("and the story trial chapter (1-6) for a milestone rank, 0 for none. Iron is given on registration; its row ");
            sb.Append("is not used. Edit and run [LeagueLadder reload. A bad file is refused whole and the table in force is kept.\",\n");
            sb.Append("  \"ranks\": [\n");

            var ladder = ClusterFLeagueRanks.Ladder;
            for (var i = 0; i < ladder.Count; i++)
            {
                var rank = ladder[i];
                var req = table.For(rank.Index);
                sb.Append(
                    $"    {{ \"metal\": \"{rank.MetalKey}\", \"renown\": {req.Renown}, \"guildRank\": \"{GuildRankWords[Math.Clamp(req.GuildRank, 0, 5)]}\", " +
                    $"\"guilds\": {req.Guilds}, \"chapter\": {req.Chapter} }}{(i < ladder.Count - 1 ? "," : "")}\n"
                );
            }

            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Reads a table from JSON. Returns null and fills errors when anything is wrong; a table is accepted whole or
        /// not at all. Checked: syntax; a "ranks" array; each row's metal is a ladder metal and appears once; renown
        /// is a whole number of 0 or more and never falls from one rank to the next; guildRank is a known word; guilds
        /// is 0 to the number of guilds; chapter is 0 to 6 and each chapter is used once; every rank from Dull Copper
        /// up has a row.
        /// </summary>
        public static LeagueLadderTable Parse(string json, string source, List<string> errors)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException ex)
            {
                errors.Add($"not valid JSON: {ex.Message}");
                return null;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("ranks", out var ranks) || ranks.ValueKind != JsonValueKind.Array)
                {
                    errors.Add("no \"ranks\" array at the top level.");
                    return null;
                }

                ClusterFGuildSystem.EnsureRegistered();
                var guildCount = ClusterFGuildSystem.AllGuilds.Count;

                var byMetal = new Dictionary<string, LeagueRankRequirement>(StringComparer.OrdinalIgnoreCase);
                var chapters = new Dictionary<int, string>();
                var row = 0;

                foreach (var el in ranks.EnumerateArray())
                {
                    row++;
                    var where = $"row {row}";

                    if (el.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"{where}: not an object.");
                        continue;
                    }

                    var metal = el.TryGetProperty("metal", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    var rank = metal == null ? null : FindRank(metal);

                    if (rank == null)
                    {
                        errors.Add($"{where}: \"metal\" {(metal == null ? "is missing" : $"'{metal}' is not a ladder metal")}.");
                        continue;
                    }

                    where = $"row {row} ({rank.MetalKey})";

                    if (byMetal.ContainsKey(rank.MetalKey))
                    {
                        errors.Add($"{where}: a second row for this metal.");
                        continue;
                    }

                    var ok = true;
                    var renown = ReadInt(el, "renown", 0, int.MaxValue, where, errors, ref ok);
                    var guilds = ReadInt(el, "guilds", 0, guildCount, where, errors, ref ok);
                    var chapter = ReadInt(el, "chapter", 0, ClusterFLeagueRanks.ChapterCount, where, errors, ref ok);

                    var guildRank = -1;
                    if (el.TryGetProperty("guildRank", out var gr) && gr.ValueKind == JsonValueKind.String)
                    {
                        guildRank = Array.FindIndex(GuildRankWords, w => w.Equals(gr.GetString(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (guildRank < 0)
                    {
                        errors.Add($"{where}: \"guildRank\" must be one of {string.Join(", ", GuildRankWords)}.");
                        ok = false;
                    }

                    if (chapter > 0)
                    {
                        if (chapters.TryGetValue(chapter, out var other))
                        {
                            errors.Add($"{where}: chapter {chapter} is already on {other}.");
                            ok = false;
                        }
                        else
                        {
                            chapters[chapter] = rank.MetalKey;
                        }
                    }

                    if (ok)
                    {
                        byMetal[rank.MetalKey] = new LeagueRankRequirement(renown, guildRank, guilds, chapter);
                    }
                }

                var previous = 0;
                foreach (var rank in ClusterFLeagueRanks.Ladder)
                {
                    if (rank.Index == 1)
                    {
                        continue; // Iron: given on registration
                    }

                    if (!byMetal.TryGetValue(rank.MetalKey, out var req))
                    {
                        if (!errors.Exists(e => e.Contains($"({rank.MetalKey})")))
                        {
                            errors.Add($"no row for {rank.MetalKey} (rank {rank.Index}).");
                        }

                        continue;
                    }

                    if (req.Renown < previous)
                    {
                        errors.Add($"{rank.MetalKey}: renown {req.Renown:N0} is less than the rank below it ({previous:N0}).");
                    }

                    previous = req.Renown;
                }

                return errors.Count == 0 ? new LeagueLadderTable(byMetal, source) : null;
            }
        }

        private static LeagueRankInfo FindRank(string metal)
        {
            foreach (var rank in ClusterFLeagueRanks.Ladder)
            {
                if (rank.MetalKey.Equals(metal, StringComparison.OrdinalIgnoreCase))
                {
                    return rank;
                }
            }

            return null;
        }

        private static int ReadInt(JsonElement el, string name, int min, int max, string where, List<string> errors, ref bool ok)
        {
            if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var value))
            {
                errors.Add($"{where}: \"{name}\" is missing or not a whole number.");
                ok = false;
                return 0;
            }

            if (value < min || value > max)
            {
                errors.Add($"{where}: \"{name}\" {value} is outside {min} to {max}.");
                ok = false;
            }

            return value;
        }

        /// <summary>At start: read the file, or write the defaults to it when it is missing. Returns lines to log.</summary>
        public static List<string> LoadOrWriteDefaults(string path)
        {
            if (!File.Exists(path))
            {
                Current = Defaults;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, ToJson(Defaults));
                    return [$"{FileName} was missing; wrote the built-in defaults to {path}."];
                }
                catch (Exception ex)
                {
                    return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in defaults."];
                }
            }

            return Reload(path);
        }

        /// <summary>Reads the file and puts it in force, or refuses it whole and keeps the table in force. Returns lines to report.</summary>
        public static List<string> Reload(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                return [$"Could not read {path} ({ex.Message}); kept {Current.Source}."];
            }

            return Apply(json, path);
        }

        /// <summary>Parses and, if it is good, puts the table in force. Returns lines to report.</summary>
        public static List<string> Apply(string json, string source)
        {
            var errors = new List<string>();
            var table = Parse(json, source, errors);

            if (table == null)
            {
                var lines = new List<string> { $"{FileName} REFUSED ({errors.Count} error{(errors.Count == 1 ? "" : "s")}); kept {Current.Source}:" };
                foreach (var e in errors)
                {
                    lines.Add($"  {e}");
                }

                return lines;
            }

            Current = table;
            return [$"Loaded the League ladder table from {source}."];
        }
    }
}

namespace Server.Mobiles
{
    // cc-P48: the League fields on [props, for staff walking a character up the ladder on the test shard.
    public partial class PlayerMobile
    {
        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Administrator)]
        public int LeagueRank
        {
            get => ClusterFLeagueRanks.GetRank(this);
            set => ClusterFLeagueRanks.SetRank(this, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string LeagueRankTitle => ClusterFLeagueRanks.RankName(ClusterFLeagueRanks.GetRank(this));

        [CommandProperty(AccessLevel.GameMaster)]
        public int LeagueMilestones => ClusterFLeagueRanks.LeagueMilestonesReached(this);

        // Account-wide: every character on the account shows and sets the same number.
        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Administrator)]
        public int LeagueLifetimeRenown
        {
            get => Account is IAccount acct ? ClusterFAccountPersistence.Get(acct)?.LifetimeRenown ?? 0 : 0;
            set
            {
                if (Account is IAccount acct)
                {
                    ClusterFAccountPersistence.GetOrCreate(acct).LifetimeRenown = Math.Max(0, value);
                }
            }
        }

        // Comma-separated chapter numbers, e.g. "1,2".
        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Administrator)]
        public string LeagueTrialsDone
        {
            get => LeagueSet(false);
            set => SetLeagueSet(false, value);
        }

        // Comma-separated rank indexes whose promotion job is done, e.g. "2,3".
        [CommandProperty(AccessLevel.GameMaster, AccessLevel.Administrator)]
        public string LeaguePromotionJobsDone
        {
            get => LeagueSet(true);
            set => SetLeagueSet(true, value);
        }

        private string LeagueSet(bool jobs)
        {
            var record = Account is IAccount acct ? ClusterFAccountPersistence.Get(acct)?.GetLeagueData(Serial) : null;
            if (record == null)
            {
                return "";
            }

            var list = new List<int>(jobs ? record.PromotionDone : record.TrialsDone);
            list.Sort();
            return string.Join(",", list);
        }

        private void SetLeagueSet(bool jobs, string value)
        {
            if (Account is not IAccount acct)
            {
                return;
            }

            var record = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateLeagueData(Serial);
            var set = jobs ? record.PromotionDone : record.TrialsDone;
            set.Clear();

            foreach (var part in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var n) && n > 0)
                {
                    set.Add(n);
                }
            }
        }
    }
}
