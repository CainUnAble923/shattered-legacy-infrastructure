using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishPlateLegs.cs (CC9).
    [TypeAlias("Server.Items.MaleGargishPlateLegs")]
    [SerializationGenerator(0, false)]
    public partial class GargishPlateLegs : BaseArmor
    {
        [Constructible]
        public GargishPlateLegs(int hue = 0) : base(0x30E) => Hue = hue;

        public override double DefaultWeight => 7.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 90;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
