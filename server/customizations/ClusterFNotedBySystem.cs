// ClusterFNotedBySystem.cs
//
// cc-P263 (ours; the features chat's list of 2026-10-11, design f-exploit-achievements-accessory-slots.md, first
// section). "Noted by the System": hidden achievements for the shard's known exploits and odd habits. They share P231's
// Mischief category (hidden until earned, a snarky flavor line and the plain "(Earned: ...)" line, D73). P82 already
// has Dressed for the Occasion and Creature of Habit, P231 already has a "Planned Obsolescence" (100 worn-out tools), and
// P262 owns Plate Bottoms Only, Iron Soles, Ring Bearer, Fully Accessorized and Smothered, so none of those is here.
//
// Each counts a game event, never message text:
//
//   (deaths)              CreatureDeletedEvent, see OnCreatureDeleted for why not CreatureDeathEvent
//   NeverReachedKills     a melee creature you killed that never damaged you
//   LowHpRetreats         Recall-arrival-achievement.patch, Moongate-arrival-achievement.patch: arriving under 10% hits
//                         while in combat
//   ProvokedKills         killed by a creature you provoked, with no damage of yours on it (BaseCreature-noted-damage-
//                         memory.patch remembers who hit it, past a heal to full)
//   LastHitLarry          a boss's killing blow with under 1% of its damage
//   PetDiedAtFullHp       your pet died while you were at full hits
//   RetameAfterRelease    PetOrders-release-achievement.patch marks the release; PetTrainingHooks.OnTamed (ours) the
//                         tame by the same master within five minutes (pinned gives a re-tame no skill check, no gain)
//   RemotePetKills        your pet's kill while you were hidden or 15+ tiles away
//   BarefootBossKills     a boss's killing blow, barefoot in armor
//   FreshRecycles         Resmelt- and SalvageBag-recycle-achievement.patch, SmithGuildSalvageBag (ours): your own
//                         crafted item recycled within 60 seconds of being made (Item.Created); "your own" is the
//                         crafter CraftItem-fail-streak-achievement.patch records (cc-P457, F07)
//   ExceptionalDestroyed  the same hooks: your exceptional item recycled within 10 seconds
//   CraftFailStreak       CraftItem-fail-streak-achievement.patch: the same item failed again and again
//   DepletedSwingStreak   Mining.CheckResources (our Mining.cs): a swing refused because the vein is empty, same tile;
//                         a swing at a vein with ore ends the streak
//   RecallSpree           the recall patch: 10 recalls in 60 seconds
//   DiedAfterArrival      PlayerDeathEvent within 10 seconds of a recall or gate arrival
//   RunebooksFilled       Runebook-full-achievement.patch: a rune that fills a book (each book counts once)
//   OverweightSteps, HotFootTiles, SnowTiles, PantslessTiles, MuleTrain, HouseStorageFull
//                         PlayerMobile-noted-step.patch (OnAfterMove): a step taken, read where it lands (a turn in
//                         place is not one)
//   DiedAtHome            PlayerDeathEvent inside a house you own or co-own
//   SameTileBoxStreak, BoxTooSoon, EdgeBox, AudienceBox
//                         LootBox.OnDoubleClick and LootBox.Open (ours)
//   QuickCommission       SmithCommissionSystem.Complete (ours): turned in within 60 seconds of being issued
//
// Data only, hidden and unreachable until their event exists (named in the notes): Middle Management (companions),
// Deja Vu and Procrastinator (loops and levels), Cold Feet (the League loop process) and Infamous (commissions never
// expire). Every achievement is earned once per account, as all achievements are.
//
// Rewards (NotedRewards below, data for Chase to tune): a Dented Box (a tier 1 box with one guaranteed joke item,
// NotedJokeItems.cs) replaces the usual earn box on the achievements that name one, and three earn a title
// (LeagueTitles, secret until held). Notes: shard-migration notes/cc-P263-noted-by-the-system-achievements.md.

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Collections;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells.Fourth;

using T = Server.AchievementTrigger;

namespace Server;

public static partial class ClusterFAchievementSystem
{
    public const string GeographyMajorKey        = "noted.geography_major";
    public const string RockEnthusiastKey        = "noted.rock_enthusiast";
    public const string TacticalRetreatKey       = "noted.tactical_retreat";
    public const string ProfessionalCowardKey    = "noted.professional_coward";
    public const string NotMyProblemKey          = "noted.not_my_problem";
    public const string SomebodyElsesProblemKey  = "noted.somebody_elses_problem";
    public const string LastHitLarryKey          = "noted.last_hit_larry";
    public const string AcceptableLossesKey      = "noted.acceptable_losses";
    public const string MeatShieldKey            = "noted.meat_shield_procurement";
    public const string MiddleManagementKey      = "noted.middle_management";
    public const string CommitmentIssuesKey      = "noted.commitment_issues";
    public const string ItsComplicatedKey        = "noted.its_complicated";
    public const string RemoteWorkKey            = "noted.remote_work";
    public const string BareMinimumKey           = "noted.bare_minimum";
    public const string BuiltToBeRecycledKey     = "noted.built_to_be_recycled";
    public const string CircularEconomyKey       = "noted.circular_economy";
    public const string SkillIssueKey            = "noted.skill_issue";
    public const string PersistenceKey           = "noted.persistence_is_not_a_strategy";
    public const string QualityControlKey        = "noted.quality_control";
    public const string RockBottomKey            = "noted.rock_bottom";
    public const string StillNothingKey          = "noted.still_nothing";
    public const string AnyPercentKey            = "noted.any_percent_speedrun";
    public const string WrongTurnKey             = "noted.wrong_turn";
    public const string DirectionallyChallengedKey = "noted.directionally_challenged";
    public const string RuneHoarderKey           = "noted.rune_hoarder";
    public const string RuneArchivistKey         = "noted.rune_archivist";
    public const string AmbitionKey              = "noted.ambition_exceeds_capacity";
    public const string BeastOfBurdenKey         = "noted.beast_of_burden";
    public const string MuleTrainKey             = "noted.mule_train";
    public const string HotFootKey               = "noted.hot_foot";
    public const string FrostbittenKey           = "noted.frostbitten";
    public const string PantslessWonderKey       = "noted.pantsless_wonder";
    public const string HomeSweetGraveKey        = "noted.home_sweet_grave";
    public const string HauntedPropertyKey       = "noted.haunted_property";
    public const string HoarderKey               = "noted.hoarder";
    public const string SuperstitiousKey         = "noted.superstitious";
    public const string LuckySpotKey             = "noted.lucky_spot";
    public const string CantWaitKey              = "noted.cant_wait";
    public const string EdgeLordKey              = "noted.edge_lord";
    public const string UnboxingVideoKey         = "noted.unboxing_video";
    public const string PreCraftedKey            = "noted.pre_crafted";
    public const string DejaVuKey                = "noted.deja_vu";
    public const string ProcrastinatorKey        = "noted.procrastinator";
    public const string ColdFeetKey              = "noted.cold_feet";
    public const string InfamousKey              = "noted.infamous";

