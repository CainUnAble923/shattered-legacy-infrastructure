using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/WardedDemonboneBracers.cs (CC9 batch 4).
    // ServUO derives from BoneArms and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock BoneArms's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from BoneArms: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    // ServUO overrides the final resistances (PhysicalResistance etc.), not the Base* ones; kept as written, ModernUO's BaseArmor.PhysicalResistance is overridable.
    [Flippable(0x144e, 0x1453)]
    [SerializationGenerator(0, false)]
    public partial class WardedDemonboneBracers : BaseSetArmor
    {
        [Constructible]
        public WardedDemonboneBracers() : base(0x144E)
        {
            Hue = 0x2E2;
            AbsorptionAttributes.CastingFocus = 2;
            Attributes.RegenMana = 1;
            Attributes.LowerManaCost = 6;
            Attributes.LowerRegCost = 12;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1115775;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int PhysicalResistance => 6;
        public override int FireResistance => 8;
        public override int ColdResistance => 4;
        public override int PoisonResistance => 5;
        public override int EnergyResistance => 5;

        // Stock BoneArms members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 2;
        public override int BaseEnergyResistance => 4;
        public override int AosStrReq => 55;
        public override int OldStrReq => 40;
        public override int OldDexBonus => -2;
        public override int ArmorBase => 30;
        public override int RevertArmorBase => 4;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Bone;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
    }
}
