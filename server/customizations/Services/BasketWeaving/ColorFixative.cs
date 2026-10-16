// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/ColorFixative.cs -> ours. Colour fixative (Alchemy 75 from
// silver serpent venom and a wine; Cooking 75 the other way round): one plant pigment and one fixative make a natural dye.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ColorFixative : Item
{
    [Constructible]
    public ColorFixative() : base(0x182D)
    {
        Weight = 1.0;
        Hue = 473; // ServUO: "...make this the proper shade of green"
    }

    public override int LabelNumber => 1112135; // color fixative
}
