// ServUO: Services/Underworld/PuzzleRoom/PuzzleRoomTeleporter.cs (cc-P197). The two teleporters at the Puzzle
// Room's door (1097,1175 and 1098,1175, z 0, into 1097/1098,1173 z 1): they carry only a player with a magic key in
// the pack (1113393 otherwise). Pinned Teleporter's CanTeleport (Teleporter.cs:186) is the same hook; its
// PointDest and MapDest are set by the generator as ServUO's are.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PuzzleRoomTeleporter : Teleporter
{
    [Constructible]
    public PuzzleRoomTeleporter()
    {
    }

    public override bool CanTeleport(Mobile m)
    {
        if (m.Backpack == null)
        {
            return false;
        }

        if (m.Backpack.FindItemByType<MagicKey>() == null)
        {
            m.SendLocalizedMessage(1113393); // You must carry a magic key to enter this room.
            return false;
        }

        return base.CanTeleport(m);
    }
}
