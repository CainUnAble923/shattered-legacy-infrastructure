// P371: a single destination rule, reached by movement, spells and boat preflight patches.
#nullable enable
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells;
using Server.Spells.Sixth;

namespace Server.Tutorial;

public static class TutorialIslandGate
{
    public static bool Enabled { get; set; } = true;
    public const string Refusal = "System: Graduate with the League recruiter in New Haven before leaving the island.";

    public static void Configure() =>
        Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.islandGate", true);

    // Pinned Distribution/Data/regions.json: Haven Island NoHousingRegion (3314,2345,500,750).
    // Include the island's underground tutorial space: Rootdeep is stored off-map, but its only entrance is on
    // Old Haven island. F-35 needs trainees to go down and return. It must not become a mainland-exit bypass.
    // These bounds work before regions are registered, and include the docks and surrounding water.
    public static bool IsOnIsland(Point3D location, Map? map) => map == Map.Trammel &&
        (location.X >= 3314 && location.X < 3814 && location.Y >= 2345 && location.Y < 3095 ||
         OldHavenBurrowsRegion.Area.Contains(location));

    private static bool NeedsGraduation(Mobile? mobile) => Enabled && mobile is PlayerMobile pm &&
        pm.AccessLevel == AccessLevel.Player && IsOnIsland(pm.Location, pm.Map) && ClusterFLoops.LoopOf(pm) == 0;

    public static bool CanMove(Mobile mobile, Point3D destination, Map? map, bool message = true)
    {
        // Internalization is server lifecycle (logout/deletion), not travel to another area.
        if (!NeedsGraduation(mobile) || map == null || map == Map.Internal || IsOnIsland(destination, map))
        {
            return true;
        }
        if (message)
        {
            mobile.SendMessage(0x3B2, Refusal);
        }
        return false;
    }

    public static bool CanCast(Mobile mobile, ISpell spell)
    {
        if (spell is not MarkSpell || !NeedsGraduation(mobile))
        {
            return true;
        }
        mobile.SendMessage(0x3B2, Refusal);
        return false;
    }

    public static bool CanTravel(Mobile? mobile, Map map, Point3D destination, TravelCheckType type)
    {
        if (mobile == null || !NeedsGraduation(mobile))
        {
            return true;
        }
        return type switch
        {
            TravelCheckType.Mark => false,
            TravelCheckType.RecallTo or TravelCheckType.GateTo or TravelCheckType.TeleportTo =>
                CanMove(mobile, destination, map, false),
            _ => true
        };
    }

    public static bool CanMoveBoat(BaseBoat boat, int xOffset, int yOffset, int zOffset)
    {
        if (!Enabled)
        {
            return true;
        }
        foreach (var entity in boat.GetMovingEntities())
        {
            if (entity is PlayerMobile pm && !CanMove(pm,
                    new Point3D(pm.X + xOffset, pm.Y + yOffset, pm.Z + zOffset), boat.Map))
            {
                return false;
            }
        }
        return true;
    }
}
