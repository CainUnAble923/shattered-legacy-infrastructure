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
    public const string Refusal = "Finish the main tutorial lessons, then double-click Rook on the New Haven plaza and choose Graduate. You can leave Haven Island afterward.";

    public static void Configure() =>
        Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.islandGate", true);

    // Pinned Distribution/Data/regions.json: Haven Island NoHousingRegion (3314,2345,500,750), read from HavenSpots (cc-P663;
    // S7 moves it with New Haven). Include the island's underground tutorial space: Rootdeep is stored off-map, but its
    // only entrance is on Old Haven island. F-35 needs trainees to go down and return. It must not become a mainland-exit
    // bypass. These bounds work before regions are registered, and include the docks and surrounding water.
    // cc-P663 (P605 risk 4): the Fold, where the Wakening Isle is (map 32 and its copies), counts as the island too. That is
    // the allowed route for a loop-0 trainee: the start choice and Rook's remedial trip from Haven onto the isle, and the
    // dock moongate back. Nothing else reaches the Fold (FoldRegion refuses every travel spell into and out of it).
    public static bool IsOnIsland(Point3D location, Map? map) =>
        FoldMaps.IsFold(map) ||
        map == HavenSpots.Map && (HavenSpots.InIslandBounds(location) || OldHavenBurrowsRegion.Area.Contains(location));

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
