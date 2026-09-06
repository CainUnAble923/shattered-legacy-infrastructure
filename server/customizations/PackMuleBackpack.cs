using ModernUO.Serialization;
using Server.Items;

namespace Server.Items
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pack Mule Backpack
    //
    // The cargo hold attached to every Pack Mule.  Vastly larger than a StrongBackpack
    // (which is 1,600 stone / 125 items) — appropriate for a prestige-tier animal.
    //
    //   Weight limit : 4,000 stone
    //   Item limit   : 400 items
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

        public override int DefaultMaxWeight => 4000;
        public override int DefaultMaxItems  => 400;
    }
}
