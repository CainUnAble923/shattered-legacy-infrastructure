using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pack Mule Backpack
    //
    // The cargo hold attached to every Pack Mule.  Vastly larger than a StrongBackpack
    // (which is 1,600 stone / 125 items) — appropriate for a prestige-tier animal.
    //
    //   Weight and item limits: the mule's, by tier (cc-P69, MuleData.Tiers: 1,600 / 150 at tier 1 to 5,000 / 500 at 5)
    // ─────────────────────────────────────────────────────────────────────────────

    [SerializationGenerator(0, false)]
    public partial class PackMuleBackpack : Backpack
    {
        [Constructible]
        public PackMuleBackpack()
        {
            Layer    = Layer.Backpack;
            Movable  = false;
        }

        // cc-P69: the capacity follows the mule wearing it (tier, pack quality, traits, modules: PackMule.Compute).
        // Loose, or on anything else, it is a tier 1 mule's pack.
        public override int DefaultMaxWeight => Parent is PackMule mule ? mule.PackWeightCap : MuleData.Tiers[0].PackWeight;
        public override int DefaultMaxItems  => Parent is PackMule mule ? mule.PackItemsCap : MuleData.Tiers[0].PackItems;

        // cc-P70: the hauling modules make some items weigh less in the pack (Ore Hopper: ore, ingots, granite and gems;
        // Timber Rack: logs and boards; Survey Saddle Roll: treasure maps and SOS bottles weigh nothing). The pinned
        // BaseQuiver precedent (BaseQuiver.cs:114-128): the container's own counters stay raw, and GetTotal takes the
        // discount off. The satchels already reduce their own contents, so items inside one are not discounted again.
        public int WeightDiscount()
        {
            if (Parent is not PackMule mule || !MuleModules.HasWeightModules(mule))
            {
                return 0;
            }

            var discount = 0.0;

            foreach (var item in FindItems())
            {
                if (item is Container || MuleModules.InsideSatchel(item, this))
                {
                    continue;
                }

                discount += (item.TotalWeight + item.PileWeight) * (1.0 - MuleModules.WeightFactor(mule, item));
            }

            return (int)discount;
        }

        public override int GetTotal(TotalType type)
        {
            var total = base.GetTotal(type);
            return type == TotalType.Weight ? total - WeightDiscount() : total;
        }

        // Pinned's capacity check adds the incoming pile at its full weight (Container.cs:257): discount it too.
        public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            if (Parent is PackMule mule && item is not Container)
            {
                plusWeight -= (int)((item.TotalWeight + item.PileWeight) * (1.0 - MuleModules.WeightFactor(mule, item)));
            }

            return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
        }

        public override void UpdateTotal(Item sender, TotalType type, int delta)
        {
            InvalidateProperties();
            base.UpdateTotal(sender, type, delta);
        }
    }
}
