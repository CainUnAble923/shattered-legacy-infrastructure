using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/FemaleGargishPlateArms.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [SerializationGenerator(0, false)]
    public partial class FemaleGargishPlateArms : BaseArmor
    {
        [Constructible]
        public FemaleGargishPlateArms(int hue = 0) : base(0x307)
        {
            Hue = hue;
        }

        public override double DefaultWeight => 5.0;
        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;
        public override int AosStrReq => 80;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
