// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/LeviathanHideBracers.cs -> ours, the High
// Seas fishing port. An Osiredon unique artifact: leather arms with Casting Focus 2.
//
// ServUO derives from LeatherArms and gets AbsorptionAttributes from BaseArmor. Here that state lives on BaseSetArmor
// (S1; AGENTS.md, the final-parent rule: a type that needs a carrier member goes on the carrier the first time it is
// ported), so the piece derives from that with SetID None and reproduces stock LeatherArms's own members, the way the
// Hunter set pieces do. [Flippable] is copied from LeatherArms.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x13cd, 0x13c5)]
[SerializationGenerator(0, false)]
public partial class LeviathanHideBracers : BaseSetArmor
{
    [Constructible]
    public LeviathanHideBracers() : base(0x13CD)
    {
        Hue = 1274;
        AbsorptionAttributes.CastingFocus = 2;
        Attributes.BonusInt = 6;
        Attributes.AttackChance = 5;
        Attributes.RegenStam = 2;
        Attributes.RegenMana = 2;
        Attributes.LowerManaCost = 8;
        Attributes.LowerRegCost = 10;
    }

    public override int LabelNumber => 1116619; // Leviathan Hide Bracers

    public override int BasePhysicalResistance => 7;
    public override int BaseFireResistance => 9;
    public override int BaseColdResistance => 10;
    public override int BasePoisonResistance => 13;
    public override int BaseEnergyResistance => 14;

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    // Stock LeatherArms members, reproduced because the parent changed.
    public override double DefaultWeight => 2.0;
    public override int AosStrReq => 20;
    public override int OldStrReq => 15;
    public override int ArmorBase => 13;
    public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
    public override CraftResource DefaultResource => CraftResource.RegularLeather;
    public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
}
