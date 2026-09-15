using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Spellbooks/ConjurersGrimoire.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ConjurersGrimoire : Spellbook
    {
        [Constructible]
        public ConjurersGrimoire()
        {
            Hue = 1157;
            Slayer = SlayerName.Silver;
            Attributes.LowerManaCost = 10;
            Attributes.BonusInt = 8;
            Attributes.SpellDamage = 15;
            SkillBonuses.SetValues(0, SkillName.Magery, 15.0);
        }

        public override int LabelNumber => 1094799;
    }
}
