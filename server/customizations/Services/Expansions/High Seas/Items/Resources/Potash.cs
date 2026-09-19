// ServUO: Services/Expansions/High Seas/Items/Resources/Potash.cs (CC4 Shame). Ported for CaveTroll's pack items; a leaf resource with no other dependency.
// ICommodity here exposes DescriptionNumber rather than ServUO's TextDefinition Description.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Potash : Item, ICommodity
{
    [Constructible]
    public Potash(int amount = 1) : base(0x423A)
    {
        Stackable = true;
        Amount = amount;
        Hue = 1102;
    }

    public override int LabelNumber => 1116319; // potash

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
