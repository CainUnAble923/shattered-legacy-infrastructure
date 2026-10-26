// cc-P630 (P567 design, stage 1): the League Echo's settings (modernuo.json, "clusterf.leagueEchoes.*"), read on each use,
// as LeagueRegisterSettings. Chase's answers (2026-10-25 evening, the design's defaults): at Mythril a reusable Tome calls a
// weak Echo of the Stygian Dragon for 20 seconds, two follower slots, a 30-minute account cooldown. Off by default until the
// later stages pass. Notes: shard-migration notes/cc-P630-build-echo-summon.md.

#nullable enable

using System;
using Server.Engines.LeagueRegister;

namespace Server.Engines.LeagueEchoes;

public static class LeagueEchoSettings
{
    private const string Prefix = "clusterf.leagueEchoes.";

    public const int ControlSlots = 2;
    public const int LeashRange = 12;
    public const int MinDuration = 1;
    public const int MaxDuration = 60;
    public const int MinCooldown = 300;
    public const int MaxCooldown = 86400;

    /// <summary>
    /// The test shard's switch ([LeagueEcho on|off): overrides the setting until the server restarts. Null follows the
    /// setting. Never written to modernuo.json.
    /// </summary>
    public static bool? EnabledOverride { get; set; }

    public static bool Enabled => EnabledOverride ?? ServerConfiguration.GetOrUpdateSetting(Prefix + "enabled", false);

    public static string RequiredRankKey => ServerConfiguration.GetOrUpdateSetting(Prefix + "requiredRank", "Mythril");

    public static int DurationSeconds => ServerConfiguration.GetOrUpdateSetting(Prefix + "durationSeconds", 20);

    public static int CooldownSeconds => ServerConfiguration.GetOrUpdateSetting(Prefix + "cooldownSeconds", 1800);

    /// <summary>The ladder index of the required rank (Mythril by default), or int.MaxValue for an unknown key.</summary>
    public static int RequiredRank => LeagueRegisterSettings.Rank(RequiredRankKey);

    /// <summary>Why the settings refuse new calls, or null when they are sound.</summary>
    public static string? Problem(string rankKey, int duration, int cooldown)
    {
        if (LeagueRegisterSettings.Rank(rankKey) == int.MaxValue)
        {
            return $"requiredRank \"{rankKey}\" is not a League rank";
        }

        if (duration is < MinDuration or > MaxDuration)
        {
            return $"durationSeconds {duration} is outside {MinDuration}-{MaxDuration}";
        }

        if (cooldown is < MinCooldown or > MaxCooldown)
        {
            return $"cooldownSeconds {cooldown} is outside {MinCooldown}-{MaxCooldown}";
        }

        return null;
    }

    public static string? Problem() => Problem(RequiredRankKey, DurationSeconds, CooldownSeconds);

    public static TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    public static TimeSpan Cooldown => TimeSpan.FromSeconds(CooldownSeconds);
}
