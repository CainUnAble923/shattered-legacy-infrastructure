using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/RoyalGuardInvestigatorCloak.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class RoyalGuardInvestigatorsCloak : Cloak
    {
        [Constructible]
        public RoyalGuardInvestigatorsCloak()
        {
            Hue = 1163;
            SkillBonuses.SetValues(0, SkillName.Stealth, 20.0);
        }

        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
        public override int LabelNumber => 1112409;
    }
}
