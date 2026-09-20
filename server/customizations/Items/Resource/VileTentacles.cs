// ServUO: Items/Resource/VileTentacles.cs (CC6 batch 3). Values verbatim; serialization by the generator; the two
// [Constructable] overloads collapsed into one optional parameter. ICommodity is ModernUO's shape
// (Items/Deeds/CommodityDeed.cs:6: int DescriptionNumber + bool IsDeedable), written as stock Shaft.cs and our
// ToxicVenomSac are (batch 2's trap 6).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class VileTentacles : Item, ICommodity
{
    [Constructible]
    public VileTentacles(int amount = 1) : base(0x5727)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1113333; // vile tentacles
    public override double DefaultWeight => 0.1;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
