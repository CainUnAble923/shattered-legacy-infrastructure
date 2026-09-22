// ServUO: Items/Resource/MedusaLightScales.cs (CC6 batch 8, Part D). What a knife harvests from Medusa while she
// lives (Medusa.Carve, 2-3 at a time from a stock of 8-9, one minute apart). Same graphic and label as the dark
// scales, hue 1266.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MedusaLightScales : Item
{
    [Constructible]
    public MedusaLightScales(int amount = 1) : base(9908)
    {
        Hue = 1266;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112626; // Medusa Scales
}
