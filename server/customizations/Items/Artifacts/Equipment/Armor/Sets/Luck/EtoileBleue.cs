using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Luck/EtoileBleue.cs (CC9 batch 4).
    // ServUO derives from GoldRing and gets set/absorption state from BaseJewel. Here that state lives on
    // BaseSetJewel (S10), so the piece derives from that and reproduces stock GoldRing's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class EtoileBleue : BaseSetJewel
    {
        [Constructible]
        public EtoileBleue() : base(0x108a, Layer.Ring)
        {
            Hue = 1165;
            Attributes.Luck = 150;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 1;
            SetHue = 1165;
            SetAttributes.Luck = 100;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenMana = 2;
            SetAttributes.CastSpeed = 1;
            SetAttributes.CastRecovery = 4;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1080238;
        public override SetItem SetID => SetItem.Luck;
        public override int Pieces => 2;

        // Stock GoldRing members, reproduced because the parent changed.
        public override int BaseGemTypeNumber => 1044176; // star sapphire ring
    }
}
