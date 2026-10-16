// ServUO: Items/Quest/SAQuestItems.cs, UndamagedIronBeetleScale (cc-P129). One extracted type per file (port recipe,
// LuckyCoin's precedent from the same ServUO file). The iron beetle's 10% drop. ServUO's Deserialize corrects an old
// ItemID; new content never needs it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UndamagedIronBeetleScale : Item, ICommodity
{
    [Constructible]
    public UndamagedIronBeetleScale(int amount = 1) : base(0x26B3)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112905; // Undamaged Iron Beetle Scale

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
