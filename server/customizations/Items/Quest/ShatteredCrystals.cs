// ServUO: Items/Quest/ShatteredCrystals.cs (CC6 batch 4). Values verbatim. A PeerlessKey (P9): on ServUO the Prism of
// Light altar (Items/Functional/PrismOfLightAltar.cs) consumes it; no altar is here, so it is a one-week blessed drop
// from CrystalHydra (25%) with the lifespan line. Its own [SerializationGenerator] gives it its own version slot after
// PeerlessKey's (the subclass rule, notes/cc6-creatures-batch4.md section 2).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShatteredCrystals : PeerlessKey
{
    [Constructible]
    public ShatteredCrystals() : base(0x223F)
    {
        Weight = 1;
        Hue = 0x47E;
    }

    public override int LabelNumber => 1074266; // shattered crystal
}
