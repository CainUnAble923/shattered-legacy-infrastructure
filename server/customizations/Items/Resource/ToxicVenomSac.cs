// ServUO: Items/Resource/ToxicVenomSac.cs (CC6 batch 2). Values verbatim; serialization by the generator; the two
// [Constructable] overloads collapsed into one optional-parameter [Constructible]. ICommodity is ModernUO's shape
// (Items/Deeds/CommodityDeed.cs:6: int DescriptionNumber + bool IsDeedable), written as stock Shaft.cs does; ServUO's
// is TextDefinition Description + bool IsDeedable. Same two facts, one type change.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ToxicVenomSac : Item, ICommodity
{
    [Constructible]
    public ToxicVenomSac(int amount = 1) : base(0x4005)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112291; // toxic venom sac

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
