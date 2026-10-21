// ServUO: Services/Expansions/Time Of Legends/Items/Resources.cs, DragonTurtleScute (cc-P198). One extracted type per
// file (port recipe); the file's other three types, the tiger pelts, came with WildTiger (cc-P129). The carve yield
// of the Dragon Turtle and its hatchling; ICommodity is pinned's int shape.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DragonTurtleScute : Item, ICommodity
{
    [Constructible]
    public DragonTurtleScute(int amount = 1) : base(0x9BCE)
    {
        Weight = 1.0;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1123910;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
