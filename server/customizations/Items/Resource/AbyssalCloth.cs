// ServUO: Items/Resource/AbyssalCloth.cs (cc-P195): the cloth the Robe of Rite is sewn from. A leaf port for that
// recipe. ServUO drops it from imbuing resource loot (Services/LootGeneration/Imbuing/ResourceDrops.cs:140), which this
// shard has not ported, so it has no source here yet (notes/cc-P195-port-exodus-encounter.md). Pinned's ICommodity is
// DescriptionNumber (the recipe's shape); OnSingleClick is left to pinned's property list, as every pinned resource is.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AbyssalCloth : Item, ICommodity, IScissorable
{
    [Constructible]
    public AbyssalCloth(int amount = 1) : base(0x1767)
    {
        Stackable = true;
        Amount = amount;
        Hue = 2075;
    }

    public override int LabelNumber => 1113350; // abyssal cloth

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;

    public bool Scissor(Mobile from, Scissors scissors)
    {
        if (Deleted || !from.CanSee(this))
        {
            return false;
        }

        ScissorHelper(from, new Bandage(), 1);
        return true;
    }
}
