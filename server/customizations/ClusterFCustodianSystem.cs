using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterF Custodian System — Phase 4E (rev 2)
//
// The Custodians — Britannia's civic cleanup guild.
//
//   • [cleanup     — target a single item on the ground; awards Civic Tokens.
//   • [cleanupall  — area sweep (10-tile radius); 60-second cooldown.
//
// Both commands require Custodians guild membership.
//
// Item eligibility (applies to both commands and TrashBag dump):
//   Eligible:   ground items, corpses (any state).
//   Ineligible: blessed, quest, player-named, exceptional weapons/armor,
//               gold, bank checks, non-empty non-corpse containers.
//
// Token math:
//   Corpse           → 2 base + 1 per 50 gp of estimated NPC vendor value of contents
//                      (weapons 20 gp × material mult, armor 15 gp × material mult,
//                       exceptional items ×2, stackables 2 gp/unit, gold at face value)
//   Weapon / Armor   → 1–6 scaled by CraftResource tier
//   Stackable        → max(1, amount / 10)
//   Default          → 1
//
// Civic Tokens accumulate in both GuildCurrency("custodians") [spendable]
// and GuildReputation("custodians") [rank standing].
// ─────────────────────────────────────────────────────────────────────────────

public static class ClusterFCustodianSystem
{
    private const int CleanupAllRadius          = 10; // tiles
    private const int CleanupAllCooldownSeconds = 60; // seconds between uses per player

    private static readonly Dictionary<Serial, DateTime> _cleanupAllCooldowns = new();

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    public static void Configure()
    {
        CommandSystem.Register("cleanup",    AccessLevel.Player, OnCleanupCommand);
        CommandSystem.Register("cleanupall", AccessLevel.Player, OnCleanupAllCommand);
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    [Usage("cleanup")]
    [Description("Target a ground item to clean it up and earn Civic Tokens.")]
    private static void OnCleanupCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (!RequireMembership(pm)) return;

        pm.SendMessage(0x59, "Target an item to clean up.");
        pm.Target = new CleanupTarget(pm);
    }

    [Usage("cleanupall")]
    [Description("Sweep a 10-tile area for all eligible ground items.")]
    private static void OnCleanupAllCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (!RequireMembership(pm)) return;

