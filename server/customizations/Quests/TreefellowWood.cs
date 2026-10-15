// ServUO: Quests/TreefellowWood.cs (cc-P119). Values verbatim; serialization by the generator. TreefellowGuardian's 5%
// pack item.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TreefellowWood : Item
{
    [Constructible]
    public TreefellowWood()
        : base(0x1BDD)
    {
        Hue = 2425;
        Movable = true;
    }

    public override string DefaultName => "Treefellow Wood";
}
