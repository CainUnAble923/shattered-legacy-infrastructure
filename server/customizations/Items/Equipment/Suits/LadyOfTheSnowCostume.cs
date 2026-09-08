using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/LadyOfTheSnowCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class LadyOfTheSnowCostume : BaseCostume
    {
        [Constructible]
        public LadyOfTheSnowCostume()
        {
            CostumeBody = 252;
        }

        public override string CreatureName => "lady of the snow";

        public override int LabelNumber => 1114241; // Lady of the Snow costume
    }
}
