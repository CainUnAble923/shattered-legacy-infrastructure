using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/DemonHuntersStandard.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class DemonHuntersStandard : Spear
    {
        [Constructible]
        public DemonHuntersStandard()
        {
            Hue = 1377;
            Attributes.CastSpeed = 1;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 50;
            WeaponAttributes.HitLeechStam = 50;
            WeaponAttributes.HitLightning = 40;
            WeaponAttributes.HitLowerDefend = 30;
            Slayer = SlayerName.Exorcism;
        }

        public override int LabelNumber => 1113864;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
