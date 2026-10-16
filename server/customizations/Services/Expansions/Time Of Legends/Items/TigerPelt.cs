// ServUO: Services/Expansions/Time Of Legends/Items/Resources.cs, TigerPelt (cc-P129). One extracted type per file
// (port recipe). The wild tiger's carve yield; Flipable is pinned's Flippable, ICommodity is pinned's int shape.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x9BCC, 0x9BCD)]
[SerializationGenerator(0, false)]
public partial class TigerPelt : Item, ICommodity
{
    [Constructible]
    public TigerPelt(int amount = 1) : base(0x9BCC)
    {
        Weight = 1.0;
        Stackable = true;
        Amount = amount;

        Hue = 243;
    }

    public override int LabelNumber => 1123908;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
