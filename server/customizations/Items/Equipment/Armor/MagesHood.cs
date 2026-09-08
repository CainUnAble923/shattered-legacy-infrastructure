using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/MagesHood.cs (CC9 batch 3). A BaseHat (clothing) with resistances,
    // as in ServUO; the ()/(int hue) constructor pair is collapsed to one optional parameter.
    [SerializationGenerator(0, false)]
    public partial class MagesHood : BaseHat
    {
        [Constructible]
        public MagesHood(int hue = 0) : base(0xA411, hue) => StrRequirement = 10;

        public override double DefaultWeight => 3.0;

        public override int LabelNumber => 1159227; // mage's hood

        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 8;

        public override int InitMinHits => 20;
        public override int InitMaxHits => 40;
    }
}