    private static void RegisterNoted()
    {
        const AchievementCategory cat = AchievementCategory.Mischief;

        // -- Combat ---------------------------------------------------------------
        Reg(GeographyMajorKey, "Geography Major",
            "Killed a melee creature that never reached you.",
            cat, ap: 5, renown: 1,
            trigger: T.Count(TriggerKind.NeverReachedKills, 1),
            flavor:  "It never touched you. You know this terrain better than it does. The System is noting your rocks.",
            hidden:  true);

        Reg(RockEnthusiastKey, "Rock Enthusiast",
            "Killed 50 melee creatures that never reached you.",
            cat, ap: 15, renown: 2,
            trigger:         T.Count(TriggerKind.NeverReachedKills, 50),
            flavor:          "Fifty. You have a favorite rock now. Don't pretend you don't.",
            itemGumpId:      0x1363,
            hidden:          true,
            prerequisiteKey: GeographyMajorKey);

        Reg(TacticalRetreatKey, "Tactical Retreat",
            "Recalled or gated out of a fight with under a tenth of your hits.",
            cat, ap: 5, renown: 1,
            trigger: T.Count(TriggerKind.LowHpRetreats, 1),
            flavor:  "Not running. Relocating. The System has updated your file to say 'relocating'.",
            hidden:  true);

        Reg(ProfessionalCowardKey, "Professional Coward",
            "Recalled or gated out of a fight with under a tenth of your hits, 25 times.",
            cat, ap: 15, renown: 2,
            trigger:         T.Count(TriggerKind.LowHpRetreats, 25),
            flavor:          "Twenty-five escapes. At this point the monsters have a name for you. It isn't a nice one.",
            itemGumpId:      0x1515,
            hidden:          true,
            prerequisiteKey: TacticalRetreatKey);

        Reg(NotMyProblemKey, "Not My Problem",
            "A creature you provoked killed another, and you never laid a finger on it.",
            cat, ap: 5, renown: 1,
            trigger: T.Count(TriggerKind.ProvokedKills, 1),
            flavor:  "You started it. You didn't finish it. You watched. The System respects the management style.",
            hidden:  true);

        Reg(SomebodyElsesProblemKey, "Somebody Else's Problem",
            "Creatures you provoked killed 100 others without your help.",
            cat, ap: 15, renown: 2,
            trigger:         T.Count(TriggerKind.ProvokedKills, 100),
            flavor:          "A hundred feuds, and your hands are clean. Have some popcorn. You've earned it.",
            hidden:          true,
            prerequisiteKey: NotMyProblemKey);

        Reg(LastHitLarryKey, "Last Hit Larry",
            "Landed the killing blow on a boss after doing under 1% of its damage.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.LastHitLarry),
            flavor:  "Everyone else did the work. You did the last hit. The System has seen this before. They're all named Larry.",
            hidden:  true);

        Reg(AcceptableLossesKey, "Acceptable Losses",
            "Your pet died while you were at full health.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.PetDiedAtFullHp, 1),
            flavor:  "Not a scratch on you. Which is more than can be said for the pet.",
            hidden:  true);

        Reg(MeatShieldKey, "Meat Shield Procurement",
            "Ten of your pets died while you were at full health.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.PetDiedAtFullHp, 10),
            flavor:          "Ten. The stablemasters have started a list. You are the whole list.",
            hidden:          true,
            prerequisiteKey: AcceptableLossesKey);

        Reg(CommitmentIssuesKey, "Commitment Issues",
            "Released a pet and tamed it again within five minutes.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.RetameAfterRelease, 1),
            flavor:  "You set it free. Then you took it back. It's fine. It's not weird. It's a little weird.",
            hidden:  true);

        Reg(ItsComplicatedKey, "It's Complicated",
            "Released a pet and tamed it again within five minutes, 10 times.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.RetameAfterRelease, 10),
            flavor:          "Ten times. The pet has started bringing a bag.",
            hidden:          true,
            prerequisiteKey: CommitmentIssuesKey);

        Reg(RemoteWorkKey, "Remote Work",
            "Your pet made a kill while you were hidden or far away.",
            cat, ap: 5, renown: 1,
            trigger: T.Event(TriggerKind.RemotePetKills),
            flavor:  "Your pet did the work. You were 'available on chat'. The System has been to these meetings.",
            hidden:  true);

        Reg(BareMinimumKey, "Bare Minimum",
            "Landed the killing blow on a boss in armor and bare feet.",
            cat, ap: 10, renown: 2,
            trigger: T.Event(TriggerKind.BarefootBossKills),
            flavor:  "Full armor. No shoes. The boss's last sight was your toes. Let that sink in.",
            hidden:  true);

        // -- Crafting ------------------------------------------------------------
        Reg(BuiltToBeRecycledKey, "Built to Be Recycled",
            "Recycled something you made less than a minute after making it.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.FreshRecycles, 1),
            flavor:  "It existed for less than a minute. It had a good life. Mostly the 'existed' part.",
            hidden:  true);

        Reg(CircularEconomyKey, "Circular Economy",
            "Recycled 1,000 things you made less than a minute after making them.",
            cat, ap: 15, renown: 2,
            trigger:         T.Count(TriggerKind.FreshRecycles, 1000),
            flavor:          "Ore to item to ore. A thousand times. The ore is tired.",
            hidden:          true,
            prerequisiteKey: BuiltToBeRecycledKey);

        Reg(SkillIssueKey, "Skill Issue",
            "Failed to make the same item 10 times in a row.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.CraftFailStreak, 10),
            flavor:  "Ten in a row. The materials would like a word.",
            hidden:  true);

        Reg(PersistenceKey, "Persistence Is Not a Strategy",
            "Failed to make the same item 25 times in a row.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.CraftFailStreak, 25),
            flavor:          "Twenty-five. You got a trophy. Everyone gets a trophy.",
            hidden:          true,
            prerequisiteKey: SkillIssueKey);

        Reg(QualityControlKey, "Quality Control",
            "Recycled an exceptional item less than ten seconds after making it.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.ExceptionalDestroyed),
            flavor:  "Exceptional. Inspected. Rejected. Your standards are either very high or very confusing.",
            hidden:  true);

        Reg(RockBottomKey, "Rock Bottom",
            "Swung 20 times in a row at an empty vein.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.DepletedSwingStreak, 20),
            flavor:  "There's no more ore. There was no more ore nineteen swings ago.",
            hidden:  true);

        Reg(StillNothingKey, "Still Nothing",
            "Swung 100 times in a row at an empty vein.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.DepletedSwingStreak, 100),
            flavor:          "A hundred. The rock has not changed its mind. Here, have a rock.",
            hidden:          true,
            prerequisiteKey: RockBottomKey);

        // -- Travel and moving about ---------------------------------------------
        Reg(AnyPercentKey, "Any% Speedrun",
            "Recalled 10 times in a minute.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.RecallSpree),
            flavor:  "Ten recalls in sixty seconds. The System is dizzy just reading the log.",
            hidden:  true);

        Reg(WrongTurnKey, "Wrong Turn",
            "Died within ten seconds of arriving by recall or gate.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.DiedAfterArrival, 1),
            flavor:  "You arrived. You looked around. You died. A complete trip.",
            hidden:  true);

        Reg(DirectionallyChallengedKey, "Directionally Challenged",
            "Died within ten seconds of arriving by recall or gate, 10 times.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.DiedAfterArrival, 10),
            flavor:          "Ten round trips to the healer, by way of somewhere else.",
            hidden:          true,
            prerequisiteKey: WrongTurnKey);

        Reg(RuneHoarderKey, "Rune Hoarder",
            "Filled a runebook.",
            cat, ap: 5, renown: 1,
            trigger: T.Count(TriggerKind.RunebooksFilled, 1),
            flavor:  "Sixteen places you could be. You will go to two of them.",
            hidden:  true);

        Reg(RuneArchivistKey, "Rune Archivist",
            "Filled 10 runebooks.",
            cat, ap: 15, renown: 2,
            trigger:         T.Count(TriggerKind.RunebooksFilled, 10),
            flavor:          "A hundred and sixty places. The System could not find your house on any of them.",
            itemGumpId:      0x22C5,
            hidden:          true,
            prerequisiteKey: RuneHoarderKey);

        Reg(AmbitionKey, "Ambition Exceeds Capacity",
            "Walked 100 steps while overweight.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.OverweightSteps, 100),
            flavor:  "You can carry it. You can't carry it well. The System is noting the grunting.",
            hidden:  true);

        Reg(BeastOfBurdenKey, "Beast of Burden",
            "Walked 1,000 steps while overweight.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.OverweightSteps, 1000),
            flavor:          "A thousand steps. The pack animals are taking notes on your technique.",
            hidden:          true,
            prerequisiteKey: AmbitionKey);

        Reg(MuleTrainKey, "Mule Train",
            "Walked with three or more nearly full pack animals following you.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.MuleTrain),
            flavor:  "You are not an adventurer. You are a logistics company.",
            hidden:  true);

        Reg(HotFootKey, "Hot Foot",
            "Walked 25 tiles of lava or fire barefoot, in armor.",
            cat, ap: 10, renown: 1,
            trigger: T.Count(TriggerKind.HotFootTiles, 25),
            flavor:  "You wore armor everywhere except the part touching the lava. Bold. Wrong, but bold.",
            hidden:  true);

        Reg(FrostbittenKey, "Frostbitten",
            "Walked 500 tiles of snow barefoot, in armor.",
            cat, ap: 10, renown: 1,
            trigger: T.Count(TriggerKind.SnowTiles, 500),
            flavor:  "Five hundred tiles of snow. You can no longer feel your toes. The System can no longer feel sorry.",
            hidden:  true);

        Reg(PantslessWonderKey, "Pantsless Wonder",
            "Walked 1,000 tiles in armor and boots, with nothing on your legs.",
            cat, ap: 10, renown: 1,
            trigger: T.Count(TriggerKind.PantslessTiles, 1000),
            flavor:  "Plate on top. Boots on the bottom. In between, a bold choice.",
            hidden:  true);

        // -- Housing --------------------------------------------------------------
        Reg(HomeSweetGraveKey, "Home Sweet Grave",
            "Died in your own house.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.DiedAtHome, 1),
            flavor:  "Of all the places to die, you picked the one with a lock on the door.",
            hidden:  true);

        Reg(HauntedPropertyKey, "Haunted Property",
            "Died in your own house 5 times.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.DiedAtHome, 5),
            flavor:          "Five times. The resale value is not what it was.",
            hidden:          true,
            prerequisiteKey: HomeSweetGraveKey);

        Reg(HoarderKey, "Hoarder",
            "Filled your house's storage to the last slot.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.HouseStorageFull),
            flavor:  "Every slot. Every shelf. You own a house so your things can have a house.",
            hidden:  true);

        Reg(SuperstitiousKey, "Superstitious",
            "Opened 5 loot boxes in a row from the same spot in your house.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.SameTileBoxStreak, 5),
            flavor:  "Same tile. Every time. The System checked: the tile is not lucky.",
            hidden:  true);

        Reg(LuckySpotKey, "Lucky Spot",
            "Opened 25 loot boxes in a row from the same spot in your house.",
            cat, ap: 15, renown: 1,
            trigger:         T.Count(TriggerKind.SameTileBoxStreak, 25),
            flavor:          "Twenty-five. Fine. The tile is lucky now. You've worn a groove in it.",
            hidden:          true,
            prerequisiteKey: SuperstitiousKey);

        // -- Loot boxes ------------------------------------------------------------
        Reg(CantWaitKey, "Can't Wait",
            "Tried to open a loot box somewhere it won't open, 10 times.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.BoxTooSoon, 10),
            flavor:  "Ten times. It still won't open there. It will never open there. Here, have a box. Just a box.",
            hidden:  true);

        Reg(EdgeLordKey, "Edge Lord",
            "Opened a loot box a step from the edge of the guards' reach.",
            cat, ap: 5, renown: 1,
            trigger: T.Event(TriggerKind.EdgeBox),
            flavor:  "One more step and the guards stop caring. You knew exactly where that step was.",
            hidden:  true);

        Reg(UnboxingVideoKey, "Unboxing Video",
            "Opened a loot box with another player watching.",
            cat, ap: 5, renown: 1,
            trigger: T.Event(TriggerKind.AudienceBox),
            flavor:  "Don't forget to like and subscribe. The System doesn't know what that means either.",
            hidden:  true);

        // -- Commissions -----------------------------------------------------------
        Reg(PreCraftedKey, "Pre-Crafted",
            "Accepted a commission and turned it in within a minute.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.QuickCommission),
            flavor:  "They asked. You already had one. The System would like to know how long it was in your pack.",
            hidden:  true);

        // -- Data only: nothing raises these yet (the missing hook is named in the notes) ------------------------
        Reg(MiddleManagementKey, "Middle Management",
            "Your companion landed the killing blow on a boss you fought.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.CompanionBossKill),
            flavor:  "You delegated. They delivered. You'll be taking the credit in the meeting.",
            hidden:  true);

        Reg(DejaVuKey, "Deja Vu",
            "Regained a previous loop's skill peak within a day of looping.",
            cat, ap: 10, renown: 1,
            trigger: T.Event(TriggerKind.PeakRegained),
            flavor:  "You've been here before. You were exactly this good. The System has the receipts.",
            hidden:  true);

        Reg(ProcrastinatorKey, "Procrastinator",
            "Spent 7 days at the level cap with a full experience bar, without looping.",
            cat, ap: 10, renown: 1,
            trigger: T.Count(TriggerKind.ProcrastinatedDays, 7),
            flavor:  "The loop will still be there tomorrow. You said that seven days ago.",
            hidden:  true);

        Reg(ColdFeetKey, "Cold Feet",
            "Started the League loop and backed out, 3 times.",
            cat, ap: 10, renown: 0,
            trigger: T.Count(TriggerKind.LoopBackedOut, 3),
            flavor:  "You walked up to the altar. Three times. You walked back three times. The League has stopped printing the forms.",
            hidden:  true);

        Reg(InfamousKey, "Infamous",
            "Let 5 commissions expire.",
            cat, ap: 5, renown: 0,
            trigger: T.Count(TriggerKind.CommissionsExpired, 5),
            flavor:  "Five patrons waited. Five patrons left. Word gets around.",
            hidden:  true);
    }

