// cc-P18, F-5: Young status by time played, 2 weeks. Ours: a deviation from OSI, in the register.
//
// Pinned (7c9215d97) ends Young three ways; this shard keeps two of them and changes one number:
//   - Time: 40 hours of account game time (Accounting/Account.cs:21 YoungDuration, checked each minute by
//     CheckYoung, :833-840). Here: 336 hours, two weeks of play (Chase, 2026-09-30). Same counter, still
//     per account (Account.TotalGameTime), so ending it ends it for every character on the account.
//   - Skill: a skill total of 450.0 (Mobiles/PlayerMobile.cs:4149-4153). Removed here: time only.
//   - Murder: gaining a murder count (PlayerMobile.cs:4121-4124). Kept: attacking players while
//     protected still ends the protection.
// A player can end it early: OSI's "I renounce my young player status" (keyword 0x35, Misc/Keywords.cs:40)
// still works, and "I want to grow up!" opens the same RenounceYoungGump confirm (Gumps/YoungGumps.cs:61),
// so a typo or a joke ends nothing.
//
// The duration and the login countdown live in pinned Account.cs and the skill rule in pinned
// PlayerMobile.cs, with no hook to reach them from here: server/patches/Account-young-duration.patch and
// PlayerMobile-young-time-only.patch point pinned at this file. The phrase needs no patch: speech is an
// EventSink event.

using System;
using System.Text;
using Server.Gumps;
using Server.Mobiles;

namespace Server;

public static class ClusterFYoungPlayer
{
    /// <summary>Young lasts this much account game time: 2 weeks of play (pinned: 40 hours).</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(336.0);

    /// <summary>The phrase that opens the renounce confirm, besides OSI's.</summary>
    public const string GrowUpPhrase = "I want to grow up!";

    public static void Configure()
    {
        EventSink.Speech += OnSpeech;
    }

    public static void OnSpeech(SpeechEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm && pm.Young && IsGrowUpPhrase(e.Speech) && !pm.HasGump<RenounceYoungGump>())
        {
            pm.SendGump(new RenounceYoungGump());
        }
    }

    /// <summary>
    /// "I want to grow up!", matched loosely: any case, any spacing, with or without the "!" (or a ".").
    /// The whole line has to be the phrase; a sentence that only contains it does not count.
    /// </summary>
    public static bool IsGrowUpPhrase(string? speech)
    {
        if (string.IsNullOrWhiteSpace(speech))
        {
            return false;
        }

        return Normalize(speech) == Normalize(GrowUpPhrase);
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;

        foreach (var c in text.Trim().TrimEnd('!', '.', ' ').ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space && sb.Length > 0)
            {
                sb.Append(' ');
            }

            space = false;
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>The login line (pinned Account.cs:788-806 counted hours only), in days and hours.</summary>
    public static string LoginMessage(TimeSpan remaining) =>
        "You will enjoy the benefits and relatively safe status of a young player for another " +
        $"{FormatRemaining(remaining)} of play time. Say \"{GrowUpPhrase}\" to give it up sooner.";

    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var days = (int)remaining.TotalDays;
        var hours = remaining.Hours;

        if (days == 0 && hours == 0)
        {
            return "less than an hour";
        }

        var d = days == 1 ? "1 day" : $"{days} days";
        var h = hours == 1 ? "1 hour" : $"{hours} hours";

        return days == 0 ? h : hours == 0 ? d : $"{d} and {h}";
    }
}
