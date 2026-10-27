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
        IsleMissing
    }

    /// <summary>The two entries, in order: the isle (index 0, the default) and New Haven.</summary>
    public static CityInfo[] Cities() =>
    [
        new(IsleCity, "The Wakening Isle", ShardClilocs.TutorialIslandCity, IsleDisplay.X, IsleDisplay.Y, IsleDisplay.Z, Map.Trammel),
        new(HavenCity, "The Bountiful Harvest Inn", ShardClilocs.NewHavenSkipCity, HavenSpots.Start.X, HavenSpots.Start.Y, HavenSpots.Start.Z, Map.Trammel)
    ];

    public static Choice ChoiceOf(CityInfo? city) => city?.City switch
    {
        IsleCity => Choice.Isle,
        HavenCity => Choice.Haven,
        _ => Choice.None
    };

    private static string Key(Mobile m) => $"character:{(uint)m.Serial}:start";

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
        return Decide(pm, choice) == Outcome.Isle && TutorialWorld.Isle != null && TutorialWorld.IsleReady()
            ? (TutorialWorld.Arrival, TutorialWorld.Isle)
            : (HavenSpots.Start, HavenSpots.Map);
    }

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
        if (outcome == Outcome.Isle && !TutorialWorld.IsleReady())
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

                SendToIsle(pm);
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
                    ? "Your account skipped the tutorial, so this character starts in New Haven. Double-click Rook on the plaza to take the lessons now and earn their rewards."
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
    public static void SendToIsle(PlayerMobile pm)
    {
        if (!TutorialWorld.IsOnIsle(pm))
        {
            TutorialWorld.Cross(pm, TutorialWorld.Arrival, TutorialWorld.MapOf(pm));
        }

        ClusterFTutorial.SystemLine(pm, "You wake on a quiet shore: the Wakening Isle.");
        ClusterFTutorial.SystemLine(pm, "Follow the arrow and double-click Tamsin, your guide, with two quick left clicks. Tamsin's window starts each lesson.");
        TutorialGuide.PointTo(pm);
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
