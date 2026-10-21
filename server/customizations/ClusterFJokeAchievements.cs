// ClusterFJokeAchievements.cs
//
// cc-P231 (ours; Chase 2026-10-16: "that's a good start"). Hidden achievements for OSI's own easter-egg moments: running
// with scissors, meditating in plate, eating while full and the rest. Every one is in the Mischief category, hidden
// until earned, with a snarky flavor line and the plain "(Earned: ...)" line its trigger writes (D73).
//
// Each fires from the game event, never from message text. Where pinned raises no event, a one-line patch calls the
// hook below at the exact branch that refuses or reports (server/patches/*-achievement.patch); where pinned or our own
// code already has an event, it is used directly:
//
//   ScissorsRunning      Scissors-running-achievement.patch       Scissors.cs, the running refusal (1063305)
//   BeggingUntrustworthy Begging-untrustworthy-achievement.patch  Begging.cs, "Thou dost not look trustworthy" (500406)
//   MeditateInArmor      our Meditation.cs (a replacement)         "Regenerative forces cannot penetrate your armor!"
//   EatWhileFull         Food-too-full-achievement.patch          Food.FillHunger, "too full to eat" (500867)
//   HealUnhurt           Bandage-unhurt-patient-achievement.patch BandageContext.BeginHeal, "not damaged" (500955)
//   ToolsWornOut         CraftItem- and HarvestSystem-tool-worn-achievement.patch, where a worn-out tool is deleted
//   ConsiderSins         pinned's speech keyword 0x0032 (Misc/Keywords.cs:31), from EventSink.Speech
//   GhostSpeech          EventSink.Speech from a dead player (spoken aloud: regular, whisper or yell)
//   Hiccup               Beverage-hiccup-achievement.patch        the drunk timer's *hic* (500849)
//   KilledByGuards       PlayerDeathEvent, the last damager a BaseGuard (the instant kill damages first, BaseGuard.cs:320)
//   SelfDetonation       BaseExplosionPotion-self-blast-achievement.patch marks the thrower hitting themselves;
//                        PlayerDeathEvent then finds them as their own last damager
//   FishedFootwear       Fishing-shoes-achievement.patch          Fishing.SendSuccessTo, the BaseShoes branch
//   KilledByFowl         PlayerDeathEvent, the last damager a Chicken, Rabbit or JackRabbit
//
// Every achievement is earned once per account, as all achievements are, so a repeatable event pays at most its tiers.
// Notes: shard-migration/notes/cc-P231-osi-joke-achievements.md (the reward table and the extras proposed to Chase).

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Mobiles;
using Server.Network;

using T = Server.AchievementTrigger;

namespace Server;

public static partial class ClusterFAchievementSystem
{
    public const string SafetyThirdKey       = "joke.safety_third";
    public const string RunWithScissorsKey   = "joke.run_with_scissors";
    public const string UnsupervisedKey      = "joke.unsupervised";
    public const string NotTrustworthyKey    = "joke.not_trustworthy";
    public const string MeditatingInPlateKey = "joke.meditating_in_plate";
    public const string MisterCreosoteKey    = "joke.mister_creosote";
    public const string PlaceboKey           = "joke.placebo";
    public const string PlannedObsolescenceKey = "joke.planned_obsolescence";
    public const string SoulSearchingKey     = "joke.soul_searching";
    public const string BooKey               = "joke.boo";
    public const string HicKey               = "joke.hic";
    public const string InstantJusticeKey    = "joke.instant_justice";
    public const string SelfDetonationKey    = "joke.self_detonation";
    public const string SoleSurvivorKey      = "joke.sole_survivor";
    public const string SoleProprietorKey    = "joke.sole_proprietor";
    public const string FowlPlayKey          = "joke.fowl_play";

