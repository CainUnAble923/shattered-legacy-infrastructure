// ServUO: Items/Decorative/Fur.cs (CC6 batch 2). Values verbatim; serialization by the generator; the two
// [Constructable] overloads collapsed into one optional-parameter [Constructible] (notes/port-recipe.md).
// FurType is ours (Mobiles/FurType.cs); the TypeAlias is ServUO's, kept so a save written under its old names
// would still load.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("Server.Items.BouraFur", "Server.Items.KepetchFur")]
[SerializationGenerator(0, false)]
public partial class Fur : Item
{
    [Constructible]
    public Fur(FurType type = FurType.None, int amount = 1) : base(0x1875)
    {
        Stackable = true;
        Amount = amount;

        Hue = type switch
        {
            FurType.Green      => 58,
            FurType.LightBrown => 1541,
            FurType.Yellow     => 153,
            FurType.Brown      => 343,
            _                  => 0
        };
    }
}
