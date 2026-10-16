// cc-P128: ServUO pub57 Scripts/Items/Resource/SilverSerpentVenom.cs -> ours, a forced leaf dependency of the basket
// weaving port: silver serpent venom, the first resource of colour fixative (Alchemy 75; Cooking the other way round).
// ServUO's silver serpent drops one on death (Mobiles/Normal/SilverSerpent.cs:72); pinned's silver serpent does not,
// and that creature is the creatures row's (P129) to bring level, so until then only staff can make one.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SilverSerpentVenom : Item, ICommodity
{
    [Constructible]
    public SilverSerpentVenom(int amount = 1) : base(0xE24)
    {
        Hue = 1155;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112173; // silver serpent venom

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
