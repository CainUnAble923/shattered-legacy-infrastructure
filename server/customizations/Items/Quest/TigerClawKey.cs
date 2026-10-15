// ServUO: Items/Quest/TigerClawKey.cs (cc-P119). Values verbatim. A PeerlessKey, the shape of batch 7's DragonFlameKey and
// batch 4's SerpentFangKey: on ServUO the Citadel altar consumes the three keys; no altar is here, so it is a
// one-week blessed drop from every TigersClawMaster death.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TigerClawKey : PeerlessKey
{
    [Constructible]
    public TigerClawKey()
        : base(0x2002)
    {
        Weight = 2.0;
        Hue = 105;
        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1074342; // tiger claw key
}
