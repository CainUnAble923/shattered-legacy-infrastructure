// cc-P79 (F-40, F-41): loot box awards, their sources, and the hooks for sources that do not exist yet. Notes:
// shard-migration notes/cc-P79-loot-boxes-fanfare.md (and the Step 0 survey, notes/loot-boxes-fanfare-survey.md).
// cc-P763: the rarity roll and the season at the earn, and the new sources (notes/cc-P763-cont-loot-boxes-diversified.md).
//
// A box is awarded the first time a character earns a source on a loop; a source earned again on a later loop gives no
// box, a repeat line with the count instead. Records are per character (CharacterLootRecord, ClusterFAccountData v18):
// source key to the loops it was earned on. Source keys:
//   ach:<achievement key>   achievements (ClusterFAchievementSystem.TryGrant: the account's first earn; see below)
//   boss:<type name>        each boss's first kill (CreatureDeletedEvent; "boss" = ClusterFRareSelfRepair.IsHighEndSource
//                           without paragons), for every player with looting rights
//   delve:<region>:<type>   cc-P763: a boss's first kill inside a dungeon region (DungeonRegion): a Delver's box
//   league:<rank>           each League rank reached (ClusterFLeagueRanks.TryPromote)
//   clear:<dungeon>         each dungeon or floor's first clear (OnDungeonCleared: a hook, no clear tracking exists)
//   loop:<n>                each loop completed (tutorial graduation records loop 1, P371)
// cc-P763, once per character ever (a later loop gives nothing again, not even the repeat line):
//   fish:<type name>        a High Seas fish's first catch of its kind (the Fishing patch, SendSuccessTo): Fisher
//   sos:<level>             an SOS chest's first recovery at its level (the Fishing patch, Construct): Sailor
//   plant:<plant type>      a plant type first set to decorative (the SetToDecorativeGump patch): Herbalist
//   guild:<key>:<rank>      each new rank in a guild of LootTable.GuildTheme (Society, Fisher/Sailor, Herbalist); ranks
//                           a character already held when this was first checked are recorded without a box
//   dive:<n>                every LootTable.LeagueDiveEvery-th guild work order finished (per account): League Dive
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
using Server.Engines.Craft;
using Server.Engines.Plants;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

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

    // -- cc-P763: the rarity roll and the season --

    /// <summary>The rarity of a box earned at a grade: LootTable.RarityOdds. Tests replace it.</summary>
    public static Func<int, BoxRarity> RarityRoll { get; set; } =
        grade => LootTable.Current.RarityForRoll(grade, Utility.RandomDouble() * 100);

    /// <summary>The shard's local date the seasons are read on (as ClusterFSeasonalSwitch reads it). Tests replace it.</summary>
    public static Func<DateTime> SeasonClock { get; set; } = () => Core.Now.ToLocalTime();

    /// <summary>A season's switch by its name in LootTable.Seasons (touching the decor classes makes their switches).</summary>
    public static ClusterFSeasonalSwitch? SeasonSwitch(string name) => name.ToLowerInvariant() switch
    {
        ClusterFHalloweenDecor.SeasonName => ClusterFHalloweenDecor.Season,
        ClusterFWinterDecor.SeasonName => ClusterFWinterDecor.Season,
        ClusterFSpringSeason.SeasonName => ClusterFSpringSeason.Season,
        _ => ClusterFSeasonalSwitch.All.GetValueOrDefault(name)
    };

    /// <summary>The season on now whose pool a box earned now draws from (the first in the table), or null.</summary>
    public static string? ActiveSeason()
    {
        var today = SeasonClock();
        foreach (var name in LootTable.Current.Seasons.Keys)
        {
            if (SeasonSwitch(name) is { } sw && sw.IsInSeason(today))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>A box's fanfare: Major from grade 4 (as before cc-P763), and at least its rarity's.</summary>
    public static FanfareLevel LevelFor(int tier, BoxRarity rarity)
    {
        var byGrade = tier >= 4 ? FanfareLevel.Major : FanfareLevel.Minor;
        var byRarity = LootTable.Current.RarityOf(rarity).Level;
        return (FanfareLevel)Math.Max((int)byGrade, (int)byRarity);
    }

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
        out LootBox? box, BoxRarity? rarity = null, string? sourceName = null)
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

        box = MakeBox(pm, theme, tier, skill, label, sourceName, rarity);
        ClusterFGuildPromotion.Deliver(pm, box);
        ClusterFImbueEffects.OnLootBoxEarned(pm, source); // cc-P82: the Fortune's Favor achievements
        PlayAwardFanfare(pm, box, tier);
        pm.SendMessage(0x44, $"System: You have received a {box.DefaultName}!");
        return AwardResult.Box;
    }

    /// <summary>cc-P763: a source that gives a box once per character ever: on a later loop it records nothing and says nothing.</summary>
    public static AwardResult AwardOnce(PlayerMobile pm, string source, BoxTheme theme, int tier, SkillName? skill, string label,
        out LootBox? box)
    {
        box = null;
        var record = RecordOf(pm);
        if (record == null)
        {
            return AwardResult.NoRecord;
        }

        return record.CountOf(source) > 0
            ? AwardResult.AlreadyThisLoop
            : Award(pm, source, theme, tier, skill, label, out box);
    }

    // cc-P763: the award's fanfare by grade and rarity. Rare and up say the rare line in the rarity's colour; a Legendary
    // box's capstone tells the shard in a plain line (the box lines are spoken to the player, not about them).
    private static void PlayAwardFanfare(PlayerMobile pm, LootBox box, int tier)
    {
        var step = LootTable.Current.RarityOf(box.Rarity);
        var level = LevelFor(tier, box.Rarity);
        int? hue = box.Rarity > BoxRarity.Common ? step.Hue : null;
        var personal = level == FanfareLevel.Capstone ? FanfareLevel.Major : level;

        ClusterFFanfare.Play(pm, personal, FanfareLines.BoxAwarded, new FanfareVars(Tier: box.BoxName), tier,
            voiceKey: $"box:{box.BoxName}", hue: hue);
        if (step.RareLine)
        {
            ClusterFFanfare.Play(pm, personal, FanfareLines.BoxRare, tier: tier, hue: hue);
        }

        if (level == FanfareLevel.Capstone)
        {
            ClusterFFanfare.Play(pm, FanfareLevel.Capstone, "", tier: tier, line: $"{pm.Name} has earned a {box.DefaultName}.", hue: hue);
        }
    }

    /// <summary>
    /// A box rolled for this character now (contents fixed at this moment). cc-P763: the rarity is rolled here (or given),
    /// and the season on now adds its slot.
    /// </summary>
    public static LootBox MakeBox(PlayerMobile pm, BoxTheme theme, int tier, SkillName? skill, string label, string? sourceName,
        BoxRarity? rarity = null)
    {
        tier = Math.Clamp(tier, 1, 6);
        var r = rarity ?? RarityRoll(tier);
        var rolled = LootBoxRoller.Roll(pm, theme, tier, skill, sourceName ?? label, r, ActiveSeason());
        // cc-P363: the keyed tier comes locked, and opens with a Vault key (ShardKeys.cs).
        var box = ShardKeys.NeedsKey(tier)
            ? new ShardLockedLootBox(theme, tier, pm.Account?.Username ?? "", label) { Rarity = r }
            : new LootBox(theme, tier, pm.Account?.Username ?? "", label) { Rarity = r };
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

        // cc-P263: an achievement whose Noted reward names a joke item earns a Dented Box (its grade, the joke item inside).
        var noted = NotedRewards.For(def.Key) is { JokeItem: not null } r ? r : null;
        var tier = noted?.BoxTier ?? table.TierForAchievement(def.Key, def.AP);
        var level = tier >= 4 ? FanfareLevel.Major : FanfareLevel.Minor;
        ClusterFFanfare.Play(pm, level, level == FanfareLevel.Major ? FanfareLines.MajorAchievement : FanfareLines.MinorAchievement,
            tier: tier, voiceKey: "achievement");
        // cc-P763: the per-key theme first (LootTable.AchievementTheme), then the category's.
        Award(pm, $"ach:{def.Key}", table.ThemeForAchievement(def.Key, def.Category.ToString()), tier, def.Trigger.OnSkill,
            def.Title, out var box);
        if (noted != null && box != null)
        {
            ClusterFNotedBySystem.Dent(box, pm, noted);
        }
    }

    /// <summary>A boss for loot boxes: the shard's high-end list (ClusterFRareSelfRepair.IsHighEndSource), paragons left out.</summary>
    public static bool IsBoss(BaseCreature bc) => !bc.IsParagon && ClusterFRareSelfRepair.IsHighEndSource(bc);

    // cc-P455 (cc-P263 notes, cc-P429 W1): which event. Pinned deletes a dead non-bonded creature inside Mobile.OnDeath, before
    // BaseCreature raises CreatureDeathEvent (Mobile.cs:4899-4902, BaseCreature.cs:3662-3669), so there every boss read
    // Deleted and IsBoss (IsHighEndSource wants it not deleted) was false: no first-kill box ever came. CreatureDeletedEvent
    // comes first (BaseCreature.cs:3674), with the damage list, LastKiller and the master still there; a creature deleted
    // with no hits left is one dying (Mobile.Kill sets Hits to 0), as Noted by the System and level gear read it. A
    // bonded pet's death deletes nothing, and a pet is never a boss here.
    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc)
    {
        if (bc.Hits > 0 || bc.IsDeadPet || bc.Controlled || bc.Summoned || !IsBoss(bc))
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

        var dungeon = DungeonOf(bc);
        foreach (var pm in killers)
        {
            OnBossKilled(pm, bc.GetType(), bc.Name ?? bc.GetType().Name, dungeon);
        }
    }

    /// <summary>cc-P763: the dungeon region a creature stands in (its name), or null outside every dungeon.</summary>
    public static string? DungeonOf(Mobile m) => m.Region?.GetRegion<DungeonRegion>() is { } d ? d.Name ?? "a dungeon" : null;

    public static AwardResult OnBossKilled(PlayerMobile pm, Type bossType, string bossName, string? dungeon = null)
    {
        var grade = LootTable.Current.TierForBoss(bossType);
        // cc-P763: the boss's own name for its trophy ("Trophy of a balron"), not the label ("slaying a balron").
        var result = Award(pm, $"boss:{bossType.Name}", BoxTheme.Boss, grade, null, $"slaying {bossName}", out _, sourceName: bossName);
        ShardKeys.OnBossKilled(pm, Utility.RandomDouble()); // cc-P363: sometimes a Vault key, on every kill
        if (result == AwardResult.Box)
        {
            ClusterFFanfare.Play(pm, FanfareLevel.Major, "", tier: grade, line: $"{pm.Name} has slain {bossName} for the first time.");
        }

        // cc-P763: the dungeon dive. Each dungeon's boss, first killed inside it, is a Delver's box.
        if (dungeon != null)
        {
            AwardOnce(pm, $"delve:{dungeon}:{bossType.Name}", BoxTheme.Delver, grade, null, $"{bossName} in {dungeon}", out _);
        }

        return result;
    }

    /// <summary>Called by ClusterFLeagueRanks.TryPromote when a rank is reached.</summary>
    public static AwardResult OnLeagueRank(PlayerMobile pm, int rank, string rankName) =>
        Award(pm, $"league:{rank}", BoxTheme.League, LootTable.Current.TierForLeagueRank(rank), null, $"League rank {rankName}", out _);

    // -- cc-P763: the sources fishing, sailing and gardening have --

    /// <summary>The Fishing patch (SendSuccessTo), every catch: a High Seas fish's first of its kind is a Fisher's box.</summary>
    public static void OnFished(Mobile from, Item item)
    {
        if (from is not PlayerMobile pm || item is not BaseHighseasFish)
        {
            return;
        }

        var type = item.GetType();
        var grade = FishInfo.IsRareFish(type) ? LootTable.Current.RareFishGrade : LootTable.Current.FishGrade;
        AwardOnce(pm, $"fish:{type.Name}", BoxTheme.Fisher, grade, SkillName.Fishing, $"your first {LootBoxRoller.Describe(item)}", out _);
    }

    /// <summary>The Fishing patch (Construct), an SOS chest raised: its level's first is a Sailor's box.</summary>
    public static void OnSosRecovered(Mobile from, int level)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        level = Math.Clamp(level, 1, 4);
        AwardOnce(pm, $"sos:{level}", BoxTheme.Sailor, LootTable.Current.SosGrade[level - 1], null,
            $"your first level {level} SOS chest", out _);
    }

    /// <summary>The SetToDecorativeGump patch: a plant type first set to decorative is a Herbalist's box.</summary>
    public static void OnPlantDecorative(Mobile from, PlantItem plant)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        AwardOnce(pm, $"plant:{plant.PlantType}", BoxTheme.Herbalist, LootTable.Current.PlantGrade, null,
            $"your first decorative {plant.PlantType}", out _);
    }

    public const string GuildSeededKey = "guild:seeded";

    /// <summary>
    /// Each new rank in a guild LootTable.GuildTheme names is a box of its theme, grade = rank. Checked at login, at each
    /// craft guild promotion (ClusterFGuildPromotion.Update) and after each work order. The first check records the ranks
    /// already held, without boxes: a box is for a promotion from now on. Returns how many boxes it gave.
    /// </summary>
    public static int CheckGuildRanks(PlayerMobile pm)
    {
        var record = RecordOf(pm);
        var data = ClusterFAccountPersistence.GetGuild(pm);
        if (record == null || data == null)
        {
            return 0;
        }

        var table = LootTable.Current;
        var loop = CurrentLoop(pm);
        var seeding = record.CountOf(GuildSeededKey) == 0;
        var given = 0;

        foreach (var key in new List<string>(data.JoinedGuilds))
        {
            var rank = ClusterFGuildSystem.GuildRankIndex(key, data);
            for (var r = 1; r <= Math.Min(rank, ClusterFGuildSystem.RankTop); r++)
            {
                if (table.ThemeForGuildRank(key, r) is not { } theme)
                {
                    break;
                }

                var source = $"guild:{key}:{r}";
                if (seeding)
                {
                    record.Record(source, loop);
                    continue;
                }

                var skills = RecipeOwners.SkillsOf(key);
                SkillName? skill = skills.Count > 0 ? skills[0] : null;
                var name = ClusterFGuildSystem.GetDef(key)?.Name ?? key;
                if (AwardOnce(pm, source, theme, Math.Clamp(r, 1, 6), skill,
                        $"{ClusterFGuildPromotion.RankNameForIndex(key, r)} of the {name}", out _) == AwardResult.Box)
                {
                    given++;
                }
            }
        }

        if (seeding)
        {
            record.Record(GuildSeededKey, loop);
        }

        return given;
    }

    public const string WorkOrderCounter = "lootbox.workOrders";

    /// <summary>A guild work order finished (GuildContractLedgerGump): every LeagueDiveEvery-th is a League Dive box.</summary>
    public static AwardResult? OnWorkOrderCompleted(PlayerMobile pm)
    {
        CheckGuildRanks(pm);

        if (pm.Account is not IAccount acct)
        {
            return null;
        }

        var n = ClusterFAchievementSystem.BumpLootBoxCounter(acct.Username, WorkOrderCounter);
        var every = Math.Max(1, LootTable.Current.LeagueDiveEvery);
        if (n % every != 0)
        {
            return null;
        }

        var rank = ClusterFLeagueRanks.GetRank(pm);
        var grade = rank > 0 ? LootTable.Current.TierForLeagueRank(rank) : 1;
        return AwardOnce(pm, $"dive:{n / every}", BoxTheme.LeagueDive, grade, null, $"{n} work orders finished", out _);
    }

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

    /// <summary>Hook: a dungeon or floor's first clear (no clear tracking exists). cc-P763: a Delver's box (it was an Explorer's).</summary>
    public static AwardResult OnDungeonCleared(PlayerMobile pm, string dungeonKey) =>
        Award(pm, $"clear:{dungeonKey}", BoxTheme.Delver, LootTable.Current.DungeonClearTier, null, $"clearing {dungeonKey}", out _);

    /// <summary>Hook: a League trial chapter completed (trials are waived and nothing completes them yet). Shard-wide.</summary>
    public static void OnTrialChapter(PlayerMobile pm, int chapter) =>
        ClusterFFanfare.Play(pm, FanfareLevel.Capstone, "", tier: 6, line: $"{pm.Name} has completed chapter {chapter} of the League's trials.",
            voiceKey: "trial");

    /// <summary>F-12 hook: reaching 200 (the level cap). Shard-wide.</summary>
    public static void OnReached200(PlayerMobile pm) =>
        ClusterFFanfare.Play(pm, FanfareLevel.Capstone, FanfareLines.Reached200, tier: 6, voiceKey: "reached200");

    /// <summary>
    /// F-12 hook: five fragments make one loop scroll. No loop scroll item exists, so nothing is made yet. cc-P763: boxes
    /// no longer give fragments; saved ones and the monster-stealing ones still answer this.
    /// </summary>
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

    [Usage("LootBox <give <theme> <grade> [rarity] [season] | season | reload | cleanup>")]
    [Description("give: rolls a box of a theme (Miner, Woodsman, Artisan, Hunter, Explorer, Tamer, Scholar, Boss, Loop, League, Fisher, Sailor, Herbalist, Society, Delver, LeagueDive), grade (1-6), and optionally rarity (Common, Rare, Epic, Legendary; else rolled) and season (halloween, winter, spring, none; else today's) for you. season: which season's slot boxes earned now get. reload: reads Configuration/loot-boxes.json and fanfare-lines.json again (a bad file is refused whole). cleanup: removes boxes opened and abandoned.")]
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

        if (verb == "season")
        {
            var on = ActiveSeason();
            e.Mobile.SendMessage(on == null ? "No season is on: boxes earned now get no season slot." : $"The {on} season is on: boxes earned now get its slot.");
            return;
        }

        if (verb != "give" || e.Length < 3 || !Enum.TryParse<BoxTheme>(e.GetString(1), true, out var theme) ||
            e.Mobile is not PlayerMobile pm)
        {
            e.Mobile.SendMessage("Usage: [LootBox <give <theme> <grade> [rarity] [season] | season | reload | cleanup>");
            return;
        }

        BoxRarity? rarity = e.Length > 3 && Enum.TryParse<BoxRarity>(e.GetString(3), true, out var named) ? named : null;
        var grade = Math.Clamp(e.GetInt32(2), 1, 6);
        LootBox box;
        if (e.Length > 4)
        {
            var season = e.GetString(4);
            var r = rarity ?? RarityRoll(grade);
            var rolled = LootBoxRoller.Roll(pm, theme, grade, null, "a staff test", r,
                season.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : season);
            box = new LootBox(theme, grade, pm.Account?.Username ?? "", "a staff test") { Rarity = r };
            box.Fill(rolled.BoxName, rolled.Hue, rolled.Gold, rolled.Renown, rolled.Items);
        }
        else
        {
            box = MakeBox(pm, theme, grade, null, "a staff test", null, rarity);
        }

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
