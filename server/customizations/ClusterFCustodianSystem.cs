using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Points;
using Server.Gumps;
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
//   * Corpses: Clean Up has no rule for them, so ours: a base by the creature that died (cc-P175 Part B,
//     ClusterFCorpseValue: its Fame x 0.001, only for the Custodian who earned the kill), plus each item inside at its
//     Clean Up value. Until cc-P175 the base was a flat 0.10 (CorpseBasePoints).
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
// cc-P415: the trash bag's "Clear a corpse" and "Clear corpses nearby" (TrashBag.cs) are corpse claim for any player
// who holds a bag, member or not: ClearCorpse and ClearCorpsesNearby below, on the same rule (CanClear, ComputePoints,
// the [cleanupall reach and cooldown). A monster corpse someone else still has looting rights to is not yours to clear
// (CanClear, which [cleanup and [cleanupall follow too), and a corpse's contents are deleted with it.
//
// Item eligibility (applies to both commands and the trash bag):
//   Eligible:   ground items, monster corpses (any state).
//   Ineligible: blessed, quest, player-named, exceptional weapons/armor, gold, bank checks, non-empty non-corpse
//               containers, and (cc-P33) a player's corpse.
// -----------------------------------------------------------------------------

public static class ClusterFCustodianSystem
{
    public const int CleanupAllRadius           = 10; // tiles; also the trash bag's "Clear corpses nearby" (cc-P415)
    private const int CleanupAllCooldownSeconds = 60; // seconds between uses per player

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

        // cc-P611 (P547 #1): nor is a bonded pet's. Its ghost lives on (pinned BaseCreature.OnDeath) and the corpse keeps
        // its owner and the pet its bond through a save (Corpse._owner, BaseCreature.IsBonded), so the rule holds after a
        // restart. A pet released or deleted since is no longer bonded, and its corpse is an ordinary monster's.
        if (item is Corpse { Owner: BaseCreature { IsBonded: true } }) return false;

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

    /// <summary>
    /// cc-P415: true if <paramref name="by"/> may clear this ground item now: it is eligible (IsEligible) and, for a
    /// monster corpse, no one else's looting rights stand in the way (LootRightsLapsedFor).
    /// </summary>
    public static bool CanClear(Item item, Mobile by) =>
        IsEligible(item) && (item is not Corpse corpse || LootRightsLapsedFor(corpse, by));

