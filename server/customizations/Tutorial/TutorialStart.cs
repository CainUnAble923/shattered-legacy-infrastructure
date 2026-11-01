// cc-P663 (P481 F3, design section 7): the start choice at character creation, and who goes to the isle.
// Notes: shard-migration notes/cc-P663-tutorial-moves-to-isle.md.
//
// The city list carries it, with no client change (CharacterCreation.ConstructAvailableStartingCities): index 0
// "Tutorial Island" (the default selection), index 1 "New Haven" (skip). Both are sent as Trammel: TazUO draws city 0's
// facet picture and has names for maps 0 to 5 only, and places each button by its share of the facet's size, so the
// isle's entry carries display coordinates in the sea south of the new New Haven (P673 1.4; the SoS map is sea at
// 3650,2932, read from the author's Map1.mul) and its button reads as an island near New Haven. Descriptions are our
// clilocs (ShardClilocs), shipped in the player package.
//
// The choice is stored on the new character (its account's FlagValues, "character:<serial>:start", saved with the
// account, so a disconnect before the first login keeps it) and consumed once, at that character's first login, where
// Decide picks the outcome from the account's tutorial record (P605 risk 5):
//
//   | Account state                     | "Tutorial Island"                             | "New Haven"                          |
//   | Fresh (NotStarted)                | isle, entered on this character               | skip (P371: Skipped, loop 1)         |
//   | InCore on another character       | New Haven, told the tutorial is with that one | New Haven                            |
//   | Graduated or Skipped              | New Haven; Rook offers Remedial (a skipper)   | New Haven                            |
//   | Tutorial character deleted mid-way| isle, re-bound to this one, done keys kept    | skip (an orphaned core or wings)     |
//   | Created before go-live (no choice)| never entered or moved (P80 item 1)           | -                                    |
//
// cc-P717 (Chase, 2026-10-28) made the account's first character take the isle, with New Haven unlocked per account.
// cc-P762 (Chase, 2026-11-01) replaced that with a shard-wide lock, one switch (NewCharacterRules, newCharacters.newHavenStart,
// false): while it is off, every new player character starts on the Wakening Isle, on every account, whatever the screen
// sends. The list's second entry is "New Haven (locked)" with its one line ("New Haven opens once the Wakening Isle is
// finished"), and Decide turns any pick into the isle. The account's tutorial record still runs once per account, so the
// isle start has two kinds:
//
//   | Account state (switch off)                         | Outcome                                                        |
//   | Fresh (NotStarted), or its tutorial character gone | Isle: entered (or re-bound) on this character, in the lessons  |
//   | InCore, InWings or Remedial on a living character  | Visit: wakes on its own copy of the isle, not in the lessons,  |
//   | Graduated or Skipped                               |   and leaves by the dock's moongate (P717's Codex P1 closed)   |
//
// With the switch on, the table above (the file's first) applies again, New Haven open to everyone. Staff accounts
// (GameMaster and up) always see both and are never entered or moved. The reset's account flags ([ResetMyAccount,
// [ClusterFReset) clear the tutorial record too, so the next character after a reset takes the lessons from the start
// (P717's Codex P2 closed).
//
// cc-P762 part 5 (Chase: "if they want to skip they could run straight for moon gate"): a character that started on the
// isle (IsleStartKey) and walks into the dock's moongate before its lessons are done leaves the normal way, once and for
// good (IsleExitKey "skipped"; Rook will not send it back), with the "Didn't Read the Sign" achievement and a loot box
// holding a pet choice scroll (TutorialGateSkip). A character that finishes the lessons leaves with its normal rewards.
//
// Creation already places an isle pick on the isle when Decide says so then; the login decides again, since another
// character of the account may have entered in between, and moves the character if the answer changed. Staff are
// never entered or moved.

#nullable enable

