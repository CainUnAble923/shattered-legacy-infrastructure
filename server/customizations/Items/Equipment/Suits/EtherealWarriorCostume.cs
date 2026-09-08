using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/EtherealWarriorCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class EtherealWarriorCostume : BaseCostume
    {
        [Constructible]
        public EtherealWarriorCostume()
        {
            CostumeBody = 123;
        }

        public override string CreatureName => "ethereal warrior";

        public override int LabelNumber => 1114243; // ethereal warrior costume
    }
}
