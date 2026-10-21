// cc-P199: ServUO pub57 Scripts/Items/Containers/MeagerImbuingBag.cs. "Meager Imbuing Bag" (ServUO's label 1112994),
// a reward-bag hued bag holding one of slith tongue, goblin blood, reflective wolf eye or raptor teeth. In ServUO it is
// the reward of the quest A Little Something (Quests/ALittleSomething.cs:13). No difference from ServUO.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MeagerImbuingBag : BaseRewardBag
{
    [Constructible]
    public MeagerImbuingBag()
    {
        switch (Utility.Random(4))
        {
            case 0:
                DropItem(new SlithTongue());
                break;
            case 1:
                DropItem(new GoblinBlood());
                break;
            case 2:
                DropItem(new ReflectiveWolfEye());
                break;
            case 3:
                DropItem(new RaptorTeeth());
                break;
        }
    }

    // ServUO's value. The same number is DustyMuseumBag's label (DustyMuseumBag.cs:33), kept as ServUO has it.
    public override int LabelNumber => 1112994;
}
