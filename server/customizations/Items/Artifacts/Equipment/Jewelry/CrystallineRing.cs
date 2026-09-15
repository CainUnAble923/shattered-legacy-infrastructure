using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/CrystallineRing.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class CrystallineRing : GoldRing
    {
        [Constructible]
        public CrystallineRing()
        {
            Hue = 0x480;
            Attributes.RegenHits = 5;
            Attributes.RegenMana = 3;
            Attributes.SpellDamage = 20;
            SkillBonuses.SetValues(0, SkillName.Magery, 20.0);
            SkillBonuses.SetValues(1, SkillName.Focus, 20.0);
        }

        public override int LabelNumber => 1075096;
    }
}
