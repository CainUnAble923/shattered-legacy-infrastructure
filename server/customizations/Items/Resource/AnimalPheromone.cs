// ServUO: Items/Resource/AnimalPheromone.cs (cc-P119). Values verbatim; serialization by the generator. FireRabbit's drop.
// On ServUO it is also one resource of the Tinkering hitching post (DefTinkering.cs:606-609); neither pinned's
// DefTinkering nor ours has that recipe, so here it is a drop with no use yet (the Crafting gaps row).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AnimalPheromone : Item
{
    [Constructible]
    public AnimalPheromone()
        : base(0x182F)
    {
    }

    public override int LabelNumber => 1071200; // animal pheromone
}
