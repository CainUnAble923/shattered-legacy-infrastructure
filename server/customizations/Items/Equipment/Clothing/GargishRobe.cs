// ServUO: Items/Equipment/Clothing/Cloaks.cs, GargishRobe (cc-P129). One extracted type per file (port recipe). The
// gargish outcast wears one half the time. Pinned has no gargish robe (no clothing at 0x4000; checked). Flipable is
// pinned's Flippable; the Constructable pair collapses to one optional hue. CanBeWornByGargoyles => true is dropped:
// pinned's BaseClothing has no such virtual and gates wear by RequiredRace alone (BaseClothing.cs:163, :425).

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4000, 0x4001)]
[SerializationGenerator(0, false)]
public partial class GargishRobe : BaseClothing
{
    public override Race RequiredRace => Race.Gargoyle;

    [Constructible]
    public GargishRobe(int hue = 0) : base(0x4000, Layer.OuterTorso, hue)
    {
        Weight = 3.0;
    }
}
