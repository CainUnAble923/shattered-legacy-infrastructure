// ServUO: Items/Equipment/Clothing/OuterTorso.cs, Epaulette (cc-P196, a leaf dependency: the Agent of the Crown
// wears one). One extracted type per file (port recipe). Pinned has no epaulette (no clothing at 0x9985; checked;
// the Blackthorn crest epaulettes are Cloaks given this graphic, as ServUO's are). The Constructable pair collapses
// to one optional hue. ServUO's GargishEpaulette (0x9986) is not needed by anything ported and is left.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Epaulette : BaseClothing
{
    [Constructible]
    public Epaulette(int hue = 0) : base(0x9985, Layer.OuterTorso, hue)
    {
        Weight = 1.0;
    }

    public override int LabelNumber => 1123325; // Epaulette
}
