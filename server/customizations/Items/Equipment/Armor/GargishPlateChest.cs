using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishPlateChest.cs (CC9).
    [TypeAlias("Server.Items.MaleGargishPlateChest")]
    [SerializationGenerator(0, false)]
    public partial class GargishPlateChest : BaseArmor
    {
        [Constructible]
        public GargishPlateChest(int hue = 0) : base(0x30A) => Hue = hue;

        public override double DefaultWeight => 10.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 95;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
