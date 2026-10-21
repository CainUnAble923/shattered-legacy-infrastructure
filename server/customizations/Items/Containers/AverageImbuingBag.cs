// cc-P199: ServUO pub57 Scripts/Items/Containers/AverageImbuingBag.cs. "Average Imbuing Bag" (1113768), a reward-bag
// hued bag holding one spider carapace or one silver snake skin. In ServUO it is Aliabeth's quest reward
// (Mobiles/NPCs/Aliabeth.cs:19). No difference from ServUO.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AverageImbuingBag : BaseRewardBag
{
    [Constructible]
    public AverageImbuingBag()
    {
        switch (Utility.Random(2))
        {
            case 0:
                DropItem(new SpiderCarapace());
                break;
            case 1:
                DropItem(new SilverSnakeSkin());
                break;
        }
    }

    public override int LabelNumber => 1113768; // Average Imbuing Bag
}
