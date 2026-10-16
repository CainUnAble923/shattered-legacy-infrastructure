// ServUO: Items/Quest/SAQuestItems.cs, LeatherWolfSkin (cc-P129). One extracted type per file (port recipe). The
// leather wolf's 20% drop. ServUO's Deserialize re-hues a version-0 skin; new content never needs it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class LeatherWolfSkin : Item
{
    [Constructible]
    public LeatherWolfSkin(int amount = 1) : base(0xDF8)
    {
        Stackable = true;
        Amount = amount;

        Hue = 248;
    }

    public override int LabelNumber => 1112906; // leather wolf skin
}
