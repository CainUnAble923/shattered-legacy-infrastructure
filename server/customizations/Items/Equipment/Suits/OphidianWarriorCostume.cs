using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/OphidianWarriorCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class OphidianWarriorCostume : BaseCostume
    {
        [Constructible]
        public OphidianWarriorCostume()
        {
            CostumeBody = 86;
        }

        public override string CreatureName => "ophidian warrior";

        public override int LabelNumber => 1114229; // ophidian warrior costume
    }
}
