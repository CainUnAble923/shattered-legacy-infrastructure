// cc-P199: ServUO pub57 Scripts/Items/Functional/SlidingDoors.cs. Three Tokuno sliding doors (paper, cloth, wooden) on
// pinned's BaseDoor. Each takes a DoorFacing; closed art is the style's base + 2 * facing and open art the next id,
// with sound 0x539 both ways and a zero offset (the door slides in place, so its tile does not move).
//
// Ported as ServUO wrote them. Pinned BaseDoor's constructor (BaseDoor.cs:64) takes the same five arguments in the
// same order (closed id, open id, open sound, close sound, offset), and pinned's DoorFacing (Doors.cs:5) is ServUO's
// enum value for value. ServUO's decoration uses facings 0-3 only (Data/Mondain's Legacy/Malas/Citadel.cfg:138-165,
// Tokuno/Misc.cfg:1-10), which give the sliding-door ids 0x2A05-0x2A1B.
//
// Pinned builds sliding house doors from the same art as GenericHouseDoor (BaseHouse.cs:1849-1855), with sound 0x539
// only for the cloth range; these three are the standalone decoration doors and keep ServUO's 0x539 on all three.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PaperSlidingDoor : BaseDoor
{
    [Constructible]
    public PaperSlidingDoor(DoorFacing facing)
        : base(0x2A05 + 2 * (int)facing, 0x2A06 + 2 * (int)facing, 0x539, 0x539, new Point3D(0, 0, 0))
    {
    }
}

[SerializationGenerator(0, false)]
public partial class ClothSlidingDoor : BaseDoor
{
    [Constructible]
    public ClothSlidingDoor(DoorFacing facing)
        : base(0x2A0D + 2 * (int)facing, 0x2A0E + 2 * (int)facing, 0x539, 0x539, new Point3D(0, 0, 0))
    {
    }
}

[SerializationGenerator(0, false)]
public partial class WoodenSlidingDoor : BaseDoor
{
    [Constructible]
    public WoodenSlidingDoor(DoorFacing facing)
        : base(0x2A15 + 2 * (int)facing, 0x2A16 + 2 * (int)facing, 0x539, 0x539, new Point3D(0, 0, 0))
    {
    }
}
