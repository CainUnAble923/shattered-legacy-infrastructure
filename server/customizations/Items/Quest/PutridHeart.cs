// ServUO: Items/Quest/PutridHeart.cs (CC4 Despise).
//
// Dropped into the possessing player's pack by DespiseRegion.OnBeforeDeath when their possessed creature
// lands the most damage on a wild one, in a stack of power*8 to power*10.
//
// ServUO's OnDoubleClick awards Amount Despise crystal points through PointsSystem.DespiseCrystals and
// deletes the stack. Services/PointsSystems is S7 (gap register B7, costed, unscheduled), so the heart
// is inert here: it stacks, it is kept, it does nothing on use (D-29). The redemption comes back with S7.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PutridHeart : Item
{
    [Constructible]
    public PutridHeart(int amount = 1) : base(0xF91)
    {
        Stackable = true;
        Amount = amount;
        Hue = 2599;
    }

    public override int LabelNumber => 1153424; // putrid heart
}
