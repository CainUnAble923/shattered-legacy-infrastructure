// ServUO: Items/Resource/MiscMLResources.cs, class Putrefaction (CC6 batch 8, Part A; one extracted type per file).
// The one of BasePeerless.PackResources' six Mondain's Legacy resources pinned does not ship: pinned's
// Items/Resources/MiscMLResources.cs has Blight, Scourge, Taint, Corruption and Muculent and no Putrefaction.
//
// ICommodity as ServUO has it, in ModernUO's shape (DescriptionNumber, the recipe). Note the asymmetry it creates:
// pinned's five siblings are plain Items and cannot be deeded; this one can. Each file is faithful to its own source.
// ServUO's (amountFrom, amountTo) constructor is collapsed with the others, as the recipe does for every pair.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Putrefaction : Item, ICommodity
{
    [Constructible]
    public Putrefaction(int amount = 1) : base(0x3186)
    {
        Stackable = true;
        Amount = amount;
        Hue = 883;
    }

    [Constructible]
    public Putrefaction(int amountFrom, int amountTo) : this(Utility.RandomMinMax(amountFrom, amountTo))
    {
    }

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
