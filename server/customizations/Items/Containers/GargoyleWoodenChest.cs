// cc-P199: ServUO pub57 Scripts/Items/Containers/GargoyleWoodenChest.cs. The gargoyle wooden chest (art 0x4025/0x4026,
// tiledata "gargoyle chest"), a lockable furniture chest on container gump 0x42. In ServUO it is not crafted or sold;
// it appears only in the engraving tool's list of engravable items (Items/Consumables/EngravingTools.cs:568).
//
// ServUO sets Weight = 2.0 in the constructor; here it is DefaultWeight, the way pinned writes its own chests
// (Container.cs WoodenChest). ServUO's Deserialize fix-up from 15 stones to 2 (GargoyleWoodenChest.cs:35-36) is for
// ServUO saves only and is dropped.

using ModernUO.Serialization;

namespace Server.Items;

[Furniture]
[Flippable(0x4025, 0x4026)]
[SerializationGenerator(0, false)]
public partial class GargoyleWoodenChest : LockableContainer
{
    [Constructible]
    public GargoyleWoodenChest() : base(0x4025) => GumpID = 0x42;

    public override double DefaultWeight => 2.0;
}