        pm.SendMessage(0x59, "Target a point to center the sweep.");
        pm.Target = new CleanupAllTarget(pm);
    }

    // ── Membership check ──────────────────────────────────────────────────────

    private static bool RequireMembership(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct
            || !ClusterFGuildSystem.IsJoined(acct, "custodians"))
        {
            pm.SendMessage(0x22,
                "You must be a member of The Custodians to use this command. " +
                "Find a Sanitation Warden in any town to join.");
            return false;
        }
        return true;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the item can be cleaned up for tokens.
    /// <paramref name="requireGround"/> — true for [cleanup/[cleanupall (ground only);
    /// false when evaluating TrashBag contents.
    /// </summary>
    public static bool IsEligible(Item item, bool requireGround = true)
    {
        if (item == null || item.Deleted) return false;

        // Ground check: item.Parent == null means it is on the ground.
        if (requireGround && item.Parent != null) return false;

        // Blessed or quest-protected items are never eligible.
        if (item.LootType == LootType.Blessed) return false;
        if (item.QuestItem)                    return false;

        // Corpses are always eligible regardless of name or contents.
        if (item is Corpse) return true;

        // Non-movable items are decorations or world fixtures — never clean these up.
        if (!item.Movable) return false;

        // Player-named items (custom name set by player or system) are skipped.
        if (!string.IsNullOrEmpty(item.Name)) return false;

        // Gold and bank checks
        if (item is Gold or BankCheck) return false;

        // Exceptional weapons and armor are too valuable to trash.
        if (item is BaseWeapon w && w.Quality == WeaponQuality.Exceptional) return false;
        if (item is BaseArmor  a && a.Quality == ArmorQuality.Exceptional)  return false;

        // Non-empty containers are skipped (could hold someone's valuables).
        if (item is Container c && c.Items.Count > 0) return false;

        return true;
    }

    /// <summary>Computes the Civic Token value of an eligible item.</summary>
    public static int ComputeTokens(Item item)
    {
        if (item is Corpse corpse) return ComputeCorpseTokens(corpse);

        if (item is BaseWeapon bw) return MaterialTokens(bw.Resource);
        if (item is BaseArmor  ba) return MaterialTokens(ba.Resource);

        if (item.Stackable && item.Amount > 1)
            return Math.Max(1, item.Amount / 10);

        return 1;
    }

    /// <summary>
    /// Computes the token value of a corpse by estimating the NPC vendor sell
    /// value of its contents, then converting at 1 token per 50 gp.
    /// Base 2 tokens are always awarded for the cleanup effort itself.
    /// Exceptional quality items count double.
    /// </summary>
    private static int ComputeCorpseTokens(Corpse corpse)
    {
        var goldTotal = 0;
        foreach (var item in corpse.Items)
            goldTotal += EstimateVendorGold(item);

        // 2 base tokens for the cleanup + 1 token per 50 gp of estimated value.
        return Math.Max(2, 2 + goldTotal / 50);
    }

    /// <summary>
    /// Returns a rough NPC-vendor-sell-price estimate for an item.
    /// Used only for corpse content valuation.
    /// </summary>
    private static int EstimateVendorGold(Item item)
    {
        if (item == null || item.Deleted) return 0;

        // Gold and bank checks: face value.
        if (item is Gold g)      return g.Amount;
        if (item is BankCheck bc) return bc.Worth;

        // Weapons: base 20 gp × material multiplier × quality bonus.
        if (item is BaseWeapon bw)
        {
            var val = (int)(20 * VendorMaterialMult(bw.Resource));
            if (bw.Quality == WeaponQuality.Exceptional) val *= 2;
            return val;
        }

        // Armor: base 15 gp × material multiplier × quality bonus.
        if (item is BaseArmor ba)
        {
            var val = (int)(15 * VendorMaterialMult(ba.Resource));
            if (ba.Quality == ArmorQuality.Exceptional) val *= 2;
            return val;
        }

        // Stackables (reagents, ingots, arrows, etc.): 2 gp per unit.
        if (item.Stackable && item.Amount > 1)
            return item.Amount * 2;

        // Everything else: 5 gp flat.
        return 5;
    }

    /// <summary>Material multiplier for NPC vendor gold estimates.</summary>
    private static double VendorMaterialMult(CraftResource res) => res switch
    {
        CraftResource.DullCopper => 1.5,
        CraftResource.ShadowIron => 2.0,
        CraftResource.Copper     => 2.5,
        CraftResource.Bronze     => 3.0,
        CraftResource.Gold       => 4.0,
        CraftResource.Agapite    => 5.0,
        CraftResource.Verite     => 7.5,
        CraftResource.Valorite   => 10.0,
        _                        => 1.0,
    };

    /// <summary>
    /// Awards Civic Tokens to the player.
    /// Adds to both spendable currency and standing (rank) for the guild.
    /// </summary>
    public static void AwardTokens(PlayerMobile pm, int tokens)
    {
        if (pm.Account is not IAccount acct) return;
        if (tokens <= 0) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        data.AddReputation("custodians", tokens);
        data.AddCurrency("custodians",   tokens);
    }

    // ── Rank helper ───────────────────────────────────────────────────────────

    public static string GetCustodianRank(int standing) => standing switch
    {
        >= 5_000 => "Chief Custodian",
        >= 2_000 => "Senior Custodian",
        >= 500   => "Custodian",
        >= 100   => "Junior Custodian",
        _        => "Volunteer",
    };

    // ── Inner targets ─────────────────────────────────────────────────────────

    private sealed class CleanupTarget : Target
    {
        private readonly PlayerMobile _pm;

        public CleanupTarget(PlayerMobile pm)
            : base(12, false, TargetFlags.None)
        {
            _pm = pm;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is not PlayerMobile pm) return;

            if (targeted is not Item item)
            {
                pm.SendMessage(0x22, "That is not an item.");
                return;
            }

            if (!IsEligible(item))
            {
                pm.SendMessage(0x22,
                    "That item cannot be cleaned up. " +
                    "Blessed, quest, named, or exceptional items are excluded.");
                return;
            }

            var tokens = ComputeTokens(item);

            // Build a context-aware confirmation message before deleting.
            string msg;
            if (item is Corpse corpse)
            {
                var count = corpse.Items.Count;
                msg = count > 0
                    ? $"Corpse cleared ({count} item{(count == 1 ? "" : "s")} inside). +{tokens} Civic Token{(tokens == 1 ? "" : "s")}."
                    : $"Empty corpse cleared. +{tokens} Civic Token{(tokens == 1 ? "" : "s")}.";
            }
            else
            {
                msg = $"Cleaned up! +{tokens} Civic Token{(tokens == 1 ? "" : "s")}.";
            }

            item.Delete();
            AwardTokens(pm, tokens);
            pm.SendMessage(0x44, msg);
        }
    }

    private sealed class CleanupAllTarget : Target
    {
        private readonly PlayerMobile _pm;

        public CleanupAllTarget(PlayerMobile pm)
            : base(15, true, TargetFlags.None)
        {
            _pm = pm;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is not PlayerMobile pm) return;

            // Cooldown check
            if (_cleanupAllCooldowns.TryGetValue(pm.Serial, out var lastUse)
                && (DateTime.UtcNow - lastUse).TotalSeconds < CleanupAllCooldownSeconds)
            {
                var remaining = CleanupAllCooldownSeconds - (int)(DateTime.UtcNow - lastUse).TotalSeconds;
                pm.SendMessage(0x22,
                    $"[cleanupall is on cooldown for {remaining} more second{(remaining == 1 ? "" : "s")}.");
                return;
            }

            var map = pm.Map;
            if (map == null || map == Map.Internal) return;

            // Resolve center point
            var center = targeted is IPoint3D p3
                ? new Point3D(p3.X, p3.Y, p3.Z)
                : pm.Location;

            // Collect eligible items first — avoids modifying collection during iteration
            var eligible = new List<Item>();
            foreach (var it in map.GetItemsInRange(center, CleanupAllRadius))
            {
                if (IsEligible(it))
                    eligible.Add(it);
            }

            if (eligible.Count == 0)
            {
                pm.SendMessage(0x59, "No eligible items found in range.");
                return;
            }

            var total = 0;
            foreach (var it in eligible)
            {
                if (it.Deleted) continue;
                total += ComputeTokens(it);
                it.Delete();
            }

            // Record cooldown timestamp
            _cleanupAllCooldowns[pm.Serial] = DateTime.UtcNow;

            AwardTokens(pm, total);

            // Award CleanedDebris bundles: 1 per 5 items cleaned in the sweep
            var bundles = eligible.Count / 5;
            if (bundles > 0 && pm.Backpack != null)
            {
                var debris = new CleanedDebris { Amount = bundles };
                pm.Backpack.DropItem(debris);
                pm.SendMessage(0x44,
                    $"Sweep complete: {eligible.Count} item{(eligible.Count == 1 ? "" : "s")} cleaned. " +
                    $"+{total} Civic Token{(total == 1 ? "" : "s")}, " +
                    $"{bundles} civic waste bundle{(bundles == 1 ? "" : "s")}.");
            }
            else
            {
                pm.SendMessage(0x44,
                    $"Sweep complete: {eligible.Count} item{(eligible.Count == 1 ? "" : "s")} cleaned. " +
                    $"+{total} Civic Token{(total == 1 ? "" : "s")}.");
            }
        }
    }

    // ── Material token scale ──────────────────────────────────────────────────

    private static int MaterialTokens(CraftResource res) => res switch
    {
        CraftResource.DullCopper => 2,
        CraftResource.ShadowIron => 2,
        CraftResource.Copper     => 3,
        CraftResource.Bronze     => 3,
        CraftResource.Gold       => 4,
        CraftResource.Agapite    => 4,
        CraftResource.Verite     => 6,
        CraftResource.Valorite   => 6,
        _                        => 1,
    };
}
