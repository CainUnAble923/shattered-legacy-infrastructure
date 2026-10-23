// cc-P80: the New Haven tutorial, framework. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Once per account (TutorialRecord, ClusterFAccountData v19). States: NotStarted, InCore (the required core),
// InWings (core done; optional wings open), Graduated, Skipped, Remedial (a skipper doing it later at the recruiter).
//
// Entry: the first login of a character whose account has never started it puts that character in the core, says so
// in the System voice and points at the League recruiter (TutorialRecruiter, New Haven plaza). Later characters on the
// account are never entered (the tutorial runs once per account).
// Skip: two System-voice confirmations (TutorialSkipGump, step 1 then 2), then "Didn't Read the Manual". Remedial: the
// recruiter offers it to a skipper; finishing it grants "Read the Manual Eventually" and the normal rewards.
// Graduation (core done): the first loot box (open it here, in the guard zone, to learn the rule) and the evo starter
// pet pick (dog, cat, fox, bird), unless the account already took its dog or cat from the farm's Farmhand (cc-P276,
// TutorialFarm). The squire comes later, from an early League quest (not built).
//
// The two voices: the League recruiter teaches (overhead and gumps); the System voice (the fanfare's) is the snark,
// confirmations and achievements, sent as System lines and through ClusterFFanfare. cc-P480: the client already shows
// such a line as "System: ...", so the text carries no prefix of its own (it read "System: System:").

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Tutorial;

public static class ClusterFTutorial
{
    // Configure refuses to enable the tutorial on live.
    public static bool Enabled { get; set; }

    // P371: provisional option (a), pending Chase's skipper decision. False requires remedial graduation.
    public static bool SkipCountsAsGraduation { get; set; } = true;

    public const string SkippedAchievement = "tutorial.skipped";
    public const string RemedialAchievement = "tutorial.remedial";

    public static void Configure()
    {
        // Read the setting directly: equal-priority Configure methods run in unstable order,
        // so TestCenter.Configure may not have populated TestCenter.Enabled yet.
        Enabled = ServerConfiguration.GetSetting("testCenter.enable", false) &&
                  ServerConfiguration.GetOrUpdateSetting("tutorial.enabled", true);
        SkipCountsAsGraduation = ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.skipCountsAsGraduation", true);
        CommandSystem.Register("Tutorial", AccessLevel.GameMaster, OnTutorialCommand);
    }