    private static void RegisterJokes()
    {
        const AchievementCategory cat = AchievementCategory.Mischief;

        // -- Run With Scissors (Chase's wording, kept exactly) --------------
        Reg(SafetyThirdKey,
            "Safety Third",
            "Tried to use scissors while running.",
            cat, ap: 5, renown: 1,
            trigger:    T.Count(TriggerKind.ScissorsRunning, 1),
            flavor:     "Your parents tried. The scissors tried. Nobody can help you.",
            itemGumpId: 0x0F9F, // Scissors
            hidden:     true);

        Reg(RunWithScissorsKey,
            "Run With Scissors",
            "Tried to use scissors while running, 25 times.",
            cat, ap: 10, renown: 2,
            trigger:         T.Count(TriggerKind.ScissorsRunning, 25),
            flavor:          "At this point it's a lifestyle.",
            itemGumpId:      0x0F9F,
            hidden:          true,
            prerequisiteKey: SafetyThirdKey);

        Reg(UnsupervisedKey,
            "Unsupervised",
            "Tried to use scissors while running, 100 times.",
            cat, ap: 15, renown: 0,
            trigger:         T.Count(TriggerKind.ScissorsRunning, 100),
            flavor:          "We've stopped telling you.",
            itemGumpId:      0x0F9F,
            hidden:          true,
            prerequisiteKey: RunWithScissorsKey);

        // -- The rest of Chase's list -----------------------------------------
        Reg(NotTrustworthyKey,
            "Not Trustworthy",
            "Begged, and was told you do not look trustworthy.",
            cat, ap: 5, renown: 0,
            trigger: T.Event(TriggerKind.BeggingUntrustworthy),
            flavor:  "Even the beggars' marks have standards. You are below them.",
            hidden:  true);

        Reg(MeditatingInPlateKey,
            "Meditating in Plate",
            "Tried to meditate in armor that will not let you.",
            cat, ap: 5, renown: 1,
            trigger:    T.Event(TriggerKind.MeditateInArmor),
            flavor:     "Inner peace, outer plate. The plate won.",
            itemGumpId: 0x1415, // PlateChest
            hidden:     true);

        Reg(MisterCreosoteKey,
            "Mister Creosote",
            "Tried to eat while already full.",
            cat, ap: 5, renown: 1,
            trigger:    T.Event(TriggerKind.EatWhileFull),
            flavor:     "It's only wafer-thin. The System has moved out of range anyway.",
            itemGumpId: 0x09C9, // Ham
            hidden:     true);

        Reg(PlaceboKey,
            "Placebo",
            "Tried to bandage someone who was not hurt.",
            cat, ap: 5, renown: 0,
            trigger:    T.Event(TriggerKind.HealUnhurt),
            flavor:     "They feel much better now. They felt fine before. Medicine.",
            itemGumpId: 0x0E21, // Bandage
            hidden:     true);

        Reg(PlannedObsolescenceKey,
            "Planned Obsolescence",
            "Wore out 100 tools.",
            cat, ap: 15, renown: 5,
            trigger:    T.Count(TriggerKind.ToolsWornOut, 100),
            flavor:     "A hundred tools have given their lives so you could keep doing this. The tinkers thank you.",
            itemGumpId: 0x13E3, // SmithHammer
            hidden:     true);

        Reg(SoulSearchingKey,
            "Soul Searching",
            "Said \"I must consider my sins\".",
            cat, ap: 5, renown: 1,
            trigger: T.Event(TriggerKind.ConsiderSins),
            flavor:  "You considered them. The System has them in alphabetical order, if that helps.",
            hidden:  true);

        Reg(BooKey,
            "Boo",
            "Spoke while a ghost.",
            cat, ap: 5, renown: 0,
            trigger: T.Event(TriggerKind.GhostSpeech),
            flavor:  "OOooOOoo. Very spooky. Nobody understood a word.",
            hidden:  true);

        Reg(HicKey,
            "*hic*",
            "Drank until you hiccuped.",
            cat, ap: 5, renown: 1,
            trigger: T.Event(TriggerKind.Hiccup),
            flavor:  "The System is not angry. The System is disappointed. *hic*",
            hidden:  true);

        Reg(InstantJusticeKey,
            "Instant Justice",
            "Died to the town guards.",
            cat, ap: 10, renown: 0,
            trigger: T.Event(TriggerKind.KilledByGuards),
            flavor:  "Due process took about a quarter of a second. Efficient, really.",
            hidden:  true);

        Reg(SelfDetonationKey,
            "Self-Detonation",
            "Died to your own explosion potion.",
            cat, ap: 10, renown: 0,
            trigger: T.Event(TriggerKind.SelfDetonation),
            flavor:  "Throw it AWAY from you. The System thought that went without saying.",
            hidden:  true);

        Reg(SoleSurvivorKey,
            "Sole Survivor",
            "Fished up 10 boots or shoes.",
            cat, ap: 5, renown: 2,
            trigger:    T.Count(TriggerKind.FishedFootwear, 10),
            flavor:     "Somewhere out there is a lot of barefoot sailors.",
            itemGumpId: 0x170B, // Boots
            hidden:     true);

        Reg(SoleProprietorKey,
            "Sole Proprietor",
            "Fished up 100 boots or shoes.",
            cat, ap: 15, renown: 5,
            trigger:         T.Count(TriggerKind.FishedFootwear, 100),
            flavor:          "You could open a shop. Nothing would match.",
            itemGumpId:      0x170B,
            hidden:          true,
            prerequisiteKey: SoleSurvivorKey);

        Reg(FowlPlayKey,
            "Fowl Play",
            "Died to a chicken or a rabbit.",
            cat, ap: 10, renown: 0,
            trigger: T.Event(TriggerKind.KilledByFowl),
            flavor:  "It went for the throat. The System will be telling this one for years.",
            hidden:  true);
    }

