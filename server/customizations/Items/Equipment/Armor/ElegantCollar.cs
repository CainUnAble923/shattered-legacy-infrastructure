using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/ElegantCollar.cs (CC9 batch 3). Leather armour on the neck layer.
    [SerializationGenerator(0, false)]
    public partial class ElegantCollar : BaseArmor
    {
        [Constructible]
        public ElegantCollar() : base(0xA40F) => Layer = Layer.Neck;

        public override double DefaultWeight => 3.0;

        public override int LabelNumber => 1159224; // elegant collar

        public override int BasePhysicalResistance => 2;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 3;

        public override int InitMinHits => 35;
        public override int InitMaxHits => 50;

        public override int AosStrReq => 30;

        public override int ArmorBase => 7;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
