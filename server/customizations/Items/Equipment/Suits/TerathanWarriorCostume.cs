using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/TerathanWarriorCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class TerathanWarriorCostume : BaseCostume
    {
        [Constructible]
        public TerathanWarriorCostume()
        {
            CostumeBody = 70;
        }

        public override string CreatureName => "terathan warrior";

        public override int LabelNumber => 1114228; // terathan warrior costume
    }
}