    /// <summary>The counter a Noted kind keeps on the account (saved with the other achievement counters).</summary>
    public static string NotedCounter(TriggerKind kind) => "noted_" + kind.ToString().ToLowerInvariant();

    /// <summary>cc-P263: one Noted event: counts it and grants every tier of that kind the count has reached.</summary>
    public static void NotifyNoted(PlayerMobile pm, TriggerKind kind)
    {
        if (pm.Account is not IAccount acct) return;
        GrantCounted(acct, kind, Increment(acct.Username, NotedCounter(kind)));
    }

    /// <summary>cc-P263: a streak kind: grants every tier the current streak has reached (the counter keeps the best).</summary>
    public static void NotifyNotedStreak(PlayerMobile pm, TriggerKind kind, int streak)
    {
        if (pm.Account is not IAccount acct) return;
        if (streak > GetCounter(acct.Username, NotedCounter(kind)))
            SetCounter(acct.Username, NotedCounter(kind), streak);
        GrantCounted(acct, kind, streak);
    }

    /// <summary>
    /// cc-P263: marks <paramref name="key"/> once per account; true the first time (a runebook is filled once). cc-P457
    /// (cc-P402 F09): a marker, set to 1, where it was a counter that every refill of the same book raised.
    /// </summary>
    public static bool FirstTimeFor(PlayerMobile pm, string key)
    {
        if (pm.Account is not IAccount acct || GetCounter(acct.Username, key) > 0)
            return false;

        SetCounter(acct.Username, key, 1);
        return true;
    }
}

