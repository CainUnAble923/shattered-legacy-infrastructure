using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/KelpWovenLeggings.cs (CC9 batch 4).
    // ServUO derives from LeatherLegs and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherLegs's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeatherLegs: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13cb, 0x13d2)]
    [SerializationGenerator(0, false)]
    public partial class KelpWovenLeggings : BaseSetArmor
    {
        [Constructible]
        public KelpWovenLeggings() : base(0x13CB)
        {
            Hue = 1155;
            AbsorptionAttributes.CastingFocus = 4;
            Attributes.BonusHits = 5;
            Attributes.BonusMana = 8;
            Attributes.RegenMana = 2;
            Attributes.SpellDamage = 8;
            Attributes.LowerRegCost = 15;
        }

        public override int LabelNumber => 1149960;
        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 13;
        public override int BaseColdResistance => 12;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 14;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock LeatherLegs members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override int AosStrReq => 20;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
