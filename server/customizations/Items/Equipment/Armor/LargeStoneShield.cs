using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/LargeStoneShield.cs (CC9). Based off WoodenKiteShield; ServUO
    // leaves the weight commented out and inherits BaseShield's material, and so does this.
    [Flippable(0x4205, 0x420B)]
    [SerializationGenerator(0, false)]
    public partial class LargeStoneShield : BaseShield
    {
        [Constructible]
        public LargeStoneShield() : base(0x4205)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 1;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 20;

        public override int ArmorBase => 12;
    }
}
