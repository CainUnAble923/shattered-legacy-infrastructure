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
    }
}
