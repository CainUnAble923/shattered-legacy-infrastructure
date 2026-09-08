using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishChaosShield.cs (CC9 batch 3). Unlike ServUO's ChaosShield (and
    // GargishOrderShield), ServUO's gargish chaos shield carries no guild Validate; ported as ServUO has it.
    [Flippable(0x4228, 0x4229)]
    [SerializationGenerator(0, false)]
    public partial class GargishChaosShield : BaseShield
    {
        [Constructible]
        public GargishChaosShield() : base(0x4228)
        {
        }

        public override double DefaultWeight => 5.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 1;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 0;

        public override int InitMinHits => 100;
        public override int InitMaxHits => 125;

        public override int AosStrReq => 95;

        public override int ArmorBase => 32;
    }
}
