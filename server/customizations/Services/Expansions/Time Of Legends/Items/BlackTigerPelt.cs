// ServUO: Services/Expansions/Time Of Legends/Items/Resources.cs, BlackTigerPelt (cc-P129). One extracted type per
// file (port recipe). The wild black tiger's carve yield.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlackTigerPelt : Item, ICommodity
{
    [Constructible]
    public BlackTigerPelt(int amount = 1) : base(0x9BCC)
    {
        Weight = 1.0;
        Stackable = true;
        Amount = amount;
        Hue = 1175;
    }

    public override int LabelNumber => 1156200;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
