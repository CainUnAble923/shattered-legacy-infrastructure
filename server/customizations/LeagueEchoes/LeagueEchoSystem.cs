// cc-P630 (P567 design, stage 1): calling, tracking and ending League Echoes. Notes: shard-migration
// notes/cc-P630-build-echo-summon.md.
//
// A call, all on the game thread: the switch and settings, the account, a living and free character of the required rank
// (Mythril by default), not in a duel, standing somewhere an Echo may be, no Echo already out for the account, the
// account's cooldown passed, two free follower slots, and a tile beside the caller. Only a complete success (the Echo
// summoned, owned and placed) starts the 30-minute account cooldown and reveals the caller; a refusal or a failed summon
// starts nothing and leaves no creature.
//
// One Echo per account, in a runtime registry (never saved: a saved Echo deletes itself after load). It ends on expiry
// (BaseCreature's UnsummonTimer), Dismiss, Release or lethal damage (it dissolves), the owner's logout or death (events),
// and, on a one-second watchdog, the owner gone, another map, more than 12 tiles away, a forbidden place, a duel, a lost
// rank or the switch turned off. Ending never refunds or shortens the cooldown.
//
// Stage 1 has no Tome and no Registrar page (stage 2); on the test shard staff use [LeagueEcho.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Mobiles;

namespace Server.Engines.LeagueEchoes;

public enum EchoCall
{
    Called,
    Disabled,
    BadSettings,
    NoAccount,
    BelowRank,
    NotFree,
    Duel,
    Facet,
    Region,
    Active,
    Cooldown,
    Slots,
    NoRoom,
    Failed
}

public static class LeagueEchoSystem
{
    private static readonly Dictionary<IAccount, LeagueDragonEcho> _active = new();
    private static readonly HashSet<IAccount> _calling = new();
    private static Timer? _watchdog;
    private static bool _configured;

    public static IReadOnlyDictionary<IAccount, LeagueDragonEcho> Active => _active;

