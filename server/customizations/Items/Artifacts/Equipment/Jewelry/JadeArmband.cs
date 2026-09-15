using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/JadeArmband.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class JadeArmband : GoldBracelet
    {
        [Constructible]
        public JadeArmband()
        {
            Hue = 2126;
            Attributes.AttackChance = 10;
            Attributes.DefendChance = 10;
            Attributes.WeaponSpeed = 5;
            Resistances.Poison = 20;
        }

        public override int LabelNumber => 1112407;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
    }
}
