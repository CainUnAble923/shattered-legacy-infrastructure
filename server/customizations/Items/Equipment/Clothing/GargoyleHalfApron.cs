// ServUO: Items/Equipment/Clothing/Waist.cs, GargoyleHalfApron (cc-P196, a leaf dependency of the Blackthorn crest
// half aprons; ServUO's own name, kept). One extracted type per file (port recipe). Pinned has no gargish half
// apron (no clothing at 0x50D8; checked). The Constructable pair collapses to one optional hue.
// CanBeWornByGargoyles => true is dropped: pinned's BaseClothing gates wear by RequiredRace alone
// (BaseClothing.cs:163, :425).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GargoyleHalfApron : BaseWaist
{
    public override Race RequiredRace => Race.Gargoyle;

    [Constructible]
    public GargoyleHalfApron(int hue = 0) : base(0x50D8, hue)
    {
        Weight = 2.0;
    }
}
