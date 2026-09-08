using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtue/HumilityCloak.cs (CC9 batch 4).
    // ServUO derives from BaseClothing and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that instead; nothing else changes.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2B04, 0x2B05)]
    [SerializationGenerator(0, false)]
    public partial class HumilityCloak : BaseSetClothing
    {
        [Constructible]
        public HumilityCloak() : base(0x2B04, Layer.Cloak)
        {
            LootType = LootType.Blessed;
            SetHue = 0;
            Hue = 0x226;
            SetSelfRepair = 5;
            SetPhysicalBonus = 5;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 5;
            SetEnergyBonus = 5;
        }

        public override double DefaultWeight => 6.0;
        public override int LabelNumber => 1075195;
        public override SetItem SetID => SetItem.Virtue;
        public override int Pieces => 8;
        public override int InitMinHits => 0;
        public override int InitMaxHits => 0;
        public override int AosStrReq => 10;
    }
}
