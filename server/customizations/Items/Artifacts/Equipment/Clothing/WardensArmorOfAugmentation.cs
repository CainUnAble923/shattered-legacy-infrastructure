using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/WardensArmorOfAugmentation.cs (CC9 batch 4).
    // ServUO derives from GargishLeatherWingArmor and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock GargishLeatherWingArmor's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from GargishLeatherWingArmor: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    // ServUO's GargishLeatherWingArmor is a BaseArmor despite the folder; so is ModernUO's. Armour carrier, not clothing.
    [Flippable(0x457E, 0x457F)]
    [SerializationGenerator(0, false)]
    public partial class WardensArmorOfAugmentation : BaseSetArmor
    {
        [Constructible]
        public WardensArmorOfAugmentation() : base(0x457E)
        {
            Hue = 0x9C2;
            AbsorptionAttributes.EaterKinetic = 5;
            Attributes.SpellDamage = 3;
            Attributes.LowerManaCost = 1;
            Attributes.WeaponSpeed = 5;
        }

        public override int LabelNumber => 1115515;

        // Stock GargishLeatherWingArmor members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
        public override int PhysicalResistance => 0;
        public override int FireResistance => 0;
        public override int ColdResistance => 0;
        public override int PoisonResistance => 0;
        public override int EnergyResistance => 0;
        public override int AosStrReq => 10;
        public override int OldStrReq => 10;
        public override int ArmorBase => 0;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