using Server.Accounting;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialStart
{
    public const string IsleCity = "Tutorial Island";
    public const string HavenCity = "New Haven";
    public const string LockedHavenCity = "New Haven (locked)";

    /// <summary>The isle entry's button on TazUO's Trammel picture: sea south of the new New Haven. Display only.</summary>
    public static readonly Point3D IsleDisplay = new(3650, 2932, -5);

    public enum Choice { None, Isle, Haven }

    public enum Outcome
    {
        /// <summary>Nothing to do: no choice, or staff.</summary>
        None,
        /// <summary>Enter (or re-bind) on this character and wake on the isle.</summary>
        Isle,
        /// <summary>Enter and skip at once (P371), in New Haven.</summary>
        Skip,
        /// <summary>New Haven; the account's tutorial is with another living character.</summary>
        Elsewhere,
        /// <summary>New Haven; the account graduated or skipped (Rook offers a skipper remedial training).</summary>
        Finished,
        /// <summary>New Haven, nothing else.</summary>
        Haven,
        /// <summary>cc-P705: chose the isle, but the isle's land is not on this server's map yet; New Haven, tutorial unused.</summary>
        IsleMissing,
        /// <summary>
        /// cc-P762: the isle, not in the lessons (the account's tutorial is with another living character, or finished); the
        /// character wakes on its own copy and leaves by the moongate.
        /// </summary>
        Visit
    }

    /// <summary>The two entries, in order: the isle (index 0, the default) and New Haven.</summary>
    public static CityInfo[] Cities() =>
    [
        new(IsleCity, "The Wakening Isle", ShardClilocs.TutorialIslandCity, IsleDisplay.X, IsleDisplay.Y, IsleDisplay.Z, Map.Trammel),
        new(HavenCity, "The Bountiful Harvest Inn", ShardClilocs.NewHavenSkipCity, HavenSpots.Start.X, HavenSpots.Start.Y, HavenSpots.Start.Z, Map.Trammel)
    ];

    /// <summary>
    /// cc-P762: the list for this account: both entries while New Haven's switch is on and for staff, otherwise the isle
    /// and New Haven shown locked with its one line.
    /// </summary>
    public static CityInfo[] Cities(IAccount? account)
    {
        var cities = Cities();
        if (NewCharacterRules.HavenStartOpenFor(account))
        {
            return cities;
        }

        var haven = cities[1];
        cities[1] = new CityInfo(LockedHavenCity, haven.Building, ShardClilocs.NewHavenLockedCity, haven.X, haven.Y, haven.Z, haven.Map);
        return cities;
    }

    public static Choice ChoiceOf(CityInfo? city) => city?.City switch
    {
        IsleCity or LockedHavenCity => Choice.Isle,
        HavenCity => Choice.Haven,
        _ => Choice.None
    };

    private static string Key(Mobile m) => $"character:{(uint)m.Serial}:start";

    /// <summary>cc-P762: set when a character's first login put it on the isle (in the lessons or visiting).</summary>
    private static string IsleStartKey(Mobile m) => $"character:{(uint)m.Serial}:isleStart";

    /// <summary>cc-P762: how a character that started on the isle left it: "skipped" (ran for the moongate) or "left".</summary>
    private static string IsleExitKey(Mobile m) => $"character:{(uint)m.Serial}:isleExit";

    public const string ExitSkipped = "skipped";
    public const string ExitLeft = "left";

    private static string? FlagValue(Mobile m, string key) =>
        m.Account is IAccount acct && ClusterFAccountPersistence.Get(acct) is { } data && data.FlagValues.TryGetValue(key, out var v) ? v : null;

    /// <summary>cc-P762: this character's first login put it on the Wakening Isle.</summary>
    public static bool IsIsleStarter(Mobile m) => FlagValue(m, IsleStartKey(m)) != null;

    /// <summary>cc-P762: how this character left the isle it started on (ExitSkipped, ExitLeft), or null while it has not.</summary>
    public static string? IsleExit(Mobile m) => FlagValue(m, IsleExitKey(m));

    /// <summary>cc-P762: this character ran for the moongate before its lessons were done; it never goes back.</summary>
    public static bool GateSkipped(Mobile m) => IsleExit(m) == ExitSkipped;

    /// <summary>cc-P762: started on the isle and has not left it yet.</summary>
    public static bool StillOnFirstVisit(Mobile m) => IsIsleStarter(m) && IsleExit(m) == null;

    public static void MarkIsleStart(Mobile m)
    {
        if (m.Account is IAccount acct)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[IsleStartKey(m)] = "1";
        }
    }

    /// <summary>cc-P762: records how a character that started on the isle left it; the first way out counts.</summary>
    public static void MarkIsleExit(Mobile m, string how)
    {
        if (m.Account is IAccount acct && IsIsleStarter(m) && IsleExit(m) == null)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[IsleExitKey(m)] = how;
        }
    }

    private static string Text(Choice c) => c == Choice.Isle ? "isle" : "haven";

    /// <summary>The choice waiting for this character's first login, or None.</summary>
    public static Choice Pending(Mobile m) =>
        m.Account is IAccount acct && ClusterFAccountPersistence.Get(acct) is { } data &&
        data.FlagValues.TryGetValue(Key(m), out var v) ? v == "isle" ? Choice.Isle : v == "haven" ? Choice.Haven : Choice.None : Choice.None;

    public static void Record(Mobile m, Choice c)
    {
        if (c != Choice.None && m.Account is IAccount acct)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[Key(m)] = Text(c);
        }
    }

    private static bool IsStaff(Mobile m) => m.AccessLevel > AccessLevel.Player || m.Account?.AccessLevel > AccessLevel.Player;

    /// <summary>The eligibility table (file header), for this character choosing <paramref name="c"/>.</summary>
    public static Outcome Decide(PlayerMobile pm, Choice c)
    {
        if (c == Choice.None || IsStaff(pm) || ClusterFTutorial.RecordOf(pm) is not { } record)
        {
            return Outcome.None;
        }

        var orphaned = record.IsActive && ClusterFTutorial.IsRecordedCharacter(pm, record);

        // cc-P762: while New Haven's switch is off, every pick is the isle's, and every new character wakes there: in the
        // lessons when the account's tutorial is free for it, visiting otherwise.
        if (!NewCharacterRules.HavenStartOpenFor(pm.Account))
        {
            return record.State == TutorialState.NotStarted || orphaned ? Outcome.Isle : Outcome.Visit;
        }

        return (record.State, c) switch
        {
            (TutorialState.NotStarted, Choice.Isle) => Outcome.Isle,
            (TutorialState.NotStarted, _) => Outcome.Skip,
            (TutorialState.Graduated or TutorialState.Skipped, _) => c == Choice.Isle ? Outcome.Finished : Outcome.Haven,
            _ when orphaned && c == Choice.Isle => Outcome.Isle,
            (TutorialState.InCore or TutorialState.InWings, _) when orphaned => Outcome.Skip,
            _ when orphaned => Outcome.Haven,
            _ => c == Choice.Isle ? Outcome.Elsewhere : Outcome.Haven
        };
    }

    /// <summary>
    /// Character creation (CharacterCreation.OnCharacterCreated): records the choice and says where to place the new
    /// character: the isle when it would go there now, New Haven's start otherwise. Staff keep their own start.
    /// </summary>
    public static (Point3D Location, Map Map) OnCreated(PlayerMobile pm, CityInfo city)
    {
        // With the tutorial off (live, until F4), the list is New Haven alone and nothing is recorded: a stored "New Haven"
        // would otherwise skip the account's tutorial at the first login after the tutorial is turned on.
        var choice = ClusterFTutorial.Enabled ? ChoiceOf(city) : Choice.None;
        if (choice == Choice.None || IsStaff(pm))
        {
            // cc-P705 (Codex on P663): the isle's entry carries display coordinates in the sea; nobody is placed there.
            return ChoiceOf(city) == Choice.Isle ? (HavenSpots.Start, HavenSpots.Map) : (city.Location, city.Map);
        }

        Record(pm, choice);
        return Decide(pm, choice) is Outcome.Isle or Outcome.Visit && TutorialWorld.Isle != null && TutorialWorld.IsleReady()
            ? (TutorialWorld.Arrival, TutorialWorld.Isle)
            : (HavenSpots.Start, HavenSpots.Map);
    }

    /// <summary>
    /// cc-P728: this character's first login will wake them on the isle's beach (TutorialWake), so the login's other windows
    /// wait for it. Read without side effects, by login handlers that may run before ConsumeAtLogin.
    /// </summary>
    public static bool WillWake(PlayerMobile pm) =>
        ClusterFTutorial.Enabled && Pending(pm) is var choice && choice != Choice.None &&
        Decide(pm, choice) is Outcome.Isle or Outcome.Visit && TutorialWorld.IsleReady();

    /// <summary>
    /// The first login: consumes this character's choice (only its own) and carries out the table. None when it had none.
    /// </summary>
    public static Outcome ConsumeAtLogin(PlayerMobile pm)
    {
        var choice = Pending(pm);
        if (choice == Choice.None || pm.Account is not IAccount acct)
        {
            return Outcome.None;
        }

        ClusterFAccountPersistence.GetOrCreate(acct).FlagValues.Remove(Key(pm));
        var outcome = Decide(pm, choice);
        // cc-P705: a release with the start choice but without the isle's map patch never strands anyone in the void.
        if (outcome is Outcome.Isle or Outcome.Visit && !TutorialWorld.IsleReady())
        {
            outcome = Outcome.IsleMissing;
        }

        switch (outcome)
        {
            case Outcome.Isle:
                if (!ClusterFTutorial.TryEnter(pm, quiet: true))
                {
                    ClusterFTutorial.AdoptOrphanedRecord(pm);
                }

                MarkIsleStart(pm); // cc-P762
                SendToIsle(pm, atNextLesson: false, wake: true); // cc-P721: the first arrival wakes on the sand
                break;
            case Outcome.Visit:
                // cc-P762: on the isle, not in the lessons; the moongate is the way out.
                MarkIsleStart(pm);
                SendToIsle(pm, atNextLesson: false, wake: true, visitor: true);
                break;
            case Outcome.Skip:
                ClusterFTutorial.AdoptOrphanedRecord(pm);
                ClusterFTutorial.TryEnter(pm, quiet: true);
                SendHome(pm);
                ClusterFTutorial.Skip(pm);
                break;
            case Outcome.Elsewhere:
                SendHome(pm);
                var name = ClusterFTutorial.RecordOf(pm) is { } r ? World.FindMobile((Serial)r.CharacterSerial)?.Name : null;
                ClusterFTutorial.SystemLine(pm, $"Your account's tutorial is with {name ?? "another character"}. Log in as that character to continue it. This character starts in New Haven.");
                break;
            case Outcome.Finished:
                SendHome(pm);
                // A skipped account's character that asked for the tutorial is the one Rook offers remedial training to
                // (cc-P453 lets only the recorded character use his window).
                if (ClusterFTutorial.RecordOf(pm) is { State: TutorialState.Skipped } skipped)
                {
                    skipped.CharacterSerial = (uint)pm.Serial;
                }

                ClusterFTutorial.SystemLine(pm, ClusterFTutorial.RecordOf(pm)?.State == TutorialState.Skipped
                    ? "Your account skipped the tutorial, so this character starts in New Haven. Double-click Rook on his bench by the Albatross tavern to take the lessons now and earn their rewards."
                    : "Your account has finished the tutorial, so this character starts in New Haven.");
                break;
            case Outcome.Haven:
                SendHome(pm);
                break;
            case Outcome.IsleMissing:
                SendHome(pm);
                ClusterFTutorial.SystemLine(pm, "The Wakening Isle is not open on this world yet, so this character starts in New Haven. Your account has not used its tutorial: a character you make once the isle opens can still take it.");
                break;
        }

        return outcome;
    }

    /// <summary>Wakes a trainee on the isle's beach (with their pets) and points them at Tamsin.</summary>
    public static void SendToIsle(PlayerMobile pm) => SendToIsle(pm, atNextLesson: false);

    /// <summary>
    /// cc-P664: onto the trainee's own copy of the isle (or their party's; TutorialCopies.Claim), on the beach, or at
    /// their first undone lesson. When every copy is in use, onto the Fold's beach to wait.
    /// </summary>
    public static void SendToIsle(PlayerMobile pm, bool atNextLesson) => SendToIsle(pm, atNextLesson, wake: false);

    /// <summary>cc-P721: <paramref name="wake"/> plays the washed-ashore wake-up (TutorialWake) before the first lines.</summary>
    public static void SendToIsle(PlayerMobile pm, bool atNextLesson, bool wake) => SendToIsle(pm, atNextLesson, wake, visitor: false);

    /// <summary>cc-P762: <paramref name="visitor"/> says the visitor's first lines (not in the lessons; the moongate out).</summary>
    public static void SendToIsle(PlayerMobile pm, bool atNextLesson, bool wake, bool visitor)
    {
        if (TutorialCopies.Claim(pm) is not { } copy)
        {
            TutorialCopies.Wait(pm);
            return;
        }

        if (pm.Map != copy || atNextLesson) // a freed copy can come back to the same trainee under a new generation
        {
            TutorialWorld.Cross(pm, atNextLesson ? TutorialCopies.NextLessonSpot(pm, copy) : TutorialWorld.Ground(copy, TutorialWorld.Beach), copy);
        }

        if (atNextLesson)
        {
            ClusterFTutorial.SystemLine(pm, "You are on your own copy of the Wakening Isle, at your next lesson.");
            TutorialGuide.PointTo(pm);
            return;
        }

        void FirstLines()
        {
            ClusterFTutorial.SystemLine(pm, "You wake on a quiet shore: the Wakening Isle.");
            if (visitor)
            {
                ClusterFTutorial.SystemLine(pm, VisitorLine(pm));
                ClusterFTutorial.SystemLine(pm, "Follow the road east to the moongate at the end of the dock; it takes you to New Haven.");
                TutorialGuide.PointTo(pm);
                return;
            }

            ClusterFTutorial.SystemLine(pm, "Follow the arrow and double-click Tamsin, your guide, with two quick left clicks. Tamsin's window starts each lesson.");
            TutorialGuide.PointTo(pm);
        }

        if (wake)
        {
            TutorialWake.Play(pm, FirstLines);
        }
        else
        {
            FirstLines();
        }
    }

    /// <summary>cc-P762: why a visitor is not in the lessons.</summary>
    public static string VisitorLine(PlayerMobile pm)
    {
        var record = ClusterFTutorial.RecordOf(pm);
        if (record is { IsActive: true })
        {
            var name = World.FindMobile((Serial)record.CharacterSerial)?.Name;
            return $"Your account's lessons are with {name ?? "another character"}; log in as that character to take them. This character is visiting.";
        }

        return record?.State == TutorialState.Skipped
            ? "Your account skipped the lessons, so this character is visiting."
            : "Your account has finished the lessons, so this character is visiting.";
    }

    /// <summary>Sets a character down at the New Haven start if they are on the Fold.</summary>
    public static void SendHome(PlayerMobile pm)
    {
        if (FoldMaps.IsFold(pm.Map))
        {
            TutorialWorld.Cross(pm, HavenSpots.Start, HavenSpots.Map);
        }
    }
}
