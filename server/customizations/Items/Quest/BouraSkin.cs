// ServUO: Items/Quest/BouraSkin.cs (CC6 batch 2). Values verbatim; serialization by the generator.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BouraSkin : Item
{
    [Constructible]
    public BouraSkin() : base(0x11F4)
    {
        LootType = LootType.Blessed;
        Weight = 1.0;
        Hue = 0x292;
    }

    public override int LabelNumber => 1112900; // Boura Skin
}
