// ServUO: Items/Equipment/Clothing/MiddleTorso.cs, GargishSash (cc-P196, a leaf dependency of the Blackthorn crest
// sash). One extracted type per file (port recipe). Pinned has no gargish sash (no clothing at 0x46B4; checked).
// The Constructable pair collapses to one optional hue. CanBeWornByGargoyles => true is dropped: pinned's
// BaseClothing gates wear by RequiredRace alone (BaseClothing.cs:163, :425).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GargishSash : BaseClothing
{
    public override Race RequiredRace => Race.Gargoyle;

    [Constructible]
    public GargishSash(int hue = 0) : base(0x46B4, Layer.MiddleTorso, hue)
    {
        Weight = 1.0;
    }
}
