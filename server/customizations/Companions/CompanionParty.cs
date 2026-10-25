// cc-P264, companions B1: the party's follower math, logging out with the owner, and the staff commands.
//
// Logout (Chase, 2026-10-03: companions log out with their owner, never auto-stabled). At logout each companion in the
// world stops, is marked away and leaves to the internal map, keeping its control master, so its slots stay counted;
// pinned's AutoStablePets skips it (CanAutoStable => false). IsStabled is set while away: the hourly loyalty sweep
// otherwise deletes a dead bonded pet whose owner is out of sight for a day (P45 probe 6g), and the companion restores
// it at load from its own saved flag. At login it comes back to the owner's tile, following. Probed shape: P45 probe 6.
// Rooms (a companion left behind on purpose) are B9's, and a roomed companion is never brought back here.
//
// Party (design, "The party"): a full party is 6 including the player; its size grows with the owner's loop;
// companions and pets share FollowersMax. The numbers are CompanionSettings'.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Companions;

public static class CompanionParty
{
    public static void Configure()
    {
        EventSink.Logout += OnOwnerLogout;
        CommandSystem.Register("CompanionMake", AccessLevel.GameMaster, CompanionMake_OnCommand);
        CommandSystem.Register("CompanionSet", AccessLevel.GameMaster, CompanionSet_OnCommand);
    }

    // --- the party -----------------------------------------------------------------------------------------------------

    /// <summary>The owner's companions that are counted against it (in the world or away with it).</summary>
    public static List<BaseCompanion> CompanionsOf(Mobile? owner)
    {
        var list = new List<BaseCompanion>();

        if (owner is PlayerMobile { AllFollowers: { } followers })
        {
            foreach (var m in followers)
            {
                if (m is BaseCompanion { Deleted: false } c && c.ControlMaster == owner)
                {
                    list.Add(c);
                }
            }
        }

        return list;
    }

    /// <summary>How many companions the owner may have: party size at its loop, less the owner.</summary>
    public static int CompanionLimit(Mobile? owner) =>
        Math.Max(0, CompanionSettings.PartySizeAt(CompanionSettings.OwnerLoopOf(owner)) - 1);

