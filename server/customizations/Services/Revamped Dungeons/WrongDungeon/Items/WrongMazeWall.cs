// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/WrongMazeWall.cs (cc-P194).
//
// The level-3 maze's walls: invisible until a mobile stands within one tile of them, invisible again when none
// does. Labelled "a dungeon wall" (or its Name) on a single click.
//
// Conversion notes: ServUO sends an AsciiMessage packet from the item; Item.LabelTo(from, text) is the same
// packet here. GetMobilesInRange(2) is Item's.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WrongMazeWall : BaseWall
{
    [Constructible]
    public WrongMazeWall() : base(578)
    {
        Movable = false;
        Visible = false;
    }

    public override bool HandlesOnMovement => true;

    public override void OnSingleClick(Mobile from)
    {
        LabelTo(from, Name ?? "a dungeon wall");
    }

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        foreach (var mobile in GetMobilesInRange(2))
        {
            if (mobile.InRange(Location, 1))
            {
                Visible = true;
                return;
            }
        }

        Visible = false;
    }
}
