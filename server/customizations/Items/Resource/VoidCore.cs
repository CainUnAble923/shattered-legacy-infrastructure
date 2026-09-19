// ServUO: Items/Resource/VoidCore.cs (CC4 Shame). Ported for UnboundEnergyVortex's 20% drop; a leaf
// resource with no other dependency. ICommodity here exposes DescriptionNumber rather than ServUO's
// TextDefinition Description.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class VoidCore : Item, ICommodity
{
    [Constructible]
    public VoidCore(int amount = 1) : base(0x5728)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1113334; // void core
    public override double DefaultWeight => 0.1;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
