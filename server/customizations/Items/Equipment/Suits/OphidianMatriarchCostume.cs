using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/OphidianMatriarchCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class OphidianMatriarchCostume : BaseCostume
    {
        [Constructible]
        public OphidianMatriarchCostume()
        {
            CostumeBody = 87;
        }

        public override string CreatureName => "ophidian matriarch";

        public override int LabelNumber => 1114230; // ophidian matriarch costume
    }
}
