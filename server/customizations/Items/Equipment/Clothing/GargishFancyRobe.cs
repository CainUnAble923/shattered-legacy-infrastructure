// ServUO: Items/Equipment/Clothing/Cloaks.cs, GargishFancyRobe (cc-P196, a leaf dependency of the Blackthorn crest
// robes). One extracted type per file (port recipe). Pinned has no gargish fancy robe (no clothing at 0x4002;
// checked). Flipable is pinned's Flippable; the Constructable pair collapses to one optional hue.
// CanBeWornByGargoyles => true is dropped: pinned's BaseClothing gates wear by RequiredRace alone
// (BaseClothing.cs:163, :425), the same reading as Items/Equipment/Clothing/GargishRobe.cs (cc-P129).

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4002, 0x4003)]
[SerializationGenerator(0, false)]
public partial class GargishFancyRobe : BaseClothing
{
    public override Race RequiredRace => Race.Gargoyle;

    [Constructible]
    public GargishFancyRobe(int hue = 0) : base(0x4002, Layer.OuterTorso, hue)
    {
        Weight = 3.0;
    }
}
