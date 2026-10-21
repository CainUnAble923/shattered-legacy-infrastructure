// cc-P199: ServUO pub57 Scripts/Items/Containers/ValuableImbuingBag.cs. "Valuable Imbuing Bag" (1113769), a
// reward-bag hued bag holding one undying flesh or one daemon claw. In ServUO it is Aliabeth's second quest reward
// (Mobiles/NPCs/Aliabeth.cs:97). No difference from ServUO: its four other cases (ValuableImbuingBag.cs:19-30, among
// them LavaSerpenCrust) are commented out there and stay out here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ValuableImbuingBag : BaseRewardBag
{
    [Constructible]
    public ValuableImbuingBag()
    {
        switch (Utility.Random(2))
        {
            case 0:
                DropItem(new UndyingFlesh());
                break;
            case 1:
                DropItem(new DaemonClaw());
                break;
        }
    }

    public override int LabelNumber => 1113769; // Valuable Imbuing Bag
}
