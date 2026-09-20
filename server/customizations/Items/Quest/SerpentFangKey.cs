// ServUO: Items/Quest/SerpentFangKey.cs (CC6 batch 4). Values verbatim. A PeerlessKey (P9): on ServUO the Citadel
// peerless altar consumes it; no altar is here, so it is a one-week blessed drop from every
// SerpentsFangHighExecutioner death, with the lifespan line.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SerpentFangKey : PeerlessKey
{
    [Constructible]
    public SerpentFangKey() : base(0x2002)
    {
        Weight = 2.0;
        Hue = 53;
        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1074341; // serpent fang key
}
