using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/MinotaurCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class MinotaurCostume : BaseCostume
    {
        [Constructible]
        public MinotaurCostume()
        {
            CostumeBody = 263;
        }

        public override string CreatureName => "minotaur";

        public override int LabelNumber => 1114237; // minotaur costume
    }
}
