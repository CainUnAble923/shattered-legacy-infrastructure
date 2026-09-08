using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/ResonantShieldOfVengeance.cs (CC9 batch 4). Two types in one file, as in ServUO.
    // ServUO derives from GargishWoodenShield / BronzeShield and gets absorption state from BaseArmor (through
    // BaseShield). Here that state lives on BaseSetShield (S10), so each piece derives from that and reproduces
    // its stock parent's own members. Both random rolls happen once in the constructor, exactly as ServUO does.
    // [Flippable] is copied from GargishWoodenShield: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x4200, 0x4207)]
    [SerializationGenerator(0, false)]
    public partial class ResonantShieldOfVengeance : BaseSetShield
    {
        [Constructible]
        public ResonantShieldOfVengeance() : base(0x4200)
        {
            Hue = 2076;

            switch (Utility.Random(5))
            {
                case 0: AbsorptionAttributes.ResonanceKinetic = 10; break;
                case 1: AbsorptionAttributes.ResonanceFire = 10; break;
                case 2: AbsorptionAttributes.ResonanceCold = 10; break;
                case 3: AbsorptionAttributes.ResonancePoison = 10; break;
                case 4: AbsorptionAttributes.ResonanceEnergy = 10; break;
            }

            Attributes.SpellChanneling = 1;
            Attributes.ReflectPhysical = 20;
            Attributes.DefendChance = 8;

            switch (Utility.Random(5))
            {
                case 0: PhysicalBonus = 10; break;
                case 1: FireBonus = 10; break;
                case 2: ColdBonus = 10; break;
                case 3: PoisonBonus = 10; break;
                case 4: EnergyBonus = 10; break;
            }
        }

        public override int LabelNumber => 1150357; // Resonant Shield of Vengeance
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock GargishWoodenShield members, reproduced because the parent changed.
        public override double DefaultWeight => 5.0;
        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 1;
        public override int AosStrReq => 20;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Wood;
    }

    [SerializationGenerator(0, false)]
    public partial class ResonantShieldOfVengeanceHuman : BaseSetShield
    {
        [Constructible]
        public ResonantShieldOfVengeanceHuman() : base(0x1B72)
        {
            Hue = 2076;

            switch (Utility.Random(5))
            {
                case 0: AbsorptionAttributes.ResonanceKinetic = 10; break;
                case 1: AbsorptionAttributes.ResonanceFire = 10; break;
                case 2: AbsorptionAttributes.ResonanceCold = 10; break;
                case 3: AbsorptionAttributes.ResonancePoison = 10; break;
                case 4: AbsorptionAttributes.ResonanceEnergy = 10; break;
            }

            Attributes.SpellChanneling = 1;
            Attributes.ReflectPhysical = 20;
            Attributes.DefendChance = 8;

            switch (Utility.Random(5))
            {
                case 0: PhysicalBonus = 10; break;
                case 1: FireBonus = 10; break;
                case 2: ColdBonus = 10; break;
                case 3: PoisonBonus = 10; break;
                case 4: EnergyBonus = 10; break;
            }
        }

        public override int LabelNumber => 1150357; // Resonant Shield of Vengeance
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock BronzeShield members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 1;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 0;
        public override int AosStrReq => 35;
        public override int ArmorBase => 10;
    }
}
