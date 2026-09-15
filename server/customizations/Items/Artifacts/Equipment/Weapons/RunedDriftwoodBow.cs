using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/RunedDriftwoodBow.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); Attributes.LowerAmmoCost = 15 (D-25: ModernUO keeps LowerAmmoCost on BaseQuiver, not in AosAttribute).
    [SerializationGenerator(0, false)]
    public partial class RunedDriftwoodBow : Bow
    {
        [Constructible]
        public RunedDriftwoodBow()
        {
            Hue = 2955;
            WeaponAttributes.HitLightning = 40;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
        }

        public override int LabelNumber => 1149961;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
