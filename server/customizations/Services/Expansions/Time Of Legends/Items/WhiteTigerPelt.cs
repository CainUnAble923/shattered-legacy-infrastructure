// ServUO: Services/Expansions/Time Of Legends/Items/Resources.cs, WhiteTigerPelt (cc-P129). One extracted type per
// file (port recipe). The wild white tiger's carve yield.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WhiteTigerPelt : Item, ICommodity
{
    [Constructible]
    public WhiteTigerPelt(int amount = 1) : base(0x9BCC)
    {
        Weight = 1.0;
        Stackable = true;
        Amount = amount;
        Hue = 0;
    }

    public override int LabelNumber => 1156201;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
