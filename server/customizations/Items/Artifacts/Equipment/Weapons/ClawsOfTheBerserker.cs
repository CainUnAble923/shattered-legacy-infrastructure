using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/ClawsOfTheBerserker.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); WeaponAttributes.BattleLust = 1 (D-3).
    [SerializationGenerator(0, false)]
    public partial class ClawsOfTheBerserker : Tekagi
    {
        [Constructible]
        public ClawsOfTheBerserker()
        {
            Hue = 1172;
            WeaponAttributes.HitLightning = 45;
            WeaponAttributes.HitLowerDefend = 50;
            Attributes.CastSpeed = 1;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 60;
        }

        public override int LabelNumber => 1113758;
        public override int InitMinHits => 35;
        public override int InitMaxHits => 60;
    }
}
