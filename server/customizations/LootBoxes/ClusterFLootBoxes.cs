// cc-P79 (F-40, F-41): loot box awards, their sources, and the hooks for sources that do not exist yet. Notes:
// shard-migration notes/cc-P79-loot-boxes-fanfare.md (and the Step 0 survey, notes/loot-boxes-fanfare-survey.md).
//
// A box is awarded the first time a character earns a source on a loop; a source earned again on a later loop gives no
// box, a repeat line with the count instead. Records are per character (CharacterLootRecord, ClusterFAccountData v18):
// source key to the loops it was earned on. Source keys:
//   ach:<achievement key>   achievements (ClusterFAchievementSystem.TryGrant: the account's first earn; see below)
//   boss:<type name>        each boss's first kill (CreatureDeathEvent; "boss" = ClusterFRareSelfRepair.IsHighEndSource
//                           without paragons), for every player with looting rights
//   league:<rank>           each League rank reached (ClusterFLeagueRanks.TryPromote)
//   clear:<dungeon>         each dungeon or floor's first clear (OnDungeonCleared: a hook, no clear tracking exists)
//   loop:<n>                each loop completed (tutorial graduation records loop 1, P371)
//
// Achievements are stored per ACCOUNT by the achievement system (ClusterFAchievements.cs:267-269), so TryGrant fires once
// per account. The box goes to the account's online character, the one the earn popup goes to. A second character on the
// account gets no box for an achievement its account already holds (decision for Chase in the notes).
//
// Hooks left for sources that do not exist: OnLevelUp, OnDungeonCleared, OnTrialChapter, OnReached200, CombineLoopScroll.
// LevelOf is a placeholder until F-12. CurrentLoop reads the per-character count (P371).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server.LootBoxes;

