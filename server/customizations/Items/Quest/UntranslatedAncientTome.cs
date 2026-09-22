// ServUO: Items/Quest/SAQuestItems.cs, class UntranslatedAncientTome (CC6 batch 8, Part C; one extracted type per
// file, the LuckyCoin precedent). Navrey Night-Eyes drops one on half her deaths. ServUO's Deserialize resets an
// old ItemID; version-0 content never runs it (the recipe).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UntranslatedAncientTome : Item
{
    [Constructible]
    public UntranslatedAncientTome(int amount = 1) : base(0x0FF2)
    {
        Stackable = true;
        Amount = amount;

        Hue = 2405;
    }

    public override int LabelNumber => 1112992; // Untranslated Ancient Tome
}
