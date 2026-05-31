using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFRangersSystem
//
// Companion system for the Rangers' League (Outriders) guild.
//
// Responsibilities:
//   • Re-apply OutridersCrook passive skill mods after server restart
//     (OnAdded fires during normal gameplay but not on deserialization).
//   • Manage the Peaceful Approach buff (T2 crook ability).
//   • HasPeacefulTaming() — consulted by the AnimalTaming patch to
//     bypass the SubdueBeforeTame HP check.
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFRangersSystem
{
    // ── Peaceful Approach buff ─────────────────────────────────────────────────
    // Simple DateTime-based buff: no timer overhead, stale entries expire naturally.

    private static readonly Dictionary<Serial, DateTime> _peacefulApproachExpiry = new();

    /// <summary>
    /// Grants a temporary Peaceful Approach buff to the player.
    /// While active, the "must subdue before taming" HP check is suppressed.
    /// </summary>
    public static void GrantPeacefulApproach(PlayerMobile pm, TimeSpan duration)
    {
        _peacefulApproachExpiry[pm.Serial] = Core.Now + duration;
    }

    private static bool HasPeacefulApproachBuff(Mobile m)
    {
        if (!_peacefulApproachExpiry.TryGetValue(m.Serial, out var expiry)) return false;
        if (Core.Now >= expiry)
        {
            _peacefulApproachExpiry.Remove(m.Serial);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns true if the tamer should skip the SubdueBeforeTame check.
    /// Called from the AnimalTaming patch at both check sites.
    /// </summary>
    public static bool HasPeacefulTaming(Mobile? tamer)
    {
        if (tamer == null) return false;

        // T3 passive: Warden's Crook anywhere in possession
        if (tamer.Backpack?.FindItemByType<WardensCrook>() != null) return true;
        if (tamer.FindItemOnLayer(Layer.OneHanded) is WardensCrook) return true;
        if (tamer.FindItemOnLayer(Layer.TwoHanded) is WardensCrook) return true;

        // T2 active buff
        return HasPeacefulApproachBuff(tamer);
    }

    // ── WorldLoad skill-mod re-application ────────────────────────────────────

    public static void Configure()
    {
        EventSink.WorldLoad += OnWorldLoad;
    }

    private static void OnWorldLoad()
    {
        // Re-apply passive skill mods for all crooks that are in player backpacks.
        // OnAdded() is not fired during deserialization so mods must be re-applied here.
        var reapplied = 0;
        foreach (var item in World.Items.Values)
        {
            if (item is OutridersCrook crook && !crook.Deleted)
            {
                crook.TryApplyMods();
                reapplied++;
            }
        }

        if (reapplied > 0)
            Console.WriteLine($"[ClusterFRangersSystem] Re-applied skill mods for {reapplied} Outriders Crook(s).");
    }
}
