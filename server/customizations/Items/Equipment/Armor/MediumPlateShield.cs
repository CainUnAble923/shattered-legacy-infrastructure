using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/MediumPlateShield.cs (CC9 batch 3). Based off a MetalShield; ServUO
    // leaves the weight commented out and so does this.
    [Flippable(0x4203, 0x4209)]
    [SerializationGenerator(0, false)]
    public partial class MediumPlateShield : BaseShield
    {
        [Constructible]
        public MediumPlateShield() : base(0x4203)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 1;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 0;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 45;

        public override int ArmorBase => 11;
    }
}
