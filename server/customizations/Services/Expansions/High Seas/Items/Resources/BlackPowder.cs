// ServUO: Services/Expansions/High Seas/Items/Resources/BlackPowder.cs (CC4 Shame). Ported for CaveTroll's pack items; a leaf resource with no other dependency.
// ICommodity here exposes DescriptionNumber rather than ServUO's TextDefinition Description.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlackPowder : Item, ICommodity
{
    [Constructible]
    public BlackPowder(int amount = 1) : base(0x423A)
    {
        Stackable = true;
        Amount = amount;
        Hue = 1109;
    }

    public override int LabelNumber => 1095826; // black powder

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
