// cc-P123: a 2016 cooking bulk-order reward (475 points), ported from ServUO
// Scripts/Services/BulkOrders/Items/PlumTreeAddon.cs: a house plum tree on pinned's BaseFruitTreeAddon (as its apple and
// peach trees are), and ServUO's Plum (Items/Consumables/Fruits.cs:780-806), which pinned does not have.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class PlumTreeAddon : BaseFruitTreeAddon
{
    [Constructible]
    public PlumTreeAddon()
    {
        AddComponent(new LocalizedAddonComponent(0x9E38, 1029965), 0, 0, 0);
        AddComponent(new LocalizedAddonComponent(0x9E39, 1029965), 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new PlumTreeAddonDeed();
    public override Item Fruit => new Plum();
}

[SerializationGenerator(0)]
public partial class PlumTreeAddonDeed : BaseAddonDeed
{
    [Constructible]
    public PlumTreeAddonDeed()
    {
    }

    public override BaseAddon Addon => new PlumTreeAddon();
    public override int LabelNumber => 1157312; // Plum Tree
}

[SerializationGenerator(0, false)]
public partial class Plum : Food
{
    [Constructible]
    public Plum(int amount = 1) : base(0x9E86, amount) => FillFactor = 1;

    public override int LabelNumber => 1157208; // plum

    public override double DefaultWeight => 1.0;
}