public static class ClusterFLootBoxes
{
    public static void Configure()
    {
        foreach (var line in LootTable.LoadOrWriteDefaults(LootTable.FilePath))
        {
            Console.WriteLine($"[LootBox] {line}");
        }

        CommandSystem.Register("LootBox", AccessLevel.GameMaster, OnLootBoxCommand);
        CommandSystem.Register("Fanfare", AccessLevel.GameMaster, OnFanfareCommand);
        EventSink.WorldLoad += () => Timer.StartTimer(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), () => CleanUpAbandoned());
    }

    // -- Loop record and the level placeholder until F-12 --

    /// <summary>Character's recorded loop; creation is loop 0, tutorial graduation is loop 1 (P371).</summary>
    public static Func<PlayerMobile, int> LoopProvider { get; set; } = Server.Tutorial.ClusterFLoops.LoopOf;

    public static int CurrentLoop(PlayerMobile pm) => LoopProvider(pm);

    /// <summary>
    /// The character's level, 1 to 200. F-12 hook: no level exists, so this stands in with total Base skill / 10
    /// (700 skill points is level 70).
    /// </summary>
    public static int LevelOf(PlayerMobile pm)
    {
        var total = 0.0;
        for (var i = 0; i < pm.Skills.Length; i++)
        {
            total += pm.Skills[i].Base;
        }

        return Math.Clamp((int)(total / 10), 1, 200);
    }

    public static CharacterLootRecord? RecordOf(PlayerMobile pm) =>
        pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateLootRecord(pm.Serial) : null;

    // -- The award --

    public enum AwardResult
    {
        Box,
        Repeat,
        AlreadyThisLoop,
        NoRecord
    }

    /// <summary>
    /// Records the source on this character's loop. A first ever: rolls and delivers a box. A later loop: no box, a repeat
    /// line with the count. The same loop again: nothing.
    /// </summary>
    public static AwardResult Award(PlayerMobile pm, string source, BoxTheme theme, int tier, SkillName? skill, string label,
        out LootBox? box)
    {
        box = null;
        var record = RecordOf(pm);
        if (record == null)
        {
            return AwardResult.NoRecord;
        }

        var first = record.CountOf(source) == 0;
        var loop = CurrentLoop(pm);
        if (!record.Record(source, loop))
        {
            return AwardResult.AlreadyThisLoop;
        }

        if (!first)
        {
            var count = record.CountOf(source);
            ClusterFFanfare.Play(pm, FanfareLevel.Minor, FanfareLines.RepeatAchievement, new FanfareVars(Loop: loop, Count: count));
            return AwardResult.Repeat;
        }

        box = MakeBox(pm, theme, tier, skill, label, null);
        ClusterFGuildPromotion.Deliver(pm, box);
        ClusterFImbueEffects.OnLootBoxEarned(pm, source); // cc-P82: the Fortune's Favor achievements
        var level = tier >= 4 ? FanfareLevel.Major : FanfareLevel.Minor;
        ClusterFFanfare.Play(pm, level, FanfareLines.BoxAwarded, new FanfareVars(Tier: box.BoxName), tier,
            voiceKey: $"box:{box.BoxName}");
        pm.SendMessage(0x44, $"System: You have received a {box.DefaultName}!");
        return AwardResult.Box;
    }

    /// <summary>A box rolled for this character now (contents fixed at this moment).</summary>
    public static LootBox MakeBox(PlayerMobile pm, BoxTheme theme, int tier, SkillName? skill, string label, string? sourceName)
    {
        var rolled = LootBoxRoller.Roll(pm, theme, tier, skill, sourceName ?? label);
        // cc-P363: the keyed tier comes locked, and opens with a Vault key (ShardKeys.cs).
        var box = ShardKeys.NeedsKey(tier)
            ? new ShardLockedLootBox(theme, tier, pm.Account?.Username ?? "", label)
            : new LootBox(theme, tier, pm.Account?.Username ?? "", label);
        box.Fill(rolled.BoxName, rolled.Hue, rolled.Gold, rolled.Renown, rolled.Items);
        return box;
    }

    // -- Sources that exist --

    /// <summary>Called by ClusterFAchievementSystem.TryGrant on the account's first earn of an achievement.</summary>
    public static void OnAchievementEarned(IAccount acct, AchievementDef def)
    {
        PlayerMobile? pm = null;
        for (var i = 0; i < acct.Length && pm == null; i++)
        {
            if (acct[i] is PlayerMobile { NetState: not null } online)
            {
                pm = online;
            }
        }

        if (pm == null)
        {
            return; // offline grants (staff): no character to celebrate or carry the box
        }

        var table = LootTable.Current;

        // cc-P263: an achievement whose Noted reward names a joke item earns a Dented Box (its tier, the joke item inside).
        var noted = NotedRewards.For(def.Key) is { JokeItem: not null } r ? r : null;
        var tier = noted?.BoxTier ?? table.TierForAchievement(def.Key, def.AP);
        var level = tier >= 4 ? FanfareLevel.Major : FanfareLevel.Minor;
        ClusterFFanfare.Play(pm, level, level == FanfareLevel.Major ? FanfareLines.MajorAchievement : FanfareLines.MinorAchievement,
            tier: tier, voiceKey: "achievement");
        Award(pm, $"ach:{def.Key}", table.ThemeForCategory(def.Category.ToString()), tier, def.Trigger.OnSkill, def.Title, out var box);
        if (noted != null && box != null)
        {
            ClusterFNotedBySystem.Dent(box, pm, noted);
        }
    }

    /// <summary>A boss for loot boxes: the shard's high-end list (ClusterFRareSelfRepair.IsHighEndSource), paragons left out.</summary>
    public static bool IsBoss(BaseCreature bc) => !bc.IsParagon && ClusterFRareSelfRepair.IsHighEndSource(bc);

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (bc.Controlled || bc.Summoned || !IsBoss(bc))
        {
            return;
        }

        var killers = new HashSet<PlayerMobile>();
        foreach (var ds in BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax))
        {
            if (ds.m_HasRight && ds.m_Mobile is PlayerMobile pm)
            {
                killers.Add(pm);
            }
        }

        if (bc.LastKiller is PlayerMobile last)
        {
            killers.Add(last);
        }

        foreach (var pm in killers)
        {
            OnBossKilled(pm, bc.GetType(), bc.Name ?? bc.GetType().Name);
        }
    }

    public static AwardResult OnBossKilled(PlayerMobile pm, Type bossType, string bossName)
    {
        var result = Award(pm, $"boss:{bossType.Name}", BoxTheme.Boss, LootTable.Current.TierForBoss(bossType), null,
            $"slaying {bossName}", out _);
        ShardKeys.OnBossKilled(pm, Utility.RandomDouble()); // cc-P363: sometimes a Vault key, on every kill
        if (result == AwardResult.Box)
        {
            ClusterFFanfare.Play(pm, FanfareLevel.Major, "", tier: LootTable.Current.TierForBoss(bossType),
                line: $"{pm.Name} has slain {bossName} for the first time.");
        }

        return result;
    }

    /// <summary>Called by ClusterFLeagueRanks.TryPromote when a rank is reached.</summary>
    public static AwardResult OnLeagueRank(PlayerMobile pm, int rank, string rankName) =>
        Award(pm, $"league:{rank}", BoxTheme.League, LootTable.Current.TierForLeagueRank(rank), null, $"League rank {rankName}", out _);

    // -- Hooks for sources that do not exist yet --

    /// <summary>F-12 hook: a level-up. Minor fanfare from the level-up pool.</summary>
    public static void OnLevelUp(PlayerMobile pm, int level) =>
        ClusterFFanfare.Play(pm, FanfareLevel.Minor, FanfareLines.LevelUp, new FanfareVars(Level: level.ToString()), 1, voiceKey: "levelup");

    /// <summary>F-12 hook: a loop completed. Shard-wide fanfare and a Loop box (each loop number is a new first).</summary>
    public static AwardResult OnLoopCompleted(PlayerMobile pm, int loop)
    {
        ClusterFFanfare.Play(pm, FanfareLevel.Capstone, FanfareLines.LoopComplete, new FanfareVars(Loop: loop), 6, voiceKey: "loop");
        return Award(pm, $"loop:{loop}", BoxTheme.Loop, LootTable.Current.LoopTier, null, $"completing loop {loop}", out _);
    }

    /// <summary>Hook: a dungeon or floor's first clear (no clear tracking exists). An Explorer's box.</summary>
    public static AwardResult OnDungeonCleared(PlayerMobile pm, string dungeonKey) =>
        Award(pm, $"clear:{dungeonKey}", BoxTheme.Explorer, LootTable.Current.DungeonClearTier, null, $"clearing {dungeonKey}", out _);

    /// <summary>Hook: a League trial chapter completed (trials are waived and nothing completes them yet). Shard-wide.</summary>
    public static void OnTrialChapter(PlayerMobile pm, int chapter) =>
        ClusterFFanfare.Play(pm, FanfareLevel.Capstone, "", tier: 6, line: $"{pm.Name} has completed chapter {chapter} of the League's trials.",
            voiceKey: "trial");

    /// <summary>F-12 hook: reaching 200 (the level cap). Shard-wide.</summary>
    public static void OnReached200(PlayerMobile pm) =>
        ClusterFFanfare.Play(pm, FanfareLevel.Capstone, FanfareLines.Reached200, tier: 6, voiceKey: "reached200");

    /// <summary>F-12 hook: five fragments make one loop scroll. No loop scroll item exists, so nothing is made yet.</summary>
    public static void CombineLoopScroll(Mobile from, LoopScrollFragment fragments) =>
        from.SendMessage("System: The fragments hum, but the loop scroll they would make does not exist yet. Keep them.");

    // -- The safety cleanup --

    /// <summary>Deletes boxes opened and left (not emptied) for LootTable.AbandonedBoxDays. Returns how many.</summary>
    public static int CleanUpAbandoned()
    {
        var cutoff = Core.Now - TimeSpan.FromDays(LootTable.Current.AbandonedBoxDays);
        var old = new List<LootBox>();
        foreach (var item in World.Items.Values)
        {
            if (item is LootBox { Deleted: false } box && box.OpenedAt != DateTime.MinValue && box.OpenedAt < cutoff)
            {
                old.Add(box);
            }
        }

        foreach (var box in old)
        {
            box.Delete();
        }

        return old.Count;
    }

    /// <summary>"Earned on loops 1, 3, 7" for the achievement page, when a character on the account has earned it on more than one loop.</summary>
    public static string? LoopsLine(string username, string achievementKey)
    {
        if (Accounts.GetAccount(username) is not IAccount acct || ClusterFAccountPersistence.Get(acct) is not { } data)
        {
            return null;
        }

        List<int>? best = null;
        foreach (var record in data.LootRecords)
        {
            if (record.Earned.TryGetValue($"ach:{achievementKey}", out var loops) && (best == null || loops.Count > best.Count))
            {
                best = loops;
            }
        }

        if (best == null || best.Count < 2)
        {
            return null;
        }

        var sorted = new List<int>(best);
        sorted.Sort();
        return $"Earned on loops {string.Join(", ", sorted)} ({sorted.Count} times)";
    }

    // -- Staff commands --

    [Usage("LootBox <give <theme> <tier> | reload | cleanup>")]
    [Description("give: rolls a box of a theme (Miner, Woodsman, Artisan, Hunter, Explorer, Tamer, Scholar, Boss, Loop, League) and tier (1-6) for you. reload: reads Configuration/loot-boxes.json and fanfare-lines.json again (a bad file is refused whole). cleanup: removes boxes opened and abandoned.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.TestOnly)]
    private static void OnLootBoxCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        if (verb == "reload")
        {
            foreach (var line in LootTable.LoadOrWriteDefaults(LootTable.FilePath))
            {
                e.Mobile.SendMessage(line);
            }

            foreach (var line in FanfareLines.LoadOrWriteDefaults(FanfareLines.FilePath))
            {
                e.Mobile.SendMessage(line);
            }

            return;
        }

        if (verb == "cleanup")
        {
            e.Mobile.SendMessage($"Removed {CleanUpAbandoned()} abandoned boxes.");
            return;
        }

        if (verb != "give" || e.Length < 3 || !Enum.TryParse<BoxTheme>(e.GetString(1), true, out var theme) ||
            e.Mobile is not PlayerMobile pm)
        {
            e.Mobile.SendMessage("Usage: [LootBox <give <theme> <tier> | reload | cleanup>");
            return;
        }

        var box = MakeBox(pm, theme, Math.Clamp(e.GetInt32(2), 1, 6), null, "a staff test", null);
        ClusterFGuildPromotion.Deliver(pm, box);
    }

    [Usage("Fanfare <minor | major | capstone>")]
    [Description("Plays a test fanfare on you at that level (a capstone broadcasts to the whole shard).")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnFanfareCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || e.Length < 1 || !Enum.TryParse<FanfareLevel>(e.GetString(0), true, out var level))
        {
            e.Mobile.SendMessage("Usage: [Fanfare <minor | major | capstone>");
            return;
        }

        var pool = level switch
        {
            FanfareLevel.Minor => FanfareLines.MinorAchievement,
            FanfareLevel.Major => FanfareLines.MajorAchievement,
            _ => FanfareLines.LoopComplete
        };
        ClusterFFanfare.Play(pm, level, pool, new FanfareVars(Loop: 1), level == FanfareLevel.Capstone ? 6 : 3);
    }
}