/// <summary>One achievement's reward (cc-P263). A joke item makes its earn box a Dented Box; a title is added on earn.</summary>
public sealed record NotedReward(string AchievementKey, Type? JokeItem = null, string? TitleKey = null, int BoxTier = 1);

/// <summary>cc-P263: the reward table, for Chase to tune. An achievement not listed gets its usual earn box.</summary>
public static class NotedRewards
{
    public const string LarryTitle      = "noted-larry";
    public const string SupervisorTitle = "noted-supervisor";
    public const string BraveTitle      = "noted-brave";

    public static readonly NotedReward[] Table =
    [
        new(ClusterFAchievementSystem.HabitKey, typeof(LuckyUnderwear)), // P82's Creature of Habit: the list's Lucky Underwear
        new(ClusterFAchievementSystem.RockEnthusiastKey, typeof(StrategicBoulder)),
        new(ClusterFAchievementSystem.ProfessionalCowardKey, typeof(CloakOfHastyDeparture), BraveTitle),
        new(ClusterFAchievementSystem.SomebodyElsesProblemKey, typeof(BucketOfPopcorn)),
        new(ClusterFAchievementSystem.LastHitLarryKey, TitleKey: LarryTitle),
        new(ClusterFAchievementSystem.MeatShieldKey, typeof(OkayestOwnerNameplate)),
        new(ClusterFAchievementSystem.MiddleManagementKey, typeof(SquiresNecktie), SupervisorTitle),
        new(ClusterFAchievementSystem.ItsComplicatedKey, typeof(FriendshipBracelet)),
        new(ClusterFAchievementSystem.PersistenceKey, typeof(ParticipationTrophy)),
        new(ClusterFAchievementSystem.StillNothingKey, typeof(TheRock)),
        new(ClusterFAchievementSystem.AnyPercentKey, typeof(DizzyRunebook)),
        new(ClusterFAchievementSystem.DirectionallyChallengedKey, typeof(SouvenirToeTag)),
        new(ClusterFAchievementSystem.MuleTrainKey, typeof(MulesSunhat)),
        new(ClusterFAchievementSystem.HauntedPropertyKey, typeof(PersonalizedTombstone)),
        new(ClusterFAchievementSystem.LuckySpotKey, typeof(LuckyRug)),
        new(ClusterFAchievementSystem.CantWaitKey, typeof(BoxShapedBox)),
        new(ClusterFAchievementSystem.UnboxingVideoKey, typeof(BoxOfSmallerBoxes)),
        new(ClusterFAchievementSystem.ProcrastinatorKey, typeof(HourglassOfEventually)),
    ];

    public static NotedReward? For(string key)
    {
        foreach (var r in Table)
            if (r.AchievementKey == key) return r;
        return null;
    }
}

/// <summary>cc-P263: the hooks the patches, our own code and the events call.</summary>
public static class ClusterFNotedBySystem
{
    // The list's numbers, in one place.
    public const double RetreatHitsFraction = 0.10;
    public const int    RemoteWorkRange     = 15;
    public const double LarryDamageShare    = 0.01;
    public static readonly TimeSpan RetameWindow        = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan FreshRecycleWindow  = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan QualityControlWindow = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan SpeedrunWindow      = TimeSpan.FromSeconds(60);
    public const int    SpeedrunRecalls     = 10;
    public static readonly TimeSpan WrongTurnWindow     = TimeSpan.FromSeconds(10);
    public const int    MuleTrainAnimals    = 3;
    public const double MuleNearlyFull      = 0.90;
    public static readonly TimeSpan PreCraftedWindow    = TimeSpan.FromSeconds(60);
    public const int    EdgeLordRange       = 1;
    public const int    UnboxingRange       = 3;

