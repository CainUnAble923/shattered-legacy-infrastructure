// ServUO: Items/Resource/MedusaBlood.cs (CC6 batch 8, Part D). A 20% extra from carving Medusa's corpse
// (Medusa.OnCarve). ICommodity as ServUO has it, in ModernUO's shape (the recipe).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MedusaBlood : Item, ICommodity
{
    [Constructible]
    public MedusaBlood(int amount = 1) : base(0x2DB6)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1031702; // Medusa Blood

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
