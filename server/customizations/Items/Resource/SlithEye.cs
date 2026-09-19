// ServUO: Items/Resource/SlithEye.cs (CC6 batch 2). Values verbatim; serialization by the generator; the two
// [Constructable] overloads collapsed into one optional-parameter [Constructible].

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SlithEye : Item
{
    [Constructible]
    public SlithEye(int amount = 1) : base(0x5749)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112396; // slith's eye
}
