using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SolesOfProvidence : Sandals
    {
        [Constructible]
        public SolesOfProvidence() : base(1177)
        {
            Attributes.Luck = 80;
        }

        // ServUO overrides IsArtifact => true. ModernUO has no IsArtifact on BaseClothing, and
        // every ServUO consumer of it (RunicReforging, BasePigmentsOfTokuno, NaturalDye,
        // AlterItem) is itself absent here, so the override has nothing to affect. Omitted.

        public override int LabelNumber => 1113376; // Soles of Providence
    }
}
