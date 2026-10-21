// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalRoomDoor.cs (cc-P197). The two types ServUO keeps in this
// file: the doors between the Experimental Room's rooms (a MetalDoor2 that opens only for a player carrying an
// ACTIVE Experimental Gem that has earned the room beyond it, or for staff) and the invisible blockers on the same
// tiles (which let the same players through and silently stop everyone else; ServUO's "TODO: Message?" is kept as
// silence). Both carry the Room they guard. ServUO's FindItemsByType array is pinned's enumerator; the two
// messages (1113410 no active gem, 1113411 not earned) fire exactly where ServUO's do.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExperimentalRoomDoor : MetalDoor2
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Room _room;

    [Constructible]
    public ExperimentalRoomDoor(Room room, DoorFacing facing) : base(facing) => _room = room;

    public override string DefaultName => "a door";

    public override void Use(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player)
        {
            from.SendMessage("You open the door with your godly powers.");
            base.Use(from);
            return;
        }

        var pack = from.Backpack;
        var hasGem = false;

        if (pack != null)
        {
            foreach (var gem in pack.FindItemsByType<ExperimentalGem>())
            {
                hasGem = true;

                if (gem.Active && (gem.CurrentRoom > _room || _room == Room.RoomZero))
                {
                    base.Use(from);
                    return;
                }
            }

            if (!hasGem)
            {
                from.SendLocalizedMessage(1113410); // You must have an active Experimental Gem to enter that room.
            }
        }

        if (hasGem)
        {
            from.SendLocalizedMessage(1113411); // You have not yet earned access to that room!
        }
    }
}

[SerializationGenerator(0, false)]
public partial class ExperimentalRoomBlocker : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Room _room;

    [Constructible]
    public ExperimentalRoomBlocker(Room room) : base(7107)
    {
        _room = room;

        Visible = false;
        Movable = false;
    }

    public override bool OnMoveOver(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        var pack = from.Backpack;

        if (pack != null)
        {
            foreach (var gem in pack.FindItemsByType<ExperimentalGem>())
            {
                if (gem.Active && (gem.CurrentRoom > _room || _room == Room.RoomZero))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