    public static TutorialRecord? RecordOf(Mobile m) =>
        m.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct).Tutorial : null;

    /// <summary>In the tutorial now, as the character doing it: what tutorial creatures engage (TutorialCreature).</summary>
    public static bool IsParticipant(Mobile? m)
    {
        var player = m is BaseCreature { Controlled: true } pet ? pet.ControlMaster : m;
        return player is PlayerMobile pm && RecordOf(pm) is { IsActive: true } r && r.CharacterSerial == (uint)pm.Serial;
    }

    public static void SystemLine(Mobile m, string text) => m.SendMessage(0x3B2, text);

    // -- Entry --

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        TutorialCoreTrials.Cancel(pm);
        if (Enabled)
        {
            if (!TryEnter(pm) && IsParticipant(pm))
            {
                SystemLine(pm, "Welcome back. Your completed lessons are saved. Ask Rook for the next one.");
                TutorialRecruiter.PointTo(pm);
            }
            else if (RecordOf(pm) is { } waiting && CompanionPending(waiting))
            {
                SystemLine(pm, "Your companion is still waiting. Double-click Rook on the New Haven plaza and choose one.");
                TutorialRecruiter.PointTo(pm);
            }
        }

        // The tutorial is once per account; each later character needs its own first loop, too. Also handles
        // accounts that graduated before P371, without replaying graduation rewards or changing Young.
        if (RecordOf(pm) is { } record &&
            (record.State == TutorialState.Graduated || record.WasSkipped && SkipCountsAsGraduation))
        {
            ClusterFLoops.Graduate(pm);
        }
    }

    /// <summary>Enters this character into the core if its account has never started the tutorial. True when it did.</summary>
    public static bool TryEnter(PlayerMobile pm)
    {
        // cc-P289 play-through: the Golem's Admin was greeted and pointed at Rook. Staff are never entered.
        if (pm.AccessLevel > AccessLevel.Player || pm.Account?.AccessLevel > AccessLevel.Player ||
            RecordOf(pm) is not { State: TutorialState.NotStarted } record)
        {
            return false;
        }

        record.State = TutorialState.InCore;
        record.CharacterSerial = (uint)pm.Serial;
        record.StartedAt = Core.Now;

        SystemLine(pm, "Welcome to New Haven. Rook, the League recruiter, is waiting by the plaza; they bill by the hour.");
        SystemLine(pm, "Follow the arrow and double-click Rook (two quick left clicks) to begin. You can skip it all there; Rook will ask twice.");
        TutorialRecruiter.PointTo(pm);
        return true;
    }

    // -- Stations --

    public static IEnumerable<TutorialStation> CoreLeft(TutorialRecord record)
    {
        foreach (var s in TutorialStations.Core)
        {
            if (!record.StationsDone.Contains(s.Key))
            {
                yield return s;
            }
        }
    }

    public static bool CoreDone(TutorialRecord record)
    {
        foreach (var _ in CoreLeft(record))
        {
            return false;
        }

        return true;
    }

    /// <summary>Runs a station's trial, or shows an optional wing's coming-soon page.</summary>
    public static void RunStation(PlayerMobile pm, TutorialStation station)
    {
        if (!IsParticipant(pm))
        {
            SystemLine(pm, "You are not in the tutorial.");
            return;
        }

        if (TutorialStations.Handlers.TryGetValue(station.Key, out var trial))
        {
            trial(pm, station);
            return;
        }

        SystemLine(pm, "Coming soon. This wing is optional; graduation needs only the core.");
        pm.SendGump(new TutorialStationGump(station, Server.TazUO.TazUOClientCommand.CanRunCommands(pm.NetState)));
    }

    /// <summary>Marks a station done. The last core station opens the wings.</summary>
    public static void CompleteStation(PlayerMobile pm, string key)
    {
        if (!IsParticipant(pm) || TutorialStations.Find(key) == null ||
            RecordOf(pm) is not { IsActive: true } record || !record.StationsDone.Add(key))
        {
            return;
        }

        var station = TutorialStations.Find(key);
        SystemLine(pm, $"Station done: {station?.Title ?? key}. Adequately.");

        if (record.State == TutorialState.InCore && CoreDone(record))
        {
            record.State = TutorialState.InWings;
            SystemLine(pm, "Core finished. The wings are open, and optional. You may graduate whenever you like.");
        }
    }

    // -- Skip and remedial --

    /// <summary>The skip, after both confirmations. Grants "Didn't Read the Manual" once.</summary>
    public static bool Skip(PlayerMobile pm)
    {
        if (!IsParticipant(pm) || RecordOf(pm) is not { } record || record.State is not (TutorialState.InCore or TutorialState.InWings))
        {
            return false;
        }

        TutorialCoreTrials.Cancel(pm);
        record.State = TutorialState.Skipped;
        record.WasSkipped = true;
        record.FinishedAt = Core.Now;
        SystemLine(pm, "Skipped. Bold. The recruiter will still be here when you need remedial training. And you will.");

        if (SkipCountsAsGraduation)
        {
            ClusterFLoops.Graduate(pm);
            SystemLine(pm, "Your first loop is recorded. The island is open; your Young hours keep running.");
        }
        else
        {
            SystemLine(pm, "Finish remedial training with the recruiter to leave the island.");
        }

        if (pm.Account is IAccount acct)
        {
            ClusterFAchievementSystem.TryGrant(acct, SkippedAchievement);
        }

        return true;
    }

    /// <summary>A skipper starts again at the recruiter: remedial training, the full tutorial with normal rewards.</summary>
    public static bool StartRemedial(PlayerMobile pm)
    {
        if (RecordOf(pm) is not { State: TutorialState.Skipped } record)
        {
            return false;
        }

        record.State = TutorialState.Remedial;
        TutorialCoreTrials.Cancel(pm);
        record.CharacterSerial = (uint)pm.Serial;
        record.StationsDone.Clear();
        record.StartedAt = Core.Now;
        SystemLine(pm, "Remedial training. Welcome back. I saved your seat.");
        return true;
    }

    // -- Graduation --

    public static readonly string[] StarterPetLines = ["dog", "cat", "fox", "bird"];

    // cc-P480 Part 7: the graduation companion is offered until it is chosen. Done keys on the record (P276's pattern, no
    // schema change): set when the pick is offered, cleared when a pet is given. P453 may move it to a record field.
    public const string CompanionPendingKey = "graduation.petPending";
    public const string CompanionTakenKey = "graduation.pet";

    public static bool CompanionPending(TutorialRecord record) => record.StationsDone.Contains(CompanionPendingKey);

    /// <summary>Opens the companion pick, with Rook's words and an arrow to him. False when none is waiting.</summary>
    public static bool OfferCompanion(PlayerMobile pm)
    {
        if (RecordOf(pm) is not { } record || !CompanionPending(record))
        {
            return false;
        }

        if (TutorialRecruiter.Find() is { } rook && rook.InRange(pm, 12))
        {
            rook.SayTo(pm, "You have earned a companion. Choose one:");
        }
        else
        {
            TutorialRecruiter.PointTo(pm);
        }

        SystemLine(pm, "You have earned a companion. Choose one in the window that opened.");
        pm.CloseGump<TutorialPetPickGump>();
        pm.SendGump(new TutorialPetPickGump());
        return true;
    }

    /// <summary>The pick: gives the waiting companion. Null when none is waiting, or no follower slot is free.</summary>
    public static EvoPet? ChooseCompanion(PlayerMobile pm, string line)
    {
        if (RecordOf(pm) is not { } record || !CompanionPending(record) || GivePet(pm, line) is not { } pet)
        {
            return null;
        }

        record.StationsDone.Remove(CompanionPendingKey);
        record.StationsDone.Add(CompanionTakenKey);
        SystemLine(pm, TutorialPetCare.IfYourPetDiesLine + " Your graduation box holds one elixir.");
        return pet;
    }

    /// <summary>
    /// Graduation, once the core is done: the first loot box and the evo pet pick. Remedial graduates also earn "Read the
    /// Manual Eventually". Returns why not, or null.
    /// </summary>
    public static string? Graduate(PlayerMobile pm)
    {
        if (!IsParticipant(pm) || RecordOf(pm) is not { IsActive: true } record)
        {
            return "You are not in the tutorial.";
        }

        if (!CoreDone(record))
        {
            return "Finish the core first.";
        }

        TutorialCoreTrials.Cancel(pm);
        var remedial = record.State == TutorialState.Remedial;
        record.State = TutorialState.Graduated;
        record.FinishedAt = Core.Now;

        ClusterFLoops.Graduate(pm);
        GraduationRewards(pm);

        if (remedial && pm.Account is IAccount acct)
        {
            ClusterFAchievementSystem.TryGrant(acct, RemedialAchievement);
        }

        ClusterFFanfare.Play(pm, FanfareLevel.Major, "", tier: 2,
            line: remedial ? "Graduated. Eventually. It counts." : "Graduated. New Haven survived you. Now the rest of Britannia gets a turn.");
        return null;
    }

    /// <summary>
    /// The first loot box (a tutorial graduation source; open it in the guard zone, which the reveal teaches) and the evo
    /// starter pet pick. Replaceable hook for the graduation trial.
    /// </summary>
    public static Action<PlayerMobile> GraduationRewards { get; set; } = pm =>
    {
        ClusterFLootBoxes.Award(pm, "tutorial:graduation", BoxTheme.Explorer, 1, null, "graduating the New Haven tutorial", out var box);
        if (box != null)
        {
            // cc-P480 Part 8: one blessed elixir of rebirth, so a new player can bring a dead pet back.
            var elixir = new StarterElixirOfRebirth();
            elixir.Internalize();
            box.Contents.Add(elixir);
            box.MarkDirty();
        }

        SystemLine(pm, "Open that box here in town. Boxes only open in guarded towns and your own house.");

        // cc-P276: the farm's Farmhand gives the dog or cat; an account that took its pet there is not offered another.
        if (RecordOf(pm) is { } record && !record.StationsDone.Contains(TutorialFarm.PetKey) &&
            !record.StationsDone.Contains(CompanionTakenKey))
        {
            record.StationsDone.Add(CompanionPendingKey);
            OfferCompanion(pm);
        }
    };

    /// <summary>The evo starter pet, by line (dog, cat, fox, bird). Null when refused (no slot, or not a starter line).</summary>
    public static EvoPet? GivePet(PlayerMobile pm, string line) =>
        Array.IndexOf(StarterPetLines, line) >= 0 ? EvoPet.GivePet(pm, line) : null;

    // -- Staff --

    [Usage("Tutorial <status | enter | station <key> | skip | remedial | graduate | reset>")]
    [Description("Targets a player and shows or moves their account's tutorial: enter starts it, station runs one station, skip skips it (no confirmations), remedial starts remedial training, graduate graduates, reset clears the record.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnTutorialCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        var key = e.Length > 1 ? e.GetString(1) : null;

        e.Mobile.SendMessage("Target a player.");
        e.Mobile.BeginTarget(-1, false, Targeting.TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm || RecordOf(pm) is not { } record)
            {
                from.SendMessage("That is not a player with an account.");
                return;
            }

            switch (verb)
            {
                case "enter":
                    from.SendMessage(TryEnter(pm) ? "Entered." : "Its account has already started the tutorial.");
                    break;
                case "station" when key != null && TutorialStations.Find(key) is { } station:
                    RunStation(pm, station);
                    break;
                case "skip":
                    from.SendMessage(Skip(pm) ? "Skipped." : "Not in the tutorial.");
                    break;
                case "remedial":
                    from.SendMessage(StartRemedial(pm) ? "Remedial started." : "Only a skipper can start remedial training.");
                    break;
                case "graduate":
                    from.SendMessage(Graduate(pm) ?? "Graduated.");
                    break;
                case "reset":
                    TutorialCoreTrials.Cancel(pm);
                    ClusterFAccountPersistence.GetOrCreate((IAccount)pm.Account).ResetTutorial();
                    from.SendMessage("Tutorial record cleared.");
                    break;
                default:
                    from.SendMessage($"{pm.Name}: {record.State}, {record.StationsDone.Count} stations done, skipped before: {record.WasSkipped}.");
                    break;
            }
        });
    }
}