    /// <summary>Whether the owner can take one more companion costing <paramref name="slotCost"/> slots.</summary>
    public static bool CanTake(Mobile owner, int slotCost, out string reason)
    {
        if (CompanionsOf(owner).Count >= CompanionLimit(owner))
        {
            reason = $"Your party is full: {CompanionLimit(owner)} companion(s) at your loop.";
            return false;
        }

        if (owner.Followers + slotCost > owner.FollowersMax)
        {
            reason = $"You have too many followers: {owner.Followers} of {owner.FollowersMax} slots used, and this one needs {slotCost}.";
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// Raises FollowersMax to the curve at the owner's loop, never lowering it (a staff-raised value stays). At loop 0 the
    /// curve is stock 5, so this changes nothing until F-12 gives players a loop.
    /// </summary>
    public static void ApplyFollowersMax(Mobile owner)
    {
        if (CompanionSettings.Enabled)
        {
            owner.FollowersMax = Math.Max(owner.FollowersMax, CompanionSettings.FollowersMaxAt(CompanionSettings.OwnerLoopOf(owner)));
        }
    }

    /// <summary>Makes a companion the owner's: bonded, following. Refuses (and deletes nothing) when it does not fit.</summary>
    public static bool Join(Mobile owner, BaseCompanion companion, out string reason)
    {
        if (!CanTake(owner, companion.ControlSlots, out reason))
        {
            return false;
        }

        if (companion.Map == null || companion.Map == Map.Internal)
        {
            companion.MoveToWorld(owner.Location, owner.Map);
        }

        if (!companion.SetControlMaster(owner))
        {
            reason = "You have too many followers to control that companion.";
            return false;
        }

        companion.IsBonded = true;
        companion.ControlTarget = owner;
        companion.ControlOrder = OrderType.Follow;
        return true;
    }

    // --- logout and login ------------------------------------------------------------------------------------------------

    public static void OnOwnerLogout(Mobile owner)
    {
        if (!CompanionSettings.Enabled)
        {
            return;
        }

        foreach (var c in CompanionsOf(owner))
        {
            if (c.AwayWithOwner || c.Map == null || c.Map == Map.Internal)
            {
                continue; // already away, or roomed (B9)
            }

            c.ControlTarget = null;
            c.ControlOrder = OrderType.Stay;
            c.SetAway(true);
            c.IsStabled = true;
            c.Internalize();
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnOwnerLogin(PlayerMobile owner)
    {
        // cc-P610 (P487 #5): a companion that left with its owner comes back even when companions were disabled since:
        // the setting stops companions leaving, never strands one that already left.
        ApplyFollowersMax(owner);

        foreach (var c in CompanionsOf(owner))
        {
            if (!c.AwayWithOwner)
            {
                continue;
            }

            c.SetAway(false);
            c.IsStabled = false;
            c.MoveToWorld(owner.Location, owner.Map);
            c.IssueOrder(OrderType.Follow, owner, owner);
        }
    }

    /// <summary>
    /// cc-P610 (P487 #6): after a world load. A save taken while an owner was online keeps the companion in the world, not
    /// away; the restart takes the owner offline without a Logout. Each such companion, alive or a ghost, goes away with
    /// its offline owner now, as a logout would have taken it (stabled, so the dead-pet sweep leaves a ghost alone), and
    /// comes back at the owner's login. Released companions have no master and roomed ones are internal already, so
    /// neither is touched; a second run finds nothing to do.
    /// </summary>
    public static int ReconcileAfterLoad()
    {
        if (!CompanionSettings.Enabled)
        {
            return 0;
        }

        var owners = new HashSet<PlayerMobile>();
        foreach (var m in World.Mobiles.Values)
        {
            if (m is BaseCompanion { Deleted: false, AwayWithOwner: false } c && c.ControlMaster is PlayerMobile { NetState: null } owner &&
                c.Map != null && c.Map != Map.Internal)
            {
                owners.Add(owner);
            }
        }

        foreach (var owner in owners)
        {
            OnOwnerLogout(owner);
        }

        return owners.Count;
    }

    public static void Initialize() => ReconcileAfterLoad();

    // --- staff commands -------------------------------------------------------------------------------------------------

    [Usage("CompanionMake <Blade|Bow|Arcane|Support|Rogue> [personality] [male|female] [loop] [rank]")]
    [Description(
        "Makes a test companion for you (companions B1): the path, and optionally its personality (random if left out), "
        + "sex, loop and rank. It joins your party if it fits. No hiring; that is B4."
    )]
    [ShardCommand(CommandCategory.DevTool)]
    public static void CompanionMake_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length < 1 || !Enum.TryParse<CompanionPath>(e.GetString(0), true, out var path) || path == CompanionPath.None)
        {
            from.SendMessage("Usage: [CompanionMake <Blade|Bow|Arcane|Support|Rogue> [personality] [male|female] [loop] [rank]");
            return;
        }

        var personality = (Personality)Utility.Random(Enum.GetValues<Personality>().Length);

        if (e.Length > 1 && !Enum.TryParse(e.GetString(1), true, out personality))
        {
            from.SendMessage($"No personality '{e.GetString(1)}'. One of: {string.Join(", ", Enum.GetNames<Personality>())}.");
            return;
        }

        var female = e.Length > 2 ? e.GetString(2).Equals("female", StringComparison.OrdinalIgnoreCase) : Utility.RandomBool();
        var loop = e.Length > 3 ? e.GetInt32(3) : 0;
        var rank = e.Length > 4 ? e.GetInt32(4) : 0;

        var companion = new BaseCompanion(path, personality, female);
        companion.SetLoop(loop);
        companion.SetRank(rank);
        companion.SetLevel(companion.MaxLevel);

        if (!Join(from, companion, out var reason))
        {
            companion.Delete();
            from.SendMessage(reason);
            return;
        }

        from.SendMessage(
            $"{companion.Name}: {companion.Personality} {(female ? "female" : "male")} {path}, loop {companion.Loop} "
            + $"(max level {companion.MaxLevel}), rank {companion.Rank}, {companion.ControlSlots} slot(s), lineage {companion.LineageId}."
        );
    }

    [Usage("CompanionSet <loop|rank|level|branch> <value>")]
    [Description("Sets a companion's loop, rank, level or branch (companions B1, for testing), then targets it.")]
    [ShardCommand(CommandCategory.DevTool)]
    public static void CompanionSet_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 2)
        {
            e.Mobile.SendMessage("Usage: [CompanionSet <loop|rank|level|branch> <value>");
            return;
        }

        var what = e.GetString(0).ToLowerInvariant();
        var value = e.GetString(1);

        e.Mobile.SendMessage("Target the companion.");
        e.Mobile.Target = new CompanionSetTarget(what, value);
    }

    /// <summary>Applies one [CompanionSet change; returns what it did, or why not.</summary>
    public static string Set(BaseCompanion c, string what, string value)
    {
        switch (what)
        {
            case "loop" when int.TryParse(value, out var loop):
                c.SetLoop(loop);
                return $"Loop {c.Loop}, max level {c.MaxLevel}.";
            case "rank" when int.TryParse(value, out var rank):
                c.SetRank(rank);
                return $"Rank {c.Rank}, {c.ControlSlots} slot(s).";
            case "level" when int.TryParse(value, out var level):
                c.SetLevel(level);
                return $"Level {c.Level} of {c.MaxLevel}.";
            case "branch" when Enum.TryParse<CompanionBranch>(value, true, out var branch):
                c.SetBranch(branch);
                return $"Branch {c.Branch}.";
            default:
                return "Usage: [CompanionSet <loop|rank|level|branch> <value>";
        }
    }

    private sealed class CompanionSetTarget : Target
    {
        private readonly string _what;
        private readonly string _value;

        public CompanionSetTarget(string what, string value) : base(12, false, TargetFlags.None)
        {
            _what = what;
            _value = value;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            from.SendMessage(targeted is BaseCompanion c ? Set(c, _what, _value) : "That is not a companion.");
        }
    }
}
