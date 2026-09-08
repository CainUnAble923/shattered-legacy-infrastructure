using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/BritchesOfWarding.cs (CC9 batch 4). Two types in one file, as in ServUO.
    // ServUO derives from ChainLegs and gets absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock ChainLegs's own members.
    // [Flippable] is copied from ChainLegs: ModernUO reads it with inherit: false.
    // The random eater is rolled once in the constructor, exactly as ServUO does; it is persisted with the item.
    [Flippable(0x13be, 0x13c3)]
    [SerializationGenerator(0, false)]
    public partial class BritchesOfWarding : BaseSetArmor
    {
        [Constructible]
        public BritchesOfWarding() : base(0x13BE)
        {
            switch (Utility.Random(6))
            {
                case 0: AbsorptionAttributes.EaterKinetic = 9; break;
                case 1: AbsorptionAttributes.EaterFire = 9; break;
                case 2: AbsorptionAttributes.EaterCold = 9; break;
                case 3: AbsorptionAttributes.EaterPoison = 9; break;
                case 4: AbsorptionAttributes.EaterEnergy = 9; break;
                case 5: AbsorptionAttributes.EaterDamage = 9; break;
            }

            Attributes.BonusStam = 12;
            Attributes.AttackChance = 10;
            Attributes.LowerManaCost = 8;
        }

        public override int LabelNumber => 1157345; // britches of warding

        public override int BasePhysicalResistance => 20;
        public override int BaseFireResistance => 20;
        public override int BaseColdResistance => 20;
        public override int BasePoisonResistance => 20;
        public override int BaseEnergyResistance => 20;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock ChainLegs members, reproduced because the parent changed.
        public override double DefaultWeight => 7.0;
        public override int AosStrReq => 60;
        public override int OldStrReq => 20;
        public override int OldDexBonus => -3;
        public override int ArmorBase => 28;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Chainmail;
    }

    // GargishPlateLegs is already on BaseSetArmor (S10), so this is a plain derivation.
    [SerializationGenerator(0, false)]
    public partial class GargishBritchesOfWarding : GargishPlateLegs
    {
        [Constructible]
        public GargishBritchesOfWarding()
        {
            switch (Utility.Random(6))
            {
                case 0: AbsorptionAttributes.EaterKinetic = 9; break;
                case 1: AbsorptionAttributes.EaterFire = 9; break;
                case 2: AbsorptionAttributes.EaterCold = 9; break;
                case 3: AbsorptionAttributes.EaterPoison = 9; break;
                case 4: AbsorptionAttributes.EaterEnergy = 9; break;
                case 5: AbsorptionAttributes.EaterDamage = 9; break;
            }

            Attributes.BonusStam = 12;
            Attributes.AttackChance = 10;
            Attributes.LowerManaCost = 8;
        }

        public override int LabelNumber => 1157345; // britches of warding

        public override int BasePhysicalResistance => 20;
        public override int BaseFireResistance => 20;
        public override int BaseColdResistance => 20;
        public override int BasePoisonResistance => 20;
        public override int BaseEnergyResistance => 20;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