    /// <summary>
    /// cc-P415, pinned's own looting rules. Corpse.IsCriminalAction (Corpse.cs:750) is false when looting this corpse is
    /// no crime for <paramref name="by"/>: for a monster corpse NotorietyHandlers.CorpseNotoriety (Notoriety.cs:332-347)
    /// allows it once Corpse.MonsterLootRightSacrifice (2 minutes, Corpse.cs:92) has passed since the death, or sooner
    /// for one who earned the rights or is in their party. And while SE's instanced loot lasts (InstancedCorpse, 3
    /// minutes, Corpse.cs:321), each item is dealt to one attacker (AssignInstancedLoot, Corpse.cs:470) and seen only by
    /// them (IsChildVisibleTo, Corpse.cs:465): a corpse holding an item dealt to someone else waits until that ends.
    /// </summary>
    public static bool LootRightsLapsedFor(Corpse corpse, Mobile by)
    {
        if (by == null || corpse.IsCriminalAction(by))
        {
            return false;
        }

        if (corpse.InstancedCorpse)
        {
            foreach (var item in corpse.Items)
            {
                if (!corpse.IsChildVisibleTo(by, item))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>cc-P415: the items inside a corpse at any depth, all deleted with it when it is cleared.</summary>
    public static int CountContents(Corpse corpse)
    {
        var count = 0;

        foreach (var _ in corpse.FindItemsByType<Item>())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The Clean Up points an eligible item earns: Clean Up's own value, or the corpse rule for the Custodian
    /// <paramref name="by"/> who clears it (no one: the contents only).
    /// </summary>
    public static double ComputePoints(Item item, Mobile by = null) =>
        item is Corpse corpse ? ComputeCorpsePoints(corpse, by) : CleanUpBritanniaData.GetPoints(item);

    /// <summary>
    /// Ours (F-3, cc-P175 Part B): Clean Up has no value for a corpse. Clearing one is worth the creature's base for this
    /// Custodian (ClusterFCorpseValue.BaseFor: Fame x 0.001 when the Custodian earned it, else 0), plus every item
    /// inside at its Clean Up value, so a corpse never pays more than its loot would in a trash barrel, plus the base.
    /// </summary>
    public static double ComputeCorpsePoints(Corpse corpse, Mobile by = null)
    {
        var points = Engines.CleanUpBritannia.ClusterFCorpseValue.BaseFor(corpse, by);

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

    // -- Corpse claim (cc-P415): the trash bag's two entries ------------------------------------------------------

    /// <summary>
    /// The 60-second sweep cooldown, shared by [cleanupall and "Clear corpses nearby": true (and says so) while it runs.
    /// </summary>
    private static bool OnSweepCooldown(PlayerMobile pm, string what)
    {
        if (!_cleanupAllCooldowns.TryGetValue(pm.Serial, out var lastUse)
            || (DateTime.UtcNow - lastUse).TotalSeconds >= CleanupAllCooldownSeconds)
        {
            return false;
        }

        var remaining = CleanupAllCooldownSeconds - (int)(DateTime.UtcNow - lastUse).TotalSeconds;
        pm.SendMessage(0x22, $"{what} is on cooldown for {remaining} more second{(remaining == 1 ? "" : "s")}.");
        return true;
    }

    /// <summary>
    /// "Clear a corpse": one corpse, by CleanupOne. A corpse that still holds items asks first (unless
    /// <paramref name="confirmed"/>), because they go with it. Returns true if cleared.
    /// </summary>
    // cc-P611 (P547 #3): stillAllowed (the trash bag's own check) is asked again when the warning
    // is accepted; if it fails, nothing is cleared.
    public static bool ClearCorpse(PlayerMobile pm, Item targeted, bool confirmed = false,
        Func<PlayerMobile, bool> stillAllowed = null)
    {
        if (targeted is not Corpse corpse || corpse.Deleted)
        {
            pm.SendMessage(0x22, "That is not a corpse.");
            return false;
        }

        if (corpse.Parent != null || corpse.Map != pm.Map || !pm.InRange(corpse.GetWorldLocation(), CleanupRange))
        {
            pm.SendMessage(0x22, "That corpse is too far away.");
            return false;
        }

        var count = CountContents(corpse);

        if (!confirmed && count > 0 && CanClear(corpse, pm))
        {
            var serial = corpse.Serial;
            pm.SendGump(new WarningGump(
                $"This corpse still holds {count} item{(count == 1 ? "" : "s")}. Clearing it deletes the corpse " +
                "and everything inside it, for good. Loot anything you want to keep first.<br><br>Clear it?",
                320, 200,
                ok =>
                {
                    if (ok && StillAllowed(pm, stillAllowed) && World.FindItem(serial) is Corpse again)
                    {
                        ClearCorpse(pm, again, confirmed: true, stillAllowed);
                    }
                }
            ));
            return false;
        }

        return CleanupOne(pm, corpse);
    }

    /// <summary>
    /// "Clear corpses nearby": the monster corpses this character may clear (CanClear) within the [cleanupall reach of
    /// where they stand, nothing else on the ground.
    /// </summary>
    public static List<Corpse> ClearableCorpsesNear(PlayerMobile pm)
    {
        var corpses = new List<Corpse>();
        var map = pm.Map;

        if (map == null || map == Map.Internal)
        {
            return corpses;
        }

        foreach (var it in map.GetItemsInRange(pm.Location, CleanupAllRadius))
        {
            if (it is Corpse corpse && CanClear(corpse, pm))
            {
                corpses.Add(corpse);
            }
        }

        return corpses;
    }

    /// <summary>
    /// "Clear corpses nearby": clears every corpse ClearableCorpsesNear finds, each deleted with its contents and paid
    /// by ComputePoints, on the [cleanupall cooldown. If any still holds items it asks first (unless
    /// <paramref name="confirmed"/>). Returns the number of corpses cleared.
    /// </summary>
    // cc-P611 (P547 #2, #3): the warning approves only the corpses it counted, on the facet it was opened on. Accepted,
    // those still in reach and still the player's to clear go (ClearApproved); a corpse that died or became clearable
    // meanwhile waits for the next use. stillAllowed (the trash bag's own check) is asked again.
    public static int ClearCorpsesNearby(PlayerMobile pm, bool confirmed = false,
        Func<PlayerMobile, bool> stillAllowed = null)
    {
        if (OnSweepCooldown(pm, "Clearing corpses")) return 0;

        var corpses = ClearableCorpsesNear(pm);

        if (corpses.Count == 0)
        {
            pm.SendMessage(0x59, "There are no corpses nearby that you may clear.");
            return 0;
        }

        var items = 0;
        foreach (var corpse in corpses)
        {
            items += CountContents(corpse);
        }

        if (!confirmed && items > 0)
        {
            var map = pm.Map;
            var approved = corpses.ConvertAll(c => c.Serial);
            pm.SendGump(new WarningGump(
                $"{corpses.Count} corpse{(corpses.Count == 1 ? "" : "s")} nearby still hold {items} " +
                $"item{(items == 1 ? "" : "s")}. Clearing deletes the corpses and everything inside them, for good. " +
                "Loot anything you want to keep first.<br><br>Clear them?",
                320, 200,
                ok =>
                {
                    if (ok && StillAllowed(pm, stillAllowed))
                    {
                        ClearApproved(pm, map, approved);
                    }
                }
            ));
            return 0;
        }

        return ClearAll(pm, corpses);
    }

    // cc-P611: the corpses a nearby warning counted, each still on its facet, in reach and the player's to clear.
    private static int ClearApproved(PlayerMobile pm, Map map, List<Serial> approved)
    {
        if (OnSweepCooldown(pm, "Clearing corpses")) return 0;

        if (pm.Map != map)
        {
            pm.SendMessage(0x22, "You are no longer where you looked those corpses over. Nothing was cleared.");
            return 0;
        }

        var corpses = new List<Corpse>();
        foreach (var serial in approved)
        {
            if (World.FindItem(serial) is Corpse corpse && corpse.Map == map &&
                pm.InRange(corpse.GetWorldLocation(), CleanupAllRadius) && CanClear(corpse, pm))
            {
                corpses.Add(corpse);
            }
        }

        if (corpses.Count == 0)
        {
            pm.SendMessage(0x22, "None of those corpses are in reach for you to clear now. Nothing was cleared.");
            return 0;
        }

        return ClearAll(pm, corpses);
    }

    private static bool StillAllowed(PlayerMobile pm, Func<PlayerMobile, bool> stillAllowed)
    {
        if (stillAllowed == null || stillAllowed(pm))
        {
            return true;
        }

        pm.SendMessage(0x22, "Your trash bag is no longer in your backpack. Nothing was cleared.");
        return false;
    }

    private static int ClearAll(PlayerMobile pm, List<Corpse> corpses)
    {
        var items = 0;
        foreach (var corpse in corpses)
        {
            items += CountContents(corpse);
        }

        var total = 0.0;
        foreach (var corpse in corpses)
        {
            total += ComputePoints(corpse, pm);
            corpse.Delete();
        }

        _cleanupAllCooldowns[pm.Serial] = DateTime.UtcNow;
        AwardPoints(pm, total);

        pm.SendMessage(0x44,
            $"Cleared {corpses.Count} corpse{(corpses.Count == 1 ? "" : "s")}" +
            (items > 0 ? $" ({items} item{(items == 1 ? "" : "s")} inside went with them)" : "") +
            $". +{FormatPoints(total)} Clean Up points.");
        return corpses.Count;
    }

    private const int CleanupRange = 12; // CleanupTarget's range

    // -- Inner targets ---------------------------------------------------------

    private sealed class CleanupTarget : Target
    {
        public CleanupTarget() : base(CleanupRange, false, TargetFlags.None)
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

            CleanupOne(pm, item);
        }
    }

    /// <summary>
    /// [cleanup's one item, and the trash bag's "Clear a corpse" (cc-P415): refuses what CanClear refuses, with the
    /// reason, else deletes the item (a corpse with everything inside) and pays ComputePoints. Returns true if cleared.
    /// </summary>
    public static bool CleanupOne(PlayerMobile pm, Item item)
    {
        if (item is Corpse { Owner: PlayerMobile })
        {
            pm.SendMessage(0x22, "That is a player's corpse. It holds their belongings and is never litter.");
            return false;
        }

        if (!IsEligible(item))
        {
            pm.SendMessage(0x22,
                "That item cannot be cleaned up. " +
                "Blessed, quest, named, or exceptional items are excluded.");
            return false;
        }

        if (!CanClear(item, pm))
        {
            pm.SendMessage(0x22, LootRightsRefusal);
            return false;
        }

        var points = ComputePoints(item, pm);

        string msg;
        if (item is Corpse corpse)
        {
            var count = CountContents(corpse);
            msg = count > 0
                ? $"Corpse cleared ({count} item{(count == 1 ? "" : "s")} inside went with it). +{FormatPoints(points)} Clean Up points."
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
        return true;
    }

    public const string LootRightsRefusal =
        "Someone else still has the right to loot that corpse. You may clear it once their rights lapse.";

    private sealed class CleanupAllTarget : Target
    {
        public CleanupAllTarget() : base(15, true, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is not PlayerMobile pm) return;

            if (OnSweepCooldown(pm, "[cleanupall")) return;

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
                if (CanClear(it, pm))
                    eligible.Add(it);
            }

            if (eligible.Count == 0)
            {
                pm.SendMessage(0x59, "No eligible items found in range.");
                return;
            }

            var total = 0.0;
            foreach (var it in eligible)
            {
                if (it.Deleted) continue;
                total += ComputePoints(it, pm);
                it.Delete();
            }

            // Record cooldown timestamp
            _cleanupAllCooldowns[pm.Serial] = DateTime.UtcNow;

            AwardPoints(pm, total);

            pm.SendMessage(0x44,
                $"Sweep complete: {eligible.Count} item{(eligible.Count == 1 ? "" : "s")} cleaned. " +
                $"+{FormatPoints(total)} Clean Up points.");
        }
    }

    /// <summary>
    /// cc-P414: civic waste bundles are retired (Chase, 2026-10-22). Cleaning made 1 per 5 items with Clean Up value in
    /// one batch; the Civic Contracts now ask for Clean Up points instead. A bundle was about 10 points of cleaning (the
    /// sizing is in ClusterFWorkOrderSystem's Custodians section), and that is what one is worth when handed in.
    /// </summary>
    public const double PointsPerBundle = 10.0;

    /// <summary>
    /// cc-P414: hands in a stack of retired civic waste bundles: Clean Up points at PointsPerBundle each, and the stack
    /// is deleted, so each bundle is redeemed once. Returns the points paid.
    /// </summary>
    public static double RedeemBundles(PlayerMobile pm, CleanedDebris bundles)
    {
        if (pm == null || bundles == null || bundles.Deleted || bundles.Amount <= 0)
        {
            return 0;
        }

        var points = bundles.Amount * PointsPerBundle;
        bundles.Delete();
        AwardPoints(pm, points);
        return points;
    }
}
