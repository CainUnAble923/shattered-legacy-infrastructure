using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/StandardOfChaos.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class StandardOfChaos : DoubleBladedStaff
    {
        [Constructible]
        public StandardOfChaos()
        {
            Hue = 2209;
            WeaponAttributes.HitHarm = 30;
            WeaponAttributes.HitFireball = 20;
            WeaponAttributes.HitLightning = 10;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = -40;
            Attributes.CastSpeed = 1;
            AosElementDamages.Chaos = 100;
        }

        public override int LabelNumber => 1113522;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
