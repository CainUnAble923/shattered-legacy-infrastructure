using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

// -----------------------------------------------------------------------------
// ClusterF Custodian System - The Custodians, Britannia's civic cleanup guild.
//
// cc-P33 (F-3): the Custodians are our layer on Clean Up Britannia (Services/PointsSystems/CleanUpBritanniaData.cs,
// ported). Civic Tokens are retired: every cleanup now earns Clean Up points, on the character, the same points
// OSI's trash barrels pay and the Clean Up store spends. Each piece of the layer is a registered deviation
// (shard-migration notes/cc-P33-clean-up-britannia.md):
//
//   * [cleanup, [cleanupall and the trash bag pay Clean Up points, valued by Clean Up's own table (GetPoints).
//   * Corpses: Clean Up has no rule for them, so ours: CorpseBasePoints plus each item inside at its Clean Up value.
//   * Ranks key on lifetime Clean Up points (CleanUpBritanniaEntry.Lifetime), which spending never lowers. The
//     character's "custodians" guild standing is kept equal to it (SyncStanding), so every reader of guild standing
//     (directory, rank names, work order gates) follows the lifetime total.
//   * Retired: AwardTokens, the token math, the Warden's token shop, and the "custodians" scrip that held the
//     balances. Balances are cleared at world load (RetireCivicTokens), with the standing re-synced.
//
//   * [cleanup     - target a single item on the ground.
//   * [cleanupall  - area sweep (10-tile radius); 60-second cooldown.
// Both commands require Custodians guild membership.
//
// Item eligibility (applies to both commands and the trash bag):
//   Eligible:   ground items, monster corpses (any state).
//   Ineligible: blessed, quest, player-named, exceptional weapons/armor, gold, bank checks, non-empty non-corpse
//               containers, and (cc-P33) a player's corpse.
// -----------------------------------------------------------------------------

public static class ClusterFCustodianSystem
{
    private const int CleanupAllRadius          = 10; // tiles
    private const int CleanupAllCooldownSeconds = 60; // seconds between uses per player

    // Ours (F-3): what clearing a corpse is worth before its contents, one iron ingot's worth of Clean Up points.
    public const double CorpseBasePoints = 0.10;

    // Ours (F-3): the rank ladder in lifetime Clean Up points. Civic Tokens paid at least 1 a piece and corpses 2 or
    // more (the old ladder was 100 / 500 / 2,000 / 5,000 tokens); Clean Up pays an iron weapon about 1 point, most
    // other litter nothing, and a corpse its contents. So the ladder is the old one divided by 4.
    public static readonly (int Threshold, string Name)[] Ranks =
    [
        (0,     "Volunteer"),
        (25,    "Junior Custodian"),
        (125,   "Custodian"),
        (500,   "Senior Custodian"),
        (1_250, "Chief Custodian")
    ];

    private static readonly Dictionary<Serial, DateTime> _cleanupAllCooldowns = new();

    // -- Bootstrap -------------------------------------------------------------

    public static void Configure()
    {
        CommandSystem.Register("cleanup",    AccessLevel.Player, OnCleanupCommand);
        CommandSystem.Register("cleanupall", AccessLevel.Player, OnCleanupAllCommand);

        EventSink.WorldLoad += RetireCivicTokensOnLoad;
    }

    // -- Command handlers ------------------------------------------------------

