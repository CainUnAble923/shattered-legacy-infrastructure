using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class TigerPeltBustier : BaseSetArmor
    {
        [Constructible]
        public TigerPeltBustier() : base(0x7823)
        {
        }

        public override double DefaultWeight => 6.0;

        public override int LabelNumber => 1109627; // Tiger Pelt Bustier

        public override int BasePhysicalResistance => 2;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 3;

        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;

        public override int AosStrReq => 25;
        public override int OldStrReq => 15;

        public override int ArmorBase => 13;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;

        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

        public override bool AllowMaleWearer => true;
    }
}
