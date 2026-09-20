// ServUO: Items/Quest/SAQuestItems.cs:679-725, one extracted type per file (the LuckyCoin precedent). Values verbatim;
// serialization by the generator; the two [Constructable] overloads collapsed into one optional parameter. ServUO's
// Deserialize forces ItemID back to 0x122E for old saves; not ported, new content starts at version 0.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SearedFireAntGoo : Item
{
    [Constructible]
    public SearedFireAntGoo(int amount = 1) : base(0x122E)
    {
        Stackable = true;
        Amount = amount;
        Hue = 1359;
    }

    public override int LabelNumber => 1112902; // Seared Fire Ant Goo
}
