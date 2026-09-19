// ServUO: Services/Expansions/High Seas/Items/Resources/Charcoal.cs (CC4 Shame). Ported for CaveTroll's pack items; a leaf resource with no other dependency.
// ICommodity here exposes DescriptionNumber rather than ServUO's TextDefinition Description.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Charcoal : Item, ICommodity
{
    [Constructible]
    public Charcoal(int amount = 1) : base(0x423A)
    {
        Stackable = true;
        Amount = amount;
        Hue = 1190;
    }

    public override int LabelNumber => 1116303; // charcoal

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
