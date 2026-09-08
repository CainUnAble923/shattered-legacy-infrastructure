using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Luck/LuneRouge.cs (CC9 batch 4).
    // ServUO derives from GoldRing and gets set/absorption state from BaseJewel. Here that state lives on
    // BaseSetJewel (S10), so the piece derives from that and reproduces stock GoldRing's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class LuneRouge : BaseSetJewel
    {
        [Constructible]
        public LuneRouge() : base(0x108a, Layer.Ring)
        {
            Hue = 1166;
            Attributes.Luck = 150;
            Attributes.AttackChance = 10;
            Attributes.WeaponDamage = 20;
            SetHue = 1166;
            SetAttributes.Luck = 100;
            SetAttributes.AttackChance = 10;
            SetAttributes.WeaponDamage = 20;
            SetAttributes.WeaponSpeed = 10;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenStam = 3;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1154372;
        public override SetItem SetID => SetItem.Luck2;
        public override int Pieces => 2;

        // Stock GoldRing members, reproduced because the parent changed.
        public override int BaseGemTypeNumber => 1044176; // star sapphire ring
    }
}
