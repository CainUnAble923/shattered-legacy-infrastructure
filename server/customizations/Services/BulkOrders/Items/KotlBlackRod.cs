// cc-P123: ported from ServUO pub57 Scripts/Services/BulkOrders/Items/KotlBlackRod.cs (the rod and its gargish twin). Not
// a reward itself: what a 2016 carpentry bulk-order recipe makes (DefCarpentry, ServUO :470). The recipe is F-43's, so
// nothing crafts it yet; staff can [add it. Its properties are ServUO's. Dropped: IsArtifact (D-1), as pinned's artifacts
// drop it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class KotlBlackRod : QuarterStaff
{
    [Constructible]
    public KotlBlackRod()
    {
        Hue = 1150;
        WeaponAttributes.MageWeapon = 30;
        Attributes.SpellChanneling = 1;
        Attributes.CastSpeed = 2;
        Attributes.LowerManaCost = 5;
        Attributes.LowerRegCost = 10;
    }

    public override int LabelNumber => 1156990; // kotl black rod

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}

[SerializationGenerator(0, false)]
public partial class GargishKotlBlackRod : GargishGnarledStaff
{
    [Constructible]
    public GargishKotlBlackRod()
    {
        Hue = 1150;
        WeaponAttributes.MageWeapon = 30;
        Attributes.SpellChanneling = 1;
        Attributes.CastSpeed = 2;
        Attributes.LowerManaCost = 5;
        Attributes.LowerRegCost = 10;
    }

    public override int LabelNumber => 1156994; // gargish kotl black rod

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
