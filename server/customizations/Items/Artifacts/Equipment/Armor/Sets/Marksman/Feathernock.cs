using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Marksman/Feathernock.cs (CC9 batch 4).
    // ServUO derives from BaseQuiver and gets set/absorption state from BaseQuiver. Here that state lives on
    // BaseSetQuiver (S10), so the piece derives from that instead; nothing else changes.
    // Dropped: IsArtifact (D-1).
    // Swiftflight, the other half of the Marksman set, is not ported (Q-041: Bow sits on BaseRanged, which no carrier can sit above), so the set bonus cannot complete yet.
    [SerializationGenerator(0, false)]
    public partial class Feathernock : BaseSetQuiver
    {
        [Constructible]
        public Feathernock() : base()
        {
            SetHue = 0x594;
            Attributes.WeaponDamage = 10;
            WeightReduction = 30;
            SetAttributes.AttackChance = 15;
            SetAttributes.BonusDex = 8;
            SetAttributes.WeaponSpeed = 30;
            SetAttributes.WeaponDamage = 20;
        }

        public override int LabelNumber => 1074324;
        public override SetItem SetID => SetItem.Marksman;
        public override int Pieces => 2;
    }
}
