// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/MysteriousTunnel.cs (cc-P194).
//
// A hole in the floor (item 0x1B71) that moves a player who steps on it, and the player's pets, to its destination
// on the same facet. Laid outside the cell blocks by BedrollSpawner and opened on a bedroll's tile by a bedroll
// that has a destination.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MysteriousTunnel : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _pointDest;

    [Constructible]
    public MysteriousTunnel() : base(0x1B71)
    {
        Movable = false;
    }

    public override int LabelNumber => 1152265; // mysterious tunnel

    public override bool OnMoveOver(Mobile m)
    {
        if (m is PlayerMobile)
        {
            var loc = _pointDest;
            m.MoveToWorld(loc, Map);
            BaseCreature.TeleportPets(m, loc, Map);

            return false;
        }

        return true;
    }
}
