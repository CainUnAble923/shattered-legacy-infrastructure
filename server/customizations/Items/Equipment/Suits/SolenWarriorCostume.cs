using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/SolenWarriorCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class SolenWarriorCostume : BaseCostume
    {
        [Constructible]
        public SolenWarriorCostume()
        {
            CostumeBody = 782;
        }

        public override string CreatureName => "solen warrior";

        public override int LabelNumber => 1114231; // solen warrior costume
    }
}
