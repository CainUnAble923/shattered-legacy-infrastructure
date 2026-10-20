// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/HelmOfVengence.cs -> ours, the High Seas
// fishing port. A Charybdis shared artifact: a norse helm. ServUO spells the type HelmOfVengence; kept.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class HelmOfVengence : NorseHelm
{
    [Constructible]
    public HelmOfVengence()
    {
        Hue = 2012;
        Attributes.RegenMana = 3;
        Attributes.ReflectPhysical = 30;
        Attributes.AttackChance = 7;
        Attributes.WeaponDamage = 10;
        Attributes.LowerManaCost = 8;
    }

    public override int LabelNumber => 1116621; // Helm of Vengeance

    public override int BasePhysicalResistance => 11;
    public override int BaseFireResistance => 10;
    public override int BaseColdResistance => 14;
    public override int BasePoisonResistance => 7;
    public override int BaseEnergyResistance => 8;

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
