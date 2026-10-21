// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Resources/GoldDust.cs (cc-P195): the Alchemy product (90
// skill, a thousand gold) the Robe of Rite takes five of. Pinned's ICommodity shape; the collapsed constructor pair.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GoldDust : Item, ICommodity
{
    [Constructible]
    public GoldDust(int amount = 1) : base(0x4C09)
    {
        Stackable = true;
        Hue = 1177;
        Weight = 1.0;
        Amount = amount;
    }

    public override int LabelNumber => 1153504; // gold dust

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