    private static void Notify(Mobile? m, TriggerKind kind)
    {
        if (m is PlayerMobile { Deleted: false } pm)
            ClusterFAchievementSystem.NotifyNoted(pm, kind);
    }

    // -- Barefoot and armor ------------------------------------------------------

    /// <summary>No shoes, boots or sandals worn.</summary>
    public static bool IsBarefoot(Mobile m) => m.FindItemOnLayer(Layer.Shoes) == null;

    private static bool Wears(Mobile m, ArmorBodyType position)
    {
        foreach (var item in m.Items)
            if (item is BaseArmor a && a.BodyPosition == position) return true;
        return false;
    }

    /// <summary>"In armor", for the barefoot set: an armor chest piece on.</summary>
    public static bool IsArmored(Mobile m) => Wears(m, ArmorBodyType.Chest);

    // -- Creature deaths ---------------------------------------------------------

    private static bool IsMelee(BaseCreature bc) =>
        bc.AI is AIType.AI_Melee or AIType.AI_Animal or AIType.AI_Berserk or AIType.AI_Predator &&
        bc.Weapon is not BaseRanged;

    // Which event: pinned deletes an NPC inside Mobile.OnDeath (Mobile.cs: "if (!m_Player) Delete()") BEFORE BaseCreature
    // raises CreatureDeathEvent, and BaseCreature.OnDelete clears its master. By CreatureDeathEvent a dead pet has no master
    // and every creature reads Deleted, so ClusterFLootBoxes.IsBoss (ClusterFRareSelfRepair.IsHighEndSource wants it not
    // deleted) is false for every boss. CreatureDeletedEvent comes first, from OnDelete, with the master, the damage
    // entries and LastKiller still there; a creature deleted there with no hits left is one dying (Mobile.Kill sets Hits
    // to 0 before OnDeath). A bonded pet is not deleted when it dies: it keeps its own CreatureDeathEvent below.

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc)
    {
        if (bc.Hits <= 0 && !bc.IsDeadPet)
            OnCreatureDied(bc);

        // cc-P427: a creature that is gone is forgotten (findings 3 and 4 of cc-P379).
        _damagedPlayers.Remove(bc);
        _hitBy.Remove(bc);
        _released.Remove(bc);
    }

    // -- Who a creature has reached (cc-P427, cc-P379 finding 3) ---------------------------------------------------
    //
    // "Never reached you" was the player's own damage list, which pinned empties whenever the player's hits are full
    // (Mobile.cs:2043) and expires 2 minutes after a hit (Mobile.cs:55-59): a creature that hit you before you healed to
    // full counted as never reaching you. This keeps its own record, from PlayerMobile.OnDamage
    // (PlayerMobile-noted-damage-memory.patch), where pinned's Mobile.Damage fills that list (Mobile.cs:5983, :6009),
    // and forgets a creature when it is deleted (dead or not).

    private static readonly Dictionary<BaseCreature, HashSet<Mobile>> _damagedPlayers = new();

    /// <summary>A hit on <paramref name="pm"/> (PlayerMobile.OnDamage). Remembers the creature that dealt it.</summary>
    public static void PlayerDamaged(PlayerMobile pm, Mobile from)
    {
        if (from is not BaseCreature { Deleted: false } bc || pm == null)
            return;

        if (!_damagedPlayers.TryGetValue(bc, out var players))
            _damagedPlayers[bc] = players = new HashSet<Mobile>();
        players.Add(pm);
    }

    /// <summary>Whether <paramref name="bc"/> has ever damaged <paramref name="pm"/> while it lived.</summary>
    public static bool HasReached(BaseCreature bc, Mobile pm) =>
        _damagedPlayers.TryGetValue(bc, out var players) && players.Contains(pm);

    // cc-P457 (cc-P402 F01, the provoked half): the other direction. "You never touched it" was the victim's own damage
    // list, which pinned empties when the victim heals to full (Mobile.cs:2043) and prunes after 2 minutes (:5880): a bard
    // who hit the creature and let it heal counted as never touching it. BaseCreature.RegisterDamage
    // (BaseCreature-noted-damage-memory.patch) reports every damage the list would have taken; this keeps the players,
    // forgotten with the creature.

    private static readonly Dictionary<BaseCreature, HashSet<Mobile>> _hitBy = new();

    /// <summary>A hit on <paramref name="bc"/> (BaseCreature.RegisterDamage). Remembers a player who dealt it.</summary>
    public static void CreatureDamaged(BaseCreature bc, Mobile from)
    {
        if (from is not PlayerMobile pm || bc == null || bc.Deleted)
            return;

        if (!_hitBy.TryGetValue(bc, out var players))
            _hitBy[bc] = players = new HashSet<Mobile>();
        players.Add(pm);
    }

    /// <summary>Whether <paramref name="pm"/> has ever damaged <paramref name="bc"/> while it lived.</summary>
    public static bool HasHit(Mobile pm, BaseCreature bc) =>
        _hitBy.TryGetValue(bc, out var players) && players.Contains(pm);

    /// <summary>For the tests: creatures with a record of players they damaged, and released pets remembered.</summary>
    internal static int ReachedCount => _damagedPlayers.Count;

    internal static bool RemembersRelease(BaseCreature bc) => _released.ContainsKey(bc);

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (bc.IsBonded && bc.Controlled)
            OnCreatureDied(bc);
    }

    /// <summary>One creature's death, with its master and its damage still on it.</summary>
    public static void OnCreatureDied(BaseCreature bc)
    {
        var killer = bc.LastKiller;

        // Your pet died while you were untouched.
        if (bc.Controlled && bc.ControlMaster is PlayerMobile { Alive: true } owner && owner.Hits >= owner.HitsMax)
            Notify(owner, TriggerKind.PetDiedAtFullHp);

        if (bc.Controlled || bc.Summoned) return;

        if (killer is PlayerMobile pm)
        {
            if (IsMelee(bc) && !HasReached(bc, pm) && pm.FindDamageEntryFor(bc) == null)
                Notify(pm, TriggerKind.NeverReachedKills);

            if (ClusterFLootBoxes.IsBoss(bc))
            {
                if (DamageShare(bc, pm) < LarryDamageShare)
                    Notify(pm, TriggerKind.LastHitLarry);

                if (IsBarefoot(pm) && IsArmored(pm))
                    Notify(pm, TriggerKind.BarefootBossKills);
            }
        }
        else if (killer is BaseCreature { BardProvoked: true, BardMaster: PlayerMobile bard } && !HasHit(bard, bc) &&
                 bc.FindDamageEntryFor(bard) == null)
        {
            Notify(bard, TriggerKind.ProvokedKills);
        }
        else if (killer is BaseCreature { Controlled: true, ControlMaster: PlayerMobile master } pet &&
                 (master.Hidden || master.Map != pet.Map || !master.InRange(pet.Location, RemoteWorkRange - 1)))
        {
            Notify(master, TriggerKind.RemotePetKills);
        }
    }

    /// <summary>The share of <paramref name="bc"/>'s recorded damage that <paramref name="m"/> did (0 to 1).</summary>
    public static double DamageShare(BaseCreature bc, Mobile m)
    {
        long total = 0, theirs = 0;
        foreach (var de in bc.DamageEntries)
        {
            total += de.DamageGiven;
            if (de.Damager == m) theirs += de.DamageGiven;
        }

        return total <= 0 ? 1.0 : (double)theirs / total;
    }

    // -- Pets: released and tamed again ---------------------------------------------

    private static readonly Dictionary<BaseCreature, (Mobile Master, long At)> _released = new();

    /// <summary>A pet released to the wild by <paramref name="master"/> (PetOrders.IssueRelease, before the master is cleared).</summary>
    public static void Released(BaseCreature bc, Mobile? master)
    {
        if (master is not PlayerMobile)
            return;

        // cc-P427 (cc-P379 finding 4): a release past the retame window can never count, so it is dropped here; a deleted
        // pet is dropped in OnCreatureDeleted. Before, every released pet stayed until it was tamed again.
        var now = Core.TickCount;
        var window = (long)RetameWindow.TotalMilliseconds;
        List<BaseCreature>? stale = null;
        foreach (var (pet, rel) in _released)
        {
            if (pet.Deleted || now - rel.At > window)
                (stale ??= new List<BaseCreature>()).Add(pet);
        }

        stale?.ForEach(pet => _released.Remove(pet));
        _released[bc] = (master, now);
    }

    /// <summary>A creature tamed (PetTrainingHooks.OnTamed). Counts when its releaser tames it back inside the window.</summary>
    public static void Tamed(BaseCreature bc, Mobile tamer)
    {
        if (_released.Remove(bc, out var rel) && rel.Master == tamer &&
            Core.TickCount - rel.At <= (long)RetameWindow.TotalMilliseconds)
            Notify(tamer, TriggerKind.RetameAfterRelease);
    }

    // -- Travel --------------------------------------------------------------------

    private static readonly Dictionary<Mobile, long> _arrivedAt = new();
    private static readonly Dictionary<Mobile, Queue<long>> _recalls = new();

    private static bool InCombat(Mobile m) => m.Combatant != null || m.Aggressors.Count > 0 || m.Aggressed.Count > 0;

    /// <summary>Just after a recall (<paramref name="byGate"/> false) or a gate (true) has moved <paramref name="m"/>.</summary>
    public static void Arrived(Mobile m, bool byGate)
    {
        if (m is not PlayerMobile pm) return;

        var now = Core.TickCount;
        _arrivedAt[pm] = now;

        if (pm.Alive && pm.Hits < pm.HitsMax * RetreatHitsFraction && InCombat(pm))
            Notify(pm, TriggerKind.LowHpRetreats);

        if (byGate) return;

        if (!_recalls.TryGetValue(pm, out var times))
            _recalls[pm] = times = new Queue<long>();
        times.Enqueue(now);
        while (times.Count > 0 && now - times.Peek() > (long)SpeedrunWindow.TotalMilliseconds)
            times.Dequeue();
        if (times.Count >= SpeedrunRecalls)
            Notify(pm, TriggerKind.RecallSpree);
    }

    /// <summary>A rune dropped into <paramref name="book"/> filled it (Runebook.OnDragDrop). Each book counts once.</summary>
    public static void RunebookFilled(Mobile from, Item book)
    {
        // cc-P457 (cc-P402 F09): once Rune Archivist is earned no book can add anything, so no marker is written.
        if (from is not PlayerMobile { Account: IAccount acct } pm ||
            ClusterFAchievementSystem.HasEarned(acct, ClusterFAchievementSystem.RuneArchivistKey))
            return;

        if (ClusterFAchievementSystem.FirstTimeFor(pm, $"noted_runebook_{book.Serial.Value}"))
            Notify(pm, TriggerKind.RunebooksFilled);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (_arrivedAt.Remove(pm, out var at) && Core.TickCount - at <= (long)WrongTurnWindow.TotalMilliseconds)
            Notify(pm, TriggerKind.DiedAfterArrival);

        if (BaseHouse.FindHouseAt(pm) is { } house && (house.IsOwner(pm) || house.IsCoOwner(pm)))
            Notify(pm, TriggerKind.DiedAtHome);
    }

    // -- Crafting and mining ----------------------------------------------------------

    private static readonly Dictionary<Mobile, (Type Item, int Fails)> _failStreak = new();

    /// <summary>
    /// One craft attempt's result (CraftItem.CompleteCraft, beside pinned's ending effect). A success passes the item
    /// made, whose crafter is remembered (cc-P457).
    /// </summary>
    public static void CraftAttempt(Mobile from, Type itemType, bool failed, Item? made = null)
    {
        if (from is not PlayerMobile pm) return;

        if (!failed)
        {
            _failStreak.Remove(pm);
            if (made != null)
                Crafted(pm, made);
            return;
        }

        var fails = _failStreak.TryGetValue(pm, out var s) && s.Item == itemType ? s.Fails + 1 : 1;
        _failStreak[pm] = (itemType, fails);
        ClusterFAchievementSystem.NotifyNotedStreak(pm, TriggerKind.CraftFailStreak, fails);
    }

    private static bool IsExceptional(Item item) => item switch
    {
        BaseWeapon w   => w.Quality == WeaponQuality.Exceptional,
        BaseArmor a    => a.Quality == ArmorQuality.Exceptional,
        BaseClothing c => c.Quality == ClothingQuality.Exceptional,
        _              => false
    };

    // cc-P457 (cc-P402 F07; Chase, 10-22): "something you made" is the recycler's own craft. Pinned records the maker
    // only with a maker's mark (Crafter, a name), and a fresh item can change hands inside the minute, so the crafter is
    // remembered here when the craft succeeds. In memory only: the windows are a minute at most, and a restart forgets
    // nothing that could still count. Weak keys: nothing is kept alive by it.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Item, Mobile> _crafter = new();

    /// <summary>A craft by <paramref name="pm"/> made <paramref name="item"/>.</summary>
    public static void Crafted(PlayerMobile pm, Item item) => _crafter.AddOrUpdate(item, pm);

    /// <summary>Who crafted <paramref name="item"/> since the server started, or null.</summary>
    public static Mobile? CrafterOf(Item item) => _crafter.TryGetValue(item, out var m) ? m : null;

    // Crafted by a player, and by this one.
    private static bool IsCraftedBy(Item item, PlayerMobile pm) =>
        item switch
        {
            BaseWeapon w   => w.PlayerConstructed,
            BaseArmor a    => a.PlayerConstructed,
            BaseClothing c => c.PlayerConstructed,
            _              => false
        } && CrafterOf(item) == pm;

    /// <summary>Just before a recycle (smelt, salvage) deletes <paramref name="item"/>.</summary>
    public static void Recycled(Mobile from, Item item)
    {
        if (from is not PlayerMobile pm || !IsCraftedBy(item, pm)) return;

        var age = Core.Now - item.Created;
        if (age <= FreshRecycleWindow)
            Notify(pm, TriggerKind.FreshRecycles);
        if (age <= QualityControlWindow && IsExceptional(item))
            Notify(pm, TriggerKind.ExceptionalDestroyed);
    }

    private static readonly Dictionary<Mobile, (Map Map, Point3D Loc, int Swings)> _emptySwings = new();

    /// <summary>A mining swing refused because the vein at <paramref name="loc"/> is empty (Mining.CheckResources).</summary>
    public static void SwungAtEmptyVein(Mobile from, Map map, Point3D loc)
    {
        if (from is not PlayerMobile pm) return;

        var swings = _emptySwings.TryGetValue(pm, out var s) && s.Map == map && s.Loc.X == loc.X && s.Loc.Y == loc.Y
            ? s.Swings + 1
            : 1;
        _emptySwings[pm] = (map, loc, swings);
        ClusterFAchievementSystem.NotifyNotedStreak(pm, TriggerKind.DepletedSwingStreak, swings);
    }

    /// <summary>
    /// cc-P457 (cc-P402 F02): a mining swing at a vein with ore (Mining.CheckResources). Ends the empty-vein streak, so
    /// empty at A, ore at B and empty at A again is a new streak of one.
    /// </summary>
    public static void SwungAtOre(Mobile from)
    {
        if (from is PlayerMobile pm)
            _emptySwings.Remove(pm);
    }

    // -- Steps -----------------------------------------------------------------------

    public enum Ground
    {
        Other,
        Lava,
        Fire,
        Snow
    }

    /// <summary>Pinned's step height (Movement.StepHeight): how far from the feet a field or an overlay still touches them.</summary>
    public const int StepHeight = 2;

    /// <summary>
    /// What a player standing at <paramref name="z"/> on (<paramref name="x"/>, <paramref name="y"/>) has underfoot, for
    /// the barefoot set. cc-P457 (cc-P402 F03): it was any land or static of the tile, at any height, so snow land under
    /// a floor, or lava a storey below, counted. Now, in order:
    ///   a fire field, or a passable lava or snow tile that is not a surface (pinned's lava flows, e.g. 0x136E), within
    ///   step height of the feet;
    ///   else the highest walkable static or multi surface at or below the feet, by its name (a wooden floor is Other);
    ///   else the land, which counts only when no such surface is there.
    /// Lava land (0x1F4-0x1F7) and lava pools are impassable, so nobody stands on them.
    /// </summary>
    public static Ground GroundAt(Map? map, int x, int y, int z)
    {
        if (map == null || map == Map.Internal) return Ground.Other;

        foreach (var item in map.GetItemsAt(x, y))
            if (item is FireFieldItem && Math.Abs(item.Z - z) <= StepHeight) return Ground.Fire;

        var floorTop = int.MinValue;
        string? floor = null;
        var overlay = Ground.Other;

        // True when the tile is an overlay at the feet, which decides the step.
        bool AtFeet(StaticTile tile)
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            if (data.Impassable)
                return false;

            if (!data.Surface)
            {
                overlay = Math.Abs(tile.Z - z) <= StepHeight ? Classify(data.Name) : Ground.Other;
                return overlay != Ground.Other;
            }

            var top = tile.Z + data.CalcHeight;
            if (top <= z && top > floorTop)
            {
                floorTop = top;
                floor = data.Name;
            }

            return false;
        }

        int landId;
        if (TilesFor != null)
        {
            var (land, statics) = TilesFor(map, x, y);
            landId = land.ID;
            foreach (var tile in statics)
                if (AtFeet(tile)) return overlay;
        }
        else
        {
            landId = map.Tiles.GetLandTile(x, y).ID;
            foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
                if (AtFeet(tile)) return overlay;
        }

        return floor != null ? Classify(floor) : Classify(TileData.LandTable[landId & TileData.MaxLandValue].Name);
    }

    /// <summary>
    /// For the facts only: the land and statics of a tile in place of the map's. The test host has no map files, and
    /// pinned's TileMatrix ignores a block set without them (GetLandBlock returns the invalid block). Null in play.
    /// </summary>
    public static Func<Map, int, int, (LandTile Land, StaticTile[] Statics)>? TilesFor { get; set; }

    private static Ground Classify(string? name) =>
        name == null ? Ground.Other
        : name.Contains("lava", StringComparison.OrdinalIgnoreCase) ? Ground.Lava
        : name.Contains("snow", StringComparison.OrdinalIgnoreCase) ? Ground.Snow
        : Ground.Other;

    private static readonly Dictionary<Mobile, int> _steps = new();

    /// <summary>
    /// cc-P457 (cc-P402 F03): a completed move (PlayerMobile.OnAfterMove, PlayerMobile-noted-step.patch). Pinned raises
    /// EventSink.Movement before SetLocation (Mobile.cs:4103, :4386), so the ground used to be read at the tile left
    /// behind; this runs after it. A turn in place leaves the location as it was and is no step.
    /// </summary>
    public static void PlayerMoved(PlayerMobile pm, Point3D oldLocation)
    {
        if (!pm.Alive || pm.AccessLevel != AccessLevel.Player || pm.Location == oldLocation)
            return;

        // cc-P402 F10: the ground only matters barefoot in armor on foot, so nobody else pays for the tile reads.
        var ground = !pm.Mounted && IsBarefoot(pm) && IsArmored(pm) ? GroundAt(pm.Map, pm.X, pm.Y, pm.Z) : Ground.Other;
        Stepped(pm, ground);
    }

    /// <summary>One step by <paramref name="pm"/>, standing on <paramref name="ground"/>.</summary>
    public static void Stepped(PlayerMobile pm, Ground ground)
    {
        if (Mobile.BodyWeight + pm.TotalWeight > pm.MaxWeight)
            Notify(pm, TriggerKind.OverweightSteps);

        if (!pm.Mounted && IsArmored(pm))
        {
            if (IsBarefoot(pm))
            {
                if (ground is Ground.Lava or Ground.Fire)
                    Notify(pm, TriggerKind.HotFootTiles);
                else if (ground == Ground.Snow)
                    Notify(pm, TriggerKind.SnowTiles);
            }
            else if (!Wears(pm, ArmorBodyType.Legs))
            {
                Notify(pm, TriggerKind.PantslessTiles);
            }
        }

        // The rarer checks every tenth step.
        _steps.TryGetValue(pm, out var n);
        _steps[pm] = ++n;
        if (n % 10 != 0) return;

        if (CountNearlyFullPackAnimals(pm) >= MuleTrainAnimals)
            Notify(pm, TriggerKind.MuleTrain);

        if (BaseHouse.FindHouseAt(pm) is { } house && house.IsOwner(pm) && IsStorageFull(house))
            Notify(pm, TriggerKind.HouseStorageFull);
    }

    public static bool IsPackAnimal(BaseCreature bc) => bc is PackHorse or PackLlama or PackMule;

    /// <summary>Pack animals following <paramref name="pm"/> with a pack at least nine tenths full, by weight or items.</summary>
    public static int CountNearlyFullPackAnimals(PlayerMobile pm)
    {
        var count = 0;
        if (pm.AllFollowers == null) return 0;

        foreach (var follower in pm.AllFollowers)
        {
            // cc-P457 (cc-P402 F06): InRange reads X and Y only (Mobile.cs:3076), so the map is checked too.
            if (follower is not BaseCreature { Controlled: true, ControlOrder: OrderType.Follow, IsDeadPet: false } bc ||
                !IsPackAnimal(bc) || bc.ControlMaster != pm || bc.Map != pm.Map || bc.Backpack is not { } pack ||
                !pm.InRange(bc.Location, 15))
                continue;

            if (pack.TotalWeight >= pack.MaxWeight * MuleNearlyFull || pack.TotalItems >= pack.MaxItems * MuleNearlyFull)
                count++;
        }

        return count;
    }

    /// <summary>No room left: secure storage under AOS rules (pinned's own CheckAosStorage), else lockdowns or secures.</summary>
    public static bool IsStorageFull(BaseHouse house) =>
        house.IsAosRules
            ? !house.CheckAosStorage(1)
            : house.LockDownCount >= house.MaxLockDowns || house.SecureCount >= house.MaxSecures;

    // -- Loot boxes ------------------------------------------------------------------

    /// <summary>A loot box refused because the player is not somewhere it opens (LootBox.OnDoubleClick).</summary>
    public static void BoxRefused(PlayerMobile pm) => Notify(pm, TriggerKind.BoxTooSoon);

    private static readonly Dictionary<Mobile, (Map Map, Point3D Loc, int Opened)> _boxSpot = new();

    /// <summary>A loot box opened by <paramref name="pm"/> where they stand (LootBox.Open).</summary>
    public static void BoxOpened(PlayerMobile pm)
    {
        if (BaseHouse.FindHouseAt(pm) is { } house && (house.IsOwner(pm) || house.IsCoOwner(pm)))
        {
            var opened = _boxSpot.TryGetValue(pm, out var s) && s.Map == pm.Map && s.Loc == pm.Location ? s.Opened + 1 : 1;
            _boxSpot[pm] = (pm.Map, pm.Location, opened);
            ClusterFAchievementSystem.NotifyNotedStreak(pm, TriggerKind.SameTileBoxStreak, opened);
        }
        else
        {
            _boxSpot.Remove(pm);
        }

        if (pm.Map == null || pm.Map == Map.Internal) return;

        if (IsAtGuardEdge(pm))
            Notify(pm, TriggerKind.EdgeBox);

        foreach (var m in pm.Map.GetMobilesInRange(pm.Location, UnboxingRange))
        {
            if (m != pm && m is PlayerMobile { Alive: true, Hidden: false })
            {
                Notify(pm, TriggerKind.AudienceBox);
                break;
            }
        }
    }

    private static bool IsGuarded(Map map, Point3D p) =>
        Region.Find(p, map).GetRegion<GuardedRegion>() is { } g && !g.IsDisabled();

    /// <summary>Inside guarded ground, with unguarded ground within <see cref="EdgeLordRange"/> tiles.</summary>
    public static bool IsAtGuardEdge(Mobile m)
    {
        var map = m.Map;
        if (map == null || map == Map.Internal || !IsGuarded(map, m.Location)) return false;

        for (var dx = -EdgeLordRange; dx <= EdgeLordRange; dx++)
            for (var dy = -EdgeLordRange; dy <= EdgeLordRange; dy++)
                if ((dx != 0 || dy != 0) && !IsGuarded(map, new Point3D(m.X + dx, m.Y + dy, m.Z)))
                    return true;

        return false;
    }

    // -- Commissions --------------------------------------------------------------------

    /// <summary>A commission issued at <paramref name="issuedAt"/> turned in now (SmithCommissionSystem.Complete).</summary>
    public static void CommissionCompleted(PlayerMobile pm, DateTime issuedAt)
    {
        if (Core.Now - issuedAt <= PreCraftedWindow)
            Notify(pm, TriggerKind.QuickCommission);
    }

    // -- Rewards --------------------------------------------------------------------------

    /// <summary>After an earn (ClusterFAchievementSystem.TryGrant): a title the reward table names.</summary>
    public static void OnEarned(IAccount acct, AchievementDef def)
    {
        if (NotedRewards.For(def.Key) is not { TitleKey: { } title }) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (!data.LeagueTitles.Owned.Add(title)) return;

        for (var i = 0; i < acct.Length; i++)
            if (acct[i] is PlayerMobile { NetState: not null } pm)
                pm.SendMessage(0x44, $"System: Title unlocked: \"{LeagueTitles.Display(title)}\". Choose it in Titles.");
    }

    /// <summary>The earn box of an achievement whose reward names a joke item becomes a Dented Box holding it.</summary>
    public static void Dent(LootBox box, PlayerMobile pm, NotedReward reward)
    {
        if (reward.JokeItem == null || Activator.CreateInstance(reward.JokeItem) is not Item joke) return;

        if (joke is INotedJoke noted)
            noted.Personalize(pm);

        box.Dent(joke);
    }
}
