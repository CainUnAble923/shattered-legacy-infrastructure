using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososEarpieces.cs (CC9 batch 4).
    // GargishEarrings is already on BaseSetArmor (S10), so this is a plain derivation.
    [SerializationGenerator(0, false)]
    public partial class VirtuososEarpieces : GargishEarrings
    {
        [Constructible]
        public VirtuososEarpieces() : base()
        {
            Hue = 1374;
            SetHue = 1374;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1151557;
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;
        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 17;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;
    }
}
