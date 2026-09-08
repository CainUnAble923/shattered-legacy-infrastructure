using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Sorcerers/SorcererHat.cs (CC9 batch 4).
    // ServUO derives from WizardsHat and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that and reproduces stock WizardsHat's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    // BaseHat's IsShipwreckedItem field, its two shipwreck tooltip lines and its exceptional-craft bonus are not reproduced: an artifact hat is neither fished up nor crafted.
    [SerializationGenerator(0, false)]
    public partial class SorcererHat : BaseSetClothing
    {
        [Constructible]
        public SorcererHat() : base(0x1718, Layer.Helm)
        {
            Hue = 1165;
            Attributes.BonusInt = 1;
            Attributes.LowerRegCost = 10;
            SetAttributes.BonusInt = 6;
            SetAttributes.RegenMana = 2;
            SetAttributes.DefendChance = 10;
            SetAttributes.LowerManaCost = 5;
            SetAttributes.LowerRegCost = 40;
            SetHue = 1165;
            SetPhysicalBonus = 28;
            SetFireBonus = 28;
            SetColdBonus = 28;
            SetPoisonBonus = 28;
            SetEnergyBonus = 28;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1080465;
        public override SetItem SetID => SetItem.Sorcerer;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
