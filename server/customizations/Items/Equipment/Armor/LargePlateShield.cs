using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/LargePlateShield.cs (CC9 batch 3). Based off a HeaterShield; ServUO
    // sets no weight and so does this.
    [Flippable(0x4204, 0x4208)]
    [SerializationGenerator(0, false)]
    public partial class LargePlateShield : BaseShield
    {
        [Constructible]
        public LargePlateShield() : base(0x4204)
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

        public override int AosStrReq => 90;

        public override int ArmorBase => 23;
    }
}