    /// <summary>The counter a joke kind keeps on the account (saved with the other achievement counters).</summary>
    public static string JokeCounter(TriggerKind kind) => "joke_" + kind.ToString().ToLowerInvariant();

    /// <summary>cc-P231: one joke event: counts it and grants every tier of that kind the count has reached.</summary>
    public static void NotifyJoke(PlayerMobile pm, TriggerKind kind)
    {
        if (pm.Account is not IAccount acct) return;
        GrantCounted(acct, kind, Increment(acct.Username, JokeCounter(kind)));
    }
}

/// <summary>cc-P231: the hooks the patches and events call. Each takes the mobile the event happened to.</summary>
public static class ClusterFJokeAchievements
{
    /// <summary>pinned's speech keyword for "I must consider my sins" (Misc/Keywords.cs:31).</summary>
    public const int ConsiderSinsKeyword = 0x0032;

    private static bool _registered;

    public static void Configure() => Register();

    /// <summary>Subscribes to pinned's speech event once (the facts call it too).</summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventSink.Speech += OnSpeech;
    }

    private static void Notify(Mobile m, TriggerKind kind)
    {
        if (m is PlayerMobile { Deleted: false } pm)
            ClusterFAchievementSystem.NotifyJoke(pm, kind);
    }

    public static void ScissorsWhileRunning(Mobile from) => Notify(from, TriggerKind.ScissorsRunning);
    public static void BeggedUntrustworthy(Mobile from) => Notify(from, TriggerKind.BeggingUntrustworthy);
    public static void MeditationBlockedByArmor(Mobile from) => Notify(from, TriggerKind.MeditateInArmor);
    public static void AteWhileFull(Mobile from) => Notify(from, TriggerKind.EatWhileFull);
    public static void HealedUnhurt(Mobile healer) => Notify(healer, TriggerKind.HealUnhurt);
    public static void ToolWornOut(Mobile from) => Notify(from, TriggerKind.ToolsWornOut);
    public static void Hiccuped(Mobile drunk) => Notify(drunk, TriggerKind.Hiccup);
    public static void FishedFootwear(Mobile from) => Notify(from, TriggerKind.FishedFootwear);

    // -- Self-Detonation ------------------------------------------------------

    // The thrower hit by their own blast, and when (Core.TickCount). Read and cleared at their death.
    private static readonly Dictionary<Mobile, long> _selfBlast = new();

    /// <summary>Just before an explosion potion damages <paramref name="victim"/>: marks a thrower caught in their own blast.</summary>
    public static void BeforeBlast(Mobile thrower, Mobile victim)
    {
        if (thrower is PlayerMobile && thrower == victim)
            _selfBlast[thrower] = Core.TickCount;
    }

    // The death that blast causes happens inside the same damage call; a second covers any delay.
    private static bool BlastedSelfJustNow(Mobile m) =>
        _selfBlast.Remove(m, out var at) && Core.TickCount - at <= 1000;

    // -- Events ---------------------------------------------------------------

    public static void OnSpeech(SpeechEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;

        if (e.HasKeyword(ConsiderSinsKeyword))
            Notify(pm, TriggerKind.ConsiderSins);

        if (!pm.Alive && e.Type is MessageType.Regular or MessageType.Whisper or MessageType.Yell &&
            !string.IsNullOrWhiteSpace(e.Speech))
            Notify(pm, TriggerKind.GhostSpeech);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        var killer = pm.FindMostRecentDamager(true);
        var selfBlast = BlastedSelfJustNow(pm);

        if (killer is BaseGuard)
            Notify(pm, TriggerKind.KilledByGuards);
        else if (killer is Chicken or Rabbit or JackRabbit)
            Notify(pm, TriggerKind.KilledByFowl);
        else if (killer == pm && selfBlast)
            Notify(pm, TriggerKind.SelfDetonation);
    }
}
