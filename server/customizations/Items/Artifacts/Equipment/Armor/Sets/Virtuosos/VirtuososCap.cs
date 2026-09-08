using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososCap.cs (CC9 batch 4).
    // ServUO derives from JesterHat and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that and reproduces stock JesterHat's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    // BaseHat's IsShipwreckedItem field, its two shipwreck tooltip lines and its exceptional-craft bonus are not reproduced: an artifact hat is neither fished up nor crafted.
    [SerializationGenerator(0, false)]
    public partial class VirtuososCap : BaseSetClothing
    {
        [Constructible]
        public VirtuososCap() : base(0x171C, Layer.Helm)
        {
            Hue = 1374;
            StrRequirement = 10;
            SetHue = 1374;
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1151320;
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;
        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 23;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;
    }
}
