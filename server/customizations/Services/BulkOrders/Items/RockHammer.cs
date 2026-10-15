// cc-P122: a 2016 smith bulk-order reward (500 points), ported from ServUO Scripts/Services/BulkOrders/Items/RockHammer.cs.
// A mining tool with 500 uses.

using ModernUO.Serialization;
using Server.Engines.Harvest;

namespace Server.Items;

[Flippable(0x9E7E, 0x9E7F)]
[SerializationGenerator(0, false)]
public partial class RockHammer : BaseHarvestTool
{
    [Constructible]
    public RockHammer(int uses = 500) : base(0x9E7E, uses)
    {
    }

    public override int LabelNumber => 1124598; // rock hammer

    public override double DefaultWeight => 5.0;

    public override HarvestSystem HarvestSystem => Mining.System;
}
