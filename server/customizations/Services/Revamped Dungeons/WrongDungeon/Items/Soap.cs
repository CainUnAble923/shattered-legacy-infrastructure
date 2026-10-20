// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/Soap.cs (cc-P194).
//
// A bar of soap (beeswax graphic 0x1422 hued 1285), stackable, found one time in a hundred in a torn Wrong bedroll.
// ServUO's X() + X(int) constructor pair collapsed into one optional parameter (the recipe's Shaft.cs shape).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Soap : Item
{
    [Constructible]
    public Soap(int amount = 1) : base(0x1422)
    {
        Hue = 1285;
        Stackable = true;
        Amount = amount;
    }

    public override double DefaultWeight => 1.0;

    public override int LabelNumber => 1152267; // soap
}
