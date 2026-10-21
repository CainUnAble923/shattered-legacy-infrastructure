// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Resources/SmallPieceofBlackrock.cs (cc-P195): blackrock, ten
// to the Sacrificial Dagger and ten to the Summoning Altar deed; one dig in a thousand at 100 Mining
// (ExodusMiningBonus). Pinned's ICommodity shape; the collapsed constructor pair.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmallPieceofBlackrock : Item, ICommodity
{
    [Constructible]
    public SmallPieceofBlackrock(int amount = 1) : base(0x0F28)
    {
        Hue = 1175;
        Stackable = true;
        Amount = amount;
        Weight = 1.0;
    }

    public override int LabelNumber => 1150016; // small piece of blackrock

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