    [Usage("cleanup")]
    [Description("Target a ground item to clean it up and earn Clean Up Britannia points.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnCleanupCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (!RequireMembership(pm)) return;

        pm.SendMessage(0x59, "Target an item to clean up.");
        pm.Target = new CleanupTarget();
    }

    [Usage("cleanupall")]
    [Description("Sweep a 10-tile area for all eligible ground items.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnCleanupAllCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm) return;
        if (!RequireMembership(pm)) return;

        pm.SendMessage(0x59, "Target a point to center the sweep.");
        pm.Target = new CleanupAllTarget();
    }

    // -- Membership check ------------------------------------------------------

    private static bool RequireMembership(PlayerMobile pm)
    {
        if (pm.Account == null || !ClusterFGuildSystem.IsJoined(pm, "custodians"))
        {
            pm.SendMessage(0x22,
                "You must be a member of The Custodians to use this command. " +
                "Find a Sanitation Warden in any town to join.");
            return false;
        }
        return true;
    }

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Returns true if the item can be cleaned up.
    /// <paramref name="requireGround"/> - true for [cleanup/[cleanupall (ground only);
    /// false when evaluating trash bag contents.
    /// </summary>
    public static bool IsEligible(Item item, bool requireGround = true)
    {
        if (item == null || item.Deleted) return false;

        // Ground check: item.Parent == null means it is on the ground.
        if (requireGround && item.Parent != null) return false;

        // Blessed or quest-protected items are never eligible.
        if (item.LootType == LootType.Blessed) return false;
        if (item.QuestItem)                    return false;

        // cc-P33: a player's corpse holds their belongings and is never litter.
        if (item is Corpse { Owner: PlayerMobile }) return false;

        // Monster corpses are always eligible regardless of name or contents.
        if (item is Corpse) return true;

        // Non-movable items are decorations or world fixtures - never clean these up.
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

    /// <summary>The Clean Up points an eligible item earns: Clean Up's own value, or the corpse rule.</summary>
    public static double ComputePoints(Item item) =>
        item is Corpse corpse ? ComputeCorpsePoints(corpse) : CleanUpBritanniaData.GetPoints(item);

    /// <summary>
    /// Ours (F-3): Clean Up has no value for a corpse. Clearing one is worth CorpseBasePoints, plus every item
    /// inside at its Clean Up value, so a corpse never pays more than its loot would in a trash barrel, plus the base.
    /// </summary>
    public static double ComputeCorpsePoints(Corpse corpse)
    {
        var points = CorpseBasePoints;

        foreach (var item in corpse.FindItemsByType<Item>())
        {
            points += CleanUpBritanniaData.GetPoints(item);
        }

        return points;
    }

    /// <summary>Awards Clean Up points for Custodian work. The award keeps the standing in step (SyncStanding).</summary>
    public static void AwardPoints(PlayerMobile pm, double points)
    {
        if (points <= 0) return;

        CleanUpBritanniaData.Instance.AwardPoints(pm, points, message: false);
    }

    public static double GetPoints(Mobile m) => CleanUpBritanniaData.Instance.GetPoints(m);

    public static double GetLifetimePoints(Mobile m) => CleanUpBritanniaData.Instance.GetLifetimePoints(m);

    /// <summary>
    /// Sets the character's "custodians" guild standing to its lifetime Clean Up points, rounded down. Called by every
    /// Clean Up award (CleanUpBritanniaData.OnPointsAwarded), on joining, and at world load. Nothing else adds
    /// Custodian standing: the Civic Contracts pay none and the Apprentice task pays none (cc-P33).
    /// </summary>
    public static void SyncStanding(PlayerMobile pm)
    {
        if (pm?.Account == null) return;

        // Only a member's standing is written (or one already there, which a leave-and-rejoin keeps). A non-member's
        // points still count: joining calls this, so the rank is right from the first day.
        var guild = ClusterFAccountPersistence.GetGuild(pm);

        if (guild == null ||
            !guild.JoinedGuilds.Contains("custodians") && !guild.GuildReputation.ContainsKey("custodians"))
        {
            return;
        }

        guild.GuildReputation["custodians"] = (int)Math.Floor(GetLifetimePoints(pm));
    }

    /// <summary>
    /// World load (cc-P33): Civic Tokens are retired. Every character's "custodians" scrip (the token balance) is
    /// removed, and its Custodian standing set from lifetime Clean Up points, so no token-earned rank survives.
    /// Idempotent: a second load finds nothing to clear and writes the same standings.
    /// </summary>
    public static (int Balances, int Standings) RetireCivicTokens()
    {
        var balances = 0;
        var standings = 0;

        foreach (var (_, account) in ClusterFAccountPersistence.All)
        {
            foreach (var (serial, guild) in account.AllGuildData)
            {
                if (guild.GuildCurrency.Remove("custodians"))
                {
                    balances++;
                }

                if (guild.GuildReputation.ContainsKey("custodians") || guild.JoinedGuilds.Contains("custodians"))
                {
                    var pm = World.FindMobile((Serial)serial) as PlayerMobile;
                    var standing = pm == null ? 0 : (int)Math.Floor(GetLifetimePoints(pm));

                    if (guild.GetReputation("custodians") != standing)
                    {
                        standings++;
                    }

                    guild.GuildReputation["custodians"] = standing;
                }
            }
        }

        if (balances > 0 || standings > 0)
        {
            Console.WriteLine(
                $"[ClusterFCustodianSystem] Civic Tokens retired (cc-P33): cleared {balances} token balance(s), " +
                $"re-synced {standings} Custodian standing(s) to lifetime Clean Up points.");
        }

        return (balances, standings);
    }

    private static void RetireCivicTokensOnLoad() => RetireCivicTokens();

    // -- Rank helper -----------------------------------------------------------

    public static string GetCustodianRank(int standing)
    {
        var name = Ranks[0].Name;

        foreach (var (threshold, rank) in Ranks)
        {
            if (standing >= threshold)
            {
                name = rank;
            }
        }

        return name;
    }

    /// <summary>The next rank above <paramref name="standing"/>, or (-1, "") at the top.</summary>
    public static (int Threshold, string Name) GetNextRank(int standing)
    {
        foreach (var rank in Ranks)
        {
            if (standing < rank.Threshold)
            {
                return rank;
            }
        }

        return (-1, string.Empty);
    }

    public static string FormatPoints(double points) => points.ToString("#,0.##");

    // -- Inner targets ---------------------------------------------------------

    private sealed class CleanupTarget : Target
    {
        public CleanupTarget() : base(12, false, TargetFlags.None)
        {
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

            var points = ComputePoints(item);

            string msg;
            if (item is Corpse corpse)
            {
                var count = corpse.Items.Count;
                msg = count > 0
                    ? $"Corpse cleared ({count} item{(count == 1 ? "" : "s")} inside). +{FormatPoints(points)} Clean Up points."
                    : $"Empty corpse cleared. +{FormatPoints(points)} Clean Up points.";
            }
            else
            {
                msg = points > 0
                    ? $"Cleaned up! +{FormatPoints(points)} Clean Up points."
                    : "Cleaned up. That had no turn-in value for Clean Up Britannia.";
            }

            item.Delete();
            AwardPoints(pm, points);
            pm.SendMessage(0x44, msg);
        }
    }

    private sealed class CleanupAllTarget : Target
    {
        public CleanupAllTarget() : base(15, true, TargetFlags.None)
        {
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

            // Collect eligible items first - avoids modifying collection during iteration
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

            var total = 0.0;
            var valued = 0;
            foreach (var it in eligible)
            {
                if (it.Deleted) continue;
                var points = ComputePoints(it);
                total += points;
                if (points > 0) valued++;
                it.Delete();
            }

            // Record cooldown timestamp
            _cleanupAllCooldowns[pm.Serial] = DateTime.UtcNow;

            AwardPoints(pm, total);

            pm.SendMessage(0x44,
                $"Sweep complete: {eligible.Count} item{(eligible.Count == 1 ? "" : "s")} cleaned. " +
                $"+{FormatPoints(total)} Clean Up points" + GiveBundles(pm, valued) + ".");
        }
    }

    /// <summary>
    /// Civic waste bundles for the Civic Contracts: 1 per 5 items cleaned in one batch. cc-P33: only items with Clean Up
    /// value count, so sweeping worthless litter no longer fills a contract.
    /// </summary>
    public static string GiveBundles(PlayerMobile pm, int valuedItems)
    {
        var bundles = valuedItems / 5;

        if (bundles <= 0 || pm.Backpack == null)
        {
            return "";
        }

        pm.Backpack.DropItem(new CleanedDebris { Amount = bundles });
        return $", {bundles} civic waste bundle{(bundles == 1 ? "" : "s")}";
    }
}
