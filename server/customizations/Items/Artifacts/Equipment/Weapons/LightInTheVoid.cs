using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/LightInTheVoid.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class LightInTheVoid : GargishTalwar
    {
        [Constructible]
        public LightInTheVoid()
        {
            Slayer = SlayerName.Silver;
            WeaponAttributes.HitLightning = 45;
            WeaponAttributes.HitLowerDefend = 30;
            Attributes.BonusStr = 8;
            Attributes.AttackChance = 10;
            Attributes.CastSpeed = 1;
            Attributes.WeaponSpeed = 20;
            Attributes.WeaponDamage = 35;
            Hue = 1072;
        }

        public override int LabelNumber => 1113521;
        public override int ArtifactRarity => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
