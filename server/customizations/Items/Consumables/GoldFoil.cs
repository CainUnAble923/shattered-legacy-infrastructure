// cc-P128: ServUO pub57 Scripts/Items/Consumables/GoldFoil.cs -> ours. A foil sheet (the chocolate nutcracker's wrapper
// in the Time of Legends cooking table).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GoldFoil : Item
{
    [Constructible]
    public GoldFoil(int amount = 1) : base(0x9C48)
    {
        Stackable = true;
        Amount = amount;
        Weight = 2.0;
        Hue = 1281;
    }

    public override int LabelNumber => 1124032; // foil sheet
}
