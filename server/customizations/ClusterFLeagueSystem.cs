using System;
using Server.Accounting;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Core system for the League of Extraordinary Citizens.
///
/// Manages:
///   - League membership flags (joined, intro completed, dispatch read, referrals seen)
///   - Citizen status tiers (Unregistered, Registered Citizen, Recognized Citizen)
///   - League achievement hooks (registered, dispatch, referral, guildbound)
///
/// All state is persisted in ClusterFAccountData.Flags / FlagValues.
/// All flag key constants are defined here -- never use raw strings outside this class.
///
/// Entry points:
///   JoinLeague(pm)                    -- called when player registers with the League
///   OnDispatchRead(pm)                -- called when player opens League Dispatch
///   OnGuildReferralSeen(pm, guildKey) -- called when player receives a guild referral
///   OnGuildJoined(pm)                 -- called after ClusterFGuildSystem.Join()
/// </summary>
public static class ClusterFLeagueSystem
{
    // ── Flag constants ─────────────────────────────────────────────────────────

    /// <summary>Set when the player registers with the League Registrar.</summary>
    public const string FlagJoined               = "league.joined";

    /// <summary>Set when the player completes the intro flow (reserved for Phase 2).</summary>
    public const string FlagIntroCompleted       = "league.intro_completed";

    /// <summary>Set the first time the player reads the League Dispatch.</summary>
    public const string FlagFirstDispatch        = "league.first_dispatch_seen";

    /// <summary>Set the first time the player receives any guild referral.</summary>
    public const string FlagFirstReferral        = "league.first_guild_referral_seen";

    /// <summary>Set when the player receives the Miners' Compact referral specifically.</summary>
    public const string FlagReferredMiners       = "league.referred_to_miners";

    /// <summary>FlagValue: ISO-8601 UTC timestamp of when the player joined.</summary>
    public const string FlagValRegisteredAt      = "league.registered_at";

    /// <summary>FlagValue: guild key of the first guild the player was referred to.</summary>
    public const string FlagValFirstReferralGuild = "league.first_referral_guild";

    // ── Citizen status ─────────────────────────────────────────────────────────

    public enum CitizenStatus
    {
        Unregistered,
        RegisteredCitizen,
        RecognizedCitizen,
    }

    // ── Status queries ─────────────────────────────────────────────────────────

    public static CitizenStatus GetStatus(ClusterFAccountData data)
    {
        if (!data.HasFlag(FlagJoined))        return CitizenStatus.Unregistered;
        if (data.JoinedGuilds.Count > 0)      return CitizenStatus.RecognizedCitizen;
        return CitizenStatus.RegisteredCitizen;
    }

    public static bool IsJoined(ClusterFAccountData data) => data.HasFlag(FlagJoined);

    public static string GetStatusLabel(CitizenStatus status) => status switch
    {
        CitizenStatus.RegisteredCitizen => "Registered Citizen",
        CitizenStatus.RecognizedCitizen => "Recognized Citizen",
        _                               => "Unregistered",
    };

    /// <summary>
    /// Returns an HTML-compatible hex colour string (no '#') for the given status.
    /// </summary>
    public static string GetStatusColor(CitizenStatus status) => status switch
    {
        CitizenStatus.RegisteredCitizen => "5599FF",  // blue
        CitizenStatus.RecognizedCitizen => "FFD700",  // gold
        _                               => "888888",  // gray
    };

    // ── Actions ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enroll the player in the League of Extraordinary Citizens.
    /// Safe to call multiple times; idempotent after first call.
    /// </summary>
    public static void JoinLeague(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (data.HasFlag(FlagJoined)) return;

        data.SetFlag(FlagJoined);
        data.SetFlagValue(FlagValRegisteredAt, DateTime.UtcNow.ToString("O"));

        pm.SendMessage(54, "Welcome to the League of Extraordinary Citizens!");
        ClusterFAchievementSystem.TryGrant(acct, "league.registered_citizen");
    }

    /// <summary>
    /// Record that the player read the League Dispatch.
    /// Marks all current bulletins as seen and grants the first-dispatch achievement once.
    /// </summary>
    public static void OnDispatchRead(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        ClusterFBulletinSystem.MarkSeen(acct);

        if (data.HasFlag(FlagFirstDispatch)) return;
        data.SetFlag(FlagFirstDispatch);
        ClusterFAchievementSystem.TryGrant(acct, "league.first_dispatch");
    }

    /// <summary>
    /// Record that the player was referred to a guild.
    /// Grants the first-referral achievement on the first call regardless of guild.
    /// Also sets the miners-referral flag when guildKey is "mining".
    /// </summary>
    public static void OnGuildReferralSeen(PlayerMobile pm, string guildKey)
    {
        if (pm.Account is not IAccount acct) return;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        if (!data.HasFlag(FlagFirstReferral))
        {
            data.SetFlag(FlagFirstReferral);
            data.SetFlagValue(FlagValFirstReferralGuild, guildKey);
            ClusterFAchievementSystem.TryGrant(acct, "league.first_referral");
        }

        if (guildKey.Equals("mining", StringComparison.OrdinalIgnoreCase))
            data.SetFlag(FlagReferredMiners);
    }

    /// <summary>
    /// Called after a player successfully joins any guild via ClusterFGuildSystem.Join().
    /// Grants the guildbound achievement on the first guild join.
    /// </summary>
    public static void OnGuildJoined(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        // JoinedGuilds already has the new guild at this point
        if (data.JoinedGuilds.Count >= 1)
            ClusterFAchievementSystem.TryGrant(acct, "league.guildbound");
    }
}
