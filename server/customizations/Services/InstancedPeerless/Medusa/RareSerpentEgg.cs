// cc-P199: ServUO pub57 Scripts/Services/InstancedPeerless/Medusa/RareSerpentEgg.cs. "a rare serpent egg" (art 0x41BF,
// blessed, weight 1, one of hues 0x21, 0x4AC or 0x41C): a PeerlessKey (ours, Services/Peerless/PeerlessKey.cs) with a
// 12-hour lifespan (43,200 s) instead of the base's week. On ServUO it is the key to Medusa's instanced lair; no such
// altar is here (gap register B14, WON'T), so it is a 12-hour blessed find, the shape DraconicOrb already has.
// Ported with this batch as the leaf item SerpentNest (Items/Functional/SerpentNest.cs) gives out. Values verbatim.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RareSerpentEgg : PeerlessKey
{
    [Constructible]
    public RareSerpentEgg() : base(0x41BF)
    {
        Weight = 1.0;
        LootType = LootType.Blessed;
        Hue = Utility.RandomList(0x21, 0x4AC, 0x41C);
    }

    public override int LabelNumber => 1112575; // a rare serpent egg

    public override int Lifespan => 43200;
}
