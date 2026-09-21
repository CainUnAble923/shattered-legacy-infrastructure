// ServUO: Items/Quest/DragonFlameKey.cs (CC6 batch 7). Values verbatim. A PeerlessKey (P9), batch 4's SerpentFangKey
// shape: on ServUO the Citadel peerless altar (Items/Functional/CitadelAltar.cs) consumes it with the tiger claw and
// serpent fang keys; no altar is here, so it is a one-week blessed drop from every MageDragonsFlameMage death, with
// the lifespan line.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DragonFlameKey : PeerlessKey
{
    [Constructible]
    public DragonFlameKey() : base(0x2002)
    {
        Weight = 2.0;
        Hue = 42;
        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1074343; // dragon flame key
}
