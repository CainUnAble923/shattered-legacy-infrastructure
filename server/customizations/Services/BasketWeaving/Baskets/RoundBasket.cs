// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/Baskets/RoundBasket.cs -> ours. A basket-weaving basket (SA
// Tinkering, softened reeds and shafts, Tinkering 75 and the basket-weaving book). ServUO declares most of these with
// no namespace at all; they sit in Server.Items here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RoundBasket : BaseContainer
{
    [Constructible]
    public RoundBasket() : base(0x990)
    {
        Weight = 1.0;
    }

    public override int LabelNumber => 1112293; // round basket
}