    /// <summary>Hooks logout and registers [LeagueEcho; once (the test host calls it itself).</summary>
    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        _configured = true;
        EventSink.Logout += OnLogout;
        CommandSystem.Register("LeagueEcho", AccessLevel.GameMaster, OnCommand);
    }

    // -- Checks ---------------------------------------------------------------------------------------------------

    public static LeagueDragonEcho? ActiveFor(IAccount? acct) =>
        acct != null && _active.TryGetValue(acct, out var echo) && !echo.Deleted ? echo : null;

    public static DateTime NextUse(IAccount acct) => ClusterFAccountPersistence.Get(acct)?.LeagueEchoNextUseUtc ?? DateTime.MinValue;

    /// <summary>Time until the account may call again; zero when ready.</summary>
    public static TimeSpan Remaining(IAccount acct)
    {
        var left = NextUse(acct) - Core.Now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    public static bool HasRank(Mobile m) => ClusterFLeagueRanks.GetRank(m) >= LeagueEchoSettings.RequiredRank;

    /// <summary>Why this character cannot call an Echo now, or Called when it can. Changes nothing.</summary>
    public static EchoCall Check(PlayerMobile pm)
    {
        if (!LeagueEchoSettings.Enabled)
        {
            return EchoCall.Disabled;
        }

        if (LeagueEchoSettings.Problem() != null)
        {
            return EchoCall.BadSettings;
        }

        if (pm.Account is not IAccount acct)
        {
            return EchoCall.NoAccount;
        }

        if (!HasRank(pm))
        {
            return EchoCall.BelowRank;
        }

        if (pm.Deleted || !pm.Alive || pm.Frozen || pm.Paralyzed || pm.Spell != null)
        {
            return EchoCall.NotFree;
        }

        if (pm.DuelContext != null)
        {
            return EchoCall.Duel;
        }

        switch (LeagueEchoPolicy.CheckPlace(pm.Location, pm.Map))
        {
            case EchoPlace.Facet: return EchoCall.Facet;
            case EchoPlace.Region: return EchoCall.Region;
        }

        if (ActiveFor(acct) != null || _calling.Contains(acct))
        {
            return EchoCall.Active;
        }

        if (Remaining(acct) > TimeSpan.Zero)
        {
            return EchoCall.Cooldown;
        }

        if (pm.Followers + LeagueEchoSettings.ControlSlots > pm.FollowersMax)
        {
            return EchoCall.Slots;
        }

        return EchoCall.Called;
    }

    // -- The call -------------------------------------------------------------------------------------------------

    public static EchoCall TryCall(PlayerMobile pm) => TryCall(pm, out _);

    public static EchoCall TryCall(PlayerMobile pm, out LeagueDragonEcho? called)
    {
        called = null;
        var check = Check(pm);
        if (check != EchoCall.Called)
        {
            return check;
        }

        if (!LeagueEchoPolicy.TryFindSpawnTile(pm, out var tile))
        {
            return EchoCall.NoRoom;
        }

        var acct = (IAccount)pm.Account;
        var duration = LeagueEchoSettings.Duration; // captured: a later change never extends this Echo
        var cooldown = LeagueEchoSettings.Cooldown;

        _calling.Add(acct);
        LeagueDragonEcho? echo = null;
        try
        {
            echo = new LeagueDragonEcho { Owner = pm, OwnerAccount = acct };
            var summoned = BaseCreature.Summon(echo, true, pm, tile, LeagueDragonEcho.SummonSound, duration);

            if (!summoned || echo.Deleted || !echo.Summoned || echo.ControlMaster != pm || echo.Map != pm.Map ||
                echo.Location != tile)
            {
                echo.Delete();
                return EchoCall.Failed;
            }

            echo.ControlTarget = pm;
            echo.ControlOrder = OrderType.Follow;

            _active[acct] = echo;
            ClusterFAccountPersistence.GetOrCreate(acct).LeagueEchoNextUseUtc = Core.Now + cooldown;
            pm.RevealingAction();
            StartWatchdog();

            called = echo;
            return EchoCall.Called;
        }
        catch
        {
            echo?.Delete();
            throw;
        }
        finally
        {
            _calling.Remove(acct);
        }
    }

    public static string Message(EchoCall result, PlayerMobile pm) => result switch
    {
        EchoCall.Called => $"The Echo answers. It will remain for {LeagueEchoSettings.DurationSeconds} seconds.",
        EchoCall.Disabled => "The League's Echoes are not answering yet.",
        EchoCall.BadSettings => "The League's Echoes are not answering. Tell staff.",
        EchoCall.NoAccount => "The Echo cannot be called here.",
        EchoCall.BelowRank =>
            $"The Echo requires {ClusterFLeagueRanks.RankName(LeagueEchoSettings.RequiredRank)} Citizen.",
        EchoCall.NotFree => "You cannot call the Echo right now.",
        EchoCall.Duel => "The Echo cannot be called during a duel.",
        EchoCall.Facet or EchoCall.Region => "The Echo cannot be called here.",
        EchoCall.Active => "Your account's Echo is already here.",
        EchoCall.Cooldown => $"Your account can call again in {Countdown(Remaining((IAccount)pm.Account))}.",
        EchoCall.Slots => $"You need {LeagueEchoSettings.ControlSlots} free follower slots.",
        EchoCall.NoRoom => "There is no room beside you.",
        _ => "The Echo did not answer. Nothing was spent."
    };

    public static string Countdown(TimeSpan t) =>
        t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m {t.Seconds:00}s" : $"{Math.Max(1, (int)Math.Ceiling(t.TotalSeconds))}s";

    // -- Ending ---------------------------------------------------------------------------------------------------

    /// <summary>Dissolves this character's account Echo if this character owns it. Works with the switch off.</summary>
    public static bool Dismiss(Mobile m)
    {
        if (ActiveFor(m.Account) is { } echo && echo.Owner == m)
        {
            echo.Dissolve();
            return true;
        }

        return false;
    }

    /// <summary>Called from the Echo's OnAfterDelete: forgets it, only if the registry still holds this same Echo.</summary>
    public static void OnEchoDeleted(LeagueDragonEcho echo)
    {
        if (echo.OwnerAccount is { } acct && _active.TryGetValue(acct, out var held) && held == echo)
        {
            _active.Remove(acct);
        }

        if (_active.Count == 0)
        {
            _watchdog?.Stop();
            _watchdog = null;
        }
    }

    private static void OnLogout(Mobile m) => DissolveOwnedBy(m);

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm) => DissolveOwnedBy(pm);

    private static void DissolveOwnedBy(Mobile m)
    {
        if (ActiveFor(m.Account) is { } echo && echo.Owner == m)
        {
            echo.Dissolve();
        }
    }

    /// <summary>Why this Echo must end now, or null while it may stay.</summary>
    public static string? WhyEnd(LeagueDragonEcho echo)
    {
        var owner = echo.Owner;
        if (owner == null || owner.Deleted || !owner.Alive || owner.Map == Map.Internal || owner.Map == null)
        {
            return "owner gone";
        }

        if (!LeagueEchoSettings.Enabled)
        {
            return "disabled";
        }

        if (owner.Map != echo.Map || !owner.InRange(echo.Location, LeagueEchoSettings.LeashRange))
        {
            return "too far";
        }

        if (LeagueEchoPolicy.CheckPlace(echo.Location, echo.Map) != EchoPlace.Ok ||
            LeagueEchoPolicy.CheckPlace(owner.Location, owner.Map) != EchoPlace.Ok)
        {
            return "forbidden place";
        }

        if (owner is PlayerMobile { DuelContext: not null })
        {
            return "duel";
        }

        if (!HasRank(owner))
        {
            return "rank";
        }

        return null;
    }

    /// <summary>One watchdog pass: dissolves every Echo that must end, and drops an illegal combatant.</summary>
    public static void Watch()
    {
        foreach (var echo in _active.Values.ToList())
        {
            if (echo.Deleted)
            {
                continue;
            }

            if (WhyEnd(echo) != null)
            {
                echo.Dissolve();
            }
            else if (echo.Combatant != null && !LeagueEchoPolicy.IsLegalTarget(echo.Combatant))
            {
                echo.Combatant = null;
            }
        }
    }

    private static void StartWatchdog()
    {
        _watchdog ??= Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), Watch);
    }

    // -- Staff (test shard) ---------------------------------------------------------------------------------------

    [Usage("LeagueEcho [status|on|off|call|dismiss]")]
    [Description("The League Echo (stage 1, test shard): shows the switch and your account's cooldown, turns the Echo on or off until restart, or calls or dismisses your own Echo through the players' rules.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";

        switch (verb)
        {
            case "on":
                LeagueEchoSettings.EnabledOverride = true;
                break;
            case "off":
                LeagueEchoSettings.EnabledOverride = false;
                Watch();
                break;
            case "call" when from is PlayerMobile pm:
                from.SendMessage(Message(TryCall(pm), pm));
                return;
            case "dismiss":
                from.SendMessage(Dismiss(from) ? "Your Echo dissolves." : "You have no Echo out.");
                return;
        }

        var problem = LeagueEchoSettings.Problem();
        var ready = from.Account is IAccount acct && Remaining(acct) > TimeSpan.Zero
            ? $"ready in {Countdown(Remaining(acct))}"
            : "ready";
        from.SendMessage(
            $"League Echo: {(LeagueEchoSettings.Enabled ? "on" : "off")}; rank {LeagueEchoSettings.RequiredRankKey}; " +
            $"{LeagueEchoSettings.DurationSeconds}s; cooldown {LeagueEchoSettings.CooldownSeconds}s; {_active.Count} out; your account {ready}" +
            (problem == null ? "." : $"; settings refused: {problem}.")
        );
    }
}
