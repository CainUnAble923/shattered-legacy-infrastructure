using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/CavalrysFolly.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class CavalrysFolly : BladedStaff
    {
        [Constructible]
        public CavalrysFolly()
        {
            Hue = 1165;
            Attributes.BonusHits = 2;
            Attributes.AttackChance = 10;
            Attributes.WeaponDamage = 45;
            Attributes.WeaponSpeed = 35;
            WeaponAttributes.HitLowerDefend = 40;
            WeaponAttributes.HitFireball = 40;
        }

        public override int LabelNumber => 1115446;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
