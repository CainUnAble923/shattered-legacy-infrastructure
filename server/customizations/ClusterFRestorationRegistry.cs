using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

// ---------------------------------------------------------------------------
// RestorationEntry
// ---------------------------------------------------------------------------

/// <summary>
/// Per-key restoration record stored inside ClusterFAccountData.
///
/// Tracks when a legacy item was first unlocked, how many times it has been
/// restored, and whether an active copy currently exists in the world.
///
/// The active-copy flag drives the one-active-copy rule: a player may only
/// have one physical copy of a legacy item at a time. When the item is
/// issued the flag is set to true; when it is confirmed lost or deleted
/// the flag is cleared and the player may restore again.
/// </summary>
public class RestorationEntry
{
    // -- Identity
    public string Key    { get; }
    public string Source { get; set; }  // "quest", "admin", "guild_contract", "legacy_migration"

    // -- History
    public DateTime  UnlockedAt      { get; }
    public int       RestorationCount { get; set; }
    public DateTime? LastRestoredAt  { get; set; }

    // -- Active-copy rule
    public bool HasActiveCopy { get; set; }

    // -- New entry
    public RestorationEntry(string key, string source)
    {
        Key        = key;
        Source     = source;
        UnlockedAt = DateTime.UtcNow;
    }

    // -- Deserialize
    public RestorationEntry(IGenericReader r)
    {
        var version = r.ReadInt(); // 0

        Key               = r.ReadString();
        Source            = r.ReadString();
        UnlockedAt        = r.ReadDateTime();
        RestorationCount  = r.ReadInt();
        HasActiveCopy     = r.ReadBool();

        if (r.ReadBool())
            LastRestoredAt = r.ReadDateTime();
    }

    // -- Serialize
    public void Serialize(IGenericWriter w)
    {
        w.Write(0); // version

        w.Write(Key);
        w.Write(Source);
        w.Write(UnlockedAt);
        w.Write(RestorationCount);
        w.Write(HasActiveCopy);

        w.Write(LastRestoredAt.HasValue);
        if (LastRestoredAt.HasValue)
            w.Write(LastRestoredAt.Value);
    }
}

// ---------------------------------------------------------------------------
// ClusterFRestorationRegistry
// ---------------------------------------------------------------------------

