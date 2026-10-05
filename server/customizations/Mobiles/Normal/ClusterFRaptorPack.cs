// ClusterFRaptorPack.cs
//
// cc-P51 (F-31, ported). ServUO's Raptor calls up to two "friend" raptors when it gains a combatant
// (ServUO pub57 Scripts/Mobiles/Normal/Raptor.cs: OnCombatantChange :109-116, CheckFriends :118-172,
// InternalTimer :219-233). Upstream's Raptor left it out (its TODO, removed by Raptor-pack-friends.patch).
// The patch adds the hook on upstream's Raptor; the behaviour is here. A friend is a RaptorPackFriend.
//
// Two differences from ServUO, both "broken" bugs fixed under the D-4 rule and argued in
// shard-migration/notes/cc-P51-staff-hub-and-raptor-pack.md, Part A:
//   - ServUO prunes dead friends walking the list forward with Remove, which skips the friend after a
//     removed one and so can call a third; this walks backward, so a caller never has more than two.
//   - The patch's override calls base.OnCombatantChange (pinned BaseCreature sets Warmode there);
//     ServUO's did not.

using System;
using System.Collections.Generic;

namespace Server.Mobiles;

public static class ClusterFRaptorPack
{
    public const int MaxFriends = 2;

    // ServUO's InternalTimer: the first check at once, then every 30 seconds while the fight lasts.
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30.0);

    private sealed class Pack
    {
        public readonly List<Mobile> Friends = new();
        public Timer Timer;
    }

    // One entry per raptor whose pack is running; removed when the pack ends.
    private static readonly Dictionary<Raptor, Pack> _packs = new();

    /// <summary>Called from Raptor.OnCombatantChange (Raptor-pack-friends.patch), after base.</summary>
    public static void OnCombatantChange(Raptor raptor)
    {
        if (raptor is RaptorPackFriend || raptor.Controlled || raptor.Combatant == null || _packs.ContainsKey(raptor))
        {
            return;
        }

        var pack = new Pack();
        _packs[raptor] = pack;
        pack.Timer = Timer.DelayCall(TimeSpan.Zero, CheckInterval, () => CheckFriends(raptor));
    }

    /// <summary>
    /// ServUO's CheckFriends: ends the pack (deleting every friend) when the caller is dead, has no combatant, is
    /// tamed, or is off the map; otherwise tops the friends back up to MaxFriends.
    /// </summary>
    public static void CheckFriends(Raptor raptor)
    {
        if (!_packs.TryGetValue(raptor, out var pack))
        {
            return;
        }

        if (raptor.Deleted || !raptor.Alive || raptor.Combatant == null || raptor.Controlled ||
            raptor.Map == null || raptor.Map == Map.Internal)
        {
            foreach (var f in pack.Friends)
            {
                f.Delete();
            }

            pack.Friends.Clear();
            pack.Timer?.Stop();
            _packs.Remove(raptor);
            return;
        }

        var count = 0;

        for (var i = pack.Friends.Count - 1; i >= 0; i--)
        {
            if (pack.Friends[i]?.Deleted != false)
            {
                pack.Friends.RemoveAt(i);
            }
            else
            {
                count++;
            }
        }

        var map = raptor.Map;

        for (var i = count; i < MaxFriends; i++)
        {
            var friend = new RaptorPackFriend();

            var loc = raptor.Location;
            var validLocation = false;

            for (var j = 0; !validLocation && j < 10; ++j)
            {
                var x = raptor.X + Utility.Random(3) - 1;
                var y = raptor.Y + Utility.Random(3) - 1;
                var z = map.GetAverageZ(x, y);

                if (validLocation = map.CanFit(x, y, raptor.Z, 16, false, false))
                {
                    loc = new Point3D(x, y, raptor.Z);
                }
                else if (validLocation = map.CanFit(x, y, z, 16, false, false))
                {
                    loc = new Point3D(x, y, z);
                }
            }

            friend.MoveToWorld(loc, map);
            friend.Combatant = raptor.Combatant;

            if (friend.AIObject != null)
            {
                friend.AIObject.Action = ActionType.Combat;
            }

            pack.Friends.Add(friend);
        }
    }

    /// <summary>The living friends this raptor has called. Empty when its pack is not running.</summary>
    public static IReadOnlyList<Mobile> FriendsOf(Raptor raptor) =>
        _packs.TryGetValue(raptor, out var pack) ? pack.Friends : Array.Empty<Mobile>();

    /// <summary>True while this raptor's pack timer runs.</summary>
    public static bool IsPackRunning(Raptor raptor) => _packs.ContainsKey(raptor);
}
