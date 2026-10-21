// cc-P199: ServUO pub57 Scripts/Items/Containers/BulgingMuseumBag.cs. A reward-bag hued bag (1112995) of 10000 gold,
// 10 to 15 random gems and 5 to 7 random ML resources (rare gems, luminescent fungi, bark fragment, switch,
// parasitic plant), gems and resources stacked. In ServUO it is Axem's third quest reward (Mobiles/NPCs/Axem.cs:118).
//
// One difference from ServUO: no Ter Mur quest reward book. ServUO drops a TerMurQuestRewardBook (BulgingMuseumBag.cs:13),
// a type neither pinned nor ours has; the rest of the bag is unchanged. Loot.RandomMLResource and DropItemStacked are
// ServUO's (Misc/Loot.cs:885-888, Containers/Container.cs:104-118) and live on BaseRewardBag (RewardBag.cs).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BulgingMuseumBag : BaseRewardBag
{
    [Constructible]
    public BulgingMuseumBag()
    {
        DropItem(new Gold(10000));

        // ServUO: DropItem(new TerMurQuestRewardBook()); no such type here.

        // ServUO re-rolls the bound on every pass (i < Utility.RandomMinMax(...) in the loop test); kept.
        for (var i = 0; i < Utility.RandomMinMax(10, 15); i++)
        {
            DropItemStacked(this, Loot.RandomGem());
        }

        for (var i = 0; i < Utility.RandomMinMax(5, 7); i++)
        {
            DropItemStacked(this, RandomMLResource());
        }
    }

    public override int LabelNumber => 1112995;
}
