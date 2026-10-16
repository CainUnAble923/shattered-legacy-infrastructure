// ServUO: Items/Quest/SAQuestItems.cs, UndeadGargHorn (cc-P129). One extracted type per file (port recipe). The stone
// monster's 20% pack item in its undead-gargoyle form. ServUO's Deserialize corrects an old ItemID; new content never
// needs it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UndeadGargHorn : Item
{
    [Constructible]
    public UndeadGargHorn(int amount = 1) : base(0x315C)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112903; // Undamaged Undead Gargoyle Horns
}
