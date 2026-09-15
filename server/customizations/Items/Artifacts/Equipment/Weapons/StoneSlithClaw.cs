using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/StoneSlithClaw.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class StoneSlithClaw : Cyclone
    {
        [Constructible]
        public StoneSlithClaw()
        {
            Hue = 1150;
            WeaponAttributes.HitHarm = 40;
            Slayer = SlayerName.DaemonDismissal;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 45;
        }

        public override int LabelNumber => 1112393;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
