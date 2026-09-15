using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/TheRedeemer.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class TheRedeemer : PaladinSword
    {
        [Constructible]
        public TheRedeemer()
        {
            Hue = 2304;
            Slayer = SlayerName.Silver;
            Slayer2 = SlayerName.Exorcism;
            Attributes.WeaponDamage = 55;
        }

        public override int LabelNumber => 1077442;
        public override int ArtifactRarity => 7;
        public override int InitMinHits => 100;
        public override int InitMaxHits => 100;
        public override bool CanFortify => false;
    }
}
