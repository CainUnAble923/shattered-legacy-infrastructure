// cc-P199: ServUO pub57 Scripts/Items/Containers/MeagerMuseumBag.cs. A reward-bag hued bag (1112993) of 3000 gold,
// 5 to 7 random gems and 1 to 3 random ML resources (rare gems, luminescent fungi, bark fragment, switch,
// parasitic plant), gems and resources stacked. In ServUO it is Axem's first quest reward (Mobiles/NPCs/Axem.cs:14).
//
// One difference from ServUO: no Ter Mur quest reward book. ServUO drops a TerMurQuestRewardBook (MeagerMuseumBag.cs:11),
// a type neither pinned nor ours has; the rest of the bag is unchanged. Loot.RandomMLResource and DropItemStacked are
// ServUO's (Misc/Loot.cs:885-888, Containers/Container.cs:104-118) and live on BaseRewardBag (RewardBag.cs).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MeagerMuseumBag : BaseRewardBag
{
    [Constructible]
    public MeagerMuseumBag()
    {
        DropItem(new Gold(3000));

        // ServUO: DropItem(new TerMurQuestRewardBook()); no such type here.

        // ServUO re-rolls the bound on every pass (i < Utility.RandomMinMax(...) in the loop test); kept.
        for (var i = 0; i < Utility.RandomMinMax(5, 7); i++)
        {
            DropItemStacked(this, Loot.RandomGem());
        }

        for (var i = 0; i < Utility.RandomMinMax(1, 3); i++)
        {
            DropItemStacked(this, RandomMLResource());
        }
    }

    public override int LabelNumber => 1112993;
}
