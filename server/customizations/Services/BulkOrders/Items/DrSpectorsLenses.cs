// cc-P123: ported from ServUO pub57 Scripts/Services/BulkOrders/Items/DrSpectorsLenses.cs. Not a reward itself: what a
// 2016 tinkering bulk-order recipe makes (DefTinkering, ServUO :766). The recipe is F-43's, so nothing crafts it yet; staff
// can [add it. Its properties are ServUO's. Dropped: IsArtifact (D-1), as pinned's artifacts drop it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DrSpectorsLenses : Glasses
{
    [Constructible]
    public DrSpectorsLenses()
    {
        Attributes.BonusInt = 8;
        Attributes.RegenMana = 4;
        Attributes.SpellDamage = 12;
        Attributes.LowerManaCost = 8;
        Attributes.LowerRegCost = 10;
    }

    public override int LabelNumber => 1156991; // Dr. Spector's lenses

    public override int BasePhysicalResistance => 5;
    public override int BaseFireResistance => 10;
    public override int BaseColdResistance => 14;
    public override int BasePoisonResistance => 20;
    public override int BaseEnergyResistance => 20;

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
