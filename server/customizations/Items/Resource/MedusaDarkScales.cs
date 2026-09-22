// ServUO: Items/Resource/MedusaDarkScales.cs (CC6 batch 8, Part D). What carving Medusa's corpse yields (1-5,
// Medusa.OnCarve). Same graphic and label as the light scales, hue 2223.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MedusaDarkScales : Item
{
    [Constructible]
    public MedusaDarkScales(int amount = 1) : base(9908)
    {
        Hue = 2223;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112626; // Medusa Scales
}
