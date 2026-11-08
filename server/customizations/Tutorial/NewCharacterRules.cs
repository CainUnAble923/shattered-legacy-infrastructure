// cc-P762 (Chase, 2026-11-01: "Haven should not be choosable at all on new character until we have tutorial island 100%",
// and "humans should be only race choosable for now"): the two new-character locks, one switch each, read live from
// modernuo.json so Chase reopens either without a code change. Notes: shard-migration notes/cc-P762-new-character-lockdown.md.
//
//   newCharacters.newHavenStart  false (locked): every new player character starts on the Wakening Isle, whatever the
//                                creation screen sends (TutorialStart.Decide). true: New Haven is a start again, as before
//                                cc-P717 (the isle first, New Haven the skip).
//   newCharacters.races          "Human" (locked): the races a player can create, comma separated (Human, Elf, Gargoyle;
//                                see the project doc claude/race-design-decisions.md before adding one). Any other race is
//                                created as Human with a short message (CharacterCreation.OnCharacterCreated). The creation
//                                screen still shows every race: TazUO greys Elf's Next button only without the character
//                                list's ML flag (a core packet patch, not made in cc-P762), and Gargoyle only without the
//                                SA feature flag, which the whole client uses.
//
// Staff accounts (GameMaster and up) are exempt from both. Existing characters are never touched.
//
// [BotStaffAccount <slbot-staff...> (test worlds only, the account wipe's guard): the playtest bot's New Haven checks
// (T-039, P64-1, P64-2) start in New Haven through a staff account, so its Admin makes a bot staff pool account
// GameMaster (tools/playtest-bot/uobot/run.py, make_staff). Only accounts named slbot-staff*, and only on a test world.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Commands;
using Server.Misc;

namespace Server.Tutorial;

public static class NewCharacterRules
{
    public const string HavenStartKey = "newCharacters.newHavenStart";
    public const string RacesKey = "newCharacters.races";
    public const string DefaultRaces = "Human";

    public const string RaceRefusedLine =
        "Only Humans can be made for now; Elves and Gargoyles come later. This character was made Human.";

    public const string BotStaffPrefix = "slbot-staff";
    public const string BotStaffDone = "is a GameMaster account now";

    public static void Configure()
    {
        // Written into modernuo.json at first start, locked, so the switches are there to find.
        ServerConfiguration.GetOrUpdateSetting(HavenStartKey, false);
        ServerConfiguration.GetOrUpdateSetting(RacesKey, DefaultRaces);
        CommandSystem.Register("BotStaffAccount", AccessLevel.Administrator, OnBotStaffAccount);
    }

    /// <summary>The bot staff pool account made GameMaster, or why not. Pure but for the account; for the tests.</summary>
    public static string MakeBotStaff(string? username, string? refusal)
    {
        if (refusal != null)
        {
            return $"Refused: {refusal}.";
        }

        if (username == null || !username.StartsWith(BotStaffPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"Only the playtest bot's {BotStaffPrefix} accounts can be made staff this way.";
        }

        if (Accounts.GetAccount(username) is not Account account)
        {
            return $"No account named {username}. Log in to it once first.";
        }

        if (account.AccessLevel < AccessLevel.GameMaster)
        {
            account.AccessLevel = AccessLevel.GameMaster;
        }

        return $"{account.Username} {BotStaffDone}.";
    }

    [Usage("BotStaffAccount <slbot-staff account>")]
    [Description("Test shard only: makes one of the playtest bot's slbot-staff accounts a GameMaster account, so its new characters may start in New Haven (staff are exempt from the new-character lock). Touches no other account.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly, Summary = "Makes a playtest bot slbot-staff account GameMaster for the bot's New Haven checks. Refuses anywhere but a test shard and any other account.")]
    private static void OnBotStaffAccount(CommandEventArgs e) =>
        e.Mobile.SendMessage(MakeBotStaff(e.Length > 0 ? e.GetString(0) : null, GolemTestAccounts.CurrentRefusal()));

    /// <summary>
    /// cc-P817 (the isle release; Chase 2026-11-07: "I also want to remove the trade choice"): the stock Blacksmith
    /// (profession 3: Blacksmithy, Tailoring, Tinkering and Mining at 30) is not offered. The player package's Prof.txt
    /// copy leaves it off the creation screen (Build-UoOverrides.ps1, $ProfRemoved), and a client that asks for it anyway
    /// (an old package, a third-party client) makes no character (CharacterCreation.OnCharacterCreated). Main refuses
    /// the same number as P714's Maker (ClusterFStartChoices.RemovedProfessions). Staff are exempt.
    /// </summary>
    public static readonly int[] RefusedProfessions = [3];

    public static bool ProfessionRefused(IAccount? account, int profession) =>
        !IsExempt(account) && Array.IndexOf(RefusedProfessions, profession) >= 0;

    /// <summary>GameMaster and up: neither lock applies.</summary>
    public static bool IsExempt(IAccount? account) => account?.AccessLevel >= AccessLevel.GameMaster;

    /// <summary>New Haven is a start choice for players (the switch is on).</summary>
    public static bool HavenStartOpen => ServerConfiguration.GetSetting(HavenStartKey, false);

    /// <summary>New Haven is a start for this account: the switch is on, or the account is staff.</summary>
    public static bool HavenStartOpenFor(IAccount? account) => HavenStartOpen || IsExempt(account);

    /// <summary>The races players may create, from the setting; Human when it names none this build knows.</summary>
    public static IReadOnlyList<Race> AllowedRaces
    {
        get
        {
            var list = new List<Race>();
            var text = ServerConfiguration.GetSetting(RacesKey, DefaultRaces) ?? DefaultRaces;
            foreach (var part in text.Split([',', ';', ' ', '[', ']', '"'], StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var race in Race.AllRaces)
                {
                    if (race != null && string.Equals(race.Name, part, StringComparison.OrdinalIgnoreCase) && !list.Contains(race))
                    {
                        list.Add(race);
                    }
                }
            }

            if (list.Count == 0)
            {
                list.Add(Race.Human);
            }

            return list;
        }
    }

    public static bool RaceAllowed(IAccount? account, Race race) => IsExempt(account) || AllowedRaces.Contains(race);

    /// <summary>
    /// The race a new character of this account is made as: the one asked for when allowed, otherwise Human (or the first
    /// allowed race, if Human is off). <paramref name="refused"/> says the request was turned down.
    /// </summary>
    public static Race RaceFor(IAccount? account, Race requested, out bool refused)
    {
        refused = !RaceAllowed(account, requested);
        if (!refused)
        {
            return requested;
        }

        var allowed = AllowedRaces;
        return allowed.Contains(Race.Human) ? Race.Human : allowed[0];
    }
}
