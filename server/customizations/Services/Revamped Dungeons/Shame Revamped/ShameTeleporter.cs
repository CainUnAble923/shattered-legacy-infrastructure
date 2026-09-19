// ServUO: Services/Revamped Dungeons/Shame Revamped/ShameTeleporter.cs (CC4 Shame).
//
// ShameTeleporter: the teleporter each Guardian's Altar places (and re-places) at its TeleporterLocation,
// creatures allowed. ShameWallTeleporter: the pair in front of and behind each ShameWall; it only works while
// the wall addon's Visible flag is set, and says "The wall becomes transparent" as it moves you. Both drop
// ServUO's version-0 read of a mobile list that never existed here.
//
// Both keep ServUO's shape: the wall teleporter overrides CanTeleport WITHOUT calling base, so the Creatures
// flag is not consulted there. Teleporter(Point3D, Map, bool creatures) exists on both emulators.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Engines.ShameRevamped;

[SerializationGenerator(0, false)]
public partial class ShameTeleporter : Teleporter
{
    public ShameTeleporter(Point3D dest, Map map) : base(dest, map, true)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class ShameWallTeleporter : Teleporter
{
    public ShameWallTeleporter(Point3D dest, Map map) : base(dest, map, true)
    {
    }

    public override bool CanTeleport(Mobile m)
    {
        if (Deleted || Map == null || Map == Map.Internal)
        {
            return false;
        }

        foreach (var item in Map.GetItemsInRange(Location, 1))
        {
            if (item is AddonComponent { Addon: ShameWall wall } && wall.Visible)
            {
                return true;
            }
        }

        return false;
    }

    public override void DoTeleport(Mobile m)
    {
        m.SendLocalizedMessage(1072790); // The wall becomes transparent, and you push your way through it.

        base.DoTeleport(m);
    }
}