/// <summary>
/// Shattered Legacy restoration registry.
///
/// Tracks which legacy items an account has legitimately unlocked, and
/// enforces the one-active-copy rule on restoration.
///
/// ## Account-wide
/// Unlocks are stored on ClusterFAccountData, not on a character. This is
/// intentional -- Shattered Legacy is a one-character shard and all
/// progression belongs to the account.
///
/// ## Key format
/// Keys follow the pattern:  legacy.{item_id}
/// Examples:
///   legacy.jacobs_pickaxe
///   legacy.hammer_of_hephaestus
///   legacy.citizens_expedition_pack
///   legacy.pet_mimic
///
/// ## Unlock sources
/// Any string is valid; suggested values:
///   "quest"            -- earned through a New Haven or guild quest
///   "guild_contract"   -- earned through a Guild Legacy Contract
///   "admin"            -- granted by a GM
///   "legacy_migration" -- migrated from an earlier save format
///
/// ## Admin commands (GameMaster+)
///   [RestorationUnlock  key [source]   -- target player to grant unlock
///   [RestorationRevoke  key            -- target player to remove unlock
///   [RestorationClear   key            -- target player to clear active-copy flag only
///   [RestorationList                   -- target player to view all entries
/// </summary>
public static class ClusterFRestorationRegistry
{
    public static void Configure()
    {
        CommandSystem.Register("RestorationUnlock", AccessLevel.GameMaster, RestorationUnlock_OnCommand);
        CommandSystem.Register("RestorationRevoke",  AccessLevel.GameMaster, RestorationRevoke_OnCommand);
        CommandSystem.Register("RestorationClear",   AccessLevel.GameMaster, RestorationClear_OnCommand);
        CommandSystem.Register("RestorationList",    AccessLevel.GameMaster, RestorationList_OnCommand);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Returns true if the account has ever unlocked this key.</summary>
    public static bool HasUnlocked(IAccount account, string key) =>
        ClusterFAccountPersistence.Get(account)?.RestorationRegistry.ContainsKey(key) ?? false;

    /// <summary>Returns the full entry, or null if the key has not been unlocked.</summary>
    public static RestorationEntry? GetEntry(IAccount account, string key)
    {
        var data = ClusterFAccountPersistence.Get(account);
        return data?.RestorationRegistry.TryGetValue(key, out var e) == true ? e : null;
    }

    /// <summary>Returns true if an active copy exists in the world.</summary>
    public static bool HasActiveCopy(IAccount account, string key) =>
        GetEntry(account, key)?.HasActiveCopy ?? false;

    /// <summary>
    /// Grants a new unlock. Does nothing if already unlocked.
    /// Returns true if this was a new unlock, false if it was already present.
    /// </summary>
    public static bool Unlock(IAccount account, string key, string source = "admin")
    {
        var data = ClusterFAccountPersistence.GetOrCreate(account);
        if (data.RestorationRegistry.ContainsKey(key))
            return false;

        data.RestorationRegistry[key] = new RestorationEntry(key, source);
        return true;
    }

    /// <summary>
    /// Removes an unlock entirely (including active-copy state).
    /// Returns true if the key existed and was removed.
    /// </summary>
    public static bool Revoke(IAccount account, string key) =>
        ClusterFAccountPersistence.Get(account)?.RestorationRegistry.Remove(key) ?? false;

    /// <summary>
    /// Clears the active-copy flag without revoking the unlock.
    /// Call this when an item is confirmed deleted or permanently lost.
    /// Returns true if the entry existed.
    /// </summary>
    public static bool ClearActiveCopy(IAccount account, string key)
    {
        var entry = GetEntry(account, key);
        if (entry == null) return false;
        entry.HasActiveCopy = false;
        return true;
    }

    /// <summary>
    /// Checks whether restoration is permitted and, if so, updates the entry.
    ///
    /// Fails if:
    ///   - The key has never been unlocked for this account.
    ///   - An active copy already exists.
    ///
    /// On success: increments RestorationCount, sets HasActiveCopy = true,
    /// records LastRestoredAt.
    /// </summary>
    public static bool TryRestore(IAccount account, string key, out string reason)
    {
        var entry = GetEntry(account, key);
        if (entry == null)
        {
            reason = "No unlock on record for this account.";
            return false;
        }

        if (entry.HasActiveCopy)
        {
            reason = "An active copy already exists. Locate or recover the existing item first.";
            return false;
        }

        entry.HasActiveCopy    = true;
        entry.RestorationCount++;
        entry.LastRestoredAt   = DateTime.UtcNow;
        reason = string.Empty;
        return true;
    }

    // -----------------------------------------------------------------------
    // Admin commands
    // -----------------------------------------------------------------------

    [Usage("RestorationUnlock <key> [source]")]
    [Description("Grants a restoration registry unlock to the targeted player. Source defaults to 'admin'.")]
    private static void RestorationUnlock_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: [RestorationUnlock <key> [source]");
            return;
        }

        var key    = e.GetString(0);
        var source = e.Length > 1 ? e.GetString(1) : "admin";

        e.Mobile.SendMessage("Target the player to grant the unlock to.");
        e.Mobile.Target = new RestorationUnlockTarget(key, source);
    }

    [Usage("RestorationRevoke <key>")]
    [Description("Revokes a restoration registry unlock (and active-copy state) from the targeted player.")]
    private static void RestorationRevoke_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: [RestorationRevoke <key>");
            return;
        }

