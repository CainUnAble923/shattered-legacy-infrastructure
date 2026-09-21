using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/LegacyOfDespair.cs (CC6 batch 5; a shared drop of FireElementalRenowned
    // and TikitaviRenowned). Stock DreadSword is its final parent: the B5 carrier regex hits nothing in it and it has no
    // ServUO descendants.
    // Dropped: IsArtifact (D-1); WeaponAttributes.HitCurse = 10 (D-73: pinned AosWeaponAttribute has no HitCurse member
    // and nothing implements the effect; P4's row).
    [SerializationGenerator(0, false)]
    public partial class LegacyOfDespair : DreadSword
    {
        [Constructible]
        public LegacyOfDespair()
        {
            Hue = 48;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 60;
            WeaponAttributes.HitLowerDefend = 50;
            WeaponAttributes.HitLowerAttack = 50;
            AosElementDamages.Cold = 75;
            AosElementDamages.Poison = 25;
        }

        public override int LabelNumber => 1113519; // Legacy of Despair

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
