using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/Mangler.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class Mangler : Broadsword
    {
        [Constructible]
        public Mangler()
        {
            Hue = 2001;
            WeaponAttributes.HitLeechMana = 50;
            Attributes.WeaponDamage = 50;
            Attributes.WeaponSpeed = 25;
            WeaponAttributes.HitHarm = 50;
            WeaponAttributes.UseBestSkill = 1;
            WeaponAttributes.HitLowerDefend = 30;
        }

        public override int LabelNumber => 1114842;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
