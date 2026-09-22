// ServUO: Items/Quest/SAQuestItems.cs, class StygianDragonHead (CC6 batch 8, Part B; one extracted type per file,
// the LuckyCoin precedent). The Stygian Dragon drops one on every death.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class StygianDragonHead : Item
{
    [Constructible]
    public StygianDragonHead(int amount = 1) : base(0x2DB4)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1031700; // Stygian Dragon Head
}
