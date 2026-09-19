// ServUO: Items/Quest/SAQuestItems.cs:769-817, one extracted type per file (the LuckyCoin precedent). Values
// verbatim; serialization by the generator. ServUO's (int amount) overload ignores its argument and sets no
// Stackable, so a scroll is always a single item; the optional parameter is kept only so `[add
// TatteredAncientScroll 1` keeps working. Its Deserialize upgrade (version 0 -> ItemID 0x1437, unstack) is for
// old saves and is not ported; new content starts at version 0 with the constructor's 0x1700.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TatteredAncientScroll : Item
{
    [Constructible]
    public TatteredAncientScroll(int amount = 1) : base(0x1700)
    {
    }

    public override int LabelNumber => 1112991; // Tattered Remnants of an Ancient Scroll
}
