// cc-P199: ServUO pub57 Scripts/Items/Containers/DustyMuseumBag.cs. A reward-bag hued bag (1112994) of 6000 gold,
// 7 to 9 random gems and 3 to 5 random ML resources (rare gems, luminescent fungi, bark fragment, switch,
// parasitic plant), gems and resources stacked. In ServUO it is Axem's second quest reward (Mobiles/NPCs/Axem.cs:66).
//
// One difference from ServUO: no Ter Mur quest reward book. ServUO drops a TerMurQuestRewardBook (DustyMuseumBag.cs:11),
// a type neither pinned nor ours has; the rest of the bag is unchanged. Loot.RandomMLResource and DropItemStacked are
// ServUO's (Misc/Loot.cs:885-888, Containers/Container.cs:104-118) and live on BaseRewardBag (RewardBag.cs).
//
// Its label is ServUO's 1112994, the same number as MeagerImbuingBag's (MeagerImbuingBag.cs:36); kept as ServUO has it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DustyMuseumBag : BaseRewardBag
{
    [Constructible]
    public DustyMuseumBag()
    {
        DropItem(new Gold(6000));

        // ServUO: DropItem(new TerMurQuestRewardBook()); no such type here.

        // ServUO re-rolls the bound on every pass (i < Utility.RandomMinMax(...) in the loop test); kept.
        for (var i = 0; i < Utility.RandomMinMax(7, 9); i++)
        {
            DropItemStacked(this, Loot.RandomGem());
        }

        for (var i = 0; i < Utility.RandomMinMax(3, 5); i++)
        {
            DropItemStacked(this, RandomMLResource());
        }
    }

    public override int LabelNumber => 1112994;
}
