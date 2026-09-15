using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/AxesOfFury.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class AxesOfFury : DualShortAxes
    {
        [Constructible]
        public AxesOfFury()
        {
            Hue = 33;
            WeaponAttributes.HitFireball = 45;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.BonusDex = 5;
            Attributes.DefendChance = -15;
            Attributes.AttackChance = 20;
            Attributes.WeaponDamage = 45;
            Attributes.WeaponSpeed = 30;
        }

        public override int LabelNumber => 1113517;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
