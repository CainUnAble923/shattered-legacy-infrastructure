// ServUO: Items/Quest/SAQuestItems.cs:247-290, one extracted type per file (the LuckyCoin precedent). Values verbatim;
// serialization by the generator; the two [Constructable] overloads collapsed into one optional parameter.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CongealedSlugAcid : Item
{
    [Constructible]
    public CongealedSlugAcid(int amount = 1) : base(0x5742)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112901; // Congealed Slug Acid
}
