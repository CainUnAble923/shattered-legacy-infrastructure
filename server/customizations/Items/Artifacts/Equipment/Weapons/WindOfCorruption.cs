using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/WindOfCorruption.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class WindOfCorruption : Cyclone
    {
        [Constructible]
        public WindOfCorruption()
        {
            WeaponAttributes.HitLeechStam = 40;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
            WeaponAttributes.HitLowerDefend = 40;
            AosElementDamages.Chaos = 100;
            Slayer = SlayerName.Fey;
            Hue = 1171;
        }

        public override int LabelNumber => 1150358;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Weapons/WindOfCorruption.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class WindOfCorruptionHuman : Bow
    {
        [Constructible]
        public WindOfCorruptionHuman()
        {
            WeaponAttributes.HitLeechStam = 40;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
            WeaponAttributes.HitLowerDefend = 40;
            AosElementDamages.Chaos = 100;
            Slayer = SlayerName.Fey;
            Hue = 1171;
        }

        public override int LabelNumber => 1150358;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
