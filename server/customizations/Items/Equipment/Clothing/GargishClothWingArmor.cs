// ServUO: Items/Equipment/Clothing/Cloaks.cs, GargishClothWingArmor (cc-P196, a leaf dependency of the Blackthorn
// crest wing armor). One extracted type per file (port recipe). Pinned has no cloth wing armor (no clothing at
// 0x45A4; checked). The Constructable pair collapses to one optional hue. CanBeWornByGargoyles => true is dropped:
// pinned's BaseClothing gates wear by RequiredRace alone (BaseClothing.cs:163, :425). AosStrReq 10 is pinned
// BaseClothing's default (BaseClothing.cs:165), so ServUO's override is already the value here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GargishClothWingArmor : BaseClothing
{
    public override Race RequiredRace => Race.Gargoyle;

    [Constructible]
    public GargishClothWingArmor(int hue = 0) : base(0x45A4, Layer.Cloak, hue)
    {
        Weight = 2.0;
    }
}
