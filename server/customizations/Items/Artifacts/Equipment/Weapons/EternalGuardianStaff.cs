using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/EternalGuardianStaff.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class EternalGuardianStaff : GnarledStaff
    {
        [Constructible]
        public EternalGuardianStaff()
        {
            Hue = 95;
            SkillBonuses.SetValues(0, SkillName.Mysticism, 15.0);
            Attributes.SpellDamage = 10;
            Attributes.LowerManaCost = 5;
            Attributes.SpellChanneling = 1;
        }

        public override int LabelNumber => 1112443;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
