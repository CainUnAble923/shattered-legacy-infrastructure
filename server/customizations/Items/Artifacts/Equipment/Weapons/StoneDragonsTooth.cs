using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/StoneDragonsTooth.cs (CC9 batch 4).
    // Dropped: IsArtifact (D-1).
    // GargishDagger is on BaseSetWeapon (batch 4 re-parent, Q-040), so AbsorptionAttributes resolves.
    [SerializationGenerator(0, false)]
    public partial class StoneDragonsTooth : GargishDagger
    {
        [Constructible]
        public StoneDragonsTooth() : base()
        {
            Hue = 2407;
            Attributes.WeaponSpeed = 10;
            Attributes.WeaponDamage = 50;
            Attributes.RegenHits = 3;
            WeaponAttributes.HitMagicArrow = 40;
            WeaponAttributes.HitLowerDefend = 30;
            WeaponAttributes.ResistFireBonus = 10;
            AbsorptionAttributes.EaterPoison = 10;
            AosElementDamages.Poison = 100;
        }

        public override int LabelNumber => 1113523;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
