using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososArmbands.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // GargishPlateArms (batch 3) is on BaseSetArmor, so the set members resolve.
    [SerializationGenerator(0, false)]
    public partial class VirtuososArmbands : GargishPlateArms
    {
        [Constructible]
        public VirtuososArmbands()
        {
            Hue = 1374;
            StrRequirement = 80;
            SetHue = 1374;
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1151558;
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;
        public override int BasePhysicalResistance => 24;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 9;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 9;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;
    }
}
