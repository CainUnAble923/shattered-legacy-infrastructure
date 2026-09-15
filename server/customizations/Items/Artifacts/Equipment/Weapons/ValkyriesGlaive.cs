using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/ValkyriesGlaive.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ValkyriesGlaive : SoulGlaive
    {
        [Constructible]
        public ValkyriesGlaive()
        {
            Attributes.SpellChanneling = 1;
            Slayer = SlayerName.Silver;
            WeaponAttributes.HitFireball = 40;
            Attributes.BonusStr = 5;
            Attributes.WeaponSpeed = 20;
            Attributes.WeaponDamage = 20;
            Hue = 1651;
        }

        public override int LabelNumber => 1113531;
        public override int ArtifactRarity => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
