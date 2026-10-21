// ServUO: Services/Underworld/Maze of Death/RolledMapOfTheUnderworld.cs (cc-P197). The rolled map (5357) beside
// the golden compass (1072,1055,-36, unmovable): double-clicked within three tiles it shows the Underworld's map
// (gump 0x7739). Pinned's legacy Gump.

using ModernUO.Serialization;
using Server.Gumps;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RolledMapOfTheUnderworld : Item
{
    [Constructible]
    public RolledMapOfTheUnderworld() : base(5357)
    {
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(GetWorldLocation(), 3))
        {
            from.CloseGump<InternalGump>();
            from.SendGump(new InternalGump());
        }
    }

    private class InternalGump : Gump
    {
        public InternalGump() : base(75, 75)
        {
            AddImage(0, 0, 0x7739);
        }
    }
}
