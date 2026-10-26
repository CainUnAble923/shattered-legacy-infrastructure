// cc-P630 (P567 design, stage 1): where a League Echo may be called and stay, where it may stand, and what it may fight.
// One place for the rules, used by the call, the watchdog and the creature's own harm checks.
//
// Facets: Trammel, Ilshenar, Malas, Tokuno and Ter Mur. Felucca, Internal and custom facets are refused.
// Regions refused anywhere in a region's parent chain: houses, the staff jail, ConPVP safe zones (even with their guards
// off), the tutorial yard and the Apprentice training grounds, and any guarded region whose guards are on.
// Targets: a living, wild creature only (no master, not controlled, summoned, bonded, invulnerable or initially innocent),
// never a player, a follower, a vendor, a guard, an escort or a tutorial creature.

#nullable enable

using Server.Engines.ConPVP;
using Server.Mobiles;
using Server.Regions;
using Server.Tutorial;

namespace Server.Engines.LeagueEchoes;

public enum EchoPlace
{
    Ok,
    Facet,
    Region
}

public static class LeagueEchoPolicy
{
    public static bool AllowedFacet(Map? map) =>
        map != null && (map == Map.Trammel || map == Map.Ilshenar || map == Map.Malas || map == Map.Tokuno || map == Map.TerMur);

    /// <summary>Whether an Echo is refused in this region (checked up its parent chain).</summary>
    public static bool IsForbiddenRegion(Region region)
    {
        if (region.IsPartOf<HouseRegion>() || region.IsPartOf<JailRegion>() || region.IsPartOf<SafeZone>() ||
            region.IsPartOf<TutorialYardRegion>() || region.IsPartOf<ApprenticeTrainingRegion>())
        {
            return true;
        }

        return region.GetRegion<GuardedRegion>() is { } guarded && !guarded.IsDisabled();
    }

    public static EchoPlace CheckPlace(Point3D p, Map? map)
    {
        if (!AllowedFacet(map))
        {
            return EchoPlace.Facet;
        }

        return IsForbiddenRegion(Region.Find(p, map)) ? EchoPlace.Region : EchoPlace.Ok;
    }

    private static readonly (int X, int Y)[] Adjacent =
    [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)
    ];

    /// <summary>
    /// The first of the eight tiles around the caller an Echo can stand on: a valid mobile spawn tile (at the caller's Z or
    /// the map's average Z there), in the caller's line of sight, in a permitted region. Never the caller's own tile.
    /// </summary>
    public static bool TryFindSpawnTile(Mobile caller, out Point3D tile)
    {
        var map = caller.Map;
        if (map != null && AllowedFacet(map))
        {
            foreach (var (dx, dy) in Adjacent)
            {
                var x = caller.X + dx;
                var y = caller.Y + dy;
                foreach (var z in new[] { caller.Z, map.GetAverageZ(x, y) })
                {
                    var p = new Point3D(x, y, z);
                    if (map.CanSpawnMobile(p) && caller.InLOS(p) && CheckPlace(p, map) == EchoPlace.Ok)
                    {
                        tile = p;
                        return true;
                    }
                }
            }
        }

        tile = Point3D.Zero;
        return false;
    }

    /// <summary>Whether an Echo may harm this mobile: a living, wild creature that nobody owns and nothing protects.</summary>
    public static bool IsLegalTarget(Mobile? target)
    {
        if (target is not BaseCreature bc || bc.Deleted || !bc.Alive || bc.Player)
        {
            return false;
        }

        if (bc.Controlled || bc.ControlMaster != null || bc.SummonMaster != null || bc.GetMaster() != null ||
            bc.Summoned || bc.IsBonded || bc.IsInvulnerable || bc.InitialInnocent || bc.Blessed)
        {
            return false;
        }

        return bc is not (BaseVendor or BaseEscortable or TutorialCreature or LeagueDragonEcho);
    }
}
