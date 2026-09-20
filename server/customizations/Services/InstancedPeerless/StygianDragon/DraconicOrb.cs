// ServUO: Services/InstancedPeerless/StygianDragon/DraconicOrb.cs (CC6 batch 4). Values verbatim. A PeerlessKey (P9)
// with a 12-hour lifespan (43,200 s) instead of the base's week: on ServUO the Stygian Dragon platform
// (StygianDragonPlatform.cs) consumes it; no platform is here, so it is a 12-hour blessed drop from FairyDragon (10%).
// The Instanced Peerless engine this file sits beside on ServUO is not ported (B14, WON'T); only the key is.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DraconicOrb : PeerlessKey
{
    [Constructible]
    public DraconicOrb() : base(0x573E)
    {
        Weight = 1.0;
        LootType = LootType.Blessed;
        Hue = 0x80F;
    }

    public override int LabelNumber => 1113515; // Draconic Orb (Lesser)

    public override int Lifespan => 43200;
}
