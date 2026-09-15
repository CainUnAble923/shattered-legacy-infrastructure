using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/IronwoodCompositeBow.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class IronwoodCompositeBow : CompositeBow
    {
        [Constructible]
        public IronwoodCompositeBow()
        {
            Hue = 1410;
            Slayer = SlayerName.Fey;
            WeaponAttributes.HitFireball = 40;
            WeaponAttributes.HitLowerDefend = 30;
            Attributes.BonusDex = 5;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 45;
            Velocity = 30;
        }

        public override int LabelNumber => 1113759;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
