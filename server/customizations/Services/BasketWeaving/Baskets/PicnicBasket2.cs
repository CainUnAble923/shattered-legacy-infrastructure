// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/Baskets/PicnicBasket2.cs -> ours. A basket-weaving basket (SA
// Tinkering, softened reeds and shafts, Tinkering 75 and the basket-weaving book). ServUO declares most of these with
// no namespace at all; they sit in Server.Items here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PicnicBasket2 : BaseContainer
{
    [Constructible]
    public PicnicBasket2() : base(0xE7A)
    {
        Weight = 1.0;
    }
}