        var key = e.GetString(0);
        e.Mobile.SendMessage("Target the player to revoke the unlock from.");
        e.Mobile.Target = new RestorationRevokeTarget(key);
    }

    [Usage("RestorationClear <key>")]
    [Description("Clears the active-copy flag for a key without revoking the unlock. Use when an item is confirmed lost.")]
    private static void RestorationClear_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: [RestorationClear <key>");
            return;
        }

        var key = e.GetString(0);
        e.Mobile.SendMessage("Target the player to clear the active-copy flag for.");
        e.Mobile.Target = new RestorationClearTarget(key);
    }

    [Usage("RestorationList")]
    [Description("Lists all restoration registry entries for the targeted player.")]
    private static void RestorationList_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the player to list registry entries for.");
        e.Mobile.Target = new RestorationListTarget();
    }

    // -----------------------------------------------------------------------
    // Target helpers
    // -----------------------------------------------------------------------

    private static IAccount? GetAccount(Mobile admin, Mobile target)
    {
        if (target is not PlayerMobile)
        {
            admin.SendMessage("That is not a player.");
            return null;
        }

        if (target.Account is not IAccount account)
        {
            admin.SendMessage("That player has no account.");
            return null;
        }

        return account;
    }

    // -----------------------------------------------------------------------
    // Inner target classes
    // -----------------------------------------------------------------------

    private sealed class RestorationUnlockTarget : Target
    {
        private readonly string _key;
        private readonly string _source;

        public RestorationUnlockTarget(string key, string source)
            : base(12, false, TargetFlags.None)
        {
            _key    = key;
            _source = source;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target) return;
            var account = GetAccount(from, target);
            if (account == null) return;

            if (Unlock(account, _key, _source))
                from.SendMessage($"Unlock granted: '{_key}' (source: {_source}) -> {account.Username}");
            else
                from.SendMessage($"'{_key}' is already unlocked for {account.Username}.");
        }
    }

    private sealed class RestorationRevokeTarget : Target
    {
        private readonly string _key;

        public RestorationRevokeTarget(string key)
            : base(12, false, TargetFlags.None) => _key = key;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target) return;
            var account = GetAccount(from, target);
            if (account == null) return;

            if (Revoke(account, _key))
                from.SendMessage($"Unlock revoked: '{_key}' from {account.Username}.");
            else
                from.SendMessage($"'{_key}' was not found in {account.Username}'s registry.");
        }
    }

    private sealed class RestorationClearTarget : Target
    {
        private readonly string _key;

        public RestorationClearTarget(string key)
            : base(12, false, TargetFlags.None) => _key = key;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target) return;
            var account = GetAccount(from, target);
            if (account == null) return;

            if (ClearActiveCopy(account, _key))
                from.SendMessage($"Active-copy flag cleared for '{_key}' on {account.Username}. They may restore again.");
            else
                from.SendMessage($"'{_key}' was not found in {account.Username}'s registry.");
        }
    }

    private sealed class RestorationListTarget : Target
    {
        public RestorationListTarget()
            : base(12, false, TargetFlags.None) { }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile target) return;
            var account = GetAccount(from, target);
            if (account == null) return;

            var data = ClusterFAccountPersistence.Get(account);
            if (data == null || data.RestorationRegistry.Count == 0)
            {
                from.SendMessage($"{account.Username} has no restoration registry entries.");
                return;
            }

            from.SendMessage($"--- Restoration Registry: {account.Username} ({data.RestorationRegistry.Count} entries) ---");
            foreach (var entry in data.RestorationRegistry.Values)
            {
                var active  = entry.HasActiveCopy ? "[ACTIVE COPY]" : "[no copy]";
                var restored = entry.RestorationCount > 0
                    ? $"  Restored {entry.RestorationCount}x, last: {entry.LastRestoredAt:yyyy-MM-dd HH:mm}"
                    : "  Never restored.";

                from.SendMessage($"  {entry.Key}  {active}");
                from.SendMessage($"    Source: {entry.Source}  Unlocked: {entry.UnlockedAt:yyyy-MM-dd HH:mm}");
                from.SendMessage(restored);
            }
            from.SendMessage("--- End of registry ---");
        }
    }
}
