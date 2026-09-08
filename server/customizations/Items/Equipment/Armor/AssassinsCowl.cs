using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/AssassinsCowl.cs (CC9 batch 3). A BaseHat (clothing) with resistances,
    // as in ServUO; the ()/(int hue) constructor pair is collapsed to one optional parameter.
    [SerializationGenerator(0, false)]
    public partial class AssassinsCowl : BaseHat
    {
        [Constructible]
        public AssassinsCowl(int hue = 0) : base(0xA410, hue) => StrRequirement = 45;

        public override double DefaultWeight => 3.0;

        public override int LabelNumber => 1126024; // assassin's cowl

        public override int BasePhysicalResistance => 2;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 2;

        public override int InitMinHits => 40;
        public override int InitMaxHits => 60;
    }
}
