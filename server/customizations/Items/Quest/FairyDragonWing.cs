// ServUO: Items/Quest/SAQuestItems.cs:348-391, class FairyDragonWing (CC6 batch 4). Extracted to its own file as batch 2
// did for TatteredAncientScroll and batch 3 for the slug and fire-ant items. Values verbatim; the two [Constructable]
// overloads collapsed into one optional-parameter constructor, the recipe's shape. Dropped by FairyDragon at 25%; on
// ServUO Zosilem's tiered quests want ten of them (ZosilemTieredQuests.cs:12), which are not here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class FairyDragonWing : Item
{
    [Constructible]
    public FairyDragonWing(int amount = 1) : base(0x1084)
    {
        Hue = 1111;

        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112899; // Fairy Dragon Wing
}
